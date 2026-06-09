using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JJSploitPremium
{
    public static class BackgroundManager
    {
        public const string DefaultImageUrl = "https://wearedevs.net/images/thumbnails/JJSploit.webp";

        private static readonly HttpClient Http = new HttpClient();
        private static Image _image;

        public static event Action ImageChanged;

        static BackgroundManager()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        public static Image CurrentImage => _image;

        public static string BundledImagePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_background.png");

        /// <summary>Load bundled/cached image immediately (no network).</summary>
        public static void LoadDefaultSync()
        {
            if (TryLoadFromPath(BundledImagePath))
                return;

            string cachePath = GetCachePath(DefaultImageUrl);
            TryLoadFromPath(cachePath);
        }

        public static async Task RefreshAsync(string url)
        {
            string target = NormalizeUrl(url);
            string cachePath = GetCachePath(target);

            if (File.Exists(cachePath) && TryLoadFromPath(cachePath))
            {
                ImageChanged?.Invoke();
                return;
            }

            if (target == DefaultImageUrl)
                TryLoadFromPath(BundledImagePath);

            try
            {
                byte[] bytes = await Http.GetByteArrayAsync(target).ConfigureAwait(false);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath) ?? ".");
                File.WriteAllBytes(cachePath, bytes);
                SetImageFromBytes(bytes);
            }
            catch
            {
                if (_image == null)
                {
                    if (!TryLoadFromPath(BundledImagePath))
                        TryLoadFromPath(cachePath);
                }
            }

            ImageChanged?.Invoke();
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return DefaultImageUrl;

            url = url.Trim();
            if (url.IndexOf("wearedevs.net/d/JJSploit", StringComparison.OrdinalIgnoreCase) >= 0)
                return DefaultImageUrl;

            return url;
        }

        private static bool TryLoadFromPath(string path)
        {
            if (!File.Exists(path)) return false;

            try
            {
                SetImageFromBytes(File.ReadAllBytes(path));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SetImageFromBytes(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                var img = Image.FromStream(ms);
                _image?.Dispose();
                _image = (Image)img.Clone();
                img.Dispose();
            }
        }

        private static string GetCachePath(string url)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                string name = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty);
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "workspace", "background_" + name + ".img");
            }
        }
    }
}
