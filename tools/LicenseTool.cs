using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DebateAI
{
    // The tool is built without the app, so it brings its own minimal error type.
    internal sealed class AppError : Exception { public AppError(string message) : base(message) { } }

    // Owner tool, runs only on the owner's PC. The private key file must never be committed or shared.
    internal static class LicenseTool
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 2 && args[0] == "keygen")
                {
                    if (File.Exists(args[1])) { Console.Error.WriteLine("Datei existiert bereits: " + args[1]); return 1; }
                    using (var rsa = new RSACryptoServiceProvider(3072))
                    {
                        rsa.PersistKeyInCsp = false;
                        File.WriteAllText(args[1], rsa.ToXmlString(true));
                        Console.WriteLine("Privater Schlüssel gespeichert: " + args[1] + "  (GEHEIM HALTEN, nie hochladen)");
                        Console.WriteLine("Öffentlicher Schlüssel (für Licensing.PublicKeyXml):");
                        Console.WriteLine(rsa.ToXmlString(false));
                    }
                    return 0;
                }
                if (args.Length >= 4 && args[0] == "owner")
                {
                    var payload = new Dictionary<string, object>();
                    payload["licensee"] = args[2];
                    payload["tier"] = "Owner";
                    payload["issued"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    payload["machines"] = args[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine(Licensing.Sign(payload, File.ReadAllText(args[1])));
                    return 0;
                }
                Console.WriteLine("LicenseTool keygen <privat.xml>");
                Console.WriteLine("LicenseTool owner <privat.xml> \"Name\" <Geräte-ID>[,<Geräte-ID>…]");
                return 1;
            }
            catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        }
    }
}
