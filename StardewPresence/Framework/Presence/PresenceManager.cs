using System;
using System.Linq;
using StardewPresence.Framework.Models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewPresence.Framework.Presence
{
    public class PresenceManager
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ModConfig config;
        private readonly long gameStartTime;
        private int cachedModCount;
        private string? dynamicImageUrl;

        public PresenceManager(IModHelper helper, IMonitor monitor, ModConfig config)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
            this.gameStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public void SetModCount(int count) => cachedModCount = count;

        public void SetDynamicImageUrl(string? url) => dynamicImageUrl = url;

        public DiscordActivity BuildActivity()
        {
            if (!config.EnablePresence)
            {
                return new DiscordActivity();
            }

            if (!Context.IsWorldReady || Game1.player == null)
            {
                return BuildTitleScreenActivity();
            }

            return BuildInGameActivity();
        }

        public DiscordActivity BuildTitleScreenActivity()
        {
            int modCount = cachedModCount > 0 ? cachedModCount : helper.ModRegistry.GetAll().Count();
            string gameVersion = Game1.version;

            var (details, state) = GetDynamicTitleMenuStrings();

            string largeText = helper.Translation.Get("title.version", new { version = gameVersion }).ToString();
            string smallText = helper.Translation.Get("title.mods", new { count = modCount }).ToString();

            string largeImage = config.TitleScreenLogo switch
            {
                "smapi" => "sv-app-logo-smapi",
                "vanilla" => "sv-app-logo",
                "expanded" => "sv-app-logo-expanded",
                _ => LocationResolver.IsStardewValleyExpandedLoaded(helper) ? "sv-app-logo-expanded" : "sv-app-logo-smapi"
            };

            return new DiscordActivity
            {
                Details = details,
                State = state,
                Timestamps = config.ShowElapsedTime ? new DiscordTimestamps { Start = gameStartTime } : null,
                Assets = new DiscordAssets
                {
                    LargeImage = largeImage,
                    LargeText = largeText,
                    SmallImage = config.TitleSmallImageKey,
                    SmallText = smallText
                },
                Instance = true
            };
        }

        public DiscordActivity BuildInGameActivity()
        {
            var farmer = Game1.player;
            int modCount = cachedModCount > 0 ? cachedModCount : helper.ModRegistry.GetAll().Count();

            string details = GetActivityDetails();
            string state = PlayerStatusFormatter.GetPlayerInfoState(farmer, config, helper);

            string largeImageKey = config.EnableDynamicFarmerImage
                ? (!string.IsNullOrEmpty(dynamicImageUrl) ? dynamicImageUrl : config.MissingPortraitImageKey)
                : config.DefaultLargeImageKey;
            string largeText = PlayerStatusFormatter.GetLargeImageTooltip(farmer, config, helper);

            string smallImageKey = PlayerStatusFormatter.GetWeatherAssetKey(config);
            string smallText = PlayerStatusFormatter.GetWeatherDateTimeTooltip(farmer, modCount, config, helper);

            DiscordParty? party = null;
            DiscordSecrets? secrets = null;

            if (config.ShowMultiplayer && (Game1.IsMultiplayer || Game1.multiplayerMode != 0 || Game1.IsServer))
            {
                int currentPlayers = Math.Max(1, Game1.getOnlineFarmers().Count());
                int maxPlayers = (Game1.netWorldState?.Value != null) ? Math.Max(4, Game1.netWorldState.Value.HighestPlayerLimit) : Math.Max(4, currentPlayers);

                string partyId = $"stardew_party_{Game1.uniqueIDForThisGame}";
                party = new DiscordParty
                {
                    Id = partyId,
                    Size = new[] { currentPlayers, maxPlayers }
                };

                if (config.EnableMultiplayerInvites && (Game1.IsServer || Game1.IsMasterGame))
                {
                    string? inviteCode = null;
                    try
                    {
                        inviteCode = Game1.server?.getInviteCode();
                    }
                    catch
                    {
                    }

                    string joinSecret = !string.IsNullOrWhiteSpace(inviteCode)
                        ? inviteCode
                        : $"farm_{Game1.uniqueIDForThisGame}";

                    secrets = new DiscordSecrets
                    {
                        Join = joinSecret
                    };
                }
            }

            return new DiscordActivity
            {
                Details = details,
                State = state,
                Timestamps = config.ShowElapsedTime ? new DiscordTimestamps { Start = gameStartTime } : null,
                Assets = new DiscordAssets
                {
                    LargeImage = largeImageKey,
                    LargeText = largeText,
                    SmallImage = smallImageKey,
                    SmallText = smallText
                },
                Party = party,
                Secrets = secrets,
                Instance = true
            };
        }

        private string GetActivityDetails()
        {
            if (!config.ShowLocation)
            {
                return "Playing Stardew Valley";
            }

            if (Game1.isFestival())
            {
                string festivalName = Game1.CurrentEvent?.FestivalName ?? helper.Translation.Get("weather.festival");
                return helper.Translation.Get("action.festival", new { festival = festivalName });
            }

            if (Game1.CurrentEvent != null && !Game1.CurrentEvent.isFestival)
            {
                if (config.ShowEventDetails)
                {
                    try
                    {
                        var npcActor = Game1.CurrentEvent.actors?.FirstOrDefault(a => a != null && !string.IsNullOrWhiteSpace(a.Name));
                        if (npcActor != null)
                        {
                            string npcName = npcActor.displayName ?? npcActor.Name;
                            int hearts = Game1.player.getFriendshipHeartLevelForNPC(npcActor.Name);
                            if (hearts > 0)
                            {
                                return helper.Translation.Get("action.heart_event", new { name = npcName, hearts = hearts }).ToString();
                            }
                            return helper.Translation.Get("action.event_with_npc", new { name = npcName }).ToString();
                        }
                    }
                    catch
                    {
                    }
                }
                return helper.Translation.Get("action.event").ToString();
            }

            if (PlayerStatusFormatter.IsPlayerFishing())
            {
                string locName = LocationResolver.GetFriendlyLocationName(Game1.currentLocation, helper, config);
                return helper.Translation.Get("action.fishing", new { location = locName });
            }

            return LocationResolver.ResolveLocationDetails(Game1.currentLocation, helper, config);
        }

        private (string details, string state) GetDynamicTitleMenuStrings()
        {
            bool hasCustomDetails = !string.IsNullOrWhiteSpace(config.TitleMenuDetails) &&
                                    !config.TitleMenuDetails.Equals("In the Main menu", StringComparison.OrdinalIgnoreCase);
            bool hasCustomState = !string.IsNullOrWhiteSpace(config.TitleMenuState) &&
                                  !config.TitleMenuState.Equals("Browsing Saves", StringComparison.OrdinalIgnoreCase) &&
                                  !config.TitleMenuState.Equals("Main Menu", StringComparison.OrdinalIgnoreCase);

            string details = hasCustomDetails ? config.TitleMenuDetails : helper.Translation.Get("title.details").ToString();
            string state = hasCustomState ? config.TitleMenuState : helper.Translation.Get("title.main_menu").ToString();

            IClickableMenu? subMenu = TitleMenu.subMenu ?? (Game1.activeClickableMenu is not TitleMenu ? Game1.activeClickableMenu : null);

            if (subMenu != null)
            {
                string menuName = subMenu.GetType().Name;

                if (subMenu is LoadGameMenu || menuName.Contains("LoadGame", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasCustomState) state = helper.Translation.Get("title.browsing_saves").ToString();
                }
                else if (subMenu is CoopMenu || menuName.Contains("Coop", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasCustomState) state = helper.Translation.Get("title.coop").ToString();
                }
                else if (subMenu is CharacterCustomization || menuName.Contains("Character", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasCustomDetails) details = helper.Translation.Get("title.creating_character").ToString();
                    if (!hasCustomState) state = helper.Translation.Get("title.new_game").ToString();
                }
                else if (subMenu is LanguageSelectionMenu || menuName.Contains("Language", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasCustomState) state = helper.Translation.Get("title.language").ToString();
                }
                else if (subMenu is AboutMenu || menuName.Contains("About", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasCustomState) state = helper.Translation.Get("title.about").ToString();
                }
            }

            return (details, state);
        }
    }
}
