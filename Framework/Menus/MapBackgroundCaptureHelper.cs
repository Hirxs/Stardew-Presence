using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewDiscordRPC.Framework.Services;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewDiscordRPC.Framework.Menus
{
    public static class MapBackgroundCaptureHelper
    {
        public static bool CaptureSpot(
            IModHelper helper,
            IMonitor monitor,
            ModConfig config,
            FarmerImageGenerator imageGenerator,
            GameLocation location,
            int tileX,
            int tileY,
            int gridSize,
            bool playSoundAndToast = true)
        {
            if (location?.Map == null || Game1.game1 == null)
            {
                monitor.Log("[StardewPresence] Cannot capture background: location or map is null.", LogLevel.Warn);
                return false;
            }

            if (location.Map.Layers.Count > 0)
            {
                int maxTileX = Math.Max(0, location.Map.Layers[0].LayerWidth - gridSize);
                int maxTileY = Math.Max(0, location.Map.Layers[0].LayerHeight - gridSize);
                tileX = Math.Clamp(tileX, 0, maxTileX);
                tileY = Math.Clamp(tileY, 0, maxTileY);
            }

            int captureMapX = tileX * 64;
            int captureMapY = tileY * 64;
            int capturePixelWidth = gridSize * 64;
            int capturePixelHeight = gridSize * 64;
            int targetResolution = 256;

            var graphicsDevice = Game1.graphics.GraphicsDevice;
            var prevTargets = graphicsDevice.GetRenderTargets();
            var prevDeviceViewport = graphicsDevice.Viewport;

            try
            {
                using var rawTarget = new RenderTarget2D(
                    graphicsDevice,
                    capturePixelWidth,
                    capturePixelHeight,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None,
                    0,
                    RenderTargetUsage.PreserveContents
                );

                var oldLocation = Game1.currentLocation;
                var oldVp = Game1.viewport;
                var oldVpFreeze = Game1.viewportFreeze;
                var oldHud = Game1.displayHUD;
                var oldMenu = Game1.activeClickableMenu;
                var oldOverlay = Game1.overlayMenu;
                var oldOnScreen = Game1.onScreenMenus;
                var oldDialogueUp = Game1.dialogueUp;

                bool wasPlayerHidden = Game1.player?.hidden.Value ?? false;
                Vector2 oldPlayerPos = Game1.player?.Position ?? Vector2.Zero;
                if (Game1.player != null)
                {
                    Game1.player.hidden.Value = true;
                    Game1.player.Position = new Vector2(-99999f, -99999f);
                }

                var otherFarmers = new List<(Farmer farmer, bool wasHidden, Vector2 oldPos)>();
                if (location.farmers != null)
                {
                    foreach (var f in location.farmers)
                    {
                        if (f != null && f != Game1.player)
                        {
                            otherFarmers.Add((f, f.hidden.Value, f.Position));
                            f.hidden.Value = true;
                            f.Position = new Vector2(-99999f, -99999f);
                        }
                    }
                }

                var oldNpcStates = new List<(NPC npc, bool wasInvisible, Vector2 oldPos)>();
                if (location.characters != null)
                {
                    foreach (var npc in location.characters)
                    {
                        if (npc != null)
                        {
                            oldNpcStates.Add((npc, npc.isInvisible.Value, npc.Position));
                            npc.isInvisible.Value = true;
                            npc.Position = new Vector2(-99999f, -99999f);
                        }
                    }
                }

                var oldAnimalStates = new List<(FarmAnimal animal, Vector2 oldPos)>();
                if (location is Farm farm && farm.animals != null)
                {
                    foreach (var animal in farm.animals.Values)
                    {
                        if (animal != null)
                        {
                            oldAnimalStates.Add((animal, animal.Position));
                            animal.Position = new Vector2(-99999f, -99999f);
                        }
                    }
                }
                else if (location is AnimalHouse animalHouse && animalHouse.animals != null)
                {
                    foreach (var animal in animalHouse.animals.Values)
                    {
                        if (animal != null)
                        {
                            oldAnimalStates.Add((animal, animal.Position));
                            animal.Position = new Vector2(-99999f, -99999f);
                        }
                    }
                }

                var oldCritters = location.critters;
                location.critters = null;

                var oldTempSprites = new List<TemporaryAnimatedSprite>(location.TemporarySprites);
                location.TemporarySprites.Clear();

                try
                {
                    if (Game1.currentLocation != location)
                    {
                        Game1.currentLocation = location;
                    }

                    bool oldTakingScreenshot = Game1.game1.takingMapScreenshot;
                    bool oldDrawLighting = Game1.drawLighting;

                    Game1.displayHUD = false;
                    Game1.activeClickableMenu = null;
                    Game1.overlayMenu = null;
                    Game1.onScreenMenus = new List<IClickableMenu>();
                    Game1.dialogueUp = false;

                    Game1.viewport = new xTile.Dimensions.Rectangle(captureMapX, captureMapY, capturePixelWidth, capturePixelHeight);
                    Game1.viewportFreeze = true;

                    graphicsDevice.SetRenderTarget(rawTarget);
                    graphicsDevice.Clear(Color.Black);

                    try
                    {
                        Game1.game1.takingMapScreenshot = true;
                        Game1.drawLighting = false;

                        Game1.game1.DrawWorld(Game1.currentGameTime, rawTarget);
                    }
                    finally
                    {
                        Game1.game1.takingMapScreenshot = oldTakingScreenshot;
                        Game1.drawLighting = oldDrawLighting;
                    }
                }
                finally
                {
                    if (Game1.currentLocation != oldLocation)
                    {
                        Game1.currentLocation = oldLocation;
                    }

                    Game1.viewport = oldVp;
                    Game1.viewportFreeze = oldVpFreeze;
                    Game1.displayHUD = oldHud;
                    Game1.activeClickableMenu = oldMenu;
                    Game1.overlayMenu = oldOverlay;
                    Game1.onScreenMenus = oldOnScreen;
                    Game1.dialogueUp = oldDialogueUp;

                    if (Game1.player != null)
                    {
                        Game1.player.hidden.Value = wasPlayerHidden;
                        Game1.player.Position = oldPlayerPos;
                    }

                    foreach (var (farmer, wasHidden, oldPos) in otherFarmers)
                    {
                        farmer.hidden.Value = wasHidden;
                        farmer.Position = oldPos;
                    }

                    foreach (var (npc, wasInvisible, oldPos) in oldNpcStates)
                    {
                        npc.isInvisible.Value = wasInvisible;
                        npc.Position = oldPos;
                    }

                    foreach (var (animal, oldPos) in oldAnimalStates)
                    {
                        animal.Position = oldPos;
                    }

                    location.critters = oldCritters;
                    location.TemporarySprites.AddRange(oldTempSprites);
                }

                using var finalTarget = new RenderTarget2D(
                    graphicsDevice,
                    targetResolution,
                    targetResolution,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None,
                    0,
                    RenderTargetUsage.PreserveContents
                );

                graphicsDevice.SetRenderTarget(finalTarget);
                graphicsDevice.Clear(Color.Transparent);

                using (var scaleBatch = new SpriteBatch(graphicsDevice))
                {
                    scaleBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, null, null);
                    scaleBatch.Draw(rawTarget, new Rectangle(0, 0, targetResolution, targetResolution), Color.White);
                    scaleBatch.End();
                }

                graphicsDevice.SetRenderTargets(prevTargets);
                graphicsDevice.Viewport = prevDeviceViewport;

                string currentSeason = (Game1.currentSeason ?? "spring").ToLowerInvariant();
                string bgDir = Path.Combine(helper.DirectoryPath, "assets", "backgrounds");
                Directory.CreateDirectory(bgDir);

                string seasonalFileName = $"custom_bg_{currentSeason}.png";
                string seasonalPath = Path.Combine(bgDir, seasonalFileName);
                using (var fs = new FileStream(seasonalPath, FileMode.Create, FileAccess.Write))
                {
                    finalTarget.SaveAsPng(fs, targetResolution, targetResolution);
                }

                string globalPath = Path.Combine(bgDir, "custom_bg.png");
                using (var fs = new FileStream(globalPath, FileMode.Create, FileAccess.Write))
                {
                    finalTarget.SaveAsPng(fs, targetResolution, targetResolution);
                }

                ModLogger.LogInfo(monitor, $"[StardewPresence] Custom background snapshot saved for season '{currentSeason}' at ({tileX},{tileY}) in '{location.NameOrUniqueName}'");

                config.UseCustomMapBackground = true;
                config.CustomMapLocation = location.NameOrUniqueName;
                config.CustomMapTileX = tileX;
                config.CustomMapTileY = tileY;
                config.CustomMapGridSize = gridSize;
                config.LastBackgroundCaptureTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                helper.WriteConfig(config);

                imageGenerator.InvalidateCache();

                if (playSoundAndToast)
                {
                    try { Game1.playSound("cameraNoise"); } catch { Game1.playSound("coin"); }
                    string successMsg = helper.Translation.Get("editor.bg_captured_toast")
                        .Default("Fondo del mapa capturado con exito!");
                    Game1.addHUDMessage(new HUDMessage(successMsg, HUDMessage.achievement_type));
                }

                return true;
            }
            catch (Exception ex)
            {
                graphicsDevice.SetRenderTargets(prevTargets);
                graphicsDevice.Viewport = prevDeviceViewport;
                monitor.Log($"[StardewPresence] Error capturing map spot: {ex}", LogLevel.Error);
                return false;
            }
        }

        public static bool TryRefreshConfiguredSpot(
            IModHelper helper,
            IMonitor monitor,
            ModConfig config,
            FarmerImageGenerator imageGenerator,
            bool notify = false)
        {
            if (!config.UseCustomMapBackground || string.IsNullOrWhiteSpace(config.CustomMapLocation))
            {
                return false;
            }

            GameLocation? targetLoc = FindLocation(config.CustomMapLocation);
            if (targetLoc == null)
            {
                monitor.Log($"[StardewPresence] Could not find saved map location '{config.CustomMapLocation}' to refresh.", LogLevel.Warn);
                return false;
            }

            return CaptureSpot(
                helper,
                monitor,
                config,
                imageGenerator,
                targetLoc,
                config.CustomMapTileX,
                config.CustomMapTileY,
                config.CustomMapGridSize > 0 ? config.CustomMapGridSize : 6,
                playSoundAndToast: notify
            );
        }

        public static GameLocation? FindLocation(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName)) return null;

            if (Game1.currentLocation != null &&
                (Game1.currentLocation.NameOrUniqueName.Equals(locationName, StringComparison.OrdinalIgnoreCase) ||
                 Game1.currentLocation.Name.Equals(locationName, StringComparison.OrdinalIgnoreCase)))
            {
                return Game1.currentLocation;
            }

            var loc = Game1.getLocationFromName(locationName);
            if (loc != null) return loc;

            foreach (var l in Game1.locations)
            {
                if (l.NameOrUniqueName.Equals(locationName, StringComparison.OrdinalIgnoreCase) ||
                    l.Name.Equals(locationName, StringComparison.OrdinalIgnoreCase))
                {
                    return l;
                }

                if (l.buildings != null)
                {
                    foreach (var building in l.buildings)
                    {
                        var indoors = building.indoors.Value;
                        if (indoors != null &&
                            (indoors.NameOrUniqueName.Equals(locationName, StringComparison.OrdinalIgnoreCase) ||
                             indoors.Name.Equals(locationName, StringComparison.OrdinalIgnoreCase)))
                        {
                            return indoors;
                        }
                    }
                }
            }

            return null;
        }
    }
}
