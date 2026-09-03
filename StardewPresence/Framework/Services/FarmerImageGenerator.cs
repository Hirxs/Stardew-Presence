using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewPresence.Framework.Models;
using StardewPresence.Framework.Rendering;
using StardewModdingAPI;
using StardewValley;

namespace StardewPresence.Framework.Services
{
    public class FarmerImageGenerator : IDisposable
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ModConfig config;
        private readonly ImageHostingService imageHostingService;
        private readonly Dictionary<string, Texture2D?> backgroundCache = new(StringComparer.OrdinalIgnoreCase);

        private string? lastAppearanceKey;
        private string? cachedUploadedUrl;
        private bool isUploading;

        public FarmerImageGenerator(IModHelper helper, IMonitor monitor, ModConfig config)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
            this.imageHostingService = new ImageHostingService(monitor);
        }

        public string? GetCachedImageUrl() => cachedUploadedUrl;

        public void ResetCache()
        {
            cachedUploadedUrl = null;
            lastAppearanceKey = null;
            ClearBackgroundCache();
        }

        public void InvalidateCache()
        {
            lastAppearanceKey = null;
            ClearBackgroundCache();
        }

        public void ClearBackgroundCache()
        {
            foreach (var kvp in backgroundCache)
            {
                try { kvp.Value?.Dispose(); } catch { }
            }
            backgroundCache.Clear();
        }

        public string GetAppearanceKey(Farmer farmer)
        {
            string season = !string.IsNullOrWhiteSpace(config.ForcedSeason) && !config.ForcedSeason.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                ? config.ForcedSeason.ToLowerInvariant()
                : (Game1.currentSeason ?? "spring").ToLowerInvariant();

            string hairColor = farmer.hairstyleColor.Value.ToString();
            string pantsColor = farmer.pantsColor.Value.ToString();
            string spouseKey = farmer.spouse ?? "single";

            return $"{season}_{config.ForcedSeason}_{config.UseCustomMapBackground}_{config.CustomBackgroundMode}_{config.LastBackgroundCaptureTimestamp}_{farmer.UniqueMultiplayerID}_{farmer.IsMale}_{farmer.skin.Value}_{farmer.hair.Value}_{hairColor}_{farmer.shirt.Value}_{farmer.pants.Value}_{pantsColor}_{farmer.accessory.Value}_{farmer.hat.Value}_{farmer.boots.Value}_{spouseKey}_{config.CompanionType}_{config.ShowFarmer}_{config.ShowCompanion}_{config.ShowSpouse}_{config.ShowPet}_{config.FarmerOffsetX}_{config.FarmerOffsetY}_{config.FarmerScale}_{config.FarmerFrame}_{config.FarmerFlip}_{config.FarmerFacingDirection}_{config.FarmerEmote}_{config.SpouseOffsetX}_{config.SpouseOffsetY}_{config.SpouseScale}_{config.SpouseFrame}_{config.SpouseLayerFront}_{config.SpouseFlip}_{config.SpouseFacingDirection}_{config.SpouseEmote}_{config.PetOffsetX}_{config.PetOffsetY}_{config.PetScale}_{config.PetFrame}_{config.PetLayerFront}_{config.PetFlip}_{config.PetEmote}";
        }

        public void CheckAndUpdate(Farmer farmer, Action<string> onUrlUpdated)
        {
            if (!config.EnableDynamicFarmerImage || farmer == null) return;
            if (isUploading) return;

            string currentKey = GetAppearanceKey(farmer);
            if (currentKey == lastAppearanceKey)
            {
                return;
            }

            lastAppearanceKey = currentKey;

            try
            {
                byte[]? pngBytes = RenderFarmerCard(farmer);
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    monitor.Log("[StardewPresence] Failed to render farmer portrait canvas.", LogLevel.Trace);
                    return;
                }

                // Save a local copy for preview inspection
                try
                {
                    string localFilePath = Path.Combine(helper.DirectoryPath, "current_farmer.png");
                    File.WriteAllBytes(localFilePath, pngBytes);
                    ModLogger.LogInfo(monitor, $"[StardewPresence] Saved local image preview to: {localFilePath}");
                }
                catch (Exception ex)
                {
                    ModLogger.LogTrace(monitor, $"[StardewPresence] Could not save local preview: {ex.Message}");
                }

                isUploading = true;

                Task.Run(async () =>
                {
                    try
                    {
                        string? url = await imageHostingService.UploadAsync(pngBytes, config);
                        if (!string.IsNullOrEmpty(url))
                        {
                            cachedUploadedUrl = url;
                            ModLogger.LogInfo(monitor, $"[StardewPresence] Uploaded dynamic farmer card to: {url}");
                            onUrlUpdated?.Invoke(url);
                        }
                    }
                    catch (Exception ex)
                    {
                        monitor.Log($"[StardewPresence] Error uploading dynamic image: {ex.Message}", LogLevel.Warn);
                    }
                    finally
                    {
                        isUploading = false;
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.LogTrace(monitor, $"[StardewPresence] Render error: {ex.Message}");
            }
        }

        public Texture2D? GetSeasonalBackgroundTexture(string season)
        {
            season = (season ?? "spring").ToLowerInvariant();
            string cacheKey = config.UseCustomMapBackground
                ? $"custom_{config.CustomBackgroundMode}_{season}_{config.LastBackgroundCaptureTimestamp}"
                : $"seasonal_{season}";

            if (backgroundCache.TryGetValue(cacheKey, out var cached) && cached != null && !cached.IsDisposed)
            {
                return cached;
            }

            if (config.UseCustomMapBackground)
            {
                string customFileName = config.CustomBackgroundMode.Equals("Seasonal", StringComparison.OrdinalIgnoreCase)
                    ? $"custom_bg_{season}.png"
                    : "custom_bg.png";

                string customPath = Path.Combine(helper.DirectoryPath, "assets", "backgrounds", customFileName);
                if (!File.Exists(customPath) && config.CustomBackgroundMode.Equals("Seasonal", StringComparison.OrdinalIgnoreCase))
                {
                    customPath = Path.Combine(helper.DirectoryPath, "assets", "backgrounds", "custom_bg.png");
                }

                if (File.Exists(customPath))
                {
                    try
                    {
                        using var fileStream = File.OpenRead(customPath);
                        var customTex = Texture2D.FromStream(Game1.graphics.GraphicsDevice, fileStream);
                        backgroundCache[cacheKey] = customTex;
                        return customTex;
                    }
                    catch (Exception ex)
                    {
                        ModLogger.LogTrace(monitor, $"[StardewPresence] Error loading custom background '{customPath}': {ex.Message}");
                    }
                }
            }

            string staticBgFile = Path.Combine(helper.DirectoryPath, "assets", "backgrounds", $"{season}_bg.png");
            if (File.Exists(staticBgFile))
            {
                try
                {
                    var tex = helper.ModContent.Load<Texture2D>($"assets/backgrounds/{season}_bg.png");
                    backgroundCache[cacheKey] = tex;
                    return tex;
                }
                catch (Exception ex)
                {
                    ModLogger.LogTrace(monitor, $"[StardewPresence] Could not load background '{staticBgFile}': {ex.Message}");
                }
            }

            backgroundCache[cacheKey] = null;
            return null;
        }

        public byte[]? RenderFarmerCard(Farmer farmer)
        {
            if (farmer == null) return null;

            int width = 256;
            int height = 256;
            var graphicsDevice = Game1.graphics.GraphicsDevice;

            string season = !string.IsNullOrWhiteSpace(config.ForcedSeason) && !config.ForcedSeason.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                ? config.ForcedSeason.ToLowerInvariant()
                : (Game1.currentSeason ?? "spring").ToLowerInvariant();

            Texture2D? bgTexture = GetSeasonalBackgroundTexture(season);
            var (spouseNpc, spouseFarmer, petNpc) = CompanionResolver.GetCompanions(farmer, config);
            var layout = UILayout.Load(helper.DirectoryPath);

            using var renderTarget = new RenderTarget2D(
                graphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
            );

            FarmerSceneRenderer.RenderSceneToTarget(
                graphicsDevice,
                renderTarget,
                farmer,
                spouseFarmer,
                spouseNpc,
                petNpc,
                bgTexture,
                config,
                layout,
                width,
                height
            );

            using MemoryStream ms = new MemoryStream();
            renderTarget.SaveAsPng(ms, width, height);
            return ms.ToArray();
        }

        public void Dispose()
        {
            ClearBackgroundCache();
            imageHostingService.Dispose();
        }
    }
}
