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
    public class PlayerEngine
    {
        InfiniminerGame gameInstance;
        PropertyBag _P;
     
        public PlayerEngine(InfiniminerGame gameInstance)
        {
            this.gameInstance = gameInstance;
        }

        public void Update(GameTime gameTime)
        {
            if (_P == null)
                return;

            foreach (Player p in _P.playerList.Values)
            {
                p.StepInterpolation(gameTime.TotalGameTime.TotalSeconds);

                p.Ping -= (float)gameTime.ElapsedGameTime.TotalSeconds/4;
                if (p.Ping < 0)
                    p.Ping = 0;

                p.TimeIdle += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (p.TimeIdle > 0.5f)
                    p.IdleAnimation = true;

                if (!(float.IsNaN(p.Position.X)))
                {
                    if (p.Class == PlayerClass.Prospector && p.Content[5] == 1)
                    {
                        p.deltaPosition = Vector3.Zero;
                    }
                    else
                    {
                        p.deltaPosition = p.deltaPosition + (((p.Position - p.deltaPosition) * (8 * (float)gameTime.ElapsedGameTime.TotalSeconds)));

                        //for (int a = -5; a < 6; a++)
                        //    for (int b = -5; b < 6; b++)
                        //        for (int c = -5; c < 6; c++)
                        //        {
                        //            int nx = a + (int)p.deltaPosition.X;
                        //            int ny = b + (int)p.deltaPosition.Y;
                        //            int nz = c + (int)p.deltaPosition.Z;
                        //            if (nx < 63 && ny < 63 && nz < 63 && nx > 0 && ny > 0 && nz > 0)
                        //            {
                        //                if ((int)p.lastPosition.X != (int)p.deltaPosition.X || (int)p.lastPosition.Y != (int)p.deltaPosition.Y || (int)p.lastPosition.Z != (int)p.deltaPosition.Z)
                        //                {
                        //                    Vector3 raydir = new Vector3(nx + 0.5f, ny + 0.5f, nz + 0.5f) - p.deltaPosition;
                        //                    raydir.Normalize();
                        //                    float distray = (new Vector3(nx + 0.5f, ny + 0.5f, nz + 0.5f) - p.deltaPosition).Length();

                        //                    if (gameInstance.propertyBag.blockEngine.RayCollision(new Vector3(nx + 0.5f, ny + 0.5f, nz + 0.5f), raydir, distray - 1.0f, 5, ref nx, ref ny, ref nz))
                        //                    {
                        //                        float lightdist = distray;
                        //                        gameInstance.propertyBag.blockEngine.Light[nx, ny, nz] = 1.0f;// 1.0f - lightdist * 0.2f;

                        //                        uint region = gameInstance.propertyBag.blockEngine.GetRegion((ushort)nx, (ushort)ny, (ushort)nz);

                        //                        for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //                        {
                        //                            gameInstance.propertyBag.blockEngine.vertexListDirty[d, region] = true;
                        //                        }
                        //                    }
                        //                    else
                        //                    {
                        //                        gameInstance.propertyBag.blockEngine.Light[nx, ny, nz] = 0.1f;
                        //                        uint region = gameInstance.propertyBag.blockEngine.GetRegion((ushort)nx, (ushort)ny, (ushort)nz);

                        //                        for (int d = 1; d < (int)BlockTexture.MAXIMUM; d++)
                        //                        {
                        //                            gameInstance.propertyBag.blockEngine.vertexListDirty[d, region] = true;
                        //                        }
                        //                    }
                        //                }
                        //            }
                        //        }

                        p.lastPosition = p.deltaPosition;
                    }
                }
                //.zero for NAN problems with dragging window
                
                p.SpriteModel.Update(gameTime);
            }

            foreach (KeyValuePair<uint, Item> i in _P.itemList)//  if (bPair.Value.Team == _P.playerTeam)//doesnt care which team
            {
                i.Value.deltaPosition = i.Value.deltaPosition + (((i.Value.Position - i.Value.deltaPosition) * (8*(float)gameTime.ElapsedGameTime.TotalSeconds)));

                switch(i.Value.Type)
                {
                    case ItemType.Bomb:
                        if(gameInstance.propertyBag.randGen.Next(0, 5) == 1)
                        _P.particleEngine.CreateTrail(i.Value.deltaPosition + Vector3.UnitY * 0.3f, Color.Gray);
                    break;
                    case ItemType.Target:
                    if (i.Value.Content[9] >= gameInstance.FrameRate / 2)//this is per frame
                    {
                        _P.particleEngine.CreateTargetTrail(i.Value.deltaPosition + Vector3.UnitY * 0.3f, new Vector4((float)(i.Value.Content[1]) / 100, (float)(i.Value.Content[2]) / 100, (float)(i.Value.Content[3]) / 100, 1.0f), i.Value.Heading);
                        i.Value.Content[9] = 0;
                    }
                    else if (i.Value.Content[9] == 0)
                    {
                        _P.particleEngine.CreateTargetTrail(i.Value.deltaPosition + Vector3.UnitY * 0.3f, new Vector4((float)(i.Value.Content[1]) / 100, (float)(i.Value.Content[2]) / 100, (float)(i.Value.Content[3]) / 100, 1.0f), i.Value.Heading);
                    }
                    i.Value.Content[9]++;
                    break;
                    case ItemType.DirtBomb:
                    if (gameInstance.propertyBag.randGen.Next(0, 10) == 1)
                        _P.particleEngine.CreateTrail(i.Value.deltaPosition + Vector3.UnitY * 0.3f, Color.Lerp(Color.Chocolate, Color.Black,0.5f));
                    break;
                }
            }
        }

        public void Render(GraphicsDevice graphicsDevice)
        {
            // If we don't have _P, grab it from the current gameInstance.
            // We can't do this in the constructor because we are created in the property bag's constructor!
            if (_P == null)
                _P = gameInstance.propertyBag;

            foreach (Player p in _P.playerList.Values)
            {
                if (p.Alive && p.ID != _P.playerMyId)
                {
                    if (p.Content[10] == 18)
                    {
                        if (gameInstance.propertyBag.randGen.Next(0, 10) == 1)
                            _P.particleEngine.CreateWingsTrail((p.deltaPosition - (p.Heading / 2)) - new Vector3(0,0.1f,0), Color.GhostWhite, p.Heading);
                    }

                    p.SpriteModel.Draw(_P.playerCamera.ViewMatrix,
                                       _P.playerCamera.ProjectionMatrix,
                                       _P.playerCamera.Position,
                                       _P.playerCamera.GetLookVector(),
                                       p.deltaPosition - Vector3.UnitY * 1.5f,//delta
                                       p.Heading,
                                       2,new Vector4(1.0f,1.0f,1.0f,1.0f));
                }
            }

            Vector4 color;
            foreach (KeyValuePair<uint, Item> i in _P.itemList)//  if (bPair.Value.Team == _P.playerTeam)//doesnt care which team
            {
                if (i.Value.Type == ItemType.Artifact)//artifact pulse
                {
                    float goldtwinkle = 0.75f + (this.gameInstance.propertyBag.colorPulse * 0.25f);
                    //* this.gameInstance.propertyBag.colorPulse
                    color = new Vector4((float)(i.Value.Content[1]) / 100 * goldtwinkle, (float)(i.Value.Content[2]) / 100 * goldtwinkle, (float)(i.Value.Content[3]) / 100 * goldtwinkle, 1.0f);
                }
                else if (i.Value.Type == ItemType.Gold)//gold twinkle
                {
                    float goldtwinkle = 0.75f + (this.gameInstance.propertyBag.colorPulse * 0.25f);
                    color = new Vector4((float)(i.Value.Content[1]) / 100 * goldtwinkle, (float)(i.Value.Content[2]) / 100 * goldtwinkle, (float)(i.Value.Content[3]) / 100 * goldtwinkle, 1.0f);
                }
                else if (i.Value.Type == ItemType.Diamond)//twinkle
                {
                    float goldtwinkle = 0.75f + (this.gameInstance.propertyBag.colorPulse * 0.25f);
                    color = new Vector4((float)(i.Value.Content[1]) / 100 * goldtwinkle, (float)(i.Value.Content[2]) / 100 * goldtwinkle, (float)(i.Value.Content[3]) / 100 * goldtwinkle, 1.0f);
                }
                else if (i.Value.Type == ItemType.Mushroom)
                {
                    color = new Vector4((float)(i.Value.Content[1]) / 100, (float)(i.Value.Content[2]) / 100, (float)(i.Value.Content[3]) / 100, 1.0f);
                }
                else if (i.Value.Type == ItemType.Spikes)
                {
                    color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                }
                else if (i.Value.Type == ItemType.Target)
                {
                    float goldtwinkle = 0.75f + (this.gameInstance.propertyBag.colorPulse * 0.25f);
                    color = new Vector4((float)(i.Value.Content[1]) / 100 * goldtwinkle, (float)(i.Value.Content[2]) / 100 * goldtwinkle, (float)(i.Value.Content[3]) / 100 * goldtwinkle, 1.0f);
                }
                else if (i.Value.Type == ItemType.DirtBomb)
                {
                    color = new Vector4((float)(i.Value.Content[1]) / 100, (float)(i.Value.Content[2]) / 100, (float)(i.Value.Content[3]) / 100, 1.0f);
                }
                else if (i.Value.Type == ItemType.None)//debug
                {//item is simply set to its content color
                    color = new Vector4(i.Value.Content[1], i.Value.Content[2], i.Value.Content[3], 1.0f);
                }
                else
                {
                    color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                }

                if (i.Value.Billboard == true)//item always faces camera
                {
                    i.Value.SpriteModel.DrawBillboard(_P.playerCamera.ViewMatrix,
                                      _P.playerCamera.ProjectionMatrix,
                                      _P.playerCamera.Position,
                                      _P.playerCamera.GetLookVector(),
                                      i.Value.deltaPosition - Vector3.UnitY * i.Value.Scale / 10,
                                      i.Value.Heading,
                                      i.Value.Scale,
                                      color);
                }
                else//constrained like player sprites
                {
                    i.Value.SpriteModel.Draw(_P.playerCamera.ViewMatrix,
                                      _P.playerCamera.ProjectionMatrix,
                                      _P.playerCamera.Position,
                                      _P.playerCamera.GetLookVector(),
                                      i.Value.deltaPosition - Vector3.UnitY * i.Value.Scale / 10,
                                      i.Value.Heading,
                                      i.Value.Scale,
                                      color);
                }
            }
        }

        public void RenderPlayerNames(GraphicsDevice graphicsDevice)
        {
            // If we don't have _P, grab it from the current gameInstance.
            // We can't do this in the constructor because we are created in the property bag's constructor!
            if (_P == null)
                _P = gameInstance.propertyBag;

            foreach (Player p in _P.playerList.Values)
            {
                if (p.Alive && p.ID != _P.playerMyId)
                {
                    // Figure out what text we should draw on the player - only for teammates and nearby enemies
                    string playerText = "";
                    bool continueDraw=false;
                    if (p.ID != _P.playerMyId && p.Team == _P.playerTeam)
                        continueDraw = true;
                    else
                    {
                        Vector3 diff = (p.Position -_P.playerPosition);
                        float len = diff.Length();
                        diff.Normalize();
                        if (len<=8){//distance you can see players name
                            Vector3 hit = Vector3.Zero;
                            Vector3 build = Vector3.Zero;
                            gameInstance.propertyBag.blockEngine.RayCollision(_P.playerPosition + new Vector3(0f, 0.1f, 0f), diff, len, 25, ref hit, ref build);
                            if (hit == Vector3.Zero)
                                continueDraw = true;
                        }
                    }

                    if (continueDraw)//p.ID != _P.playerMyId && p.Team == _P.playerTeam)
                    {
                        if (p.Class == PlayerClass.Prospector && p.Content[5] == 1)
                        {//no drawing hidden
                        }
                        else
                        {
                            playerText = p.Handle;

                            if (p.Content[10] > 0)
                            {
                                playerText += "(*)";
                            }

                            if (p.Ping > 0)
                                playerText = "*** " + playerText + " ***";

                            p.SpriteModel.DrawText(_P.playerCamera.ViewMatrix,
                                                   _P.playerCamera.ProjectionMatrix,
                                                   p.Position - Vector3.UnitY * 1.5f,
                                                   playerText, p.Team == PlayerTeam.Blue ? _P.blue : _P.red);//Defines.IM_BLUE : Defines.IM_RED);

                            //if (p.Content[10] > 0)
                            //{
                            //    p.SpriteModel.DrawText(_P.playerCamera.ViewMatrix,
                            //                           _P.playerCamera.ProjectionMatrix,
                            //                           p.Position - Vector3.UnitY * 1.3f,
                            //                           "(" + ArtifactInformation.GetName(p.Content[10]) + ")", p.Team == PlayerTeam.Blue ? _P.blue : _P.red);//Defines.IM_BLUE : Defines.IM_RED);
                            //}
                        }
                    }
                }
            }
        }
    }
}
