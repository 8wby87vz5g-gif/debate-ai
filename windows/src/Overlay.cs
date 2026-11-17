using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace DebateAI
{
    // What the stream overlay shows. Built on the UI thread, read by the overlay server.
    internal sealed class OverlayState
    {
        public string Name = "Nova", Topic = "", Phase = "idle", Mode = "";
        public double Level;     // 0..1, NOVA's own output level while she speaks
        public bool Running;

        public static string PhaseFor(Phase phase)
        {
            switch (phase)
            {
                case DebateAI.Phase.Speaking: return "speaking";
                case DebateAI.Phase.Thinking: return "thinking";
                case DebateAI.Phase.Recording: return "guest";
                case DebateAI.Phase.Listening: case DebateAI.Phase.Cooldown: case DebateAI.Phase.WaitingForQuiet: return "listening";
                case DebateAI.Phase.Connecting: return "connecting";
                case DebateAI.Phase.Paused: case DebateAI.Phase.Moderated: return "paused";
                default: return "idle";
            }
        }

        public static double LevelFromDb(double db) { return Math.Max(0, Math.Min(1, (db + 50) / 40)); }

        public string ToJson()
        {
            return new JavaScriptSerializer().Serialize(new Dictionary<string, object>
            {
                { "name", Name ?? "" }, { "topic", Topic ?? "" }, { "phase", Phase ?? "idle" }, { "mode", Mode ?? "" },
                { "level", Math.Round(Level, 3) }, { "running", Running }
            });
        }
    }

    // Tiny HTTP server for the TikTok LIVE Studio "Link" source. Listens on 127.0.0.1 only,
    // answers GET requests for the overlay page and its state, nothing else.
    internal sealed class OverlayServer : IDisposable
    {
        public const int DefaultPort = 18190;
        private readonly TcpListener listener;
        private readonly Thread thread;
        private volatile string state = new OverlayState().ToJson();
        private volatile bool stopped;

        public int Port { get; private set; }
        public string Url { get { return "http://127.0.0.1:" + Port + "/nova"; } }

        public OverlayServer(int port)
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(16);
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            thread = new Thread(Loop) { IsBackground = true, Name = "Overlay" };
            thread.Start();
        }

        public void Update(OverlayState next) { state = next.ToJson(); }

        private void Loop()
        {
            while (!stopped)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch (Exception) { if (stopped) return; Thread.Sleep(50); continue; }
                ThreadPool.QueueUserWorkItem(delegate { Serve(client); });
            }
        }

        private void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 3000; client.SendTimeout = 3000;
                    NetworkStream stream = client.GetStream();
                    string head = ReadHead(stream);
                    byte[] response = Respond(head, state);
                    stream.Write(response, 0, response.Length);
                }
                catch (Exception) { }
            }
        }

        private static string ReadHead(Stream stream)
        {
            var data = new MemoryStream();
            var buffer = new byte[1024];
            while (data.Length < 8192)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                data.Write(buffer, 0, read);
                string text = Encoding.ASCII.GetString(data.ToArray());
                if (text.Contains("\r\n\r\n")) return text;
            }
            return Encoding.ASCII.GetString(data.ToArray());
        }

        // Pure function so it can be tested without sockets.
        internal static byte[] Respond(string head, string stateJson)
        {
            string[] lines = (head ?? "").Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] request = lines[0].Split(' ');
            if (request.Length < 2 || request[0] != "GET") return Reply(405, "text/plain", "Nur GET");
            // Only pages opened as 127.0.0.1/localhost may read the state (no access from websites via DNS tricks).
            string host = "";
            foreach (string line in lines) if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) host = line.Substring(5).Trim().ToLowerInvariant();
            if (!(host.StartsWith("127.0.0.1") || host.StartsWith("localhost"))) return Reply(403, "text/plain", "Nur lokal");
            string path = request[1];
            int query = path.IndexOf('?');
            if (query >= 0) path = path.Substring(0, query);
            if (path == "/" || path == "/nova") return Reply(200, "text/html; charset=utf-8", OverlayPage.Html);
            if (path == "/state") return Reply(200, "application/json; charset=utf-8", stateJson);
            return Reply(404, "text/plain", "Nicht gefunden");
        }

        private static byte[] Reply(int code, string type, string body)
        {
            byte[] content = Encoding.UTF8.GetBytes(body);
            string status = code == 200 ? "OK" : code == 403 ? "Forbidden" : code == 404 ? "Not Found" : "Method Not Allowed";
            string header = "HTTP/1.1 " + code + " " + status + "\r\nContent-Type: " + type + "\r\nContent-Length: " + content.Length +
                "\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n";
            byte[] head = Encoding.ASCII.GetBytes(header);
            var all = new byte[head.Length + content.Length];
            Buffer.BlockCopy(head, 0, all, 0, head.Length);
            Buffer.BlockCopy(content, 0, all, head.Length, content.Length);
            return all;
        }

        public void Dispose()
        {
            stopped = true;
            try { listener.Stop(); } catch (Exception) { }
        }
    }
}
