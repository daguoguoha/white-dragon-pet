using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexGirl {
    public static class InteractionAnimations {
        static readonly string[] BasicStates = { "stroke", "eat", "happy", "angry", "bored", "sleep" };
        public static readonly string[] FoodStates = { "eat-fish", "eat-cake", "eat-lollipop", "eat-dango", "eat-diamond" };
        static readonly string[] FoodNames = { "fish", "cake", "lollipop", "dango", "diamond" };
        public static readonly string[] States = { "stroke", "eat", "happy", "angry", "bored", "sleep", "eat-fish", "eat-cake", "eat-lollipop", "eat-dango", "eat-diamond" };
        // The generated atlas has a slightly higher last gutter. Crop there to preserve the sleeping horns.
        static readonly int[] RowEdges = { 0, 256, 512, 768, 1024, 1270, 1536 };
        public static Dictionary<string, List<BitmapSource>> Load(string originalSheet) {
            BitmapImage atlas = ReadAtlas("interactions-v2.png", Path.Combine(Path.GetDirectoryName(originalSheet), "interactions-v2.png"));
            if (atlas.PixelWidth != 1024 || atlas.PixelHeight != 1536) throw new InvalidDataException("Invalid interaction atlas");
            var bank = new Dictionary<string, List<BitmapSource>>();
            for (int row = 0; row < BasicStates.Length; row++) {
                var crops = new List<BitmapSource>();
                for (int column = 0; column < 4; column++) {
                    var crop = new CroppedBitmap(atlas, new Int32Rect(column * 256, RowEdges[row], 256, RowEdges[row + 1] - RowEdges[row])); crop.Freeze();
                    crops.Add(crop);
                }
                bank.Add(BasicStates[row], Normalize(crops));
            }
            for (int food = 0; food < FoodNames.Length; food++) {
                string name = FoodNames[food] + "-v1.png";
                BitmapImage foodAtlas = ReadAtlas(name, Path.Combine(Path.GetDirectoryName(originalSheet), "foods", name));
                if (foodAtlas.PixelWidth < 512 || foodAtlas.PixelHeight < 512) throw new InvalidDataException("Invalid food animation atlas");
                var crops = new List<BitmapSource>(); int midX = foodAtlas.PixelWidth / 2, midY = foodAtlas.PixelHeight / 2;
                for (int frame = 0; frame < 4; frame++) {
                    int x = frame % 2 == 0 ? 0 : midX, y = frame < 2 ? 0 : midY;
                    var crop = new CroppedBitmap(foodAtlas, new Int32Rect(x, y, frame % 2 == 0 ? midX : foodAtlas.PixelWidth - midX, frame < 2 ? midY : foodAtlas.PixelHeight - midY)); crop.Freeze(); crops.Add(crop);
                }
                bank.Add(FoodStates[food], Normalize(crops));
            }
            return bank;
        }
        static BitmapImage ReadAtlas(string resource, string fallback) {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream == null) stream = File.OpenRead(fallback);
            using (stream) { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image; }
        }
        static List<BitmapSource> Normalize(List<BitmapSource> crops) {
            var bounds = new List<Int32Rect>(); double maxHeight = 0, maxWidth = 0;
            foreach (BitmapSource crop in crops) { Int32Rect bound = VisibleBounds(crop); bounds.Add(bound); maxHeight = Math.Max(maxHeight, bound.Height); maxWidth = Math.Max(maxWidth, bound.Width); }
            // Fixed scale per sequence and a shared foot baseline prevent snapping between frames/states.
            double factor = Math.Min(177 / maxHeight, 154 / maxWidth);
            var frames = new List<BitmapSource>();
            for (int i = 0; i < crops.Count; i++) {
                var visible = new CroppedBitmap(crops[i], bounds[i]); visible.Freeze();
                double width = bounds[i].Width * factor, height = bounds[i].Height * factor;
                var drawing = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.HighQuality);
                using (DrawingContext dc = drawing.RenderOpen()) dc.DrawImage(visible, new Rect(96 - width / 2, 202 - height, width, height));
                var frame = new RenderTargetBitmap(192, 208, 96, 96, PixelFormats.Pbgra32); frame.Render(drawing); frame.Freeze(); frames.Add(frame);
            }
            return frames;
        }
        static Int32Rect VisibleBounds(BitmapSource source) {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); int width = bgra.PixelWidth, height = bgra.PixelHeight;
            byte[] pixels = new byte[width * height * 4]; bgra.CopyPixels(pixels, width * 4, 0);
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (pixels[(y * width + x) * 4 + 3] >= 64) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
            if (right < left || bottom < top) throw new InvalidDataException("Empty interaction frame");
            left = Math.Max(0, left - 2); top = Math.Max(0, top - 2); right = Math.Min(width - 1, right + 2); bottom = Math.Min(height - 1, bottom + 2);
            return new Int32Rect(left, top, right - left + 1, bottom - top + 1);
        }
    }
}
