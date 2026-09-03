using Microsoft.Xna.Framework;

namespace StardewDiscordRPC.Framework.Rendering
{
    /// <summary>
    /// Utility methods for resolving Stardew Valley emote source rectangles and scales.
    /// </summary>
    public static class EmoteHelper
    {
        /// <summary>
        /// Returns the exact 16x16 source rectangle for the fully-formed emote frame (column 4, index 3)
        /// from Game1.emoteSpriteSheet.
        /// </summary>
        public static Rectangle GetEmoteSourceRect(int emoteId)
        {
            return emoteId switch
            {
                1 => new Rectangle(48, 80, 16, 16),   // Heart (Row 5, Tile 20)
                2 => new Rectangle(48, 224, 16, 16),  // Music (Row 14, Tile 56)
                3 => new Rectangle(48, 128, 16, 16),  // Happy (Row 8, Tile 32)
                4 => new Rectangle(48, 96, 16, 16),   // Sleep (Row 6, Tile 24)
                5 => new Rectangle(48, 64, 16, 16),   // Exclamation (Row 4, Tile 16)
                6 => new Rectangle(48, 32, 16, 16),   // Question (Row 2, Tile 8)
                7 => new Rectangle(48, 208, 16, 16),  // Gamepad (Row 13, Tile 52)
                8 => new Rectangle(48, 240, 16, 16),  // Blush (Row 15, Tile 60)
                _ => Rectangle.Empty
            };
        }
    }
}
