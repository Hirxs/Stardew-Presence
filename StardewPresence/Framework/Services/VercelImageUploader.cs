using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using StardewModdingAPI;

namespace StardewPresence.Framework.Services
{
    public class VercelImageUploader
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private readonly IMonitor monitor;
        private string? lastImageHash;
        private string? cachedUploadedUrl;

        public VercelImageUploader(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        public async Task<string?> UploadImageAsync(byte[] pngBytes, string apiUrl, string? authBearerToken = null)
        {
            if (pngBytes == null || pngBytes.Length == 0)
            {
                this.monitor.Log("[VercelUploader] Image byte array is empty.", LogLevel.Warn);
                return null;
            }

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                this.monitor.Log("[VercelUploader] Vercel endpoint URL is not configured.", LogLevel.Warn);
                return null;
            }

            string currentHash = ComputeHash(pngBytes);
            if (currentHash == this.lastImageHash && !string.IsNullOrEmpty(this.cachedUploadedUrl))
            {
                ModLogger.LogTrace(this.monitor, "[VercelUploader] Identical image detected. Reusing cached URL.");
                return this.cachedUploadedUrl;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);

                if (!string.IsNullOrWhiteSpace(authBearerToken))
                {
                    if (authBearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", authBearerToken);
                    }
                    else
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authBearerToken);
                    }
                }

                using var content = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(pngBytes);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                content.Add(imageContent, "file", "image.png");

                request.Content = content;

                using HttpResponseMessage response = await HttpClient.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = TryExtractErrorMessage(responseBody) ?? response.ReasonPhrase ?? "Unknown error";
                    this.monitor.Log($"[VercelUploader] Upload failed (HTTP {(int)response.StatusCode}): {errorMessage}", LogLevel.Warn);
                    return null;
                }

                string trimmedBody = responseBody.Trim();
                if (trimmedBody.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmedBody.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    this.lastImageHash = currentHash;
                    this.cachedUploadedUrl = trimmedBody;
                    ModLogger.LogInfo(this.monitor, $"[VercelUploader] Image uploaded successfully: {trimmedBody}");
                    return trimmedBody;
                }

                using JsonDocument doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("url", out JsonElement urlElement))
                {
                    string? uploadedUrl = urlElement.GetString();
                    if (!string.IsNullOrEmpty(uploadedUrl))
                    {
                        this.lastImageHash = currentHash;
                        this.cachedUploadedUrl = uploadedUrl;
                        ModLogger.LogInfo(this.monitor, $"[VercelUploader] Image uploaded successfully: {uploadedUrl}");
                        return uploadedUrl;
                    }
                }
                else if (doc.RootElement.TryGetProperty("data", out JsonElement dataElement) && dataElement.TryGetProperty("url", out JsonElement dataUrlElement))
                {
                    string? uploadedUrl = dataUrlElement.GetString();
                    if (!string.IsNullOrEmpty(uploadedUrl))
                    {
                        this.lastImageHash = currentHash;
                        this.cachedUploadedUrl = uploadedUrl;
                        ModLogger.LogInfo(this.monitor, $"[VercelUploader] Image uploaded successfully: {uploadedUrl}");
                        return uploadedUrl;
                    }
                }

                this.monitor.Log($"[VercelUploader] Response missing 'url' property: {responseBody}", LogLevel.Warn);
                return null;
            }
            catch (TaskCanceledException)
            {
                this.monitor.Log("[VercelUploader] Timeout connecting to Vercel API.", LogLevel.Warn);
                return null;
            }
            catch (HttpRequestException ex)
            {
                this.monitor.Log($"[VercelUploader] Network error communicating with Vercel: {ex.Message}", LogLevel.Warn);
                return null;
            }
            catch (Exception ex)
            {
                this.monitor.Log($"[VercelUploader] Unexpected exception during upload: {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        public void InvalidateCache()
        {
            this.lastImageHash = null;
            this.cachedUploadedUrl = null;
        }

        private static string ComputeHash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private static string? TryExtractErrorMessage(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                {
                    return errorElement.GetString();
                }
                if (doc.RootElement.TryGetProperty("message", out JsonElement msgElement))
                {
                    return msgElement.GetString();
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
