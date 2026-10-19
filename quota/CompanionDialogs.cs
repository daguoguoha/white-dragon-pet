using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexGirl {
    static class CompanionUI {
        public static StackPanel Setup(Window window, string title, double width) {
            window.Title = title; window.Width = width; window.SizeToContent = SizeToContent.Height; window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ResizeMode = ResizeMode.NoResize; window.Background = new SolidColorBrush(Color.FromRgb(250, 247, 255)); window.FontFamily = new FontFamily("Microsoft YaHei UI");
            var stack = new StackPanel { Margin = new Thickness(22) }; window.Content = stack; return stack;
        }
        public static void Label(Panel panel, string text) { panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 10, 0, 5), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray }); }
        public static TextBox Field(Panel panel, string value) { var box = new TextBox { Text = value, Padding = new Thickness(7), MaxLength = 2000 }; panel.Children.Add(box); return box; }
        public static Button Button(Panel panel, string label) { var button = new Button { Content = label, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 12, 0, 0) }; panel.Children.Add(button); return button; }
    }
    public sealed class CompanionChatWindow : Window {
        readonly string directory;
        readonly TextBox endpoint, model, log, input;
        readonly PasswordBox password;
        readonly TextBlock status;
        readonly Button send, save;
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly List<Dictionary<string, string>> history = new List<Dictionary<string, string>>();
        ChatConfiguration config;
        bool closed, busy;
        public CompanionChatWindow(string dataDirectory) {
            directory = dataDirectory; config = ChatConfiguration.Load(directory);
            var panel = CompanionUI.Setup(this, "和 Codex娘聊天", 490);
            var settings = new StackPanel(); var expander = new Expander { Header = "AI 接口设置", Content = settings, IsExpanded = String.IsNullOrEmpty(config.Endpoint) }; panel.Children.Add(expander);
            CompanionUI.Label(settings, "兼容 OpenAI 的接口根地址（包含 /v1，如服务要求）"); endpoint = CompanionUI.Field(settings, config.Endpoint); endpoint.MaxLength = 500;
            CompanionUI.Label(settings, "模型名称"); model = CompanionUI.Field(settings, config.Model); model.MaxLength = 200;
            CompanionUI.Label(settings, "API Key（仅保存在本机，使用 Windows 当前账户加密）"); password = new PasswordBox { Password = config.Key, Padding = new Thickness(7), MaxLength = 1000 }; settings.Children.Add(password);
            save = CompanionUI.Button(settings, "保存设置"); save.Click += delegate { SaveConfiguration(); };
            CompanionUI.Label(panel, "发送时会将输入及本窗口最近 40 条消息交给你配置的服务，可能产生 API 费用；聊天记录不写入磁盘。");
            log = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 190, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(9), Text = "Codex娘：我在这里，想聊些什么？", Background = Brushes.White }; panel.Children.Add(log);
            input = CompanionUI.Field(panel, ""); input.Margin = new Thickness(0, 10, 0, 0); input.ToolTip = "输入内容，点击发送";
            send = CompanionUI.Button(panel, "发送"); send.Click += async delegate {
                if (busy || String.IsNullOrWhiteSpace(input.Text)) return;
                if (!SaveConfiguration()) return;
                string message = input.Text.Trim(); input.Clear(); Append("你", message); busy = true; send.IsEnabled = save.IsEnabled = false; status.Text = "正在回复…";
                try { string reply = await CompanionNetwork.Chat(config, history, message, cancellation.Token); if (!closed) { Append("Codex娘", reply); status.Text = ""; } }
                catch (Exception ex) { if (!closed) status.Text = CompanionNetwork.ErrorMessage(ex); }
                finally { if (!closed) { busy = false; send.IsEnabled = save.IsEnabled = true; } }
            };
            status = new TextBlock { Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray }; panel.Children.Add(status);
            Closed += delegate { closed = true; cancellation.Cancel(); };
        }
        bool SaveConfiguration() {
            try {
                var incoming = new ChatConfiguration { Endpoint = endpoint.Text.Trim(), Model = model.Text.Trim(), Key = password.Password.Trim() };
                incoming.Save(directory);
                if (incoming.Endpoint != config.Endpoint || incoming.Model != config.Model || incoming.Key != config.Key) history.Clear();
                config = incoming; status.Text = "设置已保存。"; return true;
            } catch (ArgumentException ex) { status.Text = ex.Message; }
            catch (Exception) { status.Text = "设置保存失败，请检查当前账户的文件权限。"; }
            return false;
        }
        void Append(string name, string text) { if (log.Text.Length > 24000) log.Text = log.Text.Substring(log.Text.Length - 12000); log.AppendText("\n\n" + name + "：" + text); log.ScrollToEnd(); }
    }
    public sealed class WeatherWindow : Window {
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        bool closed;
        public WeatherWindow(string directory) {
            var panel = CompanionUI.Setup(this, "Codex娘 · 天气", 390);
            CompanionUI.Label(panel, "输入城市后点击查询，城市名将发送给 wttr.in；不会读取你的定位。");
            string saved = ""; try { saved = File.ReadAllText(Path.Combine(directory, "weather-city.txt")); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            var city = CompanionUI.Field(panel, saved); city.MaxLength = 80;
            var query = CompanionUI.Button(panel, "查询天气");
            var result = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), FontSize = 14 }; panel.Children.Add(result);
            query.Click += async delegate {
                string value = city.Text.Trim(); if (value.Length == 0) { result.Text = "请先填写城市。"; return; }
                query.IsEnabled = false; result.Text = "正在查询…";
                try {
                    string weather = await CompanionNetwork.Weather(value, cancellation.Token);
                    if (!closed) {
                        result.Text = weather;
                        try { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "weather-city.txt"), value); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                } catch (Exception) { if (!closed) result.Text = "天气查询失败，请稍后重试或换用城市拼音。"; }
                finally { if (!closed) query.IsEnabled = true; }
            };
            Closed += delegate { closed = true; cancellation.Cancel(); };
        }
    }
}
