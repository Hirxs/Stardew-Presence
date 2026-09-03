using System;
using StardewDiscordRPC.Framework.Rendering;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewDiscordRPC.Framework.Presence
{
    /// <summary>
    /// Formats in-game player state (money, Qi gems/coins, tooltips, weather, datetime).
    /// </summary>
    public static class PlayerStatusFormatter
    {
        public static bool IsPlayerFishing()
        {
            var farmer = Game1.player;
            if (farmer == null) return false;

            if (Game1.activeClickableMenu is BobberBar) return true;
            if (farmer.UsingTool && farmer.CurrentTool is FishingRod) return true;

            return false;
        }

        public static string GetPlayerInfoState(Farmer farmer, ModConfig config, IModHelper helper)
        {
            string farmName = farmer.farmName.Value;

            string moneyDisplay = "0g";
            if (config.ShowMoney)
            {
                moneyDisplay = $"{farmer.Money:N0}g";

                int qiGems = farmer.QiGems;
                int qiCoins = farmer.clubCoins;

                if (config.ShowQiCoins)
                {
                    if (qiGems > 0 && qiCoins > 0)
                    {
                        moneyDisplay += $" ({qiGems:N0} Qi Gems, {qiCoins:N0} Qi Coins)";
                    }
                    else if (qiGems > 0)
                    {
                        moneyDisplay += $" ({qiGems:N0} Qi)";
                    }
                    else if (qiCoins > 0)
                    {
                        moneyDisplay += $" ({qiCoins:N0} Qi)";
                    }
                }
            }

            if (config.ShowFarmName && config.ShowMoney)
            {
                return helper.Translation.Get("state.player_info", new
                {
                    farmName = farmName,
                    money = moneyDisplay
                }).ToString();
            }
            else if (config.ShowFarmName)
            {
                return helper.Translation.Get("location.farm", new { farmName = farmName }).ToString();
            }
            else if (config.ShowMoney)
            {
                return moneyDisplay;
            }

            return string.Empty;
        }

        public static string GetWeatherDateTimeTooltip(Farmer farmer, int modCount, ModConfig config, IModHelper helper)
        {
            string weather = GetWeatherDisplayName(helper);

            string seasonKey = Game1.currentSeason?.ToLowerInvariant() ?? "spring";
            string seasonName = helper.Translation.Get($"season.{seasonKey}");
            if (string.IsNullOrWhiteSpace(seasonName) || seasonName.StartsWith("["))
            {
                seasonName = char.ToUpperInvariant(seasonKey[0]) + seasonKey.Substring(1);
            }

            int day = Game1.dayOfMonth;
            int year = Game1.year;
            string time = Game1.getTimeOfDayString(Game1.timeOfDay);

            string baseTooltip = helper.Translation.Get("weather.datetime_tooltip", new
            {
                weather = weather,
                season = seasonName,
                day = day,
                year = year,
                time = time
            });

            if (config.ShowModCount)
            {
                baseTooltip += $" | Mods: {modCount}";
            }

            return baseTooltip;
        }

        public static string GetLargeImageTooltip(Farmer farmer, ModConfig config, IModHelper helper)
        {
            var (compNpc, compFarmer, isPet) = CompanionResolver.GetCompanion(farmer, config);
            string? companionName = null;

            if (config.ShowCompanion)
            {
                if (compFarmer != null)
                {
                    companionName = compFarmer.Name;
                }
                else if (compNpc != null)
                {
                    companionName = compNpc.displayName ?? compNpc.Name;
                }
                else if (isPet)
                {
                    try
                    {
                        var pet = farmer.getPet();
                        companionName = pet?.displayName ?? (!string.IsNullOrWhiteSpace(pet?.Name) ? pet.Name : helper.Translation.Get("editor.companion_pet_short").Default("Mascota").ToString());
                    }
                    catch
                    {
                        companionName = helper.Translation.Get("editor.companion_pet_short").Default("Mascota").ToString();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(companionName))
            {
                return $"{farmer.Name} & {companionName}";
            }

            return farmer.Name;
        }

        private static dynamic? GetActiveLocationWeather()
        {
            try
            {
                var loc = Game1.currentLocation;
                if (loc != null && loc.IsOutdoors && !loc.IsGreenhouse)
                {
                    return loc.GetWeather();
                }

                return Game1.getFarm()?.GetWeather();
            }
            catch
            {
                return null;
            }
        }

        public static string GetWeatherAssetKey(ModConfig config)
        {
            var lw = GetActiveLocationWeather();
            string? weather = lw?.Weather;

            // 1. Wedding
            if (Game1.weddingToday || weather == Game1.weather_wedding)
            {
                return "weather_marriage";
            }

            // 2. Festival
            if (Game1.isFestival() || weather == Game1.weather_festival)
            {
                return "weather_festive";
            }

            // 3. Green Rain (Stardew 1.6)
            if (weather == Game1.weather_green_rain || lw?.IsGreenRain == true)
            {
                return "weather_green_rainy";
            }

            // 4. Storm / Lightning
            if (weather == Game1.weather_lightning || lw?.IsLightning == true)
            {
                return "weather_storm";
            }

            // 5. Snow
            if (weather == Game1.weather_snow || lw?.IsSnowing == true || (Game1.currentSeason?.Equals("winter", StringComparison.OrdinalIgnoreCase) == true && (Game1.isSnowing || Game1.isRaining)))
            {
                return "weather_snowy";
            }

            // 6. Rain
            if (weather == Game1.weather_rain || lw?.IsRaining == true || Game1.isRaining)
            {
                return "weather_rainy";
            }

            // 7. Debris / Wind (Petals in Spring, Leaves in Fall)
            if (weather == Game1.weather_debris || lw?.IsDebrisWeather == true || Game1.isDebrisWeather)
            {
                string season = Game1.currentSeason?.ToLowerInvariant() ?? "spring";
                return season == "fall" ? "wind_fall_notification" : "wind_spring_notification";
            }

            // 8. Sunny / Default
            return !string.IsNullOrWhiteSpace(config.WeatherSunnyKey) ? config.WeatherSunnyKey : "weather_sunny";
        }

        public static string GetWeatherDisplayName(IModHelper helper)
        {
            var lw = GetActiveLocationWeather();
            string? weather = lw?.Weather;

            if (Game1.weddingToday || weather == Game1.weather_wedding)
                return helper.Translation.Get("weather.wedding");

            if (Game1.isFestival() || weather == Game1.weather_festival)
                return helper.Translation.Get("weather.festival");

            if (weather == Game1.weather_green_rain || lw?.IsGreenRain == true)
                return helper.Translation.Get("weather.green_rain");

            if (weather == Game1.weather_lightning || lw?.IsLightning == true)
                return helper.Translation.Get("weather.storm");

            if (weather == Game1.weather_snow || lw?.IsSnowing == true || (Game1.currentSeason?.Equals("winter", StringComparison.OrdinalIgnoreCase) == true && (Game1.isSnowing || Game1.isRaining)))
                return helper.Translation.Get("weather.snow");

            if (weather == Game1.weather_rain || lw?.IsRaining == true || Game1.isRaining)
                return helper.Translation.Get("weather.rain");

            if (weather == Game1.weather_debris || lw?.IsDebrisWeather == true || Game1.isDebrisWeather)
                return helper.Translation.Get("weather.windy");

            return helper.Translation.Get("weather.sunny");
        }
    }
}
