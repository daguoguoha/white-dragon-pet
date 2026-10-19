using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexGirl {
    public sealed class QuotaWindow {
        public double? Remaining;
        public int? Minutes;
        public DateTime? ResetUtc;
        public string Label(string fallback) {
            if (!Minutes.HasValue || Minutes.Value <= 0) return fallback;
            int m = Minutes.Value;
            if (m == 10080) return "每周";
            if (m % 1440 == 0) return (m / 1440) + "天";
            if (m % 60 == 0) return (m / 60) + "小时";
            return m + "分钟";
        }
        public string ResetText(DateTime utcNow) {
            if (!ResetUtc.HasValue) return "重置时间暂不可用";
            TimeSpan span = ResetUtc.Value - utcNow;
            if (span.TotalSeconds <= 0) return "等待服务端更新";
            if (span.TotalDays >= 1) return "重置 " + (int)span.TotalDays + "天 " + span.Hours + "小时";
            if (span.TotalHours >= 1) return "重置 " + (int)span.TotalHours + "小时 " + span.Minutes + "分";
            return "重置 " + Math.Max(1, (int)Math.Ceiling(span.TotalMinutes)) + "分钟";
        }
    }
    public sealed class QuotaBucket {
        public string Id;
        public string Name;
        public QuotaWindow Primary;
        public QuotaWindow Secondary;
        public override string ToString() { return Name; }
    }
    public sealed class QuotaSnapshot {
        public DateTime CapturedUtc;
        public List<QuotaBucket> Buckets = new List<QuotaBucket>();
    }
    public static class QuotaData {
        public static Dictionary<string, object> Object(object value) { return value as Dictionary<string, object>; }
        public static object Get(Dictionary<string, object> map, string key) {
            object value; return map != null && map.TryGetValue(key, out value) ? value : null;
        }
        public static double? Number(object value) {
            if (value == null || value is bool) return null;
            double number;
            if (!Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) || Double.IsNaN(number) || Double.IsInfinity(number)) return null;
            return number;
        }
        static QuotaWindow Window(object value) {
            var map = Object(value);
            var result = new QuotaWindow();
            double? used = Number(Get(map, "usedPercent"));
            if (used.HasValue) result.Remaining = Math.Max(0, Math.Min(100, 100 - used.Value));
            double? minutes = Number(Get(map, "windowDurationMins"));
            if (minutes.HasValue && minutes.Value > 0 && minutes.Value <= Int32.MaxValue) result.Minutes = (int)minutes.Value;
            double? reset = Number(Get(map, "resetsAt"));
            if (reset.HasValue) {
                try { result.ResetUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(reset.Value); }
                catch (ArgumentOutOfRangeException) { }
            }
            return result;
        }
        static QuotaBucket Bucket(Dictionary<string, object> map, string fallback) {
            string id = Convert.ToString(Get(map, "limitId"));
            if (String.IsNullOrWhiteSpace(id)) id = fallback;
            string name = Convert.ToString(Get(map, "limitName"));
            if (String.IsNullOrWhiteSpace(name)) name = id == "codex" ? "Codex" : id;
            return new QuotaBucket { Id = id, Name = name, Primary = Window(Get(map, "primary")), Secondary = Window(Get(map, "secondary")) };
        }
        public static QuotaSnapshot Parse(string json, DateTime capturedUtc) {
            var root = Object(new JavaScriptSerializer().DeserializeObject(json));
            if (root == null) throw new InvalidDataException("Invalid quota response");
            var result = new QuotaSnapshot { CapturedUtc = capturedUtc };
            var multiple = Object(Get(root, "rateLimitsByLimitId"));
            if (multiple != null && multiple.Count > 0) {
                foreach (var item in multiple.OrderBy(x => x.Key == "codex" ? "" : x.Key, StringComparer.Ordinal)) {
                    var map = Object(item.Value);
                    if (map != null) result.Buckets.Add(Bucket(map, item.Key));
                }
            } else {
                var single = Object(Get(root, "rateLimits"));
                if (single != null) result.Buckets.Add(Bucket(single, "codex"));
            }
            return result;
        }
    }

    // Only initialize and account/rateLimits/read are sent. No turns, logins or billing mutations.
    public sealed class QuotaClient : IDisposable {
        readonly object gate = new object();
        readonly object requestsGate = new object();
        readonly Dictionary<int, TaskCompletionSource<Dictionary<string, object>>> pending = new Dictionary<int, TaskCompletionSource<Dictionary<string, object>>>();
        Process process;
        int nextId;
        bool disposed;
        readonly string executable;
        readonly string workingDirectory;
        readonly string codexDirectory;
        public QuotaClient(string exe, string work, string home) { executable = exe; workingDirectory = work; codexDirectory = home; }
        public static string FindCodex() {
            string custom = Environment.GetEnvironmentVariable("CODEX_GIRL_CODEX_EXE");
            if (!String.IsNullOrWhiteSpace(custom)) {
                if (!Path.IsPathRooted(custom) || !File.Exists(custom) || !custom.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new FileNotFoundException("Invalid CODEX_GIRL_CODEX_EXE");
                return custom;
            }
            var candidates = new List<string>();
            foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) {
                try { string path = Path.Combine(folder.Trim('"'), "codex.exe"); if (File.Exists(path)) candidates.Add(path); } catch (ArgumentException) { }
            }
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
            if (Directory.Exists(root)) candidates.AddRange(Directory.GetDirectories(root).Select(d => Path.Combine(d, "codex.exe")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc));
            if (candidates.Count > 0) return candidates[0];
            throw new FileNotFoundException("Codex CLI was not found. Install or update Codex desktop, or set CODEX_GIRL_CODEX_EXE to codex.exe.");
        }
        void Send(object message) {
            lock (gate) {
                if (disposed || process == null || process.HasExited) throw new IOException("Codex server stopped");
                process.StandardInput.WriteLine(new JavaScriptSerializer().Serialize(message));
                process.StandardInput.Flush();
            }
        }
        void OnLine(object sender, DataReceivedEventArgs e) {
            if (!Object.ReferenceEquals(sender, process)) return;
            if (String.IsNullOrWhiteSpace(e.Data)) return;
            try {
                var msg = QuotaData.Object(new JavaScriptSerializer().DeserializeObject(e.Data));
                var id = QuotaData.Number(QuotaData.Get(msg, "id"));
                if (!id.HasValue || QuotaData.Get(msg, "method") != null) return;
                TaskCompletionSource<Dictionary<string, object>> waiter;
                lock (gate) { if (!pending.TryGetValue((int)id.Value, out waiter)) return; }
                waiter.TrySetResult(msg);
            } catch (ArgumentException) { } catch (InvalidOperationException) { }
        }
        Dictionary<string, object> Request(string method, object args) {
            int id;
            var waiter = new TaskCompletionSource<Dictionary<string, object>>();
            lock (gate) { id = ++nextId; pending.Add(id, waiter); }
            try {
                Send(new { id = id, method = method, @params = args });
                if (!waiter.Task.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Quota request timed out");
                var msg = waiter.Task.Result;
                if (QuotaData.Get(msg, "error") != null) throw new IOException("Codex rejected quota request; check login and connectivity");
                var result = QuotaData.Object(QuotaData.Get(msg, "result"));
                if (result == null) throw new InvalidDataException("Invalid server response");
                return result;
            } finally { lock (gate) { pending.Remove(id); } }
        }
        void EnsureStarted() {
            if (process != null && !process.HasExited) return;
            CloseProcess();
            var info = new ProcessStartInfo(executable, "app-server --stdio") {
                WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false)
            };
            info.EnvironmentVariables["CODEX_HOME"] = codexDirectory;
            process = new Process { StartInfo = info };
            process.OutputDataReceived += OnLine;
            // Drain stderr without storing auth diagnostics or account data on disk.
            process.ErrorDataReceived += delegate { };
            process.EnableRaisingEvents = true;
            process.Exited += delegate(object sender, EventArgs args) {
                lock (gate) { if (Object.ReferenceEquals(sender, process)) foreach (var waiter in pending.Values) waiter.TrySetException(new IOException("Codex server stopped")); }
            };
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            Request("initialize", new { clientInfo = new { name = "codex_girl_quota", title = "Codex Girl Quota", version = "1.2.0" } });
            Send(new { method = "initialized", @params = new { } });
        }
        public QuotaSnapshot Read() {
            lock (requestsGate) {
                if (disposed) throw new ObjectDisposedException("QuotaClient");
                try {
                    EnsureStarted();
                    var result = Request("account/rateLimits/read", new { });
                    return QuotaData.Parse(new JavaScriptSerializer().Serialize(result), DateTime.UtcNow);
                } catch { CloseProcess(); throw; }
            }
        }
        void CloseProcess() {
            Process old;
            lock (gate) { old = process; process = null; }
            if (old == null) return;
            try { old.StandardInput.Close(); if (!old.WaitForExit(800)) old.Kill(); }
            catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            finally { old.Dispose(); }
        }
        public void Dispose() { disposed = true; CloseProcess(); }
    }
}
