using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewPresence.Framework.Models;
using StardewValley;

namespace StardewPresence.Framework.Rendering
{
    /// <summary>
    /// Unified rendering engine for farmer portrait cards and the live in-game portrait editor.
    /// </summary>
    public static class FarmerSceneRenderer
    {
        /// <summary>
        /// Renders the farmer onto an isolated RenderTarget at 1:1 scale with PointClamp,
        /// ensuring Fashion Sense, custom clothing, and non-standard textures render without blur,
        /// out-of-bounds dye layer clamping, or lost scale matrices.
        /// </summary>
        public static RenderTarget2D RenderFarmerToTexture(
            GraphicsDevice graphicsDevice,
            Farmer farmer,
            int frame,
            bool flip,
            int facingDirection,
            int targetWidth = 192,
            int targetHeight = 192)
        {
            var previousTargets = graphicsDevice.GetRenderTargets();

            var farmerRT = new RenderTarget2D(
                graphicsDevice,
                targetWidth,
                targetHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents
            );

            graphicsDevice.SetRenderTarget(farmerRT);
            graphicsDevice.Clear(Color.Transparent);

            using (var fBatch = new SpriteBatch(graphicsDevice))
            {
                fBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    null,
                    null
                );

                bool oldDrawingForUI = FarmerRenderer.isDrawingForUI;
                int oldFacingDirection = farmer.FacingDirection;

                try
                {
                    FarmerRenderer.isDrawingForUI = true;

                    // Synchronize facing direction with frame and flip so clothes, hats, hair, and accessories
                    // match the body orientation (FarmerRenderer and Fashion Sense rely on farmer.FacingDirection)
                    int effectiveFacingDirection = GetFacingDirectionFromFrame(frame, flip);
                    farmer.FacingDirection = effectiveFacingDirection;

                    // Center within the 192x192 target (farmer base sprite is 64x96)
                    Vector2 farmerPos = new Vector2(64f, 32f);

                    farmer.FarmerRenderer.draw(
                        fBatch,
                        farmer,
                        Math.Max(0, frame),
                        farmerPos,
                        0.8f,
                        flip
                    );
                }
                finally
                {
                    FarmerRenderer.isDrawingForUI = oldDrawingForUI;
                    farmer.FacingDirection = oldFacingDirection;
                }

                fBatch.End();
            }

            graphicsDevice.SetRenderTargets(previousTargets);
            graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            return farmerRT;
        }

        /// <summary>
        /// Derives the appropriate facing direction (0=Up, 1=Right, 2=Down, 3=Left)
        /// from the specified farmer frame and horizontal flip state.
        /// </summary>
        public static int GetFacingDirectionFromFrame(int frame, bool flip = false)
        {
            int dir;
            switch (frame)
            {
                // --- DOWN / FRONT (2) ---
                // Walk down (0..5)
                case 0: case 1: case 2: case 3: case 4: case 5:
                // Run down (18..19)
                case 18: case 19:
                // Sword swing down (24..29)
                case 24: case 25: case 26: case 27: case 28: case 29:
                // Hoe / Water down (54..57)
                case 54: case 55: case 56: case 57:
                // Tool swing / Fishing / Harvesting / Milking / Poses down (66..71, 74..75, 82..122)
                case 42: case 43: case 44: case 47:
                case 66: case 67: case 68: case 69: case 70: case 71:
                case 74: case 75:
                case 82: case 83: case 84: case 85: case 86: case 87: case 88: case 89:
                case 90: case 91: case 92: case 93: case 94: case 95: case 96: case 97:
                case 98: case 99: case 100: case 101: case 102: case 103: case 104: case 105:
                case 106: case 107: case 108: case 109: case 110: case 111: case 112: case 113:
                case 114: case 115: case 116: case 117: case 118: case 119: case 120: case 121: case 122:
                    dir = 2;
                    break;

                // --- RIGHT (1) ---
                // Walk right (6..11)
                case 6: case 7: case 8: case 9: case 10: case 11:
                // Run right (17, 20..21)
                case 17: case 20: case 21:
                // Sword swing right (30..35)
                case 30: case 31: case 32: case 33: case 34: case 35:
                // Tool right / Axe / Pickaxe (45, 48..53)
                case 45: case 48: case 49: case 50: case 51: case 52: case 53:
                // Hoe / Water right (58..61)
                case 58: case 59: case 60: case 61:
                // Fish / Cast right (72..73, 80..81)
                case 72: case 73: case 80: case 81:
                    dir = 1;
                    break;

                // --- UP / BACK (0) ---
                // Walk up (12..16)
                case 12: case 13: case 14: case 15: case 16:
                // Run up (22..23)
                case 22: case 23:
                // Sword swing up (36..41)
                case 36: case 37: case 38: case 39: case 40: case 41:
                // Tool up (46)
                case 46:
                // Hoe / Water up (62..65)
                case 62: case 63: case 64: case 65:
                // Fish / Cast up (76..79)
                case 76: case 77: case 78: case 79:
                    dir = 0;
                    break;

                // --- LEFT (3) ---
                case 123: case 124: case 125:
                    dir = 3;
                    break;

                default:
                    dir = 2;
                    break;
            }

            if (flip)
            {
                if (dir == 1) dir = 3;
                else if (dir == 3) dir = 1;
            }

            return dir;
        }

        /// <summary>
        /// Pre-renders isolated textures and draws the complete portrait scene onto the specified RenderTarget2D.
        /// Handles background, companions, farmer, and emotes with proper layer ordering and zero GPU state conflicts.
        /// </summary>
        public static void RenderSceneToTarget(
            GraphicsDevice graphicsDevice,
            RenderTarget2D target,
            Farmer farmer,
            Farmer? spouseFarmer,
            NPC? spouseNpc,
            NPC? petNpc,
            Texture2D? bgTexture,
            ModConfig config,
            UILayout layout,
            int cardWidth = 256,
            int cardHeight = 256)
        {
            int cFrame = Math.Max(0, config.SpouseFrame);
            bool cFlip = config.SpouseFlip;

            // Pre-render isolated textures BEFORE binding the target to prevent target detachment in DirectX
            RenderTarget2D? farmerRT = null;
            if (config.ShowFarmer)
            {
                int effectiveDir = GetFacingDirectionFromFrame(config.FarmerFrame, config.FarmerFlip);
                config.FarmerFacingDirection = effectiveDir;

                farmerRT = RenderFarmerToTexture(
                    graphicsDevice,
                    farmer,
                    config.FarmerFrame,
                    config.FarmerFlip,
                    effectiveDir
                );
            }

            RenderTarget2D? spouseRT = null;
            if (spouseFarmer != null && config.ShowCompanion && config.ShowSpouse)
            {
                int effectiveSpouseDir = GetFacingDirectionFromFrame(cFrame, cFlip);
                config.SpouseFacingDirection = effectiveSpouseDir;

                spouseRT = RenderFarmerToTexture(
                    graphicsDevice,
                    spouseFarmer,
                    cFrame,
                    cFlip,
                    effectiveSpouseDir
                );
            }

            var previousTargets = graphicsDevice.GetRenderTargets();

            try
            {
                graphicsDevice.SetRenderTarget(target);
                graphicsDevice.Clear(Color.Transparent);

                using var spriteBatch = new SpriteBatch(graphicsDevice);

                // Draw Background
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                if (bgTexture != null)
                {
                    spriteBatch.Draw(bgTexture, new Rectangle(0, 0, cardWidth, cardHeight), Color.White);
                }
                spriteBatch.End();

                // Compute Coordinates and Scaling
                bool hasSpouse = (spouseNpc?.Sprite?.Texture != null) || (spouseFarmer != null);
                bool hasPet = petNpc?.Sprite?.Texture != null;
                bool hasBoth = hasSpouse && hasPet && config.ShowSpouse && config.ShowPet && config.ShowCompanion;
                bool hasSingleCompanion = (hasSpouse || hasPet) && !hasBoth;

                float baseScale = config.FarmerScale > 0.5f ? config.FarmerScale : 2.3f;
                float localX = ((256f / baseScale) - 64f) / 2f + (hasSingleCompanion ? 12f : 0f) + config.FarmerOffsetX;
                float localY = (36f / baseScale) + config.FarmerOffsetY;
                Vector2 localPos = new Vector2(localX, localY);

                Matrix scaleMatrix = Matrix.CreateScale(baseScale, baseScale, 1f);
                spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    null,
                    null,
                    null,
                    scaleMatrix
                );

                Action drawSpouseAction = () =>
                {
                    if (!hasSpouse || (!config.ShowCompanion || !config.ShowSpouse)) return;

                    float cOffsetX = config.SpouseOffsetX;
                    float cOffsetY = config.SpouseOffsetY;
                    float cScale   = config.SpouseScale > 0 ? config.SpouseScale : 4.0f;
                    int   cEmote   = config.SpouseEmote;
                    float cEmoteX  = config.SpouseEmoteOffsetX;
                    float cEmoteY  = config.SpouseEmoteOffsetY;
                    float cEmoteS  = config.SpouseEmoteScale;

                    Vector2 spousePos = new Vector2(localPos.X + cOffsetX, localPos.Y + cOffsetY);
                    SpriteEffects compEffects = cFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                    if (spouseNpc?.Sprite?.Texture != null)
                    {
                        int spriteW = spouseNpc.Sprite.SpriteWidth > 0 ? spouseNpc.Sprite.SpriteWidth : 16;
                        int spriteH = spouseNpc.Sprite.SpriteHeight > 0 ? spouseNpc.Sprite.SpriteHeight : 32;
                        int framesPerRow = Math.Max(1, spouseNpc.Sprite.Texture.Width / spriteW);
                        int totalRows = Math.Max(1, spouseNpc.Sprite.Texture.Height / spriteH);
                        int maxFrames = Math.Max(1, framesPerRow * totalRows);

                        int frameIdx = cFrame % maxFrames;
                        int row = frameIdx / framesPerRow;
                        int col = frameIdx % framesPerRow;

                        Rectangle sourceRect = new Rectangle(col * spriteW, row * spriteH, spriteW, spriteH);

                        spriteBatch.Draw(
                            spouseNpc.Sprite.Texture,
                            spousePos,
                            sourceRect,
                            Color.White,
                            0f,
                            Vector2.Zero,
                            cScale,
                            compEffects,
                            0.4f
                        );
                    }
                    else if (spouseFarmer != null && spouseRT != null)
                    {
                        Vector2 spouseRTPos = new Vector2(
                            MathF.Round(spousePos.X - 64f),
                            MathF.Round(spousePos.Y - 32f)
                        );
                        graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
                        spriteBatch.Draw(spouseRT, spouseRTPos, null, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.4f);
                    }

                    // Spouse Emote
                    float layoutEmoteX = layout.SpouseEmoteOffsetX;
                    float layoutEmoteY = layout.SpouseEmoteOffsetY;
                    float layoutEmoteS = layout.SpouseEmoteScale;

                    Rectangle compEmoteSrc = EmoteHelper.GetEmoteSourceRect(cEmote);
                    if (!compEmoteSrc.IsEmpty && Game1.emoteSpriteSheet != null)
                    {
                        float eScale = (cEmoteS > 0 ? cEmoteS : 1.0f) * (layoutEmoteS > 0 ? layoutEmoteS : 1.0f);
                        Vector2 emotePos = new Vector2(
                            spousePos.X + layoutEmoteX + cEmoteX,
                            spousePos.Y + layoutEmoteY + cEmoteY
                        );
                        spriteBatch.Draw(Game1.emoteSpriteSheet, emotePos, compEmoteSrc, Color.White, 0f, Vector2.Zero, eScale, SpriteEffects.None, 0.95f);
                    }
                };

                Action drawPetAction = () =>
                {
                    if (!hasPet || (!config.ShowCompanion || !config.ShowPet)) return;

                    float pOffsetX = config.PetOffsetX;
                    float pOffsetY = config.PetOffsetY;
                    float pScale   = config.PetScale > 0 ? config.PetScale : 3.2f;
                    int   pFrame   = Math.Max(0, config.PetFrame);
                    bool  pFlip    = config.PetFlip;
                    int   pEmote   = config.PetEmote;
                    float pEmoteX  = config.PetEmoteOffsetX;
                    float pEmoteY  = config.PetEmoteOffsetY;
                    float pEmoteS  = config.PetEmoteScale;

                    SpriteEffects petEffects = pFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    Vector2 petPos = new Vector2(localPos.X + pOffsetX, localPos.Y + pOffsetY);

                    if (petNpc?.Sprite?.Texture != null)
                    {
                        int spriteWidth = petNpc.Sprite.SpriteWidth > 0 ? petNpc.Sprite.SpriteWidth : 32;
                        int spriteHeight = petNpc.Sprite.SpriteHeight > 0 ? petNpc.Sprite.SpriteHeight : 32;
                        int framesPerRow = Math.Max(1, petNpc.Sprite.Texture.Width / spriteWidth);
                        int totalRows = Math.Max(1, petNpc.Sprite.Texture.Height / spriteHeight);
                        int maxFrames = Math.Max(1, framesPerRow * totalRows);

                        int safeFrame = pFrame % maxFrames;
                        int row = safeFrame / framesPerRow;
                        int col = safeFrame % framesPerRow;

                        Rectangle sourceRect = new Rectangle(
                            col * spriteWidth,
                            row * spriteHeight,
                            spriteWidth,
                            spriteHeight
                        );

                        spriteBatch.Draw(
                            petNpc.Sprite.Texture,
                            petPos,
                            sourceRect,
                            Color.White,
                            0f,
                            Vector2.Zero,
                            pScale,
                            petEffects,
                            0.4f
                        );
                    }

                    // Pet Emote
                    float layoutEmoteX = layout.PetEmoteOffsetX;
                    float layoutEmoteY = layout.PetEmoteOffsetY;
                    float layoutEmoteS = layout.PetEmoteScale;

                    Rectangle compEmoteSrc = EmoteHelper.GetEmoteSourceRect(pEmote);
                    if (!compEmoteSrc.IsEmpty && Game1.emoteSpriteSheet != null)
                    {
                        float eScale = (pEmoteS > 0 ? pEmoteS : 1.0f) * (layoutEmoteS > 0 ? layoutEmoteS : 1.0f);
                        Vector2 emotePos = new Vector2(
                            petPos.X + layoutEmoteX + pEmoteX,
                            petPos.Y + layoutEmoteY + pEmoteY
                        );
                        spriteBatch.Draw(Game1.emoteSpriteSheet, emotePos, compEmoteSrc, Color.White, 0f, Vector2.Zero, eScale, SpriteEffects.None, 0.95f);
                    }
                };

                Action drawFarmerAction = () =>
                {
                    if (!config.ShowFarmer || farmerRT == null) return;

                    Vector2 farmerRTPos = new Vector2(
                        MathF.Round(localPos.X - 64f),
                        MathF.Round(localPos.Y - 32f)
                    );

                    graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
                    spriteBatch.Draw(
                        farmerRT,
                        farmerRTPos,
                        null,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        1f,
                        SpriteEffects.None,
                        0.8f
                    );

                    // Farmer Emote
                    Rectangle farmerEmoteSrc = EmoteHelper.GetEmoteSourceRect(config.FarmerEmote);
                    if (!farmerEmoteSrc.IsEmpty && Game1.emoteSpriteSheet != null)
                    {
                        float eScale = (config.FarmerEmoteScale > 0 ? config.FarmerEmoteScale : 1.0f) * layout.FarmerEmoteScale;
                        Vector2 emotePos = new Vector2(
                            localPos.X + layout.FarmerEmoteOffsetX + config.FarmerEmoteOffsetX,
                            localPos.Y + layout.FarmerEmoteOffsetY + config.FarmerEmoteOffsetY
                        );
                        spriteBatch.Draw(Game1.emoteSpriteSheet, emotePos, farmerEmoteSrc, Color.White, 0f, Vector2.Zero, eScale, SpriteEffects.None, 0.95f);
                    }
                };

                // Draw in specified layer order
                if (!config.PetLayerFront) drawPetAction();
                if (!config.SpouseLayerFront) drawSpouseAction();

                drawFarmerAction();

                if (config.SpouseLayerFront) drawSpouseAction();
                if (config.PetLayerFront) drawPetAction();

                spriteBatch.End();
            }
            finally
            {
                graphicsDevice.SetRenderTargets(previousTargets);
                farmerRT?.Dispose();
                spouseRT?.Dispose();
            }
        }
    }
}
