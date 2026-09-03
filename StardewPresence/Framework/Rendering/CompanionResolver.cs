using System;
using StardewValley;
using StardewValley.Characters;

namespace StardewPresence.Framework.Rendering
{
    /// <summary>
    /// Handles discovery and resolution of farmer companions (Spouses, Pets, Horses).
    /// </summary>
    public static class CompanionResolver
    {
        public static (NPC? spouseNpc, Farmer? spouseFarmer, NPC? petNpc) GetCompanions(Farmer farmer, ModConfig config)
        {
            if (config.CompanionType == 4) // None
            {
                return (null, null, null);
            }

            if (config.CompanionType == 1) // Forced Spouse
            {
                var (spNpc, spFarmer) = GetSpouse(farmer);
                return (spNpc, spFarmer, null);
            }

            if (config.CompanionType == 2) // Forced Pet
            {
                return (null, null, GetPet(farmer));
            }

            if (config.CompanionType == 3) // Forced Horse
            {
                return (null, null, GetHorse(farmer));
            }

            // Auto (0): Both Spouse and Pet (or Horse if no pet)
            var (spouseNpc, spouseFarmer) = GetSpouse(farmer);
            NPC? petNpc = GetPet(farmer) ?? GetHorse(farmer);
            return (spouseNpc, spouseFarmer, petNpc);
        }

        public static (NPC? npc, Farmer? spouseFarmer, bool isPet) GetCompanion(Farmer farmer, ModConfig config)
        {
            var (spouseNpc, spouseFarmer, petNpc) = GetCompanions(farmer, config);
            if (spouseNpc != null || spouseFarmer != null)
            {
                return (spouseNpc, spouseFarmer, false);
            }
            if (petNpc != null)
            {
                return (petNpc, null, true);
            }
            return (null, null, false);
        }

        public static (NPC? npc, Farmer? spouseFarmer) GetSpouse(Farmer farmer)
        {
            NPC? spouseNpc = farmer.getSpouse() ?? (!string.IsNullOrEmpty(farmer.spouse) ? Game1.getCharacterFromName(farmer.spouse) : null);
            Farmer? spouseFarmer = null;

            if (spouseNpc == null)
            {
                long? spouseId = farmer.team?.GetSpouse(farmer.UniqueMultiplayerID);
                if (spouseId.HasValue)
                {
                    spouseFarmer = Game1.GetPlayer(spouseId.Value);
                }
            }

            return (spouseNpc, spouseFarmer);
        }

        public static NPC? GetPet(Farmer farmer)
        {
            try
            {
                if (farmer.getPet() is NPC petNpc && petNpc.Sprite?.Texture != null) return petNpc;
                if (Game1.player?.getPet() is NPC mainPet && mainPet.Sprite?.Texture != null) return mainPet;

                var farmHouse = Game1.getLocationFromName("FarmHouse");
                if (farmHouse != null)
                {
                    foreach (var c in farmHouse.characters)
                    {
                        if (c is Pet p && p.Sprite?.Texture != null) return p;
                    }
                }

                if (farmer.currentLocation != null && farmer.currentLocation != farmHouse)
                {
                    foreach (var c in farmer.currentLocation.characters)
                    {
                        if (c is Pet p && p.Sprite?.Texture != null) return p;
                    }
                }

                var farm = Game1.getFarm();
                if (farm != null && farm != farmer.currentLocation && farm != farmHouse)
                {
                    foreach (var c in farm.characters)
                    {
                        if (c is Pet p && p.Sprite?.Texture != null) return p;
                    }
                }

                if (Game1.locations != null)
                {
                    foreach (var loc in Game1.locations)
                    {
                        if (loc == null || loc == farmHouse || loc == farm || loc == farmer.currentLocation) continue;
                        foreach (var c in loc.characters)
                        {
                            if (c is Pet p && p.Sprite?.Texture != null) return p;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public static NPC? GetHorse(Farmer farmer)
        {
            try
            {
                if (farmer.mount is NPC mountNpc && mountNpc.Sprite?.Texture != null) return mountNpc;

                if (farmer.currentLocation != null)
                {
                    foreach (var c in farmer.currentLocation.characters)
                    {
                        if (c is Horse h && h.Sprite?.Texture != null) return h;
                    }
                }

                var farm = Game1.getFarm();
                if (farm != null && farm != farmer.currentLocation)
                {
                    foreach (var c in farm.characters)
                    {
                        if (c is Horse h && h.Sprite?.Texture != null) return h;
                    }
                }

                if (Game1.locations != null)
                {
                    foreach (var loc in Game1.locations)
                    {
                        if (loc == null || loc == farm || loc == farmer.currentLocation) continue;
                        foreach (var c in loc.characters)
                        {
                            if (c is Horse h && h.Sprite?.Texture != null) return h;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// Converts game facing direction (0=Up/Back, 1=Right, 2=Down/Front, 3=Left)
        /// to standard NPC spritesheet row (Row 0=Down, Row 1=Right, Row 2=Up, Row 3=Left).
        /// </summary>
        public static int GetNpcRowForDirection(int direction)
        {
            return direction switch
            {
                2 => 0, // Down (Front) -> Row 0
                1 => 1, // Right        -> Row 1
                0 => 2, // Up (Back)    -> Row 2
                3 => 3, // Left         -> Row 3
                _ => 0
            };
        }
    }
}
