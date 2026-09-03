using System;
using System.Collections.Generic;
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
    public class PresenceSettingsMenu : IClickableMenu
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ModConfig liveConfig;
        private readonly ModConfig originalConfig;
        private readonly Action onConfigSaved;
        private readonly Action? onReturnToParentMenu;

        private static readonly Rectangle YellowButtonSourceRect = new Rectangle(432, 439, 9, 9);
        private static readonly Rectangle CheckboxUncheckedRect = new Rectangle(227, 425, 9, 9);
        private static readonly Rectangle CheckboxCheckedRect   = new Rectangle(236, 425, 9, 9);

        // Buttons
        private ClickableComponent btnCancel = null!;
        private ClickableComponent btnDefault = null!;
        private ClickableComponent btnSave = null!;
        private ClickableComponent btnSaveAndClose = null!;
        private ClickableTextureComponent closeButton = null!;

        // Settings items
        private class SettingItem
        {
            public string Label { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool IsCheckbox { get; set; }
            public Func<bool>? GetBoolValue { get; set; }
            public Action<bool>? SetBoolValue { get; set; }
            public Func<string>? GetOptionValue { get; set; }
            public Action? NextOption { get; set; }
            public Rectangle Bounds { get; set; }
            public Rectangle ControlBounds { get; set; }
        }

        private readonly List<SettingItem> settingsList = new();
        private int currentScroll;
        public UILayout Layout { get; private set; }

        private FileSystemWatcher? layoutWatcher;
        private FileSystemWatcher? devLayoutWatcher;
        private DateTime lastReloadTime = DateTime.MinValue;

        public PresenceSettingsMenu(
            IModHelper helper,
            IMonitor monitor,
            ModConfig config,
            Action onConfigSaved,
            Action? onReturnToParentMenu = null)
            : base(0, 0, 860, 580, true)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.liveConfig = config;
            this.originalConfig = config.Clone();
            this.onConfigSaved = onConfigSaved;
            this.onReturnToParentMenu = onReturnToParentMenu;

            this.Layout = UILayout.Load(helper.DirectoryPath);
            DoLayout();
            InitSettings();
            InitHotReloadWatcher();
        }

        private void DoLayout()
        {
            width = Layout.SettingsMenuWidth > 0 ? Layout.SettingsMenuWidth : 860;
            height = Layout.SettingsMenuHeight > 0 ? Layout.SettingsMenuHeight : 580;

            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

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
                ModLogger.LogTrace(monitor, $"[Hot-Reload] Could not start FileSystemWatcher in SettingsMenu: {ex.Message}");
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
                DoLayout();
                Game1.playSound("drumkit6");
                ModLogger.LogInfo(monitor, "[Hot-Reload] ui_layout.json reloaded in SettingsMenu.");
            }
            catch (Exception ex)
            {
                monitor.Log($"[Hot-Reload] Error reloading in SettingsMenu: {ex.Message}", LogLevel.Warn);
            }
        }

        private void InitSettings()
        {
            settingsList.Clear();

            // 1. Title Screen Logo
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.logo").ToString(),
                IsCheckbox = false,
                GetOptionValue = () =>
                {
                    return liveConfig.TitleScreenLogo switch
                    {
                        "smapi" => helper.Translation.Get("settings.logo_smapi").ToString(),
                        "vanilla" => helper.Translation.Get("settings.logo_vanilla").ToString(),
                        "expanded" => helper.Translation.Get("settings.logo_expanded").ToString(),
                        _ => helper.Translation.Get("settings.logo_auto").ToString()
                    };
                },
                NextOption = () =>
                {
                    liveConfig.TitleScreenLogo = liveConfig.TitleScreenLogo switch
                    {
                        "auto" => "smapi",
                        "smapi" => "vanilla",
                        "vanilla" => "expanded",
                        _ => "auto"
                    };
                }
            });

            // 2. Hide NPC Houses
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.hide_npc_houses").ToString(),
                IsCheckbox = true,
                GetBoolValue = () => liveConfig.HideNpcHouses,
                SetBoolValue = (val) => liveConfig.HideNpcHouses = val
            });

            // 3. Show Event Details
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.show_event_details").ToString(),
                IsCheckbox = true,
                GetBoolValue = () => liveConfig.ShowEventDetails,
                SetBoolValue = (val) => liveConfig.ShowEventDetails = val
            });

            // 4. Show Farm Name
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.show_farm_name").ToString(),
                IsCheckbox = true,
                GetBoolValue = () => liveConfig.ShowFarmName,
                SetBoolValue = (val) => liveConfig.ShowFarmName = val
            });

            // 5. Show Money
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.show_money").ToString(),
                IsCheckbox = true,
                GetBoolValue = () => liveConfig.ShowMoney,
                SetBoolValue = (val) => liveConfig.ShowMoney = val
            });

            // 6. Show Elapsed Time
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.show_elapsed_time").ToString(),
                IsCheckbox = true,
                GetBoolValue = () => liveConfig.ShowElapsedTime,
                SetBoolValue = (val) => liveConfig.ShowElapsedTime = val
            });

            // 7. Show Mod Count
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.show_mod_count").ToString(),
                IsCheckbox = true,
                GetBoolValue = () => liveConfig.ShowModCount,
                SetBoolValue = (val) => liveConfig.ShowModCount = val
            });

            // 8. Show Multiplayer
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.show_multiplayer").ToString(),
                IsCheckbox = true,
                GetBoolValue = () => liveConfig.ShowMultiplayer,
                SetBoolValue = (val) => liveConfig.ShowMultiplayer = val
            });

            // 9. Show Companion
            settingsList.Add(new SettingItem
            {
                Label = helper.Translation.Get("settings.show_spouse").ToString(),
                IsCheckbox = true,
                GetBoolValue = () => liveConfig.ShowCompanion,
                SetBoolValue = (val) => liveConfig.ShowCompanion = val
            });
        }

        private void InitButtons()
        {
            closeButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + width - 36, yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                4f);

            int btnW = Layout.SettingsMenuButtonWidth > 0 ? Layout.SettingsMenuButtonWidth : 180;
            int btnH = Layout.SettingsMenuButtonHeight > 0 ? Layout.SettingsMenuButtonHeight : 44;
            int gap = Layout.SettingsMenuButtonGap > 0 ? Layout.SettingsMenuButtonGap : 16;
            int btnY = yPositionOnScreen + height + Layout.SettingsMenuButtonsOffsetY;
            int totalW = (btnW * 4) + (gap * 3);
            int startX = xPositionOnScreen + (width - totalW) / 2;

            btnCancel = new ClickableComponent(new Rectangle(startX, btnY, btnW, btnH), "cancel");
            btnDefault = new ClickableComponent(new Rectangle(startX + (btnW + gap), btnY, btnW, btnH), "default");
            btnSave = new ClickableComponent(new Rectangle(startX + (btnW + gap) * 2, btnY, btnW, btnH), "save");
            btnSaveAndClose = new ClickableComponent(new Rectangle(startX + (btnW + gap) * 3, btnY, btnW, btnH), "save_close");
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (closeButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");
                ExitMenu();
                return;
            }

            if (btnCancel.containsPoint(x, y))
            {
                liveConfig.CopyFrom(originalConfig);
                Game1.playSound("bigDeSelect");
                ExitMenu();
                return;
            }

            if (btnDefault.containsPoint(x, y))
            {
                var def = new ModConfig();
                liveConfig.TitleScreenLogo = def.TitleScreenLogo;
                liveConfig.HideNpcHouses = def.HideNpcHouses;
                liveConfig.ShowFarmName = def.ShowFarmName;
                liveConfig.ShowMoney = def.ShowMoney;
                liveConfig.ShowElapsedTime = def.ShowElapsedTime;
                liveConfig.ShowModCount = def.ShowModCount;
                liveConfig.ShowEventDetails = def.ShowEventDetails;
                liveConfig.ShowMultiplayer = def.ShowMultiplayer;
                liveConfig.ShowCompanion = def.ShowCompanion;
                Game1.playSound("drumkit6");
                return;
            }

            if (btnSave.containsPoint(x, y))
            {
                helper.WriteConfig(liveConfig);
                onConfigSaved?.Invoke();
                Game1.playSound("coin");
                return;
            }

            if (btnSaveAndClose.containsPoint(x, y))
            {
                helper.WriteConfig(liveConfig);
                onConfigSaved?.Invoke();
                Game1.playSound("money");
                ExitMenu();
                return;
            }

            int itemHeight = Layout.SettingsMenuItemHeight > 0 ? Layout.SettingsMenuItemHeight : 56;
            int maxVisible = Layout.SettingsMenuMaxVisibleItems > 0 ? Layout.SettingsMenuMaxVisibleItems : 7;
            int padLeft = Layout.SettingsMenuListPadLeft > 0 ? Layout.SettingsMenuListPadLeft : 50;
            int padRight = Layout.SettingsMenuListPadRight > 0 ? Layout.SettingsMenuListPadRight : 50;
            int listY = yPositionOnScreen + (Layout.SettingsMenuListPadTop > 0 ? Layout.SettingsMenuListPadTop : 105);

            int startIdx = currentScroll;
            int count = Math.Min(maxVisible, settingsList.Count - startIdx);

            for (int i = 0; i < count; i++)
            {
                var item = settingsList[startIdx + i];
                int itemY = listY + (i * itemHeight);
                var rowRect = new Rectangle(xPositionOnScreen + padLeft, itemY, width - (padLeft + padRight), itemHeight);

                if (rowRect.Contains(x, y))
                {
                    if (item.IsCheckbox)
                    {
                        bool cur = item.GetBoolValue?.Invoke() ?? false;
                        item.SetBoolValue?.Invoke(!cur);
                        Game1.playSound("drumkit6");
                    }
                    else
                    {
                        item.NextOption?.Invoke();
                        Game1.playSound("smallSelect");
                    }
                    return;
                }
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            int maxVisible = Layout.SettingsMenuMaxVisibleItems > 0 ? Layout.SettingsMenuMaxVisibleItems : 7;
            if (direction > 0 && currentScroll > 0)
            {
                currentScroll--;
                Game1.playSound("shiny4");
            }
            else if (direction < 0 && currentScroll + maxVisible < settingsList.Count)
            {
                currentScroll++;
                Game1.playSound("shiny4");
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                ExitMenu();
                return;
            }
            base.receiveKeyPress(key);
        }

        private void ExitMenu()
        {
            layoutWatcher?.Dispose();
            devLayoutWatcher?.Dispose();

            if (onReturnToParentMenu != null)
            {
                onReturnToParentMenu.Invoke();
            }
            else
            {
                Game1.exitActiveMenu();
            }
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            layoutWatcher?.Dispose();
            devLayoutWatcher?.Dispose();
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            string title = !string.IsNullOrWhiteSpace(Layout.SettingsMenuTitleText)
                ? Layout.SettingsMenuTitleText
                : helper.Translation.Get("settings.title").Default("Presence Settings").ToString();
            SpriteText.drawStringWithScrollCenteredAt(b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 20);

            int itemHeight = Layout.SettingsMenuItemHeight > 0 ? Layout.SettingsMenuItemHeight : 56;
            int maxVisible = Layout.SettingsMenuMaxVisibleItems > 0 ? Layout.SettingsMenuMaxVisibleItems : 7;
            int padLeft = Layout.SettingsMenuListPadLeft > 0 ? Layout.SettingsMenuListPadLeft : 50;
            int padRight = Layout.SettingsMenuListPadRight > 0 ? Layout.SettingsMenuListPadRight : 50;
            int listY = yPositionOnScreen + (Layout.SettingsMenuListPadTop > 0 ? Layout.SettingsMenuListPadTop : 105);

            int startIdx = currentScroll;
            int count = Math.Min(maxVisible, settingsList.Count - startIdx);

            for (int i = 0; i < count; i++)
            {
                var item = settingsList[startIdx + i];
                int itemY = listY + (i * itemHeight);
                int rowW = width - (padLeft + padRight);

                bool isHovered = new Rectangle(xPositionOnScreen + padLeft, itemY, rowW, itemHeight).Contains(Game1.getOldMouseX(), Game1.getOldMouseY());
                if (isHovered)
                {
                    b.Draw(Game1.staminaRect, new Rectangle(xPositionOnScreen + padLeft - 5, itemY + 2, rowW + 10, itemHeight - 4), Color.Wheat * 0.25f);
                }

                Utility.drawTextWithShadow(b, item.Label, Game1.dialogueFont, new Vector2(xPositionOnScreen + padLeft + 10, itemY + 8), Game1.textColor);

                if (item.IsCheckbox)
                {
                    bool isChecked = item.GetBoolValue?.Invoke() ?? false;
                    int cbX = xPositionOnScreen + width - padRight - 60;
                    int cbY = itemY + 10;
                    Rectangle srcRect = isChecked ? CheckboxCheckedRect : CheckboxUncheckedRect;
                    b.Draw(Game1.mouseCursors, new Vector2(cbX, cbY), srcRect, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.9f);
                }
                else
                {
                    string optText = item.GetOptionValue?.Invoke() ?? string.Empty;
                    int btnW = 240;
                    int btnH = 40;
                    int btnX = xPositionOnScreen + width - btnW - padRight - 20;
                    int btnY = itemY + 6;

                    DrawYellowButton(b, new Rectangle(btnX, btnY, btnW, btnH), optText, isHovered);
                }
            }

            DrawYellowButton(b, btnCancel.bounds, helper.Translation.Get("settings.cancel").Default("Cancel"), btnCancel.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()));
            DrawYellowButton(b, btnDefault.bounds, helper.Translation.Get("settings.default").Default("Default"), btnDefault.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()));
            DrawYellowButton(b, btnSave.bounds, helper.Translation.Get("settings.save").Default("Save"), btnSave.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()));
            DrawYellowButton(b, btnSaveAndClose.bounds, helper.Translation.Get("settings.save_close").Default("Save & Close"), btnSaveAndClose.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()));

            closeButton.draw(b);
            drawMouse(b);
        }

        private void DrawYellowButton(SpriteBatch b, Rectangle bounds, string text, bool isHovered)
        {
            Color tint = isHovered ? Color.Wheat : Color.White;
            DrawTextureBox(b, Game1.mouseCursors, YellowButtonSourceRect, bounds.X, bounds.Y, bounds.Width, bounds.Height, tint, 4f, false);

            Vector2 sz = Game1.dialogueFont.MeasureString(text);
            float scale = 1f;
            if (sz.X > bounds.Width - 16)
            {
                scale = (bounds.Width - 16) / sz.X;
            }

            float tx = bounds.X + (bounds.Width - sz.X * scale) / 2f;
            float ty = bounds.Y + (bounds.Height - sz.Y * scale) / 2f;

            Utility.drawTextWithShadow(b, text, Game1.dialogueFont, new Vector2(tx, ty), Game1.textColor, scale);
        }

        private void DrawTextureBox(SpriteBatch b, Texture2D texture, Rectangle sourceRect, int x, int y, int width, int height, Color color, float scale, bool drawShadow)
        {
            int cornerSize = 3;
            int scaledCorner = (int)(cornerSize * scale);

            b.Draw(texture, new Rectangle(x, y, scaledCorner, scaledCorner), new Rectangle(sourceRect.X, sourceRect.Y, cornerSize, cornerSize), color);
            b.Draw(texture, new Rectangle(x + width - scaledCorner, y, scaledCorner, scaledCorner), new Rectangle(sourceRect.X + 6, sourceRect.Y, cornerSize, cornerSize), color);
            b.Draw(texture, new Rectangle(x, y + height - scaledCorner, scaledCorner, scaledCorner), new Rectangle(sourceRect.X, sourceRect.Y + 6, cornerSize, cornerSize), color);
            b.Draw(texture, new Rectangle(x + width - scaledCorner, y + height - scaledCorner, scaledCorner, scaledCorner), new Rectangle(sourceRect.X + 6, sourceRect.Y + 6, cornerSize, cornerSize), color);

            b.Draw(texture, new Rectangle(x + scaledCorner, y, width - scaledCorner * 2, scaledCorner), new Rectangle(sourceRect.X + 3, sourceRect.Y, 3, cornerSize), color);
            b.Draw(texture, new Rectangle(x + scaledCorner, y + height - scaledCorner, width - scaledCorner * 2, scaledCorner), new Rectangle(sourceRect.X + 3, sourceRect.Y + 6, 3, cornerSize), color);
            b.Draw(texture, new Rectangle(x, y + scaledCorner, scaledCorner, height - scaledCorner * 2), new Rectangle(sourceRect.X, sourceRect.Y + 3, cornerSize, 3), color);
            b.Draw(texture, new Rectangle(x + width - scaledCorner, y + scaledCorner, scaledCorner, height - scaledCorner * 2), new Rectangle(sourceRect.X + 6, sourceRect.Y + 3, cornerSize, 3), color);

            b.Draw(texture, new Rectangle(x + scaledCorner, y + scaledCorner, width - scaledCorner * 2, height - scaledCorner * 2), new Rectangle(sourceRect.X + 3, sourceRect.Y + 3, 3, 3), color);
        }
    }
}
