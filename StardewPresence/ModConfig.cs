using StardewModdingAPI;

namespace StardewPresence
{
    public class ModConfig
    {
        public string AppId { get; set; } = "1543731931512967249";
        public bool EnablePresence { get; set; } = true;
        
        // Title Screen customization (leave empty to use localized translations)
        public string TitleMenuDetails { get; set; } = "";
        public string TitleMenuState { get; set; } = "";
        public string TitleLargeImageKey { get; set; } = "sv-app-logo-smapi";
        public string TitleSmallImageKey { get; set; } = "title_screen_bubble";

        // Asset Keys
        public string DefaultLargeImageKey { get; set; } = "sv-app-logo";
        public string ExpandedLargeImageKey { get; set; } = "sv-app-logo-expanded";
        public string WeatherSunnyKey { get; set; } = "weather_sunny";
        
        // Dynamic Farmer Portrait Generation & Image Hosting
        public bool EnableDynamicFarmerImage { get; set; } = true;
        public string MissingPortraitImageKey { get; set; } = "missing_pfp";
        public string CustomUploadUrl { get; set; } = "https://hyris-workshop-img-api.vercel.app/api/upload";
        public string CustomUploadAuthHeader { get; set; } = "";
        
        // Visual Portrait Editor Customization Settings
        public SButton EditorKey { get; set; } = SButton.F8;
        public bool ShowEditorKeyNotification { get; set; } = true;
        
        // Player Settings
        public float FarmerOffsetX { get; set; } = 0f;
        public float FarmerOffsetY { get; set; } = 0f;
        public float FarmerScale { get; set; } = 2.3f;
        public int FarmerFrame { get; set; } = 0;
        public bool FarmerFlip { get; set; } = false;
        public int FarmerFacingDirection { get; set; } = 2; // 0=Back, 1=Right, 2=Front, 3=Left
        public int FarmerEmote { get; set; } = 0;           // 0=None, 1=Heart, 2=Music, etc.
        public float FarmerEmoteOffsetX { get; set; } = 0f;
        public float FarmerEmoteOffsetY { get; set; } = 0f;
        public float FarmerEmoteScale { get; set; } = 1.0f;
        
        // Companion / Spouse Settings (Human NPC / Multiplayer Spouse)
        public int CompanionType { get; set; } = 0;         // 0=Auto (Spouse -> Pet -> Horse), 1=Spouse, 2=Pet, 3=Horse, 4=None
        public float SpouseOffsetX { get; set; } = -26f;
        public float SpouseOffsetY { get; set; } = 0.5f;
        public float SpouseScale { get; set; } = 4f;
        public int SpouseFrame { get; set; } = 0;
        public bool SpouseLayerFront { get; set; } = false;
        public bool SpouseFlip { get; set; } = false;
        public int SpouseFacingDirection { get; set; } = 2; // 0=Back, 1=Right, 2=Front, 3=Left
        public int SpouseEmote { get; set; } = 0;           // 0=None, 1=Heart, 2=Music, etc.
        public float SpouseEmoteOffsetX { get; set; } = 0f;
        public float SpouseEmoteOffsetY { get; set; } = 0f;
        public float SpouseEmoteScale { get; set; } = 1.0f;

        // Pet Settings (Dog / Cat / Horse)
        public float PetOffsetX { get; set; } = -59f;
        public float PetOffsetY { get; set; } = 6f;
        public float PetScale { get; set; } = 3.2f;
        public int PetFrame { get; set; } = 18;
        public bool PetLayerFront { get; set; } = false;
        public bool PetFlip { get; set; } = false;
        public int PetEmote { get; set; } = 0;
        public float PetEmoteOffsetX { get; set; } = 0f;
        public float PetEmoteOffsetY { get; set; } = 0f;
        public float PetEmoteScale { get; set; } = 1.0f;

        // Visibility Toggles
        public bool ShowFarmer { get; set; } = true;
        public bool ShowCompanion { get; set; } = true;

        public string ForcedSeason { get; set; } = "Auto";
        public string EditorSeasonMode { get; set; } = "Season"; // "Season" (Auto), "Manual", "Custom"

        // Custom Map Background
        public bool UseCustomMapBackground { get; set; } = true;
        public string CustomBackgroundMode { get; set; } = "Seasonal"; // "Single" (custom_bg.png) or "Seasonal" (custom_bg_spring.png, etc.)
        public string CustomMapLocation { get; set; } = "Farm";
        public int CustomMapTileX { get; set; } = 66;
        public int CustomMapTileY { get; set; } = 4;
        public int CustomMapGridSize { get; set; } = 6;
        public bool AutoUpdateCustomBackground { get; set; } = true;
        public long LastBackgroundCaptureTimestamp { get; set; } = 0;

        // Feature Toggles
        public bool ShowModCount { get; set; } = true;
        public bool ShowMoney { get; set; } = true;
        public bool ShowQiCoins { get; set; } = true;
        public bool ShowFarmName { get; set; } = true;
        public bool ShowHealthAndEnergy { get; set; } = true;
        public bool ShowSpouse { get; set; } = true;
        public bool ShowPet { get; set; } = true;
        public bool ShowLocation { get; set; } = true;
        public bool HideNpcHouses { get; set; } = false;
        public bool ShowEventDetails { get; set; } = true;
        public bool ShowMultiplayer { get; set; } = true;
        public bool EnableMultiplayerInvites { get; set; } = true;
        public bool ShowElapsedTime { get; set; } = true;
        public string TitleScreenLogo { get; set; } = "auto"; // "auto", "smapi", "vanilla", "expanded"

        public ModConfig Clone()
        {
            return (ModConfig)this.MemberwiseClone();
        }

        public void CopyFrom(ModConfig other)
        {
            this.ShowFarmer = other.ShowFarmer;
            this.ShowCompanion = other.ShowCompanion;
            this.ShowSpouse = other.ShowSpouse;
            this.ShowPet = other.ShowPet;

            this.FarmerOffsetX = other.FarmerOffsetX;
            this.FarmerOffsetY = other.FarmerOffsetY;
            this.FarmerScale = other.FarmerScale;
            this.FarmerFrame = other.FarmerFrame;
            this.FarmerFlip = other.FarmerFlip;
            this.FarmerFacingDirection = other.FarmerFacingDirection;
            this.FarmerEmote = other.FarmerEmote;
            this.FarmerEmoteOffsetX = other.FarmerEmoteOffsetX;
            this.FarmerEmoteOffsetY = other.FarmerEmoteOffsetY;
            this.FarmerEmoteScale = other.FarmerEmoteScale > 0 ? other.FarmerEmoteScale : 1.0f;

            this.CompanionType = other.CompanionType;
            this.SpouseOffsetX = other.SpouseOffsetX;
            this.SpouseOffsetY = other.SpouseOffsetY;
            this.SpouseScale = other.SpouseScale;
            this.SpouseFrame = other.SpouseFrame;
            this.SpouseLayerFront = other.SpouseLayerFront;
            this.SpouseFlip = other.SpouseFlip;
            this.SpouseFacingDirection = other.SpouseFacingDirection;
            this.SpouseEmote = other.SpouseEmote;
            this.SpouseEmoteOffsetX = other.SpouseEmoteOffsetX;
            this.SpouseEmoteOffsetY = other.SpouseEmoteOffsetY;
            this.SpouseEmoteScale = other.SpouseEmoteScale > 0 ? other.SpouseEmoteScale : 1.0f;

            this.PetOffsetX = other.PetOffsetX;
            this.PetOffsetY = other.PetOffsetY;
            this.PetScale = other.PetScale;
            this.PetFrame = other.PetFrame;
            this.PetLayerFront = other.PetLayerFront;
            this.PetFlip = other.PetFlip;
            this.PetEmote = other.PetEmote;
            this.PetEmoteOffsetX = other.PetEmoteOffsetX;
            this.PetEmoteOffsetY = other.PetEmoteOffsetY;
            this.PetEmoteScale = other.PetEmoteScale > 0 ? other.PetEmoteScale : 1.0f;

            this.ForcedSeason = other.ForcedSeason;
            this.EditorSeasonMode = other.EditorSeasonMode;
            this.UseCustomMapBackground = other.UseCustomMapBackground;
            this.CustomBackgroundMode = other.CustomBackgroundMode;
            this.CustomMapLocation = other.CustomMapLocation;
            this.CustomMapTileX = other.CustomMapTileX;
            this.CustomMapTileY = other.CustomMapTileY;
            this.CustomMapGridSize = other.CustomMapGridSize;
            this.AutoUpdateCustomBackground = other.AutoUpdateCustomBackground;
            this.LastBackgroundCaptureTimestamp = other.LastBackgroundCaptureTimestamp;

            this.MissingPortraitImageKey = other.MissingPortraitImageKey;
            this.CustomUploadUrl = other.CustomUploadUrl;
            this.CustomUploadAuthHeader = other.CustomUploadAuthHeader;
            this.ShowQiCoins = other.ShowQiCoins;
            this.ShowFarmName = other.ShowFarmName;
            this.HideNpcHouses = other.HideNpcHouses;
            this.ShowEventDetails = other.ShowEventDetails;
            this.TitleScreenLogo = other.TitleScreenLogo;
            this.TitleMenuDetails = other.TitleMenuDetails;
            this.TitleMenuState = other.TitleMenuState;
        }
    }
}
