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
    public enum PlayerClass
    {
        None,
        Prospector,
        Miner,
        Engineer,
        Sapper
    }

    public enum PlayerTools
    {
        Pickaxe,
        StrongArm,//strong arms pick up and throw ability
        Smash,//defenders charge
        Hide,//prospector
        ConstructionGun,
        DeconstructionGun,
        ProspectingRadar,
        Detonator,
        Remote,
        SetRemote,
        ThrowBomb,
        ThrowRope,
        ConstructItem,
        LeftHand,
        RightHand,
        Body,
        Head,
        LeftLeg,
        RightLeg
    }

    public enum PlayerTeam
    {
        None,
        Red,
        Blue
    }

    public class PlayerBase
    {
        public PlayerTeam team;
        public int X;
        public int Y;
        public int Z;
    }

    public class Player
    {
        public bool respawnExpired = false;
        public int FallBuffer = 40;
        public DateTime CheckTouchTime = DateTime.Now;
        public DateTime LastUpdate = DateTime.Now;
        public DateTime DisposeTime = DateTime.Now;
        public DateTime LastHit = DateTime.Now - TimeSpan.FromSeconds(20);
        public DateTime connectedTimer = DateTime.Now;
        public bool Falling = false;
        public bool Quit = false;
        public bool Resume = false;//reconnecting player
        public bool Disposing = false; // player is about to be deleted (this method is used to prevent conflicts with physics thread)
        public bool Kicked = false; // set to true when a player is kicked to let other clients know they were kicked
        public short admin = 0;
        public bool Vote = false;
        public int deathCount = 1;
        public bool IsAdmin
        {
            get
            {
                if (admin > 0)
                    return true;
                return false;
            }
        }
        public float Digs;//how many digs left over 5 seconds

        public int Annoying = 0;//boots player after spam
        public int Warnings = 0;//warnings 
        public PlayerClass Class;
        public bool AltColours = false;
        public Color redTeam = new Color();
        public Color blueTeam = new Color();
        public bool compression = false;
        public string Handle = "";
        public DateTime rTouch = DateTime.Now;
        public bool rTouching = true;
        public uint rCount = 0;
        public uint rUpdateCount = 0;
        public double rSpeedCount = 0;
        public DateTime rTime = DateTime.Now;
        public uint Health = 0;
        public uint HealthMax = 0;
        public uint OreMax = 0;
        public uint WeightMax = 0;
        public uint Ore = 0;
        public uint Weight = 0;
        public uint Cash = 0;
        public int[] Content = new Int32[100];
        public int Radar = 0;
        public bool Alive = false;
        public DateTime respawnTimer = DateTime.Now;
        public List<Vector3> ExplosiveList = new List<Vector3>();
        public uint ID;
        public Vector3 Heading = Vector3.Zero;
        public NetConnection NetConn;
        public float TimeIdle = 0;
        public uint Score = 0;
        public uint Exp = 0;
        public float Ping = 0;
        public string IP = "";
        public Int32[] StatusEffect = new Int32[20];
        // This is used to force an update that says the player is not using their tool, thus causing a break
        // in their tool usage animation.
        public bool QueueAnimationBreak = false;

        // Things that affect animation.
        public SpriteModel SpriteModel;
        private Game gameInstance;

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
                        if (idleAnimation)
                            SpriteModel.SetPassiveAnimation("1,0.2");
                        else
                            SpriteModel.SetPassiveAnimation("0,0.2;1,0.2;2,0.2;1,0.2");
                    }
                }
            }
        }
        public Vector3 deltaPosition = Vector3.Zero;
        private Vector3 position = Vector3.Zero;
        public Vector3 lastPosition = Vector3.Zero;
        public Vector3 Position
        {
            get { return position; }
            set
            {
                if (position != value)
                {
                    TimeIdle = 0;
                    IdleAnimation = false;
                    position = value;
                }
            }
        }

        private struct InterpolationPacket
        {
            public Vector3 position;
            public double gameTime;

            public InterpolationPacket(Vector3 position, double gameTime)
            {
                this.position = position;
                this.gameTime = gameTime;
            }
        }

        private List<InterpolationPacket> interpList = new List<InterpolationPacket>();

        public void UpdatePosition(Vector3 position, double gameTime)
        {
            interpList.Add(new InterpolationPacket(position, gameTime));

            // If we have less than 10 packets, go ahead and set the position directly.
            if (interpList.Count < 10)
                Position = position;

            // If we have more than 10 packets, remove the oldest.
            if (interpList.Count > 10)
                interpList.RemoveAt(0);

            if (Math.Abs(deltaPosition.X - position.X) > 5.0f)//warp player 
            {
                deltaPosition = position;
            }
            else if (Math.Abs(deltaPosition.Y - position.Y) > 5.0f)
            {
                deltaPosition = position;
            }
            else if (Math.Abs(deltaPosition.Z - position.Z) > 5.0f)
            {
                deltaPosition = position;
            }

        }

        public void StepInterpolation(double gameTime)
        {
            // We have 10 packets, so interpolate from the second to last to the last.
            if (interpList.Count == 10)
            {
                Vector3 a = interpList[8].position, b = interpList[9].position;
                double ta = interpList[8].gameTime, tb = interpList[9].gameTime;
                Vector3 d = b - a;
                double timeScale = (interpList[9].gameTime - interpList[0].gameTime) / 9;
                double timeAmount = Math.Min((gameTime - ta) / timeScale, 1);
                Position = a + d * (float)timeAmount;
            }
        }

        private PlayerTeam team = PlayerTeam.None;
        public PlayerTeam Team
        {
            get { return team; }
            set
            {
                if (value != team)
                {
                    team = value;
                    UpdateSpriteTexture();
                }
            }
        }
        private PlayerTools tool = PlayerTools.Pickaxe;
        public PlayerTools Tool
        {
            get { return tool; }
            set
            {
                if (value != tool)
                {
                    tool = value;
                    UpdateSpriteTexture();
                }
            }
        }
        private bool usingTool = false;
        public bool UsingTool
        {
            get { return usingTool; }
            set
            {
                if (value != usingTool)
                {
                    usingTool = value;
                    if (usingTool == true && gameInstance != null)
                        SpriteModel.StartActiveAnimation("3,0.15");
                }
            }
        }
        public DateTime playerToolCooldown = DateTime.Now;
        public float GetToolCooldown(PlayerTools tool)//this is only the server sides cooldown list
        {
            switch (tool)
            {
                case PlayerTools.Pickaxe: return 0.5f;// 0.55f;
                case PlayerTools.Detonator: return 0.01f;
                case PlayerTools.Remote: return 0.01f;
                case PlayerTools.ConstructionGun: return 0.5f;
                case PlayerTools.DeconstructionGun: return 0.5f;
                case PlayerTools.ProspectingRadar: return 0.5f;
                case PlayerTools.ThrowBomb: return 0.07f;
                case PlayerTools.ThrowRope: return 0.07f;
                default: return 0;
            }
        }

        public Player(NetConnection netConn, Game gameInstance)
        {
            this.gameInstance = gameInstance;
            this.NetConn = netConn;
            this.ID = Player.GetUniqueId();

            if (netConn != null)
                this.IP = netConn.RemoteEndpoint.Address.ToString();

            for (int a = 0; a < 100; a++)
            {
                Content[a] = 0;
            }

            if (gameInstance != null)
            {
                this.SpriteModel = new SpriteModel(gameInstance, 4);
                UpdateSpriteTexture();
                this.IdleAnimation = true;
            }
        }

        private void UpdateSpriteTexture()
        {
            if (gameInstance == null)
                return;

            string textureName = "sprites/tex_sprite_";
            if (team == PlayerTeam.Red)
                textureName += "red_";
            else
                textureName += "blue_";
            switch (tool)
            {
                case PlayerTools.ConstructionGun:
                case PlayerTools.DeconstructionGun:
                    textureName += "construction";
                    break;
                case PlayerTools.Detonator:
                    textureName += "detonator";
                    break;
                case PlayerTools.Remote:
                    textureName += "detonator";
                    break;
                case PlayerTools.Pickaxe:
                    textureName += "pickaxe";
                    break;
                case PlayerTools.ProspectingRadar:
                    textureName += "radar";
                    break;
                default:
                    textureName += "pickaxe";
                    break;
            }
            Texture2D orig = gameInstance.Content.Load<Texture2D>(textureName);
            if (AltColours)// && ((team == PlayerTeam.Blue && blueTeam != Defines.IM_BLUE) || (team == PlayerTeam.Red && redTeam != Defines.IM_RED)))
            {
                Color[] data = new Color[orig.Width * orig.Height];
                orig.GetData<Color>(data);
                Texture2D temp = new Texture2D(orig.GraphicsDevice,orig.Width,orig.Height);
                temp.SetData<Color>(data);
                Defines.generateShadedTexture(team == PlayerTeam.Blue ? blueTeam : redTeam, orig, ref temp);
                Console.WriteLine("Team: " + team.ToString() + "; Red col: " + redTeam.ToString() + "; Blue col: " + blueTeam.ToString());
                this.SpriteModel.SetSpriteTexture(temp);
            }
            else
                this.SpriteModel.SetSpriteTexture(orig);
        }

        static uint uniqueId = 0;
        public static uint GetUniqueId()
        {
            uniqueId += 1;
            return uniqueId;
        }
    }
}
