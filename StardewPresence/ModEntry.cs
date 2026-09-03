using System;
using System.Linq;
using StardewPresence.Framework.Clients;
using StardewPresence.Framework.Integrations;
using StardewPresence.Framework.Menus;
using StardewPresence.Framework.Models;
using StardewPresence.Framework.Presence;
using StardewPresence.Framework.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewPresence
{
    public class ModEntry : Mod
    {
        private ModConfig config = null!;
        private DiscordRpcClient? discordClient;
        private PresenceManager? presenceManager;
        private FarmerImageGenerator? farmerImageGenerator;
        private int ticksSinceLastUpdate;
        private bool shouldShowWelcomeNotification;

        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;

            helper.ConsoleCommands.Add("rpc_editor", "Opens the Discord RPC Portrait & Spouse Visual Editor Menu.", (cmd, args) => OpenEditorMenu());
            helper.ConsoleCommands.Add("rpc_menu", "Opens the Discord RPC Portrait & Spouse Visual Editor Menu.", (cmd, args) => OpenEditorMenu());
            helper.ConsoleCommands.Add("rpc_bg_select", "Opens the Discord RPC Map Background Selector to capture a farm background.", (cmd, args) => OpenMapBackgroundSelector());
            helper.ConsoleCommands.Add("rpc_map_bg", "Opens the Discord RPC Map Background Selector to capture a farm background.", (cmd, args) => OpenMapBackgroundSelector());
            helper.ConsoleCommands.Add("rpc_bg_refresh", "Refreshes the custom map background snapshot for the current season and day.", (cmd, args) => RefreshCustomBackground());
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            int totalMods = Helper.ModRegistry.GetAll().Count();
            ModLogger.LogInfo(Monitor, $"[StardewPresence] Detected {totalMods} active mods.");

            presenceManager = new PresenceManager(Helper, Monitor, config);
            presenceManager.SetModCount(totalMods);
            farmerImageGenerator = new FarmerImageGenerator(Helper, Monitor, config);

            InitDiscordClient();
            RegisterGenericModConfigMenu();

            UpdatePresence();
        }

        private void InitDiscordClient()
        {
            try
            {
                discordClient?.Dispose();
                discordClient = new DiscordRpcClient(config.AppId, (msg, isInfo) =>
                {
                    ModLogger.Log(Monitor, msg, isInfo ? LogLevel.Info : LogLevel.Trace);
                });

                discordClient.OnJoin += OnDiscordJoin;
                discordClient.OnJoinRequest += OnDiscordJoinRequest;
            }
            catch (Exception ex)
            {
                Monitor.Log($"[StardewPresence] Failed to initialize Discord RPC: {ex}", LogLevel.Error);
            }
        }

        private void OnDiscordJoin(string secret)
        {
            ModLogger.LogInfo(Monitor, $"[StardewPresence] Discord Join invitation received with secret/code: {secret}");

            try
            {
                DesktopClipboard.SetText(secret);
                Game1.addHUDMessage(new HUDMessage($"[Discord Invite] Código de invitación '{secret}' copiado al portapapeles.", HUDMessage.achievement_type));
            }
            catch (Exception ex)
            {
                ModLogger.LogTrace(Monitor, $"[StardewPresence] Could not process Discord join secret: {ex.Message}");
            }
        }

        private void OnDiscordJoinRequest(DiscordUser user)
        {
            ModLogger.LogInfo(Monitor, $"[StardewPresence] Player '{user.Username}' requested to join your farm via Discord.");
            try
            {
                Game1.addHUDMessage(new HUDMessage($"[Discord] {user.Username} solicitó unirse a tu granja.", HUDMessage.newQuest_type));
            }
            catch
            {
            }
        }

        private void UpdatePresence()
        {
            if (discordClient == null || presenceManager == null) return;

            try
            {
                var activity = presenceManager.BuildActivity();
                discordClient.SetActivity(activity);
            }
            catch (Exception ex)
            {
                ModLogger.LogTrace(Monitor, $"[StardewPresence] Error building presence update: {ex}");
            }
        }

        private void UpdateFarmerImage()
        {
            if (Context.IsWorldReady && Game1.player != null && farmerImageGenerator != null)
            {
                farmerImageGenerator.CheckAndUpdate(Game1.player, newUrl =>
                {
                    presenceManager?.SetDynamicImageUrl(newUrl);
                    UpdatePresence();
                });
            }
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (config.UseCustomMapBackground && config.AutoUpdateCustomBackground && !string.IsNullOrWhiteSpace(config.CustomMapLocation) && farmerImageGenerator != null)
            {
                MapBackgroundCaptureHelper.TryRefreshConfiguredSpot(this.Helper, this.Monitor, this.config, this.farmerImageGenerator, notify: false);
            }

            UpdateFarmerImage();
            UpdatePresence();
            shouldShowWelcomeNotification = true;
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            presenceManager?.SetDynamicImageUrl(null);
            farmerImageGenerator?.InvalidateCache();
            shouldShowWelcomeNotification = false;
            UpdatePresence();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (config.UseCustomMapBackground && config.AutoUpdateCustomBackground && !string.IsNullOrWhiteSpace(config.CustomMapLocation) && farmerImageGenerator != null)
            {
                MapBackgroundCaptureHelper.TryRefreshConfiguredSpot(this.Helper, this.Monitor, this.config, this.farmerImageGenerator, notify: false);
            }

            UpdateFarmerImage();
            UpdatePresence();
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            UpdatePresence();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (e.IsLocalPlayer)
            {
                UpdatePresence();
            }
        }

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (shouldShowWelcomeNotification && Context.IsWorldReady && Context.IsPlayerFree)
            {
                shouldShowWelcomeNotification = false;
                if (config.ShowEditorKeyNotification)
                {
                    string keyName = config.EditorKey.ToString();
                    string text = Helper.Translation.Get("notification.welcome", new { key = keyName })
                        .Default($"Stardew Presence: Presiona [{keyName}] para personalizar tu Discord RPC.")
                        .ToString();
                    Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type));
                }
            }

            ticksSinceLastUpdate++;
            if (ticksSinceLastUpdate >= 10)
            {
                ticksSinceLastUpdate = 0;
                UpdatePresence();
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button == config.EditorKey && Context.IsWorldReady && Game1.activeClickableMenu == null)
            {
                OpenEditorMenu();
            }
        }

        private void OpenEditorMenu()
        {
            if (!Context.IsWorldReady || farmerImageGenerator == null)
            {
                Monitor.Log("[StardewPresence] You must load a save file before opening the Portrait Editor.", LogLevel.Warn);
                return;
            }

            Game1.activeClickableMenu = new PortraitEditorMenu(Helper, Monitor, config, farmerImageGenerator, () =>
            {
                UpdateFarmerImage();
                UpdatePresence();
            });
        }

        private void OpenMapBackgroundSelector()
        {
            if (!Context.IsWorldReady || farmerImageGenerator == null)
            {
                Monitor.Log("[StardewPresence] You must load a save file before opening the Map Background Selector.", LogLevel.Warn);
                return;
            }

            Game1.activeClickableMenu = new MapBackgroundSelectorMenu(Helper, Monitor, config, farmerImageGenerator, () =>
            {
                UpdateFarmerImage();
                UpdatePresence();
            });
        }

        private void RefreshCustomBackground()
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("[StardewPresence] You must load a save file before refreshing the map background.", LogLevel.Warn);
                return;
            }

            if (farmerImageGenerator != null)
            {
                bool ok = MapBackgroundCaptureHelper.TryRefreshConfiguredSpot(this.Helper, this.Monitor, this.config, this.farmerImageGenerator, notify: true);
                if (ok)
                {
                    ModLogger.LogInfo(Monitor, $"[StardewPresence] Successfully refreshed map background spot at '{config.CustomMapLocation}' ({config.CustomMapTileX}, {config.CustomMapTileY}) for season '{Game1.currentSeason}'.");
                    UpdateFarmerImage();
                    UpdatePresence();
                }
                else
                {
                    Monitor.Log("[StardewPresence] Could not refresh map background. Ensure you have captured a spot first.", LogLevel.Warn);
                }
            }
        }

        private void RegisterGenericModConfigMenu()
        {
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null) return;

            gmcm.Register(
                mod: ModManifest,
                reset: () =>
                {
                    config = new ModConfig();
                    Helper.WriteConfig(config);
                    UpdateFarmerImage();
                    UpdatePresence();
                },
                save: () =>
                {
                    Helper.WriteConfig(config);
                    UpdateFarmerImage();
                    UpdatePresence();
                }
            );

            // General
            gmcm.AddSectionTitle(ModManifest, () => "General Settings");
            gmcm.AddBoolOption(ModManifest, () => config.EnablePresence, val => config.EnablePresence = val, () => "Enable Rich Presence", () => "Enable or disable Discord Rich Presence");
            gmcm.AddTextOption(ModManifest, () => config.AppId, val => { config.AppId = val; InitDiscordClient(); }, () => "Discord Application ID", () => "The Client ID from Discord Developer Portal");

            // Visual Portrait Editor
            gmcm.AddSectionTitle(ModManifest, () => "Visual Portrait & Spouse Editor");
            gmcm.AddKeybind(ModManifest, () => config.EditorKey, val => config.EditorKey = val, () => "Open Editor Key", () => "Press this key in-game to open the interactive visual editor menu (default F8).");
            gmcm.AddBoolOption(ModManifest, () => config.ShowEditorKeyNotification, val => config.ShowEditorKeyNotification = val, () => "Show Startup Notification", () => "Show a HUD notification reminding you to press the editor key when loading a save.");
            gmcm.AddBoolOption(ModManifest, () => config.EnableDynamicFarmerImage, val => { config.EnableDynamicFarmerImage = val; UpdateFarmerImage(); UpdatePresence(); }, () => "Enable Dynamic Portrait", () => "Renders farmer on seasonal background and displays it in Discord RPC");
            gmcm.AddBoolOption(ModManifest, () => config.UseCustomMapBackground, val => { config.UseCustomMapBackground = val; UpdateFarmerImage(); UpdatePresence(); }, () => "Use Custom Map Background", () => "Use custom captured map background instead of default seasonal art");
            gmcm.AddBoolOption(ModManifest, () => config.AutoUpdateCustomBackground, val => config.AutoUpdateCustomBackground = val, () => "Auto-Update Map Background", () => "Automatically updates the custom map background on new days and seasonal changes.");
            gmcm.AddTextOption(ModManifest, () => config.MissingPortraitImageKey, val => config.MissingPortraitImageKey = val, () => "Placeholder Image Key", () => "Discord asset key used while portrait is loading or missing (default: missing_pfp)");

            // Main Menu
            gmcm.AddSectionTitle(ModManifest, () => "Main Menu Presence");
            gmcm.AddTextOption(ModManifest, () => config.TitleScreenLogo, val => config.TitleScreenLogo = val, () => "Title Screen Logo", () => "Choose between: auto, smapi, vanilla, expanded", new[] { "auto", "smapi", "vanilla", "expanded" });
            gmcm.AddTextOption(ModManifest, () => config.TitleMenuDetails, val => config.TitleMenuDetails = val, () => "Menu Details Text", () => "Text shown on the Details line when in main menu");
            gmcm.AddTextOption(ModManifest, () => config.TitleMenuState, val => config.TitleMenuState = val, () => "Menu State Text", () => "Text shown on the State line when in main menu");
            gmcm.AddTextOption(ModManifest, () => config.TitleLargeImageKey, val => config.TitleLargeImageKey = val, () => "Title Large Image Key", () => "Asset key for the main menu large icon");
            gmcm.AddTextOption(ModManifest, () => config.TitleSmallImageKey, val => config.TitleSmallImageKey = val, () => "Title Small Image Key", () => "Asset key for the main menu small icon");

            // In-Game Details
            gmcm.AddSectionTitle(ModManifest, () => "In-Game Toggles & Privacy");
            gmcm.AddBoolOption(ModManifest, () => config.ShowLocation, val => config.ShowLocation = val, () => "Show Location / Activity", () => "Show current location, mining floor, fishing, etc.");
            gmcm.AddBoolOption(ModManifest, () => config.HideNpcHouses, val => config.HideNpcHouses = val, () => "Privacy: Hide NPC Houses", () => "Show generic town/forest instead of specific NPC houses");
            gmcm.AddBoolOption(ModManifest, () => config.ShowEventDetails, val => config.ShowEventDetails = val, () => "Show Heart Event Details", () => "Show NPC name and heart level during cutscenes");
            gmcm.AddBoolOption(ModManifest, () => config.ShowFarmName, val => config.ShowFarmName = val, () => "Show Farm Name on Line 2", () => "Show farm name on presence state line");
            gmcm.AddBoolOption(ModManifest, () => config.ShowMoney, val => config.ShowMoney = val, () => "Show Money in Tooltip", () => "Show farmer gold count");
            gmcm.AddBoolOption(ModManifest, () => config.ShowQiCoins, val => config.ShowQiCoins = val, () => "Show Qi Coins", () => "Show Qi club coins next to money when unlocked");
            gmcm.AddBoolOption(ModManifest, () => config.ShowHealthAndEnergy, val => config.ShowHealthAndEnergy = val, () => "Show Health & Energy", () => "Show HP and Stamina in weather icon tooltip");
            gmcm.AddBoolOption(ModManifest, () => config.ShowSpouse, val => config.ShowSpouse = val, () => "Show Spouse / Roommate", () => "Show spouse or roommate in tooltip");
            gmcm.AddBoolOption(ModManifest, () => config.ShowMultiplayer, val => config.ShowMultiplayer = val, () => "Show Multiplayer Status", () => "Show party size in multiplayer sessions");
            gmcm.AddBoolOption(ModManifest, () => config.ShowModCount, val => config.ShowModCount = val, () => "Show Mod Count", () => "Display active mod count in tooltips");
            gmcm.AddBoolOption(ModManifest, () => config.ShowElapsedTime, val => config.ShowElapsedTime = val, () => "Show Elapsed Time", () => "Display time elapsed since game launch");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                discordClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
