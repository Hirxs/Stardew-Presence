using System;
using System.Linq;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace StardewDiscordRPC.Framework.Presence
{
    /// <summary>
    /// Handles discovery, privacy masking, SVE integration, and localization of game locations.
    /// </summary>
    public static class LocationResolver
    {
        private static readonly Regex PascalCaseRegex1 = new("([a-z])([A-Z])", RegexOptions.Compiled);
        private static readonly Regex PascalCaseRegex2 = new("([A-Z]+)([A-Z][a-z])", RegexOptions.Compiled);

        public static bool IsStardewValleyExpandedLoaded(IModHelper helper)
        {
            try
            {
                return helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP")
                    || helper.ModRegistry.IsLoaded("FlashShifter.SVECode")
                    || helper.ModRegistry.GetAll().Any(m => m.Manifest.Name.Contains("Stardew Valley Expanded", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public static string ResolveLocationDetails(GameLocation? location, IModHelper helper, ModConfig config)
        {
            if (location == null) return "Exploring";

            if (location is MineShaft mine)
            {
                if (mine.mineLevel >= 121)
                {
                    int floor = mine.mineLevel - 120;
                    return helper.Translation.Get("location.skull_cavern", new { floor });
                }
                if (mine.mineLevel == 77377)
                {
                    return helper.Translation.Get("location.mountain");
                }
                return helper.Translation.Get("location.mines", new { floor = mine.mineLevel });
            }

            if (location is VolcanoDungeon volcano)
            {
                int floor = volcano.level.Value;
                return helper.Translation.Get("location.volcano", new { floor });
            }

            if (location is Farm)
            {
                return helper.Translation.Get("location.farm", new { farmName = Game1.player.farmName.Value });
            }

            if (location is FarmHouse)
            {
                return helper.Translation.Get("location.farmhouse");
            }

            if (location is Cellar)
            {
                return helper.Translation.Get("location.cellar");
            }

            if (location is AnimalHouse animalHouse)
            {
                string locName = animalHouse.NameOrUniqueName ?? string.Empty;
                if (locName.IndexOf("Barn", StringComparison.OrdinalIgnoreCase) >= 0)
                    return helper.Translation.Get("location.barn");
                if (locName.IndexOf("Coop", StringComparison.OrdinalIgnoreCase) >= 0)
                    return helper.Translation.Get("location.coop");
                return helper.Translation.Get("location.barn");
            }

            if (location is Shed)
            {
                return helper.Translation.Get("location.shed");
            }

            if (location is IslandLocation || location.Name?.StartsWith("Island", StringComparison.OrdinalIgnoreCase) == true)
            {
                string area = GetFriendlyIslandArea(location.Name ?? "Ginger Island", helper);
                return helper.Translation.Get("location.island", new { area });
            }

            string locKey = location.Name ?? string.Empty;
            return locKey switch
            {
                "Greenhouse" => helper.Translation.Get("location.greenhouse"),
                "Town" => helper.Translation.Get("location.town"),
                "Saloon" => helper.Translation.Get("location.saloon"),
                "SeedShop" => helper.Translation.Get("location.pierres"),
                "Blacksmith" => helper.Translation.Get("location.blacksmith"),
                "ArchaeologyHouse" => helper.Translation.Get("location.museum"),
                "Hospital" => helper.Translation.Get("location.hospital"),
                "CommunityCenter" => helper.Translation.Get("location.community_center"),
                "JojaMart" => helper.Translation.Get("location.joja"),
                "Beach" or "BeachNightMarket" => helper.Translation.Get("location.beach"),
                "Mountain" => helper.Translation.Get("location.mountain"),
                "Forest" or "Woods" => helper.Translation.Get("location.forest"),
                "Desert" => helper.Translation.Get("location.desert"),
                "MasteryCave" => helper.Translation.Get("location.mastery"),
                _ => helper.Translation.Get("location.generic", new { location = GetFriendlyLocationName(location, helper, config) })
            };
        }

        public static string GetFriendlyIslandArea(string locName, IModHelper helper)
        {
            return locName switch
            {
                "IslandWest" => helper.Translation.Get("island.west").Default("Farm & Shore").ToString(),
                "IslandEast" => helper.Translation.Get("island.east").Default("Jungle").ToString(),
                "IslandSouth" => helper.Translation.Get("island.south").Default("Docks").ToString(),
                "IslandNorth" => helper.Translation.Get("island.north").Default("Dig Site").ToString(),
                "IslandSouthEast" or "IslandSouthEastCave" => helper.Translation.Get("island.pirate_cove").Default("Pirate Cove").ToString(),
                "Caldera" => helper.Translation.Get("island.caldera").Default("Volcano Caldera").ToString(),
                "IslandFarmHouse" => helper.Translation.Get("island.farmhouse").Default("Farmhouse").ToString(),
                "IslandHut" => helper.Translation.Get("island.hut").Default("Leo's Hut").ToString(),
                _ => "Ginger Island"
            };
        }

        public static string GetFriendlyLocationName(GameLocation? location, IModHelper helper, ModConfig config)
        {
            if (location == null) return "Unknown";

            string rawName = location.Name ?? string.Empty;
            string displayName = location.DisplayName;

            if (config.HideNpcHouses && IsNpcHouseLocation(rawName))
            {
                return GetPrivacyAreaName(rawName, helper);
            }

            string? known = GetKnownLocationTranslation(rawName, helper);
            if (!string.IsNullOrWhiteSpace(known))
            {
                return known;
            }

            if (!string.IsNullOrWhiteSpace(displayName) &&
                !displayName.Equals(rawName, StringComparison.OrdinalIgnoreCase) &&
                !displayName.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
            {
                return CleanLocationName(displayName, helper);
            }

            return CleanLocationName(rawName, helper);
        }

        public static bool IsNpcHouseLocation(string rawName)
        {
            return rawName switch
            {
                "JoshHouse" or "HaleyHouse" or "SamHouse" or "LeahHouse" or "ElliottHouse" or
                "WizardHouse" or "WizardHouseBasement" or "ScienceHouse" or "SebastianRoom" or
                "ManorHouse" or "Trailer" or "Trailer_Big" or "Tent" or "Sunroom" or "SandyHouse" or
                "WitchHut" or "Custom_SophiaHouse" or "Custom_AndyHouse" or "Custom_OliviaHouse" or
                "Custom_VictorHouse" or "Custom_SusanHouse" or "Custom_JenkinsHouse" or
                "Custom_ApplesRoom" => true,
                _ => rawName.EndsWith("House", StringComparison.OrdinalIgnoreCase) ||
                     rawName.EndsWith("Room", StringComparison.OrdinalIgnoreCase) ||
                     rawName.EndsWith("Cabin", StringComparison.OrdinalIgnoreCase)
            };
        }

        public static string GetPrivacyAreaName(string rawName, IModHelper helper)
        {
            return rawName switch
            {
                "LeahHouse" or "WizardHouse" or "WizardHouseBasement" or "WitchHut" or
                "Custom_AuroraVineyard" or "Custom_EnchantedGrove" or "Custom_WesternForest"
                    => helper.Translation.Get("location.forest").ToString(),

                "ScienceHouse" or "SebastianRoom" or "Tent"
                    => helper.Translation.Get("location.mountain").ToString(),

                "ElliottHouse"
                    => helper.Translation.Get("location.beach").ToString(),

                "SandyHouse"
                    => helper.Translation.Get("location.desert").ToString(),

                _ => helper.Translation.Get("location.town").ToString()
            };
        }

        public static string? GetKnownLocationTranslation(string rawName, IModHelper helper)
        {
            return rawName switch
            {
                "JoshHouse" => helper.Translation.Get("location.josh_house").Default("George & Evelyn's House").ToString(),
                "HaleyHouse" => helper.Translation.Get("location.haley_house").Default("Emily & Haley's House").ToString(),
                "SamHouse" => helper.Translation.Get("location.sam_house").Default("Jodi & Sam's House").ToString(),
                "LeahHouse" => helper.Translation.Get("location.leah_house").Default("Leah's Cottage").ToString(),
                "ElliottHouse" => helper.Translation.Get("location.elliott_house").Default("Elliott's Cabin").ToString(),
                "WizardHouse" => helper.Translation.Get("location.wizard_tower").Default("Wizard's Tower").ToString(),
                "WizardHouseBasement" => helper.Translation.Get("location.wizard_basement").Default("Wizard's Basement").ToString(),
                "ScienceHouse" => helper.Translation.Get("location.science_house").Default("Robin's Carpenter Shop").ToString(),
                "SebastianRoom" => helper.Translation.Get("location.sebastian_room").Default("Sebastian's Room").ToString(),
                "ManorHouse" => helper.Translation.Get("location.manor_house").Default("Mayor's Manor").ToString(),
                "Trailer" or "Trailer_Big" => helper.Translation.Get("location.trailer").Default("Pam's Trailer").ToString(),
                "Tent" => helper.Translation.Get("location.tent").Default("Linus' Tent").ToString(),
                "Sunroom" => helper.Translation.Get("location.sunroom").Default("Caroline's Sunroom").ToString(),
                "MovieTheater" => helper.Translation.Get("location.movie_theater").Default("Movie Theater").ToString(),
                "AdventureGuild" => helper.Translation.Get("location.adventure_guild").Default("Adventurer's Guild").ToString(),
                "SandyHouse" => helper.Translation.Get("location.sandy_house").Default("Oasis").ToString(),
                "Sewer" => helper.Translation.Get("location.sewer").Default("The Sewers").ToString(),
                "BugLand" => helper.Translation.Get("location.bug_land").Default("Mutant Bug Lair").ToString(),
                "WitchSwamp" => helper.Translation.Get("location.witch_swamp").Default("Witch's Swamp").ToString(),
                "WitchHut" => helper.Translation.Get("location.witch_hut").Default("Witch's Hut").ToString(),
                "WitchWarpCave" => helper.Translation.Get("location.witch_warp_cave").Default("Witch's Warp Cave").ToString(),
                "Railroad" => helper.Translation.Get("location.railroad").Default("Railroad").ToString(),
                "Backwoods" => helper.Translation.Get("location.backwoods").Default("Backwoods").ToString(),
                "BathHouse_Pool" or "BathHouse_Entry" or "BathHouse_MensLocker" or "BathHouse_WomensLocker" => helper.Translation.Get("location.bathhouse").Default("Bathhouse").ToString(),
                "Club" => helper.Translation.Get("location.casino").Default("Qi's Casino").ToString(),
                "Submarine" => helper.Translation.Get("location.submarine").Default("Night Market Submarine").ToString(),
                "Summit" => helper.Translation.Get("location.summit").Default("The Summit").ToString(),
                "SkullCave" => helper.Translation.Get("location.skull_cave").Default("Skull Cavern Entrance").ToString(),

                "Custom_SophiaHouse" => helper.Translation.Get("location.sve_sophia_house").Default("Sophia's House").ToString(),
                "Custom_GrandpasShed" or "Custom_GrandpasShedGreenhouse" or "Custom_GrandpasShedRuins" => helper.Translation.Get("location.sve_grandpas_shed").Default("Grandpa's Shed").ToString(),
                "Custom_AuroraVineyard" or "Custom_AuroraVineyardBasement" => helper.Translation.Get("location.sve_aurora_vineyard").Default("Aurora Vineyard").ToString(),
                "Custom_BlueMoonVineyard" => helper.Translation.Get("location.sve_blue_moon_vineyard").Default("Blue Moon Vineyard").ToString(),
                "Custom_AndyHouse" => helper.Translation.Get("location.sve_andy_house").Default("Andy's House").ToString(),
                "Custom_OliviaHouse" or "Custom_OliviaCellar" => helper.Translation.Get("location.sve_olivia_house").Default("Olivia's House").ToString(),
                "Custom_VictorHouse" => helper.Translation.Get("location.sve_victor_house").Default("Victor's House").ToString(),
                "Custom_SusanHouse" => helper.Translation.Get("location.sve_susan_house").Default("Susan's House").ToString(),
                "Custom_JenkinsHouse" or "Custom_JenkinsCellar" => helper.Translation.Get("location.sve_jenkins_house").Default("Jenkins' House").ToString(),
                "Custom_CastleVillageOutpost" => helper.Translation.Get("location.sve_castle_village").Default("Castle Village Outpost").ToString(),
                "Custom_CrimsonBadlands" => helper.Translation.Get("location.sve_crimson_badlands").Default("Crimson Badlands").ToString(),
                "Custom_SpriteSpring" => helper.Translation.Get("location.sve_sprite_spring").Default("Sprite Spring").ToString(),
                "Custom_ShearwaterBridge" => helper.Translation.Get("location.sve_shearwater_bridge").Default("Shearwater Bridge").ToString(),
                "Custom_FableReef" => helper.Translation.Get("location.sve_fable_reef").Default("Fable Reef").ToString(),
                "Custom_GrampletonFields" => helper.Translation.Get("location.sve_grampleton_fields").Default("Grampleton Fields").ToString(),
                "Custom_GrampletonSuburbs" => helper.Translation.Get("location.sve_grampleton_suburbs").Default("Grampleton Suburbs").ToString(),
                "Custom_FairhavenFarm" => helper.Translation.Get("location.sve_fairhaven_farm").Default("Fairhaven Farm").ToString(),
                "Custom_ApplesRoom" => helper.Translation.Get("location.sve_apples_room").Default("Apples' Room").ToString(),
                "Custom_EnchantedGrove" => helper.Translation.Get("location.sve_enchanted_grove").Default("Enchanted Grove").ToString(),
                "Custom_JunimoWoods" => helper.Translation.Get("location.sve_junimo_woods").Default("Junimo Woods").ToString(),
                "Custom_WesternForest" => helper.Translation.Get("location.sve_western_forest").Default("Western Forest").ToString(),
                "Custom_Highlands" or "Custom_HighlandsCavern" => helper.Translation.Get("location.sve_highlands").Default("The Highlands").ToString(),

                _ => null
            };
        }

        public static string CleanLocationName(string name, IModHelper helper)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Stardew Valley";

            if (name.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(7);
            }

            name = name.Replace('_', ' ');
            name = PascalCaseRegex1.Replace(name, "$1 $2");
            name = PascalCaseRegex2.Replace(name, "$1 $2");
            name = name.Trim();

            string locale = helper.Translation.Locale?.ToLowerInvariant() ?? "";
            bool isSpanish = locale.StartsWith("es");

            if (isSpanish)
            {
                if (name.EndsWith(" House", StringComparison.OrdinalIgnoreCase))
                {
                    string person = name.Substring(0, name.Length - 6).Trim();
                    return $"Casa de {person}";
                }
                if (name.EndsWith(" Shed", StringComparison.OrdinalIgnoreCase))
                {
                    string person = name.Substring(0, name.Length - 5).Trim();
                    return $"Cobertizo de {person}";
                }
                if (name.EndsWith(" Room", StringComparison.OrdinalIgnoreCase))
                {
                    string person = name.Substring(0, name.Length - 5).Trim();
                    return $"Habitación de {person}";
                }
                if (name.EndsWith(" Vineyard", StringComparison.OrdinalIgnoreCase))
                {
                    string place = name.Substring(0, name.Length - 9).Trim();
                    return $"Viñedo {place}";
                }
                if (name.EndsWith(" Farm", StringComparison.OrdinalIgnoreCase))
                {
                    string place = name.Substring(0, name.Length - 5).Trim();
                    return $"Granja {place}";
                }
            }

            return name;
        }
    }
}
