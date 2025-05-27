using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;
using Lidgren.Network.Xna;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Infiniminer
{
    public class Item
    {
        public uint ID;
        public PlayerTeam Team;
        public int[] Content;
        public bool Disposing;//deleting from array in progress
        public Vector3 Heading;
        public Vector3 Position;
        public float Weight = 1.0f;
        public float Scale = 0.5f;
        public Vector3 Velocity = Vector3.Zero;
        public Vector3 deltaPosition;
        public bool Billboard = false;
        public ItemType Type;
        public DateTime Frozen;//frozen until greater than this time
        public bool QueueAnimationBreak = false;

        // Things that affect animation.
        public SpriteModel SpriteModel;
        private Game gameInstance;

        public Item(Game gameInstance, ItemType iType)
        {
            Content = new int[20];

            Frozen = DateTime.Now;

            Type = iType;
            this.gameInstance = gameInstance;
            if (gameInstance != null)
            {
                this.SpriteModel = new SpriteModel(gameInstance, 4);
                UpdateSpriteTexture();
                this.IdleAnimation = true;
            }
            else
            {
                Content[5] = 1;
            }
        }

        private bool idleAnimation = false;
        public bool IdleAnimation
        {
            get { return idleAnimation; }
            set
            {
                if (idleAnimation != value)
                {
                    idleAnimation = value;
                    if (gameInstance != null)
                    {
                       // if (idleAnimation)
                        if (Type == ItemType.Artifact)
                        {
                            //SpriteModel.
                            SpriteModel.SetPassiveAnimation("0,0.2;1,0.2;2,0.2;3,0.2"); //("0,0.5 ; 1,0.5 ; 2,0.5 ; 3,0.5");
                            //SpriteModel.StartActiveAnimation("3,0.15");
                            //SpriteModel.
                        }
                        else if (Type == ItemType.Bomb)
                        {
                            SpriteModel.SetPassiveAnimation("0,0.2;1,0.2;2,0.2;3,0.2");
                        }
                        else if (Type == ItemType.Spikes)
                        {
                            Scale = 1.0f;
                        }
                        else if (Type == ItemType.Target)
                        {
                            Billboard = false;
                            Scale = 0.5f;
                        }
                        //else
                          //  SpriteModel.SetPassiveAnimation("0,0.2;1,0.2;2,0.2;1,0.2");
                       // else
                       //     SpriteModel.SetPassiveAnimation("0,0.2;1,0.2;2,0.2;1,0.2");
                    }
                }
            }
        }

        private void UpdateSpriteTexture()
        {
            if (gameInstance == null)
                return;

            string textureName = "";

            switch(Type)
            {
                case ItemType.Gold:
                    textureName = "sprites/tex_sprite_lemonorgold";
                    break;
                case ItemType.Ore:
                    textureName = "sprites/tex_sprite_lemonorore";
                    break;
                case ItemType.Artifact:
                    textureName = "sprites/tex_sprite_artifact";
                    break;
                case ItemType.Diamond:
                    textureName = "sprites/tex_sprite_diamond";
                    break;
                case ItemType.Bomb:
                    textureName = "sprites/tex_sprite_bomb";
                    break;
                case ItemType.Rope:
                    textureName = "sprites/tex_sprite_diamond";
                    break;
                case ItemType.Static:
                    textureName = "sprites/tex_sprite_grass";
                    break;
                case ItemType.Mushroom:
                    textureName = "sprites/tex_sprite_mushroom";
                    break;
                case ItemType.Spikes:
                    textureName = "sprites/tex_sprite_spikes";
                    break;
                case ItemType.Target:
                    textureName = "sprites/tex_sprite_target";
                    break;
                case ItemType.DirtBomb:
                    textureName = "sprites/tex_sprite_bomb";
                    break;
                default:
                    textureName = "sprites/tex_sprite_lemonorgoldnum";
                    break;
            }
            
            Texture2D orig = gameInstance.Content.Load<Texture2D>(textureName);
           
            this.SpriteModel.SetSpriteTexture(orig);
        }
    }
}