using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewPresence.Framework.Models;
using StardewPresence.Framework.Services;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewPresence.Framework.Menus
{
    public class MapBackgroundSelectorMenu : IClickableMenu
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ModConfig config;
        private readonly FarmerImageGenerator imageGenerator;
        private readonly Action onConfigSaved;
        private readonly IClickableMenu? returnMenu;

        public UILayout Layout { get; private set; }
        private FileSystemWatcher? layoutWatcher;
        private FileSystemWatcher? devLayoutWatcher;
        private DateTime lastReloadTime = DateTime.MinValue;

        // Camera / Viewport State
        private readonly xTile.Dimensions.Rectangle savedViewport;
        private readonly bool savedViewportFreeze;
        private readonly bool savedDisplayHUD;
        private Vector2 cameraPos;
        private const float PanSpeed = 16f;
        private const float FastPanSpeed = 32f;

        // Grid Selection State
        private int gridSizeInTiles = 4;
        private static readonly int[] AvailableGridSizes = { 3, 4, 5, 6, 8 };
        private int currentGridSizeIndex = 1;
        private int currentSelectionTileX;
        private int currentSelectionTileY;

        // Mouse Drag Panning State
        private bool isDragging;
        private Point dragStartMouse;
        private Vector2 dragStartCamera;
        private bool hasDraggedSignificantly;

        // UI Components
        private ClickableComponent btnCapture = null!;
        private ClickableComponent btnCancel = null!;
        private ClickableComponent btnSize = null!;
        private ClickableComponent btnMode = null!;

        // Placement Tile Source Rectangle in Game1.mouseCursors
        private static readonly Rectangle PlacementTileSource = new Rectangle(194, 388, 16, 16);
        private static readonly Rectangle YellowButtonSourceRect = new Rectangle(432, 439, 9, 9);

        public MapBackgroundSelectorMenu(
            IModHelper helper,
            IMonitor monitor,
            ModConfig config,
            FarmerImageGenerator imageGenerator,
            Action onConfigSaved,
            IClickableMenu? returnMenu = null)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, false)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
            this.imageGenerator = imageGenerator;
            this.onConfigSaved = onConfigSaved;
            this.returnMenu = returnMenu;
            this.Layout = UILayout.Load(helper.DirectoryPath);

            InitHotReloadWatcher();

            this.savedViewport = Game1.viewport;
            this.savedViewportFreeze = Game1.viewportFreeze;
            this.savedDisplayHUD = Game1.displayHUD;

            Game1.viewportFreeze = true;
            Game1.displayHUD = false;

            if (config.CustomMapGridSize > 0)
            {
                this.gridSizeInTiles = config.CustomMapGridSize;
            }

            if (!string.IsNullOrWhiteSpace(config.CustomMapLocation) &&
                config.CustomMapTileX > 0 && config.CustomMapTileY > 0 &&
                Game1.currentLocation != null &&
                (Game1.currentLocation.NameOrUniqueName.Equals(config.CustomMapLocation, StringComparison.OrdinalIgnoreCase) ||
                 Game1.currentLocation.Name.Equals(config.CustomMapLocation, StringComparison.OrdinalIgnoreCase)))
            {
                currentSelectionTileX = config.CustomMapTileX;
                currentSelectionTileY = config.CustomMapTileY;
                int centerMapX = (currentSelectionTileX * 64) + (gridSizeInTiles * 32) - (Game1.viewport.Width / 2);
                int centerMapY = (currentSelectionTileY * 64) + (gridSizeInTiles * 32) - (Game1.viewport.Height / 2);
                cameraPos = new Vector2(centerMapX, centerMapY);
            }
            else if (Game1.player != null)
            {
                int playerCenterMapX = (int)Game1.player.Position.X - (Game1.viewport.Width / 2);
                int playerCenterMapY = (int)Game1.player.Position.Y - (Game1.viewport.Height / 2);
                cameraPos = new Vector2(playerCenterMapX, playerCenterMapY);
                currentSelectionTileX = (int)(Game1.player.Position.X / 64f) - (gridSizeInTiles / 2);
                currentSelectionTileY = (int)(Game1.player.Position.Y / 64f) - (gridSizeInTiles / 2);
            }
            else
            {
                cameraPos = new Vector2(Game1.viewport.X, Game1.viewport.Y);
                currentSelectionTileX = (Game1.viewport.X + Game1.viewport.Width / 2) / 64 - (gridSizeInTiles / 2);
                currentSelectionTileY = (Game1.viewport.Y + Game1.viewport.Height / 2) / 64 - (gridSizeInTiles / 2);
            }

            ClampCamera();
            ClampSelectionTile();
            Game1.viewport.X = (int)cameraPos.X;
            Game1.viewport.Y = (int)cameraPos.Y;

            InitButtons();
        }

        private void InitHotReloadWatcher()
        {
            try
            {
                string watchDir = helper.DirectoryPath;
                if (Directory.Exists(watchDir))
                {
                    layoutWatcher = new FileSystemWatcher(watchDir, "ui_layout.json")
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                        EnableRaisingEvents = true
                    };
                    layoutWatcher.Changed += OnLayoutFileChanged;
                    layoutWatcher.Created += OnLayoutFileChanged;
                }

                string devDir = @"C:\Users\ale_y\Documents\Stardew-Presence\StardewPresence";
                if (!Directory.Exists(devDir))
                {
                    devDir = @"C:\Users\ale_y\Documents\Stardew-Presence";
                }
                if (!Directory.Exists(devDir))
                {
                    devDir = @"C:\Users\ale_y\Documents\Stardew-Dev\StardewPresence";
                }
                if (Directory.Exists(devDir) && !devDir.Equals(watchDir, StringComparison.OrdinalIgnoreCase))
                {
                    devLayoutWatcher = new FileSystemWatcher(devDir, "ui_layout.json")
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                        EnableRaisingEvents = true
                    };
                    devLayoutWatcher.Changed += OnLayoutFileChanged;
                    devLayoutWatcher.Created += OnLayoutFileChanged;
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogTrace(monitor, $"[Hot-Reload] FileSystemWatcher notice: {ex.Message}");
            }
        }

        private void OnLayoutFileChanged(object sender, FileSystemEventArgs e)
        {
            if ((DateTime.Now - lastReloadTime).TotalMilliseconds < 250) return;
            lastReloadTime = DateTime.Now;

            try
            {
                System.Threading.Thread.Sleep(50);
                Layout = UILayout.Load(helper.DirectoryPath);
                InitButtons();
                Game1.playSound("drumkit6");
                ModLogger.LogInfo(monitor, "[Hot-Reload] MapBackgroundSelector layout reloaded.");
            }
            catch (Exception ex)
            {
                monitor.Log($"[Hot-Reload] Error reloading layout: {ex.Message}", LogLevel.Warn);
            }
        }

        private void InitButtons()
        {
            width = Game1.uiViewport.Width;
            height = Game1.uiViewport.Height;

            int btnW = Layout.BgSelectorButtonWidth > 0 ? Layout.BgSelectorButtonWidth : 240;
            int btnH = Layout.BgSelectorButtonHeight > 0 ? Layout.BgSelectorButtonHeight : 48;
            int margin = Layout.BgSelectorButtonMargin;
            int gap = Layout.BgSelectorButtonGap;
            int offsetY = Layout.BgSelectorButtonOffsetY;

            int bottomY = height - btnH - margin + offsetY;

            btnCancel = new ClickableComponent(new Rectangle(margin, bottomY, btnW, btnH), "cancel");
            btnMode = new ClickableComponent(new Rectangle(margin + btnW + gap, bottomY, btnW, btnH), "mode");
            btnCapture = new ClickableComponent(new Rectangle(width - btnW - margin, bottomY, btnW, btnH), "capture");
            btnSize = new ClickableComponent(new Rectangle(width - (btnW * 2) - gap - margin, bottomY, btnW, btnH), "size");
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            InitButtons();
        }

        private void ClampCamera()
        {
            if (Game1.currentLocation?.Map == null) return;

            int mapPixelWidth = Game1.currentLocation.Map.DisplayWidth;
            int mapPixelHeight = Game1.currentLocation.Map.DisplayHeight;

            int maxCameraX = Math.Max(0, mapPixelWidth - Game1.viewport.Width);
            int maxCameraY = Math.Max(0, mapPixelHeight - Game1.viewport.Height);

            cameraPos.X = Math.Clamp(cameraPos.X, 0, maxCameraX);
            cameraPos.Y = Math.Clamp(cameraPos.Y, 0, maxCameraY);
        }

        private void ClampSelectionTile()
        {
            if (Game1.currentLocation?.Map != null && Game1.currentLocation.Map.Layers.Count > 0)
            {
                int maxTileX = Math.Max(0, Game1.currentLocation.Map.Layers[0].LayerWidth - gridSizeInTiles);
                int maxTileY = Math.Max(0, Game1.currentLocation.Map.Layers[0].LayerHeight - gridSizeInTiles);
                currentSelectionTileX = Math.Clamp(currentSelectionTileX, 0, maxTileX);
                currentSelectionTileY = Math.Clamp(currentSelectionTileY, 0, maxTileY);
            }
        }

        private bool IsHoveringAnyHUD(int x, int y)
        {
            int bannerW = Math.Min(Layout.BgSelectorTopBannerWidth > 0 ? Layout.BgSelectorTopBannerWidth : 780, width - 40);
            int bannerH = Layout.BgSelectorTopBannerHeight > 0 ? Layout.BgSelectorTopBannerHeight : 75;
            int bannerX = (width - bannerW) / 2;
            int bannerY = Layout.BgSelectorTopBannerY;
            Rectangle topBannerRect = new Rectangle(bannerX, bannerY, bannerW, bannerH);

            return btnCancel.containsPoint(x, y) ||
                   btnCapture.containsPoint(x, y) ||
                   btnSize.containsPoint(x, y) ||
                   btnMode.containsPoint(x, y) ||
                   topBannerRect.Contains(x, y);
        }

        public override void update(GameTime time)
        {
            base.update(time);

            var kb = Keyboard.GetState();
            float speed = (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift)) ? FastPanSpeed : PanSpeed;

            Vector2 move = Vector2.Zero;
            if (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up) || Game1.isOneOfTheseKeysDown(kb, Game1.options.moveUpButton)) move.Y -= 1f;
            if (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down) || Game1.isOneOfTheseKeysDown(kb, Game1.options.moveDownButton)) move.Y += 1f;
            if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left) || Game1.isOneOfTheseKeysDown(kb, Game1.options.moveLeftButton)) move.X -= 1f;
            if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right) || Game1.isOneOfTheseKeysDown(kb, Game1.options.moveRightButton)) move.X += 1f;

            int screenMouseX = Game1.getMouseX(false);
            int screenMouseY = Game1.getMouseY(false);
            int edgeThreshold = 35;

            if (screenMouseX >= 0 && screenMouseX <= edgeThreshold) move.X -= 1f;
            else if (screenMouseX >= Game1.viewport.Width - edgeThreshold && screenMouseX <= Game1.viewport.Width) move.X += 1f;
            if (screenMouseY >= 0 && screenMouseY <= edgeThreshold) move.Y -= 1f;
            else if (screenMouseY >= Game1.viewport.Height - edgeThreshold && screenMouseY <= Game1.viewport.Height) move.Y += 1f;

            var mouseState = Mouse.GetState();
            if (mouseState.MiddleButton == ButtonState.Pressed || mouseState.RightButton == ButtonState.Pressed)
            {
                if (!isDragging)
                {
                    isDragging = true;
                    hasDraggedSignificantly = false;
                    dragStartMouse = new Point(mouseState.X, mouseState.Y);
                    dragStartCamera = cameraPos;
                }
                else
                {
                    int dx = mouseState.X - dragStartMouse.X;
                    int dy = mouseState.Y - dragStartMouse.Y;
                    if (Math.Abs(dx) > 4 || Math.Abs(dy) > 4)
                    {
                        hasDraggedSignificantly = true;
                    }
                    cameraPos = dragStartCamera - new Vector2(dx, dy);
                }
            }
            else
            {
                isDragging = false;
            }

            if (move != Vector2.Zero)
            {
                move.Normalize();
                cameraPos += move * speed;
            }

            ClampCamera();
            Game1.viewportFreeze = true;
            Game1.viewport.X = (int)cameraPos.X;
            Game1.viewport.Y = (int)cameraPos.Y;

            int uiMouseX = Game1.getMouseX(true);
            int uiMouseY = Game1.getMouseY(true);
            if (!IsHoveringAnyHUD(uiMouseX, uiMouseY) && !isDragging)
            {
                currentSelectionTileX = (int)Game1.currentCursorTile.X - (gridSizeInTiles / 2);
                currentSelectionTileY = (int)Game1.currentCursorTile.Y - (gridSizeInTiles / 2);
                ClampSelectionTile();
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            if (direction > 0)
            {
                currentGridSizeIndex = (currentGridSizeIndex + 1) % AvailableGridSizes.Length;
                gridSizeInTiles = AvailableGridSizes[currentGridSizeIndex];
                ClampSelectionTile();
                Game1.playSound("smallSelect");
            }
            else if (direction < 0)
            {
                currentGridSizeIndex = (currentGridSizeIndex - 1 + AvailableGridSizes.Length) % AvailableGridSizes.Length;
                gridSizeInTiles = AvailableGridSizes[currentGridSizeIndex];
                ClampSelectionTile();
                Game1.playSound("smallSelect");
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                CancelAndExit();
                return;
            }
            if (key == Keys.Enter || key == Keys.Space)
            {
                CaptureSelectedArea();
                return;
            }
            if (key == Keys.OemPlus || key == Keys.Add || key == Keys.E)
            {
                currentGridSizeIndex = (currentGridSizeIndex + 1) % AvailableGridSizes.Length;
                gridSizeInTiles = AvailableGridSizes[currentGridSizeIndex];
                ClampSelectionTile();
                Game1.playSound("smallSelect");
            }
            if (key == Keys.OemMinus || key == Keys.Subtract || key == Keys.Q)
            {
                currentGridSizeIndex = (currentGridSizeIndex - 1 + AvailableGridSizes.Length) % AvailableGridSizes.Length;
                gridSizeInTiles = AvailableGridSizes[currentGridSizeIndex];
                ClampSelectionTile();
                Game1.playSound("smallSelect");
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (btnCancel.containsPoint(x, y))
            {
                CancelAndExit();
                return;
            }

            if (btnCapture.containsPoint(x, y))
            {
                CaptureSelectedArea();
                return;
            }

            if (btnSize.containsPoint(x, y))
            {
                currentGridSizeIndex = (currentGridSizeIndex + 1) % AvailableGridSizes.Length;
                gridSizeInTiles = AvailableGridSizes[currentGridSizeIndex];
                ClampSelectionTile();
                Game1.playSound("smallSelect");
                return;
            }

            if (btnMode.containsPoint(x, y))
            {
                config.CustomBackgroundMode = config.CustomBackgroundMode.Equals("Single", StringComparison.OrdinalIgnoreCase)
                    ? "Seasonal" : "Single";
                Game1.playSound("coin");
                return;
            }

            if (!IsHoveringAnyHUD(x, y))
            {
                currentSelectionTileX = (int)Game1.currentCursorTile.X - (gridSizeInTiles / 2);
                currentSelectionTileY = (int)Game1.currentCursorTile.Y - (gridSizeInTiles / 2);
                ClampSelectionTile();
                CaptureSelectedArea();
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (!hasDraggedSignificantly)
            {
                CancelAndExit();
            }
            hasDraggedSignificantly = false;
        }

        private void CancelAndExit()
        {
            RestoreViewport();
            Game1.playSound("bigDeSelect");
            if (returnMenu != null)
            {
                Game1.activeClickableMenu = returnMenu;
            }
            else
            {
                exitThisMenu();
            }
        }

        private void RestoreViewport()
        {
            Game1.viewport = savedViewport;
            Game1.viewportFreeze = savedViewportFreeze;
            Game1.displayHUD = savedDisplayHUD;
        }

        private void CaptureSelectedArea()
        {
            if (Game1.currentLocation == null) return;

            ClampSelectionTile();

            bool success = MapBackgroundCaptureHelper.CaptureSpot(
                helper,
                monitor,
                config,
                imageGenerator,
                Game1.currentLocation,
                currentSelectionTileX,
                currentSelectionTileY,
                gridSizeInTiles,
                playSoundAndToast: true
            );

            if (success)
            {
                onConfigSaved?.Invoke();

                RestoreViewport();
                if (returnMenu != null)
                {
                    Game1.activeClickableMenu = returnMenu;
                }
                else
                {
                    exitThisMenu();
                }
            }
        }

        public override void draw(SpriteBatch b)
        {
            // Draw Building Placement Grid Overlay
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateScale(Game1.options.zoomLevel));

            Color gridTint = new Color(255, 65, 65, 175);
            for (int x = 0; x < gridSizeInTiles; x++)
            {
                for (int y = 0; y < gridSizeInTiles; y++)
                {
                    Vector2 localPos = Game1.GlobalToLocal(Game1.viewport, new Vector2((currentSelectionTileX + x) * 64, (currentSelectionTileY + y) * 64));

                    b.Draw(
                        Game1.mouseCursors,
                        localPos,
                        PlacementTileSource,
                        gridTint,
                        0f,
                        Vector2.Zero,
                        4f,
                        SpriteEffects.None,
                        0.9f
                    );
                }
            }

            Vector2 boxLocalPos = Game1.GlobalToLocal(Game1.viewport, new Vector2(currentSelectionTileX * 64, currentSelectionTileY * 64));
            int boxPixels = gridSizeInTiles * 64;
            DrawSelectionBorder(b, (int)boxLocalPos.X, (int)boxLocalPos.Y, boxPixels, boxPixels, Color.Red * 0.9f, 3);

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateScale(Game1.options.uiScale));

            DrawTopBanner(b);

#if DEBUG
            DrawDebugOverlay(b);
#endif

            DrawBottomHUD(b);
            drawMouse(b);
        }

        private void DrawDebugOverlay(SpriteBatch b)
        {
            int panelX = 20;
            int panelY = 20;
            int panelW = 320;
            int panelH = 160;

            b.Draw(Game1.fadeToBlackRect, new Rectangle(panelX, panelY, panelW, panelH), Color.Black * 0.85f);
            DrawSelectionBorder(b, panelX, panelY, panelW, panelH, Color.Goldenrod * 0.9f, 2);

            int textX = panelX + 14;
            int textY = panelY + 12;
            int lineH = 23;

            string locName = Game1.currentLocation?.NameOrUniqueName ?? "None";
            int curTileX = (int)Game1.currentCursorTile.X;
            int curTileY = (int)Game1.currentCursorTile.Y;

            Utility.drawTextWithShadow(b, $"Location: {locName}", Game1.smallFont, new Vector2(textX, textY), Color.Gold);
            Utility.drawTextWithShadow(b, $"Tile: ({currentSelectionTileX}, {currentSelectionTileY})", Game1.smallFont, new Vector2(textX, textY + lineH), Color.White);
            Utility.drawTextWithShadow(b, $"World: ({currentSelectionTileX * 64}, {currentSelectionTileY * 64})", Game1.smallFont, new Vector2(textX, textY + lineH * 2), Color.LightGray);
            Utility.drawTextWithShadow(b, $"Size: {gridSizeInTiles}x{gridSizeInTiles} ({gridSizeInTiles * 64}x{gridSizeInTiles * 64}px)", Game1.smallFont, new Vector2(textX, textY + lineH * 3), Color.Yellow);
            Utility.drawTextWithShadow(b, $"Cursor Tile: ({curTileX}, {curTileY})", Game1.smallFont, new Vector2(textX, textY + lineH * 4), Color.Cyan);
            Utility.drawTextWithShadow(b, $"Camera: ({(int)cameraPos.X}, {(int)cameraPos.Y})", Game1.smallFont, new Vector2(textX, textY + lineH * 5), Color.LightGreen);
        }

        private void DrawSelectionBorder(SpriteBatch b, int x, int y, int w, int h, Color color, int thickness)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(x, y, w, thickness), color);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(x, y + h - thickness, w, thickness), color);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(x, y, thickness, h), color);
            b.Draw(Game1.fadeToBlackRect, new Rectangle(x + w - thickness, y, thickness, h), color);
        }

        private void DrawTopBanner(SpriteBatch b)
        {
            string subText = helper.Translation.Get("editor.bg_selector_help")
                .Default("[ WASD / Flechas ] Mover Camara       [ Rueda / +/- ] Tamano       [ Clic / Enter ] Capturar");
            var subTextSize = Game1.smallFont.MeasureString(subText);

            int bannerW = Math.Min(Math.Max((int)subTextSize.X + 80, Layout.BgSelectorTopBannerWidth > 0 ? Layout.BgSelectorTopBannerWidth : 860), width - 40);
            int bannerH = Layout.BgSelectorTopBannerHeight > 0 ? Layout.BgSelectorTopBannerHeight : 92;
            int bannerX = (width - bannerW) / 2;
            int bannerY = Layout.BgSelectorTopBannerY;

            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                bannerX, bannerY, bannerW, bannerH, Color.White, 1f, true);

            string title = helper.Translation.Get("editor.bg_selector_title").Default("Selector de Fondo del Mapa");
            SpriteText.drawStringHorizontallyCenteredAt(b, title, bannerX + bannerW / 2, bannerY + 12);

            Color helpColor = ParseColor(Layout.BgSelectorHelpTextColor, Color.White);
            Vector2 textPos = new Vector2(bannerX + (bannerW - subTextSize.X) / 2f, bannerY + 52 + Layout.BgSelectorHelpTextOffsetY);
            Utility.drawTextWithShadow(b, subText, Game1.smallFont, textPos, helpColor);
        }

        private void DrawBottomHUD(SpriteBatch b)
        {
            int uiMouseX = Game1.getMouseX(true);
            int uiMouseY = Game1.getMouseY(true);

            string modeText = config.CustomBackgroundMode.Equals("Seasonal", StringComparison.OrdinalIgnoreCase)
                ? helper.Translation.Get("editor.bg_mode_seasonal").Default("Modo: Por Estacion")
                : helper.Translation.Get("editor.bg_mode_single").Default("Modo: Global");
            DrawYellowButton(b, btnMode.bounds, modeText, btnMode.containsPoint(uiMouseX, uiMouseY));

            string sizeText = $"{helper.Translation.Get("editor.bg_size_label").Default("Tamano")}: {gridSizeInTiles}x{gridSizeInTiles}";
            DrawYellowButton(b, btnSize.bounds, sizeText, btnSize.containsPoint(uiMouseX, uiMouseY));

            string cancelText = helper.Translation.Get("settings.cancel").Default("Cancelar (ESC)");
            DrawYellowButton(b, btnCancel.bounds, cancelText, btnCancel.containsPoint(uiMouseX, uiMouseY));

            string captureText = !string.IsNullOrWhiteSpace(Layout.BgSelectorCaptureButtonText)
                ? Layout.BgSelectorCaptureButtonText
                : helper.Translation.Get("editor.bg_capture_btn").Default("Select area");
            DrawYellowButton(b, btnCapture.bounds, captureText, btnCapture.containsPoint(uiMouseX, uiMouseY));
        }

        private static Color ParseColor(string colorName, Color defaultColor)
        {
            if (string.IsNullOrWhiteSpace(colorName)) return defaultColor;
            switch (colorName.Trim().ToLowerInvariant())
            {
                case "white": return Color.White;
                case "yellow": return Color.Gold;
                case "gold": return Color.Gold;
                case "cream": return new Color(255, 245, 200);
                case "default": return Game1.textColor;
                case "brown": return Game1.textColor;
                case "orange": return Color.Orange;
                case "cyan": return Color.Cyan;
                case "lime": return Color.LightGreen;
            }
            if (colorName.StartsWith("#") && (colorName.Length == 7 || colorName.Length == 9))
            {
                try
                {
                    uint rgba = Convert.ToUInt32(colorName.Substring(1), 16);
                    if (colorName.Length == 7) rgba = (rgba << 8) | 0xFF;
                    return new Color((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
                }
                catch
                {
                }
            }
            return defaultColor;
        }

        private static void DrawYellowButton(SpriteBatch b, Rectangle r, string text, bool hover)
        {
            if (hover)
            {
                r.X -= 2; r.Y -= 2; r.Width += 4; r.Height += 4;
            }

            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                YellowButtonSourceRect,
                r.X, r.Y, r.Width, r.Height,
                Color.White,
                4f,
                drawShadow: false
            );

            Vector2 sz = Game1.smallFont.MeasureString(text);
            Vector2 pos = new Vector2(
                r.X + (r.Width - sz.X) / 2f,
                r.Y + (r.Height - sz.Y) / 2f
            );

            b.DrawString(Game1.smallFont, text, pos + new Vector2(1, 1), Color.SaddleBrown * 0.5f);
            b.DrawString(Game1.smallFont, text, pos, Game1.textColor);
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            layoutWatcher?.Dispose();
            devLayoutWatcher?.Dispose();
            RestoreViewport();
        }
    }
}
