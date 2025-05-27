using System;
using System.Collections.Generic;

using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Infiniminer
{
    public class InterfaceEngine
    {
        InfiniminerGame gameInstance;
        PropertyBag _P;
        SpriteBatch spriteBatch;
        public SpriteFont uiFont, radarFont;
        Rectangle drawRect;

        Texture2D texCrosshairs, texBlank, texHelp;
        Texture2D texRadarBackground, texRadarForeground, texRadarPlayerSame, texRadarPlayerAbove, texRadarPlayerBelow, texRadarPlayerPing, texRadarNorth;
        Texture2D texToolRadarRed, texToolRadarBlue, texToolRadarGold, texToolRadarDiamond, texToolRadarArtcase, texToolRadarLED, texToolRadarPointer, texToolRadarFlash;
        Texture2D texToolDetonatorDownRed, texToolDetonatorUpRed, texToolDetonatorDownBlue, texToolDetonatorUpBlue;
        Texture2D texToolBuild, texToolBuildCharge, texToolBuildBlast, texToolBuildSmoke;

        Dictionary<BlockType, Texture2D> blockIcons = new Dictionary<BlockType, Texture2D>();

        public InterfaceEngine(InfiniminerGame gameInstance)
        {
            this.gameInstance = gameInstance;
            spriteBatch = new SpriteBatch(gameInstance.GraphicsDevice);

            // Load textures.
            texCrosshairs = gameInstance.Content.Load<Texture2D>("ui/tex_ui_crosshair");
            texBlank = new Texture2D(gameInstance.GraphicsDevice, 1, 1);
            texBlank.SetData(new uint[1] { 0xFFFFFFFF });
            texRadarBackground = gameInstance.Content.Load<Texture2D>("ui/tex_radar_background");
            texRadarForeground = gameInstance.Content.Load<Texture2D>("ui/tex_radar_foreground");
            texRadarPlayerSame = gameInstance.Content.Load<Texture2D>("ui/tex_radar_player_same");
            texRadarPlayerAbove = gameInstance.Content.Load<Texture2D>("ui/tex_radar_player_above");
            texRadarPlayerBelow = gameInstance.Content.Load<Texture2D>("ui/tex_radar_player_below");
            texRadarPlayerPing = gameInstance.Content.Load<Texture2D>("ui/tex_radar_player_ping");
            texRadarNorth = gameInstance.Content.Load<Texture2D>("ui/tex_radar_north");
            texHelp = gameInstance.Content.Load<Texture2D>("menus/tex_menu_help");

            texToolRadarRed = gameInstance.Content.Load<Texture2D>("tools/tex_tool_radar_red");
            texToolRadarBlue = gameInstance.Content.Load<Texture2D>("tools/tex_tool_radar_blue");
            texToolRadarGold = gameInstance.Content.Load<Texture2D>("tools/tex_tool_radar_screen_gold");
            texToolRadarDiamond = gameInstance.Content.Load<Texture2D>("tools/tex_tool_radar_screen_diamond");
            texToolRadarArtcase = gameInstance.Content.Load<Texture2D>("tools/tex_tool_radar_screen_artcase");
            texToolRadarLED = gameInstance.Content.Load<Texture2D>("tools/tex_tool_radar_led");
            texToolRadarPointer = gameInstance.Content.Load<Texture2D>("tools/tex_tool_radar_pointer");
            texToolRadarFlash = gameInstance.Content.Load<Texture2D>("tools/tex_tool_radar_flash");

            texToolBuild = gameInstance.Content.Load<Texture2D>("tools/tex_tool_build");
            texToolBuildCharge = gameInstance.Content.Load<Texture2D>("tools/tex_tool_build_charge");
            texToolBuildBlast = gameInstance.Content.Load<Texture2D>("tools/tex_tool_build_blast");
            texToolBuildSmoke = gameInstance.Content.Load<Texture2D>("tools/tex_tool_build_smoke");

            texToolDetonatorDownRed = gameInstance.Content.Load<Texture2D>("tools/tex_tool_detonator_down_red");
            texToolDetonatorUpRed = gameInstance.Content.Load<Texture2D>("tools/tex_tool_detonator_up_red");
            texToolDetonatorDownBlue = gameInstance.Content.Load<Texture2D>("tools/tex_tool_detonator_down_blue");
            texToolDetonatorUpBlue = gameInstance.Content.Load<Texture2D>("tools/tex_tool_detonator_up_blue");

            drawRect = new Rectangle(gameInstance.GraphicsDevice.Viewport.Width / 2 - 1024 / 2,
                                     gameInstance.GraphicsDevice.Viewport.Height / 2 - 768 / 2,
                                     1024,
                                     1024);

            // Load icons.
            blockIcons[BlockType.BankBlue] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_bank_blue");
            blockIcons[BlockType.BankRed] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_bank_red");
            blockIcons[BlockType.Explosive] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_explosive");
            blockIcons[BlockType.Jump] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_jump");
            blockIcons[BlockType.Water] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_water");
            blockIcons[BlockType.Ladder] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_ladder");
            blockIcons[BlockType.SolidBlue] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_solid_blue");
            blockIcons[BlockType.SolidRed] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_solid_red");
            blockIcons[BlockType.Shock] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_spikes");
            blockIcons[BlockType.TransBlue] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_translucent_blue");
            blockIcons[BlockType.TransRed] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_translucent_red");
            blockIcons[BlockType.GlassB] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_glassb");
            blockIcons[BlockType.GlassR] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_glassr");
            blockIcons[BlockType.BeaconRed] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_beacon");
            blockIcons[BlockType.BeaconBlue] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_beacon");
            blockIcons[BlockType.Road] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_road");
            blockIcons[BlockType.Generator] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_generator");
            blockIcons[BlockType.Controller] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_controller");
            blockIcons[BlockType.Pipe] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_pipe");
            blockIcons[BlockType.Lava] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_pipe");
            blockIcons[BlockType.StealthBlockB] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_stealth");
            blockIcons[BlockType.StealthBlockR] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_stealth");
            blockIcons[BlockType.TrapB] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_trap");
            blockIcons[BlockType.Metal] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_metal");
            blockIcons[BlockType.TrapR] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_trap");
            blockIcons[BlockType.Dirt] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_dirt");
            blockIcons[BlockType.Grass] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_grass");
            blockIcons[BlockType.Pump] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_pump");
            blockIcons[BlockType.Barrel] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_compressor");
            blockIcons[BlockType.Lever] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_lever");
            blockIcons[BlockType.Plate] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_plate");
            blockIcons[BlockType.MedicalR] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_medical");
            blockIcons[BlockType.MedicalB] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_medical");
            blockIcons[BlockType.Refinery] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_refinery");
            blockIcons[BlockType.Maintenance] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_maintenance");
            blockIcons[BlockType.RadarRed] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_radar");
            blockIcons[BlockType.RadarBlue] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_radar");
            blockIcons[BlockType.Hinge] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_hinge");
            blockIcons[BlockType.ArtCaseR] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_artcaser");
            blockIcons[BlockType.ArtCaseB] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_artcaseb");
            blockIcons[BlockType.ConstructionR] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_magmavent");
            blockIcons[BlockType.ConstructionB] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_magmavent");
            blockIcons[BlockType.ResearchR] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_research");
            blockIcons[BlockType.ResearchB] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_research");
            blockIcons[BlockType.InhibitorR] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_inhibitor");
            blockIcons[BlockType.InhibitorB] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_inhibitor");
            //held icons
            blockIcons[BlockType.Diamond] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_diamond");
            blockIcons[BlockType.MagmaVent] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_magmavent");
            blockIcons[BlockType.Gold] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_silver");
            blockIcons[BlockType.Ore] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_ore");
            blockIcons[BlockType.DirtSign] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_dirt_sign");
            blockIcons[BlockType.Rock] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_rock");
            blockIcons[BlockType.Sand] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_sand");
            blockIcons[BlockType.Spring] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_spring");
            blockIcons[BlockType.Mud] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_dirt");
            blockIcons[BlockType.SolidBlue2] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_solid_blue");
            blockIcons[BlockType.SolidRed2] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_solid_red");
            //blockIcons[BlockType.Spring] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_rock");
            blockIcons[BlockType.None] = gameInstance.Content.Load<Texture2D>("icons/tex_icon_deconstruction");

            // Load fonts.
            uiFont = gameInstance.Content.Load<SpriteFont>(gameInstance.font);
            uiFont = gameInstance.Content.Load<SpriteFont>(gameInstance.font);
            radarFont = gameInstance.Content.Load<SpriteFont>("font_04b03b");
        }

        public void RenderMessageCenter(SpriteBatch spriteBatch, string text, Vector2 pointCenter, Color colorText, Color colorBackground)
        {
            Vector2 textSize = uiFont.MeasureString(text);
            spriteBatch.Draw(texBlank, new Rectangle((int)(pointCenter.X - textSize.X / 2 - 10), (int)(pointCenter.Y - textSize.Y / 2 - 10), (int)(textSize.X + 20), (int)(textSize.Y + 20)), colorBackground);
            spriteBatch.DrawString(uiFont, text, pointCenter - textSize / 2, colorText);
        }

        private static bool MessageExpired(ChatMessage msg)
        {
            return msg.timestamp <= 0;
        }

        public void Update(GameTime gameTime)
        {
            if (_P == null)
                return;

            foreach (ChatMessage msg in _P.chatBuffer)
                msg.timestamp -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            _P.chatBuffer.RemoveAll(MessageExpired);

            int bufferSize = 10;
            if (_P.chatFullBuffer.Count > bufferSize)
                _P.chatFullBuffer.RemoveRange(bufferSize, _P.chatFullBuffer.Count - bufferSize);

            if (_P.constructionGunAnimation > 0)
            {
                if (_P.constructionGunAnimation > gameTime.ElapsedGameTime.TotalSeconds)
                    _P.constructionGunAnimation -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                else
                    _P.constructionGunAnimation = 0;
            }
            else
            {
                if (_P.constructionGunAnimation < -gameTime.ElapsedGameTime.TotalSeconds)
                    _P.constructionGunAnimation += (float)gameTime.ElapsedGameTime.TotalSeconds;
                else
                    _P.constructionGunAnimation = 0;
            }
        }

        public void RenderRadarBlip(SpriteBatch spriteBatch, Vector3 position, Color color, bool ping, string text)
        {
            // Figure out the relative position for the radar blip.
            Vector3 relativePosition = position - _P.playerPosition;
            float relativeAltitude = relativePosition.Y;
            relativePosition.Y = 0;
            Matrix rotationMatrix = Matrix.CreateRotationY(-_P.playerCamera.Yaw);
            relativePosition = Vector3.Transform(relativePosition, rotationMatrix) * 10;
            float relativeLength = Math.Min(relativePosition.Length(), 93);
            if (relativeLength != 0)
                relativePosition.Normalize();
            relativePosition *= relativeLength;

            // Draw the radar blip.
            if (text == "")
            {
                relativePosition.X = (int)relativePosition.X;
                relativePosition.Z = (int)relativePosition.Z;
                Texture2D texRadarSprite = texRadarPlayerSame;
                if (relativeAltitude > 2)
                    texRadarSprite = texRadarPlayerAbove;
                else if (relativeAltitude < -2)
                    texRadarSprite = texRadarPlayerBelow;
                spriteBatch.Draw(texRadarSprite, new Vector2(10 + 99 + relativePosition.X - texRadarSprite.Width / 2, 30 + 99 + relativePosition.Z - texRadarSprite.Height / 2), color);
                if (ping)
                    spriteBatch.Draw(texRadarPlayerPing, new Vector2(10 + 99 + relativePosition.X - texRadarPlayerPing.Width / 2, 30 + 99 + relativePosition.Z - texRadarPlayerPing.Height / 2), color);
            }

            // Render text.
            if (text != "")
            {
                relativePosition *= 0.9f;
                relativePosition.X = (int)relativePosition.X;
                relativePosition.Z = (int)relativePosition.Z;

                if (text == "NORTH")
                {
                    spriteBatch.Draw(texRadarNorth, new Vector2(10 + 99 + relativePosition.X - texRadarNorth.Width / 2, 30 + 99 + relativePosition.Z - texRadarNorth.Height / 2), color);
                }
                else
                {
                    if (relativeAltitude > 2)
                        text += " ^";
                    else if (relativeAltitude < -2)
                        text += " v";
                    Vector2 textSize = radarFont.MeasureString(text);
                    spriteBatch.DrawString(radarFont, text, new Vector2(10 + 99 + relativePosition.X - textSize.X / 2, 30 + 99 + relativePosition.Z - textSize.Y / 2), color);
                }
            }
            //if (_P.playerClass == PlayerClass.Prospector)
            //{
            if(!Keyboard.GetState().IsKeyDown(Keys.Tab))
            if (_P.temperature < 30 && _P.temperature > 0)
                    spriteBatch.DrawString(uiFont, "Temp: " + _P.temperature, new Vector2(200, 200), Color.Blue);
            else if (_P.temperature > 30 && _P.temperature < 71)
                    spriteBatch.DrawString(uiFont, "Temp: " + _P.temperature, new Vector2(200, 200), Color.Yellow);
            else if (_P.temperature > 70)
                    spriteBatch.DrawString(uiFont, "Temp: " + _P.temperature, new Vector2(200, 200), Color.Red);
            //}
        }

        public void RenderDetonator(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            int screenWidth = graphicsDevice.Viewport.Width;
            int screenHeight = graphicsDevice.Viewport.Height;

            Texture2D textureToUse;
            if (Mouse.GetState().LeftButton == ButtonState.Pressed || Mouse.GetState().MiddleButton == ButtonState.Pressed || Mouse.GetState().RightButton == ButtonState.Pressed)
                textureToUse = _P.playerTeam == PlayerTeam.Red ? texToolDetonatorDownRed : texToolDetonatorDownBlue;
            else
                textureToUse = _P.playerTeam == PlayerTeam.Red ? texToolDetonatorUpRed : texToolDetonatorUpBlue;

            spriteBatch.Draw(textureToUse, new Rectangle(screenWidth / 2 /*- 22 * 3*/, screenHeight - 77 * 3 + 14 * 3, 75 * 3, 77 * 3), Color.White);
        }

        public void RenderRemote(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            int screenWidth = graphicsDevice.Viewport.Width;
            int screenHeight = graphicsDevice.Viewport.Height;

            Texture2D textureToUse;
            if (Mouse.GetState().RightButton == ButtonState.Pressed)
                textureToUse = _P.playerTeam == PlayerTeam.Red ? texToolDetonatorDownRed : texToolDetonatorDownBlue;
            else
                textureToUse = _P.playerTeam == PlayerTeam.Red ? texToolDetonatorUpRed : texToolDetonatorUpBlue;

            spriteBatch.Draw(textureToUse, new Rectangle(screenWidth / 2 /*- 22 * 3*/, screenHeight - 77 * 3 + 14 * 3, 75 * 3, 77 * 3), Color.White);
        }

        public void RenderProspectron(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            int screenWidth = graphicsDevice.Viewport.Width;
            int screenHeight = graphicsDevice.Viewport.Height;

            int drawX = screenWidth / 2 - 32 * 3;
            int drawY = screenHeight - 102 * 3;

            spriteBatch.Draw(_P.playerTeam == PlayerTeam.Red ? texToolRadarRed : texToolRadarBlue, new Rectangle(drawX, drawY, 70 * 3, 102 * 3), Color.White);

            if (_P.radarValue > 0)
                spriteBatch.Draw(texToolRadarLED, new Rectangle(drawX, drawY, 70 * 3, 102 * 3), Color.White);
            if (_P.radarValue == 200)
                spriteBatch.Draw(texToolRadarGold, new Rectangle(drawX, drawY, 70 * 3, 102 * 3), Color.White);
            if (_P.radarValue == 1000)
                spriteBatch.Draw(texToolRadarDiamond, new Rectangle(drawX, drawY, 70 * 3, 102 * 3), Color.White);
            if (_P.radarValue == 2000)
                spriteBatch.Draw(texToolRadarArtcase, new Rectangle(drawX, drawY, 70 * 3, 102 * 3), Color.White);
            if (_P.playerToolCooldown > 0.2f)
                spriteBatch.Draw(texToolRadarFlash, new Rectangle(drawX, drawY, 70 * 3, 102 * 3), Color.White);

            int pointerOffset = (int)(30 - _P.radarDistance) / 2;  // ranges from 0 to 15 inclusive
            if (_P.radarDistance == 30)
                pointerOffset = 15;
            spriteBatch.Draw(texToolRadarPointer, new Rectangle(drawX + 54 * 3, drawY + 20 * 3 + pointerOffset * 3, 4 * 3, 5 * 3), Color.White);
        }

        public void RenderConstructionGun(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, BlockType blockType)
        {
            int screenWidth = graphicsDevice.Viewport.Width;
            int screenHeight = graphicsDevice.Viewport.Height;

            int drawX = screenWidth / 2 - 60 * 3;
            int drawY = screenHeight - 91 * 3;

            Texture2D gunSprite = texToolBuild;
            if (_P.constructionGunAnimation < -0.001)
                gunSprite = texToolBuildCharge;
            else if (_P.constructionGunAnimation > 0.3)
                gunSprite = texToolBuildBlast;
            else if (_P.constructionGunAnimation > 0.001)
                gunSprite = texToolBuildSmoke;
            spriteBatch.Draw(gunSprite, new Rectangle(drawX, drawY, 120 * 3, 126 * 3), Color.White);
            spriteBatch.Draw(blockIcons[blockType], new Rectangle(drawX + 37 * 3, drawY + 50 * 3, 117, 63), Color.White);
            if (BlockInformation.GetCost(blockType) < 1000 && BlockInformation.GetCost(blockType) > 0)
            spriteBatch.DrawString(uiFont, ""+BlockInformation.GetCost(blockType), new Vector2(drawX + 180, drawY + 186), Color.White);
        }

        public void drawChat(List<ChatMessage>messages, GraphicsDevice graphicsDevice)
        {
            int newlines = 0;
            for (int i = 0; i < messages.Count; i++)
            {
                Color chatColor = Color.White;
                if (messages[i].type == ChatMessageType.SayRedTeam)
                    chatColor = _P.red;// Defines.IM_RED;
                if (messages[i].type == ChatMessageType.SayBlueTeam)
                    chatColor = _P.blue;// Defines.IM_BLUE;

                int y = graphicsDevice.Viewport.Height - 114;
                newlines += messages[i].newlines;
                y -= 18 * newlines;
                //y -= 16 * i;

                spriteBatch.DrawString(uiFont, messages[i].message, new Vector2(22, y), Color.Black);
                spriteBatch.DrawString(uiFont, messages[i].message, new Vector2(20, y-2), chatColor);//graphicsDevice.Viewport.Height - 116 - 16 * i), chatColor);
            }
        }

        public void Render(GraphicsDevice graphicsDevice)
        {
            // If we don't have _P, grab it from the current gameInstance.
            // We can't do this in the constructor because we are created in the property bag's constructor!
            if (_P == null)
                _P = gameInstance.propertyBag;

            // Draw the UI.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

            if (_P.screenEffect == ScreenEffect.Death)
            {
                Color drawColor = new Color(1 - (float)_P.screenEffectCounter * 0.5f, 0f, 0f);
                spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), drawColor);
                if (_P.screenEffectCounter >= 2)
                    RenderMessageCenter(spriteBatch, "You may not respawn yet.", new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2), Color.White, Color.Black);
            }
            else if (_P.screenEffect == ScreenEffect.Respawn)
            {
                Color drawColor = new Color(0f, 0f, 0f);
                spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), drawColor);
                if (_P.screenEffectCounter >= 2)
                    RenderMessageCenter(spriteBatch, "You may now respawn.", new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2), Color.White, Color.Black);
            }
            else if (_P.screenEffect == ScreenEffect.Teleport || _P.screenEffect == ScreenEffect.Explosion)
            {
                Color drawColor = new Color(1, 1, 1, 1 - (float)_P.screenEffectCounter * 0.5f);
                spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), drawColor);
                if (_P.screenEffectCounter > 2)
                    _P.screenEffect = ScreenEffect.None;
            }
            else if (_P.screenEffect == ScreenEffect.Earthquake)
            {
                //Color drawColor = new Color(1, 1, 1, 1 - (float)_P.screenEffectCounter * 0.5f);
                //spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), drawColor);
                if (_P.screenEffectCounter > 2)
                    _P.screenEffect = ScreenEffect.None;
            }
            else if (_P.screenEffect == ScreenEffect.Fall)
            {
                Color drawColor = new Color(1, 0, 0, 1 - (float)_P.screenEffectCounter * 0.5f);
                spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), drawColor);
                if (_P.screenEffectCounter > 2)
                    _P.screenEffect = ScreenEffect.None;
            }
            else if (_P.screenEffect == ScreenEffect.Water)
            {
                Color drawColor = new Color(0, 0, 1, 1 - (float)_P.screenEffectCounter);
                spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), drawColor);
                if (_P.screenEffectCounter > 2)
                    _P.screenEffect = ScreenEffect.None;
            }
            else if (_P.screenEffect == ScreenEffect.Drown)
            {
                Color drawColor = new Color(0.5f, 0, 0.8f, 0.25f + (float)_P.screenEffectCounter * 0.2f);
                spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), drawColor);
                if (_P.screenEffectCounter > 2)
                {
                    _P.screenEffect = ScreenEffect.Water;
                    _P.screenEffectCounter = 1;
                }
            }

            // Draw the crosshair.
            if (!_P.playerDead)
            {
                spriteBatch.Draw(texCrosshairs, new Rectangle(graphicsDevice.Viewport.Width / 2 - texCrosshairs.Width / 2,
                                                                graphicsDevice.Viewport.Height / 2 - texCrosshairs.Height / 2,
                                                                texCrosshairs.Width,
                                                                texCrosshairs.Height), Color.White);

                switch (_P.playerClass)
                {
                    case PlayerClass.Miner:
                        if (_P.Content[5] > 0)
                        {
                            BlockType held = (BlockType)(byte)(_P.Content[5]);

                            int screenWidth = graphicsDevice.Viewport.Width;
                            int screenHeight = graphicsDevice.Viewport.Height;

                            int drawX = screenWidth / 2 + 61 * 3;
                            int drawY = screenHeight - 91 * 3;

                            spriteBatch.Draw(blockIcons[held], new Rectangle(drawX + 37 * 3, drawY + 50 * 3, 117, 63), Color.White);

                        }
                        break;

                    case PlayerClass.Prospector:
                        if (_P.Content[5] > 0)
                        {
                            RenderMessageCenter(spriteBatch, String.Format("H", gameInstance.FrameRate), new Vector2(60, graphicsDevice.Viewport.Height - 60), Color.DarkGray, Color.Black);
                        }
                        else if (_P.Content[6] == 4)
                        {
                            RenderMessageCenter(spriteBatch, String.Format("H", gameInstance.FrameRate), new Vector2(60, graphicsDevice.Viewport.Height - 60), Color.GhostWhite, Color.Black);
                        }
                        else
                        {
                            RenderMessageCenter(spriteBatch, String.Format("H", gameInstance.FrameRate), new Vector2(60, graphicsDevice.Viewport.Height - 60), Color.DarkOrange, Color.Black);
                        }
                        break;
                }
                // If equipped, draw the tool.
                switch (_P.playerTools[_P.playerToolSelected])
                {
                    case PlayerTools.Detonator:
                        RenderDetonator(graphicsDevice, spriteBatch);
                        break;

                    case PlayerTools.Remote:
                        RenderRemote(graphicsDevice, spriteBatch);
                        break;

                    case PlayerTools.ProspectingRadar:
                        RenderProspectron(graphicsDevice, spriteBatch);
                        break;

                    case PlayerTools.ConstructionGun:
                        if (_P.playerBlockSelected >= _P.playerBlocks.Length && (_P.playerBlockSelected - _P.playerBlocks.Length) <= _P.playerItems.Length)
                        {
                            string equipment = "";
                            if ((ItemType)_P.playerItems[(_P.playerBlockSelected - _P.playerBlocks.Length)] == ItemType.DirtBomb)
                            {
                                equipment += "Place " + ItemInformation.GetName((ItemType)_P.playerItems[(_P.playerBlockSelected - _P.playerBlocks.Length)]) + " - Cost " + ItemInformation.GetCost((ItemType)_P.playerItems[(_P.playerBlockSelected - _P.playerBlocks.Length)]);
                            }
                            else
                            {
                                equipment += "Create " + ItemInformation.GetName((ItemType)_P.playerItems[(_P.playerBlockSelected - _P.playerBlocks.Length)]) + " - Cost " + ItemInformation.GetCost((ItemType)_P.playerItems[(_P.playerBlockSelected - _P.playerBlocks.Length)]);
                            }
                            RenderMessageCenter(spriteBatch, equipment, new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height - 20), Color.White, Color.Black);
                        }
                        else
                            RenderConstructionGun(graphicsDevice, spriteBatch, _P.playerBlocks[_P.playerBlockSelected]);
                        break;

                    case PlayerTools.DeconstructionGun:
                        RenderConstructionGun(graphicsDevice, spriteBatch, BlockType.None);
                        break;

                    default:
                        {
                            // Draw info about what we have equipped.
                            PlayerTools currentTool = _P.playerTools[_P.playerToolSelected];
                            BlockType currentBlock = BlockType.None;

                            if (_P.playerBlockSelected < _P.playerBlocks.Length)
                                currentBlock = _P.playerBlocks[_P.playerBlockSelected];
                            else
                                currentBlock = _P.playerBlocks[_P.playerBlocks.Length - 1];

                            string equipment = currentTool.ToString();
                            if (currentTool == PlayerTools.ConstructionGun)
                                equipment += " - " + currentBlock.ToString() + " (" + BlockInformation.GetCost(currentBlock) + ")";
                            RenderMessageCenter(spriteBatch, equipment, new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height - 20), Color.White, Color.Black);
                        }
                        break;
                }

                if (gameInstance.DrawFrameRate)
                    RenderMessageCenter(spriteBatch, String.Format("FPS: {0:000}", gameInstance.FrameRate), new Vector2(60, graphicsDevice.Viewport.Height - 20), Color.Gray, Color.Black);

                //RenderMessageCenter(spriteBatch, "QC: " + gameInstance.propertyBag.blockEngine.occCount + "/" + gameInstance.propertyBag.blockEngine.RegionOcc.Length, new Vector2(230, graphicsDevice.Viewport.Height - 20), Color.Gray, Color.Black);
                //RenderMessageCenter(spriteBatch, String.Format("R: {0}", gameInstance.propertyBag.blockEngine.GetRegion((int)_P.playerPosition.X, (int)_P.playerPosition.Y, (int)_P.playerPosition.Z)), new Vector2(360, graphicsDevice.Viewport.Height - 20), Color.Gray, Color.Black);

                //RenderMessageCenter(spriteBatch, String.Format("MS: {0:000}", gameInstance.propertyBag.netClient.ServerConnection.AverageRoundtripTime * 1000), new Vector2(100, graphicsDevice.Viewport.Height - 20), Color.Gray, Color.Black);
                //RenderMessageCenter(spriteBatch, String.Format("EXP: {0:000}", gameInstance.propertyBag.playerList[gameInstance.propertyBag.playerMyId].Score - gameInstance.propertyBag.playerList[gameInstance.propertyBag.playerMyId].Exp), new Vector2(140, graphicsDevice.Viewport.Height - 20), Color.Gray, Color.Black);

                if (!_P.playerDead && _P.Content[10] > 0)
                    RenderMessageCenter(spriteBatch, ArtifactInformation.GetName(_P.Content[10]), new Vector2(240 + (ArtifactInformation.GetName(_P.Content[10])).Length * 2, graphicsDevice.Viewport.Height - 20), Color.Lerp(Color.White, Color.Gold, _P.colorPulse), Color.Black);

                // Show the altimeter.
                int altitude = (int)(_P.playerPosition.Y - 64 + Defines.GROUND_LEVEL);
                RenderMessageCenter(spriteBatch, String.Format("ALTITUDE: {0:00}", altitude), new Vector2(graphicsDevice.Viewport.Width - 90, graphicsDevice.Viewport.Height - 20), altitude >= 0 ? Color.Gray : Defines.IM_RED, Color.Black);

                string interact = _P.strInteract();
                if (interact != "")//interact with stuffs
                {
                    RenderMessageCenter(spriteBatch, interact, new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2 + 60), Color.White, Color.Black);
                }
            }
            //if (_P.AtBankTerminal())
            //    RenderMessageCenter(spriteBatch, "8: DEPOSIT 50 ORE  9: WITHDRAW 50 ORE", new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2 + 60), Color.White, Color.Black);
            //if (_P.AtGenerator())
            //    RenderMessageCenter(spriteBatch, "8: Generator On  9: Generator Off", new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2 + 60), Color.White, Color.Black);
            //if (_P.AtPipe())
            //    RenderMessageCenter(spriteBatch, "8: Rotate Left 9: Rotate Right", new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2 + 60), Color.White, Color.Black);

            // Are they trying to change class when they cannot?
            //if (Keyboard.GetState().IsKeyDown(Keys.M) && _P.playerPosition.Y <= 64 - Defines.GROUND_LEVEL && _P.chatMode == ChatMessageType.None)
            //    RenderMessageCenter(spriteBatch, "YOU CANNOT CHANGE YOUR CLASS BELOW THE SURFACE", new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2 + 90), Color.White, Color.Black);

            // Draw the text-based information panel.
            int textStart = (graphicsDevice.Viewport.Width - 1024) / 2;
            spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, 20), Color.Black);
            if (!_P.playerDead)
            if(_P.oreWarning < DateTime.Now && _P.playerOre > 0)
                spriteBatch.DrawString(uiFont, "ORE: " + _P.playerOre + "/" + _P.playerOreMax, new Vector2(textStart + 3, -2), Color.White);
            else
                spriteBatch.DrawString(uiFont, "ORE: " + _P.playerOre + "/" + _P.playerOreMax, new Vector2(textStart + 3, -2), Color.Red);

            if (!_P.playerDead)
            spriteBatch.DrawString(uiFont, "LOOT: $" + _P.playerCash, new Vector2(textStart + 170, -2), Color.White);
            if (!_P.playerDead)
            RenderMessageCenter(spriteBatch, String.Format("Health: {0:000}", _P.playerHealth) + "/" + String.Format("{0:000}", _P.playerHealthMax), new Vector2(graphicsDevice.Viewport.Width - 300, graphicsDevice.Viewport.Height - 20), _P.playerHealth >= _P.playerHealthMax / 4 ? _P.playerHealth >= _P.playerHealthMax * 0.8f ? Color.Green : Color.Gray : Defines.IM_RED, Color.Black);
            //spriteBatch.DrawString(uiFont, "HEALTH: " + _P.playerHealth + "/" + _P.playerHealthMax, new Vector2(textStart + 170, 2), Color.White);
            if (!_P.playerDead)
            spriteBatch.DrawString(uiFont, "WEIGHT: " + _P.playerWeight + "/" + _P.playerWeightMax, new Vector2(textStart + 320, -2), Color.White);
            spriteBatch.DrawString(uiFont, "TEAM ORE: " + _P.teamOre, new Vector2(textStart + 515, -2), Color.White);
            spriteBatch.DrawString(uiFont, _P.redName + ": $" + _P.teamRedCash, new Vector2(textStart + 700, -2), _P.red);// Defines.IM_RED);
            spriteBatch.DrawString(uiFont, _P.blueName + ": $" + _P.teamBlueCash, new Vector2(textStart + 860, -2), _P.blue);// Defines.IM_BLUE);
            spriteBatch.DrawString(uiFont, _P.teamArtifactsRed + "/" +_P.winningCashAmount, new Vector2(textStart + 700, 20), _P.red);
            spriteBatch.DrawString(uiFont, _P.teamArtifactsBlue + "/" + _P.winningCashAmount, new Vector2(textStart + 860, 20), _P.blue);
            spriteBatch.DrawString(uiFont, "Artifacts", new Vector2(textStart + 700, 40), _P.red);
            spriteBatch.DrawString(uiFont, "Artifacts", new Vector2(textStart + 860, 40), _P.blue);

            // Draw player information.
            if ((Keyboard.GetState().IsKeyDown(Keys.Tab)) || _P.teamWinners != PlayerTeam.None)
            {
                spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), new Color(Color.Black, 0.7f));

                //Server name
                RenderMessageCenter(spriteBatch, _P.serverName, new Vector2(graphicsDevice.Viewport.Width / 2, 32), _P.playerTeam == PlayerTeam.Blue ? _P.blue : _P.red, Color.Black);//Defines.IM_BLUE : Defines.IM_RED, Color.Black);
                
                if (_P.teamWinners != PlayerTeam.None)
                {
                    string teamName = _P.teamWinners == PlayerTeam.Red ? "RED" : "BLUE";
                    Color teamColor = _P.teamWinners == PlayerTeam.Red ? _P.red : _P.blue;//Defines.IM_RED : Defines.IM_BLUE;
                    string gameOverMessage = "GAME OVER - " + teamName.ToUpper() + " TEAM WINS!";
                    RenderMessageCenter(spriteBatch, gameOverMessage, new Vector2(graphicsDevice.Viewport.Width / 2, 150), teamColor, new Color(0, 0, 0, 0));
                }

                int drawY = 200;
                foreach (Player p in _P.playerList.Values)
                {
                    if (p.Team != PlayerTeam.Red)
                        continue;
                    RenderMessageCenter(spriteBatch, p.Handle + " ( $" + p.Score + " )", new Vector2(graphicsDevice.Viewport.Width / 4, drawY), _P.red, new Color(0, 0, 0, 0));//Defines.IM_RED
                    drawY += 35;
                }
                drawY = 200;
                foreach (Player p in _P.playerList.Values)
                {
                    if (p.Team != PlayerTeam.Blue)
                        continue;
                    RenderMessageCenter(spriteBatch, p.Handle + " ( $" + p.Score + " )", new Vector2(graphicsDevice.Viewport.Width * 3 / 4, drawY), _P.blue, new Color(0, 0, 0, 0)); //Defines.IM_BLUE
                    drawY += 35;
                }
            }

            // Draw the chat buffer.
            if (_P.chatMode == ChatMessageType.SayAll)
            {
                spriteBatch.DrawString(uiFont, "ALL> " + _P.chatEntryBuffer, new Vector2(22, graphicsDevice.Viewport.Height - 88), Color.Black);
                spriteBatch.DrawString(uiFont, "ALL> " + _P.chatEntryBuffer, new Vector2(20, graphicsDevice.Viewport.Height - 90), Color.White);
            }
            else if (_P.chatMode == ChatMessageType.SayBlueTeam || _P.chatMode == ChatMessageType.SayRedTeam)
            {
                spriteBatch.DrawString(uiFont, "TEAM> " + _P.chatEntryBuffer, new Vector2(22, graphicsDevice.Viewport.Height - 88), Color.Black);
                spriteBatch.DrawString(uiFont, "TEAM> " + _P.chatEntryBuffer, new Vector2(20, graphicsDevice.Viewport.Height - 90), Color.White);
            }
            if (_P.chatMode != ChatMessageType.None)
            {
                drawChat(_P.chatFullBuffer,graphicsDevice);
                /*for (int i = 0; i < _P.chatFullBuffer.Count; i++)
                {
                    Color chatColor = Color.White;
                    chatColor = _P.chatFullBuffer[i].type == ChatMessageType.SayAll ? Color.White : _P.chatFullBuffer[i].type == ChatMessageType.SayRedTeam ? InfiniminerGame.IM_RED : InfiniminerGame.IM_BLUE;
                    
                    spriteBatch.DrawString(uiFont, _P.chatFullBuffer[i].message, new Vector2(22, graphicsDevice.Viewport.Height - 114 - 16 * i), Color.Black);
                    spriteBatch.DrawString(uiFont, _P.chatFullBuffer[i].message, new Vector2(20, graphicsDevice.Viewport.Height - 116 - 16 * i), chatColor);
                }*/
            }
            else
            {
                drawChat(_P.chatBuffer,graphicsDevice);
            }

            // Draw the player radar.
            if (!_P.playerDead)
            {
                spriteBatch.Draw(texRadarBackground, new Vector2(10, 30), Color.White);
                foreach (Player p in _P.playerList.Values)
                {
                    if (p.Team == _P.playerTeam && p.Alive || p.Content[1] == 1)//within radar
                        RenderRadarBlip(spriteBatch, p.ID == _P.playerMyId ? _P.playerPosition : p.Position, p.Team == PlayerTeam.Red ? _P.red : _P.blue, p.Ping > 0, ""); //Defines.IM_RED : Defines.IM_BLUE, p.Ping > 0, "");
                }

                foreach (KeyValuePair<Vector3, Beacon> bPair in _P.beaconList)
                    if (bPair.Value.Team == _P.playerTeam)
                        RenderRadarBlip(spriteBatch, bPair.Key, Color.White, false, bPair.Value.ID);

                RenderRadarBlip(spriteBatch, new Vector3(100000, 0, 32), Color.White, false, "NORTH");

                // foreach (KeyValuePair<string, Item> bPair in _P.itemList)//  if (bPair.Value.Team == _P.playerTeam)//doesnt care which team
                //        RenderRadarBlip(spriteBatch, bPair.Value.Position, Color.Magenta, false, bPair.Value.ID);

                spriteBatch.Draw(texRadarForeground, new Vector2(10, 30), Color.White);
            }
            // Draw escape message.
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                RenderMessageCenter(spriteBatch, "Press Y to confirm that you want to quit.", new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2 + 30), Color.White, Color.Black);
                RenderMessageCenter(spriteBatch, "Press K to suicide.", new Vector2(graphicsDevice.Viewport.Width / 2, graphicsDevice.Viewport.Height / 2 + 80), Color.White, Color.Black);
            }

            // Draw the current screen effect.

            // Draw the help screen.
            if (Keyboard.GetState().IsKeyDown(Keys.F1))
            {
                spriteBatch.Draw(texBlank, new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height), Color.Black);
                spriteBatch.Draw(texHelp, drawRect, Color.White);
            }

            spriteBatch.End();
        }
    }
}
