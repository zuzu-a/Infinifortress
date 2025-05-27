using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using StateMasher;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Lidgren.Network;

namespace Infiniminer.States
{
    public class MainGameState : State
    {
        const float MOVESPEED = 3.5f;
        const float GRAVITY = -8.0f;
        const float JUMPVELOCITY = 4.0f;
        const float CLIMBVELOCITY = 2.5f;
        const float DIEVELOCITY = 15.0f;

        string nextState = null;
        bool mouseInitialized = false;

        public override void OnEnter(string oldState)
        {
            _SM.IsMouseVisible = false;
        }

        public override void OnLeave(string newState)
        {
            _P.chatEntryBuffer = "";
            _P.chatMode = ChatMessageType.None;
        }

        public override string OnUpdate(GameTime gameTime, KeyboardState keyState, MouseState mouseState)
        {
            // Update network stuff.
            (_SM as InfiniminerGame).UpdateNetwork(gameTime);

            // Update the current screen effect.
            _P.screenEffectCounter += gameTime.ElapsedGameTime.TotalSeconds;

            // Update engines.
            _P.skyplaneEngine.Update(gameTime);
            _P.playerEngine.Update(gameTime);
            _P.interfaceEngine.Update(gameTime);
            _P.particleEngine.Update(gameTime);

            // Count down the tool cooldown.
            if (_P.playerToolCooldown > 0)
            {
                _P.playerToolCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_P.playerToolCooldown <= 0)
                    _P.playerToolCooldown = 0;
            }

            // Moving the mouse changes where we look.
            if (_SM.WindowHasFocus())
            {
                if (mouseInitialized)
                {
                    int dx = mouseState.X - _SM.GraphicsDevice.Viewport.Width / 2;
                    int dy = mouseState.Y - _SM.GraphicsDevice.Viewport.Height / 2;

                    if ((_SM as InfiniminerGame).InvertMouseYAxis)
                        dy = -dy;

                    _P.playerCamera.Yaw -= dx * _P.mouseSensitivity;
                    _P.playerCamera.Pitch = (float)Math.Min(Math.PI * 0.49, Math.Max(-Math.PI * 0.49, _P.playerCamera.Pitch - dy * _P.mouseSensitivity));
                }
                else
                {
                    mouseInitialized = true;
                }
                Mouse.SetPosition(_SM.GraphicsDevice.Viewport.Width / 2, _SM.GraphicsDevice.Viewport.Height / 2);
            }
            else
                mouseInitialized = false;

            // Digging like a freaking terrier! Now for everyone!
            if (mouseInitialized && mouseState.LeftButton == ButtonState.Pressed && !_P.playerDead && _P.playerToolCooldown == 0 && _P.playerTools[_P.playerToolSelected] == PlayerTools.Pickaxe)
            {
                _P.FirePickaxe();
               //_P.playerToolCooldown = _P.GetToolCooldown(PlayerTools.Pickaxe);//(_P.playerClass == PlayerClass.Miner ? 0.4f : 1.0f);
            }
            //if (mouseInitialized && mouseState.LeftButton == ButtonState.Pressed && !_P.playerDead && _P.playerToolCooldown == 0 && _P.playerTools[_P.playerToolSelected] == PlayerTools.ThrowBomb)
            //{
            //    _P.FireBomb();
            //}
            //else if (mouseInitialized && mouseState.LeftButton == ButtonState.Pressed && !_P.playerDead && _P.playerToolCooldown == 0 && _P.playerTools[_P.playerToolSelected] == PlayerTools.ThrowRope)
            //{
            //    _P.Hide();
            //}
            //else if (mouseInitialized && mouseState.LeftButton == ButtonState.Pressed && !_P.playerDead && _P.playerToolCooldown == 0 && _P.playerTools[_P.playerToolSelected] == PlayerTools.ThrowRope)
            //{
            //    _P.Hide();
            //}
            // Prospector radar stuff.
            if (!_P.playerDead && _P.playerToolCooldown == 0 && _P.playerTools[_P.playerToolSelected] == PlayerTools.ProspectingRadar)
            {
                float oldValue = _P.radarValue;
                _P.ReadRadar(ref _P.radarDistance, ref _P.radarValue);
                if (_P.radarValue != oldValue)
                {
                    if (_P.radarValue == 200)
                        _P.PlaySound(InfiniminerSound.RadarLow);
                    if (_P.radarValue == 1000)
                        _P.PlaySound(InfiniminerSound.RadarHigh);
                    if (_P.radarValue == 2000)
                        _P.PlaySound(InfiniminerSound.RadarHigh);
                }
            }

            // Update the player's position.
            if (!_P.playerDead)
                UpdatePlayerPosition(gameTime, keyState);

            // Update the camera regardless of if we're alive or not.
            _P.UpdateCamera(gameTime);

            return nextState;
        }

        public void Lightsource(ref Vector3 src, int intensity)
        {
            for (int a = -intensity; a <= intensity; a++)
                for (int b = -intensity; b <= intensity; b++)
                    for (int c = -intensity; c <= intensity; c++)
                        {
                            int nx = a + (int)src.X;
                            int ny = b + (int)src.Y;
                            int nz = c + (int)src.Z;

                            if (nx < 63 && ny < 63 && nz < 63 && nx > 0 && ny > 0 && nz > 0)
                            {
                                

                                //if (_P.blockEngine.RayCollision(new Vector3(nx + 0.5f, ny + 0.5f, nz + 0.5f), src, 10))
                                //{
                                    float distray = (new Vector3(nx, ny, nz) - src).Length();
                                    float lightdist = distray;
                                    //_P.blockEngine.Light[nx, ny, nz, 0] = 1.0f - lightdist * 0.1f;
                                    //_P.blockEngine.Light[nx, ny, nz, 1] = 1.0f - lightdist * 0.1f;
                                    //_P.blockEngine.Light[nx, ny, nz, 2] = 1.0f - lightdist * 0.1f;
                                    //_P.blockEngine.Light[nx, ny, nz, 3] = 1.0f - lightdist * 0.1f;
                                    //_P.blockEngine.Light[nx, ny, nz, 4] = 1.0f - lightdist * 0.1f;
                                    //_P.blockEngine.Light[nx, ny, nz, 5] = 1.0f - lightdist * 0.1f;

                                    uint region = _P.blockEngine.GetRegion((ushort)nx, (ushort)ny, (ushort)nz);

                                    for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                                    {
                                        _P.blockEngine.vertexListDirty[d, region] = true;
                                    }
                                //}
                                //else
                                //{
                                //    //_P.blockEngine.Light[nx, ny, nz, 0] = 0.1f;
                                //    //_P.blockEngine.Light[nx, ny, nz, 1] = 0.1f;
                                //    //_P.blockEngine.Light[nx, ny, nz, 2] = 0.1f;
                                //    //_P.blockEngine.Light[nx, ny, nz, 3] = 0.1f;
                                //    //_P.blockEngine.Light[nx, ny, nz, 4] = 0.1f;
                                //    //_P.blockEngine.Light[nx, ny, nz, 5] = 0.1f;
                                //    uint region = _P.blockEngine.GetRegion((ushort)nx, (ushort)ny, (ushort)nz);

                                //    for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                                //    {
                                //        _P.blockEngine.vertexListDirty[d, region] = true;
                                //    }
                                //}
                            }
                        }
        }

        private void UpdatePlayerPosition(GameTime gameTime, KeyboardState keyState)
        {
            //if ((int)_P.lastPosition.X != (int)_P.playerPosition.X || (int)_P.lastPosition.Y != (int)_P.playerPosition.Y || (int)_P.lastPosition.Z != (int)_P.playerPosition.Z)
            //{//update lights
            //    Vector3 light = _P.playerPosition + Vector3.UnitY * 0.5f;
            //    Lightsource(ref light, 10);
            //}
            //temperature data

                _P.temperature = 0;
                for (int a = -5; a < 6; a++)
                    for (int b = -5; b < 6; b++)
                        for (int c = -5; c < 6; c++)
                        {
                            int nx = a + (int)_P.playerPosition.X;
                            int ny = b + (int)_P.playerPosition.Y;
                            int nz = c + (int)_P.playerPosition.Z;
                            if (nx < 63 && ny < 63 && nz < 63 && nx > 0 && ny > 0 && nz > 0)
                            {
                                BlockType block = _P.blockEngine.blockList[nx, ny, nz];
                                if (block == BlockType.Lava || block == BlockType.MagmaBurst || block == BlockType.MagmaVent)
                                {
                                    _P.temperature += 5 - Math.Abs(a) + 5 - Math.Abs(b) + 5 - Math.Abs(c);
                                }
                            }
                        }

               
            // Double-speed move flag, set if we're on road.
            _P.moveVector = Vector3.Zero;
            bool movingOnRoad = false;
            bool movingOnMud = false;
            bool sprinting = false;
            bool crouching = false;
            bool swimming = false;

            Vector3 footPosition = _P.playerPosition + new Vector3(0f, -1.5f, 0f);
            Vector3 headPosition = _P.playerPosition + new Vector3(0f, 0.1f, 0f);
            Vector3 midPosition = _P.playerPosition + new Vector3(0f, -0.7f, 0f);

           // if (!_P.blockEngine.SolidAtPointForPlayer(midBodyPoint))
           // {
                if(_P.blockEngine.BlockAtPoint(footPosition) == BlockType.Water || _P.blockEngine.BlockAtPoint(headPosition) == BlockType.Water || _P.blockEngine.BlockAtPoint(midPosition) == BlockType.Water) 
                {
                    swimming = true;
                    if (_P.blockEngine.BlockAtPoint(midPosition) == BlockType.Water)
                    {
                        if (_P.playerHoldBreath == 20)
                        {
                            _P.playerVelocity.Y *= 0.2f;
                        }
                        //if (_P.playerHoldBreath > 9)
                        //{
                            _P.screenEffect = ScreenEffect.Water;
                            _P.screenEffectCounter = 0.5;
                       // }
                       // }
                        
                        _P.playerHoldBreath -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                    }
                    else
                    {
                        _P.playerHoldBreath = 20;
                    }
                }
                else
	            {
                    swimming = false;
                    _P.playerHoldBreath = 20;
	            }
         //   }

            // 
            if (!swimming && _P.Content[10] == 18)//wings
            {
                _P.playerVelocity.Y += (GRAVITY / 4) * (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            if (swimming)
            {
                TimeSpan timeSpan = DateTime.Now - _P.lastBreath;
                _P.playerVelocity.Y += (GRAVITY/8) * (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (timeSpan.TotalMilliseconds > 1000)
                {
                    //_P.addChatMessage("Breath held.." + _P.playerHoldBreath, ChatMessageType.SayAll, 10);
                    if (_P.playerHoldBreath <= 10 && _P.Content[10] != 4 && _P.artifactActive[(byte)_P.playerTeam, 4] < 1)
                    {//water breathing is artifact 4
                        _P.screenEffect = ScreenEffect.Drown;
                        _P.screenEffectCounter = 1.0;
                        if (((int)_P.playerHealth - ((9 - _P.playerHoldBreath) * 10)) > 0)
                        {
                            _P.playerHealth -= (uint)(9 - _P.playerHoldBreath) * (_P.playerHealthMax / 10);
                            _P.SendPlayerHurt();
                            _P.lastBreath = DateTime.Now;
                        }
                        else
                        {
                            _P.playerHealth = 0;
                        }
                        _P.PlaySoundForEveryone(InfiniminerSound.Death, _P.playerPosition);
                    }
                }

                    if (_P.playerHealth <= 0)
                    {
                        _P.KillPlayer(DeathMessage.deathByDrown);
                    }
            }
            else
            {
                //float size = 0.1f;//box collision for falling prevents inside walls problems
                bool allow = true;
                //for (int x = -1; x < 2; x++)
                //        for (int z = -1; z < 2; z++)
                //        {
                //            Vector3 box = new Vector3(size * x, 0, size * z);
                //            if (_P.blockEngine.SolidAtPointForPlayer(footPosition + (_P.playerVelocity * (float)gameTime.ElapsedGameTime.TotalSeconds) + box))
                //            {
                //                allow = false;
                //            }
                //        }

                if (allow == true)
                {
                    //if (_P.Content[5] > 0 && _P.playerClass == PlayerClass.Sapper)
                    //{//half gravity during smash
                    //    if (_P.Content[5] > 250)
                    //    {
                    //        //_P.playerVelocity.Y += (GRAVITY * 0.1f) * (float)gameTime.ElapsedGameTime.TotalSeconds;
                    //    }
                    //    else//leaving smash but we're still in charge
                    //    {
                    //        _P.playerVelocity.Y += GRAVITY * (float)gameTime.ElapsedGameTime.TotalSeconds;
                    //    }

                    //}
                    //else
                    //{
                    if (_P.artifactActive[(byte)_P.playerTeam, 18] == 0)//wings
                    {
                        _P.playerVelocity.Y += GRAVITY * (float)gameTime.ElapsedGameTime.TotalSeconds;
                    }
                    else
                    {
                        _P.playerVelocity.Y += (GRAVITY + _P.artifactActive[(byte)_P.playerTeam, 18]) * (float)gameTime.ElapsedGameTime.TotalSeconds;
                    }
                    //}
                }
            }

            if (_P.retrigger < DateTime.Now)
            {
                if (_P.playerTeam == PlayerTeam.Red)
                {
                    if (_P.blockEngine.BlockAtPoint(footPosition) == BlockType.TrapB)
                    {
                        _P.retrigger = DateTime.Now + TimeSpan.FromSeconds(0.1);
                        _P.SendPlayerInteract(1, (uint)(footPosition.X), (uint)(footPosition.Y), (uint)(footPosition.Z));
                    }
                }
                else if (_P.playerTeam == PlayerTeam.Blue)
                {
                    if (_P.blockEngine.BlockAtPoint(footPosition) == BlockType.TrapR)
                    {
                        _P.retrigger = DateTime.Now + TimeSpan.FromSeconds(0.1);
                        _P.SendPlayerInteract(1, (uint)(footPosition.X), (uint)(footPosition.Y), (uint)(footPosition.Z));
                    }
                }
            }

            BlockType hittingHeadOnBlock = _P.blockEngine.BlockAtPoint(headPosition + _P.playerVelocity * (float)gameTime.ElapsedGameTime.TotalSeconds);//(headPosition + new Vector3(0f,0.25f,0f)) + _P.playerVelocity * (float)gameTime.ElapsedGameTime.TotalSeconds);

            if (_P.blockEngine.SolidAtPointForPlayer(headPosition + _P.playerVelocity * (float)gameTime.ElapsedGameTime.TotalSeconds))
            {
                _P.playerVelocity.Y = 0;
            }
            BlockType standingOnBlock = BlockType.None;
            float size = 0.1f;
            bool allowb = false;
            for (int x = -1; x < 2; x++)
                    for (int z = -1; z < 2; z++)
                    {
                        Vector3 box = new Vector3(size * x, 0, size * z);
                        if (_P.blockEngine.SolidAtPointForPlayer(footPosition + box) && !_P.blockEngine.SolidAtPointForPlayer(footPosition + new Vector3(0,0.1f,0) + box))
                        {//foot boundary
                            standingOnBlock = _P.blockEngine.BlockAtPoint(footPosition + box);
                            allowb = true;
                            x = 2;
                            z = 2;
                            break;
                        }
                    }

            if (allowb || _P.blockEngine.SolidAtPointForPlayer(footPosition) || _P.blockEngine.SolidAtPointForPlayer(headPosition + _P.playerVelocity * (float)gameTime.ElapsedGameTime.TotalSeconds))
            {
                if(standingOnBlock == BlockType.None || standingOnBlock == BlockType.Water)
                    standingOnBlock = _P.blockEngine.BlockAtPoint(footPosition);
                
                // If we"re hitting the ground with a high velocity, die!
                //if (_P.Content[10] != 18)//wings
                if (standingOnBlock != BlockType.None && _P.playerVelocity.Y < 0)
                {
                    float fallDamage = Math.Abs(_P.playerVelocity.Y) / DIEVELOCITY;
                    if (fallDamage > 0.5)
                    {
                    //    _P.PlaySoundForEveryone(InfiniminerSound.GroundHit, _P.playerPosition);
                    //    _P.KillPlayer(Defines.deathByFall);//"WAS KILLED BY GRAVITY!");
                    //    return;
                    //}
                    //else if (fallDamage > 0.5)
                    //{
                        // Fall damage of 0.5 maps to a screenEffectCounter value of 2, meaning that the effect doesn't appear.
                        // Fall damage of 1.0 maps to a screenEffectCounter value of 0, making the effect very strong.
                        if (standingOnBlock != BlockType.Jump)
                        {
                            _P.screenEffect = ScreenEffect.Fall;
                            uint fallDmg = 0;

                            if (_P.Content[10] == 9)
                                fallDmg = (uint)((fallDamage * 100) / 3);
                            else
                                fallDmg = (uint)((fallDamage * 100) / 2);

                            if ((int)_P.playerHealth - (fallDmg) > 0) 
                            {
                                _P.playerHealth -= fallDmg;
                                _P.SendPlayerHurt();
                                _P.PlaySound(InfiniminerSound.GroundHit, _P.playerPosition);
                            } 
                            else 
                            {
                                if (_P.playerDead != true)
                                {
                                    //_P.playerHealth = 0;
                                    _P.particleEngine.CreateBloodSplatter(_P.playerPosition, _P.playerTeam == PlayerTeam.Red ? Color.Red : Color.Blue, 2.0f);
                                    //_P.PlaySound(InfiniminerSound.Death, _P.playerPosition);
                                    _P.screenEffect = ScreenEffect.Death;
                                    _P.screenEffectCounter = 2;
                                    _P.KillPlayer(DeathMessage.deathByFall);//client thinks it died so we're dyin!
                                }
                               // _P.playerHealth = 0;
                            }
                            _P.screenEffectCounter = 2 - (fallDamage - 0.5) * 4;
                            
                        }
                    }
                }

                //if (_P.blockEngine.SolidAtPointForPlayer(headPosition))
                //{
                //    int blockIn = (int)(headPosition.Y);
    
                //    _P.playerPosition.Y = (float)(blockIn - 0.15f);

                //    //if (_P.Content[5] > 0 && _P.playerClass == PlayerClass.Sapper)
                //    //{
                //    //    if (_P.Content[5] < 250)//leave smash
                //    //    {
                //    //        _P.Content[5] = 0;
                //    //    }
                //    //}
                //}
                if (_P.blockEngine.SolidAtPointForPlayer(midPosition) || _P.blockEngine.SolidAtPointForPlayer(headPosition + Vector3.UnitY * 0.1f) )
                {
                    //if(allow == false)
                    _P.KillPlayer(DeathMessage.deathByCrush);//may not be reliable enough to kill players that get hit by sand
                }
                // If the player has their head stuck in a block, push them down.
                
                // If the player is stuck in the ground, bring them out.
                // This happens because we're standing on a block at -1.5, but stuck in it at -1.4, so -1.45 is the sweet spot.
                if (_P.blockEngine.SolidAtPointForPlayer(footPosition))
                {
                    int blockOn = (int)(footPosition.Y);
                    _P.playerPosition.Y = (float)(blockOn + 1 + 1.45);

                    //if (_P.Content[5] > 0 && _P.playerClass == PlayerClass.Sapper)
                    //{
                    //    if (_P.Content[5] < 250)//leave smash
                    //    {
                    //        _P.Content[5] = 0;
                    //    }
                    //}

                }

                _P.playerVelocity.Y = 0;

                // Logic for standing on a block.
                switch (standingOnBlock)
                {
                    case BlockType.Jump:
                        _P.playerVelocity.Y = 2.5f * JUMPVELOCITY;
                        _P.PlaySoundForEveryone(InfiniminerSound.Jumpblock, _P.playerPosition);
                        break;

                    case BlockType.Road:
                        movingOnRoad = true;
                        break;

                    case BlockType.Mud:
                        movingOnMud = true;
                        break;

                    case BlockType.Lava:
                        if (_P.artifactActive[(byte)_P.playerTeam, 16] == 0)
                        {
                            _P.KillPlayer(DeathMessage.deathByLava);
                            return;
                        }
                        break;

                    case BlockType.Plate:
                        if (_P.retrigger < DateTime.Now)
                        {
                            _P.retrigger = DateTime.Now + TimeSpan.FromSeconds(0.2);
                            _P.SendPlayerInteract(1, (uint)(footPosition.X), (uint)(footPosition.Y), (uint)(footPosition.Z));
                        }
                        break;
                }
            }

            switch (hittingHeadOnBlock)
            {
                case BlockType.Shock:
                    _P.KillPlayer(DeathMessage.deathByElec);
                    return;

                case BlockType.Lava:
                    if (_P.artifactActive[(byte)_P.playerTeam, 16] == 0)
                    _P.KillPlayer(DeathMessage.deathByLava);
                    return;

                case BlockType.Plate:
                    _P.SendPlayerInteract(1, (uint)(headPosition.X), (uint)(headPosition.Y + 0.2f), (uint)(headPosition.Z));
                    break;
            }

            if (!_P.blockEngine.SolidAtPointForPlayer(midPosition + _P.playerVelocity * (float)gameTime.ElapsedGameTime.TotalSeconds))
            {
               _P.playerPosition += _P.playerVelocity * (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
            // Death by falling off the map.
            if (_P.playerPosition.Y < -30)
            {
                _P.KillPlayer(DeathMessage.deathByMiss);
                return;
            }

            // Pressing forward moves us in the direction we"re looking.
            //Vector3 moveVector = Vector3.Zero;
            //if (_P.Content[5] > 250)
            //{
            //    Vector3 smashVector = new Vector3((float)(_P.Content[6]) / 1000, (float)(_P.Content[7]) / 1000, (float)(_P.Content[8]) / 1000);
            //    _P.playerVelocity = smashVector*3;
            //    _P.Content[5] = (int)((float)(_P.Content[5] / 100) - (float)gameTime.ElapsedGameTime.TotalSeconds) * 100;
            //}

            if (_P.chatMode == ChatMessageType.None)
            {
                //if (_P.Content[5] > 0 && _P.playerClass == PlayerClass.Sapper)
                //{//smash timer
                //    Vector3 smashVector = new Vector3((float)(_P.Content[6]) / 1000, (float)(_P.Content[7]) / 1000, (float)(_P.Content[8]) / 1000);
                //    _P.moveVector += smashVector;
                //    sprinting = true;
                //    crouching = false;
                //    if (_P.Content[5] > 250)
                //    {
                //        _P.moveVector += smashVector;
                //        _P.Content[5] = (int)((float)(_P.Content[5] / 100) - (float)gameTime.ElapsedGameTime.TotalSeconds) * 100;
                //    }
                //    else if (_P.Content[5] < 0)//leaving smash
                //    {

                //    }
                //    //_P.SmashDig();
                //}
                //else
                //{

                //BlockType lowerBlock = _P.blockEngine.BlockAtPoint(_P.playerPosition + new Vector3(0, -1.7f, 0));

                
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.Forward))//keyState.IsKeyDown(Keys.W))
                        _P.moveVector += _P.playerCamera.GetLookVector();
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.Backward))//keyState.IsKeyDown(Keys.S))
                        _P.moveVector -= _P.playerCamera.GetLookVector();
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.Right))//keyState.IsKeyDown(Keys.D))
                        _P.moveVector += _P.playerCamera.GetRightVector();
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.Left))//keyState.IsKeyDown(Keys.A))
                        _P.moveVector -= _P.playerCamera.GetRightVector();
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.DropOre))
                        if (_P.playerOre > 19)
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(1);
                            _P.playerOre = 0;
                            _P.DropItem(1);
                        }
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.DropGold))
                        if (_P.playerCash > 9)
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(1);
                            _P.playerCash = 0;
                            _P.DropItem(2);
                        }
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.DropArtifact))
                        if (_P.Content[10] > 0)
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(1);
                            _P.Content[10] = 0;
                            _P.DropItem(3);
                        }
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.DropDiamond))
                        if (_P.Content[11] > 0)
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(1);
                            _P.Content[11] = 0;
                            _P.DropItem(4);
                        }
                    //Sprinting
                    if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.Sprint))//keyState.IsKeyDown(Keys.LeftShift) || keyState.IsKeyDown(Keys.RightShift))
                        sprinting = true;
                    //Crouching
                    //if ((_SM as InfiniminerGame).keyBinds.IsPressed(Buttons.Crouch))
                    //{
                       
                    //    crouching = true;
                    //}
                //}
            }
            
            //grab item
            if(_P.blockPickup < DateTime.Now)
            foreach (KeyValuePair<uint, Item> bPair in _P.itemList)
            {
                TimeSpan diff = DateTime.Now - bPair.Value.Frozen;
                if (diff.Milliseconds > 0)
                {

                    float dx = bPair.Value.Position.X - _P.playerPosition.X;
                    float dy = bPair.Value.Position.Y - _P.playerPosition.Y + 1.0f;
                    float dz = bPair.Value.Position.Z - _P.playerPosition.Z;

                    float distance = (float)(Math.Sqrt(dx * dx + dy * dy + dz * dz));

                    if (distance < 1.2 && _P.blockPickup < DateTime.Now)
                    {
                        bPair.Value.Frozen = DateTime.Now + TimeSpan.FromMilliseconds(500);//no interaction for half a second after trying once

                        if (bPair.Value.Type == ItemType.Ore && _P.playerOre < _P.playerOreMax)//stops requesting items it doesnt need
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(0.3);
                            _P.GetItem(bPair.Value.ID);
                        }
                        else if (bPair.Value.Type == ItemType.Gold && _P.playerWeight < _P.playerWeightMax)
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(0.3);
                            _P.GetItem(bPair.Value.ID);
                        }
                        else if (bPair.Value.Type == ItemType.Diamond && _P.playerWeight < _P.playerWeightMax)
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(0.3);
                            _P.GetItem(bPair.Value.ID);
                        }
                        else if (bPair.Value.Type == ItemType.Artifact && _P.Content[10] == 0 && bPair.Value.Content[6] == 0)//[10] artifact slot, [6] locked item
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(0.3);
                            _P.GetItem(bPair.Value.ID);
                        }
                        else if (bPair.Value.Type == ItemType.Bomb)//[10] artifact slot, [6] locked item
                        {
                            _P.blockPickup = DateTime.Now + TimeSpan.FromSeconds(0.3);
                            bPair.Value.Frozen = DateTime.Now + TimeSpan.FromMilliseconds((int)(distance * 100));//retry based on objects distance
                            //_P.GetItem(bPair.Value.ID);
                        }
                        else
                        {
                            bPair.Value.Frozen = DateTime.Now + TimeSpan.FromMilliseconds((int)(distance * 100));
                            //we dont know what this item is
                        }
                        //break;
                    }
                    else
                    {
                        bPair.Value.Frozen = DateTime.Now + TimeSpan.FromMilliseconds((int)(distance * 100));//retry based on objects distance
                    }
                }
            }

            if (_P.moveVector.X != 0 || _P.moveVector.Z != 0)
            {
                // "Flatten" the movement vector so that we don"t move up/down.
                //if (_P.Content[5] > 0 && _P.playerClass == PlayerClass.Sapper)
                //{
                //    //smash allows upward
                //}
                //else
                //{
                    _P.moveVector.Y = 0;
                //}
                
                _P.moveVector.Normalize();
                    
                _P.moveVector *= MOVESPEED * (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (movingOnRoad)
                    _P.moveVector *= 2;

                if (movingOnMud)//12 = bog
                {
                    if (_P.artifactActive[(byte)_P.playerTeam, 12] > 0 || _P.Content[10] == 12)
                    {

                    }
                    else
                        _P.moveVector *= 0.5f;
                }
                if (swimming)
                    _P.moveVector *= 0.5f;
                // Sprinting doubles speed, even if already on road
                if (sprinting)
                    _P.moveVector *= 1.5f;
                if (crouching)
                    _P.moveVector.Y = -1;

                // Attempt to move, doing collision stuff.
                if (TryToMoveTo(_P.moveVector, gameTime)) { }
                else
                {
                    if (!TryToMoveTo(new Vector3(0, 0, _P.moveVector.Z), gameTime)) { }
                    if (!TryToMoveTo(new Vector3(_P.moveVector.X, 0, 0), gameTime)) { }
                }
            }

            if (_P.forceStrength > 0.0f)
            {
                if (TryToMoveTo((_P.forceVector * _P.forceStrength) * (float)gameTime.ElapsedGameTime.TotalSeconds, gameTime)) { }
                else
                {
                    if (!TryToMoveTo(new Vector3(0, 0, (_P.forceVector.Z * _P.forceStrength) * (float)gameTime.ElapsedGameTime.TotalSeconds), gameTime)) { }
                    if (!TryToMoveTo(new Vector3((_P.forceVector.X * _P.forceStrength) * (float)gameTime.ElapsedGameTime.TotalSeconds, 0, 0), gameTime)) { }
                }

                _P.forceStrength -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
            else
            {
                _P.forceVector = Vector3.Zero;
            }
        }

        private bool TryToMoveTo(Vector3 moveVector, GameTime gameTime)
        {
             float moveLength = moveVector.Length();
            Vector3 testVector = moveVector;
            testVector.Normalize();
            testVector = testVector * (moveLength);// + 0.1f);

            // Apply this test vector.
            Vector3 movePosition = _P.playerPosition + testVector;
            Vector3 midBodyPoint = movePosition + new Vector3(0, -0.7f, 0);
            Vector3 lowerBodyPoint = movePosition + new Vector3(0, -1.4f, 0);
            Vector3 lowerBodyPointB = movePosition + new Vector3(0, -1.2f, 0);

            if ((float)gameTime.ElapsedGameTime.TotalSeconds < 0.033f)
            {
                lowerBodyPointB = lowerBodyPoint;//low fps hack (reversed)
            }
            
            BlockType midBlock = BlockType.None;
            BlockType upperBlock = BlockType.None;

            Vector3 touch = Vector3.Zero;

            float size = 0.1f;
            bool allow = true;
            float correction = 0.0f;
            if ((float)gameTime.ElapsedGameTime.TotalSeconds > 0.032f)
            {
                correction = 1.0f;//below 30 fps there is insufficent sampling, so we move the feet up!
            }
            else
            {
                correction = 1.4f;
            }
            bool ladder = false;

            if (_P.blockEngine.BlockAtPoint(movePosition + new Vector3(-0.3f, 0f, 0f)) == BlockType.Ladder || _P.blockEngine.BlockAtPoint(movePosition + new Vector3(0.3f, 0f, 0f)) == BlockType.Ladder || _P.blockEngine.BlockAtPoint(movePosition + new Vector3(0f, 0f, -0.3f)) == BlockType.Ladder || _P.blockEngine.BlockAtPoint(movePosition + new Vector3(0f, 0f, 0.3f)) == BlockType.Ladder)
            {
                ladder = true;
            }
            else if (_P.blockEngine.BlockAtPoint(movePosition + new Vector3(-0.3f, -1.2f, 0f)) == BlockType.Ladder || _P.blockEngine.BlockAtPoint(movePosition + new Vector3(0.3f, -1.2f, 0f)) == BlockType.Ladder || _P.blockEngine.BlockAtPoint(movePosition + new Vector3(0f, -1.2f, -0.3f)) == BlockType.Ladder || _P.blockEngine.BlockAtPoint(movePosition + new Vector3(0f, -1.2f, 0.3f)) == BlockType.Ladder)
            {//definitely not synced
                ladder = true;
            }

            for (int x = -1; x < 2; x++)
                for (int y = -1; y < 2; y++)
                    for (int z = -1; z < 2; z++)
                    {
                        Vector3 box = new Vector3(size * x, 0, size * z);//size * y

                        if (_P.blockEngine.SolidAtPointForPlayer(movePosition + box)) 
                        {
                            midBlock = _P.blockEngine.BlockAtPoint(movePosition + box);
                            upperBlock = _P.blockEngine.BlockAtPoint(movePosition + box);

                            if (midBlock == BlockType.Plate || upperBlock == BlockType.Plate)
                            {
                                touch = movePosition + box;
                            }
                            
                            allow = false;
                        }
                        else if (_P.blockEngine.SolidAtPointForPlayer(movePosition + box + new Vector3(0, -(correction), 0)))
                        {//prevent moving too close when there isnt a block @ head height
                            //low fps issue ; 1.45 for high fps, 1.0 for low
                            midBlock = _P.blockEngine.BlockAtPoint(movePosition + box + new Vector3(0, -(correction), 0));
                            upperBlock = _P.blockEngine.BlockAtPoint(movePosition + box + new Vector3(0, -(correction), 0));

                            if (midBlock == BlockType.Plate || upperBlock == BlockType.Plate)
                            {
                                touch = movePosition + box;
                            }
                            allow = false;
                        }
                        else if (_P.blockEngine.SolidAtPointForPlayer(movePosition + box + new Vector3(0, -(correction/2), 0)))
                        {//prevent moving too close when there isnt a block @ head height
                            //low fps issue ; 1.45 for high fps, 1.0 for low
                            midBlock = _P.blockEngine.BlockAtPoint(movePosition + box + new Vector3(0, -(correction/2), 0));
                            upperBlock = _P.blockEngine.BlockAtPoint(movePosition + box + new Vector3(0, -(correction/2), 0));

                            if (midBlock == BlockType.Plate || upperBlock == BlockType.Plate)
                            {
                                touch = movePosition + box;
                            }
                            allow = false;
                        }
                        else if (_P.blockEngine.SolidAtPointForPlayer(moveVector + box + _P.playerPosition))
                        {
                            midBlock = _P.blockEngine.BlockAtPoint(moveVector + box + _P.playerPosition);
                            upperBlock = _P.blockEngine.BlockAtPoint(moveVector + box + _P.playerPosition);

                            if (midBlock == BlockType.Plate || upperBlock == BlockType.Plate)
                            {
                                touch = moveVector + box + _P.playerPosition;
                            }
                            allow = false;
                        }
                    }

            if (allow == true)
            {
                Vector3 diff = (movePosition - _P.playerPosition);
                diff.Normalize();

                Vector3 hitm = Vector3.Zero;
                Vector3 buildm = Vector3.Zero;
                _P.blockEngine.RayCollision(_P.playerPosition + new Vector3(0f, 0.1f, 0f), diff, 0.2f, 25, ref hitm, ref buildm, true);
                if (hitm != Vector3.Zero)
                {
                    allow = false;
                }
                //if(_P.blockEngine.RayCollision(_P.playerPosition,moveVector,1.0f,20)
            }
         
            if (ladder)
            {
                Vector3 footPosition = movePosition + new Vector3(0f, -1.6f, 0f);

                _P.playerVelocity.Y = CLIMBVELOCITY;

                if (_P.blockEngine.BlockAtPoint(footPosition) != BlockType.Ladder)//providing initial boost
                if (_P.blockEngine.BlockAtPoint(footPosition) != BlockType.None)
                {
                    if (!_P.blockEngine.SolidAtPointForPlayer(movePosition + new Vector3(0, 0.5f, 0) + Vector3.UnitY * (float)gameTime.ElapsedGameTime.TotalSeconds))
                    _P.playerPosition.Y += (float)gameTime.ElapsedGameTime.TotalSeconds;
                }
                //if (_P.blockEngine.SolidAtPointForPlayer(footPosition + _P.playerCamera.GetLookVector() * 1.25f))
                 //   if (!_P.blockEngine.SolidAtPointForPlayer(_P.moveP + Vector3.UnitY * 0.15f))
                    //{
   
                        //  _P.playerPosition.Y += 0.1f;
                   // }
             //   return true;

            }

            if (allow == true)
                if (!_P.blockEngine.SolidAtPointForPlayer(movePosition) && !_P.blockEngine.SolidAtPointForPlayer(lowerBodyPointB) && !_P.blockEngine.SolidAtPointForPlayer(midBodyPoint))
            {//&& !_P.blockEngine.SolidAtPointForPlayer(lowerBodyPoint)
                //low fps stopped here
                testVector = moveVector;
                testVector.Normalize();
                testVector = testVector * (moveLength * 0.11f);//prevent player from getting camera too close to block
                movePosition = _P.playerPosition + testVector;
                midBodyPoint = movePosition + new Vector3(0, -0.7f, 0);
                lowerBodyPoint = movePosition + new Vector3(0, -1.4f, 0);
                Vector3 upper = movePosition + new Vector3(0, 0.5f, 0);

                if (!_P.blockEngine.SolidAtPointForPlayer(movePosition) && !_P.blockEngine.SolidAtPointForPlayer(lowerBodyPoint) && !_P.blockEngine.SolidAtPointForPlayer(midBodyPoint))
                {
                    if (!_P.blockEngine.SolidAtPointForPlayer(upper))
                    {
                        _P.playerPosition = _P.playerPosition + moveVector;
                    }
                    else
                    {
                        moveVector.Y = 0;
                        _P.playerPosition = _P.playerPosition + moveVector;
                    }
                    return true;
                }
            }

            // It's solid there, so while we can't move we have officially collided with it.
            BlockType lowerBlock = _P.blockEngine.BlockAtPoint(lowerBodyPoint);// + new Vector3(0, -0.2f, 0));

            //BlockType midBlock = _P.blockEngine.BlockAtPoint(midBodyPoint);
            //BlockType upperBlock = _P.blockEngine.BlockAtPoint(movePosition);

            // It's solid there, so see if it's a lava block. If so, touching it will kill us!
            if (upperBlock == BlockType.Lava || lowerBlock == BlockType.Lava || midBlock == BlockType.Lava)
            {
                if (_P.artifactActive[(byte)_P.playerTeam, 16] == 0)
                {
                    _P.KillPlayer(DeathMessage.deathByLava);
                    return true;
                }
            }
            else if(upperBlock == BlockType.Plate || lowerBlock == BlockType.Plate || midBlock == BlockType.Plate)
            {
                if(_P.retrigger < DateTime.Now)
                if (_P.blockEngine.blockList[(uint)(touch.X), (uint)(touch.Y), (uint)(touch.Z)] == BlockType.Plate)
                {
                    _P.retrigger = DateTime.Now + TimeSpan.FromSeconds(0.2);
                    _P.SendPlayerInteract(1, (uint)(touch.X), (uint)(touch.Y), (uint)(touch.Z));
                }
                else
                {
                    _P.retrigger = DateTime.Now + TimeSpan.FromSeconds(0.2);
                    _P.SendPlayerInteract(1, (uint)(movePosition.X + testVector.X), (uint)(movePosition.Y + testVector.Y - 0.5f), (uint)(movePosition.Z + testVector.Z));
                }
            }
          
            // If it's a ladder, move up.
            
            return false;
        }

        public override void OnRenderAtEnter(GraphicsDevice graphicsDevice)
        {

        }

        public override void OnRenderAtUpdate(GraphicsDevice graphicsDevice, GameTime gameTime)
        {
            if (_P.blockEngine.bloomPosteffect != null)
                _P.blockEngine.bloomPosteffect.SetRenderTarget(graphicsDevice);

            _P.skyplaneEngine.Render(graphicsDevice);
            _P.particleEngine.Render(graphicsDevice);
            _P.playerEngine.Render(graphicsDevice);
            _P.blockEngine.Render(graphicsDevice, gameTime);

            if (_P.blockEngine.bloomPosteffect != null)
                _P.blockEngine.bloomPosteffect.Draw(graphicsDevice);

            _P.playerEngine.RenderPlayerNames(graphicsDevice);
            _P.interfaceEngine.Render(graphicsDevice);

            _SM.Window.Title = "Infiniminer";
        }

        DateTime startChat = DateTime.Now;
        public override void OnCharEntered(EventInput.CharacterEventArgs e)
        {
            if ((int)e.Character < 32 || (int)e.Character > 126) //From space to tilde
                return; //Do nothing
            if (_P.chatMode != ChatMessageType.None)
            {
                //Chat delay to avoid entering the "start chat" key, an unfortunate side effect of the new key bind system
                TimeSpan diff = DateTime.Now - startChat;
                if (diff.TotalMilliseconds >= 2)
                {
                    if (!(Keyboard.GetState().IsKeyDown(Keys.LeftControl) || Keyboard.GetState().IsKeyDown(Keys.RightControl)))
                    {
                        _P.chatEntryBuffer += e.Character;
                    }
                }
            }
        }

        private void HandleInput(Buttons input)
        {
            switch (input)
            {

                case Buttons.Fire:
                    if (!_P.playerDead)
                    if (_P.playerToolCooldown <= 0)
                    {
                        switch (_P.playerTools[_P.playerToolSelected])
                        {
                            // Disabled as everyone speed-mines now.
                            //case PlayerTools.Pickaxe:
                            //    if (_P.playerClass != PlayerClass.Miner)
                            //        _P.FirePickaxe();
                            //    break;

                            case PlayerTools.ConstructionGun:
                                if (_P.playerBlockSelected >= _P.playerBlocks.Length)
                                {
                                    _P.FireItemGun((byte)_P.playerItems[_P.playerBlockSelected - _P.playerBlocks.Length]);//_P.playerBlockSelected);
                                }
                                else
                                    _P.FireConstructionGun(_P.playerBlocks[_P.playerBlockSelected]);
                                break;

                            case PlayerTools.DeconstructionGun:
                                _P.FireDeconstructionGun();
                                break;

                            case PlayerTools.Detonator:
                                _P.PlaySound(InfiniminerSound.ClickHigh);
                                _P.FireDetonator();
                                break;

                            case PlayerTools.Remote:
                                _P.PlaySound(InfiniminerSound.ClickLow);
                                _P.FireSetRemote();
                                break;

                            case PlayerTools.ProspectingRadar:
                                _P.FireRadar();
                                break;

                            case PlayerTools.ThrowBomb:
                                _P.FireBomb();
                                break;

                            case PlayerTools.ThrowRope:
                                _P.FireRope();
                                break;

                            case PlayerTools.Hide:
                                _P.Hide();
                                break;
                        }
                    }
                    break;
                case Buttons.AltFire:
                    if (!_P.playerDead)
                    if (_P.playerToolCooldown <= 0)
                    {
                        switch (_P.playerClass)
                        {
                            case PlayerClass.Miner:
                                _P.StrongArm();
                                break;
                            case PlayerClass.Engineer:
                                _P.PlaySound(InfiniminerSound.ClickHigh);
                                _P.FireRemote();
                                break;
                            case PlayerClass.Sapper:
                               // _P.PlaySound(InfiniminerSound.ClickHigh);
                                _P.FireBomb();
                                //_P.Smash();
                                //Vector3 smashVector = _P.playerCamera.GetLookVector();// +_P.playerVelocity;
                                //_P.Content[6] = (int)(smashVector.X * 1000);
                                //_P.Content[7] = (int)(smashVector.Y * 1000);
                                //_P.Content[8] = (int)(smashVector.Z * 1000);
                                //_P.Content[5] = 5*1000;//5 second smash

                                //_P.addChatMessage(_P.Content[6] + "/" + _P.Content[7] + "/" +_P.Content[8], ChatMessageType.SayAll, 10);
                                break;
                            case PlayerClass.Prospector:
                                _P.PlaySound(InfiniminerSound.ClickHigh);
                                _P.Hide();
                                break;
                        }
                    }
                    break;
                case Buttons.Jump:
                    {
                        if (!_P.playerDead)
                        {
                            // Vector3 belowfootPosition = _P.playerPosition + new Vector3(0f, -2.5f, 0f);
                            Vector3 footPosition = _P.playerPosition + new Vector3(0f, -1.5f, 0f);
                            Vector3 midPosition = _P.playerPosition + new Vector3(0f, -0.7f, 0f);

                            if (_P.blockEngine.SolidAtPointForPlayer(footPosition) && _P.playerVelocity.Y == 0)//wings
                            {
                                if (_P.blockEngine.BlockAtPoint(footPosition) == BlockType.Mud && _P.Content[10] != 12 && _P.artifactActive[(byte)_P.playerTeam, 12] == 0)//bog artifact
                                {
                                    _P.playerVelocity.Y = JUMPVELOCITY / 3;
                                }
                                else
                                {
                                    _P.playerVelocity.Y = JUMPVELOCITY;
                                    if (_P.Content[10] == 18)
                                        _P.playerVelocity.Y *= 1.2f;
                                }
                                float amountBelowSurface = ((ushort)footPosition.Y) + 1 - footPosition.Y;
                                _P.playerPosition.Y += amountBelowSurface + 0.01f;
                            }

                            if (_P.blockEngine.BlockAtPoint(midPosition) == BlockType.Water)
                            {
                                _P.playerVelocity.Y = JUMPVELOCITY * 0.4f;
                            }
                            else if (_P.Content[10] == 18 && _P.playerVelocity.Y < -0.6f)//wings
                            {
                                bool allow = true;

                                if(_P.blockEngine.SolidAtPointForPlayer(midPosition + new Vector3(-0.2f, 0.8f, 0.0f)) || _P.blockEngine.SolidAtPointForPlayer(midPosition + new Vector3(0.2f, 0.8f, 0.0f)) || _P.blockEngine.SolidAtPointForPlayer(midPosition + new Vector3(0.0f, 0.8f, 0.2f)) || _P.blockEngine.SolidAtPointForPlayer(midPosition + new Vector3(0.0f, 0.8f, -0.2f)))
                                {
                                    allow = false;
                                }
                                else if (_P.blockEngine.SolidAtPointForPlayer(midPosition + new Vector3(-0.2f, 1.6f, 0.0f)) || _P.blockEngine.SolidAtPointForPlayer(midPosition + new Vector3(0.2f, 1.6f, 0.0f)) || _P.blockEngine.SolidAtPointForPlayer(midPosition + new Vector3(0.0f, 1.6f, 0.2f)) || _P.blockEngine.SolidAtPointForPlayer(midPosition + new Vector3(0.0f, 1.6f, -0.2f)))
                                {
                                    allow = false;
                                }
                                if (_P.playerPosition.Y < 64 && allow)
                                {
                                    if (_P.screenEffect == ScreenEffect.None)
                                    {
                                        _P.screenEffect = ScreenEffect.Explosion;
                                        _P.screenEffectCounter = 1.8f;
                                    }
                                    _P.playerVelocity.Y += JUMPVELOCITY * 1.4f;
                                }
                            }
                        }
                    }
                    break;
                case Buttons.ToolUp:
                    if (!_P.playerDead)
                    {
                        _P.PlaySound(InfiniminerSound.ClickLow);
                        _P.playerToolSelected += 1;
                        if (_P.playerToolSelected >= _P.playerTools.Length)
                            _P.playerToolSelected = 0;
                    }
                    else
                    {
                    }
                    break;
                case Buttons.ToolDown:
                    if (!_P.playerDead)
                    {
                        _P.PlaySound(InfiniminerSound.ClickLow);
                        _P.playerToolSelected -= 1;
                        if (_P.playerToolSelected < 0)
                            _P.playerToolSelected = _P.playerTools.Length;
                    }
                    break;
                case Buttons.Tool1:
                    if (!_P.playerDead)
                    {
                        if (_P.interact == BlockType.None)
                        {
                            _P.playerToolSelected = 0;
                            _P.PlaySound(InfiniminerSound.ClickLow);
                            if (_P.playerToolSelected >= _P.playerTools.Length)
                                _P.playerToolSelected = _P.playerTools.Length - 1;
                        }
                        else
                        {
                            if (_P.blockInteract < DateTime.Now)
                            {
                                _P.blockInteract = DateTime.Now + TimeSpan.FromSeconds(0.5);
                                HandleInput(Buttons.Interact1);
                            }
                        }
                    }
                    break;
                case Buttons.Tool2:
                    if (!_P.playerDead)
                    {
                        if (_P.interact == BlockType.None)
                        {
                            _P.playerToolSelected = 1;
                            _P.PlaySound(InfiniminerSound.ClickLow);
                            if (_P.playerToolSelected >= _P.playerTools.Length)
                                _P.playerToolSelected = _P.playerTools.Length - 1;
                        }
                        else
                        {
                            if (_P.blockInteract < DateTime.Now)
                            {
                                _P.blockInteract = DateTime.Now + TimeSpan.FromSeconds(0.5);
                                HandleInput(Buttons.Interact2);
                            }
                        }
                    }
                    break;
                case Buttons.Tool3:
                    if (!_P.playerDead)
                    {
                        if (_P.interact == BlockType.None)
                        {
                            _P.playerToolSelected = 2;
                            _P.PlaySound(InfiniminerSound.ClickLow);
                            if (_P.playerToolSelected >= _P.playerTools.Length)
                                _P.playerToolSelected = _P.playerTools.Length - 1;
                        }
                        else
                        {
                            if (_P.blockInteract < DateTime.Now)
                            {
                                _P.blockInteract = DateTime.Now + TimeSpan.FromSeconds(0.5);
                                HandleInput(Buttons.Interact3);
                            }
                        }
                    }
                    break;
                case Buttons.Tool4:
                    if (!_P.playerDead)
                    {
                        if (_P.interact == BlockType.None)
                        {
                            _P.playerToolSelected = 3;
                            _P.PlaySound(InfiniminerSound.ClickLow);
                            if (_P.playerToolSelected >= _P.playerTools.Length)
                                _P.playerToolSelected = _P.playerTools.Length - 1;
                        }
                        else
                        {
                            if (_P.blockInteract < DateTime.Now)
                            {
                                _P.blockInteract = DateTime.Now + TimeSpan.FromSeconds(0.5);
                                HandleInput(Buttons.Interact4);
                            }
                        }
                    }
                    break;
                case Buttons.Tool5:
                    if (!_P.playerDead)
                    {
                        if (_P.interact == BlockType.None)
                        {
                            _P.playerToolSelected = 4;
                            _P.PlaySound(InfiniminerSound.ClickLow);
                            if (_P.playerToolSelected >= _P.playerTools.Length)
                                _P.playerToolSelected = _P.playerTools.Length - 1;
                        }
                        else
                        {
                            if (_P.blockInteract < DateTime.Now)
                            {
                                _P.blockInteract = DateTime.Now + TimeSpan.FromSeconds(0.5);
                                HandleInput(Buttons.Interact5);
                            }
                        }
                    }
                    break;
                case Buttons.BlockUp:
                    if (!_P.playerDead)
                    {
                        if (_P.playerTools[_P.playerToolSelected] == PlayerTools.ConstructionGun)
                        {
                            _P.PlaySound(InfiniminerSound.ClickLow);
                            _P.playerBlockSelected += 1;
                            if (_P.playerBlockSelected >= (_P.playerBlocks.Length + _P.playerItems.Length))// >= changed to > for one item // constructitem
                                _P.playerBlockSelected = 0;
                        }
                    }
                    break;
                case Buttons.BlockDown:
                    if (!_P.playerDead)
                    {
                        if (_P.playerTools[_P.playerToolSelected] == PlayerTools.ConstructionGun)
                        {
                            _P.PlaySound(InfiniminerSound.ClickLow);
                            _P.playerBlockSelected -= 1;
                            if (_P.playerBlockSelected < 0)
                                _P.playerBlockSelected = (_P.playerBlocks.Length - 1) + _P.playerItems.Length;
                        }
                    }
                    break;
                case Buttons.Interact1:
                    if (!_P.playerDead)
                    {
                        BlockType targetd = _P.Interact();
                        if (targetd == BlockType.BankRed && _P.playerTeam == PlayerTeam.Red || targetd == BlockType.BankBlue && _P.playerTeam == PlayerTeam.Blue)
                        {
                            _P.DepositOre();
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                        else
                        {
                            _P.PlayerInteract(1);
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                    }
                    break;
                case Buttons.Interact2:
                    if (!_P.playerDead)
                    {
                        BlockType targetw = _P.Interact();
                        if (targetw == BlockType.BankRed && _P.playerTeam == PlayerTeam.Red || targetw == BlockType.BankBlue && _P.playerTeam == PlayerTeam.Blue)
                        {
                            _P.WithdrawOre();
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                        else
                        {
                            _P.PlayerInteract(2);
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                    }
                    break;
                case Buttons.Interact3:
                    if (!_P.playerDead)
                    {
                        BlockType targeta = _P.Interact();
                        if (targeta == BlockType.BankRed && _P.playerTeam == PlayerTeam.Red || targeta == BlockType.BankBlue && _P.playerTeam == PlayerTeam.Blue)
                        {
                            _P.DepositOre();
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                        else
                        {
                            _P.PlayerInteract(3);
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                    }
                    break;
                case Buttons.Interact4:
                    if (!_P.playerDead)
                    {
                        BlockType targetb = _P.Interact();
                        if (targetb == BlockType.BankRed && _P.playerTeam == PlayerTeam.Red || targetb == BlockType.BankBlue && _P.playerTeam == PlayerTeam.Blue)
                        {
                            _P.WithdrawOre();
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                        else
                        {
                            _P.PlayerInteract(4);
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                    }
                    break;
                case Buttons.Interact5:
                    if (!_P.playerDead)
                    {
                        BlockType targetc = _P.Interact();
                        if (targetc == BlockType.BankRed && _P.playerTeam == PlayerTeam.Red || targetc == BlockType.BankBlue && _P.playerTeam == PlayerTeam.Blue)
                        {
                            _P.WithdrawOre();
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                        else
                        {
                            _P.PlayerInteract(5);
                            _P.PlaySound(InfiniminerSound.ClickHigh);
                        }
                    }
                    break;
                case Buttons.Ping:
                    if (!_P.playerDead)
                    {
                        NetBuffer msgBuffer = _P.netClient.CreateBuffer();
                        msgBuffer.Write((byte)InfiniminerMessage.PlayerPing);
                        msgBuffer.Write(_P.playerMyId);
                        _P.netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
                    }
                    break;
                case Buttons.ChangeClass:
                    if (!_P.playerDead)
                    {
                        if (_P.blockEngine.BlockAtPoint(_P.playerPosition - Vector3.UnitY * 1.5f) != BlockType.None || _P.playerClass == PlayerClass.None || _P.playerDead)
                        {
                            nextState = "Infiniminer.States.ClassSelectionState";
                        }
                    }
                    break;
                case Buttons.ChangeTeam:
                    if (!_P.playerDead)
                    {
                        if (_P.blockEngine.BlockAtPoint(_P.playerPosition - Vector3.UnitY * 1.5f) != BlockType.None || _P.playerTeam == PlayerTeam.None || _P.playerDead)
                        {
                            nextState = "Infiniminer.States.TeamSelectionState";
                        }
                    }
                    break;
                case Buttons.SayAll:
                    _P.chatMode = ChatMessageType.SayAll;
                    startChat = DateTime.Now;
                    break;
                case Buttons.SayTeam:
                    _P.chatMode = _P.playerTeam == PlayerTeam.Red ? ChatMessageType.SayRedTeam : ChatMessageType.SayBlueTeam;
                    startChat = DateTime.Now;
                    break;
            }
        }

        public override void OnKeyDown(Keys key)
        {
            // Exit!
            if (key == Keys.Y && Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                _P.SendDisconnect();
                _P.netClient.Disconnect("Client disconnected.");
                nextState = "Infiniminer.States.ServerBrowserState";
            }

            // Pixelcide!
            if (key == Keys.K && Keyboard.GetState().IsKeyDown(Keys.Escape) && !_P.playerDead)
            {
                _P.KillPlayer(DeathMessage.deathBySuic);//"HAS COMMMITTED PIXELCIDE!");
                return;
            }

            //Map saving!
            if ((Keyboard.GetState().IsKeyDown(Keys.LeftControl) || Keyboard.GetState().IsKeyDown(Keys.RightControl)) && key == Keys.S)
            {
                _P.SaveMap();
                return;
            }

            if ((Keyboard.GetState().IsKeyDown(Keys.LeftControl) || Keyboard.GetState().IsKeyDown(Keys.RightControl)) && key == Keys.C)
            {
                if (_P.Challenge == false)
                {
                    _P.ChallengeHost();
                }
                return;
            }


            if (_P.chatMode != ChatMessageType.None)
            {
                if (Keyboard.GetState().IsKeyDown(Keys.LeftControl) || Keyboard.GetState().IsKeyDown(Keys.RightControl))
                {
                    if (key == Keys.V)
                    {
                        var clipboardText = CrossPlatformServices.Instance.GetClipboardTextAsync().Result;
                        _P.chatEntryBuffer += clipboardText;
                        return;
                    }
                    else if (key == Keys.C)
                    {
                        CrossPlatformServices.Instance.SetClipboardTextAsync(_P.chatEntryBuffer);
                        return;
                    }
                    else if (key == Keys.X)
                    {
                        CrossPlatformServices.Instance.SetClipboardTextAsync(_P.chatEntryBuffer);
                        _P.chatEntryBuffer = "";
                        return;
                    }
                }
                // Put the characters in the chat buffer.
                if (key == Keys.Enter)
                {
                    // If we have an actual message to send, fire it off at the server.
                    if (_P.chatEntryBuffer.Length > 0)
                    {
                        if (_P.netClient.Status == NetConnectionStatus.Connected)
                        {
                            NetBuffer msgBuffer = _P.netClient.CreateBuffer();
                            msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                            msgBuffer.Write((byte)_P.chatMode);
                            msgBuffer.Write(_P.chatEntryBuffer);
                            _P.netClient.SendMessage(msgBuffer, NetChannel.ReliableInOrder3);
                        }
                        else
                        {
                            _P.addChatMessage("Not connected to server.", ChatMessageType.SayAll, 10);
                        }
                    }

                    _P.chatEntryBuffer = "";
                    _P.chatMode = ChatMessageType.None;
                }
                else if (key == Keys.Back)
                {
                    if (_P.chatEntryBuffer.Length > 0)
                        _P.chatEntryBuffer = _P.chatEntryBuffer.Substring(0, _P.chatEntryBuffer.Length - 1);
                }
                else if (key == Keys.Escape)
                {
                    _P.chatEntryBuffer = "";
                    _P.chatMode = ChatMessageType.None;
                }
                return;
            }
            else if (key == Keys.Enter)
            {
                _P.chatMode = _P.playerTeam == PlayerTeam.Red ? ChatMessageType.SayRedTeam : ChatMessageType.SayBlueTeam;
            }
            else //if (!_P.playerDead)
            {
                Buttons boundButton = (_SM as InfiniminerGame).keyBinds.GetBound(key);
                HandleInput(boundButton);
            }
            
        }

        public override void OnKeyUp(Keys key)
        {

        }

        public override void OnMouseDown(MouseButton button, int x, int y)
        {
            // If we're dead, come back to life.
            if (_P.playerDead && _P.screenEffectCounter > 2)
                _P.RespawnPlayer();
            else if (!_P.playerDead)
                HandleInput((_SM as InfiniminerGame).keyBinds.GetBound(button));
        }

        public override void OnMouseUp(MouseButton button, int x, int y)
        {

        }

        public override void OnMouseScroll(int scrollDelta)
        {
            if (_P.playerDead)
                return;
            else
            {
                if (scrollDelta >= 120)
                {
                    //Console.WriteLine("Handling input for scroll up...");
                    HandleInput((_SM as InfiniminerGame).keyBinds.GetBound(MouseButton.WheelUp));//.keyBinds.GetBound(button));
                }
                else if (scrollDelta <= -120)
                {
                    HandleInput((_SM as InfiniminerGame).keyBinds.GetBound(MouseButton.WheelDown));
                }
            }
        }
    }
}
