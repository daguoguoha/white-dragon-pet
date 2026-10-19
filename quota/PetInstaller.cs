using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace CodexGirl {
    public sealed class PetInstallResult {
        public string DirectoryPath;
        public string BackupPath;
        public bool AlreadyInstalled;
    }
    public static class PetInstaller {
        public const string DisplayName = "Codex娘";
        public const string FolderName = "codex-girl";
        static readonly string[] Files = { "spritesheet.png", "pet.json" };

        static void Regular(string path, bool directory) {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            FileAttributes a = File.GetAttributes(path);
            if ((a & FileAttributes.ReparsePoint) != 0 || (((a & FileAttributes.Directory) != 0) != directory)) {
                throw new IOException("宠物目录中存在链接或同名文件，请检查安装目录。");
            }
        }
        static string Hash(byte[] bytes) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)); }
        static bool SameManifest(string path) {
            if (!File.Exists(path)) return false;
            try {
                if (new FileInfo(path).Length > 128 * 1024) return false;
                var m = QuotaData.Object(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path, Encoding.UTF8)));
                return Convert.ToString(QuotaData.Get(m, "displayName")) == DisplayName
                    && Convert.ToString(QuotaData.Get(m, "spritesheetPath")) == "spritesheet.png"
                    && QuotaData.Number(QuotaData.Get(m, "spriteVersionNumber")) == 2;
            } catch (ArgumentException) { return false; }
        }
        static byte[] ReadSheet(string sheet) {
            var info = new FileInfo(sheet);
            if (!info.Exists || info.Length > 20 * 1024 * 1024 || info.Length < 24) throw new InvalidDataException("找不到有效宠物素材，请完整解压安装包。");
            byte[] bytes = File.ReadAllBytes(sheet);
            if (BitConverter.ToString(bytes, 0, 8) != "89-50-4E-47-0D-0A-1A-0A"
                || BitConverter.ToString(bytes, 16, 8) != "00-00-06-00-00-00-08-F0") throw new InvalidDataException("宠物素材应为 1536 × 2288 的 v2 PNG。");
            using (var input = new MemoryStream(bytes, false)) {
                var image = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (image.Frames.Count != 1 || image.Frames[0].PixelWidth != 1536 || image.Frames[0].PixelHeight != 2288) throw new InvalidDataException("宠物素材无法解码，请重新解压安装包。");
            }
            return bytes;
        }
        static void PublishFile(string source, string destination) {
            if (File.Exists(destination)) File.Replace(source, destination, null);
            else File.Move(source, destination);
        }
        public static PetInstallResult Import(string sheet, string codexHome) {
            // Validate and read the input before creating or modifying any pet files.
            byte[] sprite = ReadSheet(sheet);
            if (String.IsNullOrWhiteSpace(codexHome) || !Path.IsPathRooted(codexHome)) throw new ArgumentException("无法确定 Codex 目录，请检查 CODEX_HOME 设置。");
            string home = Path.GetFullPath(codexHome);
            string pets = Path.Combine(home, "pets");
            string target = Path.Combine(pets, FolderName);
            Regular(home, true); Regular(pets, true); Regular(target, true);
            foreach (string name in Files) Regular(Path.Combine(target, name), false);
            string sheetTarget = Path.Combine(target, "spritesheet.png");
            if (SameManifest(Path.Combine(target, "pet.json")) && File.Exists(sheetTarget)
                && new FileInfo(sheetTarget).Length == sprite.Length && Hash(File.ReadAllBytes(sheetTarget)) == Hash(sprite)) {
                return new PetInstallResult { DirectoryPath = target, AlreadyInstalled = true };
            }
            byte[] manifest = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new {
                id = FolderName, displayName = DisplayName,
                description = "白发紫瞳的 Q 版白龙少女，含九种动画和十六向视线。",
                spriteVersionNumber = 2, spritesheetPath = "spritesheet.png"
            }));
            string stage = Path.Combine(pets, ".codex-girl-import-" + Guid.NewGuid().ToString("N"));
            var existing = new HashSet<string>(Files.Where(name => File.Exists(Path.Combine(target, name))));
            var changed = new List<string>();
            string backup = null;
            try {
                Directory.CreateDirectory(stage);
                File.WriteAllBytes(Path.Combine(stage, "spritesheet.png"), sprite);
                File.WriteAllBytes(Path.Combine(stage, "pet.json"), manifest);
                if (existing.Count > 0) {
                    string backupRoot = Path.Combine(home, "pet-backups"); Regular(backupRoot, true);
                    backup = Path.Combine(backupRoot, "codex-girl-import-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(backup);
                    foreach (string name in existing) File.Copy(Path.Combine(target, name), Path.Combine(backup, name));
                }
                Directory.CreateDirectory(target);
                try {
                    // Publish the manifest last so refresh cannot discover a half-written new pet.
                    foreach (string name in Files) {
                        PublishFile(Path.Combine(stage, name), Path.Combine(target, name)); changed.Add(name);
                    }
                } catch {
                    foreach (string name in changed.AsEnumerable().Reverse()) {
                        string destination = Path.Combine(target, name);
                        if (existing.Contains(name)) File.Copy(Path.Combine(backup, name), destination, true);
                        else File.Delete(destination);
                    }
                    throw;
                }
                return new PetInstallResult { DirectoryPath = target, BackupPath = backup, AlreadyInstalled = false };
            } finally {
                // This unique staging directory contains only these two files; never delete recursively.
                foreach (string name in Files) { string p = Path.Combine(stage, name); if (File.Exists(p)) File.Delete(p); }
                if (Directory.Exists(stage)) Directory.Delete(stage, false);
            }
        }
    }
}
