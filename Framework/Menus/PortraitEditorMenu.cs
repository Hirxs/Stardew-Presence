using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewDiscordRPC.Framework.Models;
using StardewDiscordRPC.Framework.Rendering;
using StardewDiscordRPC.Framework.Services;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewDiscordRPC.Framework.Menus
{
    public class PortraitEditorMenu : IClickableMenu
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ModConfig liveConfig;
        private readonly ModConfig config;
        private readonly FarmerImageGenerator imageGenerator;
        private readonly Action onConfigSaved;

        private int selectedTarget; // 0 = Farmer, 1 = Spouse, 2 = Pet

        public UILayout Layout { get; private set; }
        private FileSystemWatcher? layoutWatcher;
        private FileSystemWatcher? devLayoutWatcher;
        private DateTime lastReloadTime = DateTime.MinValue;

        // UI Components
        private Rectangle previewRect;
        private ClickableComponent btnBgSelect = null!;
        private ClickableComponent btnEmote = null!;
        private ClickableComponent btnSettings = null!;

        private ClickableComponent tabFarmer = null!;
        private ClickableComponent tabSpouse = null!;
        private ClickableComponent tabPet = null!;

        private ClickableTextureComponent dpadUp = null!;
        private ClickableTextureComponent dpadDown = null!;
        private ClickableTextureComponent dpadLeft = null!;
        private ClickableTextureComponent dpadRight = null!;

        private ClickableTextureComponent scaleMinus = null!;
        private ClickableTextureComponent scalePlus = null!;
        private ClickableTextureComponent frameMinus = null!;
        private ClickableTextureComponent framePlus = null!;

        private ClickableComponent btnVisible = null!;
        private ClickableComponent btnFlip = null!;
        private ClickableComponent btnLayer = null!;
        private ClickableComponent btnReset = null!;
        private ClickableComponent btnSave = null!;

        // Positions & Offsets
        private int titleBannerX, titleBannerY, titleBannerW;
        private int posHeaderX, posHeaderY;
        private int optHeaderX, optHeaderY;
        private int coordsTextX, coordsTextY;
        private int scaleTextX, scaleTextY;
        private int frameTextX, frameTextY;

        private RenderTarget2D? previewRT;
        private bool dirty = true;

        // Texture source rects from Game1.mouseCursors
        private static Rectangle SrcLeft  => Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44);
        private static Rectangle SrcRight => Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33);
        private static Rectangle SrcUp    => Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 12);
        private static Rectangle SrcDown  => Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 11);
        private static readonly Rectangle YellowButtonSourceRect = new Rectangle(432, 439, 9, 9);
        private static readonly Rectangle CheckboxUncheckedRect = new Rectangle(227, 425, 9, 9);
        private static readonly Rectangle CheckboxCheckedRect   = new Rectangle(236, 425, 9, 9);

        private (NPC? npc, Farmer? spouseFarmer) cachedSpouse;
        private NPC? cachedPet;

        public PortraitEditorMenu(
            IModHelper helper,
            IMonitor monitor,
            ModConfig config,
            FarmerImageGenerator imageGenerator,
            Action onConfigSaved)
            : base(0, 0, 940, 600, true)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.liveConfig = config;
            this.config = config.Clone();
            this.imageGenerator = imageGenerator;
            this.onConfigSaved = onConfigSaved;

            UpdateCompanionCache();

            this.config.ForcedSeason = "Auto";
            this.config.UseCustomMapBackground = true;

            Layout = UILayout.Load(helper.DirectoryPath);
            width = Layout.MenuWidth;
            height = Layout.MenuHeight;

            InitHotReloadWatcher();
            DoLayout();
        }

        private void UpdateCompanionCache()
        {
            var (spNpc, spFarmer, petNpc) = CompanionResolver.GetCompanions(Game1.player, config);
            cachedSpouse = (spNpc, spFarmer);
            cachedPet = petNpc;
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

                string devDir = @"C:\Users\ale_y\Documents\Stardew-Presence";
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
                width = Layout.MenuWidth;
                height = Layout.MenuHeight;
                DoLayout();
                Game1.playSound("drumkit6");
                ModLogger.LogInfo(monitor, "[Hot-Reload] ui_layout.json reloaded in editor.");
            }
            catch (Exception ex)
            {
                monitor.Log($"[Hot-Reload] Error reloading layout: {ex.Message}", LogLevel.Warn);
            }
        }

        public void ReloadLayoutManual()
        {
            Layout = UILayout.Load(helper.DirectoryPath);
            width = Layout.MenuWidth;
            height = Layout.MenuHeight;
            DoLayout();
            Game1.playSound("coin");
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            DoLayout();
        }

        public void DoLayout()
        {
            width = Layout.MenuWidth;
            height = Layout.MenuHeight;

            Vector2 c = Utility.getTopLeftPositionForCenteringOnScreen(width, height);
            xPositionOnScreen = (int)c.X;
            yPositionOnScreen = (int)c.Y;

            upperRightCloseButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + width - Layout.CloseButtonOffsetX, yPositionOnScreen - Layout.CloseButtonOffsetY, Layout.CloseButtonSize, Layout.CloseButtonSize),
                Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);

            string title = string.IsNullOrWhiteSpace(Layout.TitleText) ? "Stardew Presence" : Layout.TitleText;
            titleBannerW = Layout.TitleBannerWidth > 0
                ? Layout.TitleBannerWidth
                : (SpriteText.getWidthOfString(title) + 64);
            titleBannerX = xPositionOnScreen + (width - titleBannerW) / 2 + Layout.TitleBannerOffsetX;
            titleBannerY = yPositionOnScreen + Layout.TitleBannerY;

            int leftX = xPositionOnScreen + Layout.PreviewPadLeft;
            int previewTopY = yPositionOnScreen + Layout.PreviewTopY;
            previewRect = new Rectangle(leftX, previewTopY, Layout.PreviewSize, Layout.PreviewSize);

            int bgSelectButtonY = Layout.BgSelectButtonY > 0
                ? (yPositionOnScreen + Layout.BgSelectButtonY)
                : (previewRect.Bottom + 12);
            int currentLeftY = bgSelectButtonY + Layout.BgSelectButtonOffsetY;

            int bgSelectButtonW = Layout.BgSelectButtonWidth > 0 ? Layout.BgSelectButtonWidth : previewRect.Width;
            int bgSelectButtonX = previewRect.X + Layout.BgSelectButtonOffsetX;
            btnBgSelect = new ClickableComponent(
                new Rectangle(bgSelectButtonX, currentLeftY, bgSelectButtonW, Layout.BgSelectButtonHeight), "bg_select");
            currentLeftY += Layout.BgSelectButtonHeight;

            int emoteGap = Layout.EmoteButtonOffsetY != 0 ? Layout.EmoteButtonOffsetY : 6;
            currentLeftY += emoteGap;
            btnEmote = new ClickableComponent(
                new Rectangle(previewRect.X, currentLeftY, previewRect.Width, Layout.EmoteButtonHeight), "emote");
            currentLeftY += Layout.EmoteButtonHeight;

            int settingsGap = Layout.SettingsButtonOffsetY != 0 ? Layout.SettingsButtonOffsetY : 6;
            currentLeftY += settingsGap;
            int settingsButtonW = Layout.SettingsButtonWidth > 0 ? Layout.SettingsButtonWidth : previewRect.Width;
            int settingsButtonX = previewRect.X + Layout.SettingsButtonOffsetX;
            btnSettings = new ClickableComponent(
                new Rectangle(settingsButtonX, currentLeftY, settingsButtonW, Layout.SettingsButtonHeight), "settings");

            int rx = previewRect.Right + Layout.RightColumnPadLeft;
            int tabY = yPositionOnScreen + Layout.TabTopY;

            bool hasSpouse = (cachedSpouse.npc?.Sprite?.Texture != null) || (cachedSpouse.spouseFarmer != null);
            bool hasPet = cachedPet?.Sprite?.Texture != null;

            int totalTabWidth = Layout.TabButtonWidth * 2 + Layout.TabGap;

            if (hasSpouse && hasPet)
            {
                int gap = Layout.TabGap3 > 0 ? Layout.TabGap3 : 14;
                int tabW = (totalTabWidth - (gap * 2)) / 3;
                tabFarmer = new ClickableComponent(new Rectangle(rx, tabY, tabW, Layout.TabButtonHeight), "tab_farmer");
                tabSpouse = new ClickableComponent(new Rectangle(rx + tabW + gap, tabY, tabW, Layout.TabButtonHeight), "tab_spouse");
                tabPet    = new ClickableComponent(new Rectangle(rx + (tabW + gap) * 2, tabY, tabW, Layout.TabButtonHeight), "tab_pet");
            }
            else if (hasSpouse && !hasPet)
            {
                int tabW = Layout.TabButtonWidth;
                int gap = Layout.TabGap;
                tabFarmer = new ClickableComponent(new Rectangle(rx, tabY, tabW, Layout.TabButtonHeight), "tab_farmer");
                tabSpouse = new ClickableComponent(new Rectangle(rx + tabW + gap, tabY, tabW, Layout.TabButtonHeight), "tab_spouse");
                tabPet    = new ClickableComponent(new Rectangle(0, 0, 0, 0), "tab_pet");
                if (selectedTarget == 2) selectedTarget = 0;
            }
            else if (!hasSpouse && hasPet)
            {
                int tabW = Layout.TabButtonWidth;
                int gap = Layout.TabGap;
                tabFarmer = new ClickableComponent(new Rectangle(rx, tabY, tabW, Layout.TabButtonHeight), "tab_farmer");
                tabSpouse = new ClickableComponent(new Rectangle(0, 0, 0, 0), "tab_spouse");
                tabPet    = new ClickableComponent(new Rectangle(rx + tabW + gap, tabY, tabW, Layout.TabButtonHeight), "tab_pet");
                if (selectedTarget == 1) selectedTarget = 0;
            }
            else
            {
                selectedTarget = 0;
                tabFarmer = new ClickableComponent(new Rectangle(rx, tabY, totalTabWidth, Layout.TabButtonHeight), "tab_farmer");
                tabSpouse = new ClickableComponent(new Rectangle(0, 0, 0, 0), "tab_spouse");
                tabPet    = new ClickableComponent(new Rectangle(0, 0, 0, 0), "tab_pet");
            }

            posHeaderX = rx + Layout.PositionHeaderOffsetX;
            posHeaderY = Layout.PositionHeaderY > 0
                ? (yPositionOnScreen + Layout.PositionHeaderY)
                : (tabY + Layout.TabButtonHeight + Layout.PositionHeaderOffsetY);

            int dpadCenterX = rx + Layout.DpadCenterOffsetX;
            int dpadCenterY = Layout.DpadCenterOffsetY > 100
                ? (yPositionOnScreen + Layout.DpadCenterOffsetY)
                : (posHeaderY + 28 + Layout.DpadCenterOffsetY);
            int aw = Layout.DpadArrowSize;
            int gapArrows = Layout.DpadGap;

            dpadUp    = CreateArrow(dpadCenterX - aw / 2, dpadCenterY - aw - gapArrows, aw, SrcUp);
            dpadDown  = CreateArrow(dpadCenterX - aw / 2, dpadCenterY + gapArrows,      aw, SrcDown);
            dpadLeft  = CreateArrow(dpadCenterX - aw - gapArrows, dpadCenterY - aw / 2, aw, SrcLeft);
            dpadRight = CreateArrow(dpadCenterX + gapArrows,      dpadCenterY - aw / 2, aw, SrcRight);

            coordsTextX = rx + Layout.CoordsOffsetX;
            coordsTextY = dpadUp.bounds.Y + Layout.CoordsOffsetY;

            int scaleRowY = yPositionOnScreen + Layout.ScaleRowY;
            scaleMinus = CreateArrow(rx, scaleRowY, Layout.ScaleButtonSize, SrcLeft);
            scalePlus  = CreateArrow(rx + Layout.ScaleButtonGap, scaleRowY, Layout.ScaleButtonSize, SrcRight);

            scaleTextX = scalePlus.bounds.Right + Layout.ScaleTextOffsetX;
            scaleTextY = scaleRowY + Layout.ScaleTextOffsetY;

            int frameRowY = yPositionOnScreen + Layout.FrameRowY;
            frameMinus = CreateArrow(rx, frameRowY, Layout.FrameButtonSize, SrcLeft);
            framePlus  = CreateArrow(rx + Layout.FrameButtonGap, frameRowY, Layout.FrameButtonSize, SrcRight);

            frameTextX = framePlus.bounds.Right + Layout.FrameTextOffsetX;
            frameTextY = frameRowY + Layout.FrameTextOffsetY;

            optHeaderX = rx + Layout.OptionsHeaderOffsetX;
            optHeaderY = Layout.OptionsHeaderY > 0
                ? (yPositionOnScreen + Layout.OptionsHeaderY)
                : (Math.Max(scaleRowY, frameRowY) + Math.Max(Layout.ScaleButtonSize, Layout.FrameButtonSize) + Layout.OptionsHeaderOffsetY);

            int chkX = Layout.VisibleCheckboxX > 0 ? (xPositionOnScreen + Layout.VisibleCheckboxX) : (rx + Layout.VisibleCheckboxOffsetX);
            int chkY = Layout.VisibleCheckboxY > 0 ? (yPositionOnScreen + Layout.VisibleCheckboxY) : (optHeaderY + Layout.VisibleCheckboxOffsetY);
            int chkW = Layout.VisibleCheckboxWidth > 0 ? Layout.VisibleCheckboxWidth : 150;
            int chkH = Layout.VisibleCheckboxHeight > 0 ? Layout.VisibleCheckboxHeight : 36;
            btnVisible = new ClickableComponent(new Rectangle(chkX, chkY, chkW, chkH), "visible");

            int btnRow1Y = Layout.OptionsButtonRow1Y > 0
                ? (yPositionOnScreen + Layout.OptionsButtonRow1Y)
                : (optHeaderY + Layout.OptionsButtonRow1OffsetY);
            btnFlip  = new ClickableComponent(new Rectangle(rx, btnRow1Y, Layout.OptionsButtonWidth, Layout.OptionsButtonHeight), "flip");
            btnLayer = new ClickableComponent(new Rectangle(rx + Layout.OptionsButtonWidth + Layout.OptionsButtonGap, btnRow1Y, Layout.OptionsButtonWidth, Layout.OptionsButtonHeight), "layer");

            int btnRow2Y = Layout.OptionsButtonRow2Y > 0
                ? (yPositionOnScreen + Layout.OptionsButtonRow2Y)
                : (btnRow1Y + Layout.OptionsButtonHeight + Layout.OptionsButtonRow2OffsetY);
            btnReset = new ClickableComponent(new Rectangle(rx, btnRow2Y, Layout.OptionsButtonWidth, Layout.OptionsButtonHeight), "reset");
            btnSave  = new ClickableComponent(new Rectangle(rx + Layout.OptionsButtonWidth + Layout.OptionsButtonGap, btnRow2Y, Layout.OptionsButtonWidth, Layout.OptionsButtonHeight), "save");

            dirty = true;
        }

        private static ClickableTextureComponent CreateArrow(int x, int y, int size, Rectangle src)
        {
            float scale = (float)size / (float)Math.Max(src.Width, src.Height);
            int w = (int)Math.Round(src.Width * scale);
            int h = (int)Math.Round(src.Height * scale);
            return new ClickableTextureComponent(new Rectangle(x, y, w, h), Game1.mouseCursors, src, scale);
        }

        private string GetFlipText()
        {
            bool flip = selectedTarget switch
            {
                0 => config.FarmerFlip,
                1 => config.SpouseFlip,
                2 => config.PetFlip,
                _ => false
            };
            return flip
                ? helper.Translation.Get("editor.flip_yes")
                : helper.Translation.Get("editor.flip_no");
        }

        private string GetEmoteText()
        {
            int emote = selectedTarget switch
            {
                0 => config.FarmerEmote,
                1 => config.SpouseEmote,
                2 => config.PetEmote,
                _ => 0
            };

            string target = selectedTarget switch
            {
                0 => (!string.IsNullOrWhiteSpace(Layout.TabFarmerText) && !Layout.TabFarmerText.Equals("Player", StringComparison.OrdinalIgnoreCase)) ? Layout.TabFarmerText : helper.Translation.Get("editor.tab_farmer").ToString(),
                1 => (!string.IsNullOrWhiteSpace(Layout.TabSpouseText) && !Layout.TabSpouseText.Equals("Companion", StringComparison.OrdinalIgnoreCase)) ? Layout.TabSpouseText : (cachedSpouse.npc?.displayName ?? cachedSpouse.spouseFarmer?.Name ?? helper.Translation.Get("editor.tab_spouse").ToString()),
                2 => (!string.IsNullOrWhiteSpace(Layout.TabPetText)) ? Layout.TabPetText : helper.Translation.Get("editor.companion_pet_short").Default("Mascota").ToString(),
                _ => ""
            };

            string emoteName = emote switch
            {
                0 => helper.Translation.Get("editor.emote_none").ToString(),
                1 => helper.Translation.Get("editor.emote_heart").ToString(),
                2 => helper.Translation.Get("editor.emote_music").ToString(),
                3 => helper.Translation.Get("editor.emote_happy").ToString(),
                4 => helper.Translation.Get("editor.emote_sleep").ToString(),
                5 => helper.Translation.Get("editor.emote_exclamation").ToString(),
                6 => helper.Translation.Get("editor.emote_question").ToString(),
                7 => helper.Translation.Get("editor.emote_gamepad").ToString(),
                8 => helper.Translation.Get("editor.emote_blush").ToString(),
                _ => helper.Translation.Get("editor.emote_none").ToString()
            };

            if (emoteName.StartsWith("Emote:", StringComparison.OrdinalIgnoreCase))
            {
                emoteName = emoteName.Substring(6).Trim();
            }

            string locale = helper.Translation.Locale?.ToLowerInvariant() ?? "";
            bool isSpanish = locale.StartsWith("es");

            return isSpanish
                ? $"Emote {target}: {emoteName}"
                : $"{target} Emote: {emoteName}";
        }

        private string GetLayerText()
        {
            if (selectedTarget == 1 && config.CompanionType != 0)
            {
                return config.CompanionType switch
                {
                    1 => helper.Translation.Get("editor.companion_spouse"),
                    2 => helper.Translation.Get("editor.companion_pet"),
                    3 => helper.Translation.Get("editor.companion_horse"),
                    4 => helper.Translation.Get("editor.companion_none"),
                    _ => helper.Translation.Get("editor.companion_auto")
                };
            }

            bool front = selectedTarget == 2 ? config.PetLayerFront : config.SpouseLayerFront;
            return front
                ? helper.Translation.Get("editor.layer_front")
                : helper.Translation.Get("editor.layer_behind");
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            upperRightCloseButton?.tryHover(x, y, 0.2f);
            scaleMinus?.tryHover(x, y, 0.2f);
            scalePlus?.tryHover(x, y, 0.2f);
            frameMinus?.tryHover(x, y, 0.2f);
            framePlus?.tryHover(x, y, 0.2f);
            dpadUp?.tryHover(x, y, 0.2f);
            dpadDown?.tryHover(x, y, 0.2f);
            dpadLeft?.tryHover(x, y, 0.2f);
            dpadRight?.tryHover(x, y, 0.2f);
        }

        public override void receiveKeyPress(Keys key)
        {
            base.receiveKeyPress(key);
            if (key == Keys.F5)
            {
                ReloadLayoutManual();
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);
            bool shift = Game1.oldKBState.IsKeyDown(Keys.LeftShift) || Game1.oldKBState.IsKeyDown(Keys.RightShift);
            float step = shift ? 5f : 1f;

            if (tabFarmer.containsPoint(x, y))
            {
                selectedTarget = 0;
                Game1.playSound("smallSelect");
                return;
            }

            bool hasSpouse = (cachedSpouse.npc?.Sprite?.Texture != null) || (cachedSpouse.spouseFarmer != null);
            bool hasPet = cachedPet?.Sprite?.Texture != null;

            if (hasSpouse && tabSpouse.containsPoint(x, y))
            {
                selectedTarget = 1;
                Game1.playSound("smallSelect");
                return;
            }

            if (hasPet && tabPet.containsPoint(x, y))
            {
                selectedTarget = 2;
                Game1.playSound("smallSelect");
                return;
            }

            if (dpadUp.containsPoint(x, y))    { Nudge(0, -step); return; }
            if (dpadDown.containsPoint(x, y))  { Nudge(0, step); return; }
            if (dpadLeft.containsPoint(x, y))  { Nudge(-step, 0); return; }
            if (dpadRight.containsPoint(x, y)) { Nudge(step, 0); return; }

            if (scaleMinus.containsPoint(x, y)) { AdjScale(-1); return; }
            if (scalePlus.containsPoint(x, y))  { AdjScale(1); return; }
            if (frameMinus.containsPoint(x, y)) { AdjFrame(shift ? -5 : -1); return; }
            if (framePlus.containsPoint(x, y))  { AdjFrame(shift ? 5 : 1); return; }

            if (btnVisible.containsPoint(x, y))
            {
                if (selectedTarget == 0)
                    config.ShowFarmer = !config.ShowFarmer;
                else if (selectedTarget == 1)
                {
                    config.ShowSpouse = !config.ShowSpouse;
                    config.ShowCompanion = config.ShowSpouse;
                }
                else if (selectedTarget == 2)
                    config.ShowPet = !config.ShowPet;

                dirty = true;
                Game1.playSound("drumkit6");
                return;
            }

            if (btnFlip.containsPoint(x, y))
            {
                if (selectedTarget == 0)
                {
                    config.FarmerFlip = !config.FarmerFlip;
                    config.FarmerFacingDirection = FarmerSceneRenderer.GetFacingDirectionFromFrame(config.FarmerFrame, config.FarmerFlip);
                }
                else if (selectedTarget == 1)
                {
                    config.SpouseFlip = !config.SpouseFlip;
                    config.SpouseFacingDirection = FarmerSceneRenderer.GetFacingDirectionFromFrame(config.SpouseFrame, config.SpouseFlip);
                }
                else if (selectedTarget == 2)
                    config.PetFlip = !config.PetFlip;

                dirty = true;
                Game1.playSound("coin");
                return;
            }

            if (btnLayer.containsPoint(x, y))
            {
                if (selectedTarget == 1 && shift)
                {
                    config.CompanionType = (config.CompanionType + 1) % 5;
                    UpdateCompanionCache();
                    DoLayout();
                }
                else if (selectedTarget == 1)
                {
                    config.SpouseLayerFront = !config.SpouseLayerFront;
                }
                else if (selectedTarget == 2)
                {
                    config.PetLayerFront = !config.PetLayerFront;
                }
                dirty = true;
                Game1.playSound("coin");
                return;
            }

            if (btnBgSelect.containsPoint(x, y))
            {
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = new MapBackgroundSelectorMenu(
                    helper,
                    monitor,
                    config,
                    imageGenerator,
                    onConfigSaved: () =>
                    {
                        dirty = true;
                        imageGenerator.InvalidateCache();
                    },
                    returnMenu: this
                );
                return;
            }

            if (btnEmote.containsPoint(x, y))
            {
                if (selectedTarget == 0)
                    config.FarmerEmote = (config.FarmerEmote + 1) % 9;
                else if (selectedTarget == 1)
                    config.SpouseEmote = (config.SpouseEmote + 1) % 9;
                else if (selectedTarget == 2)
                    config.PetEmote = (config.PetEmote + 1) % 9;

                dirty = true;
                Game1.playSound("coin");
                return;
            }

            if (btnSettings.containsPoint(x, y))
            {
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = new PresenceSettingsMenu(
                    helper,
                    monitor,
                    liveConfig,
                    onConfigSaved: () =>
                    {
                        dirty = true;
                        onConfigSaved?.Invoke();
                    },
                    onReturnToParentMenu: () =>
                    {
                        Game1.activeClickableMenu = this;
                    }
                );
                return;
            }

            if (btnReset.containsPoint(x, y))
            {
                config.FarmerOffsetX = 0; config.FarmerOffsetY = 0; config.FarmerScale = 2.3f; config.FarmerFrame = 0; config.FarmerFlip = false;
                config.FarmerFacingDirection = FarmerSceneRenderer.GetFacingDirectionFromFrame(0, false); config.FarmerEmote = 0;
                config.SpouseOffsetX = -26; config.SpouseOffsetY = 0.5f; config.SpouseScale = 4; config.SpouseFrame = 0; config.SpouseFlip = false;
                config.SpouseFacingDirection = FarmerSceneRenderer.GetFacingDirectionFromFrame(0, false); config.SpouseEmote = 0; config.CompanionType = 0;
                config.SpouseLayerFront = false;

                bool hasBoth = (cachedSpouse.npc?.Sprite?.Texture != null || cachedSpouse.spouseFarmer != null) && cachedPet?.Sprite?.Texture != null;
                config.PetOffsetX = hasBoth ? 15f : -59f;
                config.PetOffsetY = hasBoth ? 10f : 6f;
                config.PetScale = 3.2f;
                config.PetFrame = 18;
                config.PetFlip = hasBoth;
                config.PetLayerFront = false;
                config.PetEmote = 0;

                config.ShowFarmer = true; config.ShowCompanion = true; config.ShowSpouse = true; config.ShowPet = true;
                config.ForcedSeason = "Auto";
                UpdateCompanionCache();
                DoLayout();
                dirty = true;
                Game1.playSound("grunt");
                return;
            }

            if (btnSave.containsPoint(x, y))
            {
                liveConfig.CopyFrom(config);
                helper.WriteConfig(liveConfig);
                imageGenerator.InvalidateCache();
                onConfigSaved();
                Game1.playSound("money");
                exitThisMenu();
                return;
            }
        }

        private void Nudge(float dx, float dy)
        {
            if (selectedTarget == 0) { config.FarmerOffsetX += dx; config.FarmerOffsetY += dy; }
            else if (selectedTarget == 1) { config.SpouseOffsetX += dx; config.SpouseOffsetY += dy; }
            else if (selectedTarget == 2) { config.PetOffsetX += dx; config.PetOffsetY += dy; }
            dirty = true; Game1.playSound("drumkit6");
        }

        private void AdjScale(int dir)
        {
            if (selectedTarget == 0)
                config.FarmerScale = MathF.Round(Math.Clamp(config.FarmerScale + dir * 0.1f, 1f, 6f), 1);
            else if (selectedTarget == 1)
                config.SpouseScale = MathF.Round(Math.Clamp(config.SpouseScale + dir * 0.2f, 1f, 8f), 1);
            else if (selectedTarget == 2)
                config.PetScale = MathF.Round(Math.Clamp(config.PetScale + dir * 0.1f, 0.5f, 6f), 1);
            dirty = true; Game1.playSound("smallSelect");
        }

        private int GetMaxFrames()
        {
            if (selectedTarget == 0) return 120;
            if (selectedTarget == 1)
            {
                if (cachedSpouse.spouseFarmer != null) return 120;
                if (cachedSpouse.npc?.Sprite?.Texture != null)
                {
                    int sw = cachedSpouse.npc.Sprite.SpriteWidth > 0 ? cachedSpouse.npc.Sprite.SpriteWidth : 16;
                    int sh = cachedSpouse.npc.Sprite.SpriteHeight > 0 ? cachedSpouse.npc.Sprite.SpriteHeight : 32;
                    int fpr = Math.Max(1, cachedSpouse.npc.Sprite.Texture.Width / sw);
                    int rows = Math.Max(1, cachedSpouse.npc.Sprite.Texture.Height / sh);
                    return Math.Max(1, fpr * rows);
                }
                return 32;
            }
            if (selectedTarget == 2)
            {
                if (cachedPet?.Sprite?.Texture != null)
                {
                    int sw = cachedPet.Sprite.SpriteWidth > 0 ? cachedPet.Sprite.SpriteWidth : 32;
                    int sh = cachedPet.Sprite.SpriteHeight > 0 ? cachedPet.Sprite.SpriteHeight : 32;
                    int fpr = Math.Max(1, cachedPet.Sprite.Texture.Width / sw);
                    int rows = Math.Max(1, cachedPet.Sprite.Texture.Height / sh);
                    return Math.Max(1, fpr * rows);
                }
                return 32;
            }
            return 32;
        }

        private void AdjFrame(int dir)
        {
            int max = GetMaxFrames();
            if (selectedTarget == 0)
            {
                config.FarmerFrame = Math.Clamp(config.FarmerFrame + dir, 0, 120);
                config.FarmerFacingDirection = FarmerSceneRenderer.GetFacingDirectionFromFrame(config.FarmerFrame, config.FarmerFlip);
            }
            else if (selectedTarget == 1)
            {
                config.SpouseFrame = Math.Clamp(config.SpouseFrame + dir, 0, max - 1);
                config.SpouseFacingDirection = FarmerSceneRenderer.GetFacingDirectionFromFrame(config.SpouseFrame, config.SpouseFlip);
            }
            else if (selectedTarget == 2)
                config.PetFrame = Math.Clamp(config.PetFrame + dir, 0, max - 1);
            dirty = true; Game1.playSound("smallSelect");
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.7f);
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            string title = string.IsNullOrWhiteSpace(Layout.TitleText) ? "Stardew Presence" : Layout.TitleText;
            int bHeight = Layout.TitleBannerHeight > 0 ? Layout.TitleBannerHeight : 65;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), titleBannerX, titleBannerY, titleBannerW, bHeight, Color.White, 1f, true);
            SpriteText.drawStringHorizontallyCenteredAt(b, title, titleBannerX + titleBannerW / 2, titleBannerY + 12);

            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                previewRect.X - 6, previewRect.Y - 6, previewRect.Width + 12, previewRect.Height + 12, Color.White, 1f, true);
            RenderPreview(b);

            string bgSelectText = helper.Translation.Get("editor.adjust_frame_btn").Default("Adjust Frame");
            DrawYellowButton(b, btnBgSelect.bounds, bgSelectText, true);

            DrawYellowButton(b, btnEmote.bounds, GetEmoteText(), true);

            string settingsText = !string.IsNullOrWhiteSpace(Layout.SettingsButtonText)
                ? Layout.SettingsButtonText
                : $"⚙ {helper.Translation.Get("editor.settings_btn").Default("Ajustes")}";
            DrawYellowButton(b, btnSettings.bounds, settingsText, true);

            bool hasSpouse = (cachedSpouse.npc?.Sprite?.Texture != null) || (cachedSpouse.spouseFarmer != null);
            bool hasPet = cachedPet?.Sprite?.Texture != null;

            if (selectedTarget == 1 && !hasSpouse) selectedTarget = 0;
            if (selectedTarget == 2 && !hasPet) selectedTarget = 0;

            float curX = selectedTarget switch { 0 => config.FarmerOffsetX, 1 => config.SpouseOffsetX, 2 => config.PetOffsetX, _ => 0f };
            float curY = selectedTarget switch { 0 => config.FarmerOffsetY, 1 => config.SpouseOffsetY, 2 => config.PetOffsetY, _ => 0f };
            float curS = selectedTarget switch { 0 => config.FarmerScale, 1 => config.SpouseScale, 2 => config.PetScale, _ => 1f };
            int curF   = selectedTarget switch { 0 => config.FarmerFrame, 1 => config.SpouseFrame, 2 => config.PetFrame, _ => 0 };
            int maxF   = GetMaxFrames();

            string fText = (!string.IsNullOrWhiteSpace(Layout.TabFarmerText) && !Layout.TabFarmerText.Equals("Player", StringComparison.OrdinalIgnoreCase))
                ? Layout.TabFarmerText
                : helper.Translation.Get("editor.tab_farmer").ToString();
            float tScale = Layout.TabTextScale > 0 ? Layout.TabTextScale : 1f;

            DrawYellowButton(b, tabFarmer.bounds, fText, selectedTarget == 0, Layout.TabTextOffsetX, Layout.TabTextOffsetY, tScale);
            if (hasSpouse)
            {
                string sText = (!string.IsNullOrWhiteSpace(Layout.TabSpouseText) && !Layout.TabSpouseText.Equals("Companion", StringComparison.OrdinalIgnoreCase))
                    ? Layout.TabSpouseText
                    : (cachedSpouse.npc?.displayName ?? cachedSpouse.spouseFarmer?.Name ?? helper.Translation.Get("editor.tab_spouse").ToString());
                DrawYellowButton(b, tabSpouse.bounds, sText, selectedTarget == 1, Layout.TabTextOffsetX, Layout.TabTextOffsetY, tScale);
            }
            if (hasPet)
            {
                string pText = (!string.IsNullOrWhiteSpace(Layout.TabPetText))
                    ? Layout.TabPetText
                    : helper.Translation.Get("editor.companion_pet_short").Default("Mascota").ToString();
                DrawYellowButton(b, tabPet.bounds, pText, selectedTarget == 2, Layout.TabTextOffsetX, Layout.TabTextOffsetY, tScale);
            }

            string posHeader = (!string.IsNullOrWhiteSpace(Layout.PositionHeaderText) && !Layout.PositionHeaderText.Equals("Position:", StringComparison.OrdinalIgnoreCase))
                ? Layout.PositionHeaderText
                : helper.Translation.Get("editor.position_header").Default("Position:").ToString();
            SpriteText.drawString(b, posHeader, posHeaderX, posHeaderY);

            dpadUp.draw(b);
            dpadDown.draw(b);
            dpadLeft.draw(b);
            dpadRight.draw(b);

            int lineSpacing = Layout.CoordsLineSpacing > 0 ? Layout.CoordsLineSpacing : 28;
            Utility.drawTextWithShadow(b, $"X: {curX:+0.0;-0.0;0.0}", Game1.smallFont, new Vector2(coordsTextX, coordsTextY), Game1.textColor);
            Utility.drawTextWithShadow(b, $"Y: {curY:+0.0;-0.0;0.0}", Game1.smallFont, new Vector2(coordsTextX, coordsTextY + lineSpacing), Game1.textColor);
            Utility.drawTextWithShadow(b, helper.Translation.Get("editor.shift_hint"),
                Game1.smallFont, new Vector2(coordsTextX, coordsTextY + lineSpacing * 2), Color.Gray);

            scaleMinus.draw(b);
            scalePlus.draw(b);
            string scaleLabel = helper.Translation.Get("editor.scale_label").Default("Scale").ToString();
            Utility.drawTextWithShadow(b, $"{curS:0.0}x   {scaleLabel}", Game1.smallFont, new Vector2(scaleTextX, scaleTextY), Game1.textColor);

            frameMinus.draw(b);
            framePlus.draw(b);
            string frameLabel = helper.Translation.Get("editor.frame_label").Default("Frame").ToString();
            Utility.drawTextWithShadow(b, $"{curF}/{maxF - 1} {frameLabel}", Game1.smallFont, new Vector2(frameTextX, frameTextY), Game1.textColor);

            string optHeader = (!string.IsNullOrWhiteSpace(Layout.OptionsHeaderText) && !Layout.OptionsHeaderText.Equals("Options:", StringComparison.OrdinalIgnoreCase) && !Layout.OptionsHeaderText.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                ? Layout.OptionsHeaderText
                : helper.Translation.Get("editor.options_header").Default("Options:").ToString();
            SpriteText.drawString(b, optHeader, optHeaderX, optHeaderY);

            bool isVis = selectedTarget switch
            {
                0 => config.ShowFarmer,
                1 => config.ShowSpouse && config.ShowCompanion,
                2 => config.ShowPet,
                _ => true
            };
            DrawCheckbox(b, btnVisible.bounds, helper.Translation.Get("editor.visible"), isVis);

            DrawYellowButton(b, btnFlip.bounds, GetFlipText(), true);
            DrawYellowButton(b, btnLayer.bounds, GetLayerText(), true);

            DrawYellowButton(b, btnReset.bounds, helper.Translation.Get("editor.reset"), true);
            DrawYellowButton(b, btnSave.bounds, helper.Translation.Get("editor.save"), true);

            upperRightCloseButton.draw(b);
            drawMouse(b);
        }

        private void DrawCheckbox(SpriteBatch b, Rectangle bounds, string text, bool isChecked)
        {
            Rectangle src = isChecked ? CheckboxCheckedRect : CheckboxUncheckedRect;
            int boxY = bounds.Y + (bounds.Height - 36) / 2;
            b.Draw(
                Game1.mouseCursors,
                new Vector2(bounds.X, boxY),
                src,
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.9f
            );

            Vector2 textPos = new Vector2(bounds.X + 44, boxY + 4);
            Utility.drawTextWithShadow(b, text, Game1.smallFont, textPos, isChecked ? Game1.textColor : Color.Gray);
        }

        private void DrawYellowButton(SpriteBatch b, Rectangle r, string text, bool isSelected, int extraOffsetX = 0, int extraOffsetY = 0, float customScale = 1.0f)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                YellowButtonSourceRect,
                r.X,
                r.Y,
                r.Width,
                r.Height,
                isSelected ? Color.White : (Color.DimGray * 0.7f),
                4f,
                true
            );

            Vector2 textSize = Game1.smallFont.MeasureString(text);
            float maxW = r.Width - 14f;
            float scale = customScale > 0 ? customScale : 1f;
            if (textSize.X * scale > maxW && textSize.X > 0)
            {
                scale = maxW / textSize.X;
            }

            float textX = r.X + (r.Width - textSize.X * scale) / 2f + Layout.ButtonTextOffsetX + extraOffsetX;
            float textY = r.Y + (r.Height - textSize.Y * scale) / 2f - 1f + Layout.ButtonTextOffsetY + extraOffsetY;

            Color textColor = isSelected ? Game1.textColor : (Game1.textColor * 0.6f);
            Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2((int)textX, (int)textY), textColor, scale);
        }

        private SpriteBatch? previewBatch;

        private void RenderPreview(SpriteBatch b)
        {
            Farmer farmer = Game1.player;
            if (farmer == null) return;

            if (previewRT == null || previewRT.IsDisposed)
            {
                previewRT = new RenderTarget2D(Game1.graphics.GraphicsDevice, 256, 256, false, SurfaceFormat.Color, DepthFormat.None);
                dirty = true;
            }

            if (previewBatch == null)
            {
                previewBatch = new SpriteBatch(Game1.graphics.GraphicsDevice);
            }

            if (dirty)
            {
                string season = (Game1.currentSeason ?? "spring").ToLowerInvariant();
                Texture2D? bg = imageGenerator.GetSeasonalBackgroundTexture(season);

                var (spouseNpc, spouseFarmer) = cachedSpouse;
                var petNpc = cachedPet;

                FarmerSceneRenderer.RenderSceneToTarget(
                    Game1.graphics.GraphicsDevice,
                    previewRT,
                    farmer,
                    spouseFarmer,
                    spouseNpc,
                    petNpc,
                    bg,
                    config,
                    Layout
                );

                dirty = false;
            }

            if (previewRT != null) b.Draw(previewRT, previewRect, Color.White);
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            try
            {
                layoutWatcher?.Dispose();
                layoutWatcher = null;
                devLayoutWatcher?.Dispose();
                devLayoutWatcher = null;
            }
            catch
            {
            }

            previewBatch?.Dispose();
            previewBatch = null;

            previewRT?.Dispose();
            previewRT = null;
        }
    }
}
