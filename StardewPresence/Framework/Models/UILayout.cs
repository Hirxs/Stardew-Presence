using System;
using System.IO;
using System.Text.Json;

namespace StardewPresence.Framework.Models
{
    public class UILayout
    {
        public int MenuWidth { get; set; } = 940;
        public int MenuHeight { get; set; } = 640;

        public string TitleText { get; set; } = "Stardew Presence";
        public float TitleTextScale { get; set; } = 1.0f;
        public int TitleBannerWidth { get; set; } = 0;
        public int TitleBannerHeight { get; set; } = 65;
        public int TitleBannerOffsetX { get; set; } = 0;
        public int TitleBannerY { get; set; } = -10;
        public int CloseButtonOffsetX { get; set; } = 30;
        public int CloseButtonOffsetY { get; set; } = -10;
        public int CloseButtonSize { get; set; } = 48;

        public int PreviewPadLeft { get; set; } = 130;
        public int PreviewTopY { get; set; } = 120;
        public int PreviewSize { get; set; } = 200;

        public int BgSelectButtonY { get; set; } = 0;
        public int BgSelectButtonOffsetY { get; set; } = 5;
        public int BgSelectButtonHeight { get; set; } = 38;
        public int BgSelectButtonWidth { get; set; } = 0;
        public int BgSelectButtonOffsetX { get; set; } = 0;

        public int EmoteButtonY { get; set; } = 0;
        public int EmoteButtonOffsetY { get; set; } = 5;
        public int EmoteButtonHeight { get; set; } = 38;

        public int SettingsButtonY { get; set; } = 0;
        public int SettingsButtonOffsetY { get; set; } = 5;
        public int SettingsButtonHeight { get; set; } = 38;
        public int SettingsButtonWidth { get; set; } = 0;
        public int SettingsButtonOffsetX { get; set; } = 0;
        public string SettingsButtonText { get; set; } = "";

        public string TabFarmerText { get; set; } = "";
        public string TabSpouseText { get; set; } = "";
        public string TabPetText { get; set; } = "";
        public float TabTextScale { get; set; } = 1.0f;
        public int RightColumnPadLeft { get; set; } = 28;
        public int TabTopY { get; set; } = 113;
        public int TabButtonWidth { get; set; } = 230;
        public int TabButtonHeight { get; set; } = 48;
        public int TabGap { get; set; } = 26;
        public int TabGap3 { get; set; } = 14;
        public int TabTextOffsetX { get; set; } = 0;
        public int TabTextOffsetY { get; set; } = 0;

        public string PositionHeaderText { get; set; } = "";
        public int PositionHeaderOffsetX { get; set; } = 0;
        public int PositionHeaderOffsetY { get; set; } = 10;
        public int PositionHeaderY { get; set; } = 171;

        public int DpadCenterOffsetX { get; set; } = 65;
        public int DpadCenterOffsetY { get; set; } = 275;
        public int DpadArrowSize { get; set; } = 62;
        public int DpadGap { get; set; } = 5;

        public int CoordsOffsetX { get; set; } = 145;
        public int CoordsOffsetY { get; set; } = 28;
        public int CoordsLineSpacing { get; set; } = 28;

        public int ScaleRowY { get; set; } = 375;
        public int ScaleButtonSize { get; set; } = 62;
        public int ScaleButtonGap { get; set; } = 70;
        public int ScaleTextOffsetX { get; set; } = 10;
        public int ScaleTextOffsetY { get; set; } = 17;

        public int FrameRowY { get; set; } = 325;
        public int FrameButtonSize { get; set; } = 62;
        public int FrameButtonGap { get; set; } = 70;
        public int FrameTextOffsetX { get; set; } = 10;
        public int FrameTextOffsetY { get; set; } = 17;

        public string OptionsHeaderText { get; set; } = "";
        public int OptionsHeaderOffsetX { get; set; } = 0;
        public int OptionsHeaderOffsetY { get; set; } = 30;
        public int OptionsHeaderY { get; set; } = 455;

        public int VisibleCheckboxX { get; set; } = 0;
        public int VisibleCheckboxY { get; set; } = 455;
        public int VisibleCheckboxOffsetX { get; set; } = 320;
        public int VisibleCheckboxOffsetY { get; set; } = 0;
        public int VisibleCheckboxWidth { get; set; } = 150;
        public int VisibleCheckboxHeight { get; set; } = 36;

        public int OptionsButtonRow1Y { get; set; } = 505;
        public int OptionsButtonRow1OffsetY { get; set; } = 30;
        public int OptionsButtonWidth { get; set; } = 230;
        public int OptionsButtonHeight { get; set; } = 44;
        public int OptionsButtonGap { get; set; } = 26;

        public int OptionsButtonRow2Y { get; set; } = 560;
        public int OptionsButtonRow2OffsetY { get; set; } = 10;

        public int ButtonTextOffsetX { get; set; } = 0;
        public int ButtonTextOffsetY { get; set; } = 0;

        public float FarmerEmoteOffsetX { get; set; } = 7.0f;
        public float FarmerEmoteOffsetY { get; set; } = -30.0f;
        public float FarmerEmoteScale { get; set; } = 3.0f;

        public float SpouseEmoteOffsetX { get; set; } = 8.0f;
        public float SpouseEmoteOffsetY { get; set; } = -15.0f;
        public float SpouseEmoteScale { get; set; } = 3.0f;

        public float PetEmoteOffsetX { get; set; } = 8.0f;
        public float PetEmoteOffsetY { get; set; } = -15.0f;
        public float PetEmoteScale { get; set; } = 3.0f;

        public int SettingsMenuWidth { get; set; } = 860;
        public int SettingsMenuHeight { get; set; } = 580;
        public string SettingsMenuTitleText { get; set; } = "";
        public int SettingsMenuItemHeight { get; set; } = 56;
        public int SettingsMenuMaxVisibleItems { get; set; } = 7;
        public int SettingsMenuListPadTop { get; set; } = 105;
        public int SettingsMenuListPadLeft { get; set; } = 50;
        public int SettingsMenuListPadRight { get; set; } = 50;
        public int SettingsMenuButtonWidth { get; set; } = 180;
        public int SettingsMenuButtonHeight { get; set; } = 44;
        public int SettingsMenuButtonGap { get; set; } = 16;
        public int SettingsMenuButtonsOffsetY { get; set; } = -60;

        public int BgSelectorButtonWidth { get; set; } = 220;
        public int BgSelectorButtonHeight { get; set; } = 48;
        public int BgSelectorButtonMargin { get; set; } = 25;
        public int BgSelectorButtonGap { get; set; } = 15;
        public int BgSelectorButtonOffsetY { get; set; } = -15;
        public string BgSelectorCaptureButtonText { get; set; } = "";
        public int BgSelectorTopBannerY { get; set; } = 15;
        public int BgSelectorTopBannerWidth { get; set; } = 860;
        public int BgSelectorTopBannerHeight { get; set; } = 92;
        public string BgSelectorHelpTextColor { get; set; } = "White";
        public int BgSelectorHelpTextOffsetY { get; set; } = 0;

        public static UILayout Load(string directoryPath)
        {
            string devPath = @"C:\Users\ale_y\Documents\Stardew-Presence\StardewPresence\ui_layout.json";
            if (!File.Exists(devPath))
            {
                devPath = @"C:\Users\ale_y\Documents\Stardew-Presence\ui_layout.json";
            }
            if (!File.Exists(devPath))
            {
                devPath = @"C:\Users\ale_y\Documents\Stardew-Dev\StardewPresence\ui_layout.json";
            }
            string localPath = Path.Combine(directoryPath, "ui_layout.json");

            string filePath = localPath;
            try
            {
                if (File.Exists(devPath))
                {
                    if (!File.Exists(localPath) || File.GetLastWriteTime(devPath) >= File.GetLastWriteTime(localPath))
                    {
                        filePath = devPath;
                    }
                }
            }
            catch
            {
            }

            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var layout = JsonSerializer.Deserialize<UILayout>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                    if (layout != null) return layout;
                }
            }
            catch
            {
            }

            var def = new UILayout();
            Save(def, directoryPath);
            return def;
        }

        public static void Save(UILayout layout, string directoryPath)
        {
            try
            {
                string filePath = Path.Combine(directoryPath, "ui_layout.json");
                string json = JsonSerializer.Serialize(layout, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(filePath, json);

                string devPath = @"C:\Users\ale_y\Documents\Stardew-Presence\StardewPresence\ui_layout.json";
                if (!File.Exists(devPath))
                {
                    devPath = @"C:\Users\ale_y\Documents\Stardew-Presence\ui_layout.json";
                }
                if (!File.Exists(devPath))
                {
                    devPath = @"C:\Users\ale_y\Documents\Stardew-Dev\StardewPresence\ui_layout.json";
                }
                if (File.Exists(devPath))
                {
                    File.WriteAllText(devPath, json);
                }
            }
            catch
            {
            }
        }
    }
}
