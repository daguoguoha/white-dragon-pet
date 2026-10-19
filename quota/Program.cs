using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;

namespace CodexGirl {
    public static class Program {
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
                MessageBox.Show("无法启动额度宠物。请确认安装包已完整解压，Windows 已安装 .NET Framework 4.8。", "Codex娘 Quota"); return 1;
            }
        }
    }
}
