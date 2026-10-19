using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexGirl {
    public sealed class ChatConfiguration {
        public string Endpoint = "", Model = "", Key = "";
        public static Uri ValidateEndpoint(string endpoint) {
            Uri uri;
            if (!Uri.TryCreate(endpoint.Trim().TrimEnd('/') + "/chat/completions", UriKind.Absolute, out uri) ||
                (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) || !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("请填写 HTTPS 接口根地址，例如 https://服务地址/v1；本机服务可使用 HTTP。");
            return uri;
        }
        public static ChatConfiguration Load(string directory) {
            var result = new ChatConfiguration();
            try {
                var d = QuotaData.Object(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(directory, "chat.json"))));
                result.Endpoint = Convert.ToString(QuotaData.Get(d, "endpoint")); result.Model = Convert.ToString(QuotaData.Get(d, "model"));
                string encrypted = Convert.ToString(QuotaData.Get(d, "protectedKey"));
                if (!String.IsNullOrEmpty(encrypted)) result.Key = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser));
            } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { } catch (CryptographicException) { } catch (FormatException) { }
            return result;
        }
        public void Save(string directory) {
            ValidateEndpoint(Endpoint); if (String.IsNullOrWhiteSpace(Model)) throw new ArgumentException("请填写模型名称。");
            if (Key.Contains("\r") || Key.Contains("\n")) throw new ArgumentException("密钥格式不正确。");
            string encrypted = String.IsNullOrEmpty(Key) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(Key), null, DataProtectionScope.CurrentUser));
            WriteJson(Path.Combine(directory, "chat.json"), new { endpoint = Endpoint.Trim().TrimEnd('/'), model = Model.Trim(), protectedKey = encrypted });
        }
        public static void WriteJson(string path, object value) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temp, new JavaScriptSerializer().Serialize(value), new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    public static class CompanionNetwork {
        public static async Task<string> Request(Uri uri, string key, string body, CancellationToken token) {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(uri); request.AllowAutoRedirect = false;
            request.Timeout = 20000; request.ReadWriteTimeout = 20000; request.UserAgent = "CodexGirlPet/1.3";
            if (!String.IsNullOrEmpty(key)) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + key;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                timeout.CancelAfter(20000);
                using (timeout.Token.Register(request.Abort)) {
                    if (body != null) {
                        byte[] data = Encoding.UTF8.GetBytes(body); request.Method = "POST"; request.ContentType = "application/json; charset=utf-8"; request.ContentLength = data.Length;
                        using (Stream stream = await request.GetRequestStreamAsync()) await stream.WriteAsync(data, 0, data.Length, timeout.Token);
                    }
                    using (var response = (HttpWebResponse)await request.GetResponseAsync()) {
                        int code = (int)response.StatusCode;
                        if (code < 200 || code >= 300) throw new InvalidOperationException("服务返回状态 " + code + "，请检查接口设置。");
                        using (Stream stream = response.GetResponseStream()) using (var result = new MemoryStream()) {
                            byte[] buffer = new byte[8192]; int count;
                            while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token)) > 0) {
                                if (result.Length + count > 1024 * 1024) throw new InvalidDataException("响应过大。");
                                result.Write(buffer, 0, count);
                            }
                            return Encoding.UTF8.GetString(result.ToArray());
                        }
                    }
                }
            }
        }
        public static string ErrorMessage(Exception ex) {
            var web = ex as WebException;
            if (web != null) { var response = web.Response as HttpWebResponse; if (response != null) { int code = (int)response.StatusCode; response.Dispose(); return "请求失败（HTTP " + code + "），请检查接口、密钥和模型。"; } }
            if (ex is ArgumentException) return ex.Message;
            return "暂时无法连接服务，请检查网络或配置后重试。";
        }
        public static async Task<string> Chat(ChatConfiguration config, List<Dictionary<string, string>> history, string text, CancellationToken token) {
            var messages = new List<Dictionary<string, string>>();
            messages.Add(new Dictionary<string, string> { { "role", "system" }, { "content", "你是Codex娘，一位温柔俏皮的白发紫瞳小龙桌面伙伴。用中文简短自然地回复，通常30字以内。不要声称能操作电脑或知道系统状态。" } });
            messages.AddRange(history); messages.Add(new Dictionary<string, string> { { "role", "user" }, { "content", text } });
            string body = new JavaScriptSerializer().Serialize(new { model = config.Model, messages = messages, max_tokens = 160, stream = false });
            string raw = await Request(ChatConfiguration.ValidateEndpoint(config.Endpoint), config.Key, body, token);
            var root = QuotaData.Object(new JavaScriptSerializer().DeserializeObject(raw)); var choices = QuotaData.Get(root, "choices") as object[];
            if (choices == null || choices.Length == 0) throw new InvalidDataException("回复格式无法识别。");
            var message = QuotaData.Object(QuotaData.Get(QuotaData.Object(choices[0]), "message")); string reply = Convert.ToString(QuotaData.Get(message, "content")).Trim();
            if (reply.Length == 0) throw new InvalidDataException("回复为空。");
            history.Add(new Dictionary<string, string> { { "role", "user" }, { "content", text } }); history.Add(new Dictionary<string, string> { { "role", "assistant" }, { "content", reply } });
            while (history.Count > 40) history.RemoveRange(0, 2);
            return reply;
        }
        public static async Task<string> Weather(string city, CancellationToken token) {
            string raw = await Request(new Uri("https://wttr.in/" + Uri.EscapeDataString(city) + "?format=j1&lang=zh"), "", null, token);
            var root = QuotaData.Object(new JavaScriptSerializer().DeserializeObject(raw)); var conditions = QuotaData.Get(root, "current_condition") as object[];
            if (conditions == null || conditions.Length == 0) throw new InvalidDataException();
            var current = QuotaData.Object(conditions[0]); var descriptions = (QuotaData.Get(current, "lang_zh") ?? QuotaData.Get(current, "weatherDesc")) as object[];
            string description = descriptions != null && descriptions.Length > 0 ? Convert.ToString(QuotaData.Get(QuotaData.Object(descriptions[0]), "value")) : "天气暂无描述";
            return city + " · " + Convert.ToString(QuotaData.Get(current, "temp_C")) + "℃ · " + description + "\n体感 " + Convert.ToString(QuotaData.Get(current, "FeelsLikeC")) + "℃ · 湿度 " + Convert.ToString(QuotaData.Get(current, "humidity")) + "%\n查询时间 " + DateTime.Now.ToString("HH:mm") + " · 数据来自 wttr.in";
        }
    }
    public sealed class SystemMonitor {
        public double? CpuPercent { get; private set; }
        public double MemoryPercent { get; private set; }
        ulong previousIdle, previousTotal;
        public void Sample() {
            ulong idle, kernel, user;
            if (GetSystemTimes(out idle, out kernel, out user)) {
                ulong total = kernel + user;
                if (previousTotal != 0 && total > previousTotal) CpuPercent = Math.Max(0, Math.Min(100, 100.0 * (1 - (double)(idle - previousIdle) / (total - previousTotal))));
                previousIdle = idle; previousTotal = total;
            }
            var mem = new MemoryStatus(); mem.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)); if (GlobalMemoryStatusEx(ref mem)) MemoryPercent = mem.Load;
        }
        public string Read() { Sample(); return "CPU " + (CpuPercent.HasValue ? CpuPercent.Value.ToString("0") + "%" : "采样中") + " · 内存 " + MemoryPercent.ToString("0") + "%\nGPU 温度暂不可用"; }
        [StructLayout(LayoutKind.Sequential)] struct MemoryStatus { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, ExtendedVirtual; }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    }
}
