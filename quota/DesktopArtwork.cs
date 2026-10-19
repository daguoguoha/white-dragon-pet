using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexGirl {
    // Preserve the original strip pixels until the final high-DPI render, instead of enlarging native Pet cells.
    public static class DesktopArtwork {
        public const int RasterScale = 2;
        internal static Stream Open(string name, string sheet) {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            return stream ?? File.OpenRead(Path.Combine(Path.GetDirectoryName(sheet), "desktop", name));
        }
        public static void Load(string sheet, List<List<BitmapSource>> actions) {
            Dictionary<string, object> manifest;
            using (var stream = Open("desktop-artwork.json", sheet)) using (var reader = new StreamReader(stream))
                manifest = QuotaData.Object(new JavaScriptSerializer().DeserializeObject(reader.ReadToEnd()));
            var rows = (object[])manifest["rows"];
            foreach (object value in rows) {
                var row = QuotaData.Object(value); int index = Convert.ToInt32(row["row"]);
                BitmapSource source;
                using (var stream = Open((string)row["file"], sheet)) {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); source = RemoveKey(bitmap);
                }
                var frames = new List<BitmapSource>();
                foreach (object item in (object[])row["frames"]) {
                    var frame = QuotaData.Object(item); var crop = (object[])frame["source"]; var destination = (object[])frame["target"];
                    var image = new CroppedBitmap(source, new Int32Rect(Convert.ToInt32(crop[0]), Convert.ToInt32(crop[1]), Convert.ToInt32(crop[2]), Convert.ToInt32(crop[3]))); image.Freeze();
                    // A final pass also removes isolated chroma ringing introduced by the high-quality resampler.
                    frames.Add(RemoveKey(Render(image, new Rect(Convert.ToDouble(destination[0]), Convert.ToDouble(destination[1]), Convert.ToDouble(destination[2]), Convert.ToDouble(destination[3])))));
                }
                if (index < 0 || index >= actions.Count || frames.Count != actions[index].Count) throw new InvalidDataException("Invalid desktop artwork registration");
                actions[index] = frames;
            }
        }
        public static BitmapSource Render(BitmapSource source, Rect destination) {
            var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            using (DrawingContext dc = visual.RenderOpen()) dc.DrawImage(source, destination);
            var bitmap = new RenderTargetBitmap(192 * RasterScale, 208 * RasterScale, 96 * RasterScale, 96 * RasterScale, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        }
        static BitmapSource RemoveKey(BitmapSource source) {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth, height = bgra.PixelHeight; byte[] pixels = new byte[width * height * 4]; bgra.CopyPixels(pixels, width * 4, 0);
            for (int i = 0; i < pixels.Length; i += 4) {
                double b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                if (r * r + (255 - g) * (255 - g) + b * b <= 96 * 96) { pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3] = 0; continue; }
                // Unmix the green backing at antialiased edges before resampling, preventing a green fringe.
                double spill = Math.Max(0, g - Math.Max(r, b));
                if (spill > 8) {
                    double coverage = Math.Max(.01, 1 - spill / 255);
                    pixels[i] = Channel(b / coverage); pixels[i + 2] = Channel(r / coverage);
                    pixels[i + 1] = Channel((g - 255 * (1 - coverage)) / coverage);
                    pixels[i + 3] = Channel(pixels[i + 3] * coverage);
                }
            }
            var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, width * 4); result.Freeze(); return result;
        }
        static byte Channel(double value) { return (byte)Math.Max(0, Math.Min(255, Math.Round(value))); }
    }
}
