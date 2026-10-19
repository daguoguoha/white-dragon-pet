using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexGirl {
    public sealed class PetImportWindow : Window {
        readonly TextBlock message;
        readonly Button import;
        readonly Button settings;
        readonly Button close;
        bool importing;
        static Button ActionButton(string label) { return new Button { Content = label, Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(8, 0, 0, 0), MinWidth = 80 }; }
        public PetImportWindow(string sheet, string codexHome) {
            Title = "导入到 Codex Pet"; Width = 490; SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Microsoft YaHei UI"); Background = new SolidColorBrush(Color.FromRgb(252, 250, 255));
            var root = new StackPanel { Margin = new Thickness(24) }; Content = root;
            root.Children.Add(new TextBlock { Text = "把 Codex娘导入宠物列表", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(78, 67, 94)), Margin = new Thickness(0, 0, 0, 12) });
            root.Children.Add(new TextBlock { Text = "导入后，在 Codex「设置 → 宠物」点击刷新，再选择「Codex娘」。重复导入会复用同一个条目。", TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0, 0, 0, 12) });
            root.Children.Add(new TextBlock { Text = "原生宠物显示动画，实时额度仍在本程序的独立窗口中显示。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 16) });
            message = new TextBlock { Text = "准备就绪。导入只安装宠物素材，不会切换当前宠物。", TextWrapping = TextWrapping.Wrap, FontSize = 12, MinHeight = 44, Margin = new Thickness(0, 0, 0, 16) }; root.Children.Add(message);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            import = ActionButton("导入"); import.IsDefault = true;
            settings = ActionButton("打开 Codex 设置"); settings.IsEnabled = false;
            close = ActionButton("关闭"); close.IsCancel = true;
            actions.Children.Add(import); actions.Children.Add(settings); actions.Children.Add(close); root.Children.Add(actions);
            close.Click += delegate { Close(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (importing) e.Cancel = true; };
            settings.Click += delegate {
                try { Process.Start(new ProcessStartInfo("codex://settings") { UseShellExecute = true }); }
                catch (Exception) { message.Text = "无法自动打开设置，请手动进入 Codex「设置 → 宠物」并点击刷新。"; }
            };
            import.Click += delegate {
                importing = true; import.IsEnabled = false; close.IsEnabled = false; message.Text = "正在安装宠物…";
                Task.Factory.StartNew(delegate {
                    PetInstallResult result = null; Exception error = null;
                    try { result = PetInstaller.Import(sheet, codexHome); } catch (Exception ex) { error = ex; }
                    Dispatcher.BeginInvoke(new Action(delegate {
                        importing = false; close.IsEnabled = true;
                        if (error == null) {
                            message.Text = (result.AlreadyInstalled ? "Codex娘已安装，无需重复导入。" : "已成功导入 Codex娘。") + "\n请打开 Codex 设置，在「宠物」中点击刷新并选择她。";
                            settings.IsEnabled = true; import.Content = "已导入";
                        } else {
                            message.Text = "导入失败：" + error.Message + "\n请检查目录权限或关闭占用素材的程序后重试。";
                            import.IsEnabled = true;
                        }
                    }));
                });
            };
        }
    }
}
