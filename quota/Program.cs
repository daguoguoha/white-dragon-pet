using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows;

namespace CodexGirl {
    public static class Program {
        static string BundledSheet(string dataDirectory) {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("spritesheet.png")) {
                if (stream == null) throw new InvalidDataException("Missing embedded pet asset");
                byte[] bytes;
                using (var buffer = new MemoryStream()) { stream.CopyTo(buffer); bytes = buffer.ToArray(); }
                string digest;
                using (var hash = SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                string directory = Path.Combine(dataDirectory, "bundled"); Directory.CreateDirectory(directory);
                string destination = Path.Combine(directory, "spritesheet-" + digest + ".png");
                bool valid = false;
                if (File.Exists(destination)) using (var hash = SHA256.Create()) valid = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(destination))).Replace("-", "").ToLowerInvariant() == digest;
                if (!valid) File.WriteAllBytes(destination, bytes);
                return destination;
            }
        }
        [STAThread]
        public static int Main(string[] args) {
            try {
                string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
                string sheet = Path.Combine(root, "spritesheet.png");
                if (!File.Exists(sheet)) sheet = Path.Combine(root, "pet", "spritesheet.png");
                if (!File.Exists(sheet)) sheet = Path.Combine(root, "assets", "spritesheet.png");
                string codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
                if (String.IsNullOrWhiteSpace(codexHome) && File.Exists(Path.Combine(root, "pet.json"))) codexHome = Directory.GetParent(Directory.GetParent(root).FullName).FullName;
                if (String.IsNullOrWhiteSpace(codexHome)) codexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
                string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexGirlQuota");
                if (args.Length == 3 && args[0] == "--preview") data = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), "preview-data");
                if (!File.Exists(sheet)) sheet = BundledSheet(data);
                if (args.Length == 3 && args[0] == "--preview") {
                    QuotaPet.Preview(sheet, Path.GetFullPath(args[1]), File.ReadAllText(args[2]), false); return 0;
                }
                if (args.Length == 1 && args[0] == "--read-quota") {
                    using (var client = new QuotaClient(QuotaClient.FindCodex(), root, codexHome)) Console.WriteLine(new JavaScriptSerializer().Serialize(client.Read()));
                    return 0;
                }
                QuotaPet.Run(sheet, data, codexHome); return 0;
            } catch (Exception) {
                if (args.Length > 0) return 1;
                MessageBox.Show("无法启动 Codex娘。请确认 Windows 已安装 .NET Framework 4.8，且当前用户目录可以写入。", "Codex娘"); return 1;
            }
        }
    }
}
