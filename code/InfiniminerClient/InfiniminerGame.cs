using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Lidgren.Network;
using Lidgren.Network.Xna;
using System.Text;

namespace Infiniminer
{
    class RegionSort : IComparable<Vector3>
    {
        public Vector3 rs = Vector3.Zero;

        public RegionSort(Vector3 vs)
        {
            rs = vs;
        }

        // Compare the existing point with the point passed for sorting
        // using the Distance method
        public int CompareTo(Vector3 obj)
        {
            Vector3 otherPoint = obj;
            if ((otherPoint - rs).Length() < 0)
                return -1;
            else
                return 1;
        }

        private int Distance(int p, int p_2)
        {
            return (p * p + p_2 * p_2);
        }
    }

    public class InfiniminerGame : StateMasher.StateMachine
    {
        double timeSinceLastUpdate = 0;
        string playerHandle = "Player";
        string serverListURL = "http://zuzu-is.online";
        float volumeLevel = 1.0f;
        NetBuffer msgBuffer = null;
        const float NETWORK_UPDATE_TIME = 0.05f;
        public bool RenderPretty = true;
        public bool RenderLight = true;
        public bool DrawFrameRate = false;
        public bool InvertMouseYAxis = false;
        public bool NoSound = false;
        public float mouseSensitivity = 0.005f;
        public bool customColours = false;
        public Color red=Defines.IM_RED;
        public string redName = "Red";
        public Color blue = Defines.IM_BLUE;
        public string blueName = "Blue";
        IPEndPoint lastConnection;
        public KeyBindHandler keyBinds = new KeyBindHandler();
       
        public bool anyPacketsReceived = false;

        public InfiniminerGame(string[] args)
        {
        }

        public void setServername(string newName)
        {
            propertyBag.serverName = newName;
        }
        
        public void JoinGame(IPEndPoint serverEndPoint)
        {
            anyPacketsReceived = false;
            // Clear out the map load progress indicator.
            propertyBag.mapLoadProgress = new bool[propertyBag.MAPSIZE, propertyBag.MAPSIZE];
            for (int i = 0; i < propertyBag.MAPSIZE; i++)
                for (int j = 0; j < propertyBag.MAPSIZE; j++)
                    propertyBag.mapLoadProgress[i,j] = false;

            // Create our connect message.
            NetBuffer connectBuffer = propertyBag.netClient.CreateBuffer();
            connectBuffer.Write(propertyBag.playerHandle);
            connectBuffer.Write(Defines.INFINIMINER_VERSION);

            //Compression - will be ignored by regular servers
            connectBuffer.Write(true);

            // Connect to the server.
            propertyBag.netClient.Connect(serverEndPoint, connectBuffer.ToArray());
        }

        public List<ServerInformation> EnumerateServers(float discoveryTime)
        {
            List<ServerInformation> serverList = new List<ServerInformation>();
            
            // Discover local servers.
            propertyBag.netClient.DiscoverLocalServers(5565);
            NetBuffer msgBuffer = propertyBag.netClient.CreateBuffer();
            NetMessageType msgType;
            float timeTaken = 0;
            while (timeTaken < discoveryTime)
            {
                while (propertyBag.netClient.ReadMessage(msgBuffer, out msgType))
                {
                    if (msgType == NetMessageType.ServerDiscovered)
                    {
                        bool serverFound = false;
                        ServerInformation serverInfo = new ServerInformation(msgBuffer);
                        foreach (ServerInformation si in serverList)
                            if (si.Equals(serverInfo))
                                serverFound = true;
                        if (!serverFound)
                            serverList.Add(serverInfo);
                    }
                }

                timeTaken += 0.1f;
                Thread.Sleep(100);
            }

            // Discover remote servers.
            try
            {
                string publicList = HttpRequest.Get(serverListURL + "/plain", null);
                foreach (string s in publicList.Split("\r\n".ToCharArray()))
                {
                    string[] args = s.Split(";".ToCharArray());
                    if (args.Length == 6)
                    {
                        IPAddress serverIp;
                        if (IPAddress.TryParse(args[1], out serverIp) && args[2] == "INFINIMINER")
                        {
                            ServerInformation serverInfo = new ServerInformation(serverIp, args[0], args[5], args[3], args[4]);
                            serverList.Add(serverInfo);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return serverList;
        }

        public void UpdateNetwork(GameTime gameTime)
        {
            // Update the server with our status.
            timeSinceLastUpdate += gameTime.ElapsedGameTime.TotalSeconds;
            if (timeSinceLastUpdate > NETWORK_UPDATE_TIME)
            {
                timeSinceLastUpdate = 0;
                if (CurrentStateType == "Infiniminer.States.MainGameState")
                {
                    propertyBag.SendPlayerUpdate();
                }
            }

            // Recieve messages from the server.
            NetMessageType msgType;
            while (propertyBag.netClient.ReadMessage(msgBuffer, out msgType))
            {
                switch (msgType)
                {
                    case NetMessageType.StatusChanged:
                        {
                            if (propertyBag.netClient.Status == NetConnectionStatus.Disconnected)
                            {
                                ChangeState("Infiniminer.States.ServerBrowserState");//needed to reset 

                                Thread.Sleep(50);
                                JoinGame(lastConnection);//attempts to reconnect
                                ChangeState("Infiniminer.States.LoadingState");
                            }
                        }
                        break;
                    case NetMessageType.ConnectionApproval:
                        anyPacketsReceived = true;
                        break;
                    case NetMessageType.ConnectionRejected:
                        {
                            anyPacketsReceived = false;
                            try
                            {
                                string[] reason = msgBuffer.ReadString().Split(";".ToCharArray());
                                if (reason.Length < 2 || reason[0] == "VER")
                                    CrossPlatformServices.Instance.ShowMessage("Version Incompatibility", 
                                        $"Error: client/server version incompatibility!\nServer: {reason[1]}\nClient: {Defines.INFINIMINER_VERSION}");
                                else
                                    CrossPlatformServices.Instance.ShowMessage("Banned", "Error: you are banned from this server!");
                            }
                            catch { }
                            ChangeState("Infiniminer.States.ServerBrowserState");
                        }
                        break;

                    case NetMessageType.Data:
                        {
                            try
                            {
                                InfiniminerMessage dataType = (InfiniminerMessage)msgBuffer.ReadByte();
                                switch (dataType)
                                {
                                    case InfiniminerMessage.Challenge:
                                        {
                                            propertyBag.Challenge = true;
                                            propertyBag.UploadMap();
                                        }
                                        break;
                                    case InfiniminerMessage.BlockBulkTransfer:
                                        {
                                            anyPacketsReceived = true;

                                            try
                                            {
                                                //This is either the compression flag or the x coordiante
                                                byte isCompressed = msgBuffer.ReadByte();
                                                byte x;
                                                byte y;

                                                //255 was used because it exceeds the map size - of course, bytes won't work anyway if map sizes are allowed to be this big, so this method is a non-issue
                                                if (isCompressed == 255)
                                                {
                                                    var compressed = msgBuffer.ReadBytes(msgBuffer.LengthBytes - msgBuffer.Position / 8);
                                                    var compressedstream = new System.IO.MemoryStream(compressed);
                                                    var decompresser = new System.IO.Compression.GZipStream(compressedstream, System.IO.Compression.CompressionMode.Decompress);

                                                    x = (byte)decompresser.ReadByte();
                                                    y = (byte)decompresser.ReadByte();
                                                    propertyBag.mapLoadProgress[x, y] = true;
                                                    for (byte dy = 0; dy < 16; dy++)
                                                        for (byte z = 0; z < propertyBag.MAPSIZE; z++)
                                                        {
                                                            BlockType blockType = (BlockType)decompresser.ReadByte();
                                                            if (blockType != BlockType.None)
                                                                propertyBag.blockEngine.downloadList[x, y + dy, z] = blockType;
                                                        }
                                                }
                                                else
                                                {
                                                    x = isCompressed;
                                                    y = msgBuffer.ReadByte();
                                                    propertyBag.mapLoadProgress[x, y] = true;
                                                    for (byte dy = 0; dy < 16; dy++)
                                                        for (byte z = 0; z < propertyBag.MAPSIZE; z++)
                                                        {
                                                            BlockType blockType = (BlockType)msgBuffer.ReadByte();
                                                            if (blockType != BlockType.None)
                                                                propertyBag.blockEngine.downloadList[x, y + dy, z] = blockType;
                                                        }
                                                }
                                                bool downloadComplete = true;
                                                for (x = 0; x < propertyBag.MAPSIZE; x++)
                                                    for (y = 0; y < propertyBag.MAPSIZE; y += 16)
                                                        if (propertyBag.mapLoadProgress[x, y] == false)
                                                        {
                                                            downloadComplete = false;
                                                            break;
                                                        }
                                                if (downloadComplete)
                                                {
                                                    propertyBag.connectedTimer = DateTime.Now + TimeSpan.FromSeconds(2);
                                                    if (propertyBag.playerDead == true && propertyBag.playerTeam == PlayerTeam.None)
                                                    {
                                                        propertyBag.screenEffect = ScreenEffect.Death;
                                                        propertyBag.screenEffectCounter = 2;
                                                        ChangeState("Infiniminer.States.ClassSelectionState");
                                                        //ChangeState("Infiniminer.States.TeamSelectionState");
                                                    }
                                                    else if (propertyBag.playerClass == PlayerClass.None)//reconnecting but we have no class
                                                    { 
                                                        propertyBag.screenEffect = ScreenEffect.Death;
                                                        propertyBag.screenEffectCounter = 2;
                                                        ChangeState("Infiniminer.States.ClassSelectionState");
                                                    }
                                                    else if (propertyBag.playerDead == true)//reconnecting when dead
                                                    {
                                                        propertyBag.screenEffect = ScreenEffect.Death;
                                                        propertyBag.screenEffectCounter = 2;
                                                        ChangeState("Infiniminer.States.MainGameState");
                                                    }
                                                    else
                                                    {
                                                        ChangeState("Infiniminer.States.MainGameState");
                                                    }
                                                    if (!NoSound)
                                                        MediaPlayer.Stop();

                                                    lastConnection = new IPEndPoint(propertyBag.netClient.ServerConnection.RemoteEndpoint.Address, 5565);
                                                    propertyBag.blockEngine.DownloadComplete();
                                                    propertyBag.blockEngine.CalculateLight();
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                Console.OpenStandardError();
                                                Console.Error.WriteLine(e.Message);
                                                Console.Error.WriteLine(e.StackTrace);
                                                Console.Error.Close();
                                            }
                                        }
                                        break;

                                    case InfiniminerMessage.SetBeacon:
                                        {
                                            Vector3 position = msgBuffer.ReadVector3();
                                            string text = msgBuffer.ReadString();
                                            PlayerTeam team = (PlayerTeam)msgBuffer.ReadByte();

                                            if (text == "")
                                            {
                                                if (propertyBag.beaconList.ContainsKey(position))
                                                    propertyBag.beaconList.Remove(position);
                                            }
                                            else
                                            {
                                                Beacon newBeacon = new Beacon();
                                                newBeacon.ID = text;
                                                newBeacon.Team = team;
                                                propertyBag.beaconList.Add(position, newBeacon);
                                            }
                                        }
                                        break;
                                    case InfiniminerMessage.SetItem:
                                        {
                                            ItemType iType = (ItemType)(msgBuffer.ReadByte());
                                            Item newItem = new Item((Game)this,iType);
                                            newItem.ID = msgBuffer.ReadUInt32();
                                            newItem.Position = msgBuffer.ReadVector3();
                                            newItem.Team = (PlayerTeam)msgBuffer.ReadByte();
                                            newItem.Heading = msgBuffer.ReadVector3();
                                            newItem.deltaPosition = newItem.Position;
                                            newItem.Content[1] = msgBuffer.ReadInt32();
                                            newItem.Content[2] = msgBuffer.ReadInt32();
                                            newItem.Content[3] = msgBuffer.ReadInt32();
                                            newItem.Content[10] = msgBuffer.ReadInt32();
                                            propertyBag.itemList.Add(newItem.ID, newItem);
                                        }
                                        break;
                                    case InfiniminerMessage.ActiveArtifactUpdate:
                                        {
                                            propertyBag.artifactActive[msgBuffer.ReadByte(), msgBuffer.ReadInt32()] = msgBuffer.ReadInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.ItemUpdate:
                                        {
                                            uint id = msgBuffer.ReadUInt32();

                                            //if (propertyBag.itemList.ContainsKey(id))
                                            propertyBag.itemList[id].Position = msgBuffer.ReadVector3();
                                        }
                                        break;
                                    case InfiniminerMessage.ItemScaleUpdate:
                                        {
                                            
                                            uint id = msgBuffer.ReadUInt32();

                                            //if (propertyBag.itemList.ContainsKey(id))
                                            propertyBag.itemList[id].Scale = msgBuffer.ReadFloat();
                                        }
                                        break;
                                    case InfiniminerMessage.ItemContentSpecificUpdate:
                                        {

                                            uint id = msgBuffer.ReadUInt32();
                                            uint cc = msgBuffer.ReadUInt32();
                                            //if (propertyBag.itemList.ContainsKey(id))
                                            propertyBag.itemList[id].Content[cc] = msgBuffer.ReadInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.SetItemRemove:
                                        {
                                            uint id = msgBuffer.ReadUInt32();
                                          
                                            if (propertyBag.itemList.ContainsKey(id))
                                                propertyBag.itemList.Remove(id);
                                           
                                        }
                                        break;
    
                                    case InfiniminerMessage.TriggerConstructionGunAnimation:
                                        {
                                            propertyBag.constructionGunAnimation = msgBuffer.ReadFloat();
                                            if (propertyBag.constructionGunAnimation <= -0.1)
                                                propertyBag.PlaySound(InfiniminerSound.RadarSwitch);
                                        }
                                        break;
                                    case InfiniminerMessage.ScoreUpdate:
                                        {
                                            propertyBag.teamArtifactsRed = msgBuffer.ReadUInt32();
                                            propertyBag.teamArtifactsBlue = msgBuffer.ReadUInt32();
                                            break;
                                        }
                                    case InfiniminerMessage.ResourceUpdate:
                                        {
                                            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
                                            propertyBag.playerOre = msgBuffer.ReadUInt32();
                                            propertyBag.playerCash = msgBuffer.ReadUInt32();
                                            propertyBag.playerWeight = msgBuffer.ReadUInt32();
                                            propertyBag.playerOreMax = msgBuffer.ReadUInt32();
                                            propertyBag.playerWeightMax = msgBuffer.ReadUInt32();
                                            propertyBag.teamOre = msgBuffer.ReadUInt32();
                                            propertyBag.teamRedCash = msgBuffer.ReadUInt32();
                                            propertyBag.teamBlueCash = msgBuffer.ReadUInt32();
                                            propertyBag.teamArtifactsRed = msgBuffer.ReadUInt32();
                                            propertyBag.teamArtifactsBlue = msgBuffer.ReadUInt32();
                                            propertyBag.winningCashAmount = msgBuffer.ReadUInt32();
                                            propertyBag.playerHealth = msgBuffer.ReadUInt32();
                                            propertyBag.playerHealthMax = msgBuffer.ReadUInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.TeamCashUpdate:
                                        {
                                            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
                                            propertyBag.teamRedCash = msgBuffer.ReadUInt32();
                                            propertyBag.teamBlueCash = msgBuffer.ReadUInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.TeamOreUpdate:
                                        {
                                           propertyBag.teamOre = msgBuffer.ReadUInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.HealthUpdate:
                                        {
                                            propertyBag.playerHealth = msgBuffer.ReadUInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.PlayerSlap:
                                        {
                                            uint pID = msgBuffer.ReadUInt32();
                                            uint aID = msgBuffer.ReadUInt32();

                                            if (pID == propertyBag.playerMyId)
                                            {
                                                propertyBag.screenEffect = ScreenEffect.Fall;
                                                propertyBag.screenEffectCounter = 2 - 0.5;
                                                
                                                if (aID == 0)//bomb or other hurt
                                                {
                                                    propertyBag.particleEngine.CreateBloodSplatter(propertyBag.playerPosition, propertyBag.playerTeam == PlayerTeam.Red ? Color.Red : Color.Blue, 0.4f);
                                                }
                                                else//player attack
                                                {
                                                    propertyBag.particleEngine.CreateBloodSplatter(propertyBag.playerPosition, propertyBag.playerTeam == PlayerTeam.Red ? Color.Red : Color.Blue, 0.2f);
                                                    propertyBag.forceVector = propertyBag.playerList[aID].Heading;
                                                    propertyBag.forceVector.Y = 0;
                                                    //propertyBag.forceVector.Normalize();
                                                    if (propertyBag.artifactActive[(byte)propertyBag.playerTeam, 9] > 0)//stone artifact prevents kb
                                                    {
                                                        if (propertyBag.artifactActive[(byte)propertyBag.playerTeam, 9] < 4)
                                                            propertyBag.forceStrength = 4.0f - (float)propertyBag.artifactActive[(byte)propertyBag.playerTeam, 9];
                                                        else
                                                            propertyBag.forceStrength = 0;
                                                    }
                                                    else if (propertyBag.Content[10] == 9)//stone artifact
                                                    {
                                                        propertyBag.forceStrength = 0;
                                                    }
                                                    else
                                                    {
                                                        propertyBag.forceStrength = 4.0f;
                                                    }

                                                    if (propertyBag.artifactActive[(byte)propertyBag.playerList[aID].Team, 10] > 0)
                                                    {
                                                        propertyBag.forceStrength *= (1.0f + (propertyBag.artifactActive[(byte)propertyBag.playerTeam, 10] / 4));
                                                    }
                                                    propertyBag.PlaySound(InfiniminerSound.Slap);
                                                }
                                            }
                                            else
                                            {
                                                if (aID == 0)//bomb or other hurt
                                                {
                                                    propertyBag.particleEngine.CreateBloodSplatter(propertyBag.playerList[pID].Position, propertyBag.playerList[pID].Team == PlayerTeam.Red ? Color.Red : Color.Blue, 0.4f);
                                                }
                                                else
                                                {
                                                    propertyBag.PlaySound(InfiniminerSound.Slap, propertyBag.playerList[pID].Position);
                                                    propertyBag.particleEngine.CreateBloodSplatter(propertyBag.playerList[pID].Position, propertyBag.playerList[pID].Team == PlayerTeam.Red ? Color.Red : Color.Blue, 0.2f);
                                                }
                                            }
                                            
                                        }
                                        break;
                                    case InfiniminerMessage.WeightUpdate:
                                        {
                                            propertyBag.playerWeight = msgBuffer.ReadUInt32();                                           
                                        }
                                        break;
                                    case InfiniminerMessage.OreWarning:
                                        {
                                            propertyBag.oreWarning = DateTime.Now + TimeSpan.FromMilliseconds(250);
                                        }
                                        break;
                                    case InfiniminerMessage.OreUpdate:
                                        {
                                            propertyBag.playerOre = msgBuffer.ReadUInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.CashUpdate:
                                        {
                                            propertyBag.playerCash = msgBuffer.ReadUInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.ContentUpdate:
                                        {
                                            //update all player content values
                                            for (int a = 0; a < 50; a++)
                                            {
                                                propertyBag.Content[a] = msgBuffer.ReadInt32();
                                            }
                                        }
                                        break;
                                    case InfiniminerMessage.StatusEffectUpdate:
                                        {
                                            uint pID = msgBuffer.ReadUInt32();

                                            if(pID == propertyBag.playerMyId)
                                                propertyBag.StatusEffect[msgBuffer.ReadInt32()] = msgBuffer.ReadInt32();
                                            else
                                                propertyBag.playerList[pID].StatusEffect[msgBuffer.ReadInt32()] = msgBuffer.ReadInt32();
                                        }
                                        break;
                                    case InfiniminerMessage.ContentSpecificUpdate:
                                        {
                                            // update specific value
                                            int val = msgBuffer.ReadInt32();
                                            propertyBag.Content[val] = msgBuffer.ReadInt32();

                                            if (val == 10)//artifact change
                                            {
                                                switch(propertyBag.Content[val])
                                                {
                                                    case 1:
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", granting 40 ore periodically!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 2:
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", granting life stealing attacks!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 3:
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", granting powerful life regeneration, and regenerating any nearby blocks when thrown!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 4:
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", granting water breathing and digging underwater!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 5:
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", transmuting every 100 ore into 10 gold!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 6:
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", shocking nearby enemies, and creating a torrential downpour when thrown!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 7:
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", reflecting half of damage taken!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 8://medical
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", healing friendlies with each swing; enemies become infected!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 9://stone
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", making you immune to knockback and resistant to fall damage!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 10://tremor
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", making enemies around you jump irregularly!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 11://judgement
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", striking enemies based on their maximum hit points!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 12://bog
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", making a trail of permanent mud behind you!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 13://explosive
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", and will explode shortly if you don't let go!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 14://armor
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", becoming immune to explosive damage!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 15://doom
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", dealing a death sentence!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 16://inferno
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", causing flames to appear on your opponent!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 17://clairvoyance
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", revealing all!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 18://wings
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", allowing you to fly!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 19://grapple
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", allowing you to pull enemies toward you!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 20://decay
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", !", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 21://precision
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", granting the ability to backstab!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 22://awry
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", creating havoc for nearby enemies!", ChatMessageType.SayAll, 10);
                                                        break;
                                                    case 23://shield
                                                        propertyBag.addChatMessage("You now wield the " + ArtifactInformation.GetName(propertyBag.Content[val]) + ", gifting you an invulnerable shield!", ChatMessageType.SayAll, 10);
                                                        break;
                                                }
                                            }
                                            else if(val == 5 && propertyBag.playerClass == PlayerClass.Miner)
                                            {
                                                propertyBag.playerToolCooldown = propertyBag.GetToolCooldown(PlayerTools.Pickaxe) * 4;
                                            }
                                        }
                                        break;
                                    case InfiniminerMessage.PlayerPosition:
                                        {
                                            propertyBag.playerPosition = msgBuffer.ReadVector3();
                                        }
                                        break;
                                    case InfiniminerMessage.PlayerVelocity:
                                        {
                                            propertyBag.forceVector += msgBuffer.ReadVector3();
                                            
                                            if (propertyBag.artifactActive[(byte)propertyBag.playerTeam, 9] > 0)//stone artifact prevents kb
                                            {
                                                if (propertyBag.artifactActive[(byte)propertyBag.playerTeam, 9] < 2)
                                                    propertyBag.forceStrength = 2.0f - (float)propertyBag.artifactActive[(byte)propertyBag.playerTeam, 9];
                                                else
                                                    propertyBag.forceStrength = 0;
                                            }
                                            else if (propertyBag.Content[10] == 9)//stone artifact
                                            {
                                                propertyBag.forceStrength = 0;
                                            }
                                            else
                                            {
                                                propertyBag.forceStrength = 2.0f;
                                            }
                                            
                                            propertyBag.PlaySound(InfiniminerSound.Slap);
                                        }
                                        break;
                                    case InfiniminerMessage.BlockSet:
                                        {
                                            // x, y, z, type, all bytes
                                            byte x = msgBuffer.ReadByte();
                                            byte y = msgBuffer.ReadByte();
                                            byte z = msgBuffer.ReadByte();
                                            BlockType blockType = (BlockType)msgBuffer.ReadByte();

                                            if (blockType == BlockType.None)
                                            {
                                                if (propertyBag.blockEngine.BlockAtPoint(new Vector3(x, y, z)) != BlockType.None)
                                                    propertyBag.blockEngine.RemoveBlock(x, y, z);
                                            }
                                            else
                                            {
                                                if (propertyBag.blockEngine.BlockAtPoint(new Vector3(x, y, z)) != BlockType.None)
                                                {
                                                    propertyBag.blockEngine.RemoveBlock(x, y, z);
                                                    //propertyBag.particleEngine.CreateExplosionDebris(new Vector3(x, y, z));
                                                }
                                                propertyBag.blockEngine.AddBlock(x, y, z, blockType);

                                                //if(!propertyBag.playerDead)
                                                //CheckForStandingInLava();
                                            }
                                        }
                                        break;
                                    case InfiniminerMessage.BlockSetTex:
                                        {
                                            // x, y, z, type, all bytes
                                            byte x = msgBuffer.ReadByte();
                                            byte y = msgBuffer.ReadByte();
                                            byte z = msgBuffer.ReadByte();
                                            BlockType blockType = (BlockType)msgBuffer.ReadByte();
                                            BlockType blockTypeTex = (BlockType)msgBuffer.ReadByte();

                                            if (blockType == BlockType.None)
                                            {
                                                if (propertyBag.blockEngine.BlockAtPoint(new Vector3(x, y, z)) != BlockType.None)
                                                    propertyBag.blockEngine.RemoveBlock(x, y, z);
                                            }
                                            else
                                            {
                                                if (propertyBag.blockEngine.BlockAtPoint(new Vector3(x, y, z)) != BlockType.None)
                                                {
                                                    propertyBag.blockEngine.RemoveBlock(x, y, z);
                                                    //propertyBag.particleEngine.CreateExplosionDebris(new Vector3(x, y, z));
                                                }
                                                propertyBag.blockEngine.AddBlock(x, y, z, blockType, blockTypeTex);

                                                //if(!propertyBag.playerDead)
                                                //CheckForStandingInLava();
                                            }
                                        }
                                        break;
                                    case InfiniminerMessage.BlockSetDebris:
                                        {
                                            // x, y, z, type, all bytes
                                            byte x = msgBuffer.ReadByte();
                                            byte y = msgBuffer.ReadByte();
                                            byte z = msgBuffer.ReadByte();
                                            BlockType blockType = (BlockType)msgBuffer.ReadByte();
                                            Vector3 tv = new Vector3(x, y, z);

                                            
                                            if (blockType == BlockType.None)
                                            {
                                                if (propertyBag.blockEngine.BlockAtPoint(tv) != BlockType.None)
                                                {
                                                    float distFromDebris = (tv + 0.5f * Vector3.One - propertyBag.playerPosition).Length();

                                                    propertyBag.particleEngine.CreateBlockDebris(tv + 0.5f * Vector3.One, propertyBag.blockEngine.blockList[x, y, z], Math.Min(14.0f - distFromDebris, 10.0f));
                                                    propertyBag.blockEngine.RemoveBlock(x, y, z);

                                                }

                                            }

                                        }
                                        break;
                                    case InfiniminerMessage.TriggerEarthquake:
                                        {
                                            Vector3 blockPos = msgBuffer.ReadVector3();
                                            uint expStrength = msgBuffer.ReadUInt32();

                                            // Play the explosion sound.
                                            propertyBag.PlaySound(InfiniminerSound.Explosion, blockPos);//, (int)(expStrength/1.5));

                                            // Create some particles.
                                            propertyBag.particleEngine.CreateExplosionDebris(blockPos);

                                            // Figure out what the effect is.
                                            float distFromExplosive = (blockPos + 0.5f * Vector3.One - propertyBag.playerPosition).Length();
                                            
                                            propertyBag.screenEffectCounter = Math.Min(propertyBag.screenEffectCounter, 1 / 5);
                                            
                                        }
                                        break;
                                    case InfiniminerMessage.TriggerExplosion:
                                        {
                                            Vector3 blockPos = msgBuffer.ReadVector3();
                                            uint expStrength = msgBuffer.ReadUInt32();

                                            // Play the explosion sound.
                                            propertyBag.PlaySound(InfiniminerSound.Explosion, blockPos);

                                            // Create some particles.
                                            propertyBag.particleEngine.CreateExplosionDebris(blockPos);

                                            // Figure out what the effect is.
                                            float distFromExplosive = (blockPos + 0.5f * Vector3.One - propertyBag.playerPosition).Length();
                                            //if (distFromExplosive < 3)
                                             //   propertyBag.KillPlayer(Defines.deathByExpl);//"WAS KILLED IN AN EXPLOSION!");
                                            //else 
                                            if (distFromExplosive < 8)
                                            {
                                                // If we're not in explosion mode, turn it on with the minimum ammount of shakiness.
                                                if (propertyBag.screenEffect != ScreenEffect.Explosion && propertyBag.screenEffect != ScreenEffect.Death)
                                                {
                                                    propertyBag.screenEffect = ScreenEffect.Explosion;
                                                    propertyBag.screenEffectCounter = 2;
                                                }
                                                // If this bomb would result in a bigger shake, use its value.
                                                propertyBag.screenEffectCounter = Math.Min(propertyBag.screenEffectCounter, (distFromExplosive - 2) / 5);
                                            }

                                        }
                                        break;
                                    case InfiniminerMessage.Effect:
                                        {
                                            Vector3 blockPos = msgBuffer.ReadVector3();
                                            uint effectType = msgBuffer.ReadUInt32();
                                            float distFromDebris = (blockPos + 0.5f * Vector3.One - propertyBag.playerPosition).Length();

                                            if(distFromDebris < 14)
                                                switch(effectType)
                                                {
                                                    case 1://hide
                                                        propertyBag.particleEngine.CreateHidden(blockPos, Color.GhostWhite);
                                                        break;
                                                    case 2://medical
                                                        propertyBag.particleEngine.CreateHidden(blockPos, Color.GreenYellow);
                                                        break;
                                                    case 3://shock
                                                        propertyBag.particleEngine.CreateHidden(blockPos, Color.CadetBlue);
                                                        break;
                                                    case 4://heal
                                                        propertyBag.particleEngine.CreateHidden(blockPos, Color.Green);
                                                        break;
                                                    case 5://inferno
                                                        propertyBag.particleEngine.CreateHidden(blockPos, Color.Orange);
                                                        break;
                                                }
                                        }
                                        break;
                                  case InfiniminerMessage.TriggerDebris:
                                        {
                                            Vector3 blockPos = msgBuffer.ReadVector3();
                                            BlockType blockType = (BlockType)msgBuffer.ReadByte();
                                            uint debrisType = msgBuffer.ReadUInt32();
                                            // Play the debris sound.
                                            //propertyBag.PlaySound(InfiniminerSound.Explosion, blockPos);

                                            // Create some particles.

                                            float distFromDebris = (blockPos + 0.5f * Vector3.One - propertyBag.playerPosition).Length();

                                            if (debrisType == 1)//block was destroyed
                                            {
                                                    propertyBag.particleEngine.CreateBlockDebris(blockPos, blockType, Math.Min(20.0f - distFromDebris, 10.0f)); 
                                            }
                                            else if(debrisType == 0)//other players digging
                                            {
                                                if (distFromDebris < 24)
                                                    propertyBag.particleEngine.CreateDiggingDebris(blockPos, blockType);
                                            }
                                            else if (debrisType == 2)//block dmg debris
                                            {
                                                if (distFromDebris < 16)
                                                    if (blockType == BlockType.SolidRed)
                                                        propertyBag.particleEngine.CreateBloodSplatter(blockPos, Color.Red, 1.0f);
                                                    else
                                                        propertyBag.particleEngine.CreateBloodSplatter(blockPos, Color.Blue, 1.0f);
                                            }
                                            else if (debrisType > 10)//anything over this determines damage intensity for hurt effect
                                            {
                                                if (distFromDebris < 15)
                                                {
                                                    if (blockType == BlockType.SolidRed)
                                                        propertyBag.particleEngine.CreateBloodSplatter(blockPos, Color.Red, (float)(debrisType)/100);
                                                    else if (blockType == BlockType.SolidBlue)
                                                        propertyBag.particleEngine.CreateBloodSplatter(blockPos, Color.Blue, (float)(debrisType)/100);
                                                    else if (blockType == BlockType.Highlight)
                                                        propertyBag.particleEngine.CreateBloodSplatter(blockPos, Color.Green, (float)(debrisType) / 100);
                                                }
                                            }

                                        }
                                        break;
                                    case InfiniminerMessage.PlayerSetTeam:
                                        {
                                            uint playerId = msgBuffer.ReadUInt32();
                                            if (propertyBag.playerList.ContainsKey(playerId))
                                            {
                                                Player player = propertyBag.playerList[playerId];
                                                player.Team = (PlayerTeam)msgBuffer.ReadByte();

                                                if (playerId == propertyBag.playerMyId)
                                                {
                                                    propertyBag.playerTeam = player.Team;

                                                    if (propertyBag.playerTeam == PlayerTeam.Red)
                                                    {
                                                        propertyBag.playerTeamWanted = PlayerTeam.Red;
                                                        propertyBag.blockEngine.blockTextures[(byte)BlockTexture.TrapR] = propertyBag.blockEngine.blockTextures[(byte)BlockTexture.TrapVis];
                                                        propertyBag.blockEngine.blockTextures[(byte)BlockTexture.TrapB] = propertyBag.blockEngine.blockTextures[(byte)BlockTexture.Trap];
                                                    }
                                                    else if (propertyBag.playerTeam == PlayerTeam.Blue)
                                                    {
                                                        propertyBag.playerTeamWanted = PlayerTeam.Blue;
                                                        propertyBag.blockEngine.blockTextures[(byte)BlockTexture.TrapB] = propertyBag.blockEngine.blockTextures[(byte)BlockTexture.TrapVis];
                                                        propertyBag.blockEngine.blockTextures[(byte)BlockTexture.TrapR] = propertyBag.blockEngine.blockTextures[(byte)BlockTexture.Trap];
                                                    }
                                                    propertyBag.equipWeps();
                                                }
                                            }
                                        }
                                        break;
                                    case InfiniminerMessage.PlayerSetClass:
                                        {
                                            uint playerId = msgBuffer.ReadUInt32();
                                            if (propertyBag.playerList.ContainsKey(playerId))
                                            {
                                               
                                                Player player = propertyBag.playerList[playerId];
                                                PlayerClass old = player.Class;
                                                player.Class = (PlayerClass)msgBuffer.ReadByte();

                                                if(player.Class != old)//only clear exp if score removed
                                                    player.Exp = player.Score;

                                                if (playerId == propertyBag.playerMyId)
                                                {
                                                    if (player.Class != propertyBag.playerClass)
                                                    {
                                                        propertyBag.playerClass = player.Class;

                                                        for (int a = 0; a < 50; a++)
                                                        {
                                                            propertyBag.Content[a] = 0;//empty the baggage
                                                        }

                                                        propertyBag.playerToolSelected = 0;
                                                        propertyBag.playerBlockSelected = 0;

                                                        propertyBag.equipWeps();

                                                        propertyBag.RespawnPlayer();
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    case InfiniminerMessage.PlayerJoined:
                                        {
                                            uint playerId = msgBuffer.ReadUInt32();
                                            string playerName = msgBuffer.ReadString();
                                            bool thisIsMe = msgBuffer.ReadBoolean();
                                            bool playerAlive = msgBuffer.ReadBoolean();
                                            try 
                                            {
                                                propertyBag.playerList[playerId] = new Player(null, (Game)this);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"CRASH creating Player: {ex.Message}");
                                                // Create a simpler fallback player object
                                                propertyBag.playerList[playerId] = new Player(null, null);
                                                Console.WriteLine("Created fallback Player object!");
                                            }
                                            
                                            propertyBag.playerList[playerId].Handle = playerName;
                                            propertyBag.playerList[playerId].ID = playerId;
                                            propertyBag.playerList[playerId].Alive = playerAlive;
                                            propertyBag.playerList[playerId].AltColours = customColours;
                                            propertyBag.playerList[playerId].redTeam = red;
                                            propertyBag.playerList[playerId].blueTeam = blue;
                                            if (thisIsMe)
                                            {
                                                propertyBag.playerHandle = playerName;//my name was changed by the server
                                                playerHandle = playerName;

                                                propertyBag.playerMyId = playerId;
                                                if (propertyBag.playerList[playerId].Alive == true)
                                                {//a resuming connection
                                                    propertyBag.playerDead = false;
                                                    propertyBag.allowRespawn = false;
                                                    propertyBag.screenEffect = ScreenEffect.None;
                                                    propertyBag.screenEffectCounter = 0;
                                                }
                                                else
                                                {
                                                    propertyBag.playerDead = true;
                                                    propertyBag.allowRespawn = false;
                                                    propertyBag.screenEffect = ScreenEffect.Respawn;
                                                    propertyBag.screenEffectCounter = 2;
                                                }
                                            }
                                        }
                                        break;

                                    case InfiniminerMessage.PlayerContentUpdate:
                                        {
                                            uint playerId = msgBuffer.ReadUInt32();
                                            uint cc = msgBuffer.ReadUInt32();
                                            int val = msgBuffer.ReadInt32();

                                            propertyBag.playerList[playerId].Content[cc] = val;
                                        }
                                        break;

                                    case InfiniminerMessage.PlayerLeft:
                                        {
                                            uint playerId = msgBuffer.ReadUInt32();
                                            if (propertyBag.playerList.ContainsKey(playerId))
                                                propertyBag.playerList.Remove(playerId);
                                        }
                                        break;

                                    case InfiniminerMessage.PlayerDead:
                                        {
                                            uint playerId = msgBuffer.ReadUInt32();
                                            if (propertyBag.playerList.ContainsKey(playerId))
                                            {
                                                Player player = propertyBag.playerList[playerId];
                                                player.Alive = false;
                                                propertyBag.particleEngine.CreateBloodSplatter(player.Position, player.Team == PlayerTeam.Red ? Color.Red : Color.Blue, 2.0f);
                                                if (playerId != propertyBag.playerMyId)
                                                    propertyBag.PlaySound(InfiniminerSound.Death, player.Position);
                                                else if(propertyBag.playerDead == false)
                                                {
                                                    propertyBag.PlaySound(InfiniminerSound.Death, player.Position);
                                                    propertyBag.playerVelocity = Vector3.Zero;
                                                    propertyBag.playerDead = true;
                                                    propertyBag.allowRespawn = false;
                                                    propertyBag.screenEffect = ScreenEffect.Death;
                                                    propertyBag.screenEffectCounter = 0;
                                                    propertyBag.Content[5] = 0;
                                                }
                                            }
                                        }
                                        break;

                                    case InfiniminerMessage.PlayerAlive:
                                        {
                                            uint playerId = msgBuffer.ReadUInt32();
                                            if (propertyBag.playerList.ContainsKey(playerId))
                                            {
                                                Player player = propertyBag.playerList[playerId];
                                                player.Alive = true;

                                                if (playerId == propertyBag.playerMyId)//refresh lagger hp
                                                {
                                                    propertyBag.playerDead = false;
                                                    propertyBag.screenEffect = ScreenEffect.None;
                                                    if (propertyBag.playerHealth == 0)
                                                        propertyBag.playerHealth = propertyBag.playerHealthMax;
                                                }
                                            }
                                        }
                                        break;

                                    case InfiniminerMessage.PlayerRespawn:
                                        {
                                            //propertyBag.playerList[propertyBag.playerMyId].UpdatePosition(msgBuffer.ReadVector3(), gameTime.TotalGameTime.TotalSeconds);
                                            propertyBag.playerPosition = msgBuffer.ReadVector3();
                                            propertyBag.allowRespawn = true;
                                            if (propertyBag.screenEffect == ScreenEffect.Death)
                                            {
                                                propertyBag.screenEffect = ScreenEffect.Respawn;
                                            }
                                        }
                                        break;

                                    case InfiniminerMessage.PlayerUpdate:
                                        {
                                            uint playerId = msgBuffer.ReadUInt32();
                                            if (propertyBag.playerList.ContainsKey(playerId))
                                            {
                                                Player player = propertyBag.playerList[playerId];
                                                player.UpdatePosition(msgBuffer.ReadVector3(), gameTime.TotalGameTime.TotalSeconds);
                                                player.Heading = msgBuffer.ReadVector3();
                                                player.Tool = (PlayerTools)msgBuffer.ReadByte();
                                                player.UsingTool = msgBuffer.ReadBoolean();
                                                player.Score = (uint)msgBuffer.ReadUInt16();
                                                player.Health = (uint)msgBuffer.ReadUInt16();
                                            }
                                        }
                                        break;

                                    case InfiniminerMessage.GameOver:
                                        {
                                            propertyBag.teamWinners = (PlayerTeam)msgBuffer.ReadByte();
                                        }
                                        break;

                                    case InfiniminerMessage.ChatMessage:
                                        {
                                            ChatMessageType chatType = (ChatMessageType)msgBuffer.ReadByte();
                                            string chatString = Defines.Sanitize(msgBuffer.ReadString());
                                            //Time to break it up into multiple lines
                                            propertyBag.addChatMessage(chatString, chatType, 10);
                                        }
                                        break;

                                    case InfiniminerMessage.PlayerPing:
                                        {
                                            uint playerId = (uint)msgBuffer.ReadInt32();
                                            if (propertyBag.playerList.ContainsKey(playerId))
                                            {
                                                if (propertyBag.playerList[playerId].Team == propertyBag.playerTeam)
                                                {
                                                    propertyBag.playerList[playerId].Ping = 1;
                                                    propertyBag.PlaySound(InfiniminerSound.Ping);
                                                }
                                            }
                                        }
                                        break;

                                    case InfiniminerMessage.PlaySound:
                                        {
                                            InfiniminerSound sound = (InfiniminerSound)msgBuffer.ReadByte();
                                            bool hasPosition = msgBuffer.ReadBoolean();
                                            if (hasPosition)
                                            {
                                                Vector3 soundPosition = msgBuffer.ReadVector3();
                                                propertyBag.PlaySound(sound, soundPosition);
                                            }
                                            else
                                                propertyBag.PlaySound(sound);
                                        }
                                        break;
                                }
                            }
                            catch { } //Error in a received message
                        }
                        break;
                }
            }

            // Make sure our network thread actually gets to run.
            Thread.Sleep(1);
        }

        //private void CheckForStandingInLava()
        //{
        //    // Copied from TryToMoveTo; responsible for checking if lava has flowed over us.

        //    Vector3 movePosition = propertyBag.playerPosition;
        //    Vector3 midBodyPoint = movePosition + new Vector3(0, -0.7f, 0);
        //    Vector3 lowerBodyPoint = movePosition + new Vector3(0, -1.4f, 0);
        //    BlockType lowerBlock = propertyBag.blockEngine.BlockAtPoint(lowerBodyPoint);
        //    BlockType midBlock = propertyBag.blockEngine.BlockAtPoint(midBodyPoint);
        //    BlockType upperBlock = propertyBag.blockEngine.BlockAtPoint(movePosition);
        //    if (upperBlock == BlockType.Lava || lowerBlock == BlockType.Lava || midBlock == BlockType.Lava)
        //    {
        //        propertyBag.KillPlayer(DeathMessage.deathByLava);
        //    }

        //}

        protected override void Initialize()
        {
            font = "font_04b08";
            graphicsDeviceManager.IsFullScreen = false;
            graphicsDeviceManager.PreferredBackBufferWidth = 1024;
            graphicsDeviceManager.PreferredBackBufferHeight = 768;
            graphicsDeviceManager.SynchronizeWithVerticalRetrace = true;// true;//vsync
            graphicsDeviceManager.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;
            graphicsDeviceManager.PreferMultiSampling = true;
            this.IsFixedTimeStep = false;
            
            //this.TargetElapsedTime = TimeSpan.FromSeconds(0.1);
            //Now moving to DatafileWriter only since it can read and write
            DatafileWriter dataFile = new DatafileWriter("client.config.txt");
            
            // Get monitor resolution for defaults
            int monitorWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            int monitorHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            
            // Create DatafileWriter with default values
            if (!File.Exists("client.config.txt"))
            {
                dataFile.Data["width"] = (monitorWidth / 2).ToString();
                dataFile.Data["height"] = (monitorHeight / 2).ToString();
                dataFile.WriteChanges("client.config.txt");
            }

            // Set resolution
            int preferredWidth = dataFile.Data.ContainsKey("width") 
                ? int.Parse(dataFile.Data["width"], System.Globalization.CultureInfo.InvariantCulture)
                : monitorWidth / 2;
                
            int preferredHeight = dataFile.Data.ContainsKey("height")
                ? int.Parse(dataFile.Data["height"], System.Globalization.CultureInfo.InvariantCulture)
                : monitorHeight / 2;

            // Set window mode
            string windowMode = dataFile.Data.ContainsKey("windowmode") ? dataFile.Data["windowmode"].ToLower() : "windowed";
            
            // Configure graphics device before window creation
            graphicsDeviceManager.PreferredBackBufferWidth = preferredWidth;
            graphicsDeviceManager.PreferredBackBufferHeight = preferredHeight;
            graphicsDeviceManager.SynchronizeWithVerticalRetrace = true;
            graphicsDeviceManager.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;
            graphicsDeviceManager.HardwareModeSwitch = true;
            graphicsDeviceManager.IsFullScreen = windowMode == "fullscreen";
            
            if (windowMode == "windowed") 
            {
                Window.IsBorderless = false;
                Window.Position = new Point(
                    (monitorWidth - preferredWidth) / 2,
                    (monitorHeight - preferredHeight) / 2
                );
            }

            // Apply changes once
            graphicsDeviceManager.ApplyChanges();

            // Prevent window from being moved in fullscreen mode
            Window.AllowUserResizing = windowMode == "windowed";
            Window.TextInput += (s, a) => { }; // Prevent focus loss

            // Load other settings
            if (dataFile.Data.ContainsKey("handle"))
                playerHandle = dataFile.Data["handle"];
            if (dataFile.Data.ContainsKey("serverListURL"))
                serverListURL = dataFile.Data["serverListURL"];
            if (dataFile.Data.ContainsKey("showfps"))
                DrawFrameRate = bool.Parse(dataFile.Data["showfps"]);
            if (dataFile.Data.ContainsKey("yinvert"))
                InvertMouseYAxis = bool.Parse(dataFile.Data["yinvert"]);
            if (dataFile.Data.ContainsKey("nosound"))
                NoSound = bool.Parse(dataFile.Data["nosound"]);
            if (dataFile.Data.ContainsKey("pretty"))
                RenderPretty = bool.Parse(dataFile.Data["pretty"]);
            if (dataFile.Data.ContainsKey("light"))
                RenderLight = bool.Parse(dataFile.Data["light"]);
            if (dataFile.Data.ContainsKey("volume"))
                volumeLevel = Math.Max(0,Math.Min(1,float.Parse(dataFile.Data["volume"], System.Globalization.CultureInfo.InvariantCulture)));
            if (dataFile.Data.ContainsKey("sensitivity"))
                mouseSensitivity=Math.Max(0.001f,Math.Min(0.05f,float.Parse(dataFile.Data["sensitivity"], System.Globalization.CultureInfo.InvariantCulture)/1000f));
            if (dataFile.Data.ContainsKey("red_name"))
                redName = dataFile.Data["red_name"].Trim();
            if (dataFile.Data.ContainsKey("blue_name"))
                blueName = dataFile.Data["blue_name"].Trim();
            if (dataFile.Data.ContainsKey("inputlagfix"))
            {
                this.IsFixedTimeStep = bool.Parse(dataFile.Data["inputlagfix"]);
            }

            if (dataFile.Data.ContainsKey("red"))
            {
                Color temp = new Color();
                string[] data = dataFile.Data["red"].Split(',');
                try
                {
                    temp.R = byte.Parse(data[0].Trim());
                    temp.G = byte.Parse(data[1].Trim());
                    temp.B = byte.Parse(data[2].Trim());
                    temp.A = (byte)255;
                }
                catch {
                    Console.WriteLine("Invalid colour values for red");
                }
                if (temp.A != 0)
                {
                    red = temp;
                    customColours = true;
                }
            }

            if (dataFile.Data.ContainsKey("blue"))
            {
                Color temp = new Color();
                string[] data = dataFile.Data["blue"].Split(',');
                try
                {
                    temp.R = byte.Parse(data[0].Trim());
                    temp.G = byte.Parse(data[1].Trim());
                    temp.B = byte.Parse(data[2].Trim());
                    temp.A = (byte)255;
                }
                catch {
                    Console.WriteLine("Invalid colour values for blue");
                }
                if (temp.A != 0)
                {
                    blue = temp;
                    customColours = true;
                }
            }

            //Now to read the key bindings
            if (!File.Exists("keymap.txt"))
            {
                FileStream temp = File.Create("keymap.txt");
                temp.Close();
            }
            dataFile = new DatafileWriter("keymap.txt");
            bool anyChanged = false;
            foreach (string key in dataFile.Data.Keys)
            {
                try
                {
                    Buttons button = (Buttons)Enum.Parse(typeof(Buttons),dataFile.Data[key],true);
                    if (Enum.IsDefined(typeof(Buttons), button))
                    {
                        if (keyBinds.BindKey(button, key, true))
                        {
                            anyChanged = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Enum not defined for " + dataFile.Data[key] + ".");
                    }
                } catch { }
            }

            //If no keys are bound in this manner then create the default set
            if (!anyChanged)
            {
                keyBinds.CreateDefaultSet();
                keyBinds.SaveBinds(dataFile, "keymap.txt");
            }
            graphicsDeviceManager.ApplyChanges();
            base.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public void ResetPropertyBag()
        {
            if (propertyBag != null)
                propertyBag.netClient.Shutdown("");

            propertyBag = new Infiniminer.PropertyBag(this);
            propertyBag.playerHandle = playerHandle;
            propertyBag.serverListURL = serverListURL;
            propertyBag.volumeLevel = volumeLevel;
            propertyBag.mouseSensitivity = mouseSensitivity;
            propertyBag.keyBinds = keyBinds;
            propertyBag.blue = blue;
            propertyBag.red = red;
            propertyBag.blueName = blueName;
            propertyBag.redName = redName;
            msgBuffer = propertyBag.netClient.CreateBuffer();
        }

        protected override void LoadContent()
        {
            // Initialize the property bag.
            ResetPropertyBag();

            // Set the initial state to team selection
            
            ChangeState("Infiniminer.States.TitleState");

            // Play the title music.
            if (!NoSound)
            {
                //songTitle = Content.Load<Song>("tmp");
                //MediaPlayer.Play(songTitle);
                //MediaPlayer.Volume = propertyBag.volumeLevel;
            }
        }
    }
}
