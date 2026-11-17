using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace DebateAI
{
    internal enum LicenseTier { None, Free, Creator, Pro, Owner }

    internal sealed class LicenseInfo
    {
        public LicenseTier Tier;
        public string Licensee = "", Error;
        public DateTime? Expires;
        public readonly List<string> Machines = new List<string>();
        public bool Valid { get { return Error == null && Tier != LicenseTier.None; } }

        public string Describe()
        {
            if (!Valid) return Error ?? "Keine Lizenz";
            string tier = Tier == LicenseTier.Owner ? "Eigentümer · alle Funktionen dauerhaft frei" : Tier.ToString();
            return tier + " · " + Licensee + (Expires.HasValue ? " · gültig bis " + Expires.Value.ToString("dd.MM.yyyy") : "");
        }
    }

    // Signed licences: payload.signature (Base64URL). The client only holds the PUBLIC key, so a licence
    // can neither be forged nor edited. Owner licences are bound to listed PCs (machine IDs).
    // Note: any purely local check can be removed by patching the program; paid cloud features
    // must therefore additionally be enforced by the server (planned subscription backend).
    internal static class Licensing
    {
        // Public key of the owner (RSA, XML). Generated with LicenseTool; the private key never leaves the owner's PC.
        // Empty until the owner has generated a key pair and published the public part.
        public const string PublicKeyXml = "";

        public static string File { get { return Path.Combine(LocalData.Folder, "license.key"); } }

        public static string MachineId()
        {
            string guid = "";
            try
            {
                using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    if (key != null) guid = key.GetValue("MachineGuid") as string ?? "";
            }
            catch (Exception) { }
            if (guid.Length == 0) guid = Environment.MachineName;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("DEBATE-AI|" + guid.ToLowerInvariant()));
                return BitConverter.ToString(hash, 0, 10).Replace("-", "");
            }
        }

        public static LicenseInfo Load()
        {
            try
            {
                if (!System.IO.File.Exists(File)) return new LicenseInfo { Error = "Keine Lizenz hinterlegt." };
                return Verify(System.IO.File.ReadAllText(File).Trim(), PublicKeyXml, MachineId(), DateTime.UtcNow);
            }
            catch (Exception) { return new LicenseInfo { Error = "Lizenzdatei nicht lesbar." }; }
        }

        public static LicenseInfo Import(string token)
        {
            LicenseInfo info = Verify((token ?? "").Trim(), PublicKeyXml, MachineId(), DateTime.UtcNow);
            if (!info.Valid) return info;
            Directory.CreateDirectory(LocalData.Folder);
            System.IO.File.WriteAllText(File, token.Trim());
            return info;
        }

        public static LicenseInfo Verify(string token, string publicKeyXml, string machine, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(publicKeyXml)) return new LicenseInfo { Error = "Lizenzprüfung noch nicht eingerichtet (kein öffentlicher Schlüssel in dieser Version)." };
            string[] parts = (token ?? "").Split('.');
            if (parts.Length != 2) return new LicenseInfo { Error = "Ungültiges Lizenzformat." };
            byte[] payload, signature;
            try { payload = Base64Url(parts[0]); signature = Base64Url(parts[1]); }
            catch (FormatException) { return new LicenseInfo { Error = "Ungültiges Lizenzformat." }; }
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(publicKeyXml);
                bool ok;
                try { ok = rsa.VerifyData(payload, CryptoConfig.MapNameToOID("SHA256"), signature); }
                catch (CryptographicException) { ok = false; }
                if (!ok) return new LicenseInfo { Error = "Lizenzsignatur ungültig (verändert oder nicht vom Eigentümer ausgestellt)." };
            }
            Dictionary<string, object> data;
            try { data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(payload)); }
            catch (Exception) { return new LicenseInfo { Error = "Lizenzinhalt unlesbar." }; }
            var info = new LicenseInfo { Licensee = LocalData.Text(data, "licensee", "") };
            LicenseTier tier;
            if (!Enum.TryParse(LocalData.Text(data, "tier", ""), true, out tier) || tier == LicenseTier.None) return new LicenseInfo { Error = "Unbekannter Lizenztyp." };
            info.Tier = tier;
            string expires = LocalData.Text(data, "expires", "");
            if (expires.Length != 0)
            {
                DateTime until;
                if (!DateTime.TryParse(expires, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out until))
                    return new LicenseInfo { Error = "Ablaufdatum unlesbar." };
                info.Expires = until;
                if (nowUtc > until) return new LicenseInfo { Error = "Lizenz abgelaufen am " + until.ToString("dd.MM.yyyy") + "." };
            }
            object machines;
            if (data.TryGetValue("machines", out machines) && machines is System.Collections.ArrayList)
                foreach (object id in (System.Collections.ArrayList)machines) if (id is string) info.Machines.Add(((string)id).ToUpperInvariant());
            if (tier == LicenseTier.Owner && info.Machines.Count == 0) return new LicenseInfo { Error = "Eigentümer-Lizenzen müssen an PCs gebunden sein." };
            if (info.Machines.Count != 0 && !info.Machines.Contains((machine ?? "").ToUpperInvariant()))
                return new LicenseInfo { Error = "Diese Lizenz gilt nicht für diesen PC (Geräte-ID " + machine + ")." };
            return info;
        }

        public static string Sign(Dictionary<string, object> payload, string privateKeyXml)
        {
            byte[] data = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(payload));
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(privateKeyXml);
                return Base64Url(data) + "." + Base64Url(rsa.SignData(data, CryptoConfig.MapNameToOID("SHA256")));
            }
        }

        public static string Base64Url(byte[] data) { return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }

        public static byte[] Base64Url(string text)
        {
            string padded = text.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4) { case 2: padded += "=="; break; case 3: padded += "="; break; case 1: throw new FormatException(); }
            return Convert.FromBase64String(padded);
        }

        // Today every feature is free (there are no paid tiers yet). Once tiers exist, this is the single gate.
        public static bool Allows(LicenseInfo license, string feature)
        {
            if (license != null && license.Valid && license.Tier == LicenseTier.Owner) return true;
            return true;
        }
    }
}
