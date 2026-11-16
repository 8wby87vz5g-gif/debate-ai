using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DebateAI
{
    internal static class LocalData
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DebateAI");
        private static readonly string SecretFile = Path.Combine(Folder, "api-key.dpapi");
        public static readonly string LogFile = Path.Combine(Folder, "debate-ai.log");

        public static bool HasKey { get { return File.Exists(SecretFile); } }

        public static string LoadKey()
        {
            if (!HasKey) throw new AppError("Zuerst den OpenAI-API-Schlüssel in der App speichern.");
            byte[] clear = null;
            try
            {
                clear = ProtectedData.Unprotect(File.ReadAllBytes(SecretFile), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clear);
            }
            catch (CryptographicException) { throw new AppError("Der gespeicherte Schlüssel lässt sich in diesem Windows-Konto nicht öffnen. Bitte neu speichern."); }
            catch (IOException) { throw new AppError("Der gespeicherte Schlüssel ist nicht lesbar. Bitte neu speichern."); }
            catch (UnauthorizedAccessException) { throw new AppError("Windows verweigert den Zugriff auf den gespeicherten Schlüssel."); }
            finally { if (clear != null) Array.Clear(clear, 0, clear.Length); }
        }

        public static void SaveKey(string key)
        {
            key = key.Trim();
            if (!key.StartsWith("sk-", StringComparison.Ordinal) || key.Length < 20 || key.Length > 1024)
                throw new AppError("Bitte einen gültigen OpenAI-Projekt-API-Schlüssel eingeben.");
            foreach (char c in key)
                if (char.IsWhiteSpace(c) || char.IsControl(c)) throw new AppError("Der Schlüssel enthält Leerzeichen oder Zeilenumbrüche.");

            Directory.CreateDirectory(Folder);
            string temp = SecretFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] clear = Encoding.UTF8.GetBytes(key);
            try
            {
                byte[] encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(temp, encrypted);
                if (File.Exists(SecretFile)) File.Replace(temp, SecretFile, null);
                else File.Move(temp, SecretFile);
            }
            finally
            {
                Array.Clear(clear, 0, clear.Length);
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        public static void DeleteKey() { File.Delete(SecretFile); }

        public static bool WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var info = new FileInfo(LogFile);
                if (info.Exists && info.Length >= 256 * 1024)
                {
                    File.Delete(LogFile + ".1");
                    File.Move(LogFile, LogFile + ".1");
                }
                string line = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + " | " + message;
                File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
