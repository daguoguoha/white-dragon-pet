using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace CodexGirl {
    public sealed partial class QuotaPetWindow {
        readonly PetBehavior brain = new PetBehavior(DateTime.UtcNow);
        readonly Random random = new Random();
        readonly List<List<BitmapSource>> actions = new List<List<BitmapSource>>();
        Dictionary<string, List<BitmapSource>> interactionActions;
        readonly Canvas particles = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        readonly List<Particle> floating = new List<Particle>();
        Border bubble;
        Image previousPose;
        TextBlock bubbleText;
        Forms.NotifyIcon tray;
        System.Drawing.Icon trayIcon;
        readonly Dictionary<string, MenuItem> modeItems = new Dictionary<string, MenuItem>();
        readonly Dictionary<int, MenuItem> frameRateItems = new Dictionary<int, MenuItem>();
        int targetFrameRate = 60;
        bool renderingAttached;
        double lastRenderAt = -1, nextRenderAt, nextPoseAt;
        TimeSpan lastComposition = TimeSpan.MinValue;
        MenuItem moodItem, passthroughItem;
        string movementMode = "still", renderedAction = "";
        bool pointerDown, dragging, onHead, stroked, paused, moving, passthrough;
        Point pressScreen, pressWindow, lastPointer;
        double strokeDistance;
        int actionFrame;
        DateTime bubbleUntil, nextTarget, nextIdleLine, nextParticle, edgeUntil, edgeCooldown;
        DateTime nextSystemCheck;
        string edgePose = "";
        Point? target;
        double dxLast;
        readonly Stopwatch animationClock = Stopwatch.StartNew();
        readonly ScaleTransform bodyBreath = new ScaleTransform(1, 1);
        readonly RotateTransform bodySway = new RotateTransform();
        readonly TranslateTransform bodyBob = new TranslateTransform();
        readonly TransformGroup bodyMotion = new TransformGroup();
        readonly SystemMonitor monitor = new SystemMonitor();
        sealed class Particle { public TextBlock Text; public double X, Y, Age; }
        string DesktopPrefs { get { return Path.Combine(workDirectory, "desktop.json"); } }

        void InitializeDesktop(BitmapSource source, ContextMenu menu) {
            int[] counts = {6, 8, 8, 4, 5, 8, 6, 6, 6, 8, 8};
            for (int row = 0; row < counts.Length; row++) {
                var frames = new List<BitmapSource>();
                for (int x = 0; x < counts[row]; x++) frames.Add(Crop(source, x, row));
                actions.Add(frames);
            }
            DesktopArtwork.Load(sheetPath, actions);
            pet.Source = actions[0][0];
            interactionActions = InteractionAnimations.Load(sheetPath);
            Title = "Codex娘 · 桌面伙伴";
            pet.ToolTip = "按住头部轻抚 · 拖动身体移动 · 双击切换额度 · 右键互动";
            pet.MouseLeftButtonDown += PetDown;
            pet.MouseMove += PetMove;
            pet.MouseLeftButtonUp += PetUp;
            pet.LostMouseCapture += delegate { pointerDown = dragging = false; };
            pet.RenderTransformOrigin = new Point(.5, .96);
            bodyMotion.Children.Add(bodyBreath); bodyMotion.Children.Add(bodySway); bodyMotion.Children.Add(bodyBob); pet.RenderTransform = bodyMotion;
            previousPose = new Image { Width = pet.Width, Height = pet.Height, Margin = pet.Margin, VerticalAlignment = pet.VerticalAlignment, Stretch = pet.Stretch, IsHitTestVisible = false, RenderTransformOrigin = new Point(.5, .96) };
            RenderOptions.SetBitmapScalingMode(previousPose, BitmapScalingMode.HighQuality);
            layout.Children.Add(previousPose); Panel.SetZIndex(previousPose, 1); Panel.SetZIndex(pet, 2);

            bubbleText = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = BrushOf("#625074"), MaxWidth = 160 };
            bubble = new Border { Background = BrushOf("#FFF9FD"), BorderBrush = BrushOf("#E3D8EE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(9, 6, 9, 6), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(3, 2, 3, 0), Child = bubbleText, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            Panel.SetZIndex(bubble, 10); layout.Children.Add(bubble);
            Grid.SetColumnSpan(particles, 3); Panel.SetZIndex(particles, 11); layout.Children.Add(particles);

            var interactions = new MenuItem { Header = "互动" };
            AddChild(interactions, "摸摸头", delegate { brain.Stroke(DateTime.UtcNow); Say("再摸一会儿也可以呀。", "♡"); });
            AddChild(interactions, "夸夸她", delegate { brain.Praise(DateTime.UtcNow); Say("嘿嘿，今天也会陪着你。", "♡"); });
            AddChild(interactions, "逗逗她", delegate { brain.Tease(DateTime.UtcNow); Say("哼，龙也是有脾气的！", "!"); });
            var foodMenu = new MenuItem { Header = "喂食" };
            foreach (string food in new[] { "小鱼干", "蛋糕", "棒棒糖", "团子", "钻石" }) {
                string selected = food;
                AddChild(foodMenu, selected, delegate { bool enjoyed = brain.Feed(selected, DateTime.UtcNow); Say(enjoyed ? "收到" + selected + "啦，谢谢你！" : "连续三次一样的，换个口味嘛。", enjoyed ? "♡" : "…"); });
            }
            interactions.Items.Add(foodMenu);
            AddChild(interactions, "跳一下", delegate { brain.React("jump", DateTime.UtcNow, 1); Say("看我的！", "✧"); });
            AddChild(interactions, "玩游戏", delegate { brain.React("game", DateTime.UtcNow, 12); Say("再来一局！", "✧"); });
            AddChild(interactions, "读书", delegate { brain.React("book", DateTime.UtcNow, 12); Say("嘘，陪我看一会儿。", null); });
            moodItem = new MenuItem { Header = "心情 60 / 100", IsEnabled = false }; interactions.Items.Add(moodItem);
            menu.Items.Insert(0, interactions);
            var modes = new MenuItem { Header = "活动模式" };
            string[] keys = { "still", "wander", "follow" }, labels = { "原地陪伴", "自由散步", "跟随鼠标" };
            for (int i = 0; i < keys.Length; i++) {
                string key = keys[i]; var item = new MenuItem { Header = labels[i], IsCheckable = true };
                item.Click += delegate { SetMovement(key); }; modes.Items.Add(item); modeItems.Add(key, item);
            }
            menu.Items.Insert(1, modes);
            var frameRates = new MenuItem { Header = "动画流畅度" };
            foreach (int rate in new[] { 60, 30 }) {
                int selected = rate;
                var item = new MenuItem { Header = rate == 60 ? "流畅 · 60 FPS" : "省电 · 30 FPS", IsCheckable = true };
                item.Click += delegate { SetFrameRate(selected); SaveDesktopPrefs(); };
                frameRates.Items.Add(item); frameRateItems.Add(rate, item);
            }
            menu.Items.Insert(2, frameRates);
            AddItem(menu, "和她聊天 / AI 设置", OpenChat);
            AddItem(menu, "查看天气", OpenWeather);
            AddItem(menu, "查看系统状态", delegate { Say(monitor.Read(), null); });
            AddItem(menu, "隐藏到托盘", delegate { if (tray != null) Hide(); });
            passthroughItem = new MenuItem { Header = "鼠标穿透（托盘可解除）", IsCheckable = true };
            passthroughItem.Click += delegate { SetPassthrough(passthroughItem.IsChecked); }; menu.Items.Add(passthroughItem);
            var startup = new MenuItem { Header = "开机启动", IsCheckable = true, IsChecked = IsAutostart() };
            startup.Click += delegate {
                try { SetAutostart(startup.IsChecked); Say(startup.IsChecked ? "已设置开机启动。" : "已关闭开机启动。", null); }
                catch (Exception) { startup.IsChecked = IsAutostart(); Say("设置失败，请检查当前账户权限。", null); }
            }; menu.Items.Add(startup);
            AddItem(menu, "回到屏幕内", Recover);
            AddItem(menu, "关于 / 参考项目许可", delegate {
                string notice = "Codex娘 1.3.4 · 独立桌面伙伴\n高清原画直接渲染，动作缓存 384×416。默认目标 60 FPS，支持 30 FPS 省电模式。交互和动作过渡参考 GenhuiC-sudo/11，含五种食物各自的进食动画。GPU 温度暂无支持。\n\n";
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("THIRD_PARTY_NOTICES.md"))
                    if (stream != null) using (var reader = new StreamReader(stream)) notice += reader.ReadToEnd();
                MessageBox.Show(this, notice, "Codex娘 · 关于");
            });
            menu.Opened += delegate { moodItem.Header = "心情 " + brain.Mood + " / 100"; };
            LoadDesktopPrefs(); UpdateModeChecks(); SetFrameRate(targetFrameRate);
            nextIdleLine = DateTime.UtcNow.AddSeconds(35);
            Loaded += delegate { CreateTray(); StartRendering(); Say("我来啦，右键我可以互动哦。", "✧"); };
            IsVisibleChanged += delegate { if (IsVisible && IsLoaded) StartRendering(); else StopRendering(); };
            Closed += delegate { StopRendering(); if (tray != null) { tray.Visible = false; tray.Dispose(); } if (trayIcon != null) trayIcon.Dispose(); SaveDesktopPrefs(); };
        }
        void SetFrameRate(int rate) {
            targetFrameRate = rate == 30 ? 30 : 60; nextRenderAt = 0;
            foreach (var pair in frameRateItems) pair.Value.IsChecked = pair.Key == targetFrameRate;
        }
        void StartRendering() {
            if (renderingAttached || closed || !IsVisible) return;
            lastRenderAt = -1; nextRenderAt = 0; lastComposition = TimeSpan.MinValue;
            // Resume without replaying all the sprite frames missed while hidden.
            nextPoseAt = animationClock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += RenderDesktop; renderingAttached = true;
        }
        void StopRendering() {
            if (!renderingAttached) return;
            CompositionTarget.Rendering -= RenderDesktop; renderingAttached = false;
        }
        void RenderDesktop(object sender, EventArgs args) {
            var frame = args as RenderingEventArgs;
            if (frame == null || frame.RenderingTime == lastComposition || !IsVisible || closed) return;
            lastComposition = frame.RenderingTime;
            double stamp = frame.RenderingTime.TotalSeconds, interval = 1.0 / targetFrameRate;
            if (stamp + .0005 < nextRenderAt) return;
            // Keep the phase instead of scheduling from callback arrival; timer jitter cannot slow playback.
            nextRenderAt += Math.Max(1, Math.Floor((stamp + .0005 - nextRenderAt) / interval) + 1) * interval;
            double now = animationClock.Elapsed.TotalSeconds;
            double elapsed = lastRenderAt < 0 ? 0 : Math.Max(0, Math.Min(.1, now - lastRenderAt));
            lastRenderAt = now;
            DesktopTick(DateTime.UtcNow, elapsed);
            RenderPoseAt(now);
        }
        static void AddChild(MenuItem parent, string header, Action action) { var item = new MenuItem { Header = header }; item.Click += delegate { action(); }; parent.Items.Add(item); }
        void Say(string line, string effect) {
            bubbleText.Text = line; bubble.Visibility = Visibility.Visible; bubbleUntil = DateTime.UtcNow.AddSeconds(6);
            if (effect != null) { AddParticle(effect); AddParticle(effect); }
        }
        void AddParticle(string text) {
            if (floating.Count >= 10) return;
            var label = new TextBlock { Text = text, FontSize = 17, Foreground = BrushOf("#BC8AB9") };
            var p = new Particle { Text = label, X = 65 + random.Next(70), Y = Math.Max(24, layout.Height - 210) };
            Canvas.SetLeft(label, p.X); Canvas.SetTop(label, p.Y);
            floating.Add(p); particles.Children.Add(label);
        }
        Point ScreenPointer() {
            NativePoint p; GetCursorPos(out p);
            var dpi = PresentationSource.FromVisual(this);
            return dpi != null && dpi.CompositionTarget != null ? dpi.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y)) : new Point(p.X, p.Y);
        }
        void PetDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true; brain.Wake(DateTime.UtcNow); edgePose = ""; target = null;
            if (e.ClickCount == 2) { pointerDown = false; pet.ReleaseMouseCapture(); SetQuotaVisible(!quotaVisible); return; }
            pointerDown = true; dragging = stroked = false;
            pressScreen = lastPointer = ScreenPointer(); pressWindow = new Point(Left, Top); strokeDistance = 0;
            Point local = e.GetPosition(pet); onHead = local.Y < 110 && local.X > 30 && local.X < 162;
            pet.CaptureMouse();
        }
        void PetMove(object sender, MouseEventArgs e) {
            if (!pointerDown) return;
            Point current = ScreenPointer(); Vector delta = current - pressScreen;
            if (!dragging && Math.Abs(delta.X) + Math.Abs(delta.Y) > (onHead ? 26 : 6) * scale) { dragging = true; brain.React("surprise", DateTime.UtcNow, 2); Say("轻一点，角角还在呢！", "!"); }
            if (dragging) { Left = pressWindow.X + delta.X; Top = pressWindow.Y + delta.Y; }
            else if (onHead) {
                strokeDistance += (current - lastPointer).Length;
                if (strokeDistance > 55 * scale) { stroked = true; if (brain.Stroke(DateTime.UtcNow)) Say("嗯……好舒服。", "♡"); strokeDistance = 0; }
            }
            lastPointer = current; e.Handled = true;
        }
        void PetUp(object sender, MouseButtonEventArgs e) {
            if (!pointerDown) return;
            bool wasDragging = dragging; pointerDown = dragging = false; pet.ReleaseMouseCapture(); brain.Wake(DateTime.UtcNow);
            nextTarget = DateTime.UtcNow.AddSeconds(7); target = null;
            if (wasDragging) { ClampToScreen(); SavePrefs(); }
            else if (!stroked) { if (onHead) { brain.React("happy", DateTime.UtcNow, 2); Say("我在呢。", "✧"); } else { brain.Poke(DateTime.UtcNow); Say("呜哇，有点痒！", "!"); } }
            e.Handled = true;
        }
        void DesktopTick(DateTime now, double elapsed) {
            bool blocked = paused || pointerDown || ContextMenu.IsOpen || !IsVisible;
            bool wasSleeping = brain.Sleeping; brain.Tick(now, blocked);
            if (!wasSleeping && brain.Sleeping) Say("Zzz……", "z");
            if (bubble.Visibility == Visibility.Visible && now > bubbleUntil) bubble.Visibility = Visibility.Collapsed;
            for (int i = floating.Count - 1; i >= 0; i--) {
                Particle p = floating[i]; p.Age += elapsed; p.Y -= elapsed * 22; p.Text.Opacity = Math.Max(0, 1 - p.Age / 2.5);
                Canvas.SetLeft(p.Text, p.X); Canvas.SetTop(p.Text, p.Y);
                if (p.Age >= 2.5) { particles.Children.Remove(p.Text); floating.RemoveAt(i); }
            }
            if (brain.Sleeping && now >= nextParticle) { nextParticle = now.AddSeconds(3.5); AddParticle("z"); }
            moving = false;
            if (!blocked && !brain.Sleeping) {
                UpdateEdge(now);
                if (edgePose.Length == 0 && brain.Expression == "idle") MovePet(now, elapsed);
                if (now >= nextIdleLine) {
                    nextIdleLine = now.AddSeconds(random.Next(40, 70));
                    Say(brain.Mood >= 70 ? "今天心情不错，陪你一起加油。" : brain.Mood <= 30 ? "要不要休息一下，陪陪我？" : "慢慢来，我陪着你。", null);
                }
            }
            UpdateBodyMotion();
            if (!blocked && now >= nextSystemCheck) {
                nextSystemCheck = now.AddSeconds(30);
                monitor.Sample();
                if (monitor.MemoryPercent >= 90) Say("内存有点紧张，记得保存工作。", null);
                else if (monitor.CpuPercent.HasValue && monitor.CpuPercent.Value >= 90) Say("CPU 正在忙，稍等它一会儿。", null);
            }
        }
        void UpdateEdge(DateTime now) {
            if (movementMode == "follow") { edgePose = ""; return; }
            if (edgePose.Length > 0) { if (now >= edgeUntil) { edgePose = ""; edgeCooldown = now.AddSeconds(20); } return; }
            if (now < edgeCooldown || brain.Expression != "idle") return;
            Rect area = CurrentWorkArea();
            if (Top <= area.Top + 6) edgePose = "hang";
            else if (Top + Height >= area.Bottom - 6) edgePose = "rest";
            if (edgePose.Length > 0) { edgeUntil = now.AddSeconds(random.Next(7, 17)); target = null; Say(edgePose == "hang" ? "倒过来看看世界。" : "在桌边歇一会儿。", null); }
        }
        Rect CurrentWorkArea() {
            var ps = PresentationSource.FromVisual(this);
            Point anchor = movementMode == "follow" ? new Point(Forms.Cursor.Position.X, Forms.Cursor.Position.Y) : (ps != null && ps.CompositionTarget != null ? ps.CompositionTarget.TransformToDevice.Transform(new Point(Left + Width / 2, Top + Height / 2)) : new Point(Left + Width / 2, Top + Height / 2));
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)anchor.X, (int)anchor.Y));
            // WPF positions use device-independent units. Convert native screen coordinates using this window's DPI.
            Matrix matrix = ps != null && ps.CompositionTarget != null ? ps.CompositionTarget.TransformFromDevice : Matrix.Identity;
            var a = screen.WorkingArea; return new Rect(matrix.Transform(new Point(a.Left, a.Top)), matrix.Transform(new Point(a.Right, a.Bottom)));
        }
        void MovePet(DateTime now, double elapsed) {
            if (movementMode == "still") return;
            Rect area = CurrentWorkArea();
            if (movementMode == "follow") {
                Point cursor = ScreenPointer(); double distance = (cursor - new Point(Left + 100 * scale, Top + Height / 2)).Length;
                target = distance < 135 * scale ? (Point?)null : new Point(cursor.X - 100 * scale, cursor.Y - Height / 2);
            } else if (!target.HasValue && now >= nextTarget) {
                double tx = area.Left + random.NextDouble() * Math.Max(0, area.Width - Width);
                double ty = area.Top + random.NextDouble() * Math.Max(0, area.Height - Height);
                if (random.Next(4) == 0) ty = area.Bottom - Height;
                target = new Point(tx, ty);
            }
            if (!target.HasValue) return;
            Point t = target.Value; t.X = Math.Max(area.Left, Math.Min(t.X, area.Right - Width)); t.Y = Math.Max(area.Top, Math.Min(t.Y, area.Bottom - Height));
            Vector delta = t - new Point(Left, Top); double distanceLeft = delta.Length;
            if (distanceLeft < 3) { target = null; nextTarget = now.AddSeconds(random.Next(6, 14)); return; }
            double step = Math.Min(distanceLeft, 95 * elapsed * scale); dxLast = delta.X;
            Left += delta.X / distanceLeft * step; Top += delta.Y / distanceLeft * step; moving = true;
        }
        void AnimateDesktop() {
            // Deterministic single-step entry point used by asset previews.
            RenderPoseAt(Math.Max(animationClock.Elapsed.TotalSeconds, nextPoseAt));
        }
        void RenderPoseAt(double now) {
            string state = CurrentAnimationState();
            bool changed = state != renderedAction;
            if (changed) {
                previousPose.Source = pet.Source; previousPose.RenderTransform = pet.RenderTransform.CloneCurrentValue();
                previousPose.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140)));
                pet.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
                renderedAction = state; actionFrame = 0;
            }
            int row = state == "left" ? 2 : state == "right" ? 1 : state == "jump" ? 4 : state == "surprise" || state == "angry" ? 5 : state == "rest" || state == "sleep" || state == "bored" ? 6 : state == "game" ? 7 : state == "book" ? 8 : state == "love" || state == "happy" ? 3 : 0;
            List<BitmapSource> frames = actions[row];
            if (interactionActions.ContainsKey(state)) frames = interactionActions[state];
            if (changed) nextPoseAt = now + PoseDuration(state, row, 0);
            else {
                double cycle = 0;
                for (int i = 0; i < frames.Count; i++) cycle += PoseDuration(state, row, i);
                if (now - nextPoseAt >= cycle) nextPoseAt += Math.Floor((now - nextPoseAt) / cycle) * cycle;
                while (now + .000001 >= nextPoseAt) {
                    actionFrame = (actionFrame + 1) % frames.Count;
                    nextPoseAt += PoseDuration(state, row, actionFrame);
                }
            }
            if (!Object.ReferenceEquals(pet.Source, frames[actionFrame])) pet.Source = frames[actionFrame];
        }
        double PoseDuration(string state, int row, int index) {
            return (state == "sleep" ? 480 : state == "eat" || state.StartsWith("eat-", StringComparison.Ordinal) ? 220 : interactionActions.ContainsKey(state) ? 200 : row == 1 || row == 2 ? 90 : row == 0 ? idleTiming[index] : row == 4 ? 160 : 180) / 1000.0;
        }
        string CurrentAnimationState() {
            if (dragging) return "surprise";
            if (brain.Sleeping) return "sleep";
            if (moving) return dxLast < 0 ? "left" : "right";
            if (brain.Expression != "idle") return brain.Expression;
            if (edgePose.Length > 0) return edgePose;
            return brain.Mood >= 80 ? "happy" : brain.Mood <= 25 ? "bored" : "idle";
        }
        void UpdateBodyMotion() {
            // Adapted from 大肥鱼桌宠 paintEvent sway/bob/breath formulas (MIT notice retained).
            double now = animationClock.Elapsed.TotalSeconds;
            string state = CurrentAnimationState();
            bool sleeping = state == "sleep";
            double sway = dragging ? Math.Sin(now * 30) * 2.5 : moving ? Math.Sin(now * 9) * 3.5 : sleeping ? 0 : Math.Sin(now * 2.5) * .7;
            double bob = dragging ? Math.Sin(now * 22) * 3 : moving ? -Math.Abs(Math.Sin(now * 4.5)) * 3 : state == "happy" ? -Math.Abs(Math.Sin(now * 4.5)) * 2 : 0;
            bodyBreath.ScaleX = 1; bodyBreath.ScaleY = 1 + (sleeping ? .014 : .004) * Math.Sin(now * (sleeping ? 1.6 : 2.5));
            bodySway.CenterY = state == "hang" ? -.46 * pet.Height : 0;
            bodySway.Angle = state == "hang" ? 180 : sway; bodyBob.Y = bob;
        }
        void SetMovement(string mode) { movementMode = mode; target = null; edgePose = ""; nextTarget = DateTime.UtcNow; brain.Wake(DateTime.UtcNow); UpdateModeChecks(); SaveDesktopPrefs(); }
        void UpdateModeChecks() { foreach (var pair in modeItems) pair.Value.IsChecked = pair.Key == movementMode; }
        void ClampToScreen() { Rect a = CurrentWorkArea(); Left = Math.Max(a.Left, Math.Min(Left, a.Right - Width)); Top = Math.Max(a.Top, Math.Min(Top, a.Bottom - Height)); }
        void Recover() { Show(); SetPassthrough(false); Left = SystemParameters.WorkArea.Right - Width - 20; Top = SystemParameters.WorkArea.Bottom - Height - 20; ApplyScale(); Activate(); SavePrefs(); }
        void LoadDesktopPrefs() {
            try {
                var d = QuotaData.Object(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(DesktopPrefs)));
                object mode = QuotaData.Get(d, "mode"); if (mode is string && modeItems.ContainsKey((string)mode)) movementMode = (string)mode;
                object rate = QuotaData.Get(d, "frameRate"); if (rate != null && Convert.ToString(rate) == "30") targetFrameRate = 30;
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
            // Always start clickable so a changed tray or monitor configuration cannot lock the pet out.
        }
        void SaveDesktopPrefs() { try { Directory.CreateDirectory(workDirectory); string temp = DesktopPrefs + ".tmp"; File.WriteAllText(temp, new JavaScriptSerializer().Serialize(new { mode = movementMode, frameRate = targetFrameRate })); if (File.Exists(DesktopPrefs)) File.Replace(temp, DesktopPrefs, null); else File.Move(temp, DesktopPrefs); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        void CreateTray() {
            try {
                var bitmap = new System.Drawing.Bitmap(32, 32);
                using (bitmap) using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) using (var source = System.Drawing.Image.FromFile(sheetPath)) {
                    graphics.DrawImage(source, new System.Drawing.Rectangle(0, 0, 32, 32), new System.Drawing.Rectangle(0, 0, 192, 208), System.Drawing.GraphicsUnit.Pixel);
                    IntPtr handle = bitmap.GetHicon(); try { trayIcon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); }
                }
                tray = new Forms.NotifyIcon { Text = "Codex娘 · 双击恢复宠物", Icon = trayIcon, Visible = true };
                var menu = new Forms.ContextMenuStrip();
                menu.Items.Add("显示 / 隐藏", null, delegate { Dispatcher.BeginInvoke(new Action(delegate { if (IsVisible) Hide(); else { Show(); Activate(); } })); });
                menu.Items.Add("恢复位置 / 解除鼠标穿透", null, delegate { Dispatcher.BeginInvoke(new Action(Recover)); });
                menu.Items.Add("显示 / 隐藏额度", null, delegate { Dispatcher.BeginInvoke(new Action(delegate { Show(); SetQuotaVisible(!quotaVisible); })); });
                menu.Items.Add("退出", null, delegate { Dispatcher.BeginInvoke(new Action(Close)); }); tray.ContextMenuStrip = menu;
                tray.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(Recover)); };
            } catch (Exception) { if (tray != null) tray.Dispose(); tray = null; }
        }
        void SetPassthrough(bool enabled) {
            if (enabled && tray == null) { passthroughItem.IsChecked = false; Say("托盘不可用，暂不启用穿透。", null); return; }
            IntPtr handle = new WindowInteropHelper(this).Handle; if (handle == IntPtr.Zero) return;
            long style = GetWindowLongPtr(handle, -20).ToInt64();
            SetWindowLongPtr(handle, -20, new IntPtr(enabled ? style | 0x20 : style & ~0x20L));
            passthrough = enabled; passthroughItem.IsChecked = enabled; if (enabled) Say("已穿透，双击托盘图标可以恢复。", null);
        }
        static bool IsAutostart() { using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && key.GetValue("CodexGirlPet") != null; }
        static void SetAutostart(bool enabled) {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if (enabled) {
                    string executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("请使用独立 EXE 设置开机启动。");
                    key.SetValue("CodexGirlPet", "\"" + executable + "\"");
                } else key.DeleteValue("CodexGirlPet", false);
            }
        }
        void OpenChat() { paused = true; try { new CompanionChatWindow(workDirectory) { Owner = this, Topmost = Topmost }.ShowDialog(); } finally { paused = false; brain.Wake(DateTime.UtcNow); } }
        void OpenWeather() { paused = true; try { new WeatherWindow(workDirectory) { Owner = this, Topmost = Topmost }.ShowDialog(); } finally { paused = false; brain.Wake(DateTime.UtcNow); } }
        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong32(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
        static IntPtr GetWindowLongPtr(IntPtr h, int i) { return IntPtr.Size == 8 ? GetWindowLongPtr64(h, i) : new IntPtr(GetWindowLong32(h, i)); }
        static void SetWindowLongPtr(IntPtr h, int i, IntPtr v) { if (IntPtr.Size == 8) SetWindowLongPtr64(h, i, v); else SetWindowLong32(h, i, v.ToInt32()); }
    }
}
