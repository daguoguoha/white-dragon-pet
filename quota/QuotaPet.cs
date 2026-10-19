using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CodexGirl {
    public sealed partial class QuotaPetWindow : Window {
        readonly string prefsPath;
        readonly string codexDirectory;
        readonly string workDirectory;
        readonly string sheetPath;
        readonly Image pet = new Image();
        readonly TextBlock status;
        readonly TextBlock firstTitle;
        readonly TextBlock firstValue;
        readonly TextBlock firstReset;
        readonly ProgressBar firstBar;
        readonly TextBlock secondTitle;
        readonly TextBlock secondValue;
        readonly TextBlock secondReset;
        readonly ProgressBar secondBar;
        readonly ComboBox buckets = new ComboBox();
        readonly Border card;
        readonly Grid layout;
        readonly MenuItem quotaMenu;
        readonly List<BitmapSource> idle = new List<BitmapSource>();
        readonly DispatcherTimer animation = new DispatcherTimer();
        readonly DispatcherTimer heartbeat = new DispatcherTimer();
        readonly Brush violet = BrushOf("#9683D0");
        readonly Brush muted = BrushOf("#AAA5B6");
        readonly int[] idleTiming = { 280, 110, 110, 140, 140, 320 };
        QuotaSnapshot snapshot;
        QuotaClient client;
        bool busy;
        volatile bool closed;
        bool prefsDirty;
        bool failed;
        int failures;
        DateTime nextPoll = DateTime.MinValue;
        DateTime lastAttempt = DateTime.MinValue;
        double scale = 1;
        bool quotaVisible = true;

        static Brush BrushOf(string hex) { var b = (Brush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
        static TextBlock Text(string text, double size, string color) {
            return new TextBlock { Text = text, FontSize = size, Foreground = BrushOf(color), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        }
        static Button SmallButton(string text) {
            return new Button { Content = text, Background = Brushes.Transparent, Foreground = BrushOf("#80768E"), BorderThickness = new Thickness(0), Padding = new Thickness(6, 2, 6, 2), Cursor = Cursors.Hand, FontSize = 13 };
        }
        public QuotaPetWindow(string sheet, string dataDirectory, string codexHome) {
            prefsPath = Path.Combine(dataDirectory, "settings.json");
            workDirectory = dataDirectory;
            codexDirectory = codexHome;
            sheetPath = sheet;
            Title = "Codex娘 · Quota";
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; ShowInTaskbar = true; Topmost = true;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            layout = new Grid { Width = 470, Height = 286, Background = Brushes.Transparent };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(204) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(258) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            pet.Width = 192; pet.Height = 208; pet.VerticalAlignment = VerticalAlignment.Bottom;
            pet.Margin = new Thickness(0, 0, 0, 16); pet.Stretch = Stretch.Uniform;
            RenderOptions.SetBitmapScalingMode(pet, BitmapScalingMode.HighQuality);
            pet.Cursor = Cursors.SizeAll;
            pet.ToolTip = "拖动移动 · 双击显示或隐藏额度 · 右键设置";
            layout.Children.Add(pet);
            var source = new BitmapImage(); source.BeginInit(); source.CacheOption = BitmapCacheOption.OnLoad; source.UriSource = new Uri(Path.GetFullPath(sheet)); source.EndInit(); source.Freeze();
            if (source.PixelWidth != 1536 || source.PixelHeight != 2288) throw new InvalidDataException("Expected v2 pet spritesheet");
            for (int x = 0; x < 6; x++) idle.Add(Crop(source, x, 0));
            pet.Source = idle[0];

            card = new Border { Background = BrushOf("#FCFAFF"), BorderBrush = BrushOf("#E3DDEE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Padding = new Thickness(18, 14, 18, 12), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(card, 1); layout.Children.Add(card);
            var content = new StackPanel(); card.Child = content;
            var heading = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 8) };
            var close = SmallButton("×"); close.ToolTip = "退出"; close.Click += delegate { Close(); }; DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
            var hide = SmallButton("−"); hide.ToolTip = "隐藏额度卡片；双击宠物可重新显示"; hide.Click += delegate { SetQuotaVisible(false); }; DockPanel.SetDock(hide, Dock.Right); heading.Children.Add(hide);
            var refresh = SmallButton("↻"); refresh.ToolTip = "刷新额度"; refresh.Click += delegate { Refresh(true); }; DockPanel.SetDock(refresh, Dock.Right); heading.Children.Add(refresh);
            var title = Text("Codex娘", 17, "#4E435E"); title.FontWeight = FontWeights.SemiBold; heading.Children.Add(title); content.Children.Add(heading);
            status = Text("正在读取额度…", 10.5, "#8C819B"); status.Margin = new Thickness(0, 0, 0, 12); content.Children.Add(status);

            AddMeter(content, out firstTitle, out firstValue, out firstBar, out firstReset);
            AddMeter(content, out secondTitle, out secondValue, out secondBar, out secondReset);
            buckets.FontSize = 10; buckets.Foreground = BrushOf("#80768E"); buckets.Margin = new Thickness(0, 1, 0, 0); buckets.MaxHeight = 24;
            buckets.Visibility = Visibility.Collapsed; buckets.SelectionChanged += delegate { UpdateLabels(); }; content.Children.Add(buckets);
            var importPet = SmallButton("导入到 Codex Pet");
            importPet.Margin = new Thickness(0, 5, 0, 0); importPet.HorizontalAlignment = HorizontalAlignment.Stretch;
            importPet.ToolTip = "将这只宠物添加到 Codex 的宠物列表";
            importPet.Click += delegate { OpenImport(); }; content.Children.Add(importPet);
            Content = layout;
            Left = Math.Max(SystemParameters.VirtualScreenLeft, SystemParameters.WorkArea.Right - 490);
            Top = Math.Max(SystemParameters.VirtualScreenTop, SystemParameters.WorkArea.Bottom - 310);
            LoadPrefs(); ApplyScale();
            var menu = new ContextMenu();
            quotaMenu = new MenuItem { Header = "显示额度卡片", IsCheckable = true, IsChecked = quotaVisible };
            quotaMenu.Click += delegate { SetQuotaVisible(quotaMenu.IsChecked); }; menu.Items.Add(quotaMenu);
            AddItem(menu, "立即刷新", delegate { Refresh(true); });
            AddItem(menu, "导入到 Codex Pet", OpenImport);
            var sizes = new MenuItem { Header = "显示大小" };
            foreach (double option in new double[] { .8, 1, 1.25, 1.5 }) {
                double selected = option;
                var item = new MenuItem { Header = (option * 100).ToString(CultureInfo.InvariantCulture) + "%" };
                item.Click += delegate { scale = selected; ApplyScale(); SavePrefs(); }; sizes.Items.Add(item);
            }
            menu.Items.Add(sizes);
            var pin = new MenuItem { Header = "始终置顶", IsCheckable = true, IsChecked = Topmost };
            pin.Click += delegate { Topmost = pin.IsChecked; SavePrefs(); }; menu.Items.Add(pin);
            AddItem(menu, "退出 Codex娘", delegate { Close(); }); ContextMenu = menu;
            InitializeDesktop(source, menu);
            card.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) {
                if (e.OriginalSource is TextBlock || e.OriginalSource == card) { try { DragMove(); } catch (InvalidOperationException) { } }
            };
            LocationChanged += delegate { if (IsLoaded) prefsDirty = true; };
            Closed += delegate { closed = true; animation.Stop(); heartbeat.Stop(); if (client != null) client.Dispose(); SavePrefs(); };
            Loaded += delegate {
                animation.Tick += delegate { Animate(); }; animation.Interval = TimeSpan.FromMilliseconds(280); animation.Start();
                heartbeat.Tick += delegate { UpdateLabels(); if (prefsDirty) SavePrefs(); if (DateTime.UtcNow >= nextPoll) Refresh(false); }; heartbeat.Interval = TimeSpan.FromSeconds(1); heartbeat.Start(); Refresh(false);
            };
        }
        static BitmapSource Crop(BitmapSource source, int column, int row) { var b = new CroppedBitmap(source, new Int32Rect(column * 192, row * 208, 192, 208)); b.Freeze(); return b; }
        void OpenImport() { paused = true; try { new PetImportWindow(sheetPath, codexDirectory) { Owner = this, Topmost = Topmost }.ShowDialog(); } finally { paused = false; brain.Wake(DateTime.UtcNow); } }
        void SetQuotaVisible(bool visible) {
            if (quotaVisible == visible) return;
            double previousHeight = Height;
            quotaVisible = visible;
            // Keep the pet's baseline in place while the card changes the window height.
            Top += previousHeight - (quotaVisible ? 286 : 248) * scale;
            ApplyScale(); SavePrefs();
        }
        static void AddItem(ContextMenu menu, string title, Action action) { var item = new MenuItem { Header = title }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        static void AddMeter(StackPanel content, out TextBlock label, out TextBlock value, out ProgressBar bar, out TextBlock reset) {
            var heading = new DockPanel(); value = Text("—", 15, "#5F4E79"); value.FontWeight = FontWeights.SemiBold;
            DockPanel.SetDock(value, Dock.Right); heading.Children.Add(value); label = Text("额度", 12, "#6E627C"); heading.Children.Add(label); content.Children.Add(heading);
            bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 5, Background = BrushOf("#EDE8F4"), Foreground = BrushOf("#9683D0"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 6, 0, 4) }; content.Children.Add(bar);
            reset = Text("重置时间暂不可用", 10, "#9C91A7"); reset.Margin = new Thickness(0, 0, 0, 12); content.Children.Add(reset);
        }
        void Animate() {
            AnimateDesktop();
        }
        void Refresh(bool manual) {
            DateTime now = DateTime.UtcNow;
            if (closed || busy || (manual && (now - lastAttempt).TotalSeconds < 10)) return;
            busy = true; lastAttempt = now; status.Text = "正在刷新额度…";
            Task.Factory.StartNew(delegate {
                QuotaSnapshot incoming = null; Exception error = null;
                try {
                    if (client == null) client = new QuotaClient(QuotaClient.FindCodex(), workDirectory, codexDirectory);
                    incoming = client.Read();
                } catch (Exception ex) { error = ex; }
                if (closed) { if (client != null) client.Dispose(); return; }
                Dispatcher.BeginInvoke(new Action(delegate {
                    if (closed) return;
                    busy = false; failed = error != null;
                    if (!failed) {
                        snapshot = incoming; failures = 0;
                        string selected = buckets.SelectedItem is QuotaBucket ? ((QuotaBucket)buckets.SelectedItem).Id : "codex";
                        buckets.ItemsSource = snapshot.Buckets; buckets.Visibility = snapshot.Buckets.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                        int index = snapshot.Buckets.FindIndex(b => b.Id == selected); buckets.SelectedIndex = index < 0 ? 0 : index;
                    } else { failures = Math.Min(4, failures + 1); }
                    nextPoll = DateTime.UtcNow.AddSeconds(failed ? Math.Min(300, 60 * failures) : 60);
                    UpdateLabels();
                }));
            });
        }
        void UpdateMeter(QuotaWindow window, string fallback, TextBlock title, TextBlock value, ProgressBar bar, TextBlock reset, bool stale) {
            if (window == null) window = new QuotaWindow();
            title.Text = window.Label(fallback) + " · 剩余";
            value.Text = window.Remaining.HasValue ? window.Remaining.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "暂不可用";
            bar.Value = window.Remaining ?? 0; bar.Foreground = stale ? muted : violet;
            reset.Text = window.ResetText(DateTime.UtcNow);
            reset.ToolTip = window.ResetUtc.HasValue ? window.ResetUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") : null;
        }
        void UpdateLabels() {
            bool stale = failed || (snapshot != null && (DateTime.UtcNow - snapshot.CapturedUtc).TotalSeconds > 180);
            var bucket = buckets.SelectedItem as QuotaBucket;
            if (bucket == null && snapshot != null && snapshot.Buckets.Count > 0) bucket = snapshot.Buckets[0];
            if (!busy) {
                if (snapshot == null) status.Text = failed ? "读取失败 · 请检查 Codex 登录或网络" : "正在读取额度…";
                else if (snapshot.Buckets.Count == 0) status.Text = "当前账户未提供额度数据";
                else status.Text = (stale ? "旧数据 · " : "更新 ") + snapshot.CapturedUtc.ToLocalTime().ToString("HH:mm") + (stale ? " · 刷新失败" : " · 每分钟刷新");
            }
            status.ToolTip = "读取本机 Codex CLI 登录账户的用量；并非 API 余额。若账户不同，请先在 CLI 登录对应账户。";
            UpdateMeter(bucket == null ? null : bucket.Primary, "短期", firstTitle, firstValue, firstBar, firstReset, stale);
            UpdateMeter(bucket == null ? null : bucket.Secondary, "长期", secondTitle, secondValue, secondBar, secondReset, stale);
        }
        void ApplyScale() {
            layout.Width = quotaVisible ? 470 : 204; layout.Height = quotaVisible ? 286 : 248;
            layout.ColumnDefinitions[1].Width = new GridLength(quotaVisible ? 258 : 0);
            layout.ColumnDefinitions[2].Width = new GridLength(quotaVisible ? 8 : 0);
            card.Visibility = quotaVisible ? Visibility.Visible : Visibility.Collapsed;
            if (quotaMenu != null) quotaMenu.IsChecked = quotaVisible;
            layout.LayoutTransform = new ScaleTransform(scale, scale); Width = layout.Width * scale; Height = layout.Height * scale;
            Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(Left, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
            Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(Top, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
        }
        void LoadPrefs() {
            try {
                if (!File.Exists(prefsPath)) return;
                var settings = QuotaData.Object(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(prefsPath)));
                double? s = QuotaData.Number(QuotaData.Get(settings, "scale")); if (s.HasValue && s >= .8 && s <= 1.5) scale = s.Value;
                double? x = QuotaData.Number(QuotaData.Get(settings, "left")); if (x.HasValue) Left = x.Value;
                double? y = QuotaData.Number(QuotaData.Get(settings, "top")); if (y.HasValue) Top = y.Value;
                object pin = QuotaData.Get(settings, "topmost"); if (pin is bool) Topmost = (bool)pin;
                object showQuota = QuotaData.Get(settings, "showQuota"); if (showQuota is bool) quotaVisible = (bool)showQuota;
            } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
        }
        void SavePrefs() {
            prefsDirty = false;
            try { Directory.CreateDirectory(Path.GetDirectoryName(prefsPath)); File.WriteAllText(prefsPath, new JavaScriptSerializer().Serialize(new { left = Left, top = Top, scale = scale, topmost = Topmost, showQuota = quotaVisible })); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public void SetPreview(QuotaSnapshot sample, bool isFailed) {
            snapshot = sample; failed = isFailed; buckets.ItemsSource = sample.Buckets; buckets.SelectedIndex = 0;
            buckets.Visibility = sample.Buckets.Count > 1 ? Visibility.Visible : Visibility.Collapsed; UpdateLabels();
        }
        public void Render(string destination) {
            layout.Measure(new Size(Width, Height)); layout.Arrange(new Rect(0, 0, Width, Height)); layout.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)(Width * 2), (int)(Height * 2), 192, 192, PixelFormats.Pbgra32); bitmap.Render(layout);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(destination)) encoder.Save(stream);
        }
    }
    public static class QuotaPet {
        [STAThread]
        public static void Run(string sheet, string dataDirectory, string codexHome) {
            Directory.CreateDirectory(dataDirectory);
            bool created;
            using (var mutex = new Mutex(true, "Local\\CodexGirlQuota-" + Environment.UserName, out created)) {
                if (!created) { MessageBox.Show("Codex娘额度窗口已在运行。", "Codex娘"); return; }
                try { new Application().Run(new QuotaPetWindow(sheet, dataDirectory, codexHome)); }
                finally { mutex.ReleaseMutex(); }
            }
        }
        [STAThread]
        public static void Preview(string sheet, string output, string json, bool failed) {
            var sample = QuotaData.Parse(json, DateTime.UtcNow);
            new QuotaPetWindow(sheet, Path.GetDirectoryName(output), Path.GetDirectoryName(output)).SetAndRender(sample, failed, output);
        }
        static void SetAndRender(this QuotaPetWindow window, QuotaSnapshot sample, bool failed, string output) { window.SetPreview(sample, failed); window.Render(output); }
    }
}
