using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using StardewModdingAPI;

namespace StardewDiscordRPC.Framework.Services
{
    /// <summary>
    /// Manages image uploads to hosting providers (Vercel Custom API, Freeimage.host, ImgBB, Catbox)
    /// with automatic fallback strategies.
    /// </summary>
    public class ImageHostingService : IDisposable
    {
        private readonly IMonitor monitor;
        private readonly HttpClient httpClient;
        private readonly VercelImageUploader vercelUploader;

        public ImageHostingService(IMonitor monitor)
        {
            this.monitor = monitor;
            this.httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            this.vercelUploader = new VercelImageUploader(monitor);
        }

        public async Task<string?> UploadAsync(byte[] pngBytes, ModConfig config)
        {
            if (pngBytes == null || pngBytes.Length == 0) return null;

            string service = config.ImageHostService?.Trim().ToLowerInvariant() ?? "auto";

            // 1. Custom Endpoint (e.g. Vercel Blob API)
            if (service == "custom" || !string.IsNullOrWhiteSpace(config.CustomUploadUrl))
            {
                string? url = await UploadCustomAsync(pngBytes, config.CustomUploadUrl, config.CustomUploadAuthHeader);
                if (!string.IsNullOrEmpty(url)) return url;
            }

            // 2. ImgBB
            if (service == "imgbb" && !string.IsNullOrWhiteSpace(config.ImgBbApiKey))
            {
                string? url = await UploadImgBbAsync(pngBytes, config.ImgBbApiKey);
                if (!string.IsNullOrEmpty(url)) return url;
            }

            // 3. Catbox
            if (service == "catbox")
            {
                string? url = await UploadCatboxAsync(pngBytes);
                if (!string.IsNullOrEmpty(url)) return url;
            }

            // 4. Freeimage (or default Auto)
            if (service == "freeimage" || service == "auto")
            {
                string? url = await UploadFreeimageAsync(pngBytes, config.FreeImageApiKey);
                if (!string.IsNullOrEmpty(url)) return url;
            }

            // 5. Automatic Fallback to Catbox
            ModLogger.LogTrace(monitor, "[StardewDiscordRPC] Using fallback Catbox image upload...");
            return await UploadCatboxAsync(pngBytes);
        }

        private async Task<string?> UploadCustomAsync(byte[] pngBytes, string? url, string? authHeader)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            return await vercelUploader.UploadImageAsync(pngBytes, url, authHeader);
        }

        private async Task<string?> UploadCatboxAsync(byte[] pngBytes)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("fileupload"), "reqtype");
                var imageContent = new ByteArrayContent(pngBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                content.Add(imageContent, "fileToUpload", "farmer.png");

                using HttpResponseMessage response = await httpClient.PostAsync("https://catbox.moe/user/api.php", content);
                if (response.IsSuccessStatusCode)
                {
                    string url = (await response.Content.ReadAsStringAsync()).Trim();
                    if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        return url;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogTrace(monitor, $"[StardewDiscordRPC] Catbox upload error: {ex.Message}");
            }
            return null;
        }

        private async Task<string?> UploadImgBbAsync(byte[] pngBytes, string apiKey)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(pngBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                content.Add(imageContent, "image", "farmer.png");

                using HttpResponseMessage response = await httpClient.PostAsync($"https://api.imgbb.com/1/upload?key={apiKey}", content);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("url", out var urlEl))
                    {
                        return urlEl.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogTrace(monitor, $"[StardewDiscordRPC] ImgBB upload error: {ex.Message}");
            }
            return null;
        }

        private async Task<string?> UploadFreeimageAsync(byte[] pngBytes, string? customApiKey)
        {
            string apiKey = !string.IsNullOrWhiteSpace(customApiKey)
                ? customApiKey
                : "6d207e02198a847aa98d0a2a901485a5";

            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(apiKey), "key");
                content.Add(new StringContent("upload"), "action");
                content.Add(new StringContent("json"), "format");

                var imageContent = new ByteArrayContent(pngBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                content.Add(imageContent, "source", "farmer_portrait.png");

                using HttpResponseMessage response = await httpClient.PostAsync("https://freeimage.host/api/1/upload", content);

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    ModLogger.LogTrace(monitor, $"[StardewDiscordRPC] Freeimage.host HTTP {(int)response.StatusCode}: {errBody}");
                    return null;
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseJson);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("image", out JsonElement imageEl))
                {
                    if (imageEl.TryGetProperty("url", out JsonElement urlEl))
                    {
                        return urlEl.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogTrace(monitor, $"[StardewDiscordRPC] Freeimage upload exception: {ex.Message}");
            }

            return null;
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
