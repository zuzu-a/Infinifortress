using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Lidgren.Network;
using Lidgren.Network.Xna;
using System.IO;

namespace Infiniminer
{
    public class PendingPlayerMessage
    {
        public uint PlayerId;
        public PlayerClass PlayerClass;
    }

    public class PropertyBag
    {
        // Game engines.
        public BlockEngine blockEngine = null;
        public InterfaceEngine interfaceEngine = null;
        public PlayerEngine playerEngine = null;
        public SkyplaneEngine skyplaneEngine = null;
        public ParticleEngine particleEngine = null;
        
        public bool Challenge = false;
        // Network stuff.
        public NetClient netClient = null;
        public Dictionary<uint, Player> playerList = new Dictionary<uint, Player>();
        public bool[,] mapLoadProgress = null;
        public string serverName = "";
        public int MAPSIZE = 64;
        public Int32[] StatusEffect = new Int32[20];
        public DateTime retrigger;//prevents constant triggering of interactives
        public float colorPulse = 1.0f;//color fading
        bool colorDirection = true;//increases when true
        //Input stuff.
        public KeyBindHandler keyBinds = null;
        // Player variables.
        public Int32[,] artifactActive = null;
        public Camera playerCamera = null;
        public Vector3 playerPosition = Vector3.Zero;
        public Vector3 playerVelocity = Vector3.Zero;
        public Vector3 moveVector = Vector3.Zero;
        public Vector3 lastPosition = Vector3.Zero;
        public Vector3 lastHeading = Vector3.Zero;
        public PlayerClass playerClass = PlayerClass.None;
        public PlayerTools[] playerTools = new PlayerTools[1] { PlayerTools.Pickaxe };
        public int playerToolSelected = 0;
        public BlockType[] playerBlocks = new BlockType[1] { BlockType.None };
        public ItemType[] playerItems = new ItemType[1] { ItemType.None };
        public int playerBlockSelected = 0;
        public PlayerTeam playerTeam = PlayerTeam.None;//Red;
        public PlayerTeam playerTeamWanted = PlayerTeam.None;//Red;
        public bool playerDead = true;
        public bool allowRespawn = false;
        public uint playerOre = 0;
        public DateTime connectedTimer = DateTime.Now + TimeSpan.FromSeconds(5);
        public DateTime oreWarning = DateTime.Now;
        public uint playerHealth = 0;
        public uint playerHealthMax = 0;
        public uint playerCash = 0;
        public uint playerWeight = 0;
        public uint playerOreMax = 0;
        public DateTime blockPickup = DateTime.Now;
        public DateTime blockInteract = DateTime.Now;
        public int temperature = 0;
        public Vector3 forceVector = Vector3.Zero;
        public float forceStrength = 0.0f;
        public AudioListener listenPos = new AudioListener();
        public int[] Content = new Int32[50];
        public BlockType interact = BlockType.None;
        public uint playerWeightMax = 0;
        public float playerHoldBreath = 20;
        public DateTime lastBreath = DateTime.Now;
        public bool playerRadarMute = false;
        public float playerToolCooldown = 0;
        public string playerHandle = "Player";
        public string serverListURL = "http://zuzu-is.online";
        public float volumeLevel = 1.0f;
        public uint playerMyId = 0;
        public float radarCooldown = 0;
        public float radarDistance = 0;
        public float radarValue = 0;
        public float constructionGunAnimation = 0;

        public float mouseSensitivity = 0.005f;

        // Team variables.
        public uint teamArtifactsBlue = 0;
        public uint teamArtifactsRed = 0;
        public uint teamOre = 0;
        public uint teamRedCash = 0;
        public uint teamBlueCash = 0;
        public uint winningCashAmount = 6;
        public PlayerTeam teamWinners = PlayerTeam.None;
        public Dictionary<Vector3, Beacon> beaconList = new Dictionary<Vector3, Beacon>();
        public Dictionary<uint, Item> itemList = new Dictionary<uint, Item>();

        // Screen effect stuff.
        public Random randGen = new Random();
        public ScreenEffect screenEffect = ScreenEffect.None;
        public double screenEffectCounter = 0;

        //Team colour stuff
        public bool customColours = false;
        public Color red = Defines.IM_RED;
        public Color blue = Defines.IM_BLUE;
        public string redName = "Red";
        public string blueName = "Blue";

        // Sound stuff.
        public Dictionary<InfiniminerSound, SoundEffect> soundList = new Dictionary<InfiniminerSound, SoundEffect>();
        public Dictionary<int, SoundEffectInstance> soundListInstance = new Dictionary<int, SoundEffectInstance>();
        public Dictionary<int, AudioEmitter> soundListEmitter = new Dictionary<int, AudioEmitter>();

        // Chat stuff.
        public ChatMessageType chatMode = ChatMessageType.None;
        public int chatMaxBuffer = 5;
        public List<ChatMessage> chatBuffer = new List<ChatMessage>(); // chatBuffer[0] is most recent
        public List<ChatMessage> chatFullBuffer = new List<ChatMessage>(); //same as above, holds last several messages
        public string chatEntryBuffer = "";

        // Queue for messages that arrive before PlayerJoined
        public Queue<PendingPlayerMessage> pendingPlayerMessages = new Queue<PendingPlayerMessage>();

        public PropertyBag(InfiniminerGame gameInstance)
        {
            // Initialize our network device.
            NetConfiguration netConfig = new NetConfiguration("InfiniminerPlus");

            netClient = new NetClient(netConfig);
            netClient.SetMessageTypeEnabled(NetMessageType.ConnectionRejected, true);

            //netClient.SimulatedMinimumLatency = 0.5f;
            //netClient.SimulatedLatencyVariance = 0.1f;
            //netClient.SimulatedLoss = 0.05f;
            //netClient.SimulatedDuplicates = 0.02f;

            for (int a = 0; a < 50; a++)
            {
                Content[a] = 0;
            }
            artifactActive = new int[3, 30];

            netClient.Start();

            // Initialize engines.
            blockEngine = new BlockEngine(gameInstance);
            interfaceEngine = new InterfaceEngine(gameInstance);
            playerEngine = new PlayerEngine(gameInstance);
            skyplaneEngine = new SkyplaneEngine(gameInstance);
            particleEngine = new ParticleEngine(gameInstance);
            // Create a camera.
            playerCamera = new Camera(gameInstance.GraphicsDevice);

            UpdateCamera();

            // Load sounds.
            if (!gameInstance.NoSound)
            {
                soundList[InfiniminerSound.DigDirt] = gameInstance.Content.Load<SoundEffect>("sounds/dig-dirt");
                soundList[InfiniminerSound.DigMetal] = gameInstance.Content.Load<SoundEffect>("sounds/dig-metal");
                soundList[InfiniminerSound.Ping] = gameInstance.Content.Load<SoundEffect>("sounds/ping");
                soundList[InfiniminerSound.ConstructionGun] = gameInstance.Content.Load<SoundEffect>("sounds/build");
                soundList[InfiniminerSound.Death] = gameInstance.Content.Load<SoundEffect>("sounds/death");
                soundList[InfiniminerSound.CashDeposit] = gameInstance.Content.Load<SoundEffect>("sounds/cash");
                soundList[InfiniminerSound.ClickHigh] = gameInstance.Content.Load<SoundEffect>("sounds/click-loud");
                soundList[InfiniminerSound.ClickLow] = gameInstance.Content.Load<SoundEffect>("sounds/click-quiet");
                soundList[InfiniminerSound.GroundHit] = gameInstance.Content.Load<SoundEffect>("sounds/hitground");
                soundList[InfiniminerSound.Teleporter] = gameInstance.Content.Load<SoundEffect>("sounds/teleport");
                soundList[InfiniminerSound.Jumpblock] = gameInstance.Content.Load<SoundEffect>("sounds/jumpblock");
                soundList[InfiniminerSound.Explosion] = gameInstance.Content.Load<SoundEffect>("sounds/explosion");
                soundList[InfiniminerSound.RadarHigh] = gameInstance.Content.Load<SoundEffect>("sounds/radar-high");
                soundList[InfiniminerSound.RadarLow] = gameInstance.Content.Load<SoundEffect>("sounds/radar-low");
                soundList[InfiniminerSound.RadarSwitch] = gameInstance.Content.Load<SoundEffect>("sounds/switch");
                soundList[InfiniminerSound.RockFall] = gameInstance.Content.Load<SoundEffect>("sounds/rockfall");
                soundList[InfiniminerSound.Slap] = gameInstance.Content.Load<SoundEffect>("sounds/slap");
            }
        }

        public PlayerTeam TeamFromBlock(BlockType bt)
        {
            switch (bt)
            {
                case BlockType.TransBlue:
                case BlockType.SolidBlue:
                case BlockType.SolidBlue2:
                case BlockType.BeaconBlue:
                case BlockType.BankBlue:
                case BlockType.BaseBlue:
                case BlockType.StealthBlockB:
                case BlockType.TrapB:
                case BlockType.RadarBlue:
                case BlockType.ArtCaseB:
                case BlockType.GlassR:
                case BlockType.ConstructionB:
                case BlockType.InhibitorB:
                case BlockType.MedicalB:
                case BlockType.ResearchB:
                    return PlayerTeam.Blue;
                case BlockType.ConstructionR:
                case BlockType.GlassB:
                case BlockType.ArtCaseR:
                case BlockType.TransRed:
                case BlockType.SolidRed:
                case BlockType.SolidRed2:
                case BlockType.BaseRed:
                case BlockType.BeaconRed:
                case BlockType.BankRed:
                case BlockType.StealthBlockR:
                case BlockType.TrapR:
                case BlockType.RadarRed:
                case BlockType.InhibitorR:
                case BlockType.MedicalR:
                case BlockType.ResearchR:
                    return PlayerTeam.Red;
                default:
                    return PlayerTeam.None;
            }
        }

        public void SaveMap()
        {
            string filename = "saved_" + serverName.Replace(" ","") + "_" + (UInt64)DateTime.Now.ToBinary() + ".lvl";
            
            FileStream fs = new FileStream(filename, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine(MAPSIZE);//MAPSIZE
            sw.WriteLine(true);//clientside save
            for (int x = 0; x < MAPSIZE; x++)
                for (int y = 0; y < MAPSIZE; y++)
                    for (int z = 0; z < MAPSIZE; z++)
                        sw.WriteLine((byte)blockEngine.blockList[x, y, z] + "," + (byte)TeamFromBlock(blockEngine.blockList[x, y, z]));//(byte)blockEngine.blockCreatorTeam[x, y, z]);
            sw.Close();
            fs.Close();
            addChatMessage("Map saved to " + filename, ChatMessageType.SayAll, 10f);//DateTime.Now.ToUniversalTime());
        }
        public void ChallengeHost()
        {
            if (netClient.Status == NetConnectionStatus.Connected)
            {
                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.Challenge);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
            }

            //UploadMap();
            return;
        }
        public bool UploadMap()
        {
            int bcount = 0;
            int cost = 0;

            try
            {

                if (!File.Exists("fort.lvl"))
                {
                   // ConsoleWrite("Unable to load our fort, we must save one in build phase first!");
                    return false;
                }

                FileStream fs = new FileStream("fort.lvl", FileMode.Open);
                StreamReader sr = new StreamReader(fs);

                MAPSIZE = int.Parse(sr.ReadLine());
                bool clientsave = bool.Parse(sr.ReadLine());//is this saved from a client rather than server? (lacking all information apart from block geo)

               // addChatMessage("a " + clientsave + " size:" + MAPSIZE, ChatMessageType.SayAll, 10);

                for (int x = 0; x < MAPSIZE; x++)
                    for (int y = 0; y < MAPSIZE; y++)
                        for (int z = 0; z < MAPSIZE; z++)
                        {
                            string line = sr.ReadLine();
                            blockEngine.downloadList[x, y, z] = (BlockType)int.Parse(line, System.Globalization.CultureInfo.InvariantCulture);
                            bcount++;
                            //if (blockEngine.downloadList[x, y, z] != BlockType.None)
                            //{
                            //    addChatMessage("b " + blockEngine.downloadList[x, y, z], ChatMessageType.SayAll, 10);
                            //}
                            cost += (int)BlockInformation.GetCost(blockEngine.downloadList[x, y, z]);
                        }

                sr.Close();
                fs.Close();

                //addChatMessage("Block cost of our fortress:" + cost + " " + bcount, ChatMessageType.SayAll, 10);
            }
            catch
            {
                return false;
            }

            bcount = 0;
            for (byte x = 0; x < MAPSIZE; x++)
                for (byte y = 0; y < MAPSIZE; y += 16)
                {
                    NetBuffer msgBuffer = netClient.CreateBuffer();
                    msgBuffer.Write((byte)Infiniminer.InfiniminerMessage.BlockBulkTransfer);
                    //Compress the data so we don't use as much bandwith - Xeio's work
                    var compressedstream = new System.IO.MemoryStream();
                    var uncompressed = new System.IO.MemoryStream();
                    var compresser = new System.IO.Compression.GZipStream(compressedstream, System.IO.Compression.CompressionMode.Compress);

                    //Send a byte indicating that yes, this is compressed
                    msgBuffer.Write((byte)255);

                    //Write everything we want to compress to the uncompressed stream
                    uncompressed.WriteByte(x);
                    uncompressed.WriteByte(y);

                    for (byte dy = 0; dy < 16; dy++)
                        for (byte z = 0; z < MAPSIZE; z++)
                        {
                            uncompressed.WriteByte((byte)(blockEngine.downloadList[x, y + dy, z]));
                            bcount++;
                        }

                    //Compress the input
                    compresser.Write(uncompressed.ToArray(), 0, (int)uncompressed.Length);
                    //infs.ConsoleWrite("Sending compressed map block, before: " + uncompressed.Length + ", after: " + compressedstream.Length);
                    compresser.Close();

                    //Send the compressed data
                    msgBuffer.Write(compressedstream.ToArray());
                    if (netClient.Status == NetConnectionStatus.Connected)
                        netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
                }
            //addChatMessage("bcount2 :" + bcount, ChatMessageType.SayAll, 10);
            return true;
        }

        public void GetItem(uint ID)
        {
            if (netClient.Status == NetConnectionStatus.Connected)
            {
                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.GetItem);
                msgBuffer.Write(playerPosition);//also sends player locational data for range check
                msgBuffer.Write(ID);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
            }
        }

        public void DropItem(uint ID)
        {
            if (netClient.Status == NetConnectionStatus.Connected)
            {
                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.DropItem);
                msgBuffer.Write(ID);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
            }
        }
        public void KillPlayer(DeathMessage deathMessage)
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            if (deathMessage == DeathMessage.deathByLava || deathMessage == DeathMessage.deathByMiss || deathMessage == DeathMessage.deathByCrush || deathMessage == DeathMessage.deathByDrown || deathMessage == DeathMessage.deathByElec || deathMessage == DeathMessage.deathByFall)
            {
                PlaySound(InfiniminerSound.Death);
                // playerPosition = new Vector3(randGen.Next(2, 62), 66, randGen.Next(2, 62));
                playerVelocity = Vector3.Zero;
                playerDead = true;
                allowRespawn = false;
                screenEffect = ScreenEffect.Death;
                screenEffectCounter = 0;

                Content[5] = 0;

                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerDead);
                msgBuffer.Write((byte)deathMessage);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
            }
            else
            {
                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerDead);
                msgBuffer.Write((byte)deathMessage);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
            }
           // msgBuffer = netClient.CreateBuffer();
           // msgBuffer.Write((byte)InfiniminerMessage.PlayerRespawn);
          //  netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void RespawnPlayer()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            if (connectedTimer > DateTime.Now)//too early to ask for respawns
                return;

            if (allowRespawn == false)
            {
                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerRespawn);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
                return;
            }

            for (int a = 0; a < 20; a++)
            {
                StatusEffect[a] = 0;
            }

            playerDead = false;

            // Zero out velocity and reset camera and screen effects.
            playerVelocity = Vector3.Zero;
            screenEffect = ScreenEffect.None;
            screenEffectCounter = 0;
            UpdateCamera();
            forceStrength = 0.0f;
            forceVector = Vector3.Zero;
            allowRespawn = false;
            // Tell the server we have respawned.
            NetBuffer msgBufferb = netClient.CreateBuffer();
            msgBufferb.Write((byte)InfiniminerMessage.PlayerAlive);
            netClient.SendMessage(msgBufferb, NetChannel.ReliableUnordered);
        }

        public void PlaySound(InfiniminerSound sound)
        {
            if (soundList.Count == 0)
                return;
            float pitch = 0.0f;

            pitch = (float)((randGen.NextDouble()) * 0.05);

            soundList[sound].Play(volumeLevel, pitch, 0.0f);
        }

        public void PlaySound(InfiniminerSound sound, Vector3 position)
        {
            if (soundList.Count == 0)
                return;

            float distance = (position - playerPosition).Length();
            //float volume = 1.0f;// Math.Max(0, 20 - (0)) / 10.0f * volumeLevel;

            if (distance > 24)//we cant hear this far
                return;
            AudioEmitter emitter = new AudioEmitter();
            emitter.Position = position;
            int ns = soundListInstance.Count;
            float pitch = 0.0f;

            if (sound == InfiniminerSound.RockFall)
            {
                pitch = (float)(randGen.NextDouble() * 0.5);
            }
            else
                pitch = (float)((randGen.NextDouble()) * 0.05);

            soundListInstance[ns] = soundList[sound].CreateInstance();
            soundListInstance[ns].Volume = volumeLevel;
            soundListInstance[ns].Pitch = pitch;
            soundListInstance[ns].Apply3D(listenPos, emitter);
            soundListInstance[ns].Play();
            soundListEmitter[ns] = emitter;
        }

        public void PlaySoundForEveryone(InfiniminerSound sound, Vector3 position)
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            // The PlaySound message can be used to instruct the server to have all clients play a directional sound.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlaySound);
            msgBuffer.Write((byte)sound);
            msgBuffer.Write(position);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);

            PlaySound(sound);//plays the sound locally
        }

        public void addChatMessage(string chatString, ChatMessageType chatType, float timestamp)
        {
            string[] text = chatString.Split(' ');
            string textFull = "";
            string textLine = "";
            int newlines = 0;

            float curWidth = 0;
            for (int i = 0; i < text.Length; i++)
            {//each(string part in text){
                string part = text[i];
                if (i != text.Length - 1)
                    part += ' '; //Correct for lost spaces
                float incr = interfaceEngine.uiFont.MeasureString(part).X;
                curWidth += incr;
                if (curWidth > 1024 - 64) //Assume default resolution, unfortunately
                {
                    if (textLine.IndexOf(' ') < 0)
                    {
                        curWidth = 0;
                        textFull = textFull + "\n" + textLine;
                        textLine = "";
                    }
                    else
                    {
                        curWidth = incr;
                        textFull = textFull + "\n" + textLine;
                        textLine = part;
                    }
                    newlines++;
                }
                else
                {
                    textLine = textLine + part;
                }
            }
            if (textLine != "")
            {
                textFull += "\n" + textLine;
                newlines++;
            }

            if (textFull == "")
                textFull = chatString;

            ChatMessage chatMsg = new ChatMessage(textFull, chatType, 10,newlines);
            
            chatBuffer.Insert(0, chatMsg);
            chatFullBuffer.Insert(0, chatMsg);
            PlaySound(InfiniminerSound.ClickLow);
        }

        //public void Teleport()
        //{
        //    float x = (float)randGen.NextDouble() * 74 - 5;
        //    float z = (float)randGen.NextDouble() * 74 - 5;
        //    //playerPosition = playerHomeBlock + new Vector3(0.5f, 3, 0.5f);
        //    playerPosition = new Vector3(x, 74, z);
        //    screenEffect = ScreenEffect.Teleport;
        //    screenEffectCounter = 0;
        //    UpdateCamera();
        //}

        // Version used during updates.
        public void UpdateCamera(GameTime gameTime)
        {
            if (gameTime != null)
            if (colorDirection)
            {
                colorPulse += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (colorPulse > 1.5f)
                    colorDirection = !colorDirection;
            }
            else
            {
                colorPulse -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (colorPulse < 0.5f)
                    colorDirection = !colorDirection;
            }

            // If we have a gameTime object, apply screen jitter.
            if (screenEffect == ScreenEffect.Explosion)
            {
                if (gameTime != null)
                {
                    screenEffectCounter += gameTime.ElapsedGameTime.TotalSeconds;
                    // For 0 to 2, shake the camera.
                    if (screenEffectCounter < 2)
                    {
                        Vector3 newPosition = playerPosition;
                        newPosition.X += (float)(2 - screenEffectCounter) * (float)(randGen.NextDouble() - 0.5) * 0.5f;
                        newPosition.Y += (float)(2 - screenEffectCounter) * (float)(randGen.NextDouble() - 0.5) * 0.5f;
                        newPosition.Z += (float)(2 - screenEffectCounter) * (float)(randGen.NextDouble() - 0.5) * 0.5f;
                        if (!blockEngine.SolidAtPointForPlayer(newPosition) && (newPosition - playerPosition).Length() < 0.7f)
                            playerCamera.Position = newPosition;
                    }
                    // For 2 to 3, move the camera back.
                    else if (screenEffectCounter < 3)
                    {
                        Vector3 lerpVector = playerPosition - playerCamera.Position;
                        playerCamera.Position += 0.5f * lerpVector;
                    }
                    else
                    {
                        screenEffect = ScreenEffect.None;
                        screenEffectCounter = 0;
                        playerCamera.Position = playerPosition;
                    }
                }
            }
            if (screenEffect == ScreenEffect.Earthquake)
            {
                if (gameTime != null)
                {
                    screenEffectCounter += gameTime.ElapsedGameTime.TotalSeconds;
                    // For 0 to 2, shake the camera.
                    if (screenEffectCounter < 2)
                    {
                        Vector3 newPosition = playerPosition;
                        newPosition.X += (float)(2 - screenEffectCounter) * (float)(randGen.NextDouble() - 0.5) * 0.5f;
                        newPosition.Y += (float)(2 - screenEffectCounter) * (float)(randGen.NextDouble() - 0.5) * 0.5f;
                        newPosition.Z += (float)(2 - screenEffectCounter) * (float)(randGen.NextDouble() - 0.5) * 0.5f;
                        if (!blockEngine.SolidAtPointForPlayer(newPosition) && (newPosition - playerPosition).Length() < 0.7f)
                            playerCamera.Position = newPosition;
                    }
                    // For 2 to 3, move the camera back.
                    else if (screenEffectCounter < 3)
                    {
                        Vector3 lerpVector = playerPosition - playerCamera.Position;
                        playerCamera.Position += 0.5f * lerpVector;
                    }
                    else
                    {
                        screenEffect = ScreenEffect.None;
                        screenEffectCounter = 0;
                        playerCamera.Position = playerPosition;
                    }
                }
            }
            
                playerCamera.Position = playerPosition;
                listenPos.Position = playerPosition;
                listenPos.Up = playerCamera.GetUpVector();
                listenPos.Forward = playerCamera.GetLookVector();

                //go through all sounds and sort positionally
               
                foreach (KeyValuePair<int, SoundEffectInstance> bPair in soundListInstance)
                {
                    if (soundListEmitter.ContainsKey(bPair.Key))
                    if (!soundListInstance[bPair.Key].IsDisposed)
                    {
                        soundListInstance[bPair.Key].Apply3D(listenPos, soundListEmitter[bPair.Key]);
                        if (soundListInstance[bPair.Key].State == SoundState.Stopped)
                        {
                            soundListInstance[bPair.Key].Dispose();
                            soundListEmitter.Remove(bPair.Key);                          
                        }
                    }
                }

                foreach (KeyValuePair<int, SoundEffectInstance> bPair in soundListInstance)
                {
                    if (soundListInstance[bPair.Key].IsDisposed == true)
                    {
                        soundListInstance.Remove(bPair.Key);
                        break;
                    }
                }

            playerCamera.Update();
        }

        public void UpdateCamera()
        {
            UpdateCamera(null);
        }

        public void DepositLoot()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.DepositCash);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void DepositOre()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.DepositOre);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void WithdrawOre()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.WithdrawOre);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void SetPlayerTeam(PlayerTeam playerTeam)//will no longer force set your team, it must be done by the server
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            if (this.playerTeam != playerTeam)
            {

                //this.playerTeam = playerTeam;

                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerSetTeam);
                msgBuffer.Write((byte)playerTeam);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);

                //if (playerTeam == PlayerTeam.Red)
                //{
                //    this.KillPlayer(DeathMessage.deathByTeamSwitchRed); 
                //    blockEngine.blockTextures[(byte)BlockTexture.TrapR] = blockEngine.blockTextures[(byte)BlockTexture.TrapVis];
                //    blockEngine.blockTextures[(byte)BlockTexture.TrapB] = blockEngine.blockTextures[(byte)BlockTexture.Trap];
                //}
                //else
                //{
                //    this.KillPlayer(DeathMessage.deathByTeamSwitchBlue); 
                //    blockEngine.blockTextures[(byte)BlockTexture.TrapB] = blockEngine.blockTextures[(byte)BlockTexture.TrapVis];
                //    blockEngine.blockTextures[(byte)BlockTexture.TrapR] = blockEngine.blockTextures[(byte)BlockTexture.Trap];
                //}
            }
        }

        public bool allWeps = false; //Needs to be true on sandbox servers, though that requires a server mod

        public void equipWeps()
        {
            playerToolSelected = 0;
            playerBlockSelected = 0;

            if (allWeps)
            {
                playerTools = new PlayerTools[6] { PlayerTools.Pickaxe,
                PlayerTools.ConstructionGun,
                PlayerTools.DeconstructionGun,
                PlayerTools.ProspectingRadar,
                PlayerTools.ThrowBomb,
                PlayerTools.Detonator
                };

                playerBlocks = new BlockType[22] {   playerTeam == PlayerTeam.Red ? BlockType.SolidRed : BlockType.SolidBlue,
                                             playerTeam == PlayerTeam.Red ? BlockType.TransRed : BlockType.TransBlue,
                                             BlockType.Road,
                                             BlockType.Ladder,
                                             BlockType.Jump,
                                             BlockType.Shock,
                                             playerTeam == PlayerTeam.Red ? BlockType.ArtCaseR : BlockType.ArtCaseB,
                                             playerTeam == PlayerTeam.Red ? BlockType.BeaconRed : BlockType.BeaconBlue,
                                             playerTeam == PlayerTeam.Red ? BlockType.BankRed : BlockType.BankBlue,
                                             playerTeam == PlayerTeam.Red ? BlockType.StealthBlockR : BlockType.StealthBlockB,
                                             playerTeam == PlayerTeam.Red ? BlockType.TrapR : BlockType.TrapB,
                                             BlockType.Explosive,
                                             BlockType.Pipe, 
                                             BlockType.Hinge,
                                             BlockType.Metal,
                                             BlockType.Lever,
                                             BlockType.Plate,
                                             BlockType.Pump,
                                             BlockType.Barrel,
                                             playerTeam == PlayerTeam.Red ? BlockType.RadarRed : BlockType.RadarBlue,
                                             BlockType.Water,
                                             BlockType.GlassR };
            }
            else
            {
                switch (playerClass)
                {
                    case PlayerClass.Prospector:
                        playerTools = new PlayerTools[3] {  PlayerTools.Pickaxe,
                                                            PlayerTools.ConstructionGun,
                                                            PlayerTools.ProspectingRadar
                        };// PlayerTools.Hide };//.ThrowRope
                        playerItems = new ItemType[0] { };

                        playerBlocks = new BlockType[8] {   playerTeam == PlayerTeam.Red ? BlockType.SolidRed : BlockType.SolidBlue,
                                                            playerTeam == PlayerTeam.Red ? BlockType.BeaconRed : BlockType.BeaconBlue,
                                                            BlockType.Shock,
                                                            BlockType.Plate,
                                                            BlockType.Lever,
                                                            BlockType.Hinge,
                                                            playerTeam == PlayerTeam.Red ? BlockType.StealthBlockR : BlockType.StealthBlockB,
                                                            playerTeam == PlayerTeam.Red ? BlockType.TrapR : BlockType.TrapB
                                                        };
                        break;

                    case PlayerClass.Miner:
                        playerTools = new PlayerTools[2] {  PlayerTools.Pickaxe,
                                                            PlayerTools.ConstructionGun };
                        playerItems = new ItemType[0] { };// ItemType.None };

                        playerBlocks = new BlockType[7] {   playerTeam == PlayerTeam.Red ? BlockType.SolidRed : BlockType.SolidBlue,
                                                            playerTeam == PlayerTeam.Red ? BlockType.BeaconRed : BlockType.BeaconBlue,
                                                            BlockType.Shock,
                                                            BlockType.Plate,
                                                            BlockType.Lever,
                                                            BlockType.Hinge,
                                                            BlockType.Refinery};
                        break;

                    case PlayerClass.Engineer:
                        playerTools = new PlayerTools[4] {  PlayerTools.Pickaxe,
                                                            PlayerTools.ConstructionGun,     
                                                            PlayerTools.DeconstructionGun,
                                                            PlayerTools.Remote };

                        playerItems = new ItemType[1] { ItemType.Spikes };
                        playerBlocks = new BlockType[19] {   playerTeam == PlayerTeam.Red ? BlockType.SolidRed : BlockType.SolidBlue,
                                                        playerTeam == PlayerTeam.Red ? BlockType.RadarRed : BlockType.RadarBlue,
                                                        BlockType.Ladder,
                                                        BlockType.Barrel,
                                                        BlockType.Lever,
                                                        BlockType.Plate,
                                                        BlockType.Metal,
                                                        BlockType.Hinge,
                                                        BlockType.Refinery,
                                                        BlockType.Maintenance,
                                                        playerTeam == PlayerTeam.Red ? BlockType.MedicalR : BlockType.MedicalB,
                                                        playerTeam == PlayerTeam.Red ? BlockType.ResearchR : BlockType.ResearchB,
                                                        playerTeam == PlayerTeam.Red ? BlockType.ArtCaseR : BlockType.ArtCaseB,
                                                        playerTeam == PlayerTeam.Red ? BlockType.BeaconRed : BlockType.BeaconBlue,
                                                        playerTeam == PlayerTeam.Red ? BlockType.BankRed : BlockType.BankBlue,  
                                                        playerTeam == PlayerTeam.Red ? BlockType.InhibitorR : BlockType.InhibitorB,
                                                        playerTeam == PlayerTeam.Red ? BlockType.GlassR : BlockType.GlassB,
                                                        playerTeam == PlayerTeam.Red ? BlockType.StealthBlockR : BlockType.StealthBlockB,
                                                        playerTeam == PlayerTeam.Red ? BlockType.TrapR : BlockType.TrapB};
                        break;

                    case PlayerClass.Sapper:
                        playerTools = new PlayerTools[2] {  PlayerTools.Pickaxe,
                                                            PlayerTools.ConstructionGun };
                                                            //PlayerTools.Detonator };
                        playerItems = new ItemType[1] { ItemType.DirtBomb };
                        playerBlocks = new BlockType[7] {   BlockType.Explosive,
                                                            playerTeam == PlayerTeam.Red ? BlockType.SolidRed : BlockType.SolidBlue,
                                                            playerTeam == PlayerTeam.Red ? BlockType.BeaconRed : BlockType.BeaconBlue,
                                                            BlockType.Shock,
                                                            BlockType.Plate,
                                                            BlockType.Lever,
                                                            BlockType.Hinge};

                        break;
                }
            }

        }

        public void SetPlayerClass(PlayerClass playerClass)
        {
            if (this.playerClass != playerClass)
            {
                if (netClient.Status != NetConnectionStatus.Connected)
                {
                    return;
                }

                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.SelectClass);
                msgBuffer.Write((byte)playerClass);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
            }
            else
            {
            }
        }

        public void FireRadar()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;


            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;

            blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 30, 200, ref hitPoint, ref buildPoint, BlockType.Water);

            if (hitPoint == Vector3.Zero)
                return;

            playerToolCooldown = GetToolCooldown(PlayerTools.ProspectingRadar);

            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.ProspectingRadar);
            //msgBuffer.Write((byte)BlockType.None);

            if (radarValue == 200)
            {
                msgBuffer.Write((byte)1);
            }
            else if (radarValue == 1000)
            {
                msgBuffer.Write((byte)2);
            }
            else if (radarValue == 2000)
            {
                msgBuffer.Write((byte)3);
            }
            else
            {
                msgBuffer.Write((byte)0);
            }

            //msgBuffer.Write(hitPoint.X);
            //msgBuffer.Write(hitPoint.Y);
            //msgBuffer.Write(hitPoint.Z);

            msgBuffer.Write((int)(((hitPoint.X - playerCamera.GetLookVector().X/4)) * 100));
            msgBuffer.Write((int)(((hitPoint.Y - playerCamera.GetLookVector().Y/10)) * 100));
            msgBuffer.Write((int)(((hitPoint.Z - playerCamera.GetLookVector().Z/4)) * 100));

            float size = (radarDistance / 15);
            msgBuffer.Write((int)(size*100));
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void FirePickaxe()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            //play sound locally
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;

            bool dig = false;
            if(artifactActive[(byte)playerTeam,4] > 0 || Content[10] == 4)
                dig = blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 2, 10, ref hitPoint, ref buildPoint, BlockType.Water);
            else
                dig = blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 2, 10, ref hitPoint, ref buildPoint, BlockType.None);

            Vector3 attackVector = playerPosition + (playerCamera.GetLookVector()*0.8f);

            foreach (Player p in playerList.Values)
            {
                //medical artifact infects enemies
                if (p.ID != playerMyId && (p.Team != playerTeam || Content[10] == 8))
                    if ((attackVector - (p.deltaPosition - Vector3.UnitY * 0.2f)).Length() < 1.0f)
                    {
                        NetBuffer msgBuffer = netClient.CreateBuffer();
                        msgBuffer.Write((byte)InfiniminerMessage.PlayerSlap);
                        msgBuffer.Write(playerPosition);
                        msgBuffer.Write(playerCamera.GetLookVector());
                        msgBuffer.Write((byte)PlayerTools.Pickaxe);
                        msgBuffer.Write(p.ID);
                        netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);

                        playerToolCooldown = GetToolCooldown(PlayerTools.Pickaxe);
                        return;//dig = false;//allows you to hit multiple enemies.. if it wasnt for tool cooldown!
                    }
            }

            if (artifactActive[(byte)playerTeam, 4] == 0)
            {
                if (blockEngine.BlockAtPoint(playerPosition) == BlockType.Water)
                    dig = false;
            }
            if (dig == false)
                return;
            
            ushort x = (ushort)hitPoint.X;
            ushort y = (ushort)hitPoint.Y;
            ushort z = (ushort)hitPoint.Z;

            // Figure out what the result is.
            bool removeBlock = false;
            int Damage = 0;
            InfiniminerSound sound = InfiniminerSound.DigDirt;

            BlockType block = blockEngine.BlockAtPoint(hitPoint);

            //if(TeamFromBlock(block) !=
            Damage = BlockInformation.GetMaxHP(block) > 0 ? 2 : 0;

            switch (block)
            {
                case BlockType.Grass:
                case BlockType.Dirt:
                case BlockType.Mud:
                case BlockType.Sand:
                case BlockType.DirtSign:
                    removeBlock = true;
                    sound = InfiniminerSound.DigDirt;
                    break;
                case BlockType.StealthBlockB:
                    removeBlock = true;
                    sound = InfiniminerSound.DigDirt;
                    break;
                case BlockType.StealthBlockR:
                    removeBlock = true;
                    sound = InfiniminerSound.DigDirt;
                    break;
                case BlockType.TrapB:
                    removeBlock = true;
                    sound = InfiniminerSound.DigDirt;
                    break;
                case BlockType.TrapR:
                    removeBlock = true;
                    sound = InfiniminerSound.DigDirt;
                    break;
                case BlockType.Ore:
                    removeBlock = true;
                    sound = InfiniminerSound.DigMetal;
                    break;

                case BlockType.Gold:
                    //removeBlock = true;
                    sound = InfiniminerSound.RadarLow;
                    break;

                case BlockType.Diamond:
                    //removeBlock = true;
                    sound = InfiniminerSound.RadarHigh;
                    break;

                case BlockType.ResearchB:
                case BlockType.MedicalB:
                case BlockType.GlassB:
                case BlockType.ConstructionB:
                case BlockType.BeaconBlue:
                case BlockType.ArtCaseB:
                case BlockType.BankBlue:
                case BlockType.RadarBlue:
                case BlockType.InhibitorB:
                case BlockType.SolidBlue:
                case BlockType.SolidBlue2:
                    if (playerTeam == PlayerTeam.Red)
                        Damage = 10;
                    else if (playerOre > 0)
                        Damage = 10;
                    else//player has no ore
                        Damage = 0;
                    sound = InfiniminerSound.DigMetal;
                    break;

                case BlockType.ResearchR:
                case BlockType.MedicalR:
                case BlockType.GlassR:
                case BlockType.ConstructionR:
                case BlockType.BeaconRed:
                case BlockType.ArtCaseR:
                case BlockType.BankRed:
                case BlockType.RadarRed:
                case BlockType.InhibitorR:
                case BlockType.SolidRed:
                case BlockType.SolidRed2:
                    if (playerTeam == PlayerTeam.Blue)
                        Damage = 10;
                    else if (playerOre > 0)
                        Damage = 10;
                    else//player has no ore
                        Damage = 0;
                    sound = InfiniminerSound.DigMetal;
                    break;

                case BlockType.Lava:
                    if (StatusEffect[4] > 0)
                    {
                        Damage = 10;
                        sound = InfiniminerSound.DigDirt;
                    }
                    else
                    {
                        Damage = 1;
                    }
                    break;
                case BlockType.Water:
                    if (StatusEffect[4] > 0)
                    {
                        Damage = 10;
                        sound = InfiniminerSound.DigDirt;
                    }
                    break;
            }

            if (removeBlock == true)
            {
                playerToolCooldown = GetToolCooldown(PlayerTools.Pickaxe);

                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.UseTool);
                msgBuffer.Write(playerPosition);
                msgBuffer.Write(playerCamera.GetLookVector());
                msgBuffer.Write((byte)PlayerTools.Pickaxe);
                msgBuffer.Write((byte)BlockType.None);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableInOrder1);
                blockEngine.RemoveBlock(x,y,z);//local block removal
                particleEngine.CreateDiggingDebris(hitPoint - (playerCamera.GetLookVector()*0.3f),block);
                particleEngine.CreateBlockDebris(new Vector3(x+0.5f,y+0.5f,z+0.5f), block, 10.0f);
                PlaySound(sound);//local sound effect
            }
            else if(Damage > 0)
            {
                playerToolCooldown = GetToolCooldown(PlayerTools.Pickaxe);

                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.UseTool);
                msgBuffer.Write(playerPosition);
                msgBuffer.Write(playerCamera.GetLookVector());
                msgBuffer.Write((byte)PlayerTools.Pickaxe);
                msgBuffer.Write((byte)BlockType.None);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
                particleEngine.CreateDiggingDebris(hitPoint - (playerCamera.GetLookVector() * 0.3f), block);
                PlaySound(sound);//local sound effect
            }
            else//it doesnt play the sound effect, but will still try send server ray for lag compensation
            {
                //playerToolCooldown = GetToolCooldown(PlayerTools.Pickaxe);
                //NetBuffer msgBuffer = netClient.CreateBuffer();
                //msgBuffer.Write((byte)InfiniminerMessage.UseTool);
                //msgBuffer.Write(playerPosition);
                //msgBuffer.Write(playerCamera.GetLookVector());
                //msgBuffer.Write((byte)PlayerTools.Pickaxe);
                //msgBuffer.Write((byte)BlockType.None);
                //netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
            }
        }

        public void FireConstructionGun(BlockType blockType)
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            if (StatusEffect[1] > 0)
            {
                addChatMessage("An inhibitor is preventing construction here!", ChatMessageType.SayAll, 10);
                return;
            }

            playerToolCooldown = GetToolCooldown(PlayerTools.ConstructionGun);
            constructionGunAnimation = -5;

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.ConstructionGun);
            msgBuffer.Write((byte)blockType);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void FireItemGun(int blockType)
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            if (StatusEffect[1] > 0)
            {
                addChatMessage("An inhibitor is preventing construction here!", ChatMessageType.SayAll, 10);
                return;
            }

            playerToolCooldown = GetToolCooldown(PlayerTools.ConstructionGun);
            constructionGunAnimation = -5;

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.ConstructItem);
            msgBuffer.Write((byte)blockType);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void StrongArm()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            playerToolCooldown = GetToolCooldown(PlayerTools.StrongArm);//should have its own
            constructionGunAnimation = -5;

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.StrongArm);
            msgBuffer.Write(true);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void Smash()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            playerToolCooldown = GetToolCooldown(PlayerTools.ConstructionGun);//should have its own
            constructionGunAnimation = -5;

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.Smash);
            msgBuffer.Write(true);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }
        public void SmashDig()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            //play sound locally
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            Vector3 smashVector = new Vector3((float)(Content[6]) / 1000, (float)(Content[7]) / 1000, (float)(Content[8]) / 1000);
            if (!blockEngine.RayCollision(playerPosition, smashVector, 2, 10, ref hitPoint, ref buildPoint, BlockType.Water))
                return;

            ushort x = (ushort)hitPoint.X;
            ushort y = (ushort)hitPoint.Y;
            ushort z = (ushort)hitPoint.Z;

            // Figure out what the result is.
            bool removeBlock = true;

            InfiniminerSound sound = InfiniminerSound.DigDirt;

            if (removeBlock == true)
            {
                playerToolCooldown = GetToolCooldown(PlayerTools.Pickaxe);

                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.UseTool);
                msgBuffer.Write(playerPosition);
                msgBuffer.Write(smashVector);
                msgBuffer.Write((byte)PlayerTools.Pickaxe);
                msgBuffer.Write((byte)BlockType.None);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
                blockEngine.RemoveBlock(x, y, z);//local block removal//needs a sync check
                PlaySound(sound);//local sound effect
            }
            else//it doesnt play the sound effect, but will still try send server ray for lag compensation
            {
                //playerToolCooldown = GetToolCooldown(PlayerTools.Pickaxe);
                //NetBuffer msgBuffer = netClient.CreateBuffer();
                //msgBuffer.Write((byte)InfiniminerMessage.UseTool);
                //msgBuffer.Write(playerPosition);
                //msgBuffer.Write(playerCamera.GetLookVector());
                //msgBuffer.Write((byte)PlayerTools.Pickaxe);
                //msgBuffer.Write((byte)BlockType.None);
                //netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
            }
        }
        public void FireBomb()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            //playerToolCooldown = GetToolCooldown(PlayerTools.SpawnItem);
            //constructionGunAnimation = -5;

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.ThrowBomb);
            msgBuffer.Write((byte)BlockType.None);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void FireRope()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            //playerToolCooldown = GetToolCooldown(PlayerTools.SpawnItem);
            //constructionGunAnimation = -5;

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.ThrowRope);
            msgBuffer.Write((byte)BlockType.None);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void Hide()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            //playerToolCooldown = GetToolCooldown(PlayerTools.SpawnItem);
            //constructionGunAnimation = -5;

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.Hide);
            msgBuffer.Write((byte)BlockType.None);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }
        public void FireDeconstructionGun()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            playerToolCooldown = GetToolCooldown(PlayerTools.DeconstructionGun);
            constructionGunAnimation = -5;

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.DeconstructionGun);
            msgBuffer.Write((byte)BlockType.None);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void FireDetonator()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            playerToolCooldown = GetToolCooldown(PlayerTools.Detonator);

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.Detonator);
            msgBuffer.Write((byte)BlockType.None);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void FireRemote()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            playerToolCooldown = GetToolCooldown(PlayerTools.Remote);

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.Remote);
            msgBuffer.Write((byte)BlockType.None);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }

        public void FireSetRemote()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            playerToolCooldown = GetToolCooldown(PlayerTools.SetRemote);

            // Send the message.
            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.UseTool);
            msgBuffer.Write(playerPosition);
            msgBuffer.Write(playerCamera.GetLookVector());
            msgBuffer.Write((byte)PlayerTools.SetRemote);
            msgBuffer.Write((byte)BlockType.None);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }
        public void ToggleRadar()
        {
            playerRadarMute = !playerRadarMute;
            PlaySound(InfiniminerSound.RadarSwitch);
        }

        public void ReadRadar(ref float distanceReading, ref float valueReading)
        {
            valueReading = 0;
            distanceReading = 30;

            // Scan out along the camera axis for 30 meters.
            for (int i = -3; i <= 3; i++)
                for (int j = -3; j <= 3; j++)
                {
                    Matrix rotation = Matrix.CreateRotationX((float)(i * Math.PI / 128)) * Matrix.CreateRotationY((float)(j * Math.PI / 128));
                    Vector3 scanPoint = playerPosition;
                    Vector3 lookVector = Vector3.Transform(playerCamera.GetLookVector(), rotation);
                    for (int k = 0; k < 60; k++)
                    {
                        BlockType blockType = blockEngine.BlockAtPoint(scanPoint);
                        if (blockType == BlockType.Gold)
                        {
                            distanceReading = Math.Min(distanceReading, 0.5f * k);
                            valueReading = Math.Max(valueReading, 200);
                        }
                        else if (blockType == BlockType.ArtCaseR || blockType == BlockType.ArtCaseB)
                        {
                            distanceReading = Math.Min(distanceReading, 0.5f * k);
                            valueReading = Math.Max(valueReading, 2000);
                        }
                        else if (blockType == BlockType.Diamond)
                        {
                            distanceReading = Math.Min(distanceReading, 0.5f * k);
                            valueReading = Math.Max(valueReading, 1000);
                        }
                        scanPoint += 0.5f * lookVector;
                    }
                }
        }

        public string strInteract()
        {
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            interact = BlockType.None;

            if (!blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 2.5f, 50, ref hitPoint, ref buildPoint))
            {
                return "";
            }
           
            // If it's a valid bank object, we're good!
            BlockType blockType = blockEngine.BlockAtPoint(hitPoint);

            BlockType b1 = blockEngine.BlockAtPoint(hitPoint + Vector3.UnitX * 0.1f);
            BlockType b2 = blockEngine.BlockAtPoint(hitPoint - Vector3.UnitX * 0.1f);
            BlockType b3 = blockEngine.BlockAtPoint(hitPoint + Vector3.UnitY * 0.1f);
            BlockType b4 = blockEngine.BlockAtPoint(hitPoint - Vector3.UnitY * 0.1f);
            BlockType b5 = blockEngine.BlockAtPoint(hitPoint + Vector3.UnitZ * 0.1f);
            BlockType b6 = blockEngine.BlockAtPoint(hitPoint - Vector3.UnitZ * 0.1f);

            if (b1 != BlockType.None && b1 != BlockType.Water)
                if (blockType != b1)
                    return "";

            if (b2 != BlockType.None && b2 != BlockType.Water)
                if (blockType != b2)
                    return "";

            if (b3 != BlockType.None && b3 != BlockType.Water)
                if (blockType != b3)
                    return "";

            if (b4 != BlockType.None && b4 != BlockType.Water)
                if (blockType != b4)
                    return "";

            if (b5 != BlockType.None && b5 != BlockType.Water)
                if (blockType != b5)
                    return "";

            if (b6 != BlockType.None && b6 != BlockType.Water)
                if (blockType != b6)
                    return "";

            if (blockType == BlockType.BankRed && playerTeam == PlayerTeam.Red)
            {
                interact = blockType;
                return "1: Deposit ore  2: Withdraw ore";
            }
            else if (blockType == BlockType.MedicalR && playerTeam == PlayerTeam.Red)
            {
                interact = blockType;
                return "1: Treatment 2: Status";
            }
            else if (blockType == BlockType.MedicalB && playerTeam == PlayerTeam.Blue)
            {
                interact = blockType;
                return "1: Treatment 2: Status";
            }
            else if (blockType == BlockType.Explosive)
            {

                if (playerClass == PlayerClass.Sapper)
                {
                    interact = blockType;
                    return "1: Fuse";
                }
                else
                    return "";
            }
            else if (blockType == BlockType.Refinery)
            {
               // interact = blockType;
                //return "1: REFINE DIAMOND";
            }
            else if (blockType == BlockType.BeaconRed && playerTeam == PlayerTeam.Red)
            {
                // interact = blockType;
                return "Use /rename to change beacon label";
            }
            else if (blockType == BlockType.BeaconBlue && playerTeam == PlayerTeam.Blue)
            {
                // interact = blockType;
                return "Use /rename to change beacon label";
            }
            else if (blockType == BlockType.ResearchR && playerTeam == PlayerTeam.Red)
            {
                interact = blockType;
                return "1: Toggle 2: Change topic 3: Status";
            }
            else if (blockType == BlockType.ResearchB && playerTeam == PlayerTeam.Blue)
            {
                interact = blockType;
                return "1: Toggle 2: Change topic 3: Status";
            }
            else if (blockType == BlockType.BaseRed && playerTeam == PlayerTeam.Red)
            {
                interact = blockType;
                if (Content[11] > 0)
                {
                    return "1: Forge artifact with powerstone and gold";
                }
                else
                {
                    return "1: Requires a powerstone 2: Pause construction";
                }
            }
            else if (blockType == BlockType.BaseBlue && playerTeam == PlayerTeam.Blue)
            {
                interact = blockType;
                if (Content[11] > 0)
                {
                    return "1: Forge artifact with powerstone and gold";
                }
                else
                {
                    return "1: Requires a powerstone 2: Pause construction";
                }
            }
            else if (blockType == BlockType.ArtCaseR && playerTeam == PlayerTeam.Red)
            {
                interact = blockType;
                return "1: Place artifact  2: Retrieve artifact 5: Lock permanently";
            }
            else if (blockType == BlockType.ArtCaseB && playerTeam == PlayerTeam.Blue)
            {
                interact = blockType;
                return "1: Place artifact  2: Retrieve artifact 5: Lock permanently";
            }
            else if (blockType == BlockType.BankBlue && playerTeam == PlayerTeam.Blue)
            {
                interact = blockType;
                return "1: Deposit ore  2: Withdraw ore";
            }
            else if (blockType == BlockType.Generator)
            {
                interact = blockType;
                return "1: Generator On  2: Generator Off";
            }
            else if (blockType == BlockType.Pipe)
            {
                interact = blockType;
                return "1: Rotate Left 2: Rotate Right";
            }
            else if (blockType == BlockType.Pump)
            {
                interact = blockType;
                return "1: On/Off 2: Change direction";
            }
            else if (blockType == BlockType.Barrel)
            {
                interact = blockType;
                return "1: Fill/Empty 2: Status";
            }
            else if (blockType == BlockType.Hinge)
            {
                interact = blockType;
                return "1: Activate 2: Rotate/Connect 3: Upwards/Downwards";
            }
            else if (blockType == BlockType.Lever)
            {
                interact = blockType;
                return "1: Activate 2: Link 3: Decrease timer 4: Increase timer";
            }
            else if (blockType == BlockType.ConstructionR && playerTeam == PlayerTeam.Red)
            {
                interact = blockType;
                return "1: Status";
            }
            else if (blockType == BlockType.ConstructionB && playerTeam == PlayerTeam.Blue)
            {
                interact = blockType;
                return "1: Status";
            }
            else if (blockType == BlockType.Plate)
            {
                interact = blockType;
                if(blockEngine.blockTexList[(int)hitPoint.X, (int)hitPoint.Y, (int)hitPoint.Z] == blockEngine.blockList[(int)hitPoint.X, (int)hitPoint.Y, (int)hitPoint.Z])
                    return "1: Activate 2: Link 3: Decrease timer 4: Increase timer 5: Hide plate";
            }
            return "";
        }
        public BlockType Interact()
        {
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 2.5f, 25, ref hitPoint, ref buildPoint))
                return BlockType.None;

            // If it's a valid bank object, we're good!
            BlockType blockType = blockEngine.BlockAtPoint(hitPoint);
            return blockType;
        }
        public bool PlayerInteract(int button)
        {
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 2.5f, 25, ref hitPoint, ref buildPoint))
                return false;

            //press button on server
            SendPlayerInteract(button, (uint)hitPoint.X, (uint)hitPoint.Y, (uint)hitPoint.Z);
            return true;
        }
        public bool AtGenerator()
        {
            // Figure out what we're looking at.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 2.5f, 25, ref hitPoint, ref buildPoint))
                return false;

            // If it's a valid bank object, we're good!
            BlockType blockType = blockEngine.BlockAtPoint(hitPoint);
            if (blockType == BlockType.Generator)
                return true;
            return false;
        }

        public bool AtPipe()
        {
            // Figure out what we're looking at.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 2.5f, 25, ref hitPoint, ref buildPoint))
                return false;

            // If it's a valid bank object, we're good!
            BlockType blockType = blockEngine.BlockAtPoint(hitPoint);
            if (blockType == BlockType.Pipe)
                return true;
            return false;
        }

        // Returns true if the player is able to use a bank right now.
        public bool AtBankTerminal()
        {
            // Figure out what we're looking at.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!blockEngine.RayCollision(playerPosition, playerCamera.GetLookVector(), 2.5f, 25, ref hitPoint, ref buildPoint))
                return false;

            // If it's a valid bank object, we're good!
            BlockType blockType = blockEngine.BlockAtPoint(hitPoint);
            if (blockType == BlockType.BankRed && playerTeam == PlayerTeam.Red)
                return true;
            if (blockType == BlockType.BankBlue && playerTeam == PlayerTeam.Blue)
                return true;
            return false;
        }

        public float GetToolCooldown(PlayerTools tool)
        {
            switch (tool)
            {
                case PlayerTools.Pickaxe: return 0.5f;//0.5f 0.55f;
                case PlayerTools.Detonator: return 0.1f;
                case PlayerTools.Remote: return 0.01f;
                case PlayerTools.ConstructionGun: return 0.5f;
                case PlayerTools.DeconstructionGun: return 0.5f;
                case PlayerTools.ProspectingRadar: return 2.0f;
                case PlayerTools.ThrowBomb: return 0.1f;
                case PlayerTools.ThrowRope: return 0.6f;
                case PlayerTools.Hide: return 0.6f;
                case PlayerTools.StrongArm: return 0.3f;
                default: return 0;
            }
        }

        public void SendPlayerUpdate()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;
            if(!playerDead && playerPosition != Vector3.Zero)
            if (lastPosition != playerPosition)//do full network update
            {
                lastPosition = playerPosition;

                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerUpdate);//full
                msgBuffer.Write(playerPosition);
                msgBuffer.Write(playerCamera.GetLookVector());
                msgBuffer.Write((byte)playerTools[playerToolSelected]);
                msgBuffer.Write(playerToolCooldown > 0.001f);
                netClient.SendMessage(msgBuffer, NetChannel.UnreliableInOrder2);
            }
            else if(lastHeading != playerCamera.GetLookVector())
            {
                lastHeading = playerCamera.GetLookVector();
                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerUpdate1);//just heading
                msgBuffer.Write(lastHeading);
                msgBuffer.Write((byte)playerTools[playerToolSelected]);
                msgBuffer.Write(playerToolCooldown > 0.001f);
                netClient.SendMessage(msgBuffer, NetChannel.UnreliableInOrder2);
            }
            else
            {
                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerUpdate2);//just tools
                msgBuffer.Write((byte)playerTools[playerToolSelected]);
                msgBuffer.Write(playerToolCooldown > 0.001f);
                netClient.SendMessage(msgBuffer, NetChannel.UnreliableInOrder2);
            }
        }
        public void SendDisconnect()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.Disconnect);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }
        public void SendPlayerHurt()
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

                NetBuffer msgBuffer = netClient.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerHurt);
                msgBuffer.Write(playerHealth);
                netClient.SendMessage(msgBuffer, NetChannel.ReliableInOrder1);
        }
        public void SendPlayerInteract(int button, uint x, uint y, uint z)
        {
            if (netClient.Status != NetConnectionStatus.Connected)
                return;

            NetBuffer msgBuffer = netClient.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerInteract);
            msgBuffer.Write(playerPosition);//also sends player locational data for range check
            msgBuffer.Write(button);
            msgBuffer.Write(x);
            msgBuffer.Write(y);
            msgBuffer.Write(z);
            netClient.SendMessage(msgBuffer, NetChannel.ReliableUnordered);
        }
    }
}
