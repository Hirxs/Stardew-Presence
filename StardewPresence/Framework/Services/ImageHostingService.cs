using System;
using System.Threading.Tasks;
using StardewModdingAPI;

namespace StardewPresence.Framework.Services
{
    public class ImageHostingService : IDisposable
    {
        private readonly IMonitor monitor;
        private readonly VercelImageUploader vercelUploader;

        public ImageHostingService(IMonitor monitor)
        {
            this.monitor = monitor;
            this.vercelUploader = new VercelImageUploader(monitor);
        }

        public async Task<string?> UploadAsync(byte[] pngBytes, ModConfig config)
        {
            if (pngBytes == null || pngBytes.Length == 0) return null;

            string uploadUrl = !string.IsNullOrWhiteSpace(config.CustomUploadUrl)
                ? config.CustomUploadUrl
                : "https://hyris-workshop-img-api.vercel.app/api/upload";

            return await vercelUploader.UploadImageAsync(pngBytes, uploadUrl, config.CustomUploadAuthHeader);
        }

        public void InvalidateCache()
        {
            vercelUploader.InvalidateCache();
        }

        public void Dispose()
        {
        }
    }
}
