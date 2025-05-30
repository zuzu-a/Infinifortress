using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lidgren.Network;
using Lidgren.Network.Xna;
using Microsoft.Xna.Framework;
using System.Net;

namespace Infiniminer
{
    public class InfiniminerServer
    {
        InfiniminerNetServer netServer = null;
        public BlockType[, ,] blockList = null;    // In game coordinates, where Y points up.
        public BlockType[, ,] blockListFort = null;
        public Int32[, , ,] blockListContent = null;
        public Int32[, ,] blockListHP = null;
        public Int32[, , ,] blockListAttach = null;//attach item/player/action to a tile
        public bool[, ,] allowBlock = null;
        public bool[, ,] allowItem = null;
        public Int32[,] ResearchComplete = null;
        public Int32[,] ResearchProgress = null;
        public Int32[,] artifactActive = null;
        public bool SiegeBuild = false;
        uint NEW_ART_RED = 0;
        uint NEW_ART_BLUE = 0;
        DateTime siege_start = DateTime.Now;
        Player siege_uploader = null;
        DateTime siege_uploadtime = DateTime.Now;
        int siege_blockcount = 0;
        int siege_blockcost = 0;
        public DateTime VoteStart = DateTime.Now;
        public int VoteType = 0;
        public int VoteCreator = 0;
        public int DECAY_TIMER = 1000;
        public int DECAY_TIMER_SPAWN = 3000;
        public int DECAY_TIMER_PS = 2000;
        public bool[] OreMessage = null;
        PlayerTeam[, ,] blockCreatorTeam = null;
        public int[] ResearchChange = { 0, 0, 0 };
        string Filename = "";//threaded saving
        string serverListURL = "http://zuzu-is.online";
        bool physactive = false;
        int scantime = 2;
        const int TOTAL_ARTS = 19;
        const int INHIBITOR_RANGE = 10;
        Dictionary<PlayerTeam, PlayerBase> basePosition = new Dictionary<PlayerTeam, PlayerBase>();
        PlayerBase RedBase;
        PlayerBase BlueBase;
        public TimeSpan[] serverTime = { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero };
        public int timeQueue = 0;
        float delta;
        public DateTime lastTime;
        public int artifactCost = 120;//120 x 5 = 600 gold cost

        int MAPSIZE = 64;
        int REGIONSIZE = 16;
        int REGIONRATIO = 0;//MAPSIZE / REGIONSIZE;
        int NUMREGIONS = 0;//REGIONRATIO * REGIONRATIO * REGIONRATIO;

        Thread physics;
        Thread saving;
        //Thread mechanics;
        Dictionary<NetConnection, Player> playerList = new Dictionary<NetConnection, Player>();
        bool sleeping = true;
        int lavaBlockCount = 0;
        int waterBlockCount = 0;
        uint oreFactor = 10;
        int frameCount = 100;
        uint prevMaxPlayers = 16;
        bool includeLava = true;
        bool includeWater = true;
        bool physicsEnabled = false;
        string levelToLoad = "";
        string greeter = "";
        List<NetConnection> toGreet = new List<NetConnection>();
        Dictionary<string, short> admins = new Dictionary<string, short>(); //Short represents power - 1 for mod, 2 for full admin

        bool[,,] tntExplosionPattern = new bool[0,0,0];
        bool announceChanges = true;

        DateTime lastServerListUpdate = DateTime.Now;
        DateTime lastMapBackup = DateTime.Now;
        List<string> banList = null;
        List<int> tempBan = new List<int>();
        const int CONSOLE_SIZE = 1000;
        List<string> consoleText = new List<string>();
        string consoleInput = "";
        List<string> commandHistory = new List<string>();
        int historyIndex = -1;

        bool keepRunning = true;

        uint teamCashRed = 0;
        uint teamCashBlue = 0;
        int teamOreRed = 0;
        int teamOreBlue = 0;
        uint teamArtifactsRed = 0;
        uint teamArtifactsBlue = 0;
        int[] teamRegeneration = { 0, 0, 0 };
        uint winningCashAmount = 6;

        PlayerTeam winningTeam = PlayerTeam.None;

        bool[, ,] flowSleep = null; //if true, do not calculate this turn

        // Server restarting variables.
        DateTime restartTime = DateTime.Now;
        bool restartTriggered = false;
        
        //Variable handling
        Dictionary<string,bool> varBoolBindings = new Dictionary<string, bool>();
        Dictionary<string,string> varStringBindings = new Dictionary<string, string>();
        Dictionary<string, int> varIntBindings = new Dictionary<string, int>();
        Dictionary<string,string> varDescriptions = new Dictionary<string,string>();
        Dictionary<string, bool> varAreMessage = new Dictionary<string, bool>();

        public void DropItem(Player player, uint ID)
        {
            if (player.Alive)
            {
                if (ID == 1 && player.Ore > 9)
                {
                    uint it = SetItem(ItemType.Ore, player.Position, player.Heading, player.Heading*2, PlayerTeam.None, 0, 0);
                    itemList[it].Content[5] += (int)Math.Floor((double)(player.Ore) / 10) - 1;
                    itemList[it].Scale = 0.5f + (float)(itemList[it].Content[5]) * 0.05f;
                    SendItemScaleUpdate(itemList[it]);

                    player.Ore = 0;
                    SendOreUpdate(player);
                }
                else if (ID == 2 && player.Cash > 9)
                {
                    uint it = SetItem(ItemType.Gold, player.Position, player.Heading, player.Heading*2, PlayerTeam.None, 0, 0);
                    player.Cash -= 10;
                    player.Weight--;

                    while (player.Cash > 9)
                    {
                        itemList[it].Content[5] += 1;
                        player.Cash -= 10;
                        player.Weight--;
                    }
                    itemList[it].Scale = 0.5f + (float)(itemList[it].Content[5]) * 0.1f;
                    SendItemScaleUpdate(itemList[it]);
                    SendCashUpdate(player);
                    SendWeightUpdate(player);
                }
                else if (ID == 3 && player.Content[10] > 0)//artifact drop
                {
                    uint it = SetItem(ItemType.Artifact, player.Position, player.Heading, player.Heading*2, PlayerTeam.None, player.Content[10], 0);
                    itemList[it].Content[10] = player.Content[10];//setting artifacts ID

                    if (player.Content[10] == 13)//explosive
                    {
                        player.StatusEffect[5] = 0;
                    }
                    else if (player.Content[10] == 18)//wings
                    {
                        player.rTouch = DateTime.Now;
                    }
                    player.Content[10] = 0;//artifact slot
                    SendContentSpecificUpdate(player, 10);//tell player he has no artifact now
                    SendPlayerContentUpdate(player, 10);
                }
                else if (ID == 4 && player.Content[11] > 0)//diamond
                {
                    while (player.Content[11] > 0)
                    {
                        uint it = SetItem(ItemType.Diamond, player.Position, player.Heading, player.Heading*2, PlayerTeam.None, 0, 0);
                        player.Content[11]--;
                        player.Weight--;
                    }

                    SendContentSpecificUpdate(player, 11);
                    SendWeightUpdate(player);
                }
            }
        }

        public string DeathMessages(DeathMessage reason)
        {
            /*
             * public const string deathByLava = "was incinerated!";
        public const string deathByElec = "was electrocuted!";
        public const string deathByExpl = "was killed in an explosion!";
        public const string deathByFall = "was killed by gravity!";
        public const string deathByMiss = "was killed by misadventure!";
        public const string deathByCrush = "was crushed!";
        public const string deathByDrown = "drowned!";
        public const string deathBySuic = "has commited pixelcide!";
             * */
            string msg = "";
            switch (reason)
            {
                case DeathMessage.deathByCrush:
                    return "was crushed!";
                case DeathMessage.deathByDrown:
                    return "drowned!";
                case DeathMessage.deathByElec:
                    return "was electrocuted!";
                case DeathMessage.deathByEngineer:
                    return "changed to Engineer.";
                case DeathMessage.deathByExpl:
                    return "exploded!";
                case DeathMessage.deathByFall:
                    return "was killed by gravity!";
                case DeathMessage.deathByLava:
                    return "was incinerated!";
                case DeathMessage.deathByMiss:
                    return "was killed by misadventure!";
                case DeathMessage.deathByMiner:
                    return "changed to Miner.";
                case DeathMessage.deathByProspector:
                    return "changed to Prospector.";
                case DeathMessage.deathBySapper:
                    return "changed to Sapper.";
                case DeathMessage.deathBySuic:
                    return "commited pixelcide!";
                case DeathMessage.deathByTeamSwitchBlue:
                    return "switched to the Blue team!";
                case DeathMessage.deathByTeamSwitchRed:
                    return "switched to the Red team!";
                case DeathMessage.Silent:
                    return "";

            }
            return msg;
        }

        public void Player_Dead(Player player, DeathMessage reason)
        {//player death

            if (player.LastHit < (DateTime.Now - TimeSpan.FromSeconds(15)) || reason == DeathMessage.deathByLava || reason == DeathMessage.deathByMiss || reason == DeathMessage.deathByCrush || reason == DeathMessage.deathByDrown || reason == DeathMessage.deathByElec || reason == DeathMessage.deathByFall)
            {

                ConsoleWrite("PLAYER_DEAD: " + player.Handle + " " + reason);
                player.respawnExpired = false;

                if (player.StatusEffect[4] == 0)
                {
                    player.deathCount += 50;

                    if (player.Position.Z > 32 && player.Team == PlayerTeam.Blue)//MAPSIZE/2
                    {
                        player.deathCount += 50;
                      //  ConsoleWrite("deathprot");
                    }
                    else if (player.Position.Z < 32 && player.Team == PlayerTeam.Red)
                    {
                        player.deathCount += 50;
                      //  ConsoleWrite("deathprot");
                    }

                    //ConsoleWrite("respawn time: " + ((player.deathCount / 40) * 3));
                    if ((player.deathCount / 40) * 3 < 25)
                        player.respawnTimer = DateTime.Now + TimeSpan.FromSeconds((player.deathCount / 40) * 3);
                    else
                        player.respawnTimer = DateTime.Now + TimeSpan.FromSeconds(25);
                }
                else
                {
                    player.respawnTimer = DateTime.Now + TimeSpan.FromSeconds(5);
                }
                for (int a = 0; a < 20; a++)
                {
                    if (a == 6)
                        continue;

                    player.StatusEffect[a] = 0;
                }
                if (player.Radar > 0)//remove from radar
                {
                    player.Content[1] = 0;
                    SendPlayerContentUpdate(player, 1);
                }
                if (player.Ore > 9)
                {
                    uint it = SetItem(ItemType.Ore, player.Position, Vector3.Zero, new Vector3((float)(randGen.NextDouble() - 0.5) * 2, (float)(randGen.NextDouble() * 1.5), (float)(randGen.NextDouble() - 0.5) * 2), PlayerTeam.None, 0, 0);
                    itemList[it].Content[5] += (int)Math.Floor((double)(player.Ore) / 10) - 1;
                    itemList[it].Scale = 0.5f + (float)(itemList[it].Content[5]) * 0.05f;
                    SendItemScaleUpdate(itemList[it]);
                }

                if (player.Cash > 9)
                {
                    uint it = SetItem(ItemType.Gold, player.Position, Vector3.Zero, new Vector3((float)(randGen.NextDouble() - 0.5) * 2, (float)(randGen.NextDouble() * 1.5), (float)(randGen.NextDouble() - 0.5) * 2), PlayerTeam.None, 0, 0);
                    itemList[it].Content[5] += (int)Math.Floor((double)(player.Cash) / 10) - 1;
                    itemList[it].Scale = 0.5f + (float)(itemList[it].Content[5]) * 0.1f;
                    SendItemScaleUpdate(itemList[it]);
                }

                if (player.Content[10] > 0)
                {
                    uint it = SetItem(ItemType.Artifact, player.Position, Vector3.Zero, new Vector3((float)(randGen.NextDouble() - 0.5) * 2, (float)(randGen.NextDouble() * 1.5), (float)(randGen.NextDouble() - 0.5) * 2), PlayerTeam.None, player.Content[10], 0);
                    itemList[it].Content[10] = player.Content[10];//setting artifacts ID

                    player.Content[10] = 0;//artifact slot

                    SendContentSpecificUpdate(player, 10);//tell player he has no artifact now
                    SendPlayerContentUpdate(player, 10);
                }

                while (player.Content[11] > 0)
                {
                    uint it = SetItem(ItemType.Diamond, player.Position, Vector3.Zero, new Vector3((float)(randGen.NextDouble() - 0.5) * 2, (float)(randGen.NextDouble() * 1.5), (float)(randGen.NextDouble() - 0.5) * 2), PlayerTeam.None, 0, 0);
                    player.Content[11]--;
                }

                if (player.Class == PlayerClass.Prospector && player.Content[5] > 0)
                {
                    player.Content[5] = 0;
                    SendPlayerContentUpdate(player, 5);
                }
                player.Ore = 0;
                player.Cash = 0;//gold
                player.Weight = 0;
                player.Health = 0;

                player.Content[2] = 0;
                player.Content[3] = 0;
                player.Content[4] = 0;
                player.Content[5] = 0;//ability slot
                player.Content[11] = 0;//diamond slots

                SendContentSpecificUpdate(player, 11);

                player.rSpeedCount = 0;

                if (player.Alive == true)//avoid sending multiple death threats
                {
                    if (reason != DeathMessage.Silent)
                    {
                        NetBuffer msgBuffer = netServer.CreateBuffer();
                        msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                        msgBuffer.Write((byte)(player.Team == PlayerTeam.Red ? ChatMessageType.SayRedTeam : ChatMessageType.SayBlueTeam));
                        msgBuffer.Write(player.Handle + " " + DeathMessages(reason));
                        if (reason == DeathMessage.deathByMiner || reason == DeathMessage.deathByProspector || reason == DeathMessage.deathBySapper || reason == DeathMessage.deathByEngineer)
                        {
                            foreach (NetConnection netConn in playerList.Keys)
                                if (netConn.Status == NetConnectionStatus.Connected)
                                    if (playerList[netConn].Team == player.Team)
                                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
                        }
                        else
                        {
                            foreach (NetConnection netConn in playerList.Keys)
                                if (netConn.Status == NetConnectionStatus.Connected)
                                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
                        }
                    }
                    player.Alive = false;
                    SendResourceUpdate(player);
                    SendPlayerDead(player);
                }

                if (player.HealthMax > 0 && player.Team != PlayerTeam.None)
                {
                    SendPlayerRespawn(player);//allow this player to instantly respawn
                }
            }
            else
            {
                SendServerMessageToPlayer("You are still in combat!", player.NetConn);
            }
        }
        public void Player_Dead(Player player, string reason)
        {//server side can provide strings and doesnt check if legal

            player.respawnExpired = false;
            if (reason != "")
            {
                NetBuffer msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                msgBuffer.Write((byte)(player.Team == PlayerTeam.Red ? ChatMessageType.SayRedTeam : ChatMessageType.SayBlueTeam));
                msgBuffer.Write(player.Handle + " " + reason);
                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
            }

            ConsoleWrite("PLAYER_DEAD: " + player.Handle + " " + reason);

            if (player.StatusEffect[4] == 0)
            {
                player.deathCount += 50;

                if (player.Position.Z > 32 && player.Team == PlayerTeam.Blue)//MAPSIZE/2
                {
                    player.deathCount += 50;
                   // ConsoleWrite("deathprot");
                }
                else if (player.Position.Z < 32 && player.Team == PlayerTeam.Red)
                {
                    player.deathCount += 50;
                  //  ConsoleWrite("deathprot");
                }

                //ConsoleWrite("respawn time: " + ((player.deathCount / 40) * 3));
                if ((player.deathCount / 40) * 3 < 25)
                    player.respawnTimer = DateTime.Now + TimeSpan.FromSeconds((player.deathCount / 40) * 3);
                else
                    player.respawnTimer = DateTime.Now + TimeSpan.FromSeconds(25);
            }
            else
            {
                player.respawnTimer = DateTime.Now + TimeSpan.FromSeconds(5);
            }
            
            for (int a = 0; a < 20; a++)
            {
                if (a == 6)
                    continue;

                player.StatusEffect[a] = 0;
            }
            if (player.Radar > 0)//remove from radar
            {
                player.Content[1] = 0;
                SendPlayerContentUpdate(player, 1);
            }
            if (player.Ore > 9)
            {
                uint it = SetItem(ItemType.Ore, player.Position, Vector3.Zero, new Vector3((float)(randGen.NextDouble() - 0.5) * 2, (float)(randGen.NextDouble() * 1.5), (float)(randGen.NextDouble() - 0.5) * 2), PlayerTeam.None, 0, 0);
                itemList[it].Content[5] += (int)Math.Floor((double)(player.Ore) / 10) - 1;
                itemList[it].Scale = 0.5f + (float)(itemList[it].Content[5]) * 0.05f;
                SendItemScaleUpdate(itemList[it]);
            }

            if (player.Cash > 9)
            {
                uint it = SetItem(ItemType.Gold, player.Position, Vector3.Zero, new Vector3((float)(randGen.NextDouble() - 0.5) * 2, (float)(randGen.NextDouble() * 1.5), (float)(randGen.NextDouble() - 0.5) * 2), PlayerTeam.None, 0, 0);
                itemList[it].Content[5] += (int)Math.Floor((double)(player.Cash) / 10) - 1;
                itemList[it].Scale = 0.5f + (float)(itemList[it].Content[5]) * 0.1f;
                SendItemScaleUpdate(itemList[it]);
            }

            if (player.Content[10] > 0)
            {
                uint it = SetItem(ItemType.Artifact, player.Position, Vector3.Zero, new Vector3((float)(randGen.NextDouble() - 0.5) * 2, (float)(randGen.NextDouble() * 1.5), (float)(randGen.NextDouble() - 0.5) * 2), PlayerTeam.None, player.Content[10], 0);
                itemList[it].Content[10] = player.Content[10];//setting artifacts ID

                player.Content[10] = 0;//artifact slot

                SendContentSpecificUpdate(player, 10);//tell player he has no artifact now
                SendPlayerContentUpdate(player, 10);
            }

            while (player.Content[11] > 0)
            {
                uint it = SetItem(ItemType.Diamond, player.Position, Vector3.Zero, new Vector3((float)(randGen.NextDouble() - 0.5) * 2, (float)(randGen.NextDouble() * 1.5), (float)(randGen.NextDouble() - 0.5) * 2), PlayerTeam.None, 0, 0);
                player.Content[11]--;
            }

            if (player.Class == PlayerClass.Prospector && player.Content[5] > 0)
            {
                player.Content[5] = 0;
                SendPlayerContentUpdate(player, 5);
            }
            player.Ore = 0;
            player.Cash = 0;//gold
            player.Weight = 0;
            player.Health = 0;

            player.Content[2] = 0;
            player.Content[3] = 0;
            player.Content[4] = 0;
            player.Content[5] = 0;//ability slot
            player.Content[11] = 0;//diamond slots

            SendContentSpecificUpdate(player, 11);

            player.rSpeedCount = 0;

            if (player.Alive == true)//avoid sending multiple death threats
            {
                player.Alive = false;
                SendResourceUpdate(player);
                SendPlayerDead(player);
            }

            if (player.HealthMax > 0 && player.Team != PlayerTeam.None)
            {
                SendPlayerRespawn(player);//allow this player to instantly respawn
            }
        }

        public void Auth_Refuse(Player pl)
        {
            if (pl.rTime < DateTime.Now)
            {
                pl.rTime = DateTime.Now + TimeSpan.FromSeconds(1);
                if (pl.rUpdateCount > 25 + pl.NetConn.AverageRoundtripTime * 40)//20 is easily pushed while moving and triggering levers
                {
                    ConsoleWrite("PLAYER_DEAD_UPDATE_FLOOD: " + pl.Handle + "@" + pl.rUpdateCount + " ROUNDTRIP MS:" + pl.NetConn.AverageRoundtripTime*1000);
                    Player_Dead(pl, "is a bit far from the server!");
                    
                }
                else if(pl.rSpeedCount > 10.0f && pl.Alive && pl.respawnTimer < DateTime.Now)//7
                {
                    ConsoleWrite("PLAYER_DEAD_TOO_FAST: " + pl.Handle + "@"+pl.rSpeedCount+" ROUNDTRIP MS:" + pl.NetConn.AverageRoundtripTime*1000);
                    Player_Dead(pl, "was too fast for the server to keep up!");
                }
                else if (pl.rCount > 15 && pl.Alive)
                {
                    ConsoleWrite("PLAYER_DEAD_ILLEGAL_MOVEMENT: " + pl.Handle + "@" + pl.rCount + " ROUNDTRIP MS:" + pl.NetConn.AverageRoundtripTime*1000);
                    Player_Dead(pl, "had a few too many close calls!");
                }
                pl.rCount = 0;
                pl.rUpdateCount = 0;
                pl.rSpeedCount = 0;
            }
        }

        public double Dist_Auth(Vector3 x, Vector3 y)
        {
            float dx = y.X - x.X;
            float dz = y.Z - x.Z;
            double dist = Math.Sqrt(dx * dx + dz * dz);
            return dist;
        }

        public Vector3 Auth_Position(Vector3 pos,Player pl, bool check)//check boundaries and legality of action
        {
            BlockType testpoint = BlockAtPoint(pos);

            if (testpoint == BlockType.None || testpoint == BlockType.Fire || testpoint == BlockType.Vacuum || testpoint == BlockType.Water || testpoint == BlockType.Lava || testpoint == BlockType.StealthBlockB && pl.Team == PlayerTeam.Blue || testpoint == BlockType.TransBlue && pl.Team == PlayerTeam.Blue || testpoint == BlockType.StealthBlockR && pl.Team == PlayerTeam.Red || testpoint == BlockType.TransRed && pl.Team == PlayerTeam.Red)
            {//check if player is not in wall
               //falldamage

                //if (testpoint == BlockType.Fire)
                //{
                //    //burn
                //    if (pl.Health > 1)
                //    {
                //        pl.Health = pl.Health - 10;
                //        if (pl.Health == 0)
                //        {
                //            pl.Weight = 0;
                //            pl.Alive = false;

                //            SendResourceUpdate(pl);
                //            SendPlayerDead(pl);
                //            ConsoleWrite(pl.Handle + " died in the fire.");
                //        }
                //    }
                //}
                if (check)
                {
                    //testpoint = BlockAtPoint(pos - Vector3.UnitY * 1.5f);
                   
                    if (pl.Position.Y > (pos.Y + 0.12f) && pl.rTouching == false && pl.Falling == false)
                    {
                        pl.CheckTouchTime = DateTime.Now;//starting to fall
                        pl.Falling = true;
                    }

                    bool ladder = false;
                    if (BlockAtPoint(pos + new Vector3(-0.5f, 0f, 0f)) == BlockType.Ladder || BlockAtPoint(pos + new Vector3(0.5f, 0f, 0f)) == BlockType.Ladder || BlockAtPoint(pos + new Vector3(0f, 0f, -0.5f)) == BlockType.Ladder || BlockAtPoint(pos + new Vector3(0f, 0f, 0.5f)) == BlockType.Ladder || BlockAtPoint(pos + new Vector3(-0.5f, -1.0f, 0f)) == BlockType.Ladder || BlockAtPoint(pos + new Vector3(0.5f, -1.0f, 0f)) == BlockType.Ladder || BlockAtPoint(pos + new Vector3(0f, -1.0f, -0.5f)) == BlockType.Ladder || BlockAtPoint(pos + new Vector3(0f, -1.0f, 0.5f)) == BlockType.Ladder)
                    {
                        //pl.CheckTouchTime = DateTime.Now;//negator
                        //pl.Falling = false;
                        //if (pl.Falling)
                            //ConsoleWrite("ladder");
                        //sketchy ladder protection
                        ladder = true;
                    }

                    if(pl.Falling)
                    if (BlockAtPoint(pos - Vector3.UnitY * 1.8f) == BlockType.Water)
                    {//water protection
                        pl.CheckTouchTime = DateTime.Now;
                    }

                    if (pl.Position.Y - 1.0f > 0.0f && pl.Position.X > 0 && pl.Position.Z > 0 && pl.Position.X < MAPSIZE - 1 && pl.Position.Y < MAPSIZE - 1 && pl.Position.Z < MAPSIZE - 1)
                    {
                        //if (blockList[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z] == BlockType.None && blockListContent[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 11] > 0)
                        //{
                        //    if (pl.Health > 1)//blockListContent[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 12] / 50)
                        //    {
                        //        pl.Health -= 1;// (uint)blockListContent[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 12] / 50;
                        //    }
                        //    else
                        //    {
                        //        Player_Dead(pl, "got gassed!");
                        //    }
                        //}
                        if (pl.Content[10] == 12)//bog
                        {
                            if(randGen.Next(4) == 1)
                            if (blockList[(int)pl.Position.X, (int)(pl.Position.Y - 1.7f), (int)pl.Position.Z] == BlockType.Dirt)
                            {
                                SetBlock((ushort)pl.Position.X, (ushort)(pl.Position.Y - 1.7f), (ushort)pl.Position.Z, BlockType.Mud, PlayerTeam.None);
                                blockListContent[(int)pl.Position.X, (int)(pl.Position.Y - 1.7f), (int)pl.Position.Z, 1] = (byte)BlockType.Dirt;
                                blockListContent[(int)pl.Position.X, (int)(pl.Position.Y - 1.7f), (int)pl.Position.Z, 0] = 1;//perma mud
                            }
                        }
                        if (pl.StatusEffect[7] > 0)
                        {
                            if (blockList[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z] == BlockType.Water)
                            {
                                pl.StatusEffect[7] = 0;
                                SendServerMessageToPlayer("The flame ceases!",pl.NetConn);
                            }
                        }
                        if (blockListAttach[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 0] > 0)
                        {

                            uint ID = (uint)blockListAttach[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 1];

                            if (itemList.ContainsKey(ID))
                            {

                                if (itemList[ID].Type == ItemType.Spikes)
                                {
                                    if(itemList[ID].Content[1] == 0)
                                    if (pl.Health > 30)
                                    {
                                        pl.Health -= 30;
                                        if (pl.Team == PlayerTeam.Red)
                                            DebrisEffectAtPoint(pl.Position.X, pl.Position.Y, pl.Position.Z, BlockType.SolidRed, 10 + (int)(30));
                                        else if (pl.Team == PlayerTeam.Blue)
                                            DebrisEffectAtPoint(pl.Position.X, pl.Position.Y, pl.Position.Z, BlockType.SolidBlue, 10 + (int)(30));

                                        itemList[ID].Content[1] = 20;
                                        PlaySound(InfiniminerSound.Death, pl.Position);
                                        SendHealthUpdate(pl);
                                    }
                                    else
                                    {
                                        PlaySound(InfiniminerSound.Death, pl.Position);
                                        Player_Dead(pl, DeathMessage.deathByFall);
                                    }
                                }
                                else if (itemList[ID].Type != ItemType.Spikes)
                                {
                                    //blockListAttach[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 0] = 0;
                                    //if (blockList[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z] == BlockType.None)
                                    //{
                                    //    blockListContent[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 11] = 1;
                                    //    blockListContent[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 12] = 2000;
                                    //    //DebrisEffectAtPoint((int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, BlockType.Highlight, 1);
                                    //}
                                    itemList[ID].Disposing = true;
                                }
                            }
                            else
                            {
                                blockListAttach[(int)pl.Position.X, (int)(pl.Position.Y - 1.2f), (int)pl.Position.Z, 0] = 0;
                            }
                        }
                    }
                    if (pl.Content[10] != 18)//wings
                    if (BlockAtPoint(pos - Vector3.UnitY * 1.7f) != BlockType.None || BlockAtPoint((pos + new Vector3(-0.1f, 0f, 0f)) - Vector3.UnitY * 1.7f) != BlockType.None || BlockAtPoint((pos + new Vector3(0.1f, 0f, 0f)) - Vector3.UnitY * 1.7f) != BlockType.None || BlockAtPoint((pos + new Vector3(0f, 0f, -0.1f)) - Vector3.UnitY * 1.7f) != BlockType.None || BlockAtPoint((pos + new Vector3(0f, 0f, 0.1f)) - Vector3.UnitY * 1.7f) != BlockType.None || ladder)//touching ground or substance
                    {
                        //convert time to damage
                        // last touch time - datetime.now = time in air, but update doesnt fire all the time, so it must be from the last update
                        // last touch time - last update time - datetime.now = time in air
                        if (pl.rTouching == false)
                        {
                            pl.rTouching = true;
                            pl.Falling = false;
                            TimeSpan damage = DateTime.Now - pl.CheckTouchTime;
                            double dmg = 0;
                            dmg = damage.TotalMilliseconds;
                            dmg = dmg / 55;

                            if (artifactActive[(byte)pl.Team, 18] > 0)
                            {
                                dmg = dmg / artifactActive[(byte)pl.Team, 18] + 1;
                            }

                            if (dmg > 10)
                            {
                                dmg *= 1.5;
                                if (pl.Content[10] == 9)//fall resist
                                {
                                    dmg = dmg / 2;
                                }
                                
                                pl.FallBuffer -= (int)dmg;
                                //ConsoleWrite("reduced:" + (int)dmg + " to " + pl.FallBuffer);

                                if (pl.FallBuffer < 0)
                                {
                                    if (pl.Health > dmg)
                                    {
                                        pl.FallBuffer = 10;
                                       // ConsoleWrite("hpdmg from server:" + dmg);
                                        pl.Health -= (uint)dmg;
                                        if (pl.Team == PlayerTeam.Red)
                                            DebrisEffectAtPoint((int)(pl.Position.X), (int)(pl.Position.Y), (int)(pl.Position.Z), BlockType.SolidRed, 10 + (int)(dmg));
                                        else if (pl.Team == PlayerTeam.Blue)
                                            DebrisEffectAtPoint((int)(pl.Position.X), (int)(pl.Position.Y), (int)(pl.Position.Z), BlockType.SolidBlue, 10 + (int)(dmg));

                                        if (dmg > 20)
                                        {
                                            PlaySoundForEveryoneElse(InfiniminerSound.GroundHit, pl.Position,pl);
                                        }
                                        else
                                        {
                                            PlaySound(InfiniminerSound.GroundHit, pl.Position);
                                        }
                                        SendHealthUpdate(pl);
                                    }
                                    else
                                    {
                                        PlaySound(InfiniminerSound.Death, pl.Position);
                                        Player_Dead(pl, DeathMessage.deathByFall);
                                    }
                                }
                                //time to check damage
                            }
                        }
                    }
                    else
                    {
                        //ConsoleWrite("notouch");
                        if (pl.rTouching == true)
                        {
                            pl.rTouching = false;
                            pl.rTouch = DateTime.Now;
                            pl.CheckTouchTime = DateTime.Now;
                        }
                        else if (pl.Content[10] == 18)//wings
                        {
                            
                        }
                        else if (pl.rTouch < DateTime.Now - TimeSpan.FromSeconds(7))
                        {
                            Player_Dead(pl, DeathMessage.deathByMiss);
                        }

                        //if (pl.CheckTouchTime < DateTime.Now + TimeSpan.FromSeconds(0.5))
                        //{
                        //    if (pl.rTouch > DateTime.Now + TimeSpan.FromSeconds(10))
                        //    {
                        //        Player_Dead(pl, DeathMessage.deathByMiss);
                        //    }
                        //    pl.CheckTouchTime = DateTime.Now;//last movement update 
                        //}
                        //else
                        //{
                        //    pl.CheckTouchTime = DateTime.Now;//last movement update 
                        //}
                    }
                    pl.rSpeedCount += Dist_Auth(pos, pl.Position);
                    pl.rUpdateCount += 1;
                }
                Auth_Refuse(pl);
            }
            else
            {
                if (pl.Alive)
                {
                    
                    //pl.Ore = 0;//should be calling death function for player
                    //pl.Cash = 0;
                    //pl.Weight = 0;
                    //pl.Health = 0;
                    //pl.Alive = false;

                    //SendResourceUpdate(pl);
                    //SendPlayerDead(pl);

                   // ConsoleWrite("refused" + pl.Handle + " " + pos.X + "/" + pos.Y + "/" + pos.Z);
                    ushort x = (ushort)pos.X;
                    ushort y = (ushort)pos.Y;
                    ushort z = (ushort)pos.Z;

                    
                    if (x < 0 || y < 0 || z < 0 || x >= MAPSIZE || y >= MAPSIZE || z >= MAPSIZE)
                    {
                        Auth_Refuse(pl);
                        pl.rCount += 1;
                        return pl.Position;
                    }

                    if (testpoint == BlockType.TrapB)
                        if (pl.Team == PlayerTeam.Red)//destroy trap block
                        {
                            pl.rSpeedCount += Dist_Auth(pos, pl.Position);
                            pl.rUpdateCount += 1;

                            Auth_Refuse(pl);
                            SetBlockDebris(x, y, z, BlockType.None, PlayerTeam.None);
                            return (pos);
                        }
                    else if (testpoint == BlockType.TrapR)
                        if (pl.Team == PlayerTeam.Blue)//destroy trap block
                        {
                            pl.rSpeedCount += Dist_Auth(pos, pl.Position);
                            pl.rUpdateCount += 1;

                            Auth_Refuse(pl);
                            SetBlockDebris(x, y, z, BlockType.None, PlayerTeam.None);
                            return (pos);
                        }

                    //ConsoleWrite("sync sent blocks back");
                    //needs to be a second later to prevent instakill
                    //SetBlockForPlayer(x, y, z, blockList[x, y, z], blockCreatorTeam[x, y, z], pl);
                    Auth_Refuse(pl);
                    pl.rCount += 1;

                    return pl.Position;
                }
                else//player is dead, return position silent
                {
                    return pl.Position;
                }
            }

            //if (Distf(pl.Position, pos) > 0.35)
            //{   //check that players last update is not further than it should be
            //    ConsoleWrite("refused" + pl.Handle + " speed:" + Distf(pl.Position, pos));
            //    //should call force update player position
            //    return pos;// pl.Position;
            //}
            //else
            //{
            //    return pos;
            //}

            return pos;
        }
        public Vector3 Auth_Heading(Vector3 head)//check boundaries and legality of action
        {
            return head;
        }

        public void varBindingsInitialize()
        {
            //Bool bindings
            varBind("tnt", "TNT explosions", false, true);
            varBind("stnt", "Spherical TNT explosions", true, true);
            varBind("sspreads", "Lava spreading via shock blocks", true, false);
            varBind("roadabsorbs", "Letting road blocks above lava absorb it", true, false);
            varBind("autoban", "Automatic banning for spammers", true, false);
            varBind("minelava", "Lava pickaxe mining", false, false);
            varBind("warnings", "Warnings before temporary ban", 4);
            varBind("autosave", "Autosave frequency", 4);
            //***New***
            varBind("public", "Server publicity", true, false);
            varBind("sandbox", "Sandbox mode", true, false);
            varBind("siege", "Siege mode", 0);
            varBind("decaytimer", "Decay time for artifacts", 1000);
            varBind("spawndecaytimer", "Decay time for new artifacts", 3000);
            varBind("decaytimerps", "Decay time for powerstones", 2000);
   
            varBind("enforceteams", "Players may not switch to the team with more players", false, false);
            varBind("voting", "Players can vote for restarts", true, false);
            //Announcing is a special case, as it will never announce for key name announcechanges
            varBind("announcechanges", "Toggles variable changes being announced to clients", true, false);

            //String bindings
            varBind("name", "Server name as it appears on the server browser", "Unnamed Server");
            varBind("greeter", "The message sent to new players", "");
            varBind("serverListURL", "The URL endpoint to use as a server list", "http://zuzu-is.online");

            //Int bindings
            varBind("maxplayers", "Maximum player count", 6);
            varBind("explosionradius", "The radius of spherical tnt explosions", 3);
        }

        public void varBind(string name, string desc, bool initVal, bool useAre)
        {
            varBoolBindings[name] = initVal;
            varDescriptions[name] = desc;
            /*if (varBoolBindings.ContainsKey(name))
                varBoolBindings[name] = initVal;
            else
                varBoolBindings.Add(name, initVal);

            if (varDescriptions.ContainsKey(name))
                varDescriptions[name] = desc;
            else
                varDescriptions.Add(name, desc);*/

            varAreMessage[name] = useAre;
        }

        public void varBind(string name, string desc, string initVal)
        {
            varStringBindings[name] = initVal;
            varDescriptions[name] = desc;
            /*
            if (varStringBindings.ContainsKey(name))
                varStringBindings[name] = initVal;
            else
                varStringBindings.Add(name, initVal);

            if (varDescriptions.ContainsKey(name))
                varDescriptions[name] = desc;
            else
                varDescriptions.Add(name, desc);*/
        }

        public void varBind(string name, string desc, int initVal)
        {
            varIntBindings[name] = initVal;
            varDescriptions[name] = desc;
            /*if (varDescriptions.ContainsKey(name))
                varDescriptions[name] = desc;
            else
                varDescriptions.Add(name, desc);*/
        }

        public bool varChangeCheckSpecial(string name)
        {
            switch (name)
            {
                case "maxplayers":
                    //Check if smaller than player count
                    if (varGetI(name) < playerList.Count)
                    {
                        //Bail, set to previous value
                        varSet(name, (int)prevMaxPlayers,true);
                        return false;
                    }
                    else
                    {
                        prevMaxPlayers = (uint)varGetI(name);
                        netServer.Configuration.MaxConnections = varGetI(name);
                    }
                    break;
                case "explosionradius":
                    CalculateExplosionPattern();
                    break;
                case "greeter":
                    /*PropertyBag _P = new PropertyBag(new InfiniminerGame(new string[]{}));
                    string[] format = _P.ApplyWordrwap(varGetS("greeter"));
                    */
                    greeter = varGetS("greeter");
                    break;
            }
            return true;
        }

        public bool varGetB(string name)
        {
            if (varBoolBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
                return varBoolBindings[name];
            else
                return false;
        }

        public string varGetS(string name)
        {
            if (varStringBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
                return varStringBindings[name];
            else
                return "";
        }

        public int varGetI(string name)
        {
            if (varIntBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
                return varIntBindings[name];
            else
                return -1;
        }

        public int varExists(string name)
        {
            if (varDescriptions.ContainsKey(name))
                if (varBoolBindings.ContainsKey(name))
                    return 1;
                else if (varStringBindings.ContainsKey(name))
                    return 2;
                else if (varIntBindings.ContainsKey(name))
                    return 3;
            return 0;
        }

        public void varSet(string name, bool val)
        {
            varSet(name, val, false);
        }

        public void varSet(string name, bool val, bool silent)
        {
            if (varBoolBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
            {
                varBoolBindings[name] = val;
                string enabled = val ? "enabled!" : "disabled.";
                if (name!="announcechanges"&&!silent)
                    MessageAll(varDescriptions[name] + (varAreMessage[name] ? " are " + enabled : " is " + enabled));
                if (!silent)
                {
                    varReportStatus(name, false);
                    varChangeCheckSpecial(name);
                }
            }
            else
                ConsoleWrite("Variable \"" + name + "\" does not exist!");
        }

        public void varSet(string name, string val)
        {
            varSet(name, val, false);
        }

        public void varSet(string name, string val, bool silent)
        {
            if (varStringBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
            {
                varStringBindings[name] = val;
                if (!silent)
                {
                    varReportStatus(name);
                    varChangeCheckSpecial(name);
                }
            }
            else
                ConsoleWrite("Variable \"" + name + "\" does not exist!");
        }

        public void varSet(string name, int val)
        {
            varSet(name, val, false);
        }

        public void varSet(string name, int val, bool silent)
        {
            if (varIntBindings.ContainsKey(name) && varDescriptions.ContainsKey(name))
            {
                varIntBindings[name] = val;
                if (!silent)
                {
                    MessageAll(name + " = " + val.ToString());
                    varChangeCheckSpecial(name);
                }
            }
            else
                ConsoleWrite("Variable \"" + name + "\" does not exist!");
        }

        public string varList()
        {
            return varList(false);
        }

        private void varListType(ICollection<string> keys, string naming)
        {
            
            const int lineLength = 3;
            if (keys.Count > 0)
            {
                ConsoleWrite(naming);
                int i = 1;
                string output = "";
                foreach (string key in keys)
                {
                    if (i == 1)
                    {
                        output += "\t" + key;
                    }
                    else if (i >= lineLength)
                    {
                        output += ", " + key;
                        ConsoleWrite(output);
                        output = "";
                        i = 0;
                    }
                    else
                    {
                        output += ", " + key;
                    }
                    i++;
                }
                if (i > 1)
                    ConsoleWrite(output);
            }
        }

        public string varList(bool autoOut)
        {
            if (!autoOut)
            {
                string output = "";
                int i = 0;
                foreach (string key in varBoolBindings.Keys)
                {
                    if (i == 0)
                        output += key;
                    else
                        output += "," + key;
                    i++;
                }
                foreach (string key in varStringBindings.Keys)
                {
                    if (i == 0)
                        output += "s " + key;
                    else
                        output += ",s " + key;
                    i++;
                }
                return output;
            }
            else
            {
                varListType((ICollection<string>)varBoolBindings.Keys, "Boolean Vars:");
                varListType((ICollection<string>)varStringBindings.Keys, "String Vars:");
                varListType((ICollection<string>)varIntBindings.Keys, "Int Vars:");

                /*ConsoleWrite("String count: " + varStringBindings.Keys.Count);
                outt = new string[varStringBindings.Keys.Count];
                varStringBindings.Keys.CopyTo(outt, 0);
                varListType(outt, "String Vars:");

                ConsoleWrite("Int count: " + varIntBindings.Keys.Count);
                outt = new string[varIntBindings.Keys.Count];
                varIntBindings.Keys.CopyTo(outt, 0);
                varListType(outt, "Integer Vars:");*/
                /*if (varStringBindings.Count > 0)
                {
                    ConsoleWrite("String Vars:");
                    int i = 1;
                    string output = "";
                    foreach (string key in varStringBindings.Keys)
                    {
                        if (i == 1)
                        {
                            output += key;
                        }
                        else if (i >= lineLength)
                        {
                            output += "," + key;
                            ConsoleWrite(output);
                            output = "";
                        }
                        else
                        {
                            output += "," + key;
                        }
                        i++;
                    }
                }
                if (varIntBindings.Count > 0)
                {
                    ConsoleWrite("Integer Vars:");
                    int i = 1;
                    string output = "";
                    foreach (string key in varIntBindings.Keys)
                    {
                        if (i == 1)
                        {
                            output += "\t"+key;
                        }
                        else if (i >= lineLength)
                        {
                            output += "," + key;
                            ConsoleWrite(output);
                            output = "";
                        }
                        else
                        {
                            output += "," + key;
                        }
                        i++;
                    }
                }*/
                return "";
            }
        }

        public void varReportStatus(string name)
        {
            varReportStatus(name, true);
        }

        public void varReportStatus(string name, bool full)
        {
            if (varDescriptions.ContainsKey(name))
            {
                if (varBoolBindings.ContainsKey(name))
                {
                    ConsoleWrite(name + " = " + varBoolBindings[name].ToString());
                    if (full)
                        ConsoleWrite(varDescriptions[name]);
                    return;
                }
                else if (varStringBindings.ContainsKey(name))
                {
                    ConsoleWrite(name + " = " + varStringBindings[name]);
                    if (full)
                        ConsoleWrite(varDescriptions[name]);
                    return;
                }
                else if (varIntBindings.ContainsKey(name))
                {
                    ConsoleWrite(name + " = " + varIntBindings[name]);
                    if (full)
                        ConsoleWrite(varDescriptions[name]);
                    return;
                }
            }
            ConsoleWrite("Variable \"" + name + "\" does not exist!");
        }

        public string varReportStatusString(string name, bool full)
        {
            if (varDescriptions.ContainsKey(name))
            {
                if (varBoolBindings.ContainsKey(name))
                {
                    return name + " = " + varBoolBindings[name].ToString();
                }
                else if (varStringBindings.ContainsKey(name))
                {
                    return name + " = " + varStringBindings[name];
                }
                else if (varIntBindings.ContainsKey(name))
                {
                    return name + " = " + varIntBindings[name];
                }
            }
            return "";
        }

        public InfiniminerServer()
        {
            CrossPlatformServices.Instance.ConfigureConsole(80, CONSOLE_SIZE);
            Console.WriteLine("INFINIMINER SERVER " + Defines.INFINIMINER_VERSION);
            Console.WriteLine("Type 'help' for a list of commands.");
            Console.Write("> ");

            physics = new Thread(new ThreadStart(this.DoPhysics));
            physics.Priority = ThreadPriority.Normal;
            physics.Start();

            REGIONRATIO = MAPSIZE / REGIONSIZE;
            NUMREGIONS = REGIONRATIO * REGIONRATIO * REGIONRATIO;

            //mechanics = new Thread(new ThreadStart(this.DoMechanics));
            //mechanics.Priority = ThreadPriority.AboveNormal;
            //mechanics.Start();
        }

        public string GetExtraInfo()
        {
            string extraInfo = "";

            if(varGetI("siege") == 1)
                extraInfo += "build phase";
            else if (varGetI("siege") == 2 || varGetI("siege") == 3)
                extraInfo += "challenge phase";
            else if (varGetI("siege") == 4)
                extraInfo += "siege battle";

            if (varGetB("sandbox"))
                extraInfo += "sandbox";
            else
                extraInfo += string.Format("{0:#a}", winningCashAmount);


            if (!includeLava)
                extraInfo += ", !lava";
            if (!includeWater)
                extraInfo += ", !water";
            if (!varGetB("tnt"))
                extraInfo += ", !tnt";
            //if (varGetB("insane") || varGetB("sspreads") || varGetB("stnt"))
            //    extraInfo += ", insane";

/*            if (varGetB("insanelava"))//insaneLava)
                extraInfo += ", ~lava";
            if (varGetB("sspreads"))
                extraInfo += ", shock->lava";
            if (varGetB("stnt"))//sphericalTnt && false)
                extraInfo += ", stnt";*/
            return extraInfo;
        }

        public void PublicServerListUpdate()
        {
            PublicServerListUpdate(false);
        }

        public void PublicServerListUpdate(bool doIt)
        {
            if (!varGetB("public"))
                return;

            TimeSpan updateTimeSpan = DateTime.Now - lastServerListUpdate;
            if (updateTimeSpan.TotalMinutes >= 1 || doIt)
                CommitUpdate();
        }

        public bool ProcessCommand(string chat)
        {
            return ProcessCommand(chat, (short)1, null);
        }

        public bool ProcessCommand(string input, short authority, Player sender)
        {
            //if (authority == 0)
             //   return false;
            if (sender != null && authority > 0)
                sender.admin = GetAdmin(sender.IP);
            string[] args = input.Split(' '.ToString().ToCharArray(),2);
            if (args[0].StartsWith("/") && args[0].Length > 2)
                args[0] = args[0].Substring(1);
            else if (args[0].StartsWith("/"))
            {
               
            }
            else
                return false;

            switch (args[0].ToLower())
            {
                case "help":
                    {
                        if (sender == null)
                        {
                            ConsoleWrite("SERVER CONSOLE COMMANDS:");
                            ConsoleWrite(" fps");
                            ConsoleWrite(" physics");
                            ConsoleWrite(" announce");
                            ConsoleWrite(" players");
                            ConsoleWrite(" kick <ip>");
                            ConsoleWrite(" kickn <name>");
                            ConsoleWrite(" ban <ip>");
                            ConsoleWrite(" bann <name>");
                            ConsoleWrite(" say <message>");
                            ConsoleWrite(" save <mapfile>");
                            ConsoleWrite(" load <mapfile>");
                            ConsoleWrite(" toggle <var>");
                            ConsoleWrite(" <var> <value>");
                            ConsoleWrite(" <var>");
                            ConsoleWrite(" listvars");
                            ConsoleWrite(" status");
                            ConsoleWrite(" restart");
                            ConsoleWrite(" quit");
                        }
                        else
                        {
                            SendServerMessageToPlayer(sender.Handle + ", the " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                        }
                    }
                    break;
                case "players":
                    {
                        if (sender == null)
                        {
                            ConsoleWrite("( " + playerList.Count + " / " + varGetI("maxplayers") + " )");
                            foreach (Player p in playerList.Values)
                            {
                                string teamIdent = "";
                                if (p.Team == PlayerTeam.Red)
                                    teamIdent = " (R)";
                                else if (p.Team == PlayerTeam.Blue)
                                    teamIdent = " (B)";
                                if (p.IsAdmin)
                                    teamIdent += " (Admin)";
                                ConsoleWrite(p.Handle + teamIdent);
                                ConsoleWrite("  - " + p.IP);
                            }
                        }else{
                            SendServerMessageToPlayer(sender.Handle + ", the " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                        }
                    }
                    break;
                case "rename":
                    {
                        if (sender != null)
                        if (args.Length == 2 && sender.Alive)
                        {
                            if (args[1].Length < 11)
                            {
                                int px = (int)sender.Position.X;
                                int py = (int)sender.Position.Y;
                                int pz = (int)sender.Position.Z;

                                for (int x = -1+px; x < 2+px; x++)
                                    for (int y = -1+py; y < 2+py; y++)
                                        for (int z = -1+pz; z < 2+pz; z++)
                                        {
                                            if (x < 1 || y < 1 || z < 1 || x > MAPSIZE - 2 || y > MAPSIZE - 2 || z > MAPSIZE - 2)
                                            {
                                                //out of map
                                            }
                                            else if (blockList[x, y, z] == BlockType.BeaconRed && sender.Team == PlayerTeam.Red)
                                            {
                                                SendServerMessageToPlayer("You renamed " + beaconList[new Vector3(x, y, z)].ID + " to " + args[1].ToUpper() + ".", sender.NetConn);
                                                if (beaconList.ContainsKey(new Vector3(x, y, z)))
                                                    beaconList.Remove(new Vector3(x, y, z));
                                                SendSetBeacon(new Vector3(x, y + 1, z), "", PlayerTeam.None);

                                                Beacon newBeacon = new Beacon();
                                                newBeacon.ID = args[1].ToUpper();
                                                newBeacon.Team = PlayerTeam.Red;
                                                beaconList[new Vector3(x, y, z)] = newBeacon;
                                                SendSetBeacon(new Vector3(x, y + 1, z), newBeacon.ID, newBeacon.Team);

                                                return true;
                                            }
                                            else if (blockList[x, y, z] == BlockType.BeaconBlue && sender.Team == PlayerTeam.Blue)
                                            {
                                                SendServerMessageToPlayer("You renamed " + beaconList[new Vector3(x, y, z)].ID + " to " + args[1].ToUpper() + ".", sender.NetConn);
                                                if (beaconList.ContainsKey(new Vector3(x, y, z)))
                                                    beaconList.Remove(new Vector3(x, y, z));
                                                SendSetBeacon(new Vector3(x, y + 1, z), "", PlayerTeam.None);

                                                Beacon newBeacon = new Beacon();
                                                newBeacon.ID = args[1].ToUpper();
                                                newBeacon.Team = PlayerTeam.Blue;
                                                beaconList[new Vector3(x, y, z)] = newBeacon;
                                                SendSetBeacon(new Vector3(x, y + 1, z), newBeacon.ID, newBeacon.Team);

                                                return true;
                                            }
                                        }

                                SendServerMessageToPlayer("You must be closer to the beacon.", sender.NetConn);
                               
                            }
                            else
                            {
                                SendServerMessageToPlayer("Beacons are restricted to 10 characters.", sender.NetConn);
                            }
                        }
                    }
                    break;
                case "fps":
                    {
                        ConsoleWrite("Server FPS:"+frameCount );
                    }
                    break;
                case "physics":
                    {
                        if (authority > 0)
                        {
                            physicsEnabled = !physicsEnabled;
                            ConsoleWrite("Physics state is now: " + physicsEnabled);
                        }
                    }
                    break;
                case "liquid":
                    {
                        if (authority > 0)
                        {
                            lavaBlockCount = 0;
                            waterBlockCount = 0;
                            int tempBlockCount = 0;

                            for (ushort i = 0; i < MAPSIZE; i++)
                                for (ushort j = 0; j < MAPSIZE; j++)
                                    for (ushort k = 0; k < MAPSIZE; k++)
                                    {
                                        if (blockList[i, j, k] == BlockType.Lava)
                                        {
                                            lavaBlockCount += 1;
                                            if (blockListContent[i, j, k, 1] > 0)
                                            {
                                                tempBlockCount += 1;
                                            }
                                        }
                                        else if (blockList[i, j, k] == BlockType.Water)
                                        {
                                            waterBlockCount += 1;
                                        }
                                    }

                            ConsoleWrite(waterBlockCount + " water blocks, " + lavaBlockCount + " lava blocks.");
                            ConsoleWrite(tempBlockCount + " temporary blocks.");
                        }
                    }
                    break;
                case "flowsleep":
                    {
                        if (authority > 0)
                        {
                            uint sleepcount = 0;

                            for (ushort i = 0; i < MAPSIZE; i++)
                                for (ushort j = 0; j < MAPSIZE; j++)
                                    for (ushort k = 0; k < MAPSIZE; k++)
                                        if (flowSleep[i, j, k] == true)
                                            sleepcount += 1;

                            ConsoleWrite(sleepcount + " liquids are happily sleeping.");
                        }
                    }
                    break;
                case "admins":
                    {
                        if (authority > 0)
                        {
                            ConsoleWrite("Admin list:");
                            foreach (string ip in admins.Keys)
                                ConsoleWrite(ip);
                        }
                    }
                    break;
                case "admin":
                    {
                        if (authority > 0)
                        {
                            if (args.Length == 2)
                            {
                                if (sender == null || sender.admin >= 2)
                                    AdminPlayer(args[1]);
                                else
                                    SendServerMessageToPlayer("You do not have the authority to add admins.", sender.NetConn);
                            }
                        }
                    }
                    break;
                case "adminn":
                    {
                        if (authority > 0)
                        {
                            if (args.Length == 2)
                            {
                                if (sender == null || sender.admin >= 2)
                                    AdminPlayer(args[1], true);
                                else
                                    SendServerMessageToPlayer("You do not have the authority to add admins.", sender.NetConn);
                            }
                        }
                    }
                    break;
                case "listvars":
                    if (sender==null)
                        varList(true);
                    else{
                        SendServerMessageToPlayer(sender.Handle + ", the " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                    }
                    break;
                case "status":
                    if (sender == null)
                        status();
                    else
                        SendServerMessageToPlayer(sender.Handle + ", the " + args[0].ToLower() + " command is only for use in the server console.", sender.NetConn);
                    break;
                case "announce":
                    {
                        if (authority > 0)
                        {
                            PublicServerListUpdate(true);
                        }
                    }
                    break;
                case "kick":
                    {
                        if (authority>=1&&args.Length == 2)
                        {
                            if (sender != null)
                                ConsoleWrite("SERVER: " + sender.Handle + " has kicked " + args[1]);
                            KickPlayer(args[1]);
                        }
                    }
                    break;
                case "kickn":
                    {
                        if (authority >= 1 && args.Length == 2)
                        {
                            if (sender != null)
                                ConsoleWrite("SERVER: " + sender.Handle + " has kicked " + args[1]);
                            KickPlayer(args[1], true);
                        }
                    }
                    break;

                case "ban":
                    {
                        if (authority >= 1 && args.Length == 2)
                        {
                            if (sender != null)
                                ConsoleWrite("SERVER: " + sender.Handle + " has banned " + args[1]);
                            BanPlayer(args[1]);
                            KickPlayer(args[1]);
                        }
                    }
                    break;

                case "bann":
                    {
                        if (authority >= 1 && args.Length == 2)
                        {
                            if (sender != null)
                                ConsoleWrite("SERVER: " + sender.Handle + " has banned " + args[1]);
                            BanPlayer(args[1], true);
                            KickPlayer(args[1], true);
                        }
                    }
                    break;

                case "toggle":
                    if (authority >= 1 && args.Length == 2)
                    {
                        int exists = varExists(args[1]);
                        if (exists == 1)
                        {
                            bool val = varGetB(args[1]);
                            varSet(args[1], !val);
                        }
                        else if (exists == 2)
                            ConsoleWrite("Cannot toggle a string value.");
                        else
                            varReportStatus(args[1]);
                    }
                    else
                        ConsoleWrite("Need variable name to toggle!");
                    break;
                case "quit":
                    {
                        if (authority >= 2){
                            if ( sender!=null)
                                ConsoleWrite(sender.Handle + " is shutting down the server.");
                             keepRunning = false;
                        }
                    }
                    break;
                case "cost":
                    {
                        if (sender != null)
                        if (authority > 0)
                        if(varGetI("siege") == 1)
                        {
                            int redcost = 5113560 + 30000;
                            for (int x = 0; x < MAPSIZE - 1; x++)
                                for (int y = 0; y < MAPSIZE - 1; y++)
                                    for (int z = (MAPSIZE / 2) + 4; z < MAPSIZE - 1; z++)
                                    {
                                        redcost -= (int)BlockInformation.GetCost(blockList[x, y, z]);
                                    }
                            SendServerMessageToPlayer("cost of red fortress:" + redcost, sender.NetConn);

                            int bluecost = 5113560 + 30000;
                            for (int x = 0; x < MAPSIZE - 1; x++)
                                for (int y = 2; y < MAPSIZE - 1; y++)
                                    for (int z = 1; z < (MAPSIZE / 2) - 4; z++)
                                    {
                                        bluecost -= (int)BlockInformation.GetCost(blockList[x, y, z]);
                                    }

                            //SendServerMessageToPlayer("cost of blue fortress(broken):" + bluecost, sender.NetConn);
                        }
                    }
                    break;
                case "siege":
                    {
                        //InitSiege();
                        //break;
                        if (varGetI("siege") == 1)
                        if (sender != null)
                        if (authority < 2)
                        {
                           // if(varGetB("voting"))
                            if (VoteType == 0)
                            {
                                if (sender.connectedTimer < DateTime.Now - TimeSpan.FromSeconds(120) && VoteStart < DateTime.Now - TimeSpan.FromSeconds(120))
                                {
                                    SendServerMessage("** " + sender.Handle + " has initiated a vote to allow challengers against our fortress! To agree use /yes **");
                                    VoteType = 4;//challenge vote
                                    VoteCreator = (int)sender.ID;
                                    VoteStart = DateTime.Now;// -TimeSpan.FromSeconds(90);
                                    sender.Vote = true;
                                }
                                else
                                {
                                     SendServerMessageToPlayer("You may not vote until 120 seconds have elapsed.", sender.NetConn);
                                }
                            }
                            else
                            {
                                if (VoteType == 1)
                                {
                                    SendServerMessageToPlayer("A restart vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                                else if (VoteType == 2)
                                {
                                    SendServerMessageToPlayer("A save vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                                else if (VoteType == 3)
                                {
                                    SendServerMessageToPlayer("A load vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                                else if (VoteType == 4)
                                {
                                    SendServerMessageToPlayer("A challenge vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                                else
                                SendServerMessageToPlayer("A vote is already in progress, please wait for it to finish.", sender.NetConn);
                            }
                        }
                    }
                    break;
                case "yes":
                    {
                        if (sender != null)
                        if (VoteType > 0)
                            if (sender.Team != PlayerTeam.None && !sender.Disposing && sender.connectedTimer < DateTime.Now - TimeSpan.FromSeconds(120))// && sender.IP != VoteIP)
                            {
                                sender.Vote = true;
                                SendServerMessageToPlayer("You voted in agreement.", sender.NetConn);
                            }
                            else if (!sender.Disposing)
                            {

                                if (sender.Vote != true)
                                {
                                    sender.Vote = true;//sets it anyway
                                    SendServerMessageToPlayer("You may not vote until 120 seconds have elapsed.", sender.NetConn);
                                }
                                else
                                {
                                    SendServerMessageToPlayer("You have already voted.", sender.NetConn);
                                }
                            }
                    }
                    break;
                case "no":
                    {
                        if (sender != null)
                        if(VoteType > 0)
                        if (sender.Team != PlayerTeam.None && !sender.Disposing && sender.connectedTimer < DateTime.Now - TimeSpan.FromSeconds(120))// && sender.IP != VoteIP)
                        {
                            if (sender.Vote != true)
                            {
                                sender.Vote = false;
                            }
                        }
                        else if (!sender.Disposing)
                        {
                            if (sender.Vote != true)
                            {
                                sender.Vote = false;//sets it anyway
                                SendServerMessageToPlayer("You may not vote until 120 seconds have elapsed.", sender.NetConn);
                            }
                            else
                            {
                                SendServerMessageToPlayer("You have already voted for the restart.", sender.NetConn);

                            }
                        }
                    }
                    break;
                case "restart":
                    {
                        if (authority >= 2){
                            if (sender != null)
                                ConsoleWrite(sender.Handle + " is restarting the server.");
                            else
                            {
                                ConsoleWrite("Restarting server in 5 seconds.");
                            }
                            //disconnectAll();
                           
                            SendServerMessage("Server restarting in 5 seconds.");
                            restartTriggered = true;
                            restartTime = DateTime.Now+TimeSpan.FromSeconds(5);
                        }
                        else if (authority < 2)
                        {
                            if(varGetB("voting"))
                            if (VoteType == 0)
                            {
                                if (sender.connectedTimer < DateTime.Now - TimeSpan.FromSeconds(120) && VoteStart < DateTime.Now - TimeSpan.FromSeconds(120))
                                {
                                    SendServerMessage("** " + sender.Handle + " has initiated a vote to restart! To agree use /yes **");
                                    VoteType = 1;//restart vote
                                    VoteCreator = (int)sender.ID;
                                    VoteStart = DateTime.Now;
                                    sender.Vote = true;
                                }
                                else
                                {
                                     SendServerMessageToPlayer("You may not vote until 120 seconds have elapsed.", sender.NetConn);
                                }
                            }
                            else
                            {
                                if (VoteType == 1)
                                {
                                    SendServerMessageToPlayer("A restart vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                                else if (VoteType == 2)
                                {
                                    SendServerMessageToPlayer("A save vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                                else if (VoteType == 3)
                                {
                                    SendServerMessageToPlayer("A load vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                                else if (VoteType == 4)
                                {
                                    SendServerMessageToPlayer("A challenge vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                                else
                                SendServerMessageToPlayer("A vote is already in progress, please wait for it to finish.", sender.NetConn);
                            }
                        }
                    }
                    break;

                case "say":
                    {
                        if (authority > 0)
                        {
                            if (args.Length == 2)
                            {
                                string message = "SERVER: " + args[1];
                                SendServerMessage(message);
                            }
                        }
                    }
                    break;

                case "save":
                    {
                        if (authority > 0)
                        {
                            if (args.Length >= 2)
                            {
                                if (sender != null)
                                    ConsoleWrite(sender.Handle + " is saving the map.");

                                if (Filename == "")
                                {
                                    lastMapBackup = DateTime.Now;
                                    SaveLevel(args[1]);
                                }
                                else
                                {
                                    if (sender != null)
                                        SendServerMessageToPlayer("The map is already being saved, please wait and try again!", sender.NetConn);

                                    ConsoleWrite("The map is already being saved, please wait and try again!");
                                }
                            }
                        }
                    }
                    break;

                case "savefort":
                    {
                        if (authority > 0)
                        {
                            if (args.Length >= 2)
                            {
                                if (sender != null)
                                    ConsoleWrite(sender.Handle + " is saving the map.");

                                if (Filename == "")
                                {
                                    lastMapBackup = DateTime.Now;
                                    SavingFort(args[1]);
                                }
                                else
                                {
                                    if (sender != null)
                                        SendServerMessageToPlayer("The map is already being saved, please wait and try again!", sender.NetConn);

                                    ConsoleWrite("The map is already being saved, please wait and try again!");
                                }
                            }
                        }
                        else if (authority < 2)
                        {
                            if (varGetB("voting"))
                                if (VoteType == 0)
                                {
                                    if (sender.connectedTimer < DateTime.Now - TimeSpan.FromSeconds(120) && VoteStart < DateTime.Now - TimeSpan.FromSeconds(120))
                                    {
                                        SendServerMessage("** " + sender.Handle + " has initiated a vote to save the fortress permanently! To agree use /yes **");
                                        VoteType = 2;//savefort vote
                                        VoteCreator = (int)sender.ID;
                                        VoteStart = DateTime.Now;
                                        sender.Vote = true;
                                    }
                                    else
                                    {
                                        SendServerMessageToPlayer("You may not vote until 120 seconds have elapsed.", sender.NetConn);
                                    }
                                }
                                else
                                {
                                    if (VoteType == 1)
                                    {
                                        SendServerMessageToPlayer("A restart vote is already in progress, please wait for it to finish.", sender.NetConn);
                                    }
                                    else if (VoteType == 2)
                                    {
                                        SendServerMessageToPlayer("A save vote is already in progress, please wait for it to finish.", sender.NetConn);
                                    }
                                    else if (VoteType == 3)
                                    {
                                        SendServerMessageToPlayer("A load vote is already in progress, please wait for it to finish.", sender.NetConn);
                                    }
                                    else if (VoteType == 4)
                                    {
                                        SendServerMessageToPlayer("A challenge vote is already in progress, please wait for it to finish.", sender.NetConn);
                                    }
                                    else
                                        SendServerMessageToPlayer("A vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                        }
                    }
                    break;
                case "loadfort":
                    {
                        if (authority > 0)
                        {
                            if (args.Length >= 2)
                            {
                                if (sender != null)
                                    ConsoleWrite(sender.Handle + " is loading a fortress.");
      
                                Thread.Sleep(2);
                                LoadFortLevel(args[1], PlayerTeam.Red);// PlayerTeam.Blue);

                                if (varGetI("siege") == 1)
                                {
                                    teamOreRed = 5113560 + 30000;
                                    for (int x = 0; x < MAPSIZE - 1; x++)
                                        for (int y = 0; y < MAPSIZE - 1; y++)
                                            for (int z = (MAPSIZE / 2) + 4; z < MAPSIZE - 1; z++)
                                            {
                                                if (teamOreRed >= (int)BlockInformation.GetCost(blockList[x, y, z]))
                                                {
                                                    teamOreRed -= (int)BlockInformation.GetCost(blockList[x, y, z]);
                                                }
                                                else
                                                {
                                                    teamOreRed = 0;
                                                    z = MAPSIZE + 1;
                                                    y = MAPSIZE + 1;
                                                    x = MAPSIZE + 1;
                                                }
                                            }

                                    if (teamOreRed > 30000)
                                        teamOreRed = 30000;
                                }
                                disconnectAll();
                            }
                            else if (levelToLoad != "")
                            {
                                if (levelToLoad.ToLower() == "autobk.lvl")
                                {
                                    levelToLoad = "fort.lvl";
                                }
                                Thread.Sleep(2);
                                LoadFortLevel(levelToLoad, PlayerTeam.Red); //PlayerTeam.Blue);
                                if (varGetI("siege") == 1)
                                {
                                    teamOreRed = 5113560 + 30000;
                                    for (int x = 0; x < MAPSIZE - 1; x++)
                                        for (int y = 0; y < MAPSIZE - 1; y++)
                                            for (int z = (MAPSIZE / 2) + 4; z < MAPSIZE - 1; z++)
                                            {
                                                if (teamOreRed >= (int)BlockInformation.GetCost(blockList[x, y, z]))
                                                {
                                                    teamOreRed -= (int)BlockInformation.GetCost(blockList[x, y, z]);
                                                }
                                                else
                                                {
                                                    teamOreRed = 0;
                                                    z = MAPSIZE + 1;
                                                    y = MAPSIZE + 1;
                                                    x = MAPSIZE + 1;
                                                }
                                            }

                                    if (teamOreRed > 30000)
                                        teamOreRed = 30000;
                                }
                                disconnectAll();
                            }
                        }
                        else if (authority < 2)
                        {
                            if (varGetB("voting"))
                                if (VoteType == 0)
                                {
                                    if (sender.connectedTimer < DateTime.Now - TimeSpan.FromSeconds(120) && VoteStart < DateTime.Now - TimeSpan.FromSeconds(120))
                                    {
                                        SendServerMessage("** " + sender.Handle + " has initiated a vote to load the host fortress! To agree use /yes **");
                                        VoteType = 3;//loadfort vote
                                        VoteCreator = (int)sender.ID;
                                        VoteStart = DateTime.Now;
                                        sender.Vote = true;
                                    }
                                    else
                                    {
                                        SendServerMessageToPlayer("You may not vote until 120 seconds have elapsed.", sender.NetConn);
                                    }
                                }
                                else
                                {
                                    if (VoteType == 1)
                                    {
                                        SendServerMessageToPlayer("A restart vote is already in progress, please wait for it to finish.", sender.NetConn);
                                    }
                                    else if (VoteType == 2)
                                    {
                                        SendServerMessageToPlayer("A save vote is already in progress, please wait for it to finish.", sender.NetConn);
                                    }
                                    else if (VoteType == 3)
                                    {
                                        SendServerMessageToPlayer("A load vote is already in progress, please wait for it to finish.", sender.NetConn);
                                    }
                                    else if (VoteType == 4)
                                    {
                                        SendServerMessageToPlayer("A challenge vote is already in progress, please wait for it to finish.", sender.NetConn);
                                    }
                                    else
                                        SendServerMessageToPlayer("A vote is already in progress, please wait for it to finish.", sender.NetConn);
                                }
                        }
                    }
                    break;
                case "load":
                    {
                        if (authority > 0)
                        {
                            if (args.Length >= 2)
                            {
                                if (sender != null)
                                    ConsoleWrite(sender.Handle + " is loading a map.");
                                physicsEnabled = false;
                                Thread.Sleep(2);
                                LoadLevel(args[1]);
                                physicsEnabled = true;

                                if (varGetI("siege") == 1)
                                {
                                    teamOreRed = 5113560 + 30000;
                                    for (int x = 0; x < MAPSIZE - 1; x++)
                                        for (int y = 0; y < MAPSIZE - 1; y++)
                                            for (int z = (MAPSIZE/2) + 4; z < MAPSIZE - 1; z++)
                                            {
                                                if (teamOreRed >= (int)BlockInformation.GetCost(blockList[x, y, z]))
                                                {
                                                    teamOreRed -= (int)BlockInformation.GetCost(blockList[x, y, z]);
                                                }
                                                else
                                                {
                                                    teamOreRed = 0;
                                                    z = MAPSIZE + 1;
                                                    y = MAPSIZE + 1;
                                                    x = MAPSIZE + 1;
                                                }
                                            }

                                    if (teamOreRed > 30000)
                                        teamOreRed = 30000;
                                }

                                /*if (LoadLevel(args[1]))
                                    Console.WriteLine("Loaded level " + args[1]);
                                else
                                    Console.WriteLine("Level file not found!");*/
                            }
                            else if (levelToLoad != "")
                            {
                                physicsEnabled = false;
                                Thread.Sleep(2);
                                LoadLevel(levelToLoad);
                                if (varGetI("siege") == 1)
                                {
                                    teamOreRed = 5113560 + 30000;
                                    for (int x = 0; x < MAPSIZE - 1; x++)
                                        for (int y = 0; y < MAPSIZE - 1; y++)
                                            for (int z = (MAPSIZE / 2) + 4; z < MAPSIZE - 1; z++)
                                            {
                                                if (teamOreRed >= (int)BlockInformation.GetCost(blockList[x, y, z]))
                                                {
                                                    teamOreRed -= (int)BlockInformation.GetCost(blockList[x, y, z]);
                                                }
                                                else
                                                {
                                                    teamOreRed = 0;
                                                    z = MAPSIZE + 1;
                                                    y = MAPSIZE + 1;
                                                    x = MAPSIZE + 1;
                                                }
                                            }

                                    if (teamOreRed > 30000)
                                        teamOreRed = 30000;
                                }
                                physicsEnabled = true;
                            }
                        }
                    }
                    break;

                case "spawnitem":
                    {
                        if (sender == null && authority >= 1) // Server console only
                        {
                            if (args.Length >= 2)
                            {
                                string[] commandArgs = args[1].Split(' ');
                                if (commandArgs.Length >= 2)
                                {
                                    try
                                    {
                                        // Parse item type
                                        ItemType itemType = ItemType.Gold;
                                        switch (commandArgs[0].ToLower())
                                        {
                                            case "ore": itemType = ItemType.Ore; break;
                                            case "gold": itemType = ItemType.Gold; break;
                                            case "diamond": itemType = ItemType.Diamond; break;
                                            case "artifact": itemType = ItemType.Artifact; break;
                                            case "bomb": itemType = ItemType.Bomb; break;
                                            case "mushroom": itemType = ItemType.Mushroom; break;
                                            case "spikes": itemType = ItemType.Spikes; break;
                                            case "target": itemType = ItemType.Target; break;
                                            default: itemType = ItemType.Gold; break;
                                        }
                                        
                                        // Find player by name
                                        string playerName = commandArgs[1].ToLower();
                                        Player targetPlayer = null;
                                        foreach (Player p in playerList.Values)
                                        {
                                            if (p.Handle.ToLower().Contains(playerName))
                                            {
                                                targetPlayer = p;
                                                break;
                                            }
                                        }
                                        
                                        if (targetPlayer != null && targetPlayer.Alive)
                                        {
                                            // Spawn item near the player
                                            Vector3 spawnPos = targetPlayer.Position + new Vector3(1, 1, 0);
                                            SetItem(itemType, spawnPos, Vector3.Zero, Vector3.Zero, PlayerTeam.None, 0, 0);
                                            ConsoleWrite("Spawned " + itemType + " near player " + targetPlayer.Handle);
                                        }
                                        else
                                        {
                                            ConsoleWrite("Player '" + commandArgs[1] + "' not found or not alive");
                                        }
                                    }
                                    catch
                                    {
                                        ConsoleWrite("Usage: spawnitem <type> <playername>");
                                    }
                                }
                                else
                                {
                                    ConsoleWrite("Usage: spawnitem <type> <playername> - Types: gold, ore, diamond, artifact, bomb, mushroom, spikes, target");
                                }
                            }
                            else
                            {
                                ConsoleWrite("Usage: spawnitem <type> <playername> - Types: gold, ore, diamond, artifact, bomb, mushroom, spikes, target");
                            }
                        }
                        else if (sender != null)
                        {
                            SendServerMessageToPlayer("This command can only be used from the server console.", sender.NetConn);
                        }
                        else
                        {
                            ConsoleWrite("Usage: spawnitem <type> <playername> - Types: gold, ore, diamond, artifact, bomb, mushroom, spikes, target");
                        }
                    }
                    break;

                case "setore":
                    {
                        if (sender == null && authority >= 1) // Server console only
                        {
                            if (args.Length >= 2)
                            {
                                string[] commandArgs = args[1].Split(' ');
                                if (commandArgs.Length >= 2)
                                {
                                    try
                                    {
                                        string team = commandArgs[0].ToLower();
                                        int amount = int.Parse(commandArgs[1]);
                                        
                                        if (team == "red")
                                        {
                                            teamOreRed = amount;
                                            ConsoleWrite("Set red team ore to " + amount);
                                        }
                                        else if (team == "blue")
                                        {
                                            teamOreBlue = amount;
                                            ConsoleWrite("Set blue team ore to " + amount);
                                        }
                                        else
                                        {
                                            ConsoleWrite("Invalid team. Use 'red' or 'blue'");
                                        }
                                    }
                                    catch
                                    {
                                        ConsoleWrite("Invalid amount. Usage: setore <red|blue> <amount>");
                                    }
                                }
                                else
                                {
                                    ConsoleWrite("Usage: setore <red|blue> <amount>");
                                }
                            }
                            else
                            {
                                ConsoleWrite("Usage: setore <red|blue> <amount>");
                            }
                        }
                        else if (sender != null)
                        {
                            SendServerMessageToPlayer("This command can only be used from the server console.", sender.NetConn);
                        }
                        else
                        {
                            ConsoleWrite("Usage: setore <red|blue> <amount>");
                        }
                    }
                    break;

                case "setcash":
                case "setmoney":
                    {
                        if (sender == null && authority >= 1) // Server console only
                        {
                            if (args.Length >= 2)
                            {
                                string[] commandArgs = args[1].Split(' ');
                                if (commandArgs.Length >= 2)
                                {
                                    try
                                    {
                                        string team = commandArgs[0].ToLower();
                                        uint amount = uint.Parse(commandArgs[1]);
                                        
                                        if (team == "red")
                                        {
                                            teamCashRed = amount;
                                            ConsoleWrite("Set red team cash to " + amount);
                                        }
                                        else if (team == "blue")
                                        {
                                            teamCashBlue = amount;
                                            ConsoleWrite("Set blue team cash to " + amount);
                                        }
                                        else
                                        {
                                            ConsoleWrite("Invalid team. Use 'red' or 'blue'");
                                        }
                                    }
                                    catch
                                    {
                                        ConsoleWrite("Invalid amount. Usage: setcash <red|blue> <amount>");
                                    }
                                }
                                else
                                {
                                    ConsoleWrite("Usage: setcash <red|blue> <amount>");
                                }
                            }
                            else
                            {
                                ConsoleWrite("Usage: setcash <red|blue> <amount>");
                            }
                        }
                        else if (sender != null)
                        {
                            SendServerMessageToPlayer("This command can only be used from the server console.", sender.NetConn);
                        }
                        else
                        {
                            ConsoleWrite("Usage: setcash <red|blue> <amount>");
                        }
                    }
                    break;

                default: //Check / set var
                    {
                        if (authority == 0)
                            return false;

                        string name = args[0];
                        int exists = varExists(name);
                        if (exists > 0)
                        {
                            if (args.Length == 2)
                            {
                                try
                                {
                                    if (exists == 1)
                                    {
                                        bool newVal = false;
                                        newVal = bool.Parse(args[1]);
                                        varSet(name, newVal);
                                    }
                                    else if (exists == 2)
                                    {
                                        varSet(name, args[1]);
                                    }
                                    else if (exists == 3)
                                    {
                                        varSet(name, Int32.Parse(args[1]));
                                    }

                                }
                                catch { }
                            }
                            else
                            {
                                if (sender==null)
                                    varReportStatus(name);
                                else
                                    SendServerMessageToPlayer(sender.Handle + ": The " + args[0].ToLower() + " command is only for use in the server console.",sender.NetConn);
                            }
                        }
                        else
                        {
                            char first = args[0].ToCharArray()[0];
                            if (first == 'y' || first == 'Y')
                            {
                                string message = "SERVER: " + args[0].Substring(1);
                                if (args.Length > 1)
                                    message += (message != "SERVER: " ? " " : "") + args[1];
                                SendServerMessage(message);
                            }
                            else
                            {
                                if (sender == null)
                                    ConsoleWrite("Unknown command/var.");
                                return false;
                            }
                        }
                    }
                    break;
            }
            return true;
        }

        public void MessageAll(string text)
        {
            if (announceChanges)
                SendServerMessage(text);
            ConsoleWrite(text);
        }

        public void ConsoleWrite(string text)
        {
            Console.WriteLine(text);
            consoleText.Add(text);
            if (consoleText.Count > CONSOLE_SIZE)
                consoleText.RemoveAt(0);
        }

        public Dictionary<string, short> LoadAdminList()
        {
            Dictionary<string, short> temp = new Dictionary<string, short>();

            try
            {
                if (!File.Exists("admins.txt"))
                {
                    FileStream fs = File.Create("admins.txt");
                    StreamWriter sr = new StreamWriter(fs);
                    sr.WriteLine("#A list of all admins - just add one ip per line");
                    sr.Close();
                    fs.Close();
                }
                else
                {
                    FileStream file = new FileStream("admins.txt", FileMode.Open, FileAccess.Read);
                    StreamReader sr = new StreamReader(file);
                    string line = sr.ReadLine();
                    while (line != null)
                    {
                        if (line.Trim().Length!=0&&line.Trim().ToCharArray()[0]!='#')
                            temp.Add(line.Trim(), (short)2); //This will be changed to note authority too
                        line = sr.ReadLine();
                    }
                    sr.Close();
                    file.Close();
                }
            }
            catch {
                ConsoleWrite("Unable to load admin list.");
            }

            return temp;
        }

        public bool SaveAdminList()
        {
            try
            {
                FileStream file = new FileStream("admins.txt", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(file);
                sw.WriteLine("#A list of all admins - just add one ip per line\n");
                foreach (string ip in admins.Keys)
                    sw.WriteLine(ip);
                sw.Close();
                file.Close();
                return true;
            }
            catch { }
            return false;
        }

        public List<string> LoadBanList()
        {
            List<string> retList = new List<string>();

            try
            {
                FileStream file = new FileStream("banlist.txt", FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(file);
                string line = sr.ReadLine();
                while (line != null)
                {
                    retList.Add(line.Trim());
                    line = sr.ReadLine();
                }
                sr.Close();
                file.Close();
            }
            catch { }

            return retList;
        }

        public void SaveBanList(List<string> banList)
        {
            try
            {
                FileStream file = new FileStream("banlist.txt", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(file);
                int a = 0;
                foreach (string ip in banList)
                {
                    a++;
                    if(!tempBan.Contains(a++))
                    sw.WriteLine(ip);
                }
                sw.Close();
                file.Close();
            }
            catch { }
        }

        public void KickPlayer(string ip)
        {
            KickPlayer(ip, false);
        }

        public void KickPlayer(string ip, bool name)
        {
            List<Player> playersToKick = new List<Player>();
            foreach (Player p in playerList.Values)
            {
                if ((p.IP == ip && !name) || (p.Handle.ToLower().Contains(ip.ToLower()) && name))
                    playersToKick.Add(p);
            }
            foreach (Player p in playersToKick)
            {
                if (p.Alive)
                    Player_Dead(p, "");

                p.NetConn.Disconnect("", 0);
                p.Kicked = true;
            }
        }

        public void KickPlayer(Player player, bool tempban)
        {
            List<Player> playersToKick = new List<Player>();
            foreach (Player p in playerList.Values)
            {
                if (player == p)
                {
                    playersToKick.Add(p);
                    break;
                }
            }
            foreach (Player p in playersToKick)
            {
                if (p.Alive)
                {
                    if(tempban)
                    Player_Dead(p, "has been issued a temporary ban for disruptive behaviour.");
                    else
                        Player_Dead(p, "");
                }

                if(tempban)
                TempBanPlayer(p.IP);

                p.NetConn.Disconnect("", 0);
                p.Kicked = true;
            }
        }

        public void BanPlayer(string ip)
        {
            BanPlayer(ip, false);
        }

        public void BanPlayer(string ip, bool name)
        {
            string realIp = ip;
            if (name)
            {
                foreach (Player p in playerList.Values)
                {
                    if ((p.Handle == ip && !name) || (p.Handle.ToLower().Contains(ip.ToLower()) && name))
                    {
                        realIp = p.IP;
                        break;
                    }
                }
            }
            if (!banList.Contains(realIp))
            {
                banList.Add(realIp);
                SaveBanList(banList);
            }
        }

        public void TempBanPlayer(string ip)
        {
            string realIp = ip;
            
            if (!banList.Contains(realIp))
            {
                banList.Add(realIp);
                tempBan.Add(banList.Count);
            }
        }

        public short GetAdmin(string ip)
        {
            if (admins.ContainsKey(ip.Trim()))
                return admins[ip.Trim()];
            return (short)0;
        }

        public void AdminPlayer(string ip)
        {
            AdminPlayer(ip, false,(short)2);
        }

        public void AdminPlayer(string ip, bool name)
        {
            AdminPlayer(ip, name, (short)2);
        }

        public void AdminPlayer(string ip, bool name, short authority)
        {
            string realIp = ip;
            if (name)
            {
                foreach (Player p in playerList.Values)
                {
                    if ((p.Handle == ip && !name) || (p.Handle.ToLower().Contains(ip.ToLower()) && name))
                    {
                        realIp = p.IP;
                        break;
                    }
                }
            }
            if (!admins.ContainsKey(realIp))
            {
                admins.Add(realIp,authority);
                SaveAdminList();
            }
        }

        public void ConsoleProcessInput()
        {
            ProcessCommand(consoleInput, (short)2, null);
            /*string[] args = consoleInput.Split(" ".ToCharArray(),2);

            
            switch (args[0].ToLower().Trim())
            {
                case "help":
                    {
                        ConsoleWrite("SERVER CONSOLE COMMANDS:");
                        ConsoleWrite(" announce");
                        ConsoleWrite(" players");
                        ConsoleWrite(" kick <ip>");
                        ConsoleWrite(" kickn <name>");
                        ConsoleWrite(" ban <ip>");
                        ConsoleWrite(" bann <name>");
                        ConsoleWrite(" say <message>");
                        ConsoleWrite(" save <mapfile>");
                        ConsoleWrite(" load <mapfile>");
                        ConsoleWrite(" toggle <var>");//ConsoleWrite(" toggle [" + varList() + "]");//[tnt,stnt,sspreads,insanelava,minelava,announcechanges]");
                        ConsoleWrite(" <var> <value>");
                        ConsoleWrite(" <var>");
                        ConsoleWrite(" listvars");
                        ConsoleWrite(" status");
                        ConsoleWrite(" restart");
                        //ConsoleWrite(" reload");
                        ConsoleWrite(" quit");
                    }
                    break;
                case "players":
                    {
                        ConsoleWrite("( " + playerList.Count + " / " + varGetI("maxplayers") + " )");//maxPlayers + " )");
                        foreach (Player p in playerList.Values)
                        {
                            string teamIdent = "";
                            if (p.Team == PlayerTeam.Red)
                                teamIdent = " (R)";
                            else if (p.Team == PlayerTeam.Blue)
                                teamIdent = " (B)";
                            ConsoleWrite(p.Handle + teamIdent);
                            ConsoleWrite("  - " + p.IP);
                        }
                    }
                    break;
                case "listvars":
                    varList(true);
                    break;
                case "announce":
                    {
                        PublicServerListUpdate(true);
                    }
                    break;
                case "kick":
                    {
                        if (args.Length == 2)
                        {
                            KickPlayer(args[1]);
                        }
                    }
                    break;
                case "kickn":
                    {
                        if (args.Length == 2)
                        {
                            KickPlayer(args[1], true);
                        }
                    }
                    break;

                case "ban":
                    {
                        if (args.Length == 2)
                        {
                            KickPlayer(args[1]);
                            BanPlayer(args[1]);
                        }
                    }
                    break;

                case "bann":
                    {
                        if (args.Length == 2)
                        {
                            KickPlayer(args[1],true);
                            BanPlayer(args[1],true);
                        }
                    }
                    break;

                case "toggle":
                    if (args.Length == 2)
                    {
                        int exists = varExists(args[1]);
                        if (exists == 1)
                        {
                            bool val = varGetB(args[1]);
                            varSet(args[1], !val);
                        }
                        else if (exists == 2)
                            ConsoleWrite("Cannot toggle a string value.");
                        else
                            varReportStatus(args[1]);
                    }
                    else
                        ConsoleWrite("Need variable name to toggle!");
                    break;
                case "quit":
                    {
                        keepRunning = false;
                    }
                    break;

                case "restart":
                    {
                        disconnectAll();
                        restartTriggered = true;
                        restartTime = DateTime.Now;
                    }
                    break;

                case "say":
                    {
                        if (args.Length == 2)
                        {
                            string message = "SERVER: " + args[1];
                            SendServerMessage(message);
                        }
                    }
                    break;

                case "save":
                    {
                        if (args.Length >= 2)
                        {
                            SaveLevel(args[1]);
                        }
                    }
                    break;

                case "load":
                    {
                        if (args.Length >= 2)
                        {
                            LoadLevel(args[1]);
                        }
                        else if (levelToLoad != "")
                        {
                            LoadLevel(levelToLoad);
                        }
                    }
                    break;
                case "status":
                    status();
                    break;
                default: //Check / set var
                    {
                        string name = args[0];
                        int exists = varExists(name);
                        if (exists > 0)
                        {
                            if (args.Length == 2)
                            {
                                try
                                {
                                    if (exists == 1)
                                    {
                                        bool newVal = false;
                                        newVal = bool.Parse(args[1]);
                                        varSet(name, newVal);
                                    }
                                    else if (exists == 2)
                                    {
                                        varSet(name, args[1]);
                                    }
                                    else if (exists == 3)
                                    {
                                        varSet(name, Int32.Parse(args[1]));
                                    }

                                }
                                catch { }
                            }
                            else
                            {
                                varReportStatus(name);
                            }
                        }
                        else
                        {
                            char first=args[0].ToCharArray()[0];
                            if (first == 'y' || first == 'Y')
                            {
                                string message = "SERVER: " + args[0].Substring(1);
                                if (args.Length > 1)
                                    message += (message!="SERVER: " ? " " : "") + args[1];
                                SendServerMessage(message);
                            }
                            else
                                ConsoleWrite("Unknown command/var.");
                        }
                    }
                    break;
            }*/

            consoleInput = "";
        }

        public void Saving()
        {
            string filename = Filename;
            FileStream fs = new FileStream(filename, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine(MAPSIZE);
            sw.WriteLine(false);

            for (int x = 0; x < MAPSIZE; x++)
                for (int y = 0; y < MAPSIZE; y++)
                    for (int z = 0; z < MAPSIZE; z++)
                    {
                        sw.WriteLine((byte)blockList[x, y, z] + "," + (byte)blockCreatorTeam[x, y, z] + "," + blockListHP[x, y, z]);

                        if(blockList[x,y,z] == BlockType.BeaconBlue || blockList[x,y,z] == BlockType.BeaconRed)
                        {
                            sw.WriteLine(beaconList[new Vector3(x, y, z)].ID);
                        }
                        for (int rx = 0; rx < 50; rx++)
                        {
                            sw.WriteLine(blockListContent[x, y, z, rx]);
                        }
                    }

            sw.WriteLine(itemList.Count);
            //ConsoleWrite("itemcount1: " + itemList.Count);

            int ic = 0;
            foreach (KeyValuePair<uint, Item> i in itemList)
            {
                ic++;
                sw.WriteLine(i.Key);
                sw.WriteLine((byte)itemList[i.Key].Type);
                sw.WriteLine(itemList[i.Key].Billboard);
                sw.WriteLine(itemList[i.Key].Heading.X + "," + itemList[i.Key].Heading.Y + "," + itemList[i.Key].Heading.Z);
                sw.WriteLine(itemList[i.Key].Position.X + "," + itemList[i.Key].Position.Y + "," + itemList[i.Key].Position.Z);
                sw.WriteLine(itemList[i.Key].Scale);
                sw.WriteLine((byte)itemList[i.Key].Team);
                sw.WriteLine(itemList[i.Key].Disposing);

                for (int rx = 0; rx < 20; rx++)
                {
                    sw.WriteLine(itemList[i.Key].Content[rx]);
                }

            }
            //ConsoleWrite("itemcountt: " + ic);

            for (int cr = 0; cr < 20; cr++)//save current artifact effects
            {
                sw.WriteLine(artifactActive[1, cr]);
                sw.WriteLine(artifactActive[2, cr]);
            }

            sw.WriteLine(basePosition[PlayerTeam.Red].X + "," + basePosition[PlayerTeam.Red].Y + "," + basePosition[PlayerTeam.Red].Z);
            sw.WriteLine(basePosition[PlayerTeam.Blue].X + "," + basePosition[PlayerTeam.Blue].Y + "," + basePosition[PlayerTeam.Blue].Z);
            sw.WriteLine(teamCashRed + "," + teamCashBlue + "," + teamOreRed + "," + teamOreBlue + "," + teamArtifactsRed + "," + teamArtifactsBlue);

           // for (int rx = 0; rx < (byte)Research.MAXIMUM; rx++)
                for (int ry = 0; ry < (byte)Research.TMAXIMUM; ry++)
                {
                    sw.WriteLine(ResearchComplete[(byte)PlayerTeam.Red, ry] + "," + ResearchProgress[(byte)PlayerTeam.Red, ry] + "," + ResearchComplete[(byte)PlayerTeam.Blue, ry] + "," + ResearchProgress[(byte)PlayerTeam.Blue, ry]);
                }
            sw.Close();
            fs.Close();
            Filename = "";
            ConsoleWrite("..completed!");
            physicsEnabled = true;
        }
        public void SaveLevel(string filename)
        {
            if (Filename == "")
            {
                physicsEnabled = false;
                Filename = filename;
                ConsoleWrite("Saving in progress..");
                saving = new Thread(new ThreadStart(this.Saving));
                saving.Priority = ThreadPriority.Normal;
                saving.Start();
            }
        }
        public void SavingFort(string filename)
        {
            Filename = "saveinprogress";
            FileStream fs = new FileStream(filename, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine(MAPSIZE);
            sw.WriteLine(true);

            int bcount = 0;

            if (filename == "attackingfort.lvl")
            {
                for (int x = 0; x < MAPSIZE; x++)
                    for (int y = 0; y < MAPSIZE; y++)
                        for (int z = 0; z < MAPSIZE; z++)
                        {
                            sw.WriteLine((byte)blockListFort[x, y, z]);
                            bcount++;
                        }
            }
            else
            {
                for (int x = 0; x < MAPSIZE; x++)
                    for (int y = 0; y < MAPSIZE; y++)
                        for (int z = 0; z < MAPSIZE; z++)
                        {
                            sw.WriteLine((byte)blockList[x, y, z]);
                            bcount++;
                        }
            }
            //ConsoleWrite("fortress had " + bcount + " blocks!");
            sw.Close();
            fs.Close();

            Filename = "";
            ConsoleWrite("..completed fortsave!");
        }
        public bool LoadFortLevel(string filename, PlayerTeam team)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    ConsoleWrite("Unable to load fort level - " + filename + " does not exist!");
                    return false;
                }
                //SendServerMessage("Loading fort: " + filename + "!");
                //disconnectAll();

                //ConsoleWrite("Removing items!");
                FileStream fs = new FileStream(filename, FileMode.Open);
                StreamReader sr = new StreamReader(fs);

                foreach (KeyValuePair<uint, Item> bPair in itemList)//remove all items
                {
                    itemList[bPair.Key].Disposing = true;
                    // itemList.Remove(bPair.Key);
                    // itemIDList.Remove(bPair.Key);
                }

                //beaconIDList = new List<string>();
                //beaconList = new Dictionary<Vector3, Beacon>();


                //ConsoleWrite("L_Citemcount: " + itemList.Count);
                highestitem = 0;
                teamRegeneration[(byte)PlayerTeam.Red] = 0;//remove regen, doesnt account for artifact regen
                teamRegeneration[(byte)PlayerTeam.Blue] = 0;

                MAPSIZE = int.Parse(sr.ReadLine());
                bool clientsave = bool.Parse(sr.ReadLine());//is this saved from a client rather than server? (lacking all information apart from block geo)

                uint cost = 0;

                int xstart = 0;
                int ystart = 0;
                int zstart = 0;

                //for (int x = xstart; x < MAPSIZE; x++)
                //    for (int y = ystart; y < MAPSIZE; y++)
                //        for (int z = zstart; z < MAPSIZE; z++)
                //        {
                //            blockList[x, y, z] = BlockType.None;
                //        }

                if (team == PlayerTeam.Red)
                {
                     //for (int x = xstart; x < MAPSIZE; x++)
                     //   for (int y = 2; y < MAPSIZE; y++)
                     //       for (int z = MAPSIZE / 2; z < MAPSIZE; z++)
                     //       {
                     //           blockList[x, y, z] = BlockType.None;
                     //           blockCreatorTeam[x, y, z] = PlayerTeam.Red;
                     //       }

                    for (int x = xstart; x < MAPSIZE; x++)
                        for (int y = ystart; y < MAPSIZE; y++)
                            for (int z = 0; z < MAPSIZE; z++)
                            {
                                string line = sr.ReadLine();

                                BlockType bl = (BlockType)int.Parse(line, System.Globalization.CultureInfo.InvariantCulture);
                                //if (bl != BlockType.None)
                                {
                                    if (z > (MAPSIZE / 2) + 3 && y > 2)
                                    {
                                        blockList[x, y, z] = BlockType.None;
                                        blockCreatorTeam[x, y, z] = PlayerTeam.Red;
                                        blockList[x, y, z] = bl;

                                        cost += BlockInformation.GetCost(blockList[x, y, z]);

                                        if (bl != BlockType.None)
                                        {
                                            blockList[x, y, z] = bl;
                                            blockListHP[x, y, z] = BlockInformation.GetMaxHP(bl);
                                            blockCreatorTeam[x, y, z] = team;

                                            switch (bl)
                                            {
                                                case BlockType.Rock:
                                                    blockList[x, y, z] = BlockType.Dirt;
                                                    blockListHP[x, y, z] = BlockInformation.GetMaxHP(BlockType.Dirt);
                                                    blockCreatorTeam[x, y, z] = PlayerTeam.None;
                                                    break;
                                                case BlockType.Ore:
                                                    blockList[x, y, z] = BlockType.Dirt;
                                                    blockListHP[x, y, z] = BlockInformation.GetMaxHP(BlockType.Dirt);
                                                    blockCreatorTeam[x, y, z] = PlayerTeam.None;
                                                    break;
                                                case BlockType.Diamond:
                                                    blockList[x, y, z] = BlockType.Dirt;
                                                    blockListHP[x, y, z] = BlockInformation.GetMaxHP(BlockType.Dirt);
                                                    blockCreatorTeam[x, y, z] = PlayerTeam.None;
                                                    break;
                                                case BlockType.Gold:
                                                    blockList[x, y, z] = BlockType.Dirt;
                                                    blockListHP[x, y, z] = BlockInformation.GetMaxHP(BlockType.Dirt);
                                                    blockCreatorTeam[x, y, z] = PlayerTeam.None;
                                                    break;
                                                case BlockType.BeaconRed:
                                                case BlockType.BeaconBlue:
                                                    Beacon newBeacon = new Beacon();
                                                    newBeacon.ID = GenerateBeaconID();
                                                    newBeacon.Team = bl == BlockType.BeaconRed ? PlayerTeam.Red : PlayerTeam.Blue;
                                                    beaconList[new Vector3(x, y, z)] = newBeacon;
                                                    SendSetBeacon(new Vector3(x, y + 1, z), newBeacon.ID, newBeacon.Team);
                                                    break;
                                                case BlockType.Explosive:
                                                    blockListContent[x, y, z, 1] = 0;//fuse
                                                    break;
                                                case BlockType.ResearchR:
                                                case BlockType.ResearchB:
                                                    blockListContent[x, y, z, 1] = 0;//activated
                                                    blockListContent[x, y, z, 2] = 0;//topic
                                                    blockListContent[x, y, z, 3] = 0;//progress points
                                                    blockListContent[x, y, z, 4] = 10;//timer between updates
                                                    break;
                                                case BlockType.Maintenance:
                                                    blockListContent[x, y, z, 4] = 5;//timer
                                                    break;
                                                case BlockType.Pipe:
                                                    blockListContent[x, y, z, 1] = 0;//Is pipe connected? [0-1]
                                                    blockListContent[x, y, z, 2] = 0;//Is pipe a source? [0-1]
                                                    blockListContent[x, y, z, 3] = 0;//Pipes connected
                                                    blockListContent[x, y, z, 4] = 0;//Is pipe destination?
                                                    blockListContent[x, y, z, 5] = 0;//src x
                                                    blockListContent[x, y, z, 6] = 0;//src y
                                                    blockListContent[x, y, z, 7] = 0;//src z
                                                    blockListContent[x, y, z, 8] = 0;//pipe must not contain liquid
                                                    break;
                                                case BlockType.Barrel:
                                                    blockListContent[x, y, z, 1] = 0;//containtype
                                                    blockListContent[x, y, z, 2] = 0;//amount
                                                    blockListContent[x, y, z, 3] = 0;
                                                    break;
                                                case BlockType.Lever:
                                                    for (int ca = 0; ca < 50; ca++)
                                                        blockListContent[x, y, z, ca] = 0;
                                                    break;
                                                case BlockType.Plate:
                                                    for (int ca = 0; ca < 50; ca++)
                                                        blockListContent[x, y, z, ca] = 0;

                                                    blockListContent[x, y, z, 5] = (byte)BlockType.Plate;
                                                    break;
                                                case BlockType.Hinge:
                                                    for (int ca = 0; ca < 50; ca++)
                                                        blockListContent[x, y, z, ca] = 0;

                                                    blockListContent[x, y, z, 1] = 0;//rotation state [0-1] 0: flat 1: vertical
                                                    blockListContent[x, y, z, 2] = 2;//rotation 
                                                    blockListContent[x, y, z, 3] = 0;//attached block count
                                                    blockListContent[x, y, z, 4] = 0;//attached block count
                                                    blockListContent[x, y, z, 5] = 0;//attached block count
                                                    blockListContent[x, y, z, 6] = 0;//start of block array
                                                    break;
                                                case BlockType.Pump:
                                                    blockListContent[x, y, z, 1] = 0;//direction
                                                    blockListContent[x, y, z, 2] = 0;//x input
                                                    blockListContent[x, y, z, 3] = -1;//y input
                                                    blockListContent[x, y, z, 4] = 0;//z input
                                                    blockListContent[x, y, z, 5] = 0;//x output
                                                    blockListContent[x, y, z, 6] = 1;//y output
                                                    blockListContent[x, y, z, 7] = 0;//z output
                                                    break;
                                                case BlockType.MedicalR:
                                                case BlockType.MedicalB:
                                                    blockListContent[x, y, z, 1] = 10;//half charged for one
                                                    break;
                                                default:
                                                    break;
                                            }
                                        }

                                        for (int rx = 0; rx < 50; rx++)
                                            blockListContent[x, y, z, rx] = 0;
                                    }
                                }
                               
                            }
                }
                else
                {
                //    for (int x = xstart; x < MAPSIZE; x++)
                //        for (int y = 2; y < MAPSIZE; y++)
                //            for (int z = 0; z < MAPSIZE / 2; z++)
                //            {
                //                blockList[x, y, z] = BlockType.None;
                //                blockCreatorTeam[x, y, z] = PlayerTeam.Blue;
                //            }

                    for (int x = xstart; x < MAPSIZE; x++)
                        for (int y = ystart; y < MAPSIZE; y++)
                            for (int z = zstart; z < MAPSIZE; z++)
                            {
                                string line = sr.ReadLine();

                                int tz = MAPSIZE - z;

                                if (z > (MAPSIZE / 2) + 3 && y > 2)
                                {
                                    blockList[x, y, tz] = BlockType.None;
                                   
                                    BlockType bl = (BlockType)int.Parse(line, System.Globalization.CultureInfo.InvariantCulture);
                                    //if (bl != BlockType.None)
                                        blockList[x, y, tz] = bl;

                                    cost += BlockInformation.GetCost(blockList[x, y, tz]);

                                    if (team == PlayerTeam.Blue)
                                    {
                                        BlockType newblock = BlockType.None;
                                        switch (bl)
                                        {//convert blocks
                                            case BlockType.ArtCaseR:
                                                newblock = BlockType.ArtCaseB;
                                                break;
                                            case BlockType.BankRed:
                                                newblock = BlockType.BankBlue;
                                                break;
                                            case BlockType.BaseRed:
                                                newblock = BlockType.BaseBlue;
                                                break;
                                            case BlockType.ConstructionR:
                                                newblock = BlockType.ConstructionB;
                                                break;
                                            case BlockType.ForceR:
                                                newblock = BlockType.ForceB;
                                                break;
                                            case BlockType.GlassR:
                                                newblock = BlockType.GlassB;
                                                break;
                                            case BlockType.InhibitorR:
                                                newblock = BlockType.InhibitorB;
                                                break;
                                            case BlockType.MedicalR:
                                                newblock = BlockType.MedicalB;
                                                break;
                                            case BlockType.RadarRed:
                                                newblock = BlockType.RadarBlue;
                                                break;
                                            case BlockType.ResearchR:
                                                newblock = BlockType.ResearchB;
                                                break;
                                            case BlockType.SolidRed:
                                                newblock = BlockType.SolidBlue;
                                                break;
                                            case BlockType.SolidRed2:
                                                newblock = BlockType.SolidBlue2;
                                                break;
                                            case BlockType.StealthBlockR:
                                                newblock = BlockType.StealthBlockB;
                                                break;
                                            case BlockType.TrapR:
                                                newblock = BlockType.TrapB;
                                                break;
                                            default:
                                                newblock = bl;
                                                break;
                                        }

                                        if (newblock != BlockType.None)
                                        {
                                            blockList[x, y, tz] = newblock;
                                            blockListHP[x, y, tz] = BlockInformation.GetMaxHP(newblock);
                                            blockCreatorTeam[x, y, tz] = team;

                                            switch (bl)
                                            {
                                                case BlockType.Rock:
                                                    blockList[x, y, tz] = BlockType.Dirt;
                                                    blockListHP[x, y, tz] = BlockInformation.GetMaxHP(BlockType.Dirt);
                                                    blockCreatorTeam[x, y, tz] = PlayerTeam.None;
                                                    break;
                                                case BlockType.Ore:
                                                    blockList[x, y, tz] = BlockType.Dirt;
                                                    blockListHP[x, y, tz] = BlockInformation.GetMaxHP(BlockType.Dirt);
                                                    blockCreatorTeam[x, y, tz] = PlayerTeam.None;
                                                    break;
                                                case BlockType.Diamond:
                                                    blockList[x, y, tz] = BlockType.Dirt;
                                                    blockListHP[x, y, tz] = BlockInformation.GetMaxHP(BlockType.Dirt);
                                                    blockCreatorTeam[x, y, tz] = PlayerTeam.None;
                                                    break;
                                                case BlockType.Gold:
                                                    blockList[x, y, tz] = BlockType.Dirt;
                                                    blockListHP[x, y, tz] = BlockInformation.GetMaxHP(BlockType.Dirt);
                                                    blockCreatorTeam[x, y, tz] = PlayerTeam.None;
                                                    break;
                                                case BlockType.BeaconRed:
                                                case BlockType.BeaconBlue:
                                                    Beacon newBeacon = new Beacon();
                                                    newBeacon.ID = GenerateBeaconID();
                                                    newBeacon.Team = bl == BlockType.BeaconRed ? PlayerTeam.Red : PlayerTeam.Blue;
                                                    beaconList[new Vector3(x, y, z)] = newBeacon;
                                                    SendSetBeacon(new Vector3(x, y + 1, z), newBeacon.ID, newBeacon.Team);
                                                    break;
                                                case BlockType.Explosive:
                                                    blockListContent[x, y, tz, 1] = 0;//fuse
                                                    break;
                                                case BlockType.ResearchR:
                                                case BlockType.ResearchB:
                                                    blockListContent[x, y, tz, 1] = 0;//activated
                                                    blockListContent[x, y, tz, 2] = 0;//topic
                                                    blockListContent[x, y, tz, 3] = 0;//progress points
                                                    blockListContent[x, y, tz, 4] = 10;//timer between updates
                                                    break;
                                                case BlockType.Maintenance:
                                                    blockListContent[x, y, tz, 4] = 5;//timer
                                                    break;
                                                case BlockType.Pipe:
                                                    blockListContent[x, y, tz, 1] = 0;//Is pipe connected? [0-1]
                                                    blockListContent[x, y, tz, 2] = 0;//Is pipe a source? [0-1]
                                                    blockListContent[x, y, tz, 3] = 0;//Pipes connected
                                                    blockListContent[x, y, tz, 4] = 0;//Is pipe destination?
                                                    blockListContent[x, y, tz, 5] = 0;//src x
                                                    blockListContent[x, y, tz, 6] = 0;//src y
                                                    blockListContent[x, y, tz, 7] = 0;//src z
                                                    blockListContent[x, y, tz, 8] = 0;//pipe must not contain liquid
                                                    break;
                                                case BlockType.Barrel:
                                                    blockListContent[x, y, tz, 1] = 0;//containtype
                                                    blockListContent[x, y, tz, 2] = 0;//amount
                                                    blockListContent[x, y, tz, 3] = 0;
                                                    break;
                                                case BlockType.Lever:
                                                    for (int ca = 0; ca < 50; ca++)
                                                        blockListContent[x, y, tz, ca] = 0;
                                                    break;
                                                case BlockType.Plate:
                                                    for (int ca = 0; ca < 50; ca++)
                                                        blockListContent[x, y, z, ca] = 0;

                                                    blockListContent[x, y, z, 5] = (byte)BlockType.Plate;
                                                    break;
                                                case BlockType.Hinge:
                                                    for (int ca = 0; ca < 50; ca++)
                                                        blockListContent[x, y, tz, ca] = 0;

                                                    blockListContent[x, y, tz, 1] = 0;//rotation state [0-1] 0: flat 1: vertical
                                                    blockListContent[x, y, tz, 2] = 2;//rotation 
                                                    blockListContent[x, y, tz, 3] = 0;//attached block count
                                                    blockListContent[x, y, tz, 4] = 0;//attached block count
                                                    blockListContent[x, y, tz, 5] = 0;//attached block count
                                                    blockListContent[x, y, tz, 6] = 0;//start of block array
                                                    break;
                                                case BlockType.Pump:
                                                    blockListContent[x, y, tz, 1] = 0;//direction
                                                    blockListContent[x, y, tz, 2] = 0;//x input
                                                    blockListContent[x, y, tz, 3] = -1;//y input
                                                    blockListContent[x, y, tz, 4] = 0;//z input
                                                    blockListContent[x, y, tz, 5] = 0;//x output
                                                    blockListContent[x, y, tz, 6] = 1;//y output
                                                    blockListContent[x, y, tz, 7] = 0;//z output
                                                    break;
                                                case BlockType.MedicalR:
                                                case BlockType.MedicalB:
                                                    blockListContent[x, y, tz, 1] = 10;//half charged for one
                                                    break;
                                                default:
                                                    break;
                                            }
                                        }
                                    }

                                    for (int rx = 0; rx < 50; rx++)
                                        blockListContent[x, y, tz, rx] = 0;
                                }
                            }
                }

                //ConsoleWrite("cost:" + cost);

                if (clientsave)
                {
                    ResearchComplete[(byte)PlayerTeam.Red, (byte)Research.OreRefinery] = 0;
                    ResearchComplete[(byte)PlayerTeam.Blue, (byte)Research.OreRefinery] = 0;
                    //recreate base
                    for (int x = 0; x < MAPSIZE; x++)
                        for (int y = 0; y < MAPSIZE; y++)
                            for (int z = 0; z < MAPSIZE; z++)
                            {
                                if (blockList[x, y, z] == BlockType.Refinery)
                                {
                                    if (ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] < 32)
                                        ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] += 2;
                                    else
                                        ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] += 1;
                                }
                                else if (blockList[x, y, z] == BlockType.BaseRed)
                                {
                                    if (basePosition.ContainsKey(PlayerTeam.Red))
                                    {
                                        basePosition.Remove(PlayerTeam.Red);
                                        createBase(PlayerTeam.Red, x + 2, y + 1, false);//must recreate vacuum
                                    }
                                    else
                                    {
                                        createBase(PlayerTeam.Red, x + 2, y + 1, false);
                                    }
                                }
                                else if (blockList[x, y, z] == BlockType.BaseBlue)
                                {
                                    if (basePosition.ContainsKey(PlayerTeam.Red))
                                    {
                                        basePosition.Remove(PlayerTeam.Blue);
                                        createBase(PlayerTeam.Blue, x + 2, y + 1, false);//must recreate vacuum
                                    }
                                    else
                                    {
                                        createBase(PlayerTeam.Blue, x + 2, y + 1, false);
                                    }
                                }
                            }

                    sr.Close();
                    fs.Close();
                    for (ushort i = 0; i < MAPSIZE; i++)
                        for (ushort j = 0; j < MAPSIZE; j++)
                            for (ushort k = 0; k < MAPSIZE; k++)
                                flowSleep[i, j, k] = false;

                    ConsoleWrite("Fortress loaded!");

                    if (team == PlayerTeam.Blue)
                    {
                      //  varSet("siege", 4, true);
                      //  SendServerMessage("The siege will begin shortly, all players must reconnect!");
                    }
                    return true;
                }
             
                return false;
            }
            catch { ConsoleWrite("Fortress map seems corrupted!"); }
            return false;
        }
        public bool LoadLevel(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    ConsoleWrite("Unable to load level - " + filename + " does not exist!");
                    return false;
                }
                SendServerMessage("Changing map to " + filename + "!");
                disconnectAll();

                //ConsoleWrite("Removing items!");
                FileStream fs = new FileStream(filename, FileMode.Open);
                StreamReader sr = new StreamReader(fs);

                foreach (KeyValuePair<uint, Item> bPair in itemList)//remove all items
                {
                    itemList[bPair.Key].Disposing = true;
                   // itemList.Remove(bPair.Key);
                   // itemIDList.Remove(bPair.Key);
                }

                beaconIDList = new List<string>();
                beaconList = new Dictionary<Vector3, Beacon>();

                //ConsoleWrite("L_Citemcount: " + itemList.Count);
                highestitem = 0;
                teamRegeneration[(byte)PlayerTeam.Red] = 0;//remove regen, doesnt account for artifact regen
                teamRegeneration[(byte)PlayerTeam.Blue] = 0;

                MAPSIZE = int.Parse(sr.ReadLine());
                bool clientsave = bool.Parse(sr.ReadLine());//is this saved from a client rather than server? (lacking all information apart from block geo)

                for (int x = 0; x < MAPSIZE; x++)
                    for (int y = 0; y < MAPSIZE; y++)
                        for (int z = 0; z < MAPSIZE; z++)
                        {
                            string line = sr.ReadLine();
                            string[] fileArgs = line.Split(",".ToCharArray());
                            if (fileArgs.Length == 3 && !clientsave)
                            {
                                blockList[x, y, z] = (BlockType)int.Parse(fileArgs[0], System.Globalization.CultureInfo.InvariantCulture);
                                blockCreatorTeam[x, y, z] = (PlayerTeam)int.Parse(fileArgs[1], System.Globalization.CultureInfo.InvariantCulture);
                                blockListHP[x, y, z] = int.Parse(fileArgs[2], System.Globalization.CultureInfo.InvariantCulture);

                                if (blockList[x, y, z] == BlockType.BaseRed)
                                {
                                    if (basePosition.ContainsKey(PlayerTeam.Red))
                                    {
                                        basePosition[PlayerTeam.Red].X = x+2;
                                        basePosition[PlayerTeam.Red].Y = y+1;
                                        basePosition[PlayerTeam.Red].Z = 50-1;

                                        Beacon newBeacon = new Beacon();
                                        newBeacon.ID = "HOME";
                                        newBeacon.Team = PlayerTeam.Red;
                                        beaconList[new Vector3(x, y - 1, 50)] = newBeacon;
                                    }
                                    else
                                    {
                                        createBase(PlayerTeam.Red, x + 2, y + 1, false);//will be losing data
                                    }
                                }
                                else if (blockList[x, y, z] == BlockType.BaseBlue)
                                {
                                    if (basePosition.ContainsKey(PlayerTeam.Blue))
                                    {
                                        basePosition[PlayerTeam.Blue].X = x+2;
                                        basePosition[PlayerTeam.Blue].Y = y+1;
                                        basePosition[PlayerTeam.Blue].Z = 14-1;

                                        Beacon newBeacon = new Beacon();
                                        newBeacon.ID = "HOME";
                                        newBeacon.Team = PlayerTeam.Blue;
                                        beaconList[new Vector3(x, y - 1, 14)] = newBeacon;
                                    }
                                    else
                                    {
                                        createBase(PlayerTeam.Blue, x + 2, y + 1, false);//will be losing data
                                    }
                                }
                            }
                            else
                            {

                                blockList[x, y, z] = (BlockType)int.Parse(fileArgs[0], System.Globalization.CultureInfo.InvariantCulture);
                            }

                            if (!clientsave)
                            {
                                if (blockList[x, y, z] == BlockType.BeaconBlue || blockList[x, y, z] == BlockType.BeaconRed)
                                {
                                    line = sr.ReadLine();
                                    Beacon newBeacon = new Beacon();
                                    newBeacon.ID = line;
                                    newBeacon.Team = blockList[x, y, z] == BlockType.BeaconRed ? PlayerTeam.Red : PlayerTeam.Blue;
                                    beaconList[new Vector3(x, y, z)] = newBeacon;
                                }
                                for (int rx = 0; rx < 50; rx++)
                                {
                                    string lineC = sr.ReadLine();
                                    blockListContent[x, y, z, rx] = int.Parse(lineC, System.Globalization.CultureInfo.InvariantCulture);
                                }
                            }
                            else
                            {
                                for (int rx = 0; rx < 50; rx++)
                                    blockListContent[x, y, z, rx] = 0;
                            }
                        }

                if (clientsave)
                {
                    ResearchComplete[(byte)PlayerTeam.Red, (byte)Research.OreRefinery] = 0;
                    ResearchComplete[(byte)PlayerTeam.Red, (byte)Research.OreRefinery] = 0;
                    //recreate base
                    for (int x = 0; x < MAPSIZE; x++)
                    for (int y = 0; y < MAPSIZE; y++)
                        for (int z = 0; z < MAPSIZE; z++)
                        {

                            if (blockList[x, y, z] == BlockType.Refinery)
                            {
                                if (ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] < 32)
                                    ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] += 2;
                                else
                                    ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] += 1;
                            }
                            else if (blockList[x, y, z] == BlockType.BaseRed)
                            {
                            if (basePosition.ContainsKey(PlayerTeam.Red))
                            {
                                basePosition.Remove(PlayerTeam.Red);
                                createBase(PlayerTeam.Red, x + 2, y + 1, false);//must recreate vacuum
                            }
                            else
                            {
                                createBase(PlayerTeam.Red, x + 2, y + 1, false);
                            }
                        }
                        else if (blockList[x, y, z] == BlockType.BaseBlue)
                        {
                            if (basePosition.ContainsKey(PlayerTeam.Red))
                            {
                                basePosition.Remove(PlayerTeam.Blue);
                                createBase(PlayerTeam.Blue, x + 2, y + 1, false);//must recreate vacuum
                            }
                            else
                            {
                                createBase(PlayerTeam.Blue, x + 2, y + 1, false);
                            }
                        }
                        }

                    sr.Close();
                    fs.Close();
                    for (ushort i = 0; i < MAPSIZE; i++)
                        for (ushort j = 0; j < MAPSIZE; j++)
                            for (ushort k = 0; k < MAPSIZE; k++)
                                flowSleep[i, j, k] = false;

                    ConsoleWrite("Client-side level loaded successfully - now playing " + filename + "!");
                   
                    return true;
                }
                //ConsoleWrite("Blockstrip completed!");
                string lineIc = sr.ReadLine();
               
                int items = int.Parse(lineIc, System.Globalization.CultureInfo.InvariantCulture);
                //ConsoleWrite("L_itemcount1: " + items);

                for (int ri = 0; ri < items; ri++)
                {
                    string lineC = sr.ReadLine();
                    uint key = uint.Parse(lineC, System.Globalization.CultureInfo.InvariantCulture);

                    lineC = sr.ReadLine();
                    ItemType itype = (ItemType)byte.Parse(lineC, System.Globalization.CultureInfo.InvariantCulture);

                    lineC = sr.ReadLine();
                    bool billboard = bool.Parse(lineC);

                    lineC = sr.ReadLine();
                    string[] fileArgsC = lineC.Split(",".ToCharArray());
                    Vector3 heading = new Vector3(float.Parse(fileArgsC[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(fileArgsC[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(fileArgsC[2], System.Globalization.CultureInfo.InvariantCulture));

                    lineC = sr.ReadLine();
                    fileArgsC = lineC.Split(",".ToCharArray());
                    Vector3 position = new Vector3(float.Parse(fileArgsC[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(fileArgsC[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(fileArgsC[2], System.Globalization.CultureInfo.InvariantCulture));

                    lineC = sr.ReadLine();
                    float scale = float.Parse(lineC, System.Globalization.CultureInfo.InvariantCulture);

                    lineC = sr.ReadLine();
                    PlayerTeam team = (PlayerTeam)byte.Parse(lineC, System.Globalization.CultureInfo.InvariantCulture);

                    lineC = sr.ReadLine();
                    bool dispose = bool.Parse(lineC);

                    //ConsoleWrite("k: " + key + " itype: " + itype + " bill: " + billboard);
                    SetItem(itype, position, heading, Vector3.Zero, team, 0, key);

                    itemList[key].Billboard = billboard;
                    itemList[key].Scale = scale;
                    itemList[key].Disposing = dispose;
                    for (int rx = 0; rx < 20; rx++)
                    {
                        lineC = sr.ReadLine();
                        itemList[key].Content[rx] = int.Parse(lineC, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                //ConsoleWrite("L_Nitemcount: " + itemList.Count);

                for (int rx = 0; rx < 20; rx++)
                {
                    string lineC = sr.ReadLine();
                    artifactActive[1, rx] = int.Parse(lineC, System.Globalization.CultureInfo.InvariantCulture);
                    lineC = sr.ReadLine();
                    artifactActive[2, rx] = int.Parse(lineC, System.Globalization.CultureInfo.InvariantCulture);
                }

                string lineB = sr.ReadLine();
                string[] fileArgsB = lineB.Split(",".ToCharArray());
                if (fileArgsB.Length == 3)
                {
                    basePosition[PlayerTeam.Red].X = int.Parse(fileArgsB[0], System.Globalization.CultureInfo.InvariantCulture);
                    basePosition[PlayerTeam.Red].Y = int.Parse(fileArgsB[1], System.Globalization.CultureInfo.InvariantCulture);
                    basePosition[PlayerTeam.Red].Z = int.Parse(fileArgsB[2], System.Globalization.CultureInfo.InvariantCulture);
                }

                string lineBB = sr.ReadLine();
                string[] fileArgsBB = lineBB.Split(",".ToCharArray());
                if (fileArgsBB.Length == 3)
                {
                    basePosition[PlayerTeam.Blue].X = int.Parse(fileArgsBB[0], System.Globalization.CultureInfo.InvariantCulture);
                    basePosition[PlayerTeam.Blue].Y = int.Parse(fileArgsBB[1], System.Globalization.CultureInfo.InvariantCulture);
                    basePosition[PlayerTeam.Blue].Z = int.Parse(fileArgsBB[2], System.Globalization.CultureInfo.InvariantCulture);
                }

                string lineS = sr.ReadLine();
                string[] fileArgsS = lineS.Split(",".ToCharArray());

                if (fileArgsS.Length == 6)
                {
                    teamCashRed = (uint)(int.Parse(fileArgsS[0], System.Globalization.CultureInfo.InvariantCulture));
                    teamCashBlue = (uint)(int.Parse(fileArgsS[1], System.Globalization.CultureInfo.InvariantCulture));
                    teamOreRed = (int)(int.Parse(fileArgsS[2], System.Globalization.CultureInfo.InvariantCulture));
                    teamOreBlue = (int)(int.Parse(fileArgsS[3], System.Globalization.CultureInfo.InvariantCulture));
                    teamArtifactsRed = (uint)(int.Parse(fileArgsS[4], System.Globalization.CultureInfo.InvariantCulture));
                    teamArtifactsBlue = (uint)(int.Parse(fileArgsS[5], System.Globalization.CultureInfo.InvariantCulture));
                }
                

               // for (int rx = 0; rx < (byte)Research.MAXIMUM; rx++)
                    for (int ry = 0; ry < (byte)Research.TMAXIMUM; ry++)
                    {
                        string lineR = sr.ReadLine();
                        string[] fileArgsR = lineR.Split(",".ToCharArray());
                        if (fileArgsR.Length == 4)
                        {
                            ResearchComplete[(byte)PlayerTeam.Red, ry] = int.Parse(fileArgsR[0], System.Globalization.CultureInfo.InvariantCulture);
                            ResearchProgress[(byte)PlayerTeam.Red, ry] = int.Parse(fileArgsR[1], System.Globalization.CultureInfo.InvariantCulture);
                            ResearchComplete[(byte)PlayerTeam.Blue, ry] = int.Parse(fileArgsR[2], System.Globalization.CultureInfo.InvariantCulture);
                            ResearchProgress[(byte)PlayerTeam.Blue, ry] = int.Parse(fileArgsR[3], System.Globalization.CultureInfo.InvariantCulture);

                            //recreate research
                            for (int rc = 0; rc < ResearchComplete[(byte)PlayerTeam.Red, ry]; rc++)
                            {
                                ResearchRecalculate(PlayerTeam.Red, ry);
                            }
                            for (int rc = 0; rc < ResearchComplete[(byte)PlayerTeam.Blue, ry]; rc++)
                            {
                                ResearchRecalculate(PlayerTeam.Blue, ry);
                            }
                        }
                    }

                sr.Close();
                fs.Close();
                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 0; j < MAPSIZE; j++)
                        for (ushort k = 0; k < MAPSIZE; k++)
                            flowSleep[i, j, k] = false;

                ConsoleWrite("Level loaded successfully - now playing " + filename + "!");
                return true;
            }
            catch { ConsoleWrite("Level " + filename + " seems corrupted!"); }
            return false;
        }

        public void ResetLevel()
        {
            disconnectAll();
            newMap();
        }

        public void disconnectAll()
        {
            foreach (Player p in playerList.Values)
            {
                p.NetConn.Disconnect("",0);  
            }
            playerList.Clear();
        }

        List<string> beaconIDList = new List<string>();
        Dictionary<Vector3, Beacon> beaconList = new Dictionary<Vector3, Beacon>();
        List<uint> itemIDList = new List<uint>();
        Dictionary<uint, Item> itemList = new Dictionary<uint, Item>();
        int highestitem = 0;

        Random randGen = new Random();
        Random randGenB = null;//physics thread random because random aint threadsafe

        int frameid = 10000;
        public string _GenerateBeaconID()
        {
            string id = "K";
            for (int i = 0; i < 3; i++)
                id += (char)randGen.Next(48, 58);
            return id;
        }
        public string GenerateBeaconID()
        {
            string newId = _GenerateBeaconID();
            while (beaconIDList.Contains(newId))
                newId = _GenerateBeaconID();
            beaconIDList.Add(newId);
            return newId;
        }

        public uint _GenerateItemID()
        {
            uint id = (uint)(randGen.Next(1, 2300000));
            return id;
        }
        public uint GenerateItemID()
        {
            uint newId = 1;// _GenerateItemID();
            while (itemIDList.Contains(newId))
            {
                newId++;// = _GenerateItemID();
            }

            if (newId > highestitem)
                highestitem = (int)newId;

            itemIDList.Add(newId);
            return newId;
        }

        public Vector3 GetRegion(Vector3 reg)
        {
            reg = reg / 4;

            return new Vector3((int)reg.X, (int)reg.Y, (int)reg.Z);
           // return (int)((int)reg.X / REGIONSIZE + ((int)reg.Y / REGIONSIZE) * REGIONRATIO + ((int)reg.Z / REGIONSIZE) * REGIONRATIO * REGIONRATIO);
        }
        public Vector3 GetRegion(int x, int y, int z)
        {
            Vector3 gr = new Vector3(x, y, z);
            gr = gr / 4;
            return new Vector3((int)gr.X, (int)gr.Y, (int)gr.Z);
           // return (int)(x / REGIONSIZE + (y / REGIONSIZE) * REGIONRATIO + (z / REGIONSIZE) * REGIONRATIO * REGIONRATIO);
        }

        public uint SetItem(ItemType iType, Vector3 pos, Vector3 heading, Vector3 vel, PlayerTeam team, int val, uint forceID)
        {
            if(forceID == 0)//dont merge when loading
            if(iType == ItemType.Gold || iType == ItemType.Ore)//merge minerals on the ground
            foreach (KeyValuePair<uint, Item> iF in itemList)//pretty inefficient
            {
                    if (Distf(pos, iF.Value.Position) < 2.0f)
                    {
                        if (iType == iF.Value.Type && !iF.Value.Disposing && iF.Value.Content[5] < 10)//limit stacks to 10
                        {
                            iF.Value.Content[5] += 1;//supposed ore content
                            iF.Value.Content[1] = 0;//reset timer
                            if(iType == ItemType.Ore)
                                iF.Value.Scale = 0.5f + (float)(iF.Value.Content[5]) * 0.05f;
                            else
                                iF.Value.Scale = 0.5f + (float)(iF.Value.Content[5]) * 0.1f;
                            SendItemScaleUpdate(iF.Value);
                            return iF.Key;//item does not get created, instead merges
                        }
                    }
            }
            
                Item newItem = new Item(null, iType);
                if (forceID == 0)
                    newItem.ID = GenerateItemID();
                else
                {
                    newItem.ID = forceID;
                    if (newItem.ID > highestitem)
                        highestitem = (int)newItem.ID;

                    itemIDList.Add(newItem.ID);
                }

                newItem.Team = team;
                newItem.Heading = heading;
                newItem.Position = pos;
                newItem.Velocity = vel;

                if (iType == ItemType.Artifact)
                {
                    newItem.Content[10] = val;
                    if (newItem.Content[10] == 0)//undefined artifact, give it a random color
                    {
                        newItem.Content[1] = (int)(randGen.NextDouble() * 100);//r
                        newItem.Content[2] = (int)(randGen.NextDouble() * 100);//g
                        newItem.Content[3] = (int)(randGen.NextDouble() * 100);//b
                    }
                    else if (newItem.Content[10] == 1)//material artifact: generates 10 ore periodically
                    {
                        newItem.Content[1] = (int)(0.6 * 100);//r
                        newItem.Content[2] = (int)(0.6 * 100);//g
                        newItem.Content[3] = (int)(0.6 * 100);//b
                    }
                    else if (newItem.Content[10] == 2)//vampiric artifact
                    {
                        newItem.Content[1] = (int)(0.5 * 100);//r
                        newItem.Content[2] = (int)(0.1 * 100);//g
                        newItem.Content[3] = (int)(0.1 * 100);//b
                    }
                    else if (newItem.Content[10] == 3)//regeneration artifact
                    {
                        newItem.Content[1] = (int)(0.3 * 100);//r
                        newItem.Content[2] = (int)(0.9 * 100);//g
                        newItem.Content[3] = (int)(0.3 * 100);//b
                    }
                    else if (newItem.Content[10] == 4)//aqua artifact: personal: gives waterbreathing, waterspeed and digging underwater, team: gives team water breathing and ability to dig underwater
                    {
                        newItem.Content[1] = (int)(0.5 * 100);//r
                        newItem.Content[2] = (int)(0.5 * 100);//g
                        newItem.Content[3] = (int)(0.8 * 100);//b
                    }
                    else if (newItem.Content[10] == 5)//golden artifact: personal: converts ore to gold, team: generates gold slowly
                    {
                        newItem.Content[1] = (int)(0.87 * 100);//r
                        newItem.Content[2] = (int)(0.71 * 100);//g
                        newItem.Content[3] = (int)(0.25 * 100);//b
                    }
                    else if (newItem.Content[10] == 6)//storm artifact: ground: creates water in empty spaces, personal: periodically shocks opponents, team: protects against aoe
                    {
                        newItem.Content[1] = (int)(0.1 * 100);//r
                        newItem.Content[2] = (int)(0.4 * 100);//g
                        newItem.Content[3] = (int)(0.9 * 100);//b
                    }
                    else if (newItem.Content[10] == 7)//reflection artifact: ground: ?repels bombs?, personal: reflects half damage, team: 
                    {
                        newItem.Content[1] = (int)(0.3 * 100);//r
                        newItem.Content[2] = (int)(0.3 * 100);//g
                        newItem.Content[3] = (int)(0.3 * 100);//b
                    }
                    else if (newItem.Content[10] == 8)//medical artifact: ground: heals any players nearby, personal: allows player to hit friendlies to heal them, team: 
                    {
                        newItem.Content[1] = (int)(0.1 * 100);//r
                        newItem.Content[2] = (int)(1.0 * 100);//g
                        newItem.Content[3] = (int)(0.1 * 100);//b
                    }
                    else if (newItem.Content[10] == 9)//stone artifact: personal: immune to knockback, team: reduces knockback
                    {
                        newItem.Content[1] = (int)(0.9 * 100);//r
                        newItem.Content[2] = (int)(0.7 * 100);//g
                        newItem.Content[3] = (int)(0.7 * 100);//b
                    }
                    else if (newItem.Content[10] == 10)//tremor artifact: ground: causes blocks to fall that arent attached to similar types, personal: makes enemy jump at random, team: reducing the weight of blocks to zero
                    {
                        newItem.Content[1] = (int)(0.1 * 100);//r
                        newItem.Content[2] = (int)(0.1 * 100);//g
                        newItem.Content[3] = (int)(0.1 * 100);//b
                    }
                    else if (newItem.Content[10] == 11)//judgement artifact: ground: personal: deals damage based on hp difference, team: small damage increase
                    {
                        newItem.Content[1] = (int)(1.0 * 100);//r
                        newItem.Content[2] = (int)(1.0 * 100);//g
                        newItem.Content[3] = (int)(0.35 * 100);//b
                    }
                    else if (newItem.Content[10] == 12)//bog artifact: ground: personal: leaves a trail of deepseated mud and immunity to mud, team: immunity to mud
                    {
                        newItem.Content[1] = (int)(0.1 * 100);//r
                        newItem.Content[2] = (int)(0.4 * 100);//g
                        newItem.Content[3] = (int)(0.1 * 100);//b
                    }
                    else if (newItem.Content[10] == 13)//explosive artifact: ground: , personal: kills the holder in 3 seconds, team: boosts explosive damage
                    {
                        newItem.Content[1] = (int)(1.0 * 100);//r
                        newItem.Content[2] = (int)(0.0 * 100);//g
                        newItem.Content[3] = (int)(0.0 * 100);//b
                    }
                    else if (newItem.Content[10] == 14)//armor artifact: ground:, personal: immunity to explosive, team: resistance to explosives
                    {
                        newItem.Content[1] = (int)(0.3 * 100);//r
                        newItem.Content[2] = (int)(0.3 * 100);//g
                        newItem.Content[3] = (int)(0.5 * 100);//b
                    }
                    else if (newItem.Content[10] == 15)//doom artifact: ground: , personal: dooms anyone hit(random timer, persistant), team: 
                    {
                        newItem.Content[1] = (int)(0.0 * 100);//r
                        newItem.Content[2] = (int)(0.0 * 100);//g
                        newItem.Content[3] = (int)(0.0 * 100);//b
                    }
                    else if (newItem.Content[10] == 16)//inferno artifact: ground: randomly melts floor into lava, personal: grants burning attacks, team: allows us to walk on lava
                    {
                        newItem.Content[1] = (int)(0.6 * 100);//r
                        newItem.Content[2] = (int)(0.6 * 100);//g
                        newItem.Content[3] = (int)(0.2 * 100);//b
                    }
                    else if (newItem.Content[10] == 17)//clairvoyance artifact: ground: , personal: gives a personal radar, team: lets us see enemies names further
                    {
                        newItem.Content[1] = (int)(0.3 * 100);//r
                        newItem.Content[2] = (int)(0.7 * 100);//g
                        newItem.Content[3] = (int)(0.8 * 100);//b
                    }
                    else if (newItem.Content[10] == 18)//wings artifact: ground:, personal: allows flight, team: improves our jumping
                    {
                        newItem.Content[1] = (int)(1.0 * 100);//r
                        newItem.Content[2] = (int)(1.0 * 100);//g
                        newItem.Content[3] = (int)(1.0 * 100);//b
                    }
                    else if (newItem.Content[10] == 19)//grapple artifact: ground: pulls player to it, personal: melee attacks cause reverse knockback / extra range / half damage, team: 
                    {
                        newItem.Content[1] = (int)(0.0 * 100);//r
                        newItem.Content[2] = (int)(0.7 * 100);//g
                        newItem.Content[3] = (int)(0.7 * 100);//b
                    }
                    else if (newItem.Content[10] == 20)//decay artifact: ground: causes blocks around it to decay, personal: reverses life regeneration around him, team: granting us a bonus to block damage
                    {
                        newItem.Content[1] = (int)(0.8 * 100);//r
                        newItem.Content[2] = (int)(0.7 * 100);//g
                        newItem.Content[3] = (int)(0.1 * 100);//b
                    }
                    else if (newItem.Content[10] == 21)//precision artifact: ground: causes a sphere of invisibility, personal: allows you to backstab, team: improving team damage
                    {
                        newItem.Content[1] = (int)(0.0 * 100);//r
                        newItem.Content[2] = (int)(1.0 * 100);//g
                        newItem.Content[3] = (int)(1.0 * 100);//b
                    }
                    else if (newItem.Content[10] == 22)//awry artifact: ground: causes everyone to have reversed controls, personal: causes enemies around you to suffer drunken movement, team: causing research topics to change randomly
                    {
                        newItem.Content[1] = (int)(0.2 * 100);//r
                        newItem.Content[2] = (int)(0.7 * 100);//g
                        newItem.Content[3] = (int)(0.2 * 100);//b
                    }
                    else if (newItem.Content[10] == 23)//shield artifact: ground: allowing no one to die, personal: reducing all nearby friendly damage to 10% if they dont attack, team: reducing team damage
                    {
                        newItem.Content[1] = (int)(0.0 * 100);//r
                        newItem.Content[2] = (int)(1.0 * 100);//g
                        newItem.Content[3] = (int)(1.0 * 100);//b
                    }
                    //whisperer
                }
                else if (iType == ItemType.Bomb)
                {
                    newItem.Content[1] = 100;//r
                    newItem.Content[2] = 100;//g
                    newItem.Content[3] = 100;//b
                    newItem.Content[5] = 80;//4 second fuse
                    newItem.Weight = 1.5f;
                }
                else if (iType == ItemType.Rope)
                {
                    newItem.Content[1] = 100;//r
                    newItem.Content[2] = 100;//g
                    newItem.Content[3] = 100;//b
                    newItem.Weight = 0.6f;
                }
                else if (iType == ItemType.Spikes)
                {
                    newItem.Content[1] = 100;//r
                    newItem.Content[2] = 100;//g
                    newItem.Content[3] = 100;//b
                    newItem.Weight = 1.5f;
                }
                else if (iType == ItemType.Mushroom)
                {
                    int mushtype = randGen.Next(1,8);

                    if (mushtype == 1)//life
                    {
                        newItem.Content[1] = (int)(0.2 * 100);//r
                        newItem.Content[2] = (int)(0.8 * 100);//g
                        newItem.Content[3] = (int)(0.2 * 100);//g
                    }
                    else if (mushtype == 2)//poison
                    {
                        newItem.Content[1] = 0;//r
                        newItem.Content[2] = (int)(0.4 * 100);//g
                        newItem.Content[3] = 0;//g
                    }
                    else if (mushtype == 3)//fire
                    {
                        newItem.Content[1] = (int)(0.7 * 100);//r
                        newItem.Content[2] = (int)(0.1 * 100);//g
                        newItem.Content[3] = (int)(0.1 * 100);//g
                    }
                    else if (mushtype == 4)//something
                    {
                        newItem.Content[1] = (int)(0.1 * 100);//r
                        newItem.Content[2] = (int)(0.1 * 100);//g
                        newItem.Content[3] = (int)(0.7 * 100);//g
                    }
                    else
                    {
                        newItem.Content[1] = 100;//r
                        newItem.Content[2] = 100;//g
                        newItem.Content[3] = 100;//b
                    }
                }
                else if (iType == ItemType.Target)
                {
                    if (val == 0)
                    {
                        if (team == PlayerTeam.Red)
                        {
                            newItem.Content[1] = (int)(0.8 * 100);//r
                            newItem.Content[2] = (int)(0.2 * 100);//g
                            newItem.Content[3] = (int)(0.2 * 100);//b
                        }
                        else if (team == PlayerTeam.Blue)
                        {
                            newItem.Content[1] = (int)(0.2 * 100);//r
                            newItem.Content[2] = (int)(0.2 * 100);//g
                            newItem.Content[3] = (int)(0.8 * 100);//b
                        }
                    }
                    else if (val == 1)//gold
                    {
                        newItem.Content[1] = (int)(0.87 * 100);//r
                        newItem.Content[2] = (int)(0.71 * 100);//g
                        newItem.Content[3] = (int)(0.25 * 100);//b
                    }
                    else if (val == 2)//diamond
                    {
                        newItem.Content[1] = (int)(0.9 * 100);//r
                        newItem.Content[2] = (int)(0.9 * 100);//g
                        newItem.Content[3] = (int)(0.9 * 100);//b
                    }
                    else if (val == 3)//artifact safe
                    {
                        newItem.Content[1] = (int)(0.2 * 100);//r
                        newItem.Content[2] = (int)(1.0 * 100);//g
                        newItem.Content[3] = (int)(0.2 * 100);//b
                    }
                }
                else if (iType == ItemType.DirtBomb)
                {
                    newItem.Content[1] = (int)(0.8 * 100);//r
                    newItem.Content[2] = (int)(0.4 * 100);//g
                    newItem.Content[3] = (int)(0.2 * 100);//b
                    newItem.Content[5] = 80;//6 second fuse
                    newItem.Weight = 1.5f;
                }
                else
                {
                    newItem.Content[1] = 100;//r
                    newItem.Content[2] = 100;//g
                    newItem.Content[3] = 100;//b
                }

                itemList[newItem.ID] = newItem;
                SendSetItem(newItem.ID, newItem.Type, newItem.Position, newItem.Team, newItem.Heading);
                return newItem.ID;
        }

        public void SetBlockForPlayer(ushort x, ushort y, ushort z, BlockType blockType, PlayerTeam team, Player player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.BlockSet);
            msgBuffer.Write((byte)x);
            msgBuffer.Write((byte)y);
            msgBuffer.Write((byte)z);

            if (blockType == BlockType.Vacuum)
            {
                msgBuffer.Write((byte)BlockType.None);
            }
            else
            {
                msgBuffer.Write((byte)blockType);
            }

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                {
                    if (playerList[netConn] == player)
                    {
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
                        return;
                    }
                }
        }

        public void SetBlockTex(ushort x, ushort y, ushort z, BlockType blockType, BlockType blockTex, PlayerTeam teamr)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.BlockSetTex);
            msgBuffer.Write((byte)x);
            msgBuffer.Write((byte)y);
            msgBuffer.Write((byte)z);
            msgBuffer.Write((byte)blockType);
            msgBuffer.Write((byte)blockTex);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
        }

        public void SetBlockTexForClient(ushort x, ushort y, ushort z, BlockType blockType, BlockType blockTex, PlayerTeam teamr, NetConnection client)
        {//for connecting clients wanting texture changes
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.BlockSetTex);
            msgBuffer.Write((byte)x);
            msgBuffer.Write((byte)y);
            msgBuffer.Write((byte)z);
            msgBuffer.Write((byte)blockType);
            msgBuffer.Write((byte)blockTex);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    if(client == netConn)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
        }

        public void SetBlock(ushort x, ushort y, ushort z, BlockType blockType, PlayerTeam team)//dont forget duplicate function SetBlock
        {
            if (x <= 0 || y <= 0 || z <= 0 || (int)x > MAPSIZE - 1 || (int)y > MAPSIZE - 1 || (int)z > MAPSIZE - 1)
                return;

            if (blockType == BlockType.None)//block removed, we must unsleep liquids nearby
            {

                Disturb(x, y, z);
            }

            blockListContent[x, y, z, 0] = 0;//dangerous stuff can happen if we dont set this

            switch(blockType)
            {
                case BlockType.BeaconRed:
                case BlockType.BeaconBlue:
                    Beacon newBeacon = new Beacon();
                    newBeacon.ID = GenerateBeaconID();
                    newBeacon.Team = blockType == BlockType.BeaconRed ? PlayerTeam.Red : PlayerTeam.Blue;
                    beaconList[new Vector3(x, y, z)] = newBeacon;
                    SendSetBeacon(new Vector3(x, y+1, z), newBeacon.ID, newBeacon.Team);
                    break;
                case BlockType.Explosive:
                    blockListContent[x, y, z, 1] = 0;//fuse
                    break;
                case BlockType.ResearchR:
                case BlockType.ResearchB:
                    blockListContent[x, y, z, 1] = 0;//activated
                    blockListContent[x, y, z, 2] = 0;//topic
                    blockListContent[x, y, z, 3] = 0;//progress points
                    blockListContent[x, y, z, 4] = 10;//timer between updates
                    break;
                case BlockType.Maintenance:
                    blockListContent[x, y, z, 4] = 5;//timer
                    break;
                case BlockType.Pipe:
                    blockListContent[x, y, z, 1] = 0;//Is pipe connected? [0-1]
                    blockListContent[x, y, z, 2] = 0;//Is pipe a source? [0-1]
                    blockListContent[x, y, z, 3] = 0;//Pipes connected
                    blockListContent[x, y, z, 4] = 0;//Is pipe destination?
                    blockListContent[x, y, z, 5] = 0;//src x
                    blockListContent[x, y, z, 6] = 0;//src y
                    blockListContent[x, y, z, 7] = 0;//src z
                    blockListContent[x, y, z, 8] = 0;//pipe must not contain liquid
                    break;
                case BlockType.Barrel:
                    blockListContent[x, y, z, 1] = 0;//containtype
                    blockListContent[x, y, z, 2] = 0;//amount
                    blockListContent[x, y, z, 3] = 0;
                    break;
                case BlockType.Lever:
                    for (int ca = 0; ca < 50; ca++)
                        blockListContent[x, y, z, ca] = 0;
                    break;
                case BlockType.Plate:
                    for(int ca = 0;ca < 50;ca++)
                        blockListContent[x, y, z, ca] = 0;
                    break;
                case BlockType.Hinge:
                    for (int ca = 0; ca < 50; ca++)
                        blockListContent[x, y, z, ca] = 0;

                    blockListContent[x, y, z, 1] = 0;//rotation state [0-1] 0: flat 1: vertical
                    blockListContent[x, y, z, 2] = 2;//rotation 
                    blockListContent[x, y, z, 3] = 0;//attached block count
                    blockListContent[x, y, z, 4] = 0;//attached block count
                    blockListContent[x, y, z, 5] = 0;//attached block count
                    blockListContent[x, y, z, 6] = 0;//start of block array
                    break;
                case BlockType.Pump:
                    blockListContent[x, y, z, 1] = 0;//direction
                    blockListContent[x, y, z, 2] = 0;//x input
                    blockListContent[x, y, z, 3] = -1;//y input
                    blockListContent[x, y, z, 4] = 0;//z input
                    blockListContent[x, y, z, 5] = 0;//x output
                    blockListContent[x, y, z, 6] = 1;//y output
                    blockListContent[x, y, z, 7] = 0;//z output
                    break;
                case BlockType.MedicalR:
                case BlockType.MedicalB:
                    blockListContent[x, y, z, 1] = 10;//half charged for one
                    break;
                default:
                    break;
            }

            if (blockType == BlockType.None && (blockList[x, y, z] == BlockType.BeaconRed || blockList[x, y, z] == BlockType.BeaconBlue))
            {
                if (beaconList.ContainsKey(new Vector3(x,y,z)))
                    beaconList.Remove(new Vector3(x,y,z));
                SendSetBeacon(new Vector3(x, y+1, z), "", PlayerTeam.None);
            }

            if (blockType == blockList[x, y, z])//duplicate block, no need to send players data
            {
                blockList[x, y, z] = blockType;
                blockCreatorTeam[x, y, z] = team;
                flowSleep[x, y, z] = false;
            }
            else
            {
             
                blockList[x, y, z] = blockType;
                blockCreatorTeam[x, y, z] = team;
                blockListHP[x, y, z] = BlockInformation.GetHP(blockType);
                flowSleep[x, y, z] = false;

                // x, y, z, type, all bytes
                NetBuffer msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.BlockSet);
                msgBuffer.Write((byte)x);
                msgBuffer.Write((byte)y);
                msgBuffer.Write((byte)z);
                if (blockType == BlockType.Vacuum)
                {
                    msgBuffer.Write((byte)BlockType.None);
                }
                else
                {
                    msgBuffer.Write((byte)blockType);
                }
                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder4);//.ReliableUnordered);

            }
            //ConsoleWrite("BLOCKSET: " + x + " " + y + " " + z + " " + blockType.ToString());
        }

        public void SetBlockDebris(ushort x, ushort y, ushort z, BlockType blockType, PlayerTeam team)//dont forget duplicate function SetBlock
        {
            if (x <= 0 || y <= 0 || z <= 0 || (int)x > MAPSIZE - 1 || (int)y > MAPSIZE - 1 || (int)z > MAPSIZE - 1)
                return;

            if (blockType == BlockType.None)//block removed, we must unsleep liquids nearby
            {
                Disturb(x, y, z);
            }

            blockListContent[x, y, z, 0] = 0;//dangerous stuff can happen if we dont set this

            if (blockType == BlockType.BeaconRed || blockType == BlockType.BeaconBlue)
            {
                Beacon newBeacon = new Beacon();
                newBeacon.ID = GenerateBeaconID();
                newBeacon.Team = blockType == BlockType.BeaconRed ? PlayerTeam.Red : PlayerTeam.Blue;
                beaconList[new Vector3(x, y, z)] = newBeacon;
                SendSetBeacon(new Vector3(x, y + 1, z), newBeacon.ID, newBeacon.Team);
            }
            else if (blockType == BlockType.Pipe)
            {
                blockListContent[x, y, z, 1] = 0;//Is pipe connected? [0-1]
                blockListContent[x, y, z, 2] = 0;//Is pipe a source? [0-1]
                blockListContent[x, y, z, 3] = 0;//Pipes connected
                blockListContent[x, y, z, 4] = 0;//Is pipe destination?
                blockListContent[x, y, z, 5] = 0;//src x
                blockListContent[x, y, z, 6] = 0;//src y
                blockListContent[x, y, z, 7] = 0;//src z
                blockListContent[x, y, z, 8] = 0;//pipe must not contain liquid
            }
            else if (blockType == BlockType.Barrel)
            {
                blockListContent[x, y, z, 1] = 0;//containtype
                blockListContent[x, y, z, 2] = 0;//amount
                blockListContent[x, y, z, 3] = 0;
            }
            else if (blockType == BlockType.Pump)
            {
                blockListContent[x, y, z, 1] = 0;//direction
                blockListContent[x, y, z, 2] = 0;//x input
                blockListContent[x, y, z, 3] = -1;//y input
                blockListContent[x, y, z, 4] = 0;//z input
                blockListContent[x, y, z, 5] = 0;//x output
                blockListContent[x, y, z, 6] = 1;//y output
                blockListContent[x, y, z, 7] = 0;//z output
            }

            if (blockType == BlockType.None && (blockList[x, y, z] == BlockType.BeaconRed || blockList[x, y, z] == BlockType.BeaconBlue))
            {
                if (beaconList.ContainsKey(new Vector3(x, y, z)))
                    beaconList.Remove(new Vector3(x, y, z));
                SendSetBeacon(new Vector3(x, y + 1, z), "", PlayerTeam.None);
            }

            if (blockType == blockList[x, y, z])//duplicate block, no need to send players data
            {
                blockList[x, y, z] = blockType;
                blockCreatorTeam[x, y, z] = team;
                flowSleep[x, y, z] = false;
            }
            else
            {
                
                blockList[x, y, z] = blockType;
                blockCreatorTeam[x, y, z] = team;
                flowSleep[x, y, z] = false;

                // x, y, z, type, all bytes
                NetBuffer msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.BlockSetDebris);
                msgBuffer.Write((byte)x);
                msgBuffer.Write((byte)y);
                msgBuffer.Write((byte)z);
                if (blockType == BlockType.Vacuum)
                {
                    msgBuffer.Write((byte)BlockType.None);
                }
                else
                {
                    msgBuffer.Write((byte)blockType);
                }
                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

            }
            //ConsoleWrite("BLOCKSET: " + x + " " + y + " " + z + " " + blockType.ToString());
        }

        public void createBase(PlayerTeam team, int x, int y, bool nokey)
        {
            int pos = randGen.Next(10, 50);
            int posy = 61 - randGen.Next(10, 20);

            if (x > 0)
            {
                pos = x;
                posy = y;
            }

            if(team == PlayerTeam.Red)
            {
                for (int a = -10; a < 10; a++)
                    for (int b = -3; b < 3; b++)
                        for (int c = -10; c < 10; c++)//clear rock
                        {
                            if (blockList[pos + a, posy + b, 50 + c] == BlockType.Rock)
                            {
                                blockList[pos + a, posy + b, 50 + c] = BlockType.Dirt;
                            }
                        }

                for (int a = -3; a < 3; a++)
                    for (int b = -2; b < 3; b++)
                        for (int c = -3; c < 3; c++)//place outer shell
                        {
                            blockList[pos + a, posy + b, 50 + c] = BlockType.SolidRed2;
                            blockListHP[pos + a, posy + b, 50 + c] = 2000;
                            blockCreatorTeam[pos + a, posy + b, 50 + c] = PlayerTeam.None;
                        }

                for (int a = -2; a < 2; a++)
                    for (int b = -1; b < 2; b++)
                        for (int c = -2; c < 2; c++)//prevent players from adding stuff to it
                        {
                            blockList[pos + a, posy + b, 50 + c] = BlockType.Vacuum;
                        }

                blockList[pos - 2, posy - 1, 50 - 2] = BlockType.InhibitorR;
                blockListHP[pos - 2, posy - 1, 50 - 2] = 50000;
                blockCreatorTeam[pos - 2, posy - 1, 50 - 2] = PlayerTeam.None;

                blockList[pos, posy - 1, 50 - 3] = BlockType.TransRed;
                blockList[pos, posy, 50 - 3] = BlockType.TransRed;
                blockList[pos-1, posy - 1, 50 - 3] = BlockType.TransRed;
                blockList[pos-1, posy, 50 - 3] = BlockType.TransRed;

                blockList[pos - 3, posy - 1, 50] = BlockType.TransRed;
                blockList[pos - 3, posy - 1, 50 - 1] = BlockType.TransRed;
                blockList[pos - 3, posy, 50] = BlockType.TransRed;
                blockList[pos - 3, posy, 50 - 1] = BlockType.TransRed;

                blockList[pos + 2, posy - 1, 50] = BlockType.TransRed;
                blockList[pos + 2, posy - 1, 50 - 1] = BlockType.TransRed;
                blockList[pos + 2, posy, 50] = BlockType.TransRed;
                blockList[pos + 2, posy, 50 - 1] = BlockType.TransRed;

                blockList[pos, posy - 1, 50 - 4] = BlockType.None;
                blockList[pos, posy, 50 - 4] = BlockType.None;
                blockList[pos - 1, posy - 1, 50 - 4] = BlockType.None;
                blockList[pos - 1, posy, 50 - 4] = BlockType.None;

                if (!nokey)
                {
                    RedBase = new PlayerBase();
                    basePosition.Add(PlayerTeam.Red, RedBase);
                    basePosition[PlayerTeam.Red].team = PlayerTeam.Red;
                    basePosition[PlayerTeam.Red].X = pos;
                    basePosition[PlayerTeam.Red].Y = posy;
                    basePosition[PlayerTeam.Red].Z = 50;
                }
                blockList[pos - 2, posy - 1, 51] = BlockType.BaseRed;
                //SetBlock((ushort)(pos - 2), (ushort)(posy - 1), 50, BlockType.BeaconRed, PlayerTeam.Red);
                blockList[pos - 2, posy, 48] = BlockType.BankRed;//[pos - 2, posy - 1, 49]
                blockCreatorTeam[pos - 2, posy, 48] = PlayerTeam.None;
                blockListHP[pos - 2, posy, 48] = 50000;

                Beacon newBeacon = new Beacon();
                newBeacon.ID = "HOME";
                newBeacon.Team = PlayerTeam.Red;
                beaconList[new Vector3(pos - 2, posy - 1, 50)] = newBeacon;
                SendSetBeacon(new Vector3(pos - 2, posy, 50), newBeacon.ID, newBeacon.Team);
            }
            else
            {
                for (int a = -10; a < 10; a++)
                    for (int b = -3; b < 3; b++)
                        for (int c = -10; c < 10; c++)
                        {
                            if (blockList[pos + a, posy + b, 14 + c] == BlockType.Rock)
                            {
                                blockList[pos + a, posy + b, 14 + c] = BlockType.Dirt;
                            }
                        }

                for (int a = -3; a < 3; a++)
                    for (int b = -3; b < 3; b++)
                        for (int c = -3; c < 3; c++)
                        {
                            blockList[pos + a, posy + b, 14 + c] = BlockType.SolidBlue2;
                            blockListHP[pos + a, posy + b, 14 + c] = 2000;
                            blockCreatorTeam[pos + a, posy + b, 14 + c] = PlayerTeam.None;
                        }

                for (int a = -2; a < 2; a++)
                    for (int b = -1; b < 2; b++)
                        for (int c = -2; c < 2; c++)
                        {
                            blockList[pos + a, posy + b, 14 + c] = BlockType.Vacuum;
                        }

                blockList[pos-2, posy - 1, 15] = BlockType.InhibitorB;
                blockListHP[pos-2, posy - 1, 15] = 50000;
                blockCreatorTeam[pos-2, posy - 1, 15] = PlayerTeam.None;

                blockList[pos, posy - 1, 14 + 2] = BlockType.TransBlue;
                blockList[pos, posy, 14 + 2] = BlockType.TransBlue;
                blockList[pos - 1, posy - 1, 14 + 2] = BlockType.TransBlue;
                blockList[pos - 1, posy, 14 + 2] = BlockType.TransBlue;

                blockList[pos - 3, posy - 1, 14] = BlockType.TransBlue;
                blockList[pos - 3, posy - 1, 14 - 1] = BlockType.TransBlue;
                blockList[pos - 3, posy, 14] = BlockType.TransBlue;
                blockList[pos - 3, posy, 14 - 1] = BlockType.TransBlue;

                blockList[pos + 2, posy - 1, 14] = BlockType.TransBlue;
                blockList[pos + 2, posy - 1, 14 - 1] = BlockType.TransBlue;
                blockList[pos + 2, posy, 14] = BlockType.TransBlue;
                blockList[pos + 2, posy, 14 - 1] = BlockType.TransBlue;

                blockList[pos, posy - 1, 14 + 3] = BlockType.None;
                blockList[pos, posy, 14 + 3] = BlockType.None;
                blockList[pos - 1, posy - 1, 14 + 3] = BlockType.None;
                blockList[pos - 1, posy, 14 + 3] = BlockType.None;

                if (!nokey)
                {
                    BlueBase = new PlayerBase();
                    basePosition.Add(PlayerTeam.Blue, BlueBase);
                    basePosition[PlayerTeam.Blue].team = PlayerTeam.Blue;
                    basePosition[PlayerTeam.Blue].X = pos;
                    basePosition[PlayerTeam.Blue].Y = posy;
                    basePosition[PlayerTeam.Blue].Z = 14;
                }
                blockList[pos-2, posy-1, 12] = BlockType.BaseBlue;
                blockList[pos-2, posy, 15] = BlockType.BankBlue;
                blockListHP[pos-2, posy, 15] = 50000;
                blockCreatorTeam[pos-2, posy, 15] = PlayerTeam.None;
                //SetBlock((ushort)(pos - 2), (ushort)(posy - 1), 14, BlockType.BeaconBlue, PlayerTeam.Blue);
                Beacon newBeacon = new Beacon();
                newBeacon.ID = "HOME";
                newBeacon.Team = PlayerTeam.Blue;
                beaconList[new Vector3(pos - 2, posy - 1, 14)] = newBeacon;
                SendSetBeacon(new Vector3(pos - 2, posy, 14), newBeacon.ID, newBeacon.Team);
            }
        }
        public void Init()
        {
            physicsEnabled = false;
            Thread.Sleep(2);

            blockList = new BlockType[MAPSIZE, MAPSIZE, MAPSIZE];
            if (varGetI("siege") > 0)
            {
                blockListFort = new BlockType[MAPSIZE, MAPSIZE, MAPSIZE];
                SiegeBuild = true;
            }
            if (varGetI("decaytimer") > 0)
            {
                DECAY_TIMER = varGetI("decaytimer");
            }

            if (varGetI("spawndecaytimer") > 0)
            {
                DECAY_TIMER_SPAWN = varGetI("spawndecaytimer");
            }

            if (varGetI("decaytimerps") > 0)
            {
                DECAY_TIMER_PS = varGetI("decaytimerps");
            }
            blockListContent = new Int32[MAPSIZE, MAPSIZE, MAPSIZE, 50];
            blockListAttach = new Int32[MAPSIZE, MAPSIZE, MAPSIZE, 10];
            blockListHP = new Int32[MAPSIZE, MAPSIZE, MAPSIZE];
            blockCreatorTeam = new PlayerTeam[MAPSIZE, MAPSIZE, MAPSIZE];
            flowSleep = new bool[MAPSIZE, MAPSIZE, MAPSIZE];
            ResearchComplete = new Int32[3, (byte)Research.TMAXIMUM + 1];
            ResearchProgress = new Int32[3, (byte)Research.TMAXIMUM + 1];
            artifactActive = new Int32[3, 20];
            OreMessage = new bool[3];//for maintenance array message
            OreMessage[(byte)PlayerTeam.Red] = false;
            OreMessage[(byte)PlayerTeam.Blue] = false;

            NEW_ART_RED = 0;
            NEW_ART_BLUE = 0;

            allowBlock = new bool[3, 6, (byte)BlockType.MAXIMUM];
            allowItem = new bool[3, 6, (byte)ItemType.MAXIMUM];

            for (int cr = 0; cr < 20; cr++)//clear artifact stores
            {
                artifactActive[0, cr] = 0;
                artifactActive[1, cr] = 0;
                artifactActive[2, cr] = 0;
            }

            for (ushort ct = 0; ct < 3; ct++)
            {
                for (ushort ca = 0; ca < 6; ca++)
                {
                    for (ushort cb = 0; cb < (byte)BlockType.MAXIMUM; cb++)
                    {
                        allowBlock[ct, ca, cb] = false;
                    }

                    for (ushort ce = 0; ce < (byte)ItemType.MAXIMUM; ce++)
                    {
                        allowItem[ct, ca, ce] = false;
                    }

                }
            }

            if(varGetB("sandbox"))
            {
                allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.MagmaVent] = true;
                allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.MagmaVent] = true;
                allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.MagmaVent] = true;
                allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.MagmaVent] = true;
                allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.MagmaVent] = true;
                allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.MagmaVent] = true;

                allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Spring] = true;
                allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Spring] = true;
                allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.Spring] = true;
                allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.Spring] = true;
                allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.Spring] = true;
                allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.Spring] = true;

                //allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.] = true;
                //allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.] = true;
                //allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.] = true;
                //allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.] = true;
                //allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.] = true;
                //allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.] = true;
            }
            allowItem[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)ItemType.Spikes] = true;
            allowItem[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)ItemType.Spikes] = true;
            allowItem[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)ItemType.Target] = true;
            allowItem[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)ItemType.Target] = true;
            allowItem[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)ItemType.DirtBomb] = true;
            allowItem[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)ItemType.DirtBomb] = true;

            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.SolidRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.InhibitorR] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.ArtCaseR] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.BankRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.BeaconRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Barrel] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.GlassR] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Hinge] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Jump] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Ladder] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Lever] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Metal] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Pipe] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Pump] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.RadarRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Maintenance] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.ResearchR] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Shock] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.StealthBlockR] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.TrapR] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Plate] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.MedicalR] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Engineer, (byte)BlockType.Refinery] = true;

            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.SolidBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.InhibitorB] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.ArtCaseB] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.BankBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.BeaconBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Barrel] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.GlassB] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Hinge] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Jump] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Ladder] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Lever] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Metal] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Pipe] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Pump] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.RadarBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Maintenance] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.ResearchB] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Shock] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.StealthBlockB] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.TrapB] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Plate] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.MedicalB] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Engineer, (byte)BlockType.Refinery] = true;

            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Miner, (byte)BlockType.SolidRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Miner, (byte)BlockType.BeaconRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Miner, (byte)BlockType.Hinge] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Miner, (byte)BlockType.Lever] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Miner, (byte)BlockType.Shock] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Miner, (byte)BlockType.Plate] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Miner, (byte)BlockType.Refinery] = true;

            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Miner, (byte)BlockType.SolidBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Miner, (byte)BlockType.BeaconBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Miner, (byte)BlockType.Hinge] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Miner, (byte)BlockType.Lever] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Miner, (byte)BlockType.Shock] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Miner, (byte)BlockType.Plate] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Miner, (byte)BlockType.Refinery] = true;

            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.SolidRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.BeaconRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.Hinge] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.Lever] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.Shock] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.Plate] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.StealthBlockR] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Prospector, (byte)BlockType.TrapR] = true;

            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.SolidBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.BeaconBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.Hinge] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.Lever] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.Shock] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.Plate] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.StealthBlockR] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Prospector, (byte)BlockType.TrapR] = true;

            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.SolidRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.BeaconRed] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.Hinge] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.Lever] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.Shock] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.Explosive] = true;
            allowBlock[(byte)PlayerTeam.Red, (byte)PlayerClass.Sapper, (byte)BlockType.Plate] = true;

            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.SolidBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.BeaconBlue] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.Hinge] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.Lever] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.Shock] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.Explosive] = true;
            allowBlock[(byte)PlayerTeam.Blue, (byte)PlayerClass.Sapper, (byte)BlockType.Plate] = true;

            for (ushort c = 0; c < (byte)Research.TMAXIMUM; c++)
            {
                ResearchComplete[1, c] = 0;
                ResearchProgress[1, c] = ResearchInformation.GetCost((Research)c);
                ResearchComplete[2, c] = 0;
                ResearchProgress[2, c] = ResearchInformation.GetCost((Research)c);
            }
        }
        public void InitSiege()
        {
            physicsEnabled = false;
            SavingFort("fortdefender.lvl");
            basePosition.Clear();
            newMap();
            beaconIDList = new List<string>();
            beaconList = new Dictionary<Vector3, Beacon>();

            LoadFortLevel("fortdefender.lvl", PlayerTeam.Red);
            int redcost = 5113560 + 30000;
                for (int x = 0; x < MAPSIZE - 1; x++)
                    for (int y = 0; y < MAPSIZE - 1; y++)
                        for (int z = (MAPSIZE / 2) + 4; z < MAPSIZE - 1; z++)
                        {
                            redcost -= (int)BlockInformation.GetCost(blockList[x, y, z]);
                        }
            LoadFortLevel("attackingfort.lvl", PlayerTeam.Blue);
            int bluecost = 5113560 + 30000;
            for (int x = 0; x < MAPSIZE - 1; x++)
                for (int y = 0; y < MAPSIZE - 1; y++)
                    for (int z = 1; z < (MAPSIZE / 2) - 4; z++)
                    {
                        bluecost -= (int)BlockInformation.GetCost(blockList[x, y, z]);
                    }
            //ConsoleWrite("red cost:" + redcost + " blue cost:" + bluecost);

            
                //    
                //while (lengthofgold > 0)
                //{
                //    if(wx+gx < MAPSIZE && wx+gx > 0 && wy+gy < MAPSIZE && wy+gy > 0)
                //    if ((BlockInformation.GetMaxHP(blockList[gx+wx, gy+wy, gz+wz]) != 0 && BlockInformation.GetMaxHP(blockList[gx+wx, gy+wy, gz+wz]) < 10) || blockList[gx+wx, gy+wy, gz+wz] == BlockType.None)
                //    {
                //        SetBlock((ushort)(gx + wx), (ushort)(gy + wy), (ushort)(gz + wz), BlockType.Gold, PlayerTeam.None);
                //        blockListHP[gx + wx, gy + wy, gz + wz] = BlockInformation.GetMaxHP(BlockType.Gold);
                //    }

                //    wx += randGen.Next(0, 2) - 1;
                //    wy += randGen.Next(0, 2) - 1;
                //    wz += randGen.Next(0, 2) - 1;

                //    lengthofgold--;
                //}

            //}
            //varSet("siege", 4);//temporary

             //for (ushort i = 0; i < MAPSIZE; i++)
             //       for (ushort j = 0; j < MAPSIZE; j++)
             //           for (ushort k = 0; k < MAPSIZE;k++)
             //           {
             //               if (blockList[i, j, k] == BlockType.Refinery)
             //               {
             //                   if (ResearchComplete[(byte)blockCreatorTeam[i, j, k], (byte)Research.OreRefinery] < 32)
             //                       ResearchComplete[(byte)blockCreatorTeam[i, j, k], (byte)Research.OreRefinery] += 2;
             //                   else
             //                       ResearchComplete[(byte)blockCreatorTeam[i, j, k], (byte)Research.OreRefinery] += 1;
             //               }
             //           }

            //if (varGetI("siege") == 4)
            //{
                BlockType[, ,] worldData = CaveGenerator.GenerateCaveSystem(MAPSIZE, includeLava, oreFactor, false);

                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 0; j < MAPSIZE; j++)
                        for (ushort k = (ushort)(MAPSIZE - 5); k > 0; k--)
                        {
                            flowSleep[i, (ushort)(MAPSIZE - 1 - k), j] = false;
                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Dirt)
                            {
                                blockList[i, (ushort)(MAPSIZE - 1 - k), j] = worldData[i, j, k];
                                blockCreatorTeam[i, (ushort)(MAPSIZE - 1 - k), j] = PlayerTeam.None;
                            }
                            //blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.Dirt;
                            //if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Dirt)
                            //{
                            //    blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.Sand;//covers map with block
                            //}
                            for (ushort c = 0; c < 20; c++)
                            {
                                blockListContent[i, (ushort)(MAPSIZE - 1 - k), k, c] = 0;//content data for blocks, such as pumps
                            }
                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Gold)
                            {
                                blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.Dirt;
                                blockCreatorTeam[i, (ushort)(MAPSIZE - 1 - k), j] = PlayerTeam.None;
                            }
                            else if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Diamond)
                            {
                                blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.Dirt;
                                blockCreatorTeam[i, (ushort)(MAPSIZE - 1 - k), j] = PlayerTeam.None;
                            }

                            blockListHP[i, (ushort)(MAPSIZE - 1 - k), j] = BlockInformation.GetMaxHP(blockList[i, (ushort)(MAPSIZE - 1 - k), j]);
                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Rock)
                            {
                                blockListHP[i, (ushort)(MAPSIZE - 1 - k), j] = 200;
                            }
                        }

                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 0; j < MAPSIZE; j++)
                        for (ushort k = 5; k < 15; k++)
                        {
                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Dirt)
                                if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] != BlockType.MagmaVent && blockList[i, (ushort)(MAPSIZE - 1 - k), j] != BlockType.Spring)
                                    if (randGen.Next(500) == 1)
                                    {
                                        if (i < 1 || j < 1 || k < 1 || i > MAPSIZE - 2 || j > MAPSIZE - 2 || k > MAPSIZE - 2)
                                        {
                                        }
                                        else
                                        {
                                            if (blockList[i - 1, (ushort)(MAPSIZE - 1 - k), j] != BlockType.None)
                                                if (blockList[i + 1, (ushort)(MAPSIZE - 1 - k), j] != BlockType.None)
                                                    if (blockList[i, (ushort)(MAPSIZE - k), j] != BlockType.None)
                                                        if (blockList[i, (ushort)(MAPSIZE + 1 - k), j] != BlockType.None)
                                                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j - 1] != BlockType.None)
                                                                if (blockList[i, (ushort)(MAPSIZE - 1 - k), j + 1] != BlockType.None)
                                                                    blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.MagmaBurst;
                                        }
                                    }
                        }
            //if (varGetI("siege") == 4)
            //{
                ushort gx = (ushort)randGen.Next(4, 57);
                ushort gy = (ushort)randGen.Next(3, 8);
                ushort gz = (ushort)randGen.Next(4, (MAPSIZE / 2) - 4);

               // int totals = 0;//lengthofgold = randGen.Next(2, 7);
                int amount = randGen.Next(3, 4);
                int wx = 0;
                int wy = 0;
                int wz = 0;
                int times = 4;
                int loopbreak = 0;
                while (times > 0)
                {
                    while (amount > 0)
                    {
                        if (wx + gx < MAPSIZE && wx + gx > 0 && wy + gy < 10 && wy + gy > 0)
                        {
                            if ((BlockInformation.GetMaxHP(blockList[gx + wx, gy + wy, gz + wz]) != 0 && BlockInformation.GetMaxHP(blockList[gx + wx, gy + wy, gz + wz]) < 10) || blockList[gx + wx, gy + wy, gz + wz] == BlockType.None)
                            {
                                SetBlock((ushort)(gx + wx), (ushort)(gy + wy), (ushort)(gz + wz), BlockType.Gold, PlayerTeam.None);
                                blockListHP[gx + wx, gy + wy, gz + wz] = BlockInformation.GetMaxHP(BlockType.Gold);
                                amount--;
                                wx += randGen.Next(0, 3) - 1;
                                wy += randGen.Next(0, 3) - 1;
                                wz += randGen.Next(0, 3) - 1;
                                loopbreak = 0;

                                if (amount == 0)
                                    break;
                            }
                            else
                            {
                                wx += randGen.Next(0, 3) - 1;
                                wy += randGen.Next(0, 3) - 1;
                                wz += randGen.Next(0, 3) - 1;
                                loopbreak++;
                                if (loopbreak > 5)
                                    break;
                            }
                        }
                        else
                        {
                            loopbreak++;
                            if (loopbreak > 5)
                                break;
                        }
                    }
                    times--;
                    gx = (ushort)randGen.Next(4, 57);
                    gy = (ushort)randGen.Next(3, 8);
                    gz = (ushort)randGen.Next(4, (MAPSIZE / 2) - 4);
                    amount = randGen.Next(4, 5);
                }
                //other team
                amount = randGen.Next(3, 4);
                times = 4;
                gx = (ushort)randGen.Next(4, 57);
                gy = (ushort)randGen.Next(3, 8);
                gz = (ushort)randGen.Next((MAPSIZE / 2) + 4, MAPSIZE-4);
                while (times > 0)
                {
                    while (amount > 0)
                    {
                        if (wx + gx < MAPSIZE && wx + gx > 0 && wy + gy < 10 && wy + gy > 0)
                        {
                            if ((BlockInformation.GetMaxHP(blockList[gx + wx, gy + wy, gz + wz]) != 0 && BlockInformation.GetMaxHP(blockList[gx + wx, gy + wy, gz + wz]) < 10) || blockList[gx + wx, gy + wy, gz + wz] == BlockType.None)
                            {
                                SetBlock((ushort)(gx + wx), (ushort)(gy + wy), (ushort)(gz + wz), BlockType.Gold, PlayerTeam.None);
                                blockListHP[gx + wx, gy + wy, gz + wz] = BlockInformation.GetMaxHP(BlockType.Gold);
                                amount--;
                                wx += randGen.Next(0, 3) - 1;
                                wy += randGen.Next(0, 3) - 1;
                                wz += randGen.Next(0, 3) - 1;
                                loopbreak = 0;

                                if (amount == 0)
                                    break;
                            }
                            else
                            {
                                wx += randGen.Next(0, 3) - 1;
                                wy += randGen.Next(0, 3) - 1;
                                wz += randGen.Next(0, 3) - 1;
                                loopbreak++;
                                if (loopbreak > 5)
                                    break;
                            }
                        }
                        else
                        {
                            loopbreak++;
                            if (loopbreak > 5)
                                break;
                        }
                    }
                    times--;
                    gx = (ushort)randGen.Next(4, 57);
                    gy = (ushort)randGen.Next(3, 9);
                    gz = (ushort)randGen.Next(0, (MAPSIZE / 2) - 4);
                    amount = randGen.Next(4, 5);
                }
                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                        for (ushort j = (ushort)(MAPSIZE - 1); j > 0; j--)
                        {
                            if (blockList[i, j, k] == BlockType.Dirt || blockList[i, j, k] == BlockType.Grass)
                            {
                                blockList[i, j, k] = BlockType.Grass;
                                blockListContent[i, j, k, 0] = 300;//greenery may reside here

                                //if (randGen.Next(20) == 1)
                                //{
                                //    uint im = SetItem(ItemType.Mushroom, new Vector3(i + 0.5f, j + 1.0f, k + 0.5f), Vector3.Zero, Vector3.Zero, PlayerTeam.None, 0, 0);

                                //    CreateAttach(im, (int)itemList[im].Position.X, (int)itemList[im].Position.Y, (int)itemList[im].Position.Z);
                                //}

                                break;
                            }
                            else if (blockList[i, j, k] != BlockType.None)
                            {
                                break;
                            }
                        }

                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 3; j < MAPSIZE; j++)
                        for (ushort k = 29; k < 36; k++)
                        {
                            blockList[i, j, k] = BlockType.Vacuum;
                        }

                siege_start = DateTime.Now;

            //}

            teamOreBlue = 1000;
            teamOreRed = 1000;
            SiegeBuild = false;
            physicsEnabled = true;
        }
        public int newMap()
        {
            Init();

            if (varGetI("siege") > 0)
            {
                SiegeBuild = true;
                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 0; j < MAPSIZE; j++)
                        for (ushort k = 0; k < MAPSIZE; k++)
                        {
                            blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.None;
                            flowSleep[i, j, k] = false;
                            blockCreatorTeam[i, j, k] = PlayerTeam.None;
                        }

                // for (ushort j = (ushort)(MAPSIZE - (MAPSIZE / 3) + 2); j > 0; j--)
                 for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 2; j < 15; j++)
                        for (ushort k = (ushort)((MAPSIZE / 2) + 4); k < MAPSIZE; k++)
                        {
                            blockList[i, j, k] = BlockType.Dirt;
                        }

                 for (ushort i = 0; i < MAPSIZE; i++)
                     for (ushort j = 0; j < 3; j++)
                         for (ushort k = 0; k < MAPSIZE; k++)
                         {
                             blockList[i, j, k] = BlockType.Lava;
                         }

                 //for (ushort i = 0; i < MAPSIZE; i++)
                 //    for (ushort j = (ushort)(MAPSIZE - (MAPSIZE / 10)); j < MAPSIZE; j++)
                 //        for (ushort k = 0; k < MAPSIZE; k++)
                 //        {
                 //            blockList[i, j, k] = BlockType.Lava;
                 //        }

                createBase(PlayerTeam.Red, MAPSIZE / 2, 8, false);
                createBase(PlayerTeam.Blue, MAPSIZE / 2, 8, false);

                //createBase(PlayerTeam.Red, MAPSIZE / 2, MAPSIZE - 20, true);
                //createBase(PlayerTeam.Blue, MAPSIZE / 2, MAPSIZE - 20, true);

                if (varGetI("siege") == 4)
                {
                    BlockType[, ,] worldData = CaveGenerator.GenerateCaveSystem(MAPSIZE, includeLava, oreFactor, includeWater);

                    for (ushort i = 0; i < MAPSIZE; i++)
                        for (ushort j = 5; j < 15; j++)
                            for (ushort k = 0; k < MAPSIZE; k++)
                            {
                                flowSleep[i, j, k] = false;
                                if (blockList[i, j, k] == BlockType.Dirt)
                                {
                                   // if(worldData[i, j, k] != BlockType.W)
                                    blockList[i, (ushort)(MAPSIZE - 1 - k), j] = worldData[i, j, k];
                                }
                                //blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.Dirt;
                                //if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Dirt)
                                //{
                                //    blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.Sand;//covers map with block
                                //}
                                for (ushort c = 0; c < 20; c++)
                                {
                                    blockListContent[i, (ushort)(MAPSIZE - 1 - k), k, c] = 0;//content data for blocks, such as pumps
                                }

                                blockListHP[i, (ushort)(MAPSIZE - 1 - k), j] = BlockInformation.GetMaxHP(blockList[i, (ushort)(MAPSIZE - 1 - k), j]);

                                if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Gold)
                                {
                                    blockListHP[i, (ushort)(MAPSIZE - 1 - k), j] = 40;
                                }
                                else if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Diamond)
                                {
                                    blockListHP[i, (ushort)(MAPSIZE - 1 - k), j] = 120;
                                }
                               
                            }

                    for (ushort i = 0; i < MAPSIZE; i++)
                        for (ushort j = 5; j < 15; j++)
                            for (ushort k = 0; k < MAPSIZE; k++)
                            {
                                if(blockList[i, j, k] == BlockType.Dirt)
                                if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] != BlockType.MagmaVent && blockList[i, (ushort)(MAPSIZE - 1 - k), j] != BlockType.Spring)
                                    if (randGen.Next(500) == 1)
                                    {
                                        if (i < 1 || j < 1 || k < 1 || i > MAPSIZE - 2 || j > MAPSIZE - 2 || k > MAPSIZE - 2)
                                        {
                                        }
                                        else
                                        {
                                            if (blockList[i - 1, (ushort)(MAPSIZE - 1 - k), j] != BlockType.None)
                                                if (blockList[i + 1, (ushort)(MAPSIZE - 1 - k), j] != BlockType.None)
                                                    if (blockList[i, (ushort)(MAPSIZE - k), j] != BlockType.None)
                                                        if (blockList[i, (ushort)(MAPSIZE + 1 - k), j] != BlockType.None)
                                                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j - 1] != BlockType.None)
                                                                if (blockList[i, (ushort)(MAPSIZE - 1 - k), j + 1] != BlockType.None)
                                                                    blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.MagmaBurst;
                                        }
                                    }
                            }

                    for (ushort i = 0; i < MAPSIZE; i++)
                        for (ushort k = 0; k < MAPSIZE; k++)
                            for (ushort j = (ushort)(MAPSIZE - 1); j > 0; j--)
                            {
                                if (blockList[i, j, k] == BlockType.Dirt || blockList[i, j, k] == BlockType.Grass)
                                {
                                    blockList[i, j, k] = BlockType.Grass;
                                    blockListContent[i, j, k, 0] = 300;//greenery may reside here

                                    //if (randGen.Next(20) == 1)
                                    //{
                                    //    uint im = SetItem(ItemType.Mushroom, new Vector3(i + 0.5f, j + 1.0f, k + 0.5f), Vector3.Zero, Vector3.Zero, PlayerTeam.None, 0, 0);

                                    //    CreateAttach(im, (int)itemList[im].Position.X, (int)itemList[im].Position.Y, (int)itemList[im].Position.Z);
                                    //}

                                    break;
                                }
                                else if (blockList[i, j, k] != BlockType.None)
                                {
                                    break;
                                }
                            }

                }
                else
                {
                    teamOreRed = 30000;
                    teamCashRed = 0;
                    teamOreBlue = 0;// 30000;
                    teamCashBlue = 0;
                }

            }
            else
            {
                BlockType[, ,] worldData = CaveGenerator.GenerateCaveSystem(MAPSIZE, includeLava, oreFactor, includeWater);
            
                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 0; j < MAPSIZE; j++)
                        for (ushort k = 0; k < MAPSIZE; k++)
                        {
                            flowSleep[i, j, k] = false;
                            blockList[i, (ushort)(MAPSIZE - 1 - k), j] = worldData[i, j, k];
                            //blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.Dirt;
                            //if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Dirt)
                            //{
                            //    blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.Sand;//covers map with block
                            //}
                            for (ushort c = 0; c < 20; c++)
                            {
                                blockListContent[i, (ushort)(MAPSIZE - 1 - k), k, c] = 0;//content data for blocks, such as pumps
                            }

                            blockListHP[i, (ushort)(MAPSIZE - 1 - k), j] = BlockInformation.GetHP(blockList[i, (ushort)(MAPSIZE - 1 - k), j]);

                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Gold)
                            {
                                blockListHP[i, (ushort)(MAPSIZE - 1 - k), j] = 40;
                            }
                            else if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] == BlockType.Diamond)
                            {
                                blockListHP[i, (ushort)(MAPSIZE - 1 - k), j] = 120;
                            }
                            else
                            {

                            }

                            blockCreatorTeam[i, j, k] = PlayerTeam.None;

                            if (i < 1 || j < 1 || k < 1 || i > MAPSIZE - 2 || j > MAPSIZE - 2 || k > MAPSIZE - 2)
                            {
                                blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.None;
                            }
                        }

                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 0; j < MAPSIZE; j++)
                        for (ushort k = 0; k < MAPSIZE; k++)
                        {
                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j] != BlockType.MagmaVent && blockList[i, (ushort)(MAPSIZE - 1 - k), j] != BlockType.Spring)
                                if (randGen.Next(500) == 1)
                                {
                                    if (i < 1 || j < 1 || k < 1 || i > MAPSIZE - 2 || j > MAPSIZE - 2 || k > MAPSIZE - 2)
                                    {
                                    }
                                    else
                                    {
                                        if (blockList[i - 1, (ushort)(MAPSIZE - 1 - k), j] != BlockType.None)
                                            if (blockList[i + 1, (ushort)(MAPSIZE - 1 - k), j] != BlockType.None)
                                                if (blockList[i, (ushort)(MAPSIZE - k), j] != BlockType.None)
                                                    if (blockList[i, (ushort)(MAPSIZE + 1 - k), j] != BlockType.None)
                                                        if (blockList[i, (ushort)(MAPSIZE - 1 - k), j - 1] != BlockType.None)
                                                            if (blockList[i, (ushort)(MAPSIZE - 1 - k), j + 1] != BlockType.None)
                                                                blockList[i, (ushort)(MAPSIZE - 1 - k), j] = BlockType.MagmaBurst;
                                    }
                                }
                        }
                //add bases
                createBase(PlayerTeam.Red, 0, 0, false);
                createBase(PlayerTeam.Blue, 0, 0, false);

                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                        for (ushort j = (ushort)(MAPSIZE - 1); j > 0; j--)
                        {
                            if (blockList[i, j, k] == BlockType.Dirt || blockList[i, j, k] == BlockType.Grass)
                            {
                                blockList[i, j, k] = BlockType.Grass;
                                blockListContent[i, j, k, 0] = 300;//greenery may reside here

                                //if (randGen.Next(20) == 1)
                                //{
                                //    uint im = SetItem(ItemType.Mushroom, new Vector3(i + 0.5f, j + 1.0f, k + 0.5f), Vector3.Zero, Vector3.Zero, PlayerTeam.None, 0, 0);

                                //    CreateAttach(im, (int)itemList[im].Position.X, (int)itemList[im].Position.Y, (int)itemList[im].Position.Z);
                                //}

                                break;
                            }
                            else if (blockList[i, j, k] != BlockType.None)
                            {
                                break;
                            }
                        }


                //for (int i = 0; i < MAPSIZE * 2; i++)
                //{
                //    DoStuff();
                //}
                physicsEnabled = true;
            }
            return 1;
        }

        public void CreateAttach(uint i, int x, int y, int z)
        {
            for(int a = 0;a < 5;a+=2)
            {
                if(blockListAttach[x,y,z,a] == 0)
                {
                    blockListAttach[x, y, z, a] = 2;//item
                    blockListAttach[x, y, z, a + 1] = (int)i;
                    break;
                }
            }
        }
        public void Sunray()
        {
             ushort i = (ushort)(randGen.Next(MAPSIZE - 1));
             ushort k = (ushort)(randGen.Next(MAPSIZE - 1));
            
                    for (ushort j = (ushort)(MAPSIZE-1); j > 0; j--)
                    {
                        if (blockList[i, j, k] == BlockType.Dirt || blockList[i, j, k] == BlockType.Grass)
                        {
                            if(blockListContent[i, j, k, 0] < 150)
                                blockListContent[i, j, k, 0] = blockListContent[i, j, k, 0] + 100;//greenery may reside here
                            break;
                        }
                        else if (blockList[i, j, k] != BlockType.None)
                        {
                            return;
                        }
                    }
        }
        public double Get3DDistance(int x1, int y1, int z1, int x2, int y2, int z2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;
            int dz = z2 - z1;
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return distance;
        }
        public double Distf(Vector3 x, Vector3 y)
        {
            float dx = y.X - x.X;
            float dy = y.Y - x.Y;
            float dz = y.Z - x.Z;
            float dist = (float)(Math.Sqrt(dx * dx + dy * dy + dz * dz));
            return dist;
        }
        public string GetExplosionPattern(int n)
        {
            string output="";
            int radius = (int)Math.Ceiling((double)varGetI("explosionradius"));
            int size = radius * 2 + 1;
            int center = radius; //Not adding one because arrays start from 0
            for (int z = n; z==n&&z<size; z++)
            {
                ConsoleWrite("Z" + z + ": ");
                output += "Z" + z + ": ";
                for (int x = 0; x < size; x++)
                {
                    string output1 = "";
                    for (int y = 0; y < size; y++)
                    {
                        output1+=tntExplosionPattern[x, y, z] ? "1, " : "0, ";
                    }
                    ConsoleWrite(output1);
                }
                output += "\n";
            }
            return "";
        }

        public void CalculateExplosionPattern()
        {
            int radius = (int)Math.Ceiling((double)varGetI("explosionradius"));
            int size = radius * 2 + 1;
            tntExplosionPattern = new bool[size, size, size];
            int center = radius; //Not adding one because arrays start from 0
            for(int x=0;x<size;x++)
                for(int y=0;y<size;y++)
                    for (int z = 0; z < size; z++)
                    {
                        if (x == y && y == z && z == center)
                            tntExplosionPattern[x, y, z] = true;
                        else
                        {
                            double distance = Get3DDistance(center, center, center, x, y, z);//Use center of blocks
                            if (distance <= (double)varGetI("explosionradius"))
                                tntExplosionPattern[x, y, z] = true;
                            else
                                tntExplosionPattern[x, y, z] = false;
                        }
                    }
        }

        public void status()
        {
            ConsoleWrite("[IF]" + varGetS("name"));//serverName);
            ConsoleWrite(playerList.Count + " / " + varGetI("maxplayers") + " players");
            foreach (string name in varBoolBindings.Keys)
            {
                ConsoleWrite(name + " = " + varBoolBindings[name]);
            }
        }

        public bool Start()
        {
            //Setup the variable toggles
            varBindingsInitialize();
            int tmpMaxPlayers = 16;

            // Create default server config if it doesn't exist
            if (!File.Exists("server.config.txt"))
            {
                using (StreamWriter writer = new StreamWriter("server.config.txt"))
                {
                    writer.WriteLine("maxplayers=16");
                    writer.WriteLine("public=false");
                    writer.WriteLine("servername=An Infinifortress Server");
                    writer.WriteLine("sandbox=false");
                    writer.WriteLine("enforceteams=false");
                    writer.WriteLine("voting=true");
                    writer.WriteLine("notnt=false");
                    writer.WriteLine("sphericaltnt=true");
                    writer.WriteLine("minelava=false");
                    writer.WriteLine("autoban=true");
                    writer.WriteLine("warnings=4");
                    writer.WriteLine("siege=0");
                    writer.WriteLine("decaytimer=1000");
                    writer.WriteLine("spawndecaytimer=3000");
                    writer.WriteLine("decaytimerps=2000");
                    writer.WriteLine("autosave=4");
                    writer.WriteLine("autoannounce=true");
                    writer.WriteLine("winningcash=10");
                    writer.WriteLine("includelava=true");
                    writer.WriteLine("includewater=true");
                    writer.WriteLine("orefactor=10");
                    writer.WriteLine("serverListURL=http://zuzu-is.online");
                }
            }

            // Read in from the config file.
            DatafileWriter dataFile = new DatafileWriter("server.config.txt");
            if (dataFile.Data.ContainsKey("winningcash"))
                winningCashAmount = uint.Parse(dataFile.Data["winningcash"], System.Globalization.CultureInfo.InvariantCulture);
            if (dataFile.Data.ContainsKey("includelava"))
                includeLava = bool.Parse(dataFile.Data["includelava"]);
            if (dataFile.Data.ContainsKey("includewater"))
                includeWater = bool.Parse(dataFile.Data["includewater"]);
            if (dataFile.Data.ContainsKey("orefactor"))
                oreFactor = uint.Parse(dataFile.Data["orefactor"], System.Globalization.CultureInfo.InvariantCulture);
            if (dataFile.Data.ContainsKey("maxplayers"))
                tmpMaxPlayers = (int)Math.Min(32, uint.Parse(dataFile.Data["maxplayers"], System.Globalization.CultureInfo.InvariantCulture));
            if (dataFile.Data.ContainsKey("public"))
                varSet("public", bool.Parse(dataFile.Data["public"]), true);
            if (dataFile.Data.ContainsKey("servername"))
                varSet("name", dataFile.Data["servername"], true);
            if (dataFile.Data.ContainsKey("serverListURL"))
                varSet("serverListURL", dataFile.Data["serverListURL"], true);
            if (dataFile.Data.ContainsKey("sandbox"))
                varSet("sandbox", bool.Parse(dataFile.Data["sandbox"]), true);
            if (dataFile.Data.ContainsKey("enforceteams"))
                varSet("enforceteams", bool.Parse(dataFile.Data["enforceteams"]), true);
            if (dataFile.Data.ContainsKey("voting"))
                varSet("voting", bool.Parse(dataFile.Data["voting"]), true);
            if (dataFile.Data.ContainsKey("notnt"))
                varSet("tnt", !bool.Parse(dataFile.Data["notnt"]), true);
            if (dataFile.Data.ContainsKey("sphericaltnt"))
                varSet("stnt", bool.Parse(dataFile.Data["sphericaltnt"]), true);
            if (dataFile.Data.ContainsKey("minelava"))
                varSet("minelava", bool.Parse(dataFile.Data["minelava"]), true);
            if (dataFile.Data.ContainsKey("autoban"))
                varSet("autoban", bool.Parse(dataFile.Data["autoban"]), true);
            if (dataFile.Data.ContainsKey("warnings"))
                varSet("warnings", int.Parse(dataFile.Data["warnings"], System.Globalization.CultureInfo.InvariantCulture),true);
            if (dataFile.Data.ContainsKey("siege"))
                varSet("siege", int.Parse(dataFile.Data["siege"], System.Globalization.CultureInfo.InvariantCulture), true);
            if (dataFile.Data.ContainsKey("decaytimer"))
                varSet("decaytimer", int.Parse(dataFile.Data["decaytimer"], System.Globalization.CultureInfo.InvariantCulture), true);
            if (dataFile.Data.ContainsKey("spawndecaytimer"))
                varSet("spawndecaytimer", int.Parse(dataFile.Data["spawndecaytimer"], System.Globalization.CultureInfo.InvariantCulture), true);
            if (dataFile.Data.ContainsKey("decaytimerps"))
                varSet("decaytimerps", int.Parse(dataFile.Data["decaytimerps"], System.Globalization.CultureInfo.InvariantCulture), true);
            if (dataFile.Data.ContainsKey("autosave"))
                varSet("autosave", int.Parse(dataFile.Data["autosave"], System.Globalization.CultureInfo.InvariantCulture), true);
            if (dataFile.Data.ContainsKey("levelname"))
                levelToLoad = dataFile.Data["levelname"];
            if (dataFile.Data.ContainsKey("greeter"))
                varSet("greeter", dataFile.Data["greeter"],true);

            bool autoannounce = true;
            if (dataFile.Data.ContainsKey("autoannounce"))
                autoannounce = bool.Parse(dataFile.Data["autoannounce"]);

            // Load the ban-list.
            banList = LoadBanList();
            // Load the admin-list
            admins = LoadAdminList();

            if (tmpMaxPlayers>=0)
                varSet("maxplayers", tmpMaxPlayers, true);
 
            // Initialize the server.
            NetConfiguration netConfig = new NetConfiguration("InfiniminerPlus");
            netConfig.MaxConnections = (int)varGetI("maxplayers");
            netConfig.Port = 5565;
            netServer = new InfiniminerNetServer(netConfig);
            netServer.SetMessageTypeEnabled(NetMessageType.ConnectionApproval, true);

            //netServer.SimulatedMinimumLatency = 0.5f;//5f;
            //netServer.SimulatedLatencyVariance = 0.05f;
            //netServer.SimulatedLoss = 0.05f;
            //netServer.SimulatedDuplicates = 0.02f;
            //netServer.Configuration.SendBufferSize = 2048000;
            //netServer.Start();//starts too early
            // Initialize variables we'll use.
            NetBuffer msgBuffer = netServer.CreateBuffer();
            NetMessageType msgType;
            NetConnection msgSender;

            // Store the last time that we did a flow calculation.
            DateTime lastFlowCalc = DateTime.Now;
            DateTime lastFlowCalcZ = DateTime.Now;//temporary
            DateTime sysTimer = DateTime.Now;
            //Check if we should autoload a level
            if (dataFile.Data.ContainsKey("autoload") && bool.Parse(dataFile.Data["autoload"]))
            {
                Init();
                LoadLevel(levelToLoad);

                lavaBlockCount = 0;
                waterBlockCount = 0;
                int burstBlockCount = 0;

                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 0; j < MAPSIZE; j++)
                        for (ushort k = 0; k < MAPSIZE; k++)
                        {
                            if (blockList[i, j, k] == BlockType.Lava)
                            {
                                lavaBlockCount += 1;
                            }
                            else if (blockList[i, j, k] == BlockType.Water)
                            {
                                waterBlockCount += 1;
                            }
                            else if (blockList[i, j, k] == BlockType.MagmaBurst)
                            {
                                burstBlockCount += 1;
                            }
                        }

                ConsoleWrite(waterBlockCount + " water blocks, " + lavaBlockCount + " lava blocks, " + burstBlockCount + " possible bursts." ); 
            }
            else
            {
                // Calculate initial lava flows.
                ConsoleWrite("CALCULATING INITIAL LIQUID BLOCKS");
                newMap();

                lavaBlockCount = 0;
                waterBlockCount = 0;
                int burstBlockCount = 0;

                //for (int precalc = 0; precalc < 100; precalc++)
                //{
                //    Sunray();
                //    DoStuff();
                //}

                for (ushort i = 0; i < MAPSIZE; i++)
                    for (ushort j = 0; j < MAPSIZE; j++)
                        for (ushort k = 0; k < MAPSIZE; k++)
                        {
                            if (blockList[i, j, k] == BlockType.Lava)
                            {
                                lavaBlockCount += 1;
                            }
                            else if (blockList[i, j, k] == BlockType.Water)
                            {
                                waterBlockCount += 1;
                            }
                            else if (blockList[i, j, k] == BlockType.MagmaBurst)
                            {
                                burstBlockCount += 1;
                            }
                        }

                ConsoleWrite(waterBlockCount + " water blocks, " + lavaBlockCount + " lava blocks, " + burstBlockCount + " possible bursts.");        
            }
            
            //Caculate the shape of spherical tnt explosions
            CalculateExplosionPattern();
            
            // Send the initial server list update.
            if (autoannounce)
                PublicServerListUpdate(true);

            lastMapBackup = DateTime.Now;
           
            DateTime lastFPScheck = DateTime.Now;
            double frameRate = 0;
            
            // Main server loop!
            netServer.Start();
            ConsoleWrite("SERVER READY");

            if (!physics.IsAlive)
            {
                ConsoleWrite("Physics thread is limp.");
            }

            while (keepRunning)
            {
                if (!physics.IsAlive)
                {
                    ConsoleWrite("Physics thread died.");
                   // physics.Abort();
                   // physics.Join();
                    //physics.Start();
                }

                frameCount = frameCount + 1;
                if (lastFPScheck <= DateTime.Now - TimeSpan.FromMilliseconds(1000))
                {
                    lastFPScheck = DateTime.Now;
                    frameRate = frameCount;// / gameTime.ElapsedTotalTime.TotalSeconds;
                    
                    if (sleeping == false && frameCount < 20)
                    {
                        ConsoleWrite("Heavy load: " + frameCount + " FPS");
                    }
                    frameCount = 0;
                }
                
                // Process any messages that are here.
                while (netServer.ReadMessage(msgBuffer, out msgType, out msgSender))
                {
                    try
                    {
                        switch (msgType)
                        {
                            case NetMessageType.ConnectionApproval:
                                {
                                    Player newPlayer = new Player(msgSender, null);
                                    newPlayer.Handle = Defines.Sanitize(msgBuffer.ReadString()).Trim();
                                    if (newPlayer.Handle.Length == 0)
                                    {
                                        newPlayer.Handle = "Player";
                                    }

                                    string clientVersion = msgBuffer.ReadString();
                                    if (clientVersion != Defines.INFINIMINER_VERSION)
                                    {
                                        msgSender.Disapprove("VER;" + Defines.INFINIMINER_VERSION);
                                    }
                                    else if (banList.Contains(newPlayer.IP))
                                    {
                                        msgSender.Disapprove("BAN;");
                                    }/*
                                else if (playerList.Count == maxPlayers)
                                {
                                    msgSender.Disapprove("FULL;");
                                }*/
                                    else
                                    {
                                        if (admins.ContainsKey(newPlayer.IP))
                                            newPlayer.admin = admins[newPlayer.IP];

                                        bool resume = false;
                                        foreach (Player p in playerList.Values)
                                        {
                                            if(p.Disposing && !p.Quit || p.LastUpdate < DateTime.Now - TimeSpan.FromSeconds(10) && !p.Quit)//players quitting normally forfeit immediately
                                            if (p.IP == msgSender.RemoteEndpoint.Address.ToString() && newPlayer.Handle == p.Handle)
                                            {
                                                p.LastUpdate = DateTime.Now;
                                                resume = true;
                                                NetConnection old = p.NetConn;
                                                p.NetConn = msgSender;
                                                p.Resume = true;
                                                p.Disposing = false;
                                                playerList.Add(p.NetConn, p);
                                                playerList.Remove(old);

                                                //ConsoleWrite(p.Handle + " reconnected.");
                                                break;
                                            }
                                        }

                                        if(resume == false)
                                        playerList[msgSender] = newPlayer;
                                        
                                        //Check if we should compress the map for the client
                                        try
                                        {
                                            bool compression = msgBuffer.ReadBoolean();
                                            if (compression)
                                            {
                                                playerList[msgSender].compression = true;
                                            }
                                        } catch { }
                                        toGreet.Add(msgSender);

                                        this.netServer.SanityCheck(msgSender);
                                        msgSender.Approve();
                                        PublicServerListUpdate(true);
                                    }
                                }
                                break;

                            case NetMessageType.StatusChanged:
                                {
                                    if (!this.playerList.ContainsKey(msgSender))
                                    {
                                        break;
                                    }

                                    Player player = playerList[msgSender];

                                    if (msgSender.Status == NetConnectionStatus.Connected)
                                    {
                                        if (sleeping == true)
                                        {
                                            sleeping = false;
                                            physicsEnabled = true;
                                        }

                                        player.connectedTimer = DateTime.Now;

                                        if (player.Resume)//resuming connection
                                        {
                                            ConsoleWrite("RECONNECT: " + playerList[msgSender].Handle + " ( " + playerList[msgSender].IP + " )");

                                            SendPlayerJoined(player, player.Resume);
                                            SendPlayerPosition(player);
                                            
                                            if (player.Alive)
                                                SendPlayerAlive(player);

                                            SendContentSpecificUpdate(player, 10);//tell player about his arties
                                            SendContentSpecificUpdate(player, 11);//tell player about his powerstones
                                            SendContentSpecificUpdate(player, 5);//tell player about his ability
                                            SendContentSpecificUpdate(player, 6);//tell player about his ability

                                            SendResourceUpdate(player);
                                        }
                                        else
                                        {
                                            ConsoleWrite("CONNECT: " + playerList[msgSender].Handle + " ( " + playerList[msgSender].IP + " )");
                                        }

                                        SendCurrentMap(msgSender);
                                        if (!player.Resume)
                                            SendPlayerJoined(player, player.Resume);

                                        PublicServerListUpdate();
                                    }
                                    else if (msgSender.Status == NetConnectionStatus.Disconnected)
                                    {
                                         
                                        if (player.Kicked)
                                        {
                                            if (playerList.ContainsKey(msgSender))
                                            {
                                                player.Disposing = true;
                                                player.DisposeTime = DateTime.Now;
                                            }    
                                        }
                                        else if (!player.Quit)
                                        {
                                            ConsoleWrite("DISCONNECTED: " + playerList[msgSender].Handle);

                                            SendServerMessage(player.Handle + " lost connection!");
                                            if (playerList.ContainsKey(msgSender))
                                            {
                                                int dcount = 0;
                                                foreach (Player p in playerList.Values)
                                                {
                                                    if (p.Disposing)
                                                        dcount++;
                                                }

                                                if (dcount < 6)
                                                {
                                                    player.Disposing = true;
                                                    player.DisposeTime = DateTime.Now + TimeSpan.FromSeconds(120);
                                                }
                                                else//prevents players filling up server with disconnected clients
                                                {
                                                    player.Disposing = true;
                                                    player.DisposeTime = DateTime.Now;
                                                }
                                            }
                                        }
                                        else if (player.Quit && player.StatusEffect[6] > 0 || player.Quit && player.deathCount > 100)
                                        {
                                            player.Quit = false;//unquit this player
                                            ConsoleWrite("TRIEDTOQUIT: " + playerList[msgSender].Handle);

                                            SendServerMessage(player.Handle + " lost connection!");

                                            if (playerList.ContainsKey(msgSender))
                                            {
                                                int dcount = 0;
                                                foreach (Player p in playerList.Values)
                                                {
                                                    if (p.Disposing)
                                                        dcount++;
                                                }

                                                if (dcount < 6)
                                                {
                                                    player.Disposing = true;
                                                    player.DisposeTime = DateTime.Now + TimeSpan.FromSeconds(120);
                                                }
                                                else//prevents players filling up server with disconnected clients
                                                {
                                                    player.Disposing = true;
                                                    player.DisposeTime = DateTime.Now;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            ConsoleWrite("QUIT: " + playerList[msgSender].Handle);
                                            if (playerList.ContainsKey(msgSender))
                                            {
                                                player.Disposing = true;
                                                player.DisposeTime = DateTime.Now;
                                            }
                                        }

                                        sleeping = true;
                                        foreach (Player p in playerList.Values)
                                        {
                                            sleeping = false;
                                        }

                                        if (sleeping == true)
                                        {
                                            //ConsoleWrite("HIBERNATING");
                                            physicsEnabled = false;
                                        }
                                    }
                                }
                                break;

                            case NetMessageType.Data:
                                {
                                    if (!this.playerList.ContainsKey(msgSender))
                                    {
                                        break;
                                    }

                                    Player player = playerList[msgSender];
                                    InfiniminerMessage dataType = (InfiniminerMessage)msgBuffer.ReadByte();
                                    switch (dataType)
                                    {
                                        case InfiniminerMessage.Challenge:
                                            {
                                                if (varGetI("siege") == 2)
                                                {
                                                    if (siege_uploader == null && player.Team != PlayerTeam.None && !player.Kicked && !player.Disposing)
                                                    {
                                                        siege_uploader = player;
                                                        ConsoleWrite(Defines.Sanitize(player.Handle + " challenged the host fortress!"));
                                                        SendServerMessage(player.Handle + " challenged the host fortress!");

                                                        NetBuffer netBuffer = netServer.CreateBuffer();
                                                        netBuffer.Write((byte)InfiniminerMessage.Challenge);
                                                        netServer.SendMessage(netBuffer, msgSender, NetChannel.ReliableInOrder3);
                                                        siege_uploadtime = DateTime.Now;
                                                    }
                                                }
                                            }
                                            break;
                                        case InfiniminerMessage.BlockBulkTransfer:
                                            {
                                                if (varGetI("siege") == 2)
                                                {
                                                    if (siege_uploader == player)
                                                    {
                                                        siege_uploadtime = DateTime.Now;
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

                                                            for (byte dy = 0; dy < 16; dy++)
                                                                for (byte z = 0; z < MAPSIZE; z++) //for (byte z = 0; z < MAPSIZE; z++)
                                                                {
                                                                    siege_blockcount++;
                                                                    if (x < MAPSIZE && dy + y < MAPSIZE && z < MAPSIZE)
                                                                    {
                                                                        BlockType blockType = (BlockType)decompresser.ReadByte();
                                                                        siege_blockcost += (int)BlockInformation.GetCost(blockType);

                                                                        if (blockType != BlockType.None)
                                                                        {
                                                                            blockListFort[x, y + dy, z] = blockType;
                                                                        }
                                                                    }
                                                                }
                                                        }
                                  
                                                        if (siege_blockcount == 262144)
                                                        {
                                                            SendServerMessage("The enemy fortress has been uploaded!");
                                                            SavingFort("attackingfort.lvl");
                                                            varSet("siege", 3, true);
                                                            siege_uploader = null;
                                                            siege_blockcount = 0;
                                                        }
                                                        else if (siege_blockcount > 262144)
                                                        {
                                                            SendServerMessage("The attacker had an invalid fort with " + siege_blockcount + " blocks! Awaiting new challenger.");
                                                            varSet("siege", 2, true);
                                                            siege_uploader = null;
                                                            siege_blockcount = 0;
                                                        }
                                                    }
                                                    else if (siege_uploader == null)
                                                    {
                                                        //siege_uploader = player;
                                                        //ConsoleWrite(Defines.Sanitize(player.Handle + " challenged the host fortress!"));

                                                        //siege_blockcount = 0;
                                                        //byte isCompressed = msgBuffer.ReadByte();
                                                        //byte x;
                                                        //byte y;

                                                        ////255 was used because it exceeds the map size - of course, bytes won't work anyway if map sizes are allowed to be this big, so this method is a non-issue
                                                        //if (isCompressed == 255)
                                                        //{
                                                        //    var compressed = msgBuffer.ReadBytes(msgBuffer.LengthBytes - msgBuffer.Position / 8);
                                                        //    var compressedstream = new System.IO.MemoryStream(compressed);
                                                        //    var decompresser = new System.IO.Compression.GZipStream(compressedstream, System.IO.Compression.CompressionMode.Decompress);

                                                        //    x = (byte)decompresser.ReadByte();
                                                        //    y = (byte)decompresser.ReadByte();

                                                        //    for (byte dy = 0; dy < 16; dy++)
                                                        //        for (byte z = 0; z < MAPSIZE; z++)
                                                        //        {
                                                        //            //if (x > 0 && x < MAPSIZE && dy + y > 0 && dy + y < MAPSIZE && z > 0 && z < MAPSIZE)
                                                        //            //{
                                                        //                siege_blockcount++;
                                                        //                BlockType blockType = (BlockType)decompresser.ReadByte();
                                                        //                if (blockType != BlockType.None)
                                                        //                {
                                                        //                    blockListFort[x, y + dy, z] = blockType;
                                                        //                }
                                                        //            //}
                                                        //        }
                                                        //}
                                                    }
                                                }
                                            }
                                            break;
                                        case InfiniminerMessage.ChatMessage:
                                            {
                                                // Read the data from the packet.
                                                ChatMessageType chatType = (ChatMessageType)msgBuffer.ReadByte();
                                                string chatString = Defines.Sanitize(msgBuffer.ReadString());
                                                if (!ProcessCommand(chatString,GetAdmin(playerList[msgSender].IP),playerList[msgSender]))
                                                {
                                                    player.Annoying += 2;

                                                    if (chatType == ChatMessageType.SayAll)
                                                    ConsoleWrite("CHAT: (" + player.Handle + ") " + chatString);

                                                    // Append identifier information.
                                                    if (chatType == ChatMessageType.SayAll)
                                                        chatString = player.Handle + " (ALL): " + chatString;
                                                    else
                                                        chatString = player.Handle + " (TEAM): " + chatString;

                                                    // Construct the message packet.
                                                    NetBuffer chatPacket = netServer.CreateBuffer();
                                                    chatPacket.Write((byte)InfiniminerMessage.ChatMessage);
                                                    chatPacket.Write((byte)((player.Team == PlayerTeam.Red) ? ChatMessageType.SayRedTeam : ChatMessageType.SayBlueTeam));
                                                    chatPacket.Write(chatString);

                                                    // Send the packet to people who should recieve it.
                                                    foreach (Player p in playerList.Values)
                                                    {
                                                        if (chatType == ChatMessageType.SayAll ||
                                                            chatType == ChatMessageType.SayBlueTeam && p.Team == PlayerTeam.Blue ||
                                                            chatType == ChatMessageType.SayRedTeam && p.Team == PlayerTeam.Red)
                                                            if (p.NetConn.Status == NetConnectionStatus.Connected)
                                                                netServer.SendMessage(chatPacket, p.NetConn, NetChannel.ReliableInOrder3);
                                                    }
                                                }
                                            }
                                            break;

                                        case InfiniminerMessage.UseTool:
                                            {
                                                //no living check due to sync
                                                Vector3 playerPosition = msgBuffer.ReadVector3();
                                                Vector3 playerHeading = msgBuffer.ReadVector3();
                                                PlayerTools playerTool = (PlayerTools)msgBuffer.ReadByte();
                                                BlockType blockType = (BlockType)msgBuffer.ReadByte();

                                                //getTo
                                                switch (playerTool)
                                                {
                                                    case PlayerTools.Pickaxe:
                                                        UsePickaxe(player, playerPosition, playerHeading);
                                                        break;
                                                    case PlayerTools.StrongArm:
                                                        if (player.Class == PlayerClass.Miner && player.Alive)
                                                        UseStrongArm(player, playerPosition, playerHeading);
                                                        break;
                                                    case PlayerTools.Smash:
                                                        //if(player.Class == PlayerClass.Sapper)
                                                        //UseSmash(player, playerPosition, playerHeading);
                                                        break;
                                                    case PlayerTools.ConstructionGun:
                                                        UseConstructionGun(player, playerPosition, playerHeading, blockType);
                                                        break;
                                                    case PlayerTools.ConstructItem:
                                                        ConstructItem(player, playerPosition, playerHeading, blockType, 6);
                                                        break;
                                                    case PlayerTools.DeconstructionGun:
                                                        UseDeconstructionGun(player, playerPosition, playerHeading);
                                                        break;
                                                    case PlayerTools.ProspectingRadar:
                                                        if (player.Class == PlayerClass.Prospector)
                                                        {
                                                            int val = (int)blockType;
                                                            if (val > -1 && val < 4)
                                                            {
                                                                if (msgBuffer.Position < msgBuffer.LengthBits)
                                                                {
                                                                    UseSignPainter(player, playerPosition, playerHeading, val, msgBuffer.ReadInt32(), msgBuffer.ReadInt32(), msgBuffer.ReadInt32(), msgBuffer.ReadInt32());
                                                                }
                                                                else
                                                                {//for clients without new prospector
                                                                    UseSignPainter(player, playerPosition, playerHeading, val, (int)((playerPosition.X + playerHeading.X) * 100), (int)((playerPosition.Y + playerHeading.Y) * 100), (int)((playerPosition.Z + playerHeading.Z) * 100), (int)(0.5f*100));
                                                                }
                                                            }
                                                            else
                                                            {
                                                                UseSignPainter(player, playerPosition, playerHeading, 0, (int)((playerPosition.X + playerHeading.X) * 100), (int)((playerPosition.Y + playerHeading.Y) * 100), (int)((playerPosition.Z + playerHeading.Z) * 100), (int)(0.5f * 100));
                                                            }
                                                        }
                                                        break;
                                                    case PlayerTools.Detonator:
                                                        if (player.Class == PlayerClass.Sapper && player.Alive)
                                                        UseDetonator(player);
                                                        break;
                                                    case PlayerTools.Remote:
                                                        if (player.Class == PlayerClass.Engineer && player.Alive)
                                                        UseRemote(player);
                                                        break;
                                                    case PlayerTools.SetRemote:
                                                        if (player.Class == PlayerClass.Engineer && player.Alive)
                                                        SetRemote(player);
                                                        break;
                                                    case PlayerTools.ThrowBomb:
                                                        if (player.Class == PlayerClass.Sapper && player.Alive)
                                                        ThrowBomb(player, playerPosition, playerHeading);
                                                        break;
                                                    case PlayerTools.ThrowRope:
                                                        if (player.Class == PlayerClass.Prospector && player.Alive)
                                                            ThrowRope(player, playerPosition, playerHeading);
                                                        break;
                                                    case PlayerTools.Hide:
                                                        if (player.Class == PlayerClass.Prospector && player.Alive)
                                                            Hide(player);
                                                        break;
                                                }
                                            }
                                            break;

                                        case InfiniminerMessage.SelectClass:
                                            {
                                                PlayerClass playerClass = (PlayerClass)msgBuffer.ReadByte();
                                                if (player.LastHit < DateTime.Now - TimeSpan.FromSeconds(15))
                                                {
                                                    if (player.Alive && player.Class != PlayerClass.None)
                                                    {
                                                        if(player.Team != PlayerTeam.None)
                                                        if(playerClass == PlayerClass.Engineer)
                                                            Player_Dead(player, DeathMessage.deathByEngineer);
                                                        else if(playerClass == PlayerClass.Miner)
                                                            Player_Dead(player, DeathMessage.deathByMiner);
                                                        else if(playerClass == PlayerClass.Prospector)
                                                            Player_Dead(player, DeathMessage.deathByProspector);
                                                        else if(playerClass == PlayerClass.Sapper)
                                                            Player_Dead(player, DeathMessage.deathBySapper);
                                                    }

                                                    SendPlayerDead(player);
                                                    //ConsoleWrite("SELECT_CLASS: " + player.Handle + ", " + playerClass.ToString());
                                                    player.Annoying += 3;
                                                    switch (playerClass)
                                                    {
                                                        case PlayerClass.Engineer:
                                                            player.Class = playerClass;
                                                            player.OreMax = 200 + (uint)(ResearchComplete[(byte)player.Team, 2] * 10);
                                                            player.WeightMax = 4 + (uint)(ResearchComplete[(byte)player.Team, 2]);
                                                            player.HealthMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 1] * 5);
                                                            player.Health = player.HealthMax;
                                                            for (int a = 0; a < 100; a++)
                                                            {
                                                                player.Content[a] = 0;
                                                            }
                                                            break;
                                                        case PlayerClass.Miner://strong arm/throws blocks
                                                            player.Class = playerClass;
                                                            player.OreMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 2] * 10);
                                                            player.WeightMax = 10 + (uint)(ResearchComplete[(byte)player.Team, 2]);
                                                            player.HealthMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 1] * 5);
                                                            player.Health = player.HealthMax;
                                                            for (int a = 0; a < 100; a++)
                                                            {
                                                                player.Content[a] = 0;
                                                            }
                                                            break;
                                                        case PlayerClass.Prospector://profiteer/has prospectron/stealth/climb/traps
                                                            player.Class = playerClass;
                                                            player.OreMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 2] * 10);
                                                            player.WeightMax = 6 + (uint)(ResearchComplete[(byte)player.Team, 2]);
                                                            player.HealthMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 1] * 5);
                                                            player.Health = player.HealthMax;
                                                            for (int a = 0; a < 100; a++)
                                                            {
                                                                player.Content[a] = 0;
                                                            }
                                                            break;
                                                        case PlayerClass.Sapper://berserker/charge that knocks people and blocks away/repairs block
                                                            player.Class = playerClass;
                                                            player.OreMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 2] * 10);
                                                            player.WeightMax = 4 + (uint)(ResearchComplete[(byte)player.Team, 2]);
                                                            player.HealthMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 1] * 5);
                                                            player.Health = player.HealthMax;
                                                            for (int a = 0; a < 100; a++)
                                                            {
                                                                player.Content[a] = 0;
                                                            }
                                                            break;
                                                    }
                                                    for (int a = 0; a < 20; a++)
                                                    {
                                                        player.StatusEffect[a] = 0;
                                                    }
                                                    player.Exp = 0;
                                                    SendPlayerSetClass(player);
                                                    SendResourceUpdate(player);
                                                    SendContentUpdate(player);
                                                }
                                                else
                                                {
                                                    SendServerMessageToPlayer("You are still in combat!", player.NetConn);
                                                }
                                            }
                                            break;

                                        case InfiniminerMessage.PlayerSetTeam:
                                            {
                                                PlayerTeam playerTeam = (PlayerTeam)msgBuffer.ReadByte();

                                                if (player.LastHit < DateTime.Now - TimeSpan.FromSeconds(15))
                                                {
                                                    if (varGetI("siege") > 0 && varGetI("siege") != 4)
                                                    {
                                                        playerTeam = PlayerTeam.Red;
                                                    }
                                                    else if(varGetB("enforceteams"))
                                                    {
                                                        int redteam = 0;
                                                        int blueteam = 0;
                                                        foreach (Player p in playerList.Values)
                                                        {
                                                            if (p.Team == PlayerTeam.Red)
                                                            {
                                                                redteam++;
                                                            }
                                                            else if (p.Team == PlayerTeam.Blue)
                                                            {
                                                                blueteam++;
                                                            }
                                                        }

                                                        if (playerTeam == PlayerTeam.Red)
                                                        {
                                                            if (redteam < blueteam || redteam == 0)
                                                            {
                                                            }
                                                            else
                                                            {
                                                                SendServerMessageToPlayer("You may not uneven the teams.", player.NetConn);
                                                                break;
                                                            }
                                                        }
                                                        else if (playerTeam == PlayerTeam.Blue)
                                                        {
                                                            if (blueteam < redteam || blueteam == 0)
                                                            {
                                                            }
                                                            else
                                                            {
                                                                SendServerMessageToPlayer("You may not uneven the teams.", player.NetConn);
                                                                break;
                                                            }
                                                        }
                                                    }
                                                    player.Annoying += 3;
                                                    ConsoleWrite("SELECT_TEAM: " + player.Handle + ", " + playerTeam.ToString());
                                                    player.Team = playerTeam;
                                                    Player_Dead(player, "has joined the " + player.Team + " team!");

                                                    if(player.Alive)
                                                        SendPlayerDead(player);

                                                    switch (player.Class)
                                                    {
                                                        case PlayerClass.Engineer:
                                                            player.OreMax = 200 + (uint)(ResearchComplete[(byte)player.Team, 2] * 10);
                                                            player.WeightMax = 4 + (uint)(ResearchComplete[(byte)player.Team, 2]);
                                                            player.HealthMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 1] * 5);
                                                            player.Health = player.HealthMax;
                                                            for (int a = 0; a < 100; a++)
                                                            {
                                                                player.Content[a] = 0;
                                                            }
                                                            break;
                                                        case PlayerClass.Miner://strong arm/throws blocks
                                                            player.OreMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 2] * 10);
                                                            player.WeightMax = 10 + (uint)(ResearchComplete[(byte)player.Team, 2]);
                                                            player.HealthMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 1] * 5);
                                                            player.Health = player.HealthMax;
                                                            for (int a = 0; a < 100; a++)
                                                            {
                                                                player.Content[a] = 0;
                                                            }
                                                            break;
                                                        case PlayerClass.Prospector://profiteer/has prospectron/stealth/traps
                                                            player.OreMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 2] * 10);
                                                            player.WeightMax = 6 + (uint)(ResearchComplete[(byte)player.Team, 2]);
                                                            player.HealthMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 1] * 5);
                                                            player.Health = player.HealthMax;
                                                            for (int a = 0; a < 100; a++)
                                                            {
                                                                player.Content[a] = 0;
                                                            }
                                                            break;
                                                        case PlayerClass.Sapper://throws explosives
                                                            player.OreMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 2] * 10);
                                                            player.WeightMax = 4 + (uint)(ResearchComplete[(byte)player.Team, 2]);
                                                            player.HealthMax = 100 + (uint)(ResearchComplete[(byte)player.Team, 1] * 5);
                                                            player.Health = player.HealthMax;
                                                            for (int a = 0; a < 100; a++)
                                                            {
                                                                player.Content[a] = 0;
                                                            }
                                                            break;
                                                    }
                                                    SendResourceUpdate(player);
                                                    SendPlayerSetTeam(player);
                                                }
                                                else
                                                {
                                                    SendServerMessageToPlayer("You are still in combat!", player.NetConn);
                                                }
                                            }
                                            break;

                                        case InfiniminerMessage.PlayerDead:
                                            {
                                                DeathMessage deathMessage = (DeathMessage)msgBuffer.ReadByte();

                                                if (player.Alive)
                                                {
                                                    Player_Dead(player, deathMessage);
                                                }
                                            }
                                            break;

                                        case InfiniminerMessage.PlayerAlive:
                                            {
                                                if (toGreet.Contains(msgSender))
                                                {
                                                    if (player.Resume == false)
                                                    {
                                                        string greeting = varGetS("greeter");
                                                        greeting = greeting.Replace("[name]", playerList[msgSender].Handle);
                                                        if (greeting != "")
                                                        {
                                                            NetBuffer greetBuffer = netServer.CreateBuffer();
                                                            greetBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                                                            greetBuffer.Write((byte)ChatMessageType.SayAll);
                                                            greetBuffer.Write(Defines.Sanitize(greeting));
                                                            netServer.SendMessage(greetBuffer, msgSender, NetChannel.ReliableInOrder3);
                                                        }
                                                    }
                                                    toGreet.Remove(msgSender);
                                                }
                                                ConsoleWrite("PLAYER ALIVE: " + player.Handle);
                                                player.Ore = 0;
                                                player.Cash = 0;
                                                player.Weight = 0;
                                                player.Health = player.HealthMax;
                                                player.Alive = true;
                                                //ConsoleWrite("respawn time: " + ((player.deathCount / 20) * 3));
                                                //player.respawnTimer = DateTime.Now + TimeSpan.FromSeconds((player.deathCount/20) * 3);
                                                SendResourceUpdate(player);
                                                SendPlayerAlive(player);
                                            }
                                            break;
                                        case InfiniminerMessage.PlayerRespawn:
                                            {
                                                SendPlayerRespawn(player);//new respawn
                                            }
                                            break;
                                        case InfiniminerMessage.Disconnect:
                                            {
                                                player.Quit = true;
                                            }
                                            break;
                                        case InfiniminerMessage.PlayerUpdate:
                                            {
                                                player.LastUpdate = DateTime.Now;
                                                if (player.Alive)
                                                {
                                                    player.Position = Auth_Position(msgBuffer.ReadVector3(), player, true);
                                                    player.Heading = Auth_Heading(msgBuffer.ReadVector3());
                                                    player.Tool = (PlayerTools)msgBuffer.ReadByte();
                                                    player.UsingTool = msgBuffer.ReadBoolean();
                                                    SendPlayerUpdate(player);
                                                }
                                            }
                                            break;
                                        case InfiniminerMessage.PlayerSlap:
                                            {
                                                if (player.Alive)
                                                {
                                                    if (player.playerToolCooldown > DateTime.Now)
                                                    {
                                                        break;//discard fast packet
                                                    }

                                                    player.Position = Auth_Position(msgBuffer.ReadVector3(), player, true);
                                                    player.Heading = Auth_Heading(msgBuffer.ReadVector3());
                                                    player.Tool = (PlayerTools)msgBuffer.ReadByte();
                                                    player.UsingTool = true;
                                                    if (varGetI("siege") == 4)
                                                    {
                                                        if(player.Team == PlayerTeam.Red)
                                                        {
                                                            if (player.Position.Z > MAPSIZE/2)
                                                            {
                                                                Auth_Slap(player, msgBuffer.ReadUInt32());
                                                            }
                                                        }
                                                        else if (player.Team == PlayerTeam.Blue)
                                                        {
                                                            if (player.Position.Z < MAPSIZE/2)
                                                            {
                                                                Auth_Slap(player, msgBuffer.ReadUInt32());
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Auth_Slap(player, msgBuffer.ReadUInt32());
                                                    }
                                                    SendPlayerUpdate(player);

                                                    player.playerToolCooldown = DateTime.Now + TimeSpan.FromSeconds((float)(player.GetToolCooldown(PlayerTools.Pickaxe)));

                                                    if (player.Class == PlayerClass.Prospector && player.Content[5] > 0)//reveal when hit
                                                    {
                                                        player.Content[6] = 0;//uncharge
                                                        //player.Content[1] = 0;//reappear on radar//this should be autoupdating via p.radar
                                                        SendPlayerContentUpdate(player, 1);
                                                        player.Content[5] = 0;//sight
                                                        SendContentSpecificUpdate(player, 5);
                                                        SendContentSpecificUpdate(player, 6);
                                                        SendPlayerContentUpdate(player, 5);
                                                        SendServerMessageToPlayer("You have been revealed!", player.NetConn);
                                                        EffectAtPoint(player.Position - Vector3.UnitY * 1.5f, 1);
                                                    }
                                                }
                                            }
                                            break;
                                        case InfiniminerMessage.PlayerUpdate1://minus position
                                            {
                                                player.LastUpdate = DateTime.Now;
                                                if (player.Alive)
                                                {
                                                    player.Heading = Auth_Heading(msgBuffer.ReadVector3());
                                                    player.Tool = (PlayerTools)msgBuffer.ReadByte();
                                                    player.UsingTool = msgBuffer.ReadBoolean();
                                                    SendPlayerUpdate(player);
                                                }
                                            }
                                            break;
                                        case InfiniminerMessage.PlayerUpdate2://minus position and heading
                                            {
                                                player.LastUpdate = DateTime.Now;
                                                if (player.Alive)
                                                {
                                                    player.Tool = (PlayerTools)msgBuffer.ReadByte();
                                                    player.UsingTool = msgBuffer.ReadBoolean();
                                                    SendPlayerUpdate(player);
                                                }
                                            }
                                            break;
                                        case InfiniminerMessage.PlayerHurt://client speaks of fall damage
                                            {
                                                if (player.Alive)
                                                {
                                                    uint newhp = msgBuffer.ReadUInt32();
                                                    if (newhp < player.Health)
                                                    {
                                                        if (player.Team == PlayerTeam.Red)
                                                        {
                                                            DebrisEffectAtPoint((int)(player.Position.X), (int)(player.Position.Y), (int)(player.Position.Z), BlockType.SolidRed, 10 + (int)(player.Health - newhp));
                                                        }
                                                        else
                                                        {
                                                            DebrisEffectAtPoint((int)(player.Position.X), (int)(player.Position.Y), (int)(player.Position.Z), BlockType.SolidBlue, 10 + (int)(player.Health - newhp));
                                                        }

                                                        player.FallBuffer += ((int)(player.Health) - (int)(newhp));

                                                        if (player.FallBuffer > 30)
                                                            player.FallBuffer = 30;

                                                        //ConsoleWrite("buffer:" + player.FallBuffer);
                                                        player.Health = newhp;

                                                        if (player.Health < 1)
                                                        {
                                                            Player_Dead(player, "fell to their death!");
                                                        }
                                                    }
                                                }
                                            }
                                            break;
                                        case InfiniminerMessage.PlayerPosition://server not interested in clients complaints about position
                                            {
                                              
                                            }
                                            break;
                                        case InfiniminerMessage.PlayerInteract://client speaks of mashing on block
                                            {
                                                if (player.Alive)
                                                {
                                                    player.Position = Auth_Position(msgBuffer.ReadVector3(), player, true);

                                                    uint btn = msgBuffer.ReadUInt32();
                                                    uint btnx = msgBuffer.ReadUInt32();
                                                    uint btny = msgBuffer.ReadUInt32();
                                                    uint btnz = msgBuffer.ReadUInt32();

                                                    //if (blockList[btnx, btny, btnz] == BlockType.Pump || blockList[btnx, btny, btnz] == BlockType.Pipe || blockList[btnx, btny, btnz] == BlockType.Generator || blockList[btnx, btny, btnz] == BlockType.Barrel || blockList[btnx, btny, btnz] == BlockType.Switch)
                                                    //{
                                                    if (Get3DDistance((int)btnx, (int)btny, (int)btnz, (int)player.Position.X, (int)player.Position.Y, (int)player.Position.Z) < 4)
                                                    {
                                                        PlayerInteract(player, btn, btnx, btny, btnz);
                                                    }
                                                    //}
                                                }
                                            }
                                            break;
                                        case InfiniminerMessage.DepositOre:
                                            {
                                                if (player.Alive)
                                                {
                                                    DepositOre(player);
                                                    foreach (Player p in playerList.Values)
                                                        SendResourceUpdate(p);
                                                }
                                            }
                                            break;

                                        case InfiniminerMessage.WithdrawOre:
                                            {
                                                if (player.Alive)
                                                {
                                                    WithdrawOre(player);
                                                    foreach (Player p in playerList.Values)
                                                        SendResourceUpdate(p);
                                                }
                                            }
                                            break;

                                        case InfiniminerMessage.PlayerPing:
                                            {
                                                if (player.Alive)
                                                    if (player.Ping == 0)
                                                    {
                                                        SendPlayerPing((uint)msgBuffer.ReadInt32());
                                                        player.Ping = 2;
                                                    }
                                            }
                                            break;

                                        case InfiniminerMessage.PlaySound:
                                            {
                                                if (player.Alive)
                                                {
                                                    InfiniminerSound sound = (InfiniminerSound)msgBuffer.ReadByte();
                                                    Vector3 position = msgBuffer.ReadVector3();
                                                    PlaySoundForEveryoneElse(sound, position, player);
                                                }
                                            }
                                            break;

                                        case InfiniminerMessage.DropItem:
                                            {
                                                DropItem(player, msgBuffer.ReadUInt32());
                                            }
                                            break;

                                        case InfiniminerMessage.GetItem:
                                            {
                                                if (player.Alive)
                                                {
                                                    //verify players position before get
                                                    player.Position = Auth_Position(msgBuffer.ReadVector3(), player, false);

                                                    GetItem(player, msgBuffer.ReadUInt32());
                                                }
                                            }
                                            break;
                                    }
                                }
                                break;
                        }
                    }
                    catch { }
                }

                //Time to backup map?
                if (!sleeping)
                {
                    TimeSpan mapUpdateTimeSpan = DateTime.Now - lastMapBackup;

                    if (varGetI("autosave") > 0)
                        if (mapUpdateTimeSpan.TotalMinutes > varGetI("autosave"))
                        {
                            lastMapBackup = DateTime.Now;
                            SaveLevel("autoBK.lvl");
                        }
                }
                else
                {
                    lastMapBackup = DateTime.Now;//avoid backing up while hibernating
                }
                // Time to send a new server update?
                PublicServerListUpdate(); //It checks for public server / time span

                //Time to terminate finished map sending threads?
                TerminateFinishedThreads();

                // Check for players who are in the zone to deposit.
                VictoryCheck();

                foreach (Player p in playerList.Values)//remove players
                {
                    if (p.Disposing && !physactive && p.DisposeTime < DateTime.Now)
                    {
                        SendPlayerLeft(p, p.Kicked ? "was kicked from the game!" : "has abandoned their duties!");

                        if (p.Alive)
                            Player_Dead(p, "");

                        playerList.Remove(p.NetConn);
                        PublicServerListUpdate();
                        break;
                    }
                }
                // Is it time to do a lava calculation? If so, do it!
                TimeSpan timeSpan = DateTime.Now - sysTimer;
                if(!sleeping)
                if (timeSpan.TotalMilliseconds > 2000 && Filename == "")
                {
                    //ConsoleWrite("" + delta);
                    sysTimer = DateTime.Now;

                    if(varGetI("siege") == 3)
                    {
                        if (LoadFortLevel("attackingfort.lvl", PlayerTeam.Blue) == false)
                        {
                            SendServerMessage("Attacking fort was invalid, awaiting new challengers.");
                            varSet("siege", 2, true);
                            siege_uploader = null;
                            siege_blockcount = 0;
                        }
                        else
                        {
                            varSet("siege", 4, true);
                            SendServerMessage("The siege will begin shortly, you will be reconnected.");
                            disconnectAll();
                            netServer.Shutdown("");
                            InitSiege();
                            netServer.Start();
                        }
                    }
                    else if (varGetI("siege") == 4)
                    {
                        if (siege_start < DateTime.Now - TimeSpan.FromSeconds(60))
                        {
                            for (ushort i = 0; i < MAPSIZE; i++)
                                for (ushort j = 3; j < MAPSIZE; j++)
                                    for (ushort k = 29; k < 36; k++)
                                    {
                                        blockList[i, j, k] = BlockType.None;
                                    }

                            varSet("siege", 5, true);//
                            SendServerMessage("The siege has begun!");
                        }
                        else
                        {
                            if((siege_start - (DateTime.Now - TimeSpan.FromSeconds(60))).Seconds < 12)
                            {
                                SendServerMessage("Siege begins in " + (siege_start - (DateTime.Now - TimeSpan.FromSeconds(60))).Seconds + "..");
                            }
                            else
                            {
                                if(randGen.Next(1, 8) == 1)
                                {
                                    SendServerMessage("Siege begins in " + (siege_start - (DateTime.Now - TimeSpan.FromSeconds(60))).Seconds + "..");
                                }
                            }
                           // if(randGen.Next(1,6) == 2)
                           // SendServerMessage("Siege begins in " + siege_start - (DateTime.Now - TimeSpan.FromSeconds(60)));
                        }
                    }
                    else if (varGetI("siege") == 2)
                    {
                        if (siege_uploader != null)
                        {
                            if (playerList.ContainsValue(siege_uploader))
                            {
                                if(randGen.Next(1, 10) == 2)
                                SendServerMessage( (262144 - siege_blockcount) + " remain to upload.");

                                if (siege_uploadtime < DateTime.Now - TimeSpan.FromSeconds(30))
                                {
                                    SendServerMessage("Attacking fort timed out, awaiting new challengers.");
                                    varSet("siege", 2, true);
                                    siege_uploader = null;
                                    siege_blockcount = 0;
                                }
                            }
                            else
                            {
                                SendServerMessage("Attacking fort has been disconnected, awaiting new challengers.");
                                varSet("siege", 2, true);
                                siege_uploader = null;
                                siege_blockcount = 0;
                            }
                        }
                    }
                    else if (varGetI("siege") == 5 && includeLava)
                    {
                        int lcount = randGen.Next(3, 10);
                        //ConsoleWrite("sput: " + lcount);

                        while (lcount > 0)
                        {
                            int rx = randGen.Next(1, MAPSIZE);
                            int rz = randGen.Next((MAPSIZE / 2) - 4, (MAPSIZE / 2) + 4);

                            if (blockList[rx, 2, rz] == BlockType.Lava)
                            {
                                Catapult((uint)rx, 2, (uint)rz, new Vector3((float)(randGen.NextDouble()-0.5f), (float)(randGen.NextDouble() * 2f + 0.5f), (float)(randGen.NextDouble()-0.5f)) * 3.0f);
                                lcount--;
                            }
                        }
                    }

                    if (VoteType > 0)
                    {
                        if (VoteStart < DateTime.Now - TimeSpan.FromSeconds(30))
                        {
                            int forvote = 0;
                            int against = 0;
                            foreach (Player p in playerList.Values)
                            {
                                if (!p.Disposing && p.connectedTimer < DateTime.Now - TimeSpan.FromSeconds(120))
                                {
                                    if (p.Vote == true)
                                        forvote++;
                                    else
                                        against++;

                                    p.Vote = false;
                                }
                            }

                            int pcount = (int)((forvote + against) * 0.33);
                            
                            if (forvote > against + pcount || against == 0)
                            {
                                if (VoteType == 1)
                                {
                                    SendServerMessage("The vote to restart has passed! Restarting in 15 seconds. You may save the map locally using Ctrl-S.");
                                    BroadcastGameOver();
                                    restartTriggered = true;
                                    restartTime = DateTime.Now.AddSeconds(15);
                                }
                                else if (VoteType == 2)
                                {
                                    SendServerMessage("The vote to save has passed! The current map will become permanent.");
                                    lastMapBackup = DateTime.Now;
                                    SavingFort("fort.lvl");
                                }
                                else if (VoteType == 3)
                                {
                                    SendServerMessage("The vote to load has passed!");
                                    lastMapBackup = DateTime.Now;

                                    if (varGetI("siege") == 5)
                                        varSet("siege", 1, true);

                                    LoadFortLevel("fort.lvl", PlayerTeam.Red);
                                    disconnectAll();
                                    //SavingFort("fort.lvl");
                                }
                                else if (VoteType == 4)
                                {
                                    if (varGetI("siege") == 1 && siege_uploader == null)
                                    {
                                        SendServerMessage("The vote to allow challengers has passed! Players can use ctrl-c to challenge us with their fortress.");
                                        lastMapBackup = DateTime.Now;
                                        varSet("siege", 2, true);
                                    }
                                    //disconnectAll();
                                }
                            }
                            else
                            {
                                if (VoteType == 1)
                                {
                                    SendServerMessage("The vote to restart failed! for: " + forvote + " against: " + against);
                                }
                                else if (VoteType == 2)
                                {
                                    SendServerMessage("The vote to save failed! for: " + forvote + " against: " + against);
                                }
                                else if (VoteType == 3)
                                {
                                    SendServerMessage("The vote to load failed! for: " + forvote + " against: " + against);
                                }
                                else if (VoteType == 4)
                                {
                                    SendServerMessage("The vote to siege failed! for: " + forvote + " against: " + against);
                                }
                            }
                            VoteType = 0;
                            VoteCreator = 0;
                        }
                    }

                    if (varGetB("sandbox") && !SiegeBuild)
                    {
                        if (teamOreRed < 9999)
                        {
                            teamOreRed = 9999;
                            foreach (Player p in playerList.Values)
                            {
                                if(p.Team == PlayerTeam.Red)
                                SendTeamOreUpdate(p);
                            }
                        }
                        if (teamOreBlue < 9999)
                        {
                            teamOreBlue = 9999;
                            foreach (Player p in playerList.Values)
                            {
                                if (p.Team == PlayerTeam.Blue)
                                    SendTeamOreUpdate(p);
                            }
                        }
                    }

                    //secondflow += 1;

                    //if (secondflow > 2)//every 2nd flow, remove the vacuum that prevent re-spread
                    //{
                    //    EraseVacuum();
                    //    secondflow = 0;
                    //}
                    if (randGen.Next(1, 4) == 3)
                    {
                        bool isUpdateOre = false;
                        bool isUpdateCash = false;
                        for (int a = 1; a < 3; a++)
                        {
                            if (artifactActive[a, 1] > 0)//material artifact
                            {
                                isUpdateOre = true;
                                if (a == 1)
                                {
                                    if (teamOreRed < 9999 - (40 * artifactActive[a, 1]))
                                    {
                                        teamOreRed = teamOreRed + (40 * artifactActive[a, 1]);
                                    }
                                    else
                                    {
                                        teamOreRed = 9999;
                                    }
                                }
                                else if (a == 2)
                                {
                                    if (teamOreBlue < 9999 - (40 * artifactActive[a, 1]))
                                    {
                                        teamOreBlue = teamOreBlue + (40 * artifactActive[a, 1]);
                                    }
                                    else
                                    {
                                        teamOreBlue = 9999;
                                    }
                                }

                            }
                            if (artifactActive[a, 5] > 0)//golden artifact
                            {
                                isUpdateCash = true;
                                if (a == 1)
                                {
                                    teamCashRed = teamCashRed + (uint)(2 * artifactActive[a, 5]);
                                }
                                else if (a == 2)
                                {
                                    teamCashBlue = teamCashBlue + (uint)(2 * artifactActive[a, 5]);
                                }

                            }
                        }

                        if (isUpdateOre)
                            foreach (Player p in playerList.Values)
                                SendTeamOreUpdate(p);

                        if(isUpdateCash)
                        foreach (Player p in playerList.Values)
                            SendTeamCashUpdate(p);
                    }

                    foreach (Player p in playerList.Values)//regeneration
                    {
                        if (p.Ping > 0)
                            p.Ping--;

                        if (!p.Disposing)
                        {
                            if (p.deathCount > 2)
                            {
                                p.deathCount -= 2;
                               // ConsoleWrite("" + p.deathCount);
                            }

                            if (!p.Alive && p.respawnTimer < DateTime.Now && !p.respawnExpired)
                            {
                                //ConsoleWrite("active");
                                p.respawnExpired = true;
                                SendPlayerRespawn(p);//new respawn
                            }
                        }
                        if (p.Alive)
                        {
                            p.Digs += 2.0f;//2 seconds
                            if (p.Digs > 5.0f)//5 seconds maximum buffer
                            {
                                p.Digs = 5.0f;
                            }
                            if (p.Annoying > 0)
                            {
                                p.Annoying -= 1;

                                if (p.Annoying > 15)
                                {
                                    if (varGetB("autoban"))
                                    {
                                        p.Warnings += 1;
                                        if (p.Warnings < varGetI("warnings"))
                                        {
                                            SendServerMessageToPlayer("*** You now have a warning for disruptive behaviour! " + p.Warnings + "/" + varGetI("warnings") + " ***", p.NetConn);
                                            p.Annoying = 0;
                                        }
                                        else
                                        {
                                            KickPlayer(p, true);
                                            continue;
                                        }

                                    }
                                }
                            }

                            if (varGetB("sandbox") && !SiegeBuild)
                            {
                                if (p.Ore < p.OreMax)
                                {
                                    p.Ore = p.OreMax;
                                    SendOreUpdate(p);
                                }
                            }

                            switch (p.Content[10])
                            {
                                case 1://material artifact personal
                                    if (randGen.Next(1, 4) == 3)
                                    {
                                        if (p.Ore < p.OreMax)
                                        {
                                            p.Ore += 40;
                                            if (p.Ore >= p.OreMax)
                                                p.Ore = p.OreMax;

                                            SendOreUpdate(p);
                                        }
                                    }
                                    break;
                                case 5://golden artifact personal
                                    if (p.Ore > 99)
                                    {
                                        if (p.Weight < p.WeightMax)
                                        {
                                            p.Weight++;
                                            p.Cash += 10;
                                            p.Ore -= 100;
                                            SendCashUpdate(p);
                                            SendWeightUpdate(p);
                                            SendOreUpdate(p);
                                            PlaySound(InfiniminerSound.CashDeposit, p.Position);
                                        }
                                    }
                                    break;
                                case 6://storm artifact personal
                                    if (artifactActive[(byte)((p.Team == PlayerTeam.Red) ? PlayerTeam.Blue : PlayerTeam.Red), 6] == 0)//stored storm artifact makes team immune
                                        foreach (Player pt in playerList.Values)
                                        {
                                            if (p.Team != pt.Team && pt.Alive)
                                            {
                                                float distfromPlayer = (p.Position - pt.Position).Length();
                                                if (distfromPlayer < 5 && pt.StatusEffect[4] == 0)
                                                {
                                                    if (pt.Health > 5 + (uint)(ResearchComplete[(byte)p.Team, (byte)Research.Destruction] * 2))
                                                    {
                                                        pt.Health -= 5 + (uint)(ResearchComplete[(byte)p.Team, (byte)Research.Destruction] * 2);
                                                        SendHealthUpdate(pt);
                                                    }
                                                    else
                                                    {
                                                        p.Score += 10;
                                                        p.Exp += 10;
                                                        Player_Dead(pt, "was shocked!");
                                                    }

                                                    EffectAtPoint(pt.Position - Vector3.UnitY * 1.5f, 3);
                                                }
                                            }
                                        }
                                    break;
                                case 10://tremor
                                    if(artifactActive[(byte)((p.Team == PlayerTeam.Red) ? PlayerTeam.Blue : PlayerTeam.Red),6] == 0)//stored storm artifact makes team immune
                                    foreach (Player pt in playerList.Values)
                                    {
                                        if (p.Team != pt.Team && pt.Alive)
                                        {
                                            float distfromPlayer = (p.Position - pt.Position).Length();
                                            if (distfromPlayer < 7 && pt.StatusEffect[4] == 0)
                                            {
                                                SendPlayerVelocity(pt, new Vector3(0, (float)(randGen.NextDouble()+0.2), 0));
                                            }
                                        }
                                    }
                                    break;
                                case 13://explosive
                                    if (artifactActive[(byte)((p.Team == PlayerTeam.Red) ? PlayerTeam.Blue : PlayerTeam.Red), 8] == 0)//medical prevents explosion
                                    {
                                        p.StatusEffect[5]++;

                                        if (p.StatusEffect[5] == 4)
                                        {
                                            Player_Dead(p, "exploded violently!");
                                            DetonateAtPoint((int)p.Position.X, (int)p.Position.Y, (int)p.Position.Z, p.Team, false);

                                            p.StatusEffect[5] = 0;
                                        }
                                        else
                                        {
                                            PlaySound(InfiniminerSound.RadarSwitch, p.Position);
                                        }
                                    }
                                    break;
                            }

                            if (p.FallBuffer > 30)
                            {
                                p.FallBuffer = 30;
                            }
                            else
                            {
                                p.FallBuffer += 2;
                            }

                            if (p.StatusEffect[6] > 1)
                            {
                                p.StatusEffect[6]--;
                                if (p.StatusEffect[6] == 4)
                                {
                                    int rmsg = randGen.Next(1,6);
                                    switch (rmsg)
                                    {
                                        case 1:
                                            SendServerMessageToPlayer("You feel numb.", p.NetConn);
                                            break;
                                        case 2:
                                            SendServerMessageToPlayer("Something is watching you!", p.NetConn);
                                            break;
                                        case 3:
                                            SendServerMessageToPlayer("You feel a spine chilling cold!", p.NetConn);
                                            break;
                                        case 4:
                                            SendServerMessageToPlayer("You gasp for air!", p.NetConn);
                                            break;

                                        default:
                                            SendServerMessageToPlayer("You feel death's grip!", p.NetConn);
                                            break;
                                    }
                                }
                                else if (p.StatusEffect[6] == 1)
                                {
                                    if (randGen.Next(1, 6) == 2)
                                    {
                                        p.StatusEffect[6] = 0;
                                        Player_Dead(p, "was pulled into the underworld!");
                                    }
                                    else
                                    {
                                        SendServerMessageToPlayer("The feeling passes..", p.NetConn);
                                        p.StatusEffect[6] = randGen.Next(10, 20);
                                    }
                                }
                            }
                            if (p.StatusEffect[7] > 0)//inferno
                            {
                                uint flamedamage = p.HealthMax / 20;
                                if (p.Health > flamedamage)
                                {
                                    p.Health -= flamedamage;
                                    EffectAtPoint(p.Position - Vector3.UnitY * 1.5f, 5);
                                    SendHealthUpdate(p);
                                }
                                else
                                {
                                    EffectAtPoint(p.Position - Vector3.UnitY * 1.5f, 5);
                                    p.StatusEffect[5] = 0;
                                    Player_Dead(p, "melted!");
                                }
                            }

                            if(p.Alive)
                            if (p.Health >= p.HealthMax && p.StatusEffect[2] == 0)
                            {
                                p.Health = p.HealthMax;
                            }
                            else
                            {
                                if (p.StatusEffect[2] < 1)
                                {
                                    if (p.StatusEffect[2] < 0)//bane immunity
                                        p.StatusEffect[2]++;

                                    if (p.StatusEffect[3] > 0)
                                    {
                                        int healDmg = 0;
                                        if (p.Team == PlayerTeam.Red)
                                        {    
                                            healDmg = 10 + ResearchComplete[(byte)PlayerTeam.Red, (byte)Research.Fortify]*4;
                                        }
                                        else if (p.Team == PlayerTeam.Blue)
                                        {
                                            healDmg = 10 + ResearchComplete[(byte)PlayerTeam.Blue, (byte)Research.Fortify]*4;
                                        }
                                        p.Health += (uint)healDmg;

                                        if (p.Health >= p.HealthMax)
                                            p.StatusEffect[3] = 0;
                                        else 
                                            p.StatusEffect[3]--;
                                    }

                                    if (randGen.Next(2) == 1)//halved regeneration
                                    p.Health = (uint)(p.Health + teamRegeneration[(byte)p.Team]);

                                    if (p.Content[10] == 3)//regeneration artifact
                                    {
                                        p.Health += 4;
                                    }

                                    if (p.Health >= p.HealthMax)
                                    {
                                        p.Health = p.HealthMax;
                                    }
                                    SendHealthUpdate(p);
                                }
                                else//bane effect
                                {
                                    if (p.StatusEffect[3] > 0)//bane removes healing
                                        p.StatusEffect[3] = 0;

                                    int baneDmg = 0;
                                    if (p.Team == PlayerTeam.Red)
                                    {
                                        baneDmg = p.StatusEffect[2]/8 + ResearchComplete[(byte)PlayerTeam.Blue, (byte)Research.Destruction];
                                    }
                                    else
                                    {
                                        baneDmg = p.StatusEffect[2]/8 + ResearchComplete[(byte)PlayerTeam.Red, (byte)Research.Destruction];
                                    }

                                        if (p.Health > baneDmg)//medical
                                        {
                                            p.Health -= (uint)(baneDmg);
                                            p.LastHit = DateTime.Now;
                                            SendHealthUpdate(p);

                                            p.StatusEffect[2]--;
                                            foreach (Player pd in playerList.Values)
                                            {
                                                if (pd.Alive)
                                                    if (pd.Team == p.Team && pd != p && artifactActive[(byte)p.Team, 8] == 0)
                                                    {
                                                        float distfromPlayer = (p.Position - pd.Position).Length();
                                                        if (distfromPlayer < 4)
                                                        {
                                                            if (pd.StatusEffect[2] == 0)
                                                            {
                                                                SendServerMessageToPlayer("You have been infected!", pd.NetConn);
                                                                pd.StatusEffect[2] = 20 + p.StatusEffect[2] / 2;
                                                                EffectAtPoint(pd.Position - Vector3.UnitY * 1.5f, 2);
                                                            }
                                                        }
                                                    }
                                            }

                                        }
                                        else
                                        {
                                            Player_Dead(p, "was ravaged by disease!");
                                        }
                                }
                            }
                            
                            if (p.Class == PlayerClass.Prospector)
                            {
                                if (p.Content[5] == 1)
                                {
                                    p.Content[6]--;
                                    if (p.Content[6] < 1)
                                    {
                                        //p.Content[1] = 0;//should autoupdate via p.radar
                                        SendPlayerContentUpdate(p, 1);
                                        p.Content[5] = 0;//sight
                                        SendContentSpecificUpdate(p, 5);
                                        SendContentSpecificUpdate(p, 6);
                                        SendPlayerContentUpdate(p, 5);
                                        SendServerMessageToPlayer("Hide must now recharge!", p.NetConn);
                                        EffectAtPoint(p.Position - Vector3.UnitY * 1.5f, 1);
                                    }
                                }
                                else
                                {
                                    if (p.Content[6] < 3)
                                    {
                                        p.Content[6]++;
                                    }
                                    else if (p.Content[6] < 4)
                                    {
                                        p.Content[6]++;
                                        SendContentSpecificUpdate(p, 6);//hide recharged
                                    }

                                }
                            }

                            //if (p.Class == PlayerClass.Prospector)//temperature data//giving everyone
                            //{
                            //    p.Content[6] = 0;
                            //    for(int a = -5;a < 6;a++)
                            //        for(int b = -5;b < 6;b++)
                            //            for (int c = -5; c < 6; c++)
                            //            {
                            //                int nx = a + (int)p.Position.X;
                            //                int ny = b + (int)p.Position.Y;
                            //                int nz = c + (int)p.Position.Z;
                            //                if (nx < MAPSIZE - 1 && ny < MAPSIZE - 1 && nz < MAPSIZE - 1 && nx > 0 && ny > 0 && nz > 0)
                            //                {
                            //                    BlockType block = blockList[nx,ny,nz];
                            //                    if (block == BlockType.Lava || block == BlockType.MagmaBurst || block == BlockType.MagmaVent)
                            //                    {
                            //                        p.Content[6] += 5 - Math.Abs(a) + 5 - Math.Abs(b) + 5 - Math.Abs(c);
                            //                    }
                            //                }
                            //            }

                            //    if (p.Content[6] > 0)
                            //        SendContentSpecificUpdate(p, 6);
                            //}
                        }
                    }
                }

                TimeSpan timeSpanZ = DateTime.Now - lastFlowCalcZ;
                serverTime[timeQueue] = DateTime.Now - lastTime;//timeQueue

                timeQueue += 1;
                if (timeQueue > 19)
                    timeQueue = 0;

                lastTime = DateTime.Now;
                delta = (float)((serverTime[0].TotalSeconds + serverTime[1].TotalSeconds + serverTime[2].TotalSeconds + serverTime[3].TotalSeconds + serverTime[4].TotalSeconds + serverTime[5].TotalSeconds + serverTime[6].TotalSeconds + serverTime[7].TotalSeconds + serverTime[8].TotalSeconds + serverTime[9].TotalSeconds + serverTime[10].TotalSeconds + serverTime[11].TotalSeconds + serverTime[12].TotalSeconds + serverTime[13].TotalSeconds + serverTime[14].TotalSeconds + serverTime[15].TotalSeconds + serverTime[16].TotalSeconds + serverTime[17].TotalSeconds + serverTime[18].TotalSeconds + serverTime[19].TotalSeconds) / 20);
                Sunray();

                if (!sleeping)
                {
                    if (timeSpanZ.TotalMilliseconds > 50)
                    {
                        lastFlowCalcZ = DateTime.Now;
                        DoItems();
                    }
                    //random diamond appearance
                    int chance = 100000;

                    if (varGetI("siege") == 5)
                    {
                        chance = 50000;
                    }

                    if (randGen.Next(1, chance) == 2)
                    {
                        ushort diamondx = (ushort)randGen.Next(4, 57);
                        ushort diamondy = 0;
                        ushort diamondz = (ushort)randGen.Next(4, 57);

                        if (varGetI("siege") == 5)
                        {
                            diamondy = (ushort)randGen.Next(3, 11);
                        }
                        else
                        {
                            diamondy = (ushort)randGen.Next(3, 30);
                        }
                        if (varGetI("siege") == 0 || varGetI("siege") == 5)
                        {
                            if (blockList[diamondx, diamondy, diamondz] == BlockType.Dirt)
                            {
                                // ConsoleWrite("diamond spawned at " + diamondx + "/" + diamondy + "/" + diamondz);
                                SetBlock(diamondx, diamondy, diamondz, BlockType.Diamond, PlayerTeam.None);
                                blockListHP[diamondx, diamondy, diamondz] = BlockInformation.GetMaxHP(BlockType.Diamond);
                            }
                        }
                        //else if (varGetI("siege") == 4)
                        //{
                        //    diamondx = (ushort)randGen.Next(4, 57);
                        //    diamondy = (ushort)randGen.Next(2, 30);
                        //    diamondz = (ushort)randGen.Next((MAPSIZE/2) -6, (MAPSIZE/2) + 6);

                        //    if ((BlockInformation.GetMaxHP(blockList[diamondx, diamondy, diamondz]) != 0 && BlockInformation.GetMaxHP(blockList[diamondx, diamondy, diamondz]) < 10) || blockList[diamondx, diamondy, diamondz] == BlockType.None)
                        //    {
                        //        SetBlock(diamondx, diamondy, diamondz, BlockType.Diamond, PlayerTeam.None);
                        //        blockListHP[diamondx, diamondy, diamondz] = BlockInformation.GetMaxHP(BlockType.Diamond);
                        //    }
                        //}
                    }
                    if (randGen.Next(1, 11000) == 2)
                    {
                        if (varGetI("siege") == 5)
                        {
                            ushort gx = (ushort)randGen.Next(4, 57);
                            ushort gy = 3;// (ushort)randGen.Next(3, 24);
                            ushort gz = (ushort)randGen.Next((MAPSIZE/2) - 4, (MAPSIZE/2) + 4);

                            
                                int totals = 0;//lengthofgold = randGen.Next(2, 7);
                                int amount = randGen.Next(1, 3);
                                int wx = 0;
                                int wy = 0;
                                int wz = 0;

                                while (totals < 11)
                                    if (wx + gx < MAPSIZE && wx + gx > 0 && wy + gy < MAPSIZE && wy + gy > 0)
                                    {
                                        if (blockList[gx + wx, gy + wy, gz + wz] == BlockType.Gold || blockList[gx + wx, gy + wy, gz + wz] == BlockType.Lava)
                                        {
                                            wy++;
                                            totals++;
                                        }
                                        else
                                        {
                                            if ((BlockInformation.GetMaxHP(blockList[gx + wx, gy + wy, gz + wz]) != 0 && BlockInformation.GetMaxHP(blockList[gx + wx, gy + wy, gz + wz]) < 10) || blockList[gx + wx, gy + wy, gz + wz] == BlockType.None)
                                            {
                                                int ly = gy + wx;
                                                for(int lx = gx + wx - 1;lx < gx + wx + 2;lx++)
                                                   // for (int ly = gy + wy - 1; ly < gy + wy + 2; ly++)
                                                        for (int lz = gz + wz - 1; lz < gz + wz + 2; lz++)
                                                        {
                                                            if (lz == gz + wz && ly == gy + wy && lx == gz + wz)
                                                            {
                                                            }
                                                            else if (randGen.Next(1, 20) == 1)
                                                            {
                                                                if(blockList[lx,ly,lz] == BlockType.None)
                                                                SetBlock((ushort)(lx), (ushort)(ly), (ushort)(lz), BlockType.Lava, PlayerTeam.None);
                                                            }
                                                        }
                                                //if(gy + wy - 1 > 0)
                                                //if (blockList[gx + wx, gy + wy - 1, gz + wz] == BlockType.Lava)
                                                //{
                                                //    if (gy + wy < MAPSIZE)
                                                //    SetBlock((ushort)(gx + wx), (ushort)(gy + wy + 1), (ushort)(gz + wz), BlockType.Lava, PlayerTeam.None);
                                                //}
                                                SetBlock((ushort)(gx + wx), (ushort)(gy + wy), (ushort)(gz + wz), BlockType.Gold, PlayerTeam.None);
                                                blockListHP[gx + wx, gy + wy, gz + wz] = BlockInformation.GetMaxHP(BlockType.Gold);
                                                amount--;

                                                if (amount == 0)
                                                    break;
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                    }
                                //    
                                //while (lengthofgold > 0)
                                //{
                                //    if(wx+gx < MAPSIZE && wx+gx > 0 && wy+gy < MAPSIZE && wy+gy > 0)
                                //    if ((BlockInformation.GetMaxHP(blockList[gx+wx, gy+wy, gz+wz]) != 0 && BlockInformation.GetMaxHP(blockList[gx+wx, gy+wy, gz+wz]) < 10) || blockList[gx+wx, gy+wy, gz+wz] == BlockType.None)
                                //    {
                                //        SetBlock((ushort)(gx + wx), (ushort)(gy + wy), (ushort)(gz + wz), BlockType.Gold, PlayerTeam.None);
                                //        blockListHP[gx + wx, gy + wy, gz + wz] = BlockInformation.GetMaxHP(BlockType.Gold);
                                //    }

                                //    wx += randGen.Next(0, 2) - 1;
                                //    wy += randGen.Next(0, 2) - 1;
                                //    wz += randGen.Next(0, 2) - 1;
                                    
                                //    lengthofgold--;
                                //}

                        }
                    }
                }
                // Handle console keypresses.
                while (Console.KeyAvailable)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Enter:
                            Console.WriteLine();
                            if (consoleInput.Length > 0)
                            {
                                ConsoleProcessInput();
                                if (commandHistory.Count == 0 || commandHistory[commandHistory.Count - 1] != consoleInput)
                                {
                                    commandHistory.Add(consoleInput);
                                }
                                historyIndex = commandHistory.Count;
                                consoleInput = "";
                            }
                            Console.Write("> ");
                            break;

                        case ConsoleKey.Backspace:
                            if (consoleInput.Length > 0)
                            {
                                consoleInput = consoleInput.Substring(0, consoleInput.Length - 1);
                                Console.Write("\b \b");
                            }
                            break;

                        case ConsoleKey.UpArrow:
                            if (commandHistory.Count > 0 && historyIndex > 0)
                            {
                                historyIndex--;
                                Console.Write("\r" + new string(' ', consoleInput.Length + 2));
                                Console.Write("\r> ");
                                consoleInput = commandHistory[historyIndex];
                                Console.Write(consoleInput);
                            }
                            break;

                        case ConsoleKey.DownArrow:
                            if (historyIndex < commandHistory.Count - 1)
                            {
                                historyIndex++;
                                Console.Write("\r" + new string(' ', consoleInput.Length + 2));
                                Console.Write("\r> ");
                                consoleInput = commandHistory[historyIndex];
                                Console.Write(consoleInput);
                            }
                            else if (historyIndex == commandHistory.Count - 1)
                            {
                                historyIndex = commandHistory.Count;
                                Console.Write("\r" + new string(' ', consoleInput.Length + 2));
                                Console.Write("\r> ");
                                consoleInput = "";
                            }
                            break;

                        default:
                            if (!char.IsControl(keyInfo.KeyChar))
                            {
                                consoleInput += keyInfo.KeyChar;
                                Console.Write(keyInfo.KeyChar);
                            }
                            break;
                    }
                }

                // Is the game over?
                if (winningTeam != PlayerTeam.None && !restartTriggered)
                {
                    BroadcastGameOver();
                    restartTriggered = true;
                    restartTime = DateTime.Now.AddSeconds(10);
                }

                // Restart the server?
                if (restartTriggered && DateTime.Now > restartTime)
                {
                    if(varGetI("autosave") > 0)
                    SaveLevel("autosave_" + (UInt64)DateTime.Now.ToBinary() + ".lvl");
                    lastMapBackup = DateTime.Now;
                    netServer.Shutdown("The server is restarting.");
                    
                    Thread.Sleep(100);

                    physicsEnabled = false;
                    physics?.Join(1000); // Wait up to 1 second for thread to finish
                    if(varGetI("siege") > 0)
                        varSet("siege", 1);
                    return true; // terminates server thread completely
                }

                // Pass control over to waiting threads.
                if(sleeping == true) {
                    Thread.Sleep(50);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }

            MessageAll("Server going down NOW!");

            netServer.Shutdown("The server was terminated.");
            return false;
        }

        public void VictoryCheck()
        {
            //foreach (Player p in playerList.Values)
            //{
              //  if (p.Position.Y > 64 - Defines.GROUND_LEVEL)
             //       DepositCash(p);
           // }

            if (varGetB("sandbox"))
                return;
            if (teamArtifactsBlue >= winningCashAmount && winningTeam == PlayerTeam.None)
                winningTeam = PlayerTeam.Blue;
            if (teamArtifactsRed >= winningCashAmount && winningTeam == PlayerTeam.None)
                winningTeam = PlayerTeam.Red;
        }

        public void EraseVacuum()
        {
            for (ushort i = 0; i < MAPSIZE; i++)
                for (ushort j = 0; j < MAPSIZE; j++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                        if (blockList[i, j, k] == BlockType.Vacuum)
                        {
                            blockList[i, j, k] = BlockType.None;
                        }
        }

        public void DoPhysics()
        {
            DateTime lastFlowCalc = DateTime.Now;
            randGenB = new Random();

            while (1==1)
            {
                while (physicsEnabled)
                {
                    if (SiegeBuild == false)
                    {
                        TimeSpan timeSpan = DateTime.Now - lastFlowCalc;

                        if (timeSpan.TotalMilliseconds > 400)
                        {

                            lastFlowCalc = DateTime.Now;
                            DoStuff();

                        }
                    }
                    Thread.Sleep(2);
                }
                Thread.Sleep(50);
            }
        }

        public void DoItems()
        {
            Vector3 tv = Vector3.Zero;
            Vector3 tvv = Vector3.Zero;
        
            float GRAVITY = 0.1f;

            for (int a = highestitem; a >= 0; a--)
            {
                if(itemList.ContainsKey((uint)(a)))
                {
                    Item i = itemList[(uint)(a)];

                    switch (i.Type)
                    {
                        case ItemType.Bomb:
                            {
                                i.Content[5]--;

                                if (i.Content[5] == 1)
                                {
                                    BombAtPoint((int)(i.Position.X), (int)(i.Position.Y), (int)(i.Position.Z), (PlayerTeam)i.Content[6]);
                                    i.Disposing = true;
                                    continue;
                                }
                            }
                            break;
                        case ItemType.DirtBomb:
                            {
                                i.Content[5]--;

                                if (i.Content[5] == 1)
                                {
                                    double dist;
                                    foreach(Player p in playerList.Values)
                                    {
                                        if (!p.Disposing)
                                        {
                                            dist = Distf(p.Position, i.Position);
                                            if (dist < 5)//player in range of bomb on server?
                                            {
                                                if (randGen.Next(10) == 1)//defuse
                                                {
                                                    i.Disposing = true;
                                                    continue;
                                                }
                                                else
                                                {
                                                    i.Content[5] = 6 + randGen.Next(10);//stall bomb
                                                }
                                                //ConsoleWrite("bomb stalling");
                                                break;
                                            }
                                        }
                                    }

                                    if(!i.Disposing)
                                    if (i.Content[5] == 1)
                                    {
                                        MaterialBombAtPoint((int)(i.Position.X), (int)(i.Position.Y), (int)(i.Position.Z), (PlayerTeam)i.Content[6], BlockType.Dirt);

                                        NetBuffer msgBuffer = netServer.CreateBuffer();
                                        msgBuffer.Write((byte)InfiniminerMessage.TriggerExplosion);
                                        msgBuffer.Write(i.Position);
                                        msgBuffer.Write(0);
                                        foreach (NetConnection netConn in playerList.Keys)
                                            if (netConn.Status == NetConnectionStatus.Connected)
                                                netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

                                        i.Disposing = true;
                                        continue;
                                    }
                                }
                            }
                            break;
                        case ItemType.Gold:
                            i.Content[1]++;
                            if (i.Content[1] > 24000)//ten minute decay
                            {
                                i.Disposing = true;
                            }
                            break;
                        case ItemType.Ore:
                            i.Content[1]++;
                            if (i.Content[1] > 2400)//two minute decay
                            {
                                i.Disposing = true;
                            }
                            break;
                        case ItemType.Diamond:
                            i.Content[1]++;

                            if (i.Content[1] > DECAY_TIMER_PS)//two minute decay
                            {
                                i.Disposing = true;
                            }
                            break;
                        case ItemType.Artifact:
                            {
                                if (i.Content[7] > 0)
                                {
                                    if (NEW_ART_RED == i.ID || NEW_ART_BLUE == i.ID)
                                    {
                                        if (i.Content[7] > DECAY_TIMER_SPAWN)
                                        {
                                            i.Content[7] = 0;
                                            i.Content[1] = 0;
                                        }
                                        else
                                        {
                                            i.Content[7]++;
                                        }
                                    }
                                    else
                                    {
                                        i.Content[7] = 0;
                                    }
                                }

                                if (i.Content[6] == 0 && i.Content[7] == 0)//not locked
                                {
                                    i.Content[1]++;
                                    if (i.Content[1] > DECAY_TIMER)//decay
                                    {
                                        SendServerMessageRed(ArtifactInformation.GetName(i.Content[10]) + " was forgotten!");
                                        SendServerMessageBlue(ArtifactInformation.GetName(i.Content[10]) + " was forgotten!");

                                        i.Disposing = true;
                                        break;
                                    }
                                    else if (i.Content[1] == DECAY_TIMER - 200)//decay warning
                                    {
                                         SendServerMessageRed(ArtifactInformation.GetName(i.Content[10]) + " will decay shortly!");
                                         SendServerMessageBlue(ArtifactInformation.GetName(i.Content[10]) + " will decay shortly!");
                                    }
                                    
                                    if (i.Content[10] == 3)//regeneration artifact
                                    {
                                        if (randGen.Next(1, 25) == 10)
                                        {
                                            int maxhp;
                                            DebrisEffectAtPoint(i.Position.X, i.Position.Y, i.Position.Z, BlockType.Highlight, 1);
                                            for (int ax = -3 + (int)i.Position.X; ax < 4 + (int)i.Position.X; ax++)
                                                for (int ay = -3 + (int)i.Position.Y; ay < 4 + (int)i.Position.Y; ay++)
                                                    for (int az = -3 + (int)i.Position.Z; az < 4 + (int)i.Position.Z; az++)
                                                    {
                                                        if (ax < MAPSIZE - 1 && ay < MAPSIZE - 1 && az < MAPSIZE - 1 && ax > 0 && ay > 0 && az > 0)
                                                        {
                                                            if (blockCreatorTeam[ax, ay, az] != PlayerTeam.None)
                                                            {
                                                                maxhp = BlockInformation.GetMaxHP(blockList[ax, ay, az]);
                                                                if (maxhp > 1)
                                                                    if (blockListHP[ax, ay, az] < BlockInformation.GetMaxHP(blockList[ax, ay, az]))
                                                                    {
                                                                        blockListHP[ax, ay, az] += 6 + ResearchComplete[(byte)blockCreatorTeam[ax, ay, az], (byte)Research.Fortify];

                                                                        if (blockListHP[ax, ay, az] >= maxhp)
                                                                        {
                                                                            switch (blockList[ax, ay, az])
                                                                            {
                                                                                case BlockType.SolidBlue:
                                                                                    SetBlock((ushort)ax, (ushort)ay, (ushort)az, BlockType.SolidBlue2, PlayerTeam.Blue);
                                                                                    blockListHP[ax, ay, az] = maxhp;
                                                                                    break;

                                                                                case BlockType.SolidRed:
                                                                                    SetBlock((ushort)ax, (ushort)ay, (ushort)az, BlockType.SolidRed2, PlayerTeam.Red);
                                                                                    blockListHP[ax, ay, az] = maxhp;
                                                                                    break;

                                                                                default:
                                                                                    blockListHP[ax, ay, az] = maxhp;
                                                                                    break;
                                                                            }

                                                                        }
                                                                    }
                                                            }
                                                        }
                                                    }
                                        }
                                    }
                                    else if (i.Content[10] == 6)//storm artifact
                                    {
                                        if (randGen.Next(1, 20) == 10 && i.Content[11] < 30)
                                        {
                                            int ax = randGen.Next(3) - 1;
                                            int ay = randGen.Next(2) + 1;
                                            int az = randGen.Next(3) - 1;

                                            if (BlockAtPoint(new Vector3(ax + i.Position.X, ay + i.Position.Y, az + i.Position.Z)) == BlockType.None)
                                            {
                                                i.Content[11]++;
                                                SetBlock((ushort)(ax + i.Position.X), (ushort)(ay + i.Position.Y), (ushort)(az + i.Position.Z), BlockType.Water, PlayerTeam.None);
                                            }
                                        }
                                    }
                                    else if (i.Content[10] == 7)//reflection artifact
                                    {

                                    }
                                    else if (i.Content[10] == 10)//tremor artifact
                                    {
                                        if (randGen.Next(1, 10) == 5 && i.Content[11] < 40)
                                        {
                                            BlockType block = BlockType.None;

                                            int ax = randGen.Next(8) - 4;
                                            int ay = randGen.Next(12) + 2;
                                            int az = randGen.Next(8) - 4;
                                            int bx = 0;
                                            int by = 0;
                                            int bz = 0;
                                            bx = ax + (int)i.Position.X;
                                            by = ay + (int)i.Position.Y;
                                            bz = az + (int)i.Position.Z;

                                            if (bx > 0 && by > 1 && bz > 0 && bx < MAPSIZE - 1 && by < MAPSIZE - 1 && bz < MAPSIZE - 1)
                                            {
                                                bx = ax + (int)i.Position.X;
                                                by = ay + (int)i.Position.Y;
                                                bz = az + (int)i.Position.Z;
                                                block = blockList[bx, by, bz];

                                                if(blockList[bx, by - 1,bz] == BlockType.None)
                                                if(blockCreatorTeam[bx, by, bz] == PlayerTeam.None)
                                                {
                                                    if (BlockInformation.GetMaxHP(block) > 0)
                                                    {
                                                        if (block != BlockType.SolidBlue2 && block != BlockType.SolidRed2)
                                                        {
                                                            blockListContent[bx, by, bz, 10] = 1;//fall
                                                            i.Content[11]++;
                                                        }
                                                    }
                                                 
                                                }
                                            }
                                        }
                                    }
                                    else if (i.Content[10] == 16)//inferno
                                    {
                                        if (randGen.Next(1, 50) == 5 && i.Content[11] < 9)
                                        {
                                            BlockType block = BlockType.None;

                                            int ax = randGen.Next(4) - 2;
                                            int ay = -1;
                                            int az = randGen.Next(4) - 2;
                                            int bx = 0;
                                            int by = 0;
                                            int bz = 0;
                                            bx = ax + (int)i.Position.X;
                                            by = ay + (int)i.Position.Y;
                                            bz = az + (int)i.Position.Z;

                                            if (bx > 0 && by > 1 && bz > 0 && bx < MAPSIZE - 1 && by < MAPSIZE - 1 && bz < MAPSIZE - 1)
                                            {
                                                bx = ax + (int)i.Position.X;
                                                by = ay + (int)i.Position.Y;
                                                bz = az + (int)i.Position.Z;
                                                block = blockList[bx, by, bz];

                                                if (blockCreatorTeam[bx, by, bz] == PlayerTeam.None)
                                                {
                                                    if (BlockInformation.GetMaxHP(block) > 0 && blockListHP[bx,by,bz] < 201)
                                                    {
                                                        if (block != BlockType.SolidBlue2 && block != BlockType.SolidRed2)
                                                        {
                                                            SetBlock((ushort)bx, (ushort)by, (ushort)bz, BlockType.Lava, PlayerTeam.None);
                                                            blockListContent[bx,by,bz,1] = 120;
                                                            i.Content[11]++;
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                    }
                                }
                                else//arty was locked
                                {
                                }
                            }
                            break;

                        case ItemType.Mushroom://mushrooms of floating
                            continue;
                        case ItemType.Target://prospector target
                            i.Content[1]++;
                            if (i.Content[1] > 400)//twenty second decay
                            {
                                i.Disposing = true;
                            }
                            continue;
                        case ItemType.Spikes://
                            if (i.Velocity.Y > 0.4f)
                            {
                                blockListAttach[(int)i.Position.X, (int)i.Position.Y, (int)i.Position.Z, 0] = 0;
                                i.Disposing = true;
                            }
                            else if(!i.Disposing) 
                            {
                                if (i.Content[1] > 0)
                                    i.Content[1]--;
                            }
                            break;
                    }

                    tv = i.Position;
                    tv.Y -= 0.05f;//changes where the item rests

                    if (BlockAtPoint(tv + i.Velocity * (delta * 50)) == BlockType.None || BlockAtPoint(tv + i.Velocity * (delta * 50)) == BlockType.Water)//shouldnt be checking every 100ms, needs area check
                    {
                        i.Velocity.Y -= GRAVITY*i.Weight;// *(delta * 50);//delta interferes with sleep states
                        i.Position += i.Velocity * (delta * 50);
                        //i.Velocity.X = i.Velocity.X * 0.99f;
                        //i.Velocity.Z = i.Velocity.Z * 0.99f;
                        SendItemUpdate(i);

                        if (i.Position.Y < -50)//fallen off map
                        {
                            i.Disposing = true;
                        }


                    }
                    else if (i.Velocity.X != 0.0f || i.Velocity.Y != 0.0f || i.Velocity.Z != 0.0f)
                    {
                        Vector3 nv = i.Velocity;//adjustment axis
                        nv.Y = i.Velocity.Y;
                        nv.X = 0;
                        nv.Z = 0;
                        if (Math.Abs(i.Velocity.Y) > 0.5f)
                        {
                            if (BlockAtPoint(tv + nv) != BlockType.None || BlockAtPoint(tv + nv) != BlockType.Water)
                            {
                                i.Velocity.Y = -i.Velocity.Y / 2;
                                if (i.Type == ItemType.Rope)
                                {
                                    i.Velocity = Vector3.Zero;
                                }
                                continue;
                            }
                        }
                        else
                        {
                            i.Velocity.Y = 0;
                        }

                        nv.X = i.Velocity.X;
                        nv.Y = 0;
                        nv.Z = 0;
                        if (Math.Abs(i.Velocity.X) > 0.2f)
                        {
                            if (BlockAtPoint(tv + nv) != BlockType.None || BlockAtPoint(tv + nv) != BlockType.Water)
                            {
                                i.Velocity.X = -i.Velocity.X / 2;
                                if (i.Type == ItemType.Rope)
                                {
                                    i.Velocity = Vector3.Zero;
                                }
                                continue;
                            }
                        }
                        else
                        {
                            i.Velocity.X = 0;
                        }


                        nv.X = 0;
                        nv.Y = 0;
                        nv.Z = i.Velocity.Z;
                        if (Math.Abs(i.Velocity.Z) > 0.2f)
                        {
                            if (BlockAtPoint(tv + nv) != BlockType.None || BlockAtPoint(tv + nv) != BlockType.Water)
                            {
                                i.Velocity.Z = -i.Velocity.Z / 2;
                                if (i.Type == ItemType.Rope)
                                {
                                    i.Velocity = Vector3.Zero;
                                }
                                continue;
                            }
                        }
                        else
                        {
                            i.Velocity.Z = 0;
                        }
                    }
                    else
                    {
                        //item no longer needs to move
                    }
                }
            }

           /* foreach (KeyValuePair<uint, Item> i in itemList)
            {

                if (i.Value.Type == ItemType.Bomb)
                {
                    i.Value.Content[5]--;

                    if (i.Value.Content[5] == 1)
                    {
                        BombAtPoint((int)(i.Value.Position.X), (int)(i.Value.Position.Y), (int)(i.Value.Position.Z));
                        i.Value.Disposing = true;
                        continue;
                    }
                }
                tv = i.Value.Position;
                tv.Y -= 0.2f;//changes where the item rests
                
                if (BlockAtPoint(tv + i.Value.Velocity * (delta*50)) == BlockType.None)//shouldnt be checking every 100ms, needs area check
                {
                    i.Value.Velocity.Y -= GRAVITY;// *(delta * 50);//delta interferes with sleep states
                    i.Value.Position += i.Value.Velocity * (delta*50);
                    //i.Value.Velocity.X = i.Value.Velocity.X * 0.99f;
                    //i.Value.Velocity.Z = i.Value.Velocity.Z * 0.99f;
                    SendItemUpdate(i.Value);

                    if (i.Value.Position.Y < -50)//fallen off map
                    {
                        i.Value.Disposing = true;
                    }
                   

                }
                else if (i.Value.Velocity.X != 0.0f || i.Value.Velocity.Y != 0.0f || i.Value.Velocity.Z != 0.0f)
                {

                    Vector3 nv = i.Value.Velocity;//adjustment axis
                    nv.Y = i.Value.Velocity.Y;
                    nv.X = 0;
                    nv.Z = 0;
                    if (Math.Abs(i.Value.Velocity.Y) > 0.5f)
                    {
                        if (BlockAtPoint(tv + nv) != BlockType.None)
                        {
                            i.Value.Velocity.Y = -i.Value.Velocity.Y / 2;
                        }
                    }
                    else
                    {
                        i.Value.Velocity.Y = 0;
                    }

                    nv.X = i.Value.Velocity.X;
                    nv.Y = 0;
                    nv.Z = 0;
                    if (Math.Abs(i.Value.Velocity.X) > 0.5f)
                    {
                        if (BlockAtPoint(tv + nv) != BlockType.None)
                        {
                            i.Value.Velocity.X = -i.Value.Velocity.X / 2;
                        }
                    }
                    else
                    {
                        i.Value.Velocity.X = 0;
                    }


                    nv.X = 0;
                    nv.Y = 0;
                    nv.Z = i.Value.Velocity.Z;
                    if (Math.Abs(i.Value.Velocity.Z) > 0.5f)
                    {
                        if (BlockAtPoint(tv + nv) != BlockType.None)
                        {
                            i.Value.Velocity.Z = -i.Value.Velocity.Z / 2;
                        }
                    }
                    else
                    {
                        i.Value.Velocity.Z = 0;
                    }
                }
                else
                {
                   //item no longer needs to move
                }
                
            }*/
           
            foreach (KeyValuePair<uint, Item> i in itemList)
            {
                if (i.Value.Disposing && Filename == "")//dont remove items during save-state
                {
                    DeleteItem(i.Key);
                    break;
                }
            }
        }
        public void DoStuff()
        {
            physactive = true;
            int oreRedBefore = teamOreRed;
            int oreBlueBefore = teamOreBlue;
            frameid += 1;//make unique id to prevent reprocessing gravity

            if (scantime == 0)
            {
                foreach (Player p in playerList.Values)
                {
                    if (p.Alive && !p.Disposing)
                    {
                        p.Radar = p.Content[1];
                        p.Content[1] = 0;//clear all radars
                    }
                }
            }
            
            //volcano frequency
            if (randGenB.Next(1, 500) == 1 && physicsEnabled)
            {
                bool volcanospawn = true;
                while (volcanospawn == true)
                {
                    int vx = randGenB.Next(8, 52);
                    int vy = randGenB.Next(4, 50);
                    int vz = randGenB.Next(8, 52);

                    if (blockList[vx, vy, vz] != BlockType.Lava || blockList[vx, vy, vz] != BlockType.Spring || blockList[vx, vy, vz] != BlockType.MagmaVent || blockList[vx, vy, vz] != BlockType.Rock)//Fire)//volcano testing
                    {
                        if (blockList[vx, vy+1, vz] != BlockType.Lava || blockList[vx, vy+1, vz] != BlockType.Spring || blockList[vx, vy+1, vz] != BlockType.MagmaVent || blockList[vx, vy+1, vz] != BlockType.Rock)//Fire)//volcano testing
                        {
                            volcanospawn = false;
                            int vmag = randGenB.Next(30, 60);
                            ConsoleWrite("Volcanic eruption at " + vx + ", " + vy + ", " + vz + " Magnitude: "+ vmag);
                            SetBlock((ushort)(vx), (ushort)(vy), (ushort)(vz), BlockType.Lava, PlayerTeam.None);//magma cools down into dirt
                            blockListContent[vx, vy, vz, 0] = vmag;//volcano strength
                            blockListContent[vx, vy, vz, 1] = 960;//temperature
                            EarthquakeEffectAtPoint(vx, vy, vz, vmag);
                        }
                    }
                }
            }

            for (ushort i = 0; i < MAPSIZE; i++)
                for (ushort j = 0; j < MAPSIZE; j++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                    {
                        //gravity //needs to readd the block for processing, its missing on certain gravity changes
                        if (blockListContent[i, j, k, 10] > 0)
                        if (frameid != blockListContent[i, j, k, 10])
                        {// divide acceleration vector by 100 to create ghetto float vector
                            Vector3 newpoint = new Vector3((float)(blockListContent[i, j, k, 14] + blockListContent[i, j, k, 11]) / 100, (float)(blockListContent[i, j, k, 15] + blockListContent[i, j, k, 12]) / 100, (float)(blockListContent[i, j, k, 16] + blockListContent[i, j, k, 13]) / 100);
                            
                            ushort nx = (ushort)(newpoint.X);
                            ushort ny = (ushort)(newpoint.Y);
                            ushort nz = (ushort)(newpoint.Z);

                            blockListContent[i, j, k, 10] = 0;

                            if (nx < MAPSIZE - 1 && ny < MAPSIZE - 1 && nz < MAPSIZE - 1 && nx > 0 && ny > 0 && nz > 0)
                            {
                                if (BlockAtPoint(newpoint) == BlockType.None && blockList[i, j, k] != BlockType.None)
                                {
                                    SetBlock(nx, ny, nz, blockList[i, j, k], blockCreatorTeam[i, j, k]);
                                    blockListHP[nx, ny, nz] = blockListHP[i, j, k];
                                    for (ushort c = 0; c < 14; c++)//copy content from 0-13
                                    {
                                        blockListContent[nx, ny, nz, c] = blockListContent[i, j, k, c];

                                    }
                                    blockListContent[nx, ny, nz, 10] = frameid;

                                    if (blockListContent[nx, ny, nz, 12] > -50)//stop gravity from overflowing and skipping tiles
                                        blockListContent[nx, ny, nz, 12] = (int)((float)(blockListContent[nx, ny, nz, 12] - 50.0f));
                                    else
                                    {
                                        blockListContent[nx, ny, nz, 12] = -100;
                                    }

                                    blockListContent[nx, ny, nz, 14] = (int)(newpoint.X * 100);
                                    blockListContent[nx, ny, nz, 15] = (int)(newpoint.Y * 100);
                                    blockListContent[nx, ny, nz, 16] = (int)(newpoint.Z * 100);

                                    if (blockList[i, j, k] == BlockType.Explosive)//explosive list for tnt update
                                    {
                                        if (blockListContent[i, j, k, 17] == 0)//create owner
                                        {
                                            foreach (Player p in playerList.Values)
                                            {
                                                int cc = p.ExplosiveList.Count;

                                                int ca = 0;
                                                while (ca < cc)
                                                {
                                                    if (p.ExplosiveList[ca].X == i && p.ExplosiveList[ca].Y == j && p.ExplosiveList[ca].Z == k)
                                                    {
                                                        p.ExplosiveList.RemoveAt(ca);
                                                        blockListContent[i, j, k, 17] = (int)(p.ID);
                                                        break;
                                                    }
                                                    ca += 1;
                                                }
                                            }
                                        }

                                        if (blockListContent[i, j, k, 17] > 0)
                                        {
                                            foreach (Player p in playerList.Values)
                                            {
                                                if (p.ID == (uint)(blockListContent[i, j, k, 17]))
                                                {
                                                    //found explosive this belongs to
                                                    //p.ExplosiveList.Add(new Vector3(nx, ny, nz));
                                                    blockListContent[nx, ny, nz, 17] = blockListContent[i, j, k, 17];
                                                    //p.ExplosiveList.Remove(new Vector3(i, j, k));
                                                    blockListContent[i, j, k, 17] = 0;

                                                }
                                            }
                                        }
                                    }
                                    SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                }
                                else
                                {
                                    if (j > 0)
                                        if (blockList[i, j - 1, k] == BlockType.None || blockList[i, j - 1, k] == BlockType.Water || blockList[i, j - 1, k] == BlockType.Lava)//still nothing underneath us, but gravity state has just ended
                                        {
                                            BlockType oldblock = blockList[i, j - 1, k];//this replaces any lost water/lava

                                            blockListContent[i, j, k, 11] = 0;
                                            blockListContent[i, j, k, 12] = -100;
                                            blockListContent[i, j, k, 13] = 0;

                                            SetBlock(i, (ushort)(j - 1), k, blockList[i, j, k], blockCreatorTeam[i, j, k]);
                                            blockListHP[i, j - 1, k] = blockListHP[i, j, k];
                                            for (ushort c = 0; c < 14; c++)//copy content from 0-13
                                            {
                                                blockListContent[i, j - 1, k, c] = blockListContent[i, j, k, c];

                                            }
                                            blockListContent[i, j - 1, k, 10] = frameid;
                                            blockListContent[i, j - 1, k, 14] = (int)(i * 100);
                                            blockListContent[i, j - 1, k, 15] = (int)(j * 100);//120 for curve
                                            blockListContent[i, j - 1, k, 16] = (int)(k * 100);

                                            if (blockListContent[i, j, k, 17] > 0 && blockList[i, j, k] == BlockType.Explosive)//explosive list for tnt update
                                            {
                                                if (blockListContent[i, j, k, 17] == 0)//create owner if we dont have it
                                                {
                                                    foreach (Player p in playerList.Values)
                                                    {
                                                        int cc = p.ExplosiveList.Count;

                                                        int ca = 0;
                                                        while (ca < cc)
                                                        {
                                                            if (p.ExplosiveList[ca].X == i && p.ExplosiveList[ca].Y == j && p.ExplosiveList[ca].Z == k)
                                                            {
                                                                p.ExplosiveList.RemoveAt(ca);
                                                                blockListContent[i, j, k, 17] = (int)(p.ID);
                                                                break;
                                                            }
                                                            ca += 1;
                                                        }
                                                    }
                                                }

                                                if (blockListContent[i, j, k, 17] > 0)
                                                {
                                                    foreach (Player p in playerList.Values)
                                                    {
                                                        if (p.ID == (uint)(blockListContent[i, j, k, 17]))
                                                        {
                                                            //found explosive this belongs to
                                                            //p.ExplosiveList.Add(new Vector3(i, j - 1, k));
                                                            blockListContent[i, j - 1, k, 17] = blockListContent[i, j, k, 17];
                                                            //p.ExplosiveList.Remove(new Vector3(i, j, k));
                                                            blockListContent[i, j, k, 17] = 0;

                                                        }
                                                    }
                                                }
                                            }
                                            
                                            SetBlock(i, j, k, oldblock, PlayerTeam.None);
                                        }
                                        else
                                        {
                                            PlaySound(InfiniminerSound.RockFall, new Vector3(i, j, k));
                                        }
                                }
                            }
                            else
                            {
                                if (j > 0)//entire section is to allow blocks to drop once they have hit ceiling
                                    if (blockList[i, j - 1, k] == BlockType.None || blockList[i, j - 1, k] == BlockType.Water || blockList[i, j - 1, k] == BlockType.Lava)//still nothing underneath us, but gravity state has just ended
                                    {
                                        BlockType oldblock = blockList[i, j - 1, k];//this replaces any lost water/lava

                                        blockListContent[i, j, k, 11] = 0;
                                        blockListContent[i, j, k, 12] = -100;
                                        blockListContent[i, j, k, 13] = 0;

                                        SetBlock(i, (ushort)(j - 1), k, blockList[i, j, k], blockCreatorTeam[i, j, k]);
                                        blockListHP[i, j - 1, k] = blockListHP[i, j, k];
                                        for (ushort c = 0; c < 14; c++)//copy content from 0-13
                                        {
                                            blockListContent[i, j - 1, k, c] = blockListContent[i, j, k, c];

                                        }
                                        blockListContent[i, j - 1, k, 10] = frameid;
                                        blockListContent[i, j - 1, k, 14] = (int)(i * 100);
                                        blockListContent[i, j - 1, k, 15] = (int)(j * 100);//120 for curve
                                        blockListContent[i, j - 1, k, 16] = (int)(k * 100);

                                        if (blockListContent[i, j, k, 17] > 0 && blockList[i, j, k] == BlockType.Explosive)//explosive list for tnt update
                                        {
                                            foreach (Player p in playerList.Values)
                                            {
                                                if (p.ID == (uint)(blockListContent[i, j, k, 17]))
                                                {
                                                    //found explosive this belongs to
                                                    //p.ExplosiveList.Add(new Vector3(i, j - 1, k));
                                                    blockListContent[i, j - 1, k, 17] = blockListContent[i, j, k, 17];
                                                    //p.ExplosiveList.Remove(new Vector3(i, j, k));
                                                    blockListContent[i, j, k, 17] = 0;

                                                }
                                            }
                                        }
                                        SetBlock(i, j, k, oldblock, PlayerTeam.None);
                                    }
                                    else
                                    {
                                        PlaySound(InfiniminerSound.RockFall, new Vector3(i,j,k));
                                    }
                            }

                        }
                        //temperature
                        if (blockList[i, j, k] == BlockType.Lava && blockListContent[i, j, k, 1] > 0)//block is temperature sensitive
                        {
                            //if (blockList[i, j, k] == BlockType.Lava)
                            //{
                            if (blockListContent[i, j, k, 1] > 0)
                            {
                                blockListContent[i, j, k, 1] -= 1;
                                if (blockListContent[i, j, k, 1] == 0)
                                {
                                    SetBlock(i, j, k, BlockType.Mud, PlayerTeam.None);//magma cools down into dirt
                                    blockListContent[i, j, k, 0] = 120;//two minutes of mudout
                                    if (randGenB.Next(1, 10) == 5)
                                    {
                                        blockListContent[i, j, k, 1] = (byte)BlockType.Gold;//becomes this block
                                    }
                                    else
                                    {
                                        blockListContent[i, j, k, 1] = (byte)BlockType.Dirt;
                                    }
                                }
                                //    }
                            }
                        }
                            if (blockList[i, j, k] == BlockType.Water && !flowSleep[i, j, k] || blockList[i, j, k] == BlockType.Lava && !flowSleep[i, j, k] || blockList[i, j, k] == BlockType.Fire)//should be liquid check, not comparing each block
                            {//dowaterstuff //dolavastuff

                                BlockType liquid = blockList[i, j, k];
                                BlockType opposing = BlockType.None;

                                BlockType typeBelow = (j <= 0) ? BlockType.Vacuum : blockList[i, j - 1, k];//if j <= 0 then use block vacuum

                                if (liquid == BlockType.Water)
                                {
                                    opposing = BlockType.Lava;
                                }
                                else
                                {
                                    //lava stuff
                                    if (varGetB("roadabsorbs"))
                                    {
                                        BlockType typeAbove = ((int)j == MAPSIZE - 1) ? BlockType.None : blockList[i, j + 1, k];
                                        if (typeAbove == BlockType.Road)
                                        {
                                            SetBlock(i, j, k, BlockType.Road, PlayerTeam.None);
                                        }
                                    }
                                }

                                //if (liquid == BlockType.Lava && blockListContent[i, j, k, 0] > 0)//upcoming volcano
                                //{
                                //    if (i - 1 > 0 && i + 1 < MAPSIZE - 1 && k - 1 > 0 && k + 1 < MAPSIZE - 1 )
                                //    if (blockList[i + 1, j, k] == BlockType.None || blockList[i - 1, j, k] == BlockType.None || blockList[i, j, k + 1] == BlockType.None || blockList[i, j, k - 1] == BlockType.None || blockList[i + 1, j, k] == BlockType.Lava || blockList[i - 1, j, k] == BlockType.Lava || blockList[i, j, k + 1] == BlockType.Lava || blockList[i, j, k - 1] == BlockType.Lava)
                                //    {//if air surrounds the magma, then decrease volcanos power
                                //        blockListContent[i, j, k, 0] = blockListContent[i, j, k, 0] - 1;
                                //        blockListContent[i, j, k, 1] = 240 + blockListContent[i, j, k, 0] * 4;//temperature lowers as volcano gets further from its source
                                //    }

                                //    int x = randGenB.Next(-1, 1);
                                //    int z = randGenB.Next(-1, 1);

                                //    if (i + x > 0 && i + x < MAPSIZE - 1 && k + z > 0 && k + z < MAPSIZE - 1 && j + 1 < MAPSIZE - 1)
                                //        if (blockList[i + x, j + 1, k + z] != BlockType.Rock)
                                //        {
                                //            SetBlock((ushort)(i + x), (ushort)(j + 1), (ushort)(k + z), liquid, PlayerTeam.None);
                                //            blockListContent[i + x, j + 1, k + z, 0] = blockListContent[i, j, k, 0] - 1;//volcano strength decreases every upblock
                                //            blockListContent[i + x, j + 1, k + z, 1] = randGenB.Next(blockListContent[i, j, k, 0]*3, blockListContent[i, j, k, 0]*4);//give it temperature
                                //        }

                                //}

                                if (typeBelow != liquid && varGetB("insane") || liquid == BlockType.Fire)
                                {
                                    if (liquid == BlockType.Fire && blockListContent[i, j, k, 0] == 0)
                                    {
                                    }
                                    else
                                    {
                                        if (i > 0 && blockList[i - 1, j, k] == BlockType.None)
                                        {
                                            SetBlock((ushort)(i - 1), j, k, liquid, PlayerTeam.None);
                                            if (liquid == BlockType.Fire)
                                            {
                                                if (blockListContent[i, j, k, 0] > 5)
                                                    blockListContent[i - 1, j, k, 0] = 1;
                                            }
                                        }
                                        if (k > 0 && blockList[i, j, k - 1] == BlockType.None)
                                        {
                                            SetBlock(i, j, (ushort)(k - 1), liquid, PlayerTeam.None);
                                            if (liquid == BlockType.Fire)
                                            {
                                                if (blockListContent[i, j, k, 0] > 5)
                                                    blockListContent[i, j, k - 1, 0] = 1;
                                            }
                                        }
                                        if ((int)i < MAPSIZE - 1 && blockList[i + 1, j, k] == BlockType.None)
                                        {
                                            SetBlock((ushort)(i + 1), j, k, liquid, PlayerTeam.None);
                                            if (liquid == BlockType.Fire)
                                            {
                                                if (blockListContent[i, j, k, 0] > 5)
                                                    blockListContent[i + 1, j, k, 0] = 1;
                                            }
                                        }
                                        if ((int)k < MAPSIZE - 1 && blockList[i, j, k + 1] == BlockType.None)
                                        {
                                            SetBlock(i, j, (ushort)(k + 1), liquid, PlayerTeam.None);
                                            if (liquid == BlockType.Fire)
                                            {
                                                if (blockListContent[i, j, k, 0] > 5)
                                                    blockListContent[i, j, k + 1, 0] = 1;
                                            }
                                        }
                                    }

                                    if (liquid == BlockType.Fire && blockListContent[i, j, k, 0] > 0)//flame explosion
                                    {
                                        blockListContent[i, j, k, 0] = blockListContent[i, j, k, 0] - 1;
                                        if ((int)j < MAPSIZE - 1 && blockList[i, j + 1, k] == BlockType.None)
                                        {
                                            SetBlock(i, (ushort)(j + 1), k, liquid, PlayerTeam.None);
                                            blockListContent[i, j + 1, k, 0] = blockListContent[i, j, k, 0] - 1;//strength decreases every upblock
                                        }
                                    }
                                    else if (liquid == BlockType.Fire)
                                    {
                                        SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                    }
                                }

                                //check for conflicting lava//may need to check bounds
                                if (opposing != BlockType.None)
                                {
                                    BlockType transform = BlockType.Rock;

                                    if (i > 0 && blockList[i - 1, j, k] == opposing)
                                    {
                                        SetBlock((ushort)(i - 1), j, k, transform, PlayerTeam.None);
                                        //steam
                                    }
                                    if ((int)i < MAPSIZE - 1 && blockList[i + 1, j, k] == opposing)
                                    {
                                        SetBlock((ushort)(i + 1), j, k, transform, PlayerTeam.None);
                                    }
                                    if (j > 0 && blockList[i, j - 1, k] == opposing)
                                    {
                                        SetBlock(i, (ushort)(j - 1), k, transform, PlayerTeam.None);
                                    }
                                    if (j < MAPSIZE - 1 && blockList[i, (ushort)(j + 1), k] == opposing)
                                    {
                                        SetBlock(i, (ushort)(j + 1), k, transform, PlayerTeam.None);
                                    }
                                    if (k > 0 && blockList[i, j, k - 1] == opposing)
                                    {
                                        SetBlock(i, j, (ushort)(k - 1), transform, PlayerTeam.None);
                                    }
                                    if (k < MAPSIZE - 1 && blockList[i, j, k + 1] == opposing)
                                    {
                                        SetBlock(i, j, (ushort)(k + 1), transform, PlayerTeam.None);
                                    }

                                    if (liquid == BlockType.Water)//make mud
                                    {
                                        if (typeBelow == BlockType.Dirt || typeBelow == BlockType.Grass)
                                        {

                                            SetBlock(i, (ushort)(j - 1), k, BlockType.Mud, PlayerTeam.None);
                                            blockListContent[i, j - 1, k, 0] = 120;//two minutes @ 250ms 
                                            blockListContent[i, j - 1, k, 1] = (byte)BlockType.Dirt;//becomes this
                                        }
                                    }
                                }//actual water/liquid calculations
                                if (typeBelow != BlockType.None && typeBelow != liquid)//none//trying radius fill
                                {
                                    for (ushort a = (ushort)(i - 1); a < i + 2; a++)
                                    {
                                        for (ushort b = (ushort)(k - 1); b < k + 2; b++)
                                        {
                                            if (a == (ushort)(i - 1) && b == (ushort)(k - 1))
                                            {
                                                continue;
                                            }
                                            else if (a == i + 1 && b == k + 1)
                                            {
                                                continue;
                                            }
                                            else if (a == i - 1 && b == k + 1)
                                            {
                                                continue;
                                            }
                                            else if (a == i + 1 && b == (ushort)(k - 1))
                                            {
                                                continue;
                                            }

                                            if (blockList[i, j, k] != BlockType.None)//has our water block moved on us?
                                            {
                                                //water slides if standing on an edge
                                                if (a > 0 && b > 0 && a < MAPSIZE && b < MAPSIZE && j - 1 > 0)
                                                    if (blockList[a, j - 1, b] == BlockType.None)
                                                    {
                                                        SetBlock(a, (ushort)(j - 1), b, liquid, PlayerTeam.None);
                                                        blockListContent[a, j - 1, b, 1] = blockListContent[i, j, k, 1];
                                                        SetBlockDebris(i, j, k, BlockType.None, PlayerTeam.None);
                                                        a = 3;
                                                        break;
                                                    }
                                            }
                                        }
                                    }
                                }
                                else if (typeBelow == liquid || typeBelow == BlockType.None)
                                {
                                    ushort maxradius = 1;//1

                                    while (maxradius < 25)//need to exclude old checks and require a* pathing check to source
                                    {
                                        for (ushort a = (ushort)(-maxradius + i); a <= maxradius + i; a++)
                                        {
                                            for (ushort b = (ushort)(-maxradius + k); b <= maxradius + k; b++)
                                            {
                                                if (a > 0 && b > 0 && a < MAPSIZE && b < MAPSIZE && j - 1 > 0)
                                                    if (blockList[a, j - 1, b] == BlockType.None)
                                                    {
                                                        if (blockTrace(a, (ushort)(j - 1), b, i, (ushort)(j - 1), k, liquid))//needs to be a pathfind
                                                        {

                                                            if (blockListContent[i, j, k, 0] > 0 && liquid == BlockType.Lava)//volcano
                                                            {
                                                                SetBlock(a, (ushort)(j - 1), b, liquid, PlayerTeam.None);
                                                                blockListContent[a, j - 1, b, 1] = 240 + blockListContent[i, j, k, 0] * 4 + randGenB.Next(1, 20);//core stream
                                                            }
                                                            else if (blockListContent[i, j, k, 1] > 0)
                                                            {
                                                                SetBlock(a, (ushort)(j - 1), b, liquid, PlayerTeam.None);
                                                                blockListContent[a, j - 1, b, 1] = 240 + randGenB.Next(1, 20);// blockListContent[i, j, k, 0] * 20;
                                                                SetBlockDebris(i, j, k, BlockType.None, PlayerTeam.None);//using vacuum blocks temporary refill
                                                            }
                                                            else
                                                            {
                                                                SetBlock(a, (ushort)(j - 1), b, liquid, PlayerTeam.None);
                                                                SetBlockDebris(i, j, k, BlockType.None, PlayerTeam.None);//using vacuum blocks temporary refill
                                                            }
                                                            maxradius = 30;
                                                            a = 65;
                                                            b = 65;
                                                        }
                                                    }
                                            }

                                        }
                                        maxradius += 1;//prevent water spreading too large, this is mainly to stop loop size getting too large
                                    }
                                    if (maxradius != 30)//block could not find a new home
                                    {
                                        flowSleep[i, j, k] = true;
                                        continue;//skip the surround check
                                    }
                                }
                                //extra checks for sleep
                                uint surround = 0;
                                if (blockList[i, j, k] == liquid)
                                {
                                    for (ushort a = (ushort)(-1 + i); a <= 1 + i; a++)
                                    {
                                        for (ushort b = (ushort)(-1 + j); b <= 1 + j; b++)
                                        {
                                            for (ushort c = (ushort)(-1 + k); c <= 1 + k; c++)
                                            {
                                                if (a > 0 && b > 0 && c > 0 && a < MAPSIZE && b < MAPSIZE && c < MAPSIZE)
                                                {
                                                    if (blockList[a, b, c] != BlockType.None)
                                                    {
                                                        surround += 1;//block is surrounded by types it cant move through
                                                    }
                                                }
                                                else//surrounded by edge of map
                                                {
                                                    surround += 1;
                                                }
                                            }
                                        }
                                    }
                                    if (surround >= 27)
                                    {
                                        flowSleep[i, j, k] = true;
                                    }
                                }
                            }

                            else if (blockList[i, j, k] == BlockType.Pump && blockListContent[i, j, k, 0] > 0)// content0 = determines if on
                            {//dopumpstuff
                                BlockType pumpheld = BlockType.None;

                                if (i + blockListContent[i, j, k, 2] < MAPSIZE && j + blockListContent[i, j, k, 3] < MAPSIZE && k + blockListContent[i, j, k, 4] < MAPSIZE && i + blockListContent[i, j, k, 2] > 0 && j + blockListContent[i, j, k, 3] > 0 && k + blockListContent[i, j, k, 4] > 0)
                                {
                                    if (blockList[i + blockListContent[i, j, k, 2], j + blockListContent[i, j, k, 3], k + blockListContent[i, j, k, 4]] == BlockType.Water)
                                    {
                                        pumpheld = BlockType.Water;
                                        SetBlock((ushort)(i + blockListContent[i, j, k, 2]), (ushort)(j + blockListContent[i, j, k, 3]), (ushort)(k + blockListContent[i, j, k, 4]), BlockType.None, PlayerTeam.None);
                                    }
                                    if (blockList[i + blockListContent[i, j, k, 2], j + blockListContent[i, j, k, 3], k + blockListContent[i, j, k, 4]] == BlockType.Lava)
                                    {
                                        pumpheld = BlockType.Lava;
                                        SetBlock((ushort)(i + blockListContent[i, j, k, 2]), (ushort)(j + blockListContent[i, j, k, 3]), (ushort)(k + blockListContent[i, j, k, 4]), BlockType.None, PlayerTeam.None);
                                    }

                                    if (pumpheld != BlockType.None)
                                    {
                                        if (i + blockListContent[i, j, k, 5] < MAPSIZE && j + blockListContent[i, j, k, 6] < MAPSIZE && k + blockListContent[i, j, k, 7] < MAPSIZE && i + blockListContent[i, j, k, 5] > 0 && j + blockListContent[i, j, k, 6] > 0 && k + blockListContent[i, j, k, 7] > 0)
                                        {
                                            if (blockList[i + blockListContent[i, j, k, 5], j + blockListContent[i, j, k, 6], k + blockListContent[i, j, k, 7]] == BlockType.None)
                                            {//check bounds
                                                SetBlock((ushort)(i + blockListContent[i, j, k, 5]), (ushort)(j + blockListContent[i, j, k, 6]), (ushort)(k + blockListContent[i, j, k, 7]), pumpheld, PlayerTeam.None);//places its contents in desired direction
                                            }
                                            else if (blockList[i + blockListContent[i, j, k, 5], j + blockListContent[i, j, k, 6], k + blockListContent[i, j, k, 7]] == pumpheld)//exit must be clear or same substance
                                            {
                                                for (ushort m = 2; m < 10; m++)//multiply exit area to fake upward/sideward motion
                                                {
                                                    if (i + blockListContent[i, j, k, 5] * m < MAPSIZE && j + blockListContent[i, j, k, 6] * m < MAPSIZE && k + blockListContent[i, j, k, 7] * m < MAPSIZE && i + blockListContent[i, j, k, 5] * m > 0 && j + blockListContent[i, j, k, 6] * m > 0 && k + blockListContent[i, j, k, 7] * m > 0)
                                                    {
                                                        if (blockList[i + blockListContent[i, j, k, 5] * m, j + blockListContent[i, j, k, 6] * m, k + blockListContent[i, j, k, 7] * m] == BlockType.None)
                                                        {
                                                            SetBlock((ushort)(i + blockListContent[i, j, k, 5] * m), (ushort)(j + blockListContent[i, j, k, 6] * m), (ushort)(k + blockListContent[i, j, k, 7] * m), pumpheld, PlayerTeam.None);//places its contents in desired direction at a distance
                                                            break;//done with this pump
                                                        }
                                                        else// if (blockList[i + blockListContent[i, j, k, 5] * m, j + blockListContent[i, j, k, 6] * m, k + blockListContent[i, j, k, 7] * m] != pumpheld)//check that we're not going through walls to pump this
                                                        {
                                                            break;//pipe has run aground .. and dont refund the intake
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.None) // Gas
                            {
                                if (blockListContent[i, j, k, 11] == 1)//poison
                                {
                                    if (blockListContent[i, j, k, 12] > 29)//psi
                                    {
                                        int fl = blockListContent[i, j, k, 12];
                                        //int fa = 0;
                                        //int fb = 0;
                                        //int fc = 0;

                                        for (int a = i - 1; a < 2 + i; a++)
                                            for (int b = j - 1; b < 2 + j; b++)
                                                for (int c = k - 1; c < 2 + k; c++)
                                                {
                                                    if (a > 0 && b > 0 && c > 0 && a < MAPSIZE && b < MAPSIZE && c < MAPSIZE)
                                                    {
                                                        if (blockList[a, b, c] == BlockType.None)
                                                            if (blockListContent[a, b, c, 12] < fl && (blockListContent[a, b, c, 11] == 0 || blockListContent[a, b, c, 11] == 1))
                                                            {
                                                                blockListContent[a, b, c, 11] = blockListContent[i, j, k, 11];
                                                                blockListContent[a, b, c, 12] += 10;
                                                               // blockListContent[i, j, k, 12] -= 10;
                                                               // fl -= 10;
                                                                //DebrisEffectAtPoint(a, b, c, BlockType.Highlight, 1);
                                                            }
                                                            //if (blockListContent[a, b, c, 12] < fl)
                                                            //{
                                                            //    fl = blockListContent[a, b, c, 12];
                                                            //    fa = a;
                                                            //    fb = b;
                                                            //    fc = c;
                                                            //}

                                                            //if (blockListContent[a, b, c, 12] < fl && (blockListContent[a, b, c, 11] == 0 || blockListContent[a, b, c, 11] == 1))
                                                            //{
                                                            //    //ConsoleWrite("" + blockListContent[a, b, c, 12]);
                                                            //    if (blockListContent[a, b, c, 12] == 0)
                                                            //    {
                                                            //        blockListContent[a, b, c, 11] = blockListContent[i, j, k, 11];
                                                            //        blockListContent[a, b, c, 12] += 10;
                                                            //        blockListContent[i, j, k, 12] -= 10;
                                                            //        DebrisEffectAtPoint(a, b, c, BlockType.Metal, 1);
                                                            //        a = i + 6;
                                                            //        b = j + 6;
                                                            //        c = k + 6;
                                                            //        fl = 1001;
                                                            //    }
                                                            //    else
                                                            //    {
                                                            //        fl = blockListContent[a, b, c, 12];
                                                            //        fa = a;
                                                            //        fb = b;
                                                            //        fc = c;
                                                            //    }

                                                           // }

                                                    }
                                                }

                                        //if (fl < blockListContent[i, j, k, 12])
                                        //{
                                        //    blockListContent[fa, fb, fc, 11] = blockListContent[i, j, k, 11];
                                        //    blockListContent[fa, fb, fc, 12] += 10;
                                        //    blockListContent[i, j, k, 12] -= 10;
                                        //    DebrisEffectAtPoint(fa, fb, fc, BlockType.Highlight, 1);
                                        //}
                                    }
                                    else//evap
                                    {
                                        if (blockListContent[i, j, k, 12] > 0)
                                        blockListContent[i, j, k, 12] -= 1;

                                        if (blockListContent[i, j, k, 12] < 1)
                                        {
                                            blockListContent[i, j, k, 11] = 0;
                                            blockListContent[i, j, k, 12] = 0;
                                        }
                                    }
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.Pipe) // Do pipe stuff
                            {
                                // Check if pipe connected to a source

                                int PipesConnected = 0;
                                int BlockIsSource = 0;
                                BlockType PipeSourceLiquid = BlockType.None;

                                for (ushort a = (ushort)(-1 + i); a < 2 + i; a++)
                                {
                                    for (ushort b = (ushort)(-1 + j); b < 2 + j; b++)
                                    {
                                        for (ushort c = (ushort)(-1 + k); c < 2 + k; c++)
                                        {
                                            if (a > 0 && b > 0 && c > 0 && a < MAPSIZE && b < MAPSIZE && c < MAPSIZE)
                                            {
                                                if (a == i && b == j && c == k)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    if (blockList[a, b, c] == BlockType.Water || blockList[a, b, c] == BlockType.Lava)//we are either the dst or src
                                                    {
                                                        //PipeSourceLiquid = blockList[a, b, c];
                                                        //blockListContent[i, j, k, 1] = 1; // Set as connected
                                                        //ChainConnectedToSource = 1;
                                                        if (blockListContent[i, j, k, 4] != 1 && blockListContent[i, j, k, 3] == 1)//too early to have full connection count here
                                                        {
                                                            BlockIsSource = 1;
                                                            //blockListContent[i, j, k, 2] = 1;// Set as a source pipe

                                                            //blockListContent[i, j, k, 5] = i;
                                                            //blockListContent[i, j, k, 6] = j;
                                                            //blockListContent[i, j, k, 7] = k;//src happens to know itself to spread the love
                                                            //SetBlock(a, b, c, BlockType.None, PlayerTeam.None);
                                                            //blockListContent[i, j, k, 9] = (byte)(blockList[a, b, c]);
                                                            //blockListContent[i, j, k, 8] += 1;//liquidin
                                                            // blockListContent[i, j, k, 8] = 0;//pipe starts with no liquid
                                                        }
                                                    }

                                                    if (blockList[a, b, c] == BlockType.Pipe)//Found a pipe surrounding this pipe
                                                    {
                                                        if ((a == (ushort)(i + 1) || a == (ushort)(i - 1) || a == (ushort)(i)) && b != j && c != k)
                                                        {
                                                            continue;
                                                        }
                                                        else if (a != i && (b == (ushort)(j + 1) || b == (ushort)(j - 1) || b == (ushort)(j)) && c != k)
                                                        {
                                                            continue;
                                                        }
                                                        else if (a != i && b != j && (c == (ushort)(k + 1) || c == (ushort)(k - 1) || c == (ushort)(k)))
                                                        {
                                                            continue;
                                                        }
                                                        if (blockList[a, b, c] == BlockType.Pipe)//Found a pipe surrounding this pipe
                                                        {
                                                            if (blockListContent[a, b, c, 1] == 1 && (a == i || b == j || c == k))//Check if other pipe connected to a source
                                                            {
                                                                //ChainConnectedToSource = 1;
                                                                blockListContent[i, j, k, 1] = 1; //set as connected chain connected to source
                                                            }
                                                            if (blockListContent[a, b, c, 5] > 0)// && blockListContent[i, j, k, 5] == 0)//this pipe knows the source! hook us up man.
                                                            {
                                                                blockListContent[i, j, k, 5] = blockListContent[a, b, c, 5];//record src 
                                                                blockListContent[i, j, k, 6] = blockListContent[a, b, c, 6];
                                                                blockListContent[i, j, k, 7] = blockListContent[a, b, c, 7];
                                                                // ConsoleWrite("i" + i + "j" + j + "k" + k + " got src: " + blockListContent[a, b, c, 5] + "/" + blockListContent[a, b, c, 6] + "/" + blockListContent[a, b, c, 7]);
                                                            }
                                                            if (blockListContent[i, j, k, 5] > 0)
                                                            {
                                                                if (blockListContent[blockListContent[i, j, k, 5], blockListContent[i, j, k, 6], blockListContent[i, j, k, 7], 3] != 1)
                                                                {//src no longer valid
                                                                    blockListContent[i, j, k, 5] = 0;
                                                                    //                                                                    ConsoleWrite("src negated");
                                                                }
                                                            }

                                                            PipesConnected += 1;
                                                            blockListContent[i, j, k, 3] = PipesConnected;// Set number of pipes connected to pipe
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                if (BlockIsSource == 1 && blockListContent[i, j, k, 3] == 1)
                                {
                                    blockListContent[i, j, k, 2] = 1;// Set as a source pipe

                                    blockListContent[i, j, k, 5] = i;
                                    blockListContent[i, j, k, 6] = j;
                                    blockListContent[i, j, k, 7] = k;//src happens to know itself to spread the love

                                    for (ushort a2 = (ushort)(-1 + i); a2 < 2 + i; a2++)
                                    {
                                        for (ushort b2 = (ushort)(-1 + j); b2 < 2 + j; b2++)
                                        {
                                            for (ushort c2 = (ushort)(-1 + k); c2 < 2 + k; c2++)
                                            {
                                                if (a2 > 0 && b2 > 0 && c2 > 0 && a2 < MAPSIZE && b2 < MAPSIZE && c2 < MAPSIZE)
                                                {
                                                    if (blockList[a2, b2, c2] == BlockType.Water || blockList[a2, b2, c2] == BlockType.Lava)
                                                    {
                                                        PipeSourceLiquid = blockList[a2, b2, c2];
                                                        blockListContent[i, j, k, 1] = 1;
                                                        blockListContent[i, j, k, 5] = i;
                                                        blockListContent[i, j, k, 6] = j;
                                                        blockListContent[i, j, k, 7] = k;//src happens to know itself to spread the love
                                                        SetBlock(a2, b2, c2, BlockType.None, PlayerTeam.None);
                                                        blockListContent[i, j, k, 9] = (byte)(blockList[a2, b2, c2]);
                                                        blockListContent[i, j, k, 8] += 1;//liquidin
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                if (blockListContent[i, j, k, 3] > 1)
                                {
                                    blockListContent[i, j, k, 2] = 0;// do notSet as a source pipe
                                }

                                if (blockListContent[i, j, k, 1] == 1 && blockListContent[i, j, k, 3] == 1 && blockListContent[i, j, k, 2] == 0)
                                {
                                    blockListContent[i, j, k, 4] = 1; //Set as a Destination Pipe
                                    if (blockListContent[i, j, k, 5] > 0)//do we know where the src is?
                                        if (blockListContent[blockListContent[i, j, k, 5], blockListContent[i, j, k, 6], blockListContent[i, j, k, 7], 2] == 1 && blockList[blockListContent[i, j, k, 5], blockListContent[i, j, k, 6], blockListContent[i, j, k, 7]] == BlockType.Pipe)
                                            for (ushort bob = (ushort)(-1 + i); bob < 2 + i; bob++)
                                            {
                                                for (ushort fat = (ushort)(-1 + k); fat < 2 + k; fat++)
                                                {
                                                    if (blockList[bob, j + 1, fat] == BlockType.None)
                                                    {
                                                        if (blockListContent[blockListContent[i, j, k, 5], blockListContent[i, j, k, 6], blockListContent[i, j, k, 7], 8] > 0)
                                                        {
                                                            //blockList[bob, j + 1, fat] = PipeSourceLiquid;
                                                            SetBlock(bob, (ushort)(j + 1), fat, BlockType.Water, PlayerTeam.None);// (BlockType)(blockListContent[blockListContent[i, j, k, 5], blockListContent[i, j, k, 6], blockListContent[i, j, k, 7], 9]), PlayerTeam.None);
                                                            //                                                    ConsoleWrite("pump attempt");
                                                            blockListContent[blockListContent[i, j, k, 5], blockListContent[i, j, k, 6], blockListContent[i, j, k, 7], 8] -= 1;
                                                        }
                                                    }
                                                }
                                            }
                                }
                                else
                                {
                                    blockListContent[i, j, k, 4] = 0;
                                }



                                /*
                                if (ChainConnectedToSource == 0 && PipeIsSource == 0)
                                {
                                    blockListContent[i, j, k, 1] = 0;
                                    blockListContent[i, j, k, 2] = 0;
                                }
                                if (PipeIsSource == 0)
                                {
                                    blockListContent[i, j, k, 2] = 0;
                                }

                                if (blockListContent[i, j, k, 3] == 1 && blockListContent[i, j, k, 2] == 0 && blockListContent[i, j, k, 1] == 1)// find outputs (not source with 1 pipe only connected)
                                {
                                    //set as dst pipe
                                    blockListContent[i, j, k, 4] = 1;
                                }
                                else
                                {
                                    blockListContent[i, j, k, 4] = 0;
                                }

                                if (blockListContent[i, j, k, 4] == 1)
                                {
                                    if (blockList[i , j + 1, k] == BlockType.None) 
                                    {
                                        blockList[i, j + 1, k] = BlockType.Water;
                                    }

                                }
                                */
                            }
                            else if (blockList[i, j, k] == BlockType.Barrel)
                            {//docompressorstuff

                                if (blockListContent[i, j, k, 0] == 1)
                                {
                                    if (blockListContent[i, j, k, 2] < 20)//not full
                                        for (ushort a = (ushort)(-1 + i); a < 2 + i; a++)
                                        {
                                            for (ushort b = (ushort)(-1 + j); b < 2 + j; b++)
                                            {
                                                for (ushort c = (ushort)(-1 + k); c < 2 + k; c++)
                                                {
                                                    if (blockListContent[i, j, k, 2] < 20)
                                                        if (a > 0 && b > 0 && c > 0 && a < MAPSIZE && b < MAPSIZE && c < MAPSIZE)
                                                        {
                                                            if (blockList[a, b, c] == BlockType.Water || blockList[a, b, c] == BlockType.Lava)
                                                            {
                                                                if (blockListContent[i, j, k, 1] == 0 || blockListContent[i, j, k, 2] == 0)
                                                                {
                                                                    blockListContent[i, j, k, 1] = (byte)blockList[a, b, c];
                                                                    SetBlock((ushort)(a), (ushort)(b), (ushort)(c), BlockType.None, PlayerTeam.None);
                                                                    blockListContent[i, j, k, 2] += 1;
                                                                }
                                                                else if (blockListContent[i, j, k, 1] == (byte)blockList[a, b, c])
                                                                {
                                                                    SetBlock((ushort)(a), (ushort)(b), (ushort)(c), BlockType.None, PlayerTeam.None);
                                                                    blockListContent[i, j, k, 2] += 1;
                                                                }
                                                            }

                                                        }

                                                }
                                            }
                                        }
                                }
                                else//venting
                                {
                                    if (blockListContent[i, j, k, 1] > 0)//has type
                                    {
                                        if (blockListContent[i, j, k, 2] > 0)//has content
                                        {
                                            if (blockList[i, j + 1, k] == BlockType.None)
                                            {
                                                SetBlock(i, (ushort)(j + 1), k, (BlockType)(blockListContent[i, j, k, 1]), PlayerTeam.None);//places its contents in desired direction at a distance
                                                blockListContent[i, (ushort)(j + 1), k, 1] = 120;
                                                blockListContent[i, j, k, 2] -= 1;
                                                continue;
                                            }
                                            else if (blockList[i, j + 1, k] == (BlockType)(blockListContent[i, j, k, 1]))//exit must be clear or same substance
                                            {
                                                blockListContent[i, j + 1, k, 1] = 120;//refresh temperature
                                                for (ushort m = 2; m < 10; m++)//multiply exit area to fake upward motion
                                                {
                                                    if (j + m < MAPSIZE)
                                                    {
                                                        if (blockList[i, j + m, k] == BlockType.None)
                                                        {
                                                            SetBlock(i, (ushort)(j + m), k, (BlockType)(blockListContent[i, j, k, 1]), PlayerTeam.None);//places its contents in desired direction at a distance
                                                            blockListContent[i, (ushort)(j + m), k, 1] = 120;
                                                            blockListContent[i, j, k, 2] -= 1;
                                                            break;//done with this pump
                                                        }
                                                        else if (blockList[i, j + m, k] != (BlockType)(blockListContent[i, j, k, 1]))// if (blockList[i + blockListContent[i, j, k, 5] * m, j + blockListContent[i, j, k, 6] * m, k + blockListContent[i, j, k, 7] * m] != pumpheld)//check that we're not going through walls to pump this
                                                        {
                                                            break;//pipe has run aground .. and dont refund the intake
                                                        }
                                                        else//must be the liquid in the way, refresh its temperature
                                                        {
                                                            blockListContent[i, j + m, k, 1] = 120;
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                    }
                                    else//had type in contents but no content
                                    {
                                        blockListContent[i, j, k, 1] = 0;
                                    }
                                }

                                if(blockListContent[i, j, k, 10] == 0)
                                if (j + 1 < MAPSIZE && j - 1 > 0 && i - 1 > 0 && i + 1 < MAPSIZE && k - 1 > 0 && k + 1 < MAPSIZE)
                                    if (blockList[i, j - 1, k] == BlockType.None)
                                    {//no block above or below, so fall
                                        blockListContent[i, j, k, 10] = frameid;
                                        blockListContent[i, j, k, 11] = 0;
                                        blockListContent[i, j, k, 12] = -100;
                                        blockListContent[i, j, k, 13] = 0;
                                        blockListContent[i, j, k, 14] = i * 100;
                                        blockListContent[i, j, k, 15] = j * 100;
                                        blockListContent[i, j, k, 16] = k * 100;
                                        blockListContent[i, j, k, 0] = 0;//empty
                                        continue;
                                    }
                            }
                            else if (blockList[i, j, k] == BlockType.Spring)
                            {//dospringstuff
                                if (blockList[i, j - 1, k] == BlockType.None)
                                {
                                    SetBlock(i, (ushort)(j - 1), k, BlockType.Water, PlayerTeam.None);
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.MagmaVent)
                            {//dospringstuff
                                if (blockList[i, j - 1, k] == BlockType.None)
                                {
                                    SetBlock(i, (ushort)(j - 1), k, BlockType.Lava, PlayerTeam.None);
                                    blockListContent[i, (ushort)(j - 1), k, 1] = 0;
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.MagmaBurst)
                            {//dospringstuff
                                if (blockListContent[i, j, k, 0] < 10 && blockListContent[i, j, k, 1] > 0)
                                {
                                    blockListContent[i, j, k, 0]++;

                                    if (j > 0)
                                        for (ushort m = 1; m < 10; m++)//multiply exit area
                                        {
                                            if (j - m > 0)
                                            {
                                                if (blockList[i, j - m, k] == BlockType.None)
                                                {
                                                    SetBlock(i, (ushort)(j - m), k, BlockType.Lava, PlayerTeam.None);//places its contents in desired direction at a distance
                                                    blockListContent[i, j - m, k, 1] = 40;
                                                    break;
                                                }
                                                else if (blockList[i, j - m, k] == BlockType.Lava)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }

                                    if (j < MAPSIZE - 1)
                                        for (ushort m = 1; m < 10; m++)//multiply exit area
                                        {
                                            if (j + m < MAPSIZE - 1)
                                            {
                                                if (blockList[i, j + m, k] == BlockType.None)
                                                {
                                                    SetBlock(i, (ushort)(j + m), k, BlockType.Lava, PlayerTeam.None);//places its contents in desired direction at a distance
                                                    blockListContent[i, j + m, k, 1] = 40;
                                                    break;
                                                }
                                                else if (blockList[i, j + m, k] == BlockType.Lava)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }

                                        }

                                    if (i > 0)
                                        for (ushort m = 1; m < 10; m++)//multiply exit area
                                        {
                                            if (i - m > 0)
                                            {
                                                if (blockList[i - m, j, k] == BlockType.None)
                                                {
                                                    SetBlock((ushort)(i - m), j, k, BlockType.Lava, PlayerTeam.None);//places its contents in desired direction at a distance
                                                    blockListContent[i - m, j, k, 1] = 40;
                                                    break;
                                                }
                                                else if (blockList[i - m, j, k] == BlockType.Lava)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }

                                        }

                                    if (i < MAPSIZE - 1)
                                        for (ushort m = 1; m < 10; m++)//multiply exit area
                                        {
                                            if (i + m < MAPSIZE - 1)
                                            {
                                                if (blockList[i + m, j, k] == BlockType.None)
                                                {
                                                    SetBlock((ushort)(i + m), j, k, BlockType.Lava, PlayerTeam.None);//places its contents in desired direction at a distance
                                                    blockListContent[i + m, j, k, 1] = 40;
                                                    break;
                                                }
                                                else if (blockList[i + m, j, k] == BlockType.Lava)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }

                                        }

                                    if (k > 0)
                                        for (ushort m = 1; m < 10; m++)//multiply exit area
                                        {
                                            if (k - m > 0)
                                            {
                                                if (blockList[i, j, k - m] == BlockType.None)
                                                {
                                                    SetBlock(i, j, (ushort)(k - m), BlockType.Lava, PlayerTeam.None);//places its contents in desired direction at a distance
                                                    blockListContent[i, j, k - m, 1] = 40;
                                                    break;
                                                }
                                                else if (blockList[i, j, k - m] == BlockType.Lava)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }

                                    if (k < MAPSIZE - 1)
                                        for (ushort m = 1; m < 10; m++)//multiply exit area
                                        {
                                            if (k + m < MAPSIZE - 1)
                                            {
                                                if (blockList[i, j, k + m] == BlockType.None)
                                                {
                                                    SetBlock(i, j, (ushort)(k + m), BlockType.Lava, PlayerTeam.None);//places its contents in desired direction at a distance
                                                    blockListContent[i, j, k + m, 1] = 40;
                                                    break;
                                                }
                                                else if (blockList[i, j, k + m] == BlockType.Lava)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }

                                        }
                                }
                                else if (blockListContent[i, j, k, 1] < 1)//priming time / 400ms
                                {
                                    if (j > 0)
                                        if (blockList[i, j - 1, k] == BlockType.None)
                                        {
                                            blockListContent[i, j, k, 1]++;
                                        }

                                    if (j < MAPSIZE - 1)
                                        if (blockList[i, j + 1, k] == BlockType.None)
                                        {
                                            blockListContent[i, j, k, 1]++;
                                        }

                                    if (i > 0)
                                        if (blockList[i - 1, j, k] == BlockType.None)
                                        {
                                            blockListContent[i, j, k, 1]++;
                                        }

                                    if (i < MAPSIZE - 1)
                                        if (blockList[i + 1, j, k] == BlockType.None)
                                        {
                                            blockListContent[i, j, k, 1]++;
                                        }

                                    if (k > 0)
                                        if (blockList[i, j, k - 1] == BlockType.None)
                                        {
                                            blockListContent[i, j, k, 1]++;
                                        }

                                    if (k < MAPSIZE - 1)
                                        if (blockList[i, j, k + 1] == BlockType.None)
                                        {
                                            blockListContent[i, j, k, 1]++;
                                        }

                                    if (blockListContent[i, j, k, 1] == 0)
                                    {
                                        //talk a walk around the map
                                        if (randGenB.Next(1000) == 1 && sleeping == false)//500+
                                        {
                                            int x = i + randGenB.Next(2) - 1;
                                            int y = j + randGenB.Next(2) - 1;
                                            int z = k + randGenB.Next(2) - 1;

                                            if (x < 1 || y < 1 || z < 1 || x > MAPSIZE - 2 || y > MAPSIZE - 2 || z > MAPSIZE - 2)
                                            {
                                            }
                                            else
                                            {
                                                if (blockList[x, y, z] == BlockType.Dirt)
                                                    if (blockList[x - 1, y, z] != BlockType.None)
                                                        if (blockList[x + 1, y, z] != BlockType.None)
                                                            if (blockList[x, y - 1, z] != BlockType.None)
                                                                if (blockList[x, y + 1, z] != BlockType.None)
                                                                    if (blockList[x, y, z - 1] != BlockType.None)
                                                                        if (blockList[x, y, z + 1] != BlockType.None)
                                                                        {
                                                                            //   ConsoleWrite("magmaburst moved from " + i + "/" + j + "/" + k + " to " + x + "/" + y + "/" + z);
                                                                            SetBlock((ushort)i, (ushort)j, (ushort)k, BlockType.Dirt, PlayerTeam.None);
                                                                            SetBlock((ushort)x, (ushort)y, (ushort)z, BlockType.MagmaBurst, PlayerTeam.None);
                                                                        }
                                            }
                                        }
                                    }
                                }
                                else//run out of magma->turn into rock (gold became too frequent)
                                {
                                    SetBlock(i, j, k, BlockType.Rock, PlayerTeam.None);
                                    blockListHP[i, j, k] = BlockInformation.GetHP(BlockType.Rock);
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.Mud)//mud dries out
                            {
                                if(j - 1 > 0)
                                if (blockList[i, j - 1, k] != BlockType.Water)
                                    if (blockListContent[i, j, k, 0] == 2)
                                    {
                                        blockListContent[i, j, k, 0] = 0;
                                        SetBlock(i, j, k, (BlockType)blockListContent[i, j, k, 1], PlayerTeam.None);
                                        blockListHP[i, j, k] = BlockInformation.GetHP((BlockType)blockListContent[i, j, k, 1]);
                                    }
                                    else if (blockListContent[i, j, k, 0] > 1)
                                    {
                                        blockListContent[i, j, k, 0] -= 1;
                                    }
                            }
                            else if (blockList[i, j, k] == BlockType.Sand)//sand falls straight down and moves over edges
                            {
                                if (j - 1 > 0)
                                {
                                    if (blockList[i, j - 1, k] == BlockType.None && blockListContent[i, j, k, 10] == 0)
                                    {
                                        blockListContent[i, j, k, 10] = frameid;
                                        blockListContent[i, j, k, 11] = 0;
                                        blockListContent[i, j, k, 12] = -100;
                                        blockListContent[i, j, k, 13] = 0;
                                        blockListContent[i, j, k, 14] = i * 100;
                                        blockListContent[i, j, k, 15] = j * 100;
                                        blockListContent[i, j, k, 16] = k * 100;
                                        //SetBlock(i, (ushort)(j - 1), k, BlockType.Sand, PlayerTeam.None);
                                        //SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                        continue;
                                    }

                                    if (j - 2 > 0)
                                        if (blockList[i, j - 1, k] == BlockType.Sand && blockListContent[i, j, k, 10] == 0)
                                            for (ushort m = 1; m < 2; m++)//how many squares to fall over
                                            {
                                                if (i + m < MAPSIZE)
                                                    if (blockList[i + m, j - 1, k] == BlockType.None)
                                                    {
                                                        SetBlock((ushort)(i + m), (ushort)(j - 1), k, BlockType.Sand, PlayerTeam.None);
                                                        SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                                        continue;
                                                    }
                                                if (i - m > 0)
                                                    if (blockList[i - m, j - 1, k] == BlockType.None)
                                                    {
                                                        SetBlock((ushort)(i - m), (ushort)(j - 1), k, BlockType.Sand, PlayerTeam.None);
                                                        SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                                        continue;
                                                    }
                                                if (k + m < MAPSIZE)
                                                    if (blockList[i, j - 1, k + m] == BlockType.None)
                                                    {
                                                        SetBlock(i, (ushort)(j - 1), (ushort)(k + m), BlockType.Sand, PlayerTeam.None);
                                                        SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                                        continue;
                                                    }
                                                if (k - m > 0)
                                                    if (blockList[i, j - 1, k - m] == BlockType.None)
                                                    {
                                                        SetBlock(i, (ushort)(j - 1), (ushort)(k - m), BlockType.Sand, PlayerTeam.None);
                                                        SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                                        continue;
                                                    }
                                            }
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.Dirt || blockList[i, j, k] == BlockType.Grass)//loose dirt falls straight down / topmost dirt grows
                            {
                                if (j + 1 < MAPSIZE && j - 1 > 0 && i - 1 > 0 && i + 1 < MAPSIZE && k - 1 > 0 && k + 1 < MAPSIZE)
                                {
                                    if (blockListContent[i, j, k, 0] > 0)
                                    {
                                        if (randGenB.Next(1, 3) == 2)
                                            blockListContent[i, j, k, 0]--;
                                        //greenery
                                        if (blockListContent[i, j, k, 0] > 150 && blockList[i, j, k] == BlockType.Dirt)
                                        {
                                            SetBlock(i, j, k, BlockType.Grass, PlayerTeam.None);
                                            blockListContent[i, j, k, 0] = 150;
                                            // ConsoleWrite("change to grass");
                                        }
                                        else if (blockListContent[i, j, k, 0] == 0 && blockList[i, j, k] == BlockType.Grass)
                                        {
                                            // ConsoleWrite("change to dirt");
                                            SetBlock(i, j, k, BlockType.Dirt, PlayerTeam.None);
                                        }
                                    }

                                    if (blockListContent[i, j, k, 10] == 0)
                                        if (blockList[i, j - 1, k] == BlockType.None)
                                            if (blockList[i, j + 1, k] == BlockType.None && blockList[i + 1, j, k] == BlockType.None && blockList[i - 1, j, k] == BlockType.None && blockList[i, j, k + 1] == BlockType.None && blockList[i, j, k - 1] == BlockType.None && blockListContent[i, j, k, 10] == 0)
                                            {//no block above or below, so fall
                                                blockListContent[i, j, k, 10] = frameid;
                                                blockListContent[i, j, k, 11] = 0;
                                                blockListContent[i, j, k, 12] = -100;
                                                blockListContent[i, j, k, 13] = 0;
                                                blockListContent[i, j, k, 14] = i * 100;
                                                blockListContent[i, j, k, 15] = j * 100;
                                                blockListContent[i, j, k, 16] = k * 100;
                                                // SetBlock(i, (ushort)(j - 1), k, BlockType.Dirt, PlayerTeam.None);
                                                //SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                                continue;
                                            }
                                }

                            }
                            else if (blockList[i, j, k] == BlockType.Ore || blockList[i, j, k] == BlockType.Rock)//falls straight down
                            {
                                if (blockListContent[i, j, k, 10] == 0)
                                    if (j + 1 < MAPSIZE && j - 1 > 0 && i - 1 > 0 && i + 1 < MAPSIZE && k - 1 > 0 && k + 1 < MAPSIZE)
                                    {
                                        if (blockList[i, j - 1, k] == BlockType.None)
                                            if (blockList[i, j + 1, k] == BlockType.None && blockList[i + 1, j, k] == BlockType.None && blockList[i - 1, j, k] == BlockType.None && blockList[i, j, k + 1] == BlockType.None && blockList[i, j, k - 1] == BlockType.None && blockListContent[i, j, k, 10] == 0)
                                            {//no block above or below, so fall
                                                blockListContent[i, j, k, 10] = frameid;
                                                blockListContent[i, j, k, 11] = 0;
                                                blockListContent[i, j, k, 12] = -100;
                                                blockListContent[i, j, k, 13] = 0;
                                                blockListContent[i, j, k, 14] = i * 100;
                                                blockListContent[i, j, k, 15] = j * 100;
                                                blockListContent[i, j, k, 16] = k * 100;
                                                // SetBlock(i, (ushort)(j - 1), k, BlockType.Dirt, PlayerTeam.None);
                                                //SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                                continue;
                                            }
                                    }

                            }
                            else if (blockList[i, j, k] == BlockType.Explosive && blockCreatorTeam[i, j, k] != PlayerTeam.None)
                            {
                                if(!SiegeBuild)
                                if (blockListContent[i, j, k, 1] > 1)//tnt fuse
                                {
                                    blockListContent[i, j, k, 1]--;
                                    PlaySound(InfiniminerSound.RadarSwitch, new Vector3(i, j, k));
                                }
                                else if (blockListContent[i, j, k, 1] == 1)//tnt explode
                                {
                                    blockListContent[i, j, k, 1] = 0;
                                    if(varGetB("tnt"))
                                    DetonateAtPoint(i, j, k, blockCreatorTeam[(ushort)(i), (ushort)(j), (ushort)(k)], true);
                                    continue;
                                }

                                if (blockListContent[i, j, k, 10] == 0)
                                    if (blockList[i, j - 1, k] == BlockType.None)
                                    {//no block below, so fall
                                        blockListContent[i, j, k, 10] = frameid;
                                        blockListContent[i, j, k, 11] = 0;
                                        blockListContent[i, j, k, 12] = -100;
                                        blockListContent[i, j, k, 13] = 0;
                                        blockListContent[i, j, k, 14] = i * 100;
                                        blockListContent[i, j, k, 15] = j * 100;
                                        blockListContent[i, j, k, 16] = k * 100;
                                        // SetBlock(i, (ushort)(j - 1), k, BlockType.Dirt, PlayerTeam.None);
                                        //SetBlock(i, j, k, BlockType.None, PlayerTeam.None);
                                        continue;
                                    }
                            }
                            else if (blockList[i, j, k] == BlockType.Maintenance)
                            {
                                if (blockListContent[i, j, k, 4] > 0)
                                {
                                    blockListContent[i, j, k, 4]--;//timer
                                }
                                else
                                {
                                    if (blockCreatorTeam[i, j, k] == PlayerTeam.Red)
                                    {
                                        if (teamOreRed <= 0)
                                        {
                                            if (OreMessage[(byte)PlayerTeam.Red] == false)
                                            {
                                                SendServerMessageRed("Our maintenance arrays require ore!");
                                                OreMessage[(byte)PlayerTeam.Red] = true;
                                            }
                                            continue;
                                        }
                                    }
                                    else if (blockCreatorTeam[i, j, k] == PlayerTeam.Blue)
                                    {
                                        if (teamOreBlue <= 0)
                                        {
                                            if (OreMessage[(byte)PlayerTeam.Blue] == false)
                                            {
                                                SendServerMessageBlue("Our maintenance arrays require ore!");
                                                OreMessage[(byte)PlayerTeam.Blue] = true;
                                            }
                                            continue;
                                        }
                                    }

                                    blockListContent[i, j, k, 4] = 5;
                                    for (ushort a = (ushort)(-2 + i); a < 3 + i; a++)
                                        for (ushort b = (ushort)(-2 + j); b < 3 + j; b++)
                                            for (ushort c = (ushort)(-2 + k); c < 3 + k; c++)
                                            {
                                                if (a > 0 && b > 0 && c > 0 && a < MAPSIZE && b < MAPSIZE && c < MAPSIZE)
                                                    if (a == i && b == j && c == k)
                                                    {
                                                    }
                                                    else
                                                    {

                                                        if (blockCreatorTeam[i, j, k] == blockCreatorTeam[a, b, c])
                                                            if (blockListHP[a, b, c] < BlockInformation.GetMaxHP(blockList[a, b, c]))
                                                            {
                                                                if (blockCreatorTeam[i, j, k] == PlayerTeam.Red)
                                                                {
                                                                    if (teamOreRed <= 0)
                                                                    {
                                                                        a = (ushort)(3 + i);
                                                                        b = (ushort)(3 + j);
                                                                        c = (ushort)(3 + k);
                                                                        continue;
                                                                    }
                                                                    else
                                                                        teamOreRed--;
                                                                }
                                                                else if (blockCreatorTeam[i, j, k] == PlayerTeam.Blue)
                                                                {
                                                                    if (teamOreBlue <= 0)
                                                                    {
                                                                        a = (ushort)(3 + i);
                                                                        b = (ushort)(3 + j);
                                                                        c = (ushort)(3 + k);
                                                                        continue;
                                                                    }
                                                                    else
                                                                        teamOreBlue--;
                                                                }

                                                                blockListHP[a, b, c] += 4 + ResearchComplete[(byte)blockCreatorTeam[i, j, k], (byte)Research.Fortify];
                                                                //ConsoleWrite("bhp:" + blockListHP[a, b, c] + "/" + blockList[a, b, c]);
                                                                if (blockListHP[a, b, c] >= BlockInformation.GetMaxHP(blockList[a, b, c]))
                                                                {
                                                                    switch (blockList[a, b, c])
                                                                    {
                                                                        case BlockType.SolidBlue:
                                                                            SetBlock(a, b, c, BlockType.SolidBlue2, PlayerTeam.Blue);
                                                                            blockListHP[a, b, c] = BlockInformation.GetMaxHP(BlockType.SolidBlue);
                                                                            break;

                                                                        case BlockType.SolidRed:
                                                                            SetBlock(a, b, c, BlockType.SolidRed2, PlayerTeam.Red);
                                                                            blockListHP[a, b, c] = BlockInformation.GetMaxHP(BlockType.SolidRed);
                                                                            break;

                                                                        default:
                                                                            blockListHP[a, b, c] = BlockInformation.GetMaxHP(blockList[a, b, c]);
                                                                            break;
                                                                    }

                                                                }
                                                            }
                                                    }
                                            }
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.RadarRed)
                            {
                                //blockListContent[i, j, k, 0] += 1;

                                if (scantime == 0)//limit scans
                                {
                                    foreach (Player p in playerList.Values)
                                    {
                                        if (p.Alive && p.Team == PlayerTeam.Blue)
                                            if (Get3DDistance((int)(p.Position.X), (int)(p.Position.Y), (int)(p.Position.Z), i, j, k) < 12)
                                            {
                                                //this player has been detected by the radar
                                                if (p.Content[1] == 0)
                                                {
                                                    if (p.Class == PlayerClass.Prospector && p.Content[5] == 1)
                                                    {//character is hidden
                                                    }
                                                    else
                                                    {
                                                        p.Content[1] = 1;//goes on radar
                                                    }
                                                }
                                            }
                                    }
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.InhibitorR)
                            {
                                blockListContent[i, j, k, 0] += 1;

                                if (blockListContent[i, j, k, 0] == 2)//limit scans
                                {
                                    blockListContent[i, j, k, 0] = 0;
                                    foreach (Player p in playerList.Values)
                                    {
                                        if (p.Alive && p.Team == PlayerTeam.Blue)
                                            if (Get3DDistance((int)(p.Position.X), (int)(p.Position.Y), (int)(p.Position.Z), i, j, k) < INHIBITOR_RANGE)
                                            {
                                                //this player will be inhibited from placing blocks
                                                if (p.StatusEffect[1] == 0)
                                                {
                                                    p.StatusEffect[1] = 1;//may not place blocks
                                                    SendStatusEffectSpecificUpdate(p, 1);
                                                }
                                            }
                                            else//player is out of range//should be checking for other inhibs
                                            {
                                                if (p.StatusEffect[1] == 1)
                                                {
                                                    p.StatusEffect[1] = 0;//may place blocks
                                                    SendStatusEffectSpecificUpdate(p, 1);
                                                }
                                            }
                                    }
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.RadarBlue)
                            {
                                //blockListContent[i, j, k, 0] += 1;

                                if (scantime == 0)//limit scans
                                {
                                    //blockListContent[i, j, k, 0] = 0;
                                    foreach (Player p in playerList.Values)
                                    {
                                        if (p.Alive && p.Team == PlayerTeam.Red)
                                            if (Get3DDistance((int)(p.Position.X), (int)(p.Position.Y), (int)(p.Position.Z), i, j, k) < 12)
                                            {
                                                //this player has been detected by the radar
                                                if (p.Content[1] == 0)
                                                {
                                                    if (p.Class == PlayerClass.Prospector && p.Content[5] == 1)
                                                    {//character is hidden
                                                    }
                                                    else
                                                    {

                                                        p.Content[1] = 1;//goes on radar
                                                    }
                                                }
                                            }
                                            else//player is out of range
                                            {
                                                //if (p.Content[1] == 1)
                                                //{
                                                //    p.Content[1] = 0;//goes off radar again
                                                //    SendPlayerContentUpdate(p, 1);
                                                //}
                                            }
                                    }
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.InhibitorB)
                            {
                                blockListContent[i, j, k, 0] += 1;

                                if (blockListContent[i, j, k, 0] == 2)//limit scans
                                {
                                    blockListContent[i, j, k, 0] = 0;
                                    foreach (Player p in playerList.Values)
                                    {
                                        if (p.Alive && p.Team == PlayerTeam.Red)
                                            if (Get3DDistance((int)(p.Position.X), (int)(p.Position.Y), (int)(p.Position.Z), i, j, k) < INHIBITOR_RANGE)
                                            {
                                                //this player will be inhibited from placing blocks
                                                if (p.StatusEffect[1] == 0)
                                                {
                                                    p.StatusEffect[1] = 1;//may not place blocks
                                                    SendStatusEffectSpecificUpdate(p, 1);
                                                }
                                            }
                                            else//player is out of range//should be checking for other inhibs
                                            {
                                                if (p.StatusEffect[1] == 1)
                                                {
                                                    p.StatusEffect[1] = 0;//may place blocks
                                                    SendStatusEffectSpecificUpdate(p, 1);
                                                }
                                            }
                                    }
                                }
                            }
                            else if (blockListContent[i, j, k, 1] > 0 && (blockList[i, j, k] == BlockType.Plate || blockList[i, j, k] == BlockType.Lever))
                            {
                                if (blockListContent[i, j, k, 6] > 0)
                                    blockListContent[i, j, k, 6]--;

                                if (blockListContent[i, j, k, 1] == 1)
                                {
                                    blockListContent[i, j, k, 1] = 0;
                                    //untrigger the plate/lever
                                    Trigger(i, j, k, i, j, k, 1, null, 1);
                                }
                                else if (blockListContent[i, j, k, 1] > 0)
                                {
                                    blockListContent[i, j, k, 1] -= 1;
                                }

                            }
                            else if (blockList[i, j, k] == BlockType.ResearchR)
                            {
                                if (blockListContent[i, j, k, 1] != ResearchChange[(byte)PlayerTeam.Red])
                                {
                                    if (ResearchChange[(byte)PlayerTeam.Red] != -1 && ResearchChange[(byte)PlayerTeam.Red] != -2)//halted
                                    {
                                        if (ResearchChange[(byte)PlayerTeam.Red] != 0)
                                        {
                                            blockListContent[i, j, k, 1] = ResearchChange[(byte)PlayerTeam.Red];
                                            blockListContent[i, j, k, 0] = 1;
                                        }
                                        else
                                        {
                                            blockListContent[i, j, k, 1] = 0;
                                            blockListContent[i, j, k, 0] = 0;
                                        }
                                    }
                                }

                                if (ResearchChange[(byte)PlayerTeam.Red] == -2 && teamCashRed == 0)
                                {
                                    //  blockListContent[i, j, k, 0] = 0;//stop research
                                    continue;
                                }
                                else if (ResearchChange[(byte)PlayerTeam.Red] == -2)
                                {
                                    //resume
                                    //SendServerMessage("" + (Research)blockListContent[i, j, k, 1]);
                                    ResearchChange[(byte)PlayerTeam.Red] = blockListContent[i, j, k, 1];
                                }

                                if (ResearchChange[(byte)PlayerTeam.Red] != -1)
                                    if (blockListContent[i, j, k, 0] > 0)
                                        if (blockListContent[i, j, k, 1] > 0)
                                        {
                                            if (teamCashRed > 0 && ResearchProgress[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]] > 0 && blockListContent[i, j, k, 4] == 0)
                                            {
                                                ResearchProgress[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]]--;

                                                blockListContent[i, j, k, 4] = 10;//timer
                                                blockListContent[i, j, k, 3] = 0;//message warnings
                                                teamCashRed -= 1;

                                                NetBuffer msgBuffer = netServer.CreateBuffer();
                                                foreach (NetConnection netConn in playerList.Keys)
                                                    if (netConn.Status == NetConnectionStatus.Connected)
                                                        SendTeamCashUpdate(playerList[netConn]);

                                                if (ResearchProgress[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]] == 0)//research complete
                                                {
                                                    ResearchChange[(byte)PlayerTeam.Red] = 0;
                                                    ResearchComplete[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]]++;//increase rank
                                                    ResearchProgress[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]] = ResearchInformation.GetCost((Research)blockListContent[i, j, k, 1]) * ((ResearchComplete[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]] + 1) * 2);
                                                    NetBuffer msgBufferr = netServer.CreateBuffer();
                                                    msgBufferr = netServer.CreateBuffer();
                                                    msgBufferr.Write((byte)InfiniminerMessage.ChatMessage);

                                                    msgBufferr.Write((byte)ChatMessageType.SayRedTeam);
                                                    if (ResearchComplete[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]] == 1)//first rank
                                                        msgBufferr.Write(Defines.Sanitize(ResearchInformation.GetName((Research)blockListContent[i, j, k, 1]) + " research was completed!"));
                                                    else
                                                        msgBufferr.Write(Defines.Sanitize(ResearchInformation.GetName((Research)blockListContent[i, j, k, 1]) + " rank " + ResearchComplete[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]] + " research was completed!"));
                                                    foreach (NetConnection netConn in playerList.Keys)
                                                        if (netConn.Status == NetConnectionStatus.Connected)
                                                            if (playerList[netConn].Team == PlayerTeam.Red)
                                                                netServer.SendMessage(msgBufferr, netConn, NetChannel.ReliableUnordered);

                                                    //recalculate player statistics/buffs
                                                    ResearchRecalculate(PlayerTeam.Red, blockListContent[i, j, k, 1]);
                                                    //
                                                    blockListContent[i, j, k, 1] = 0;
                                                    blockListContent[i, j, k, 0] = 0;
                                                }
                                            }
                                            else if (blockListContent[i, j, k, 4] > 0)
                                            {
                                                blockListContent[i, j, k, 4]--;
                                            }
                                            else
                                            {
                                                if (blockListContent[i, j, k, 3] == 0)
                                                {
                                                    blockListContent[i, j, k, 3] = 1;
                                                    NetBuffer msgBuffer = netServer.CreateBuffer();
                                                    msgBuffer = netServer.CreateBuffer();
                                                    msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                                                    msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                                                    //"Research topic: " + ResearchInformation.GetName((Research)blockListContent[x, y, z, 1]) + "(" + ResearchComplete[(byte)player.Team, blockListContent[x, y, z, 1]] + ") (" + ResearchProgress[(byte)player.Team, blockListContent[x, y, z, 1]] + " gold)
                                                    msgBuffer.Write(Defines.Sanitize(ResearchInformation.GetName((Research)blockListContent[i, j, k, 1]) + " research requires " + ResearchProgress[(byte)PlayerTeam.Red, blockListContent[i, j, k, 1]] + " more gold!"));
                                                    foreach (NetConnection netConn in playerList.Keys)
                                                        if (netConn.Status == NetConnectionStatus.Connected)
                                                            if (playerList[netConn].Team == PlayerTeam.Red)
                                                                netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

                                                    ResearchChange[(byte)PlayerTeam.Red] = -2;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (blockListContent[i, j, k, 3] == 0)
                                            {
                                                blockListContent[i, j, k, 3] = 1;
                                                NetBuffer msgBuffer = netServer.CreateBuffer();
                                                msgBuffer = netServer.CreateBuffer();
                                                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                                                msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                                                msgBuffer.Write(Defines.Sanitize("Research has halted!"));
                                                foreach (NetConnection netConn in playerList.Keys)
                                                    if (netConn.Status == NetConnectionStatus.Connected)
                                                        if (playerList[netConn].Team == PlayerTeam.Red)
                                                            netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

                                            }

                                            //no longer has research topic
                                        }
                            }
                            else if (blockList[i, j, k] == BlockType.ResearchB)
                            {
                                if (blockListContent[i, j, k, 1] != ResearchChange[(byte)PlayerTeam.Blue])
                                {
                                    if (ResearchChange[(byte)PlayerTeam.Blue] != -1 && ResearchChange[(byte)PlayerTeam.Blue] != -2)//halted
                                    {
                                        if (ResearchChange[(byte)PlayerTeam.Blue] != 0)
                                        {
                                            blockListContent[i, j, k, 1] = ResearchChange[(byte)PlayerTeam.Blue];
                                            blockListContent[i, j, k, 0] = 1;
                                        }
                                        else
                                        {
                                            blockListContent[i, j, k, 1] = 0;
                                            blockListContent[i, j, k, 0] = 0;
                                        }
                                    }
                                }

                                if (ResearchChange[(byte)PlayerTeam.Blue] == -2 && teamCashBlue == 0)
                                {
                                    //  blockListContent[i, j, k, 0] = 0;//stop research
                                    continue;
                                }
                                else if (ResearchChange[(byte)PlayerTeam.Blue] == -2)
                                {
                                    //resume
                                    //SendServerMessage("" + (Research)blockListContent[i, j, k, 1]);
                                    ResearchChange[(byte)PlayerTeam.Blue] = blockListContent[i, j, k, 1];
                                }

                                if (ResearchChange[(byte)PlayerTeam.Blue] != -1)
                                    if (blockListContent[i, j, k, 0] > 0)
                                        if (blockListContent[i, j, k, 1] > 0)
                                        {
                                            if (teamCashBlue > 0 && ResearchProgress[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]] > 0 && blockListContent[i, j, k, 4] == 0)
                                            {
                                                ResearchProgress[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]]--;

                                                blockListContent[i, j, k, 4] = 10;//timer
                                                blockListContent[i, j, k, 3] = 0;//message warnings
                                                teamCashBlue -= 1;

                                                NetBuffer msgBuffer = netServer.CreateBuffer();
                                                foreach (NetConnection netConn in playerList.Keys)
                                                    if (netConn.Status == NetConnectionStatus.Connected)
                                                        SendTeamCashUpdate(playerList[netConn]);

                                                if (ResearchProgress[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]] == 0)//research complete
                                                {
                                                    ResearchChange[(byte)PlayerTeam.Blue] = 0;
                                                    ResearchComplete[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]]++;//increase rank
                                                    ResearchProgress[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]] = ResearchInformation.GetCost((Research)blockListContent[i, j, k, 1]) * ((ResearchComplete[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]] + 1) * 2);
                                                    NetBuffer msgBufferr = netServer.CreateBuffer();
                                                    msgBufferr = netServer.CreateBuffer();
                                                    msgBufferr.Write((byte)InfiniminerMessage.ChatMessage);

                                                    msgBufferr.Write((byte)ChatMessageType.SayBlueTeam);
                                                    if (ResearchComplete[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]] == 1)//first rank
                                                        msgBufferr.Write(Defines.Sanitize(ResearchInformation.GetName((Research)blockListContent[i, j, k, 1]) + " research was completed!"));
                                                    else
                                                        msgBufferr.Write(Defines.Sanitize(ResearchInformation.GetName((Research)blockListContent[i, j, k, 1]) + " rank " + ResearchComplete[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]] + " research was completed!"));
                                                    foreach (NetConnection netConn in playerList.Keys)
                                                        if (netConn.Status == NetConnectionStatus.Connected)
                                                            if (playerList[netConn].Team == PlayerTeam.Blue)
                                                                netServer.SendMessage(msgBufferr, netConn, NetChannel.ReliableUnordered);

                                                    //recalculate player statistics/buffs
                                                    ResearchRecalculate(PlayerTeam.Blue, blockListContent[i, j, k, 1]);
                                                    //
                                                    blockListContent[i, j, k, 1] = 0;
                                                    blockListContent[i, j, k, 0] = 0;
                                                }
                                            }
                                            else if (blockListContent[i, j, k, 4] > 0)
                                            {
                                                blockListContent[i, j, k, 4]--;
                                            }
                                            else
                                            {
                                                if (blockListContent[i, j, k, 3] == 0)
                                                {
                                                    blockListContent[i, j, k, 3] = 1;
                                                    NetBuffer msgBuffer = netServer.CreateBuffer();
                                                    msgBuffer = netServer.CreateBuffer();
                                                    msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                                                    msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);
                                                    //"Research topic: " + ResearchInformation.GetName((Research)blockListContent[x, y, z, 1]) + "(" + ResearchComplete[(byte)player.Team, blockListContent[x, y, z, 1]] + ") (" + ResearchProgress[(byte)player.Team, blockListContent[x, y, z, 1]] + " gold)
                                                    msgBuffer.Write(Defines.Sanitize(ResearchInformation.GetName((Research)blockListContent[i, j, k, 1]) + " research requires " + ResearchProgress[(byte)PlayerTeam.Blue, blockListContent[i, j, k, 1]] + " more gold!"));
                                                    foreach (NetConnection netConn in playerList.Keys)
                                                        if (netConn.Status == NetConnectionStatus.Connected)
                                                            if (playerList[netConn].Team == PlayerTeam.Blue)
                                                                netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

                                                    ResearchChange[(byte)PlayerTeam.Blue] = -2;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (blockListContent[i, j, k, 3] == 0)
                                            {
                                                blockListContent[i, j, k, 3] = 1;
                                                NetBuffer msgBuffer = netServer.CreateBuffer();
                                                msgBuffer = netServer.CreateBuffer();
                                                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                                                msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);
                                                msgBuffer.Write(Defines.Sanitize("Research has halted!"));
                                                foreach (NetConnection netConn in playerList.Keys)
                                                    if (netConn.Status == NetConnectionStatus.Connected)
                                                        if (playerList[netConn].Team == PlayerTeam.Blue)
                                                            netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

                                            }

                                            //no longer has research topic
                                        }
                            }
                            else if (blockList[i, j, k] == BlockType.MedicalR || blockList[i, j, k] == BlockType.MedicalB)
                            {
                                if (blockListContent[i, j, k, 1] < 100)
                                {
                                    blockListContent[i, j, k, 1]++;
                                }
                            }
                            else if (blockList[i, j, k] == BlockType.BaseBlue || blockList[i, j, k] == BlockType.BaseRed)
                            {
                                foreach (Player p in playerList.Values)
                                {
                                    if (p.Team == PlayerTeam.Blue && blockList[i, j, k] == BlockType.BaseBlue)
                                    {
                                        float distfromBase = (p.Position - new Vector3(i, j + 2, k - 1)).Length();
                                        if (distfromBase < 3)
                                        {
                                            DepositCash(p);
                                            if (p.Alive)
                                            {
                                                if (p.Health < p.HealthMax)
                                                {
                                                    p.Health = p.HealthMax;
                                                    SendHealthUpdate(p);
                                                }
                                                if (p.StatusEffect[4] == 0)//damage buff
                                                {//apply block damage buff to prevent walling players in
                                                    p.StatusEffect[4] = 1;
                                                    SendStatusEffectSpecificUpdate(p, 4);
                                                    //SendServerMessageToPlayer("You have the block damage buff!", p.NetConn);
                                                }
                                            }
                                        }
                                        else if (distfromBase < 7)
                                        {
                                            //apply block damage buff to prevent walling players in
                                            if (p.Alive)
                                            {
                                                if (p.Health < p.HealthMax)
                                                {
                                                    p.Health++;
                                                    SendHealthUpdate(p);
                                                }
                                                if (p.StatusEffect[4] == 0)//damage buff
                                                {//apply block damage buff to prevent walling players in
                                                    //SendServerMessageToPlayer("You have the block damage buff!", p.NetConn);
                                                    p.StatusEffect[4] = 1;
                                                    SendStatusEffectSpecificUpdate(p, 4);
                                                }
                                            }
                                        }
                                        else if (distfromBase < 10)
                                        {
                                            if (p.Alive)
                                            {
                                                if (p.Health < p.HealthMax)
                                                {
                                                    p.Health++;
                                                    SendHealthUpdate(p);
                                                }
                                                if (p.StatusEffect[4] > 0)
                                                {
                                                    //SendServerMessageToPlayer("You have lost the block damage buff!", p.NetConn);
                                                    p.StatusEffect[4] = 0;//remove the buff
                                                    SendStatusEffectSpecificUpdate(p, 4);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (p.StatusEffect[4] > 0)
                                            {
                                                //SendServerMessageToPlayer("You have lost the block damage buff!", p.NetConn);
                                                p.StatusEffect[4] = 0;//remove the buff
                                                SendStatusEffectSpecificUpdate(p, 4);
                                            }
                                        }
                                    }
                                    else if (p.Team == PlayerTeam.Red && blockList[i, j, k] == BlockType.BaseRed)
                                    {
                                        float distfromBase = (p.Position - new Vector3(i, j + 2, k + 1)).Length();
                                        if (distfromBase < 3)
                                        {
                                            DepositCash(p);
                                            //apply block damage buff to prevent walling players in
                                            if (p.Alive)
                                            {
                                                if (p.Health < p.HealthMax)
                                                {
                                                    p.Health = p.HealthMax;
                                                    SendHealthUpdate(p);
                                                }
                                                if (p.StatusEffect[4] == 0)//damage buff
                                                {//apply block damage buff to prevent walling players in
                                                    //SendServerMessageToPlayer("You have the block damage buff!", p.NetConn);
                                                    p.StatusEffect[4] = 1;
                                                    SendStatusEffectSpecificUpdate(p, 4);
                                                }
                                            }
                                        }
                                        else if (distfromBase < 7)
                                        {
                                            //apply block damage buff to prevent walling players in
                                            if (p.Alive)
                                            {
                                                if (p.Health < p.HealthMax)
                                                {
                                                    p.Health++;
                                                    SendHealthUpdate(p);
                                                }
                                                if (p.StatusEffect[4] == 0)//damage buff
                                                {//apply block damage buff to prevent walling players in
                                                    //SendServerMessageToPlayer("You have the block damage buff!", p.NetConn);
                                                    p.StatusEffect[4] = 1;
                                                    SendStatusEffectSpecificUpdate(p, 4);
                                                }
                                            }
                                        }
                                        else if (distfromBase < 10)
                                        {
                                            //apply block damage buff to prevent walling players in
                                            if (p.Alive)
                                            {
                                                if (p.Health < p.HealthMax)
                                                {
                                                    p.Health++;
                                                    SendHealthUpdate(p);
                                                }
                                                if (p.StatusEffect[4] > 0)
                                                {
                                                    //SendServerMessageToPlayer("You have lost the block damage buff!", p.NetConn);
                                                    p.StatusEffect[4] = 0;//remove the buff
                                                    SendStatusEffectSpecificUpdate(p, 4);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (p.StatusEffect[4] > 0)
                                            {
                                                //SendServerMessageToPlayer("You have lost the block damage buff!", p.NetConn);
                                                p.StatusEffect[4] = 0;//remove the buff
                                                SendStatusEffectSpecificUpdate(p, 4);
                                            }
                                        }
                                    }
                                }

                                if (blockListContent[i, j, k, 1] > 0)
                                {
                                    if (blockList[i, j, k] == BlockType.BaseBlue)
                                    {
                                        if (teamCashBlue > 4 && blockListContent[i, j, k, 4] == 0)
                                        {
                                            blockListContent[i, j, k, 4] = 3;
                                            blockListContent[i, j, k, 1]--;
                                            blockListContent[i, j, k, 2] = 0;
                                            teamCashBlue -= 5;

                                            NetBuffer msgBuffer = netServer.CreateBuffer();
                                            foreach (NetConnection netConn in playerList.Keys)
                                                if (netConn.Status == NetConnectionStatus.Connected)
                                                    SendTeamCashUpdate(playerList[netConn]);
                                        }
                                        else if (blockListContent[i, j, k, 4] > 0 && blockListContent[i, j, k, 5] == 1)
                                        {
                                            blockListContent[i, j, k, 4]--;
                                        }
                                        else if (blockListContent[i, j, k, 5] == 1)
                                        {
                                            if (blockListContent[i, j, k, 2] == 0)//warning message
                                            {
                                                blockListContent[i, j, k, 2] = 1;

                                                NetBuffer msgBuffer = netServer.CreateBuffer();
                                                msgBuffer = netServer.CreateBuffer();
                                                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                                                msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);
                                                msgBuffer.Write(Defines.Sanitize("The forge requires " + blockListContent[i, j, k, 1] * 5 + " more gold!"));
                                                foreach (NetConnection netConn in playerList.Keys)
                                                    if (netConn.Status == NetConnectionStatus.Connected)
                                                        if (playerList[netConn].Team == PlayerTeam.Blue)
                                                            netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

                                            }
                                        }
                                    }
                                    else if (blockList[i, j, k] == BlockType.BaseRed)
                                    {
                                        if (teamCashRed > 4 && blockListContent[i, j, k, 4] == 0)
                                        {
                                            blockListContent[i, j, k, 4] = 3;
                                            blockListContent[i, j, k, 1]--;
                                            blockListContent[i, j, k, 2] = 0;
                                            teamCashRed -= 5;

                                            NetBuffer msgBuffer = netServer.CreateBuffer();
                                            foreach (NetConnection netConn in playerList.Keys)
                                                if (netConn.Status == NetConnectionStatus.Connected)
                                                    SendTeamCashUpdate(playerList[netConn]);
                                        }
                                        else if (blockListContent[i, j, k, 4] > 0 && blockListContent[i, j, k, 5] == 1)
                                        {
                                            blockListContent[i, j, k, 4]--;
                                        }
                                        else if (blockListContent[i, j, k, 5] == 1)
                                        {
                                            if (blockListContent[i, j, k, 2] == 0)//warning message
                                            {
                                                blockListContent[i, j, k, 2] = 1;

                                                NetBuffer msgBuffer = netServer.CreateBuffer();
                                                msgBuffer = netServer.CreateBuffer();
                                                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);


                                                msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                                                msgBuffer.Write(Defines.Sanitize("The forge requires " + blockListContent[i, j, k, 1] * 5 + " more gold!"));
                                                foreach (NetConnection netConn in playerList.Keys)
                                                    if (netConn.Status == NetConnectionStatus.Connected)
                                                        if (playerList[netConn].Team == PlayerTeam.Red)
                                                            netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
                                            }
                                        }
                                    }

                                    if (blockListContent[i, j, k, 1] == 0)
                                    {
                                        int arttype = randGenB.Next(1, TOTAL_ARTS);
                                        if (arttype == 17)//clair
                                            arttype = 1;//material

                                        uint arty = SetItem(ItemType.Artifact, new Vector3(i + 0.5f, j + 1.5f, k + 0.5f), Vector3.Zero, Vector3.Zero, PlayerTeam.None, arttype, 0);
                                        blockListContent[i, j, k, 5] = 0;
                                       
                                        NetBuffer msgBuffer = netServer.CreateBuffer();
                                        msgBuffer = netServer.CreateBuffer();
                                        msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                                        if (blockList[i, j, k] == BlockType.BaseRed)
                                        {
                                            NEW_ART_RED = arty;
                                            itemList[arty].Content[7] = 1;
                                            msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                                        }
                                        else if (blockList[i, j, k] == BlockType.BaseBlue)
                                        {
                                            NEW_ART_BLUE = arty;
                                            itemList[arty].Content[7] = 1;
                                            msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);
                                        }

                                        if (blockList[i, j, k] == BlockType.BaseRed)
                                            msgBuffer.Write(Defines.Sanitize("The " + PlayerTeam.Red + " have formed the " + ArtifactInformation.GetName(arttype) + "!"));
                                        else if (blockList[i, j, k] == BlockType.BaseBlue)
                                            msgBuffer.Write(Defines.Sanitize("The " + PlayerTeam.Blue + " have formed the " + ArtifactInformation.GetName(arttype) + "!"));
                                        foreach (NetConnection netConn in playerList.Keys)
                                            if (netConn.Status == NetConnectionStatus.Connected)
                                                netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
                                    }
                                }
                            }
                    }

            if (scantime == 0)
            {
                foreach (Player p in playerList.Values)
                {
                    if (p.Alive && !p.Disposing)
                    {
                        if (p.Radar == 1)//was on radar
                        {
                            if (p.Content[1] == 0)//but no radars found us
                            {
                                //ConsoleWrite("radar off");
                                SendPlayerContentUpdate(p, 1);
                            }
                        }
                        else if (p.Radar == 0)//wasnt on radar
                        {
                            if (p.Content[1] == 1)//a radar found us
                            {
                                //ConsoleWrite("radar on");
                                SendPlayerContentUpdate(p, 1);
                            }
                        }
                    }
                }
                scantime = 2;//reset scan time (for radars)
            }
            else
            {
                scantime -= 1;
            }

            if (oreRedBefore != teamOreRed)
            {
                foreach (Player p in playerList.Values)
                {
                    if (p.Alive && !p.Disposing && p.Team == PlayerTeam.Red)
                    {
                        SendTeamOreUpdate(p);
                    }
                }
            }
            if (oreBlueBefore != teamOreBlue)
            {
                foreach (Player p in playerList.Values)
                {
                    if (p.Alive && !p.Disposing && p.Team == PlayerTeam.Blue)
                    {
                        SendTeamOreUpdate(p);
                    }
                }
            }

            physactive = false;
        }
        public void Disturb(ushort i, ushort j, ushort k)
        {
            for (ushort a = (ushort)(i-1); a <= 1 + i; a++)
                for (ushort b = (ushort)(j-1); b <= 1 + j; b++)
                    for (ushort c = (ushort)(k-1); c <= 1 + k; c++)
                        if (a > 0 && b > 0 && c > 0 && a < MAPSIZE && b < MAPSIZE && c < MAPSIZE)
                        {
                            flowSleep[a, b, c] = false;
                        }
        }
        public BlockType BlockAtPoint(Vector3 point)
        {
            ushort x = (ushort)point.X;
            ushort y = (ushort)point.Y;
            ushort z = (ushort)point.Z;
            if (x <= 0 || y <= 0 || z <= 0 || (int)x > MAPSIZE - 1 || (int)y > MAPSIZE - 1 || (int)z > MAPSIZE - 1)
                return BlockType.None;
            return blockList[x, y, z];
        }

        public bool blockTrace(ushort oX,ushort oY,ushort oZ,ushort dX,ushort dY,ushort dZ,BlockType allow)//only traces x/y not depth
        {
            while (oX != dX || oY != dY || oZ != dZ)
            {
                if (oX - dX > 0)
                {
                    oX = (ushort)(oX - 1);
                    if (blockList[oX, oY, oZ] != allow)
                    {
                        return false;
                    }
                }
                else if (oX - dX < 0)
                {
                    oX = (ushort)(oX + 1);
                    if (blockList[oX, oY, oZ] != allow)
                    {
                        return false;
                    }
                }

                if (oZ - dZ > 0)
                {
                    oZ = (ushort)(oZ - 1);
                    if (blockList[oX, oY, oZ] != allow)
                    {
                        return false;
                    }
                }
                else if (oZ - dZ < 0)
                {
                    oZ = (ushort)(oZ + 1);
                    if (blockList[oX, oY, oZ] != allow)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public bool RayCollision(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint)
        {
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;
            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (testBlock != BlockType.None)
                {
                    hitPoint = testPos;
                    buildPoint = buildPos;
                    return true;
                }
                buildPos = testPos;
            }
            return false;
        }
        public bool RayCollision(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, BlockType allow)
        {
            Vector3 testPos = startPosition;
            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (testBlock != BlockType.None || testBlock != allow)
                {
                    return false;
                }
            }
            return true;
        }

        public bool RayCollision(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint, BlockType ignore)
        {
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;
            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (testBlock != BlockType.None && testBlock != ignore)
                {
                    hitPoint = testPos;
                    buildPoint = buildPos;
                    return true;
                }
                buildPos = testPos;
            }
            return false;
        }

        public bool RayCollisionDig(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint, BlockType ignore, BlockType ignore2)
        {
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;
            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (testBlock != BlockType.None && testBlock != ignore && testBlock != ignore2 && testBlock != BlockType.Vacuum)
                {
                    hitPoint = testPos;
                    buildPoint = buildPos;
                    return true;
                }
                buildPos = testPos;
            }
            return false;
        }

        public Vector3 RayCollisionExact(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint)
        {
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;
           
            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (testBlock != BlockType.None)
                {
                    hitPoint = testPos;
                    buildPoint = buildPos;
                    return hitPoint;
                }
                buildPos = testPos;
            }

            return startPosition;
        }

        public Vector3 RayCollisionExactNone(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint)
        {//returns a point in space when it reaches distance
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;

            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);

                if (testBlock != BlockType.None)
                {
                    return startPosition;
                }
            }
            return testPos;
        }
        public void UsePickaxe(Player player, Vector3 playerPosition, Vector3 playerHeading)
        {
            player.QueueAnimationBreak = true;
            
            // Figure out what we're hitting.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;

            if (artifactActive[(byte)player.Team, 4] > 0 || player.Content[10] == 4)
            {
                if (!RayCollisionDig(playerPosition, playerHeading, 2, 10, ref hitPoint, ref buildPoint, BlockType.Water, player.Team == PlayerTeam.Red ? BlockType.TransRed : BlockType.TransBlue))
                {
                    return;
                }
            }
            else
            {
                if (!RayCollisionDig(playerPosition, playerHeading, 2, 10, ref hitPoint, ref buildPoint, BlockType.None, player.Team == PlayerTeam.Red ? BlockType.TransRed : BlockType.TransBlue))
                {
                    return;
                }
            }
            ushort x = (ushort)hitPoint.X;
            ushort y = (ushort)hitPoint.Y;
            ushort z = (ushort)hitPoint.Z;

            if (player.Alive == false || player.Digs < player.GetToolCooldown(PlayerTools.Pickaxe))
            {
                //ConsoleWrite("dead fixed " + player.Handle + " synchronization");
                SetBlockForPlayer(x, y, z, blockList[x, y, z], blockCreatorTeam[x, y, z], player);
                return;
            }
            else
            {
                player.Digs -= player.GetToolCooldown(PlayerTools.Pickaxe);
                //ConsoleWrite("dig:" + player.Digs);
                player.playerToolCooldown = DateTime.Now + TimeSpan.FromSeconds((float)(player.GetToolCooldown(PlayerTools.Pickaxe)));
            }
            // Figure out what the result is.
            bool removeBlock = false;
            uint giveOre = 0;
            uint giveCash = 0;
            uint giveWeight = 0;
            int Damage = 2 + ResearchComplete[(byte)player.Team, 5];
            if (player.StatusEffect[4] > 0)//base dmg buff
            {
                Damage = 30;
            }
            InfiniminerSound sound = InfiniminerSound.DigDirt;
            BlockType block = BlockAtPoint(hitPoint);

            switch (block)
            {
                case BlockType.None:
                    {
                        //client has poor sync and sees stuff he shouldnt
                        SetBlockForPlayer(x, y, z, blockList[x, y, z], PlayerTeam.None, player);
                        break;
                    }
                case BlockType.Lava:
                    if (varGetB("minelava"))
                    {
                        removeBlock = true;
                        sound = InfiniminerSound.DigDirt;
                    }
                    else if (player.StatusEffect[4] > 0)
                    {
                        removeBlock = true;
                        sound = InfiniminerSound.DigDirt;
                    }
                    break;
                case BlockType.Water:
                    if (varGetB("minelava"))
                    {
                        removeBlock = true;
                        sound = InfiniminerSound.DigDirt;
                    }
                    else if (player.StatusEffect[4] > 0)
                    {
                        removeBlock = true;
                        sound = InfiniminerSound.DigDirt;
                    }
                    break;
                case BlockType.Dirt:
                case BlockType.Mud:
                case BlockType.Grass:
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
                    if(!varGetB("sandbox"))
                    giveOre = 10 + (uint)(ResearchComplete[(byte)player.Team, (byte)Research.OreRefinery]);
                    sound = InfiniminerSound.DigMetal;
                    break;

                case BlockType.Gold:
                    Damage = 2;//damage must be preset for gold
                    giveWeight = 1;
                    giveCash = 10;
                    sound = InfiniminerSound.RadarLow;
                    break;

                case BlockType.Diamond:
                    //removeBlock = true;
                    //giveWeight = 1;
                    //giveCash = 1000;
                    sound = InfiniminerSound.RadarHigh;
                    break;

                case BlockType.SolidRed:
                case BlockType.SolidBlue:
                case BlockType.InhibitorR:
                case BlockType.InhibitorB:
                case BlockType.SolidRed2:
                case BlockType.SolidBlue2:
                    sound = InfiniminerSound.DigMetal;
                    break;

                default:
                    break;
            }

            if (giveOre > 0)
            {
                if (player.OreMax > player.Ore + giveOre)
                {
                    player.Ore += giveOre;
                    SendOreUpdate(player);
                }
                else if(player.Ore < player.OreMax)//vaporize some ore to fit into players inventory
                {
                    player.Ore = player.OreMax;
                    SendOreUpdate(player);
                }
                else//ore goes onto ground
                {
                    SetItem(ItemType.Ore, hitPoint - (playerHeading * 0.3f), playerHeading, new Vector3(playerHeading.X * 1.5f, 0.0f, playerHeading.Z * 1.5f), PlayerTeam.None, 0, 0);
                }
            }

            if (giveWeight > 0)
            {
                if (player.Weight < player.WeightMax)
                {
                    player.Weight = Math.Min(player.Weight + giveWeight, player.WeightMax);
                    player.Cash += giveCash;
                    SendWeightUpdate(player);
                    SendCashUpdate(player);
                }
                else
                {
                    removeBlock = false;
                    if (block == BlockType.Gold)
                    {
                        if (player.Weight == player.WeightMax)
                        {
                            //gold goes onto the ground
                            SetItem(ItemType.Gold, hitPoint, playerHeading, new Vector3(playerHeading.X * 1.5f, 0.0f, playerHeading.Z * 1.5f), PlayerTeam.None, 0, 0);
                        }
                    }
                }
            }

            if (removeBlock)//block falls away with any hit
            {
                //SetBlock(x, y, z, BlockType.None, PlayerTeam.None);
                SetBlockDebris(x, y, z, BlockType.None, PlayerTeam.None);//blockset + adds debris for all players
                PlaySoundForEveryoneElse(sound, player.Position, player);
            }
            else if (Damage > 0 && BlockInformation.GetMaxHP(block) > 0)//this block is resistant to pickaxes
            {
                if (blockCreatorTeam[x, y, z] != player.Team)//block does not belong to us: destroy it
                {
                    if (blockListHP[x, y, z] < Damage)
                    {
                        player.Score += 5;
                        player.Exp += 5;
                        SpecialRemove(x, y, z);

                        switch (block)
                        {
                            case BlockType.RadarRed:
                            case BlockType.RadarBlue:
                                player.Score += 5;
                                player.Exp += 5;
                                break;
                            case BlockType.InhibitorR:
                            case BlockType.InhibitorB:
                                player.Score += 4;
                                player.Exp += 4;
                                break;
                            case BlockType.ArtCaseR:
                                player.Score += 40;
                                player.Exp += 40;
                                SendServerMessageRed("The enemy has breached an artifact safe!");
                                break;
                            case BlockType.ArtCaseB:
                                player.Score += 40;
                                player.Exp += 40;
                                SendServerMessageBlue("The enemy has breached an artifact safe!");
                                break;

                            default:
                                break;
                        }
                        
                        SetBlockDebris(x, y, z, BlockType.None, PlayerTeam.None);//blockset + adds debris for all players
                        
                        blockListHP[x, y, z] = 0;
                        sound = InfiniminerSound.Explosion;
                    }
                    else
                    {
                        hitPoint -= playerHeading * 0.3f;

                        DebrisEffectAtPoint(hitPoint.X, hitPoint.Y, hitPoint.Z, block, 0);

                        blockListHP[x, y, z] -= Damage;
                        hitPoint -= (playerHeading*0.4f);
                        
                        if (block == BlockType.SolidRed2 || block == BlockType.SolidBlue2)
                        {
                            if (blockCreatorTeam[x, y, z] != PlayerTeam.None)
                            {
                                player.Score += 1;
                                player.Exp += 1;
                            }
                            else//reverse damage for base walls (only for friendly teams hitting them)
                            {
                                if(player.Team == PlayerTeam.Red && block == BlockType.SolidRed2)
                                    blockListHP[x, y, z] += Damage;
                                else if (player.Team == PlayerTeam.Blue && block == BlockType.SolidBlue2)
                                    blockListHP[x, y, z] += Damage;
                            }

                            if (blockListHP[x, y, z] < 21)
                            {
                                SetBlock(x, y, z, block == BlockType.SolidRed2 ? BlockType.SolidRed : BlockType.SolidBlue, blockCreatorTeam[x, y, z]);
                            }
                        }
                        else if (block == BlockType.Gold)
                        {
                            player.Score += 1;
                            player.Exp += 1;
                            PlaySoundForEveryoneElse(InfiniminerSound.RadarLow, new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), player);
                            //InfiniminerSound.RadarHigh
                        }
                        else if (block == BlockType.Diamond)
                        {
                            player.Score += 1;
                            player.Exp += 1;
                            PlaySoundForEveryoneElse(InfiniminerSound.RadarHigh, new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), player);
                        }
                        else
                        {
                            if (blockCreatorTeam[x, y, z] != PlayerTeam.None)
                            {
                                player.Score += 1;
                                player.Exp += 1;
                            }
                        }

                        if (artifactActive[(byte)blockCreatorTeam[x, y, z], 7] > 0)//reflection artifact
                        {
                            if (player.Health > 2 * artifactActive[(byte)blockCreatorTeam[x, y, z], 7])
                            {
                                player.Health -= (uint)(2 * artifactActive[(byte)blockCreatorTeam[x, y, z], 7]);
                                SendHealthUpdate(player);
                            }
                            else
                            {
                                Player_Dead(player, "beat their head against a wall!");
                            }
                        }
                    }

                    PlaySoundForEveryoneElse(sound, player.Position, player);
                }
                else if (blockCreatorTeam[x, y, z] == player.Team)
                {
                    if (player.Ore > ResearchComplete[(byte)player.Team, 4])//make repairs
                    {
                        Damage = -(2 * ResearchComplete[(byte)player.Team, 4] + 2);

                        if(randGen.Next(3) == 2)
                        if (player.deathCount > 0)
                        {
                            player.deathCount--;
                            //ConsoleWrite("" + player.deathCount);
                        }
                        //sound = repair?

                        if (blockListHP[x, y, z] >= BlockInformation.GetMaxHP(blockList[x, y, z]))
                        {
                            if (block == BlockType.SolidRed || block == BlockType.SolidBlue)
                            {
                                hitPoint -= playerHeading * 0.3f;
                                player.Ore -= (uint)ResearchComplete[(byte)player.Team, 4] + 1;
                                blockListHP[x, y, z] -= Damage;
                                DebrisEffectAtPoint(hitPoint.X, hitPoint.Y, hitPoint.Z, block, 0);
                                SetBlock(x, y, z, block == BlockType.SolidRed ? BlockType.SolidRed2 : BlockType.SolidBlue2, player.Team);
                                SendOreUpdate(player);
                                player.Score += 20;
                                player.Exp += 20;
                                PlaySoundForEveryoneElse(sound, player.Position, player);
                            }
                            else if (block == BlockType.ConstructionR && player.Team == PlayerTeam.Red || block == BlockType.ConstructionB && player.Team == PlayerTeam.Blue)//construction complete
                            {
                                SetBlock(x, (ushort)(y + 1), z, player.Team == PlayerTeam.Red ? BlockType.ForceR : BlockType.ForceB, player.Team);
                                SetBlock(x, y, z, (BlockType)blockListContent[x, y, z, 0], player.Team);
                                blockListContent[x, y, z, 7] = 0;
                                blockListHP[x, y, z] = BlockInformation.GetMaxHP(blockList[x, y, z]);
                            }
                        }
                        else
                        {
                            player.Score += 1;
                            player.Exp += 1;
                            hitPoint -= playerHeading * 0.3f;
                            player.Ore -= (uint)ResearchComplete[(byte)player.Team, 4] + 1;
                            DebrisEffectAtPoint(hitPoint.X, hitPoint.Y, hitPoint.Z, block, 0);
                            blockListHP[x, y, z] -= Damage;
                            SendOreUpdate(player);
                            PlaySoundForEveryoneElse(sound, player.Position, player);
                        }
                    }
                    else
                    {
                        SendPlayerOreWarning(player);//insuff ore for repairs
                    }
                }
            }
            else
            {//player was out of sync, replace his empty block
                //ConsoleWrite("fixedemptyblock " + player.Handle + " synchronization");
                SetBlockForPlayer(x, y, z, block, blockCreatorTeam[x, y, z], player);
            }
        }

        public void UseSmash(Player player, Vector3 playerPosition, Vector3 playerHeading)
        {

        }

        public void Catapult(uint x, uint y, uint z, Vector3 heading)
        {
            BlockType block = blockList[x, y, z];

            blockListContent[x, y, z, 11] = (int)((heading.X*0.5) * 100);//1.2 = throw strength
            blockListContent[x, y, z, 12] = (int)((heading.Y*0.5) * 100);
            blockListContent[x, y, z, 13] = (int)((heading.Z*0.5) * 100);
            blockListContent[x, y, z, 14] = (int)(x+0.5f) * 100;
            blockListContent[x, y, z, 15] = (int)(y+0.5f) * 100;
            blockListContent[x, y, z, 16] = (int)(z+0.5f) * 100;
            //ConsoleWrite("" + blockListContent[x, y, z, 11] + "/" + blockListContent[x, y, z, 12] + "/" + blockListContent[x, y, z, 13]);
            if (block == BlockType.Explosive)//must update player explosive keys
            {
                foreach (Player p in playerList.Values)
                {
                    int cc = p.ExplosiveList.Count;

                    int ca = 0;
                    while (ca < cc)
                    {
                        if (p.ExplosiveList[ca].X == x && p.ExplosiveList[ca].Y == y && p.ExplosiveList[ca].Z == z)
                        {
                            blockListContent[x, y, z, 17] = (int)p.ID;
                            p.ExplosiveList.RemoveAt(ca);//experimental
                            break;
                        }
                        ca += 1;
                    }
                }

            }
            else if (block == BlockType.Barrel)
            {
                blockListContent[x, y, z, 0] = 0;//empty barrel
            }
            blockListContent[x, y, z, 10] = 1;//undergoing gravity changes 

            if(block != BlockType.Lava)
            PlaySound(InfiniminerSound.GroundHit, new Vector3(x+0.5f,y+0.5f,z+0.5f));
        }

        public void UseStrongArm(Player player, Vector3 playerPosition, Vector3 playerHeading)
        {
            player.QueueAnimationBreak = true;
            Vector3 headPosition = playerPosition + new Vector3(0f, 0.1f, 0f);
            // Figure out what we're hitting.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;

            if (player.Content[5] == 0)
                if (!RayCollision(playerPosition, playerHeading, 2, 10, ref hitPoint, ref buildPoint, BlockType.Water))
                    return;

            if (player.Content[5] > 0)
            {
                //Vector3 throwPoint = RayCollisionExact(playerPosition, playerHeading, 10, 100, ref hitPoint, ref buildPoint);
                //if (throwPoint != playerPosition)
                //{
                    //double dist = Distf(playerPosition, throwPoint);
                    //if (dist < 2)
                     //   return;//distance of ray should be strength
                    //else
                    {
                        //begin throw
                        buildPoint = headPosition + (playerHeading*2);
                            //RayCollisionExactNone(playerPosition, playerHeading, 2, 10, ref hitPoint, ref buildPoint);
                        //
                    }
              //  }
            }

            if (player.Digs >= player.GetToolCooldown(PlayerTools.Pickaxe) * 4 && player.playerToolCooldown < DateTime.Now)
            {
                
            }
            else
            {
                return;
            }
            ushort x = (ushort)hitPoint.X;
            ushort y = (ushort)hitPoint.Y;
            ushort z = (ushort)hitPoint.Z;
            // Figure out what the result is.
            bool grabBlock = false;

            if (player.Content[5] == 0)
            {
                uint giveWeight = 0;
                InfiniminerSound sound = InfiniminerSound.DigDirt;

                BlockType block = BlockAtPoint(hitPoint);
                switch (block)
                {

                    case BlockType.Dirt:
                    case BlockType.Grass:
                    case BlockType.Pump:
                    case BlockType.Barrel:
                    case BlockType.Pipe:
                    case BlockType.Rock:
                    case BlockType.Mud:
                    case BlockType.Sand:
                    case BlockType.DirtSign:
                    case BlockType.StealthBlockB:
                    case BlockType.StealthBlockR:
                    case BlockType.TrapB:
                    case BlockType.TrapR:
                    case BlockType.Ore:
                        grabBlock = true;
                        giveWeight = 10;
                        sound = InfiniminerSound.DigMetal;
                        break;
                    case BlockType.Explosive:
                        if (blockListContent[x, y, z, 1] == 0)
                        {
                            grabBlock = true;
                            giveWeight = 10;
                            sound = InfiniminerSound.DigMetal;
                        }
                        break;
                    case BlockType.SolidBlue:
                        if (player.Team == PlayerTeam.Blue)
                        {
                            grabBlock = true;
                            giveWeight = 10;
                            sound = InfiniminerSound.DigMetal;
                        }
                        break;
                    case BlockType.SolidRed:
                        if (player.Team == PlayerTeam.Red)
                        {
                            grabBlock = true;
                            giveWeight = 10;
                            sound = InfiniminerSound.DigMetal;
                        }
                        break;
                }

                if (blockCreatorTeam[x, y, z] == PlayerTeam.Blue && player.Team == PlayerTeam.Red)
                {
                    return;//dont allow enemy team to manipulate other teams team-blocks
                }
                else if (blockCreatorTeam[x, y, z] == PlayerTeam.Red && player.Team == PlayerTeam.Blue)
                {
                    return;
                }

                if (giveWeight > 0)
                {
                    if (player.Weight + giveWeight <= player.WeightMax)
                    {
                        player.Weight += giveWeight;
                        SendWeightUpdate(player);
                    }
                    else
                    {
                        SendServerMessageToPlayer("You are already carrying too much!", player.NetConn);
                        grabBlock = false;
                    }
                }

                if (grabBlock)
                {
                    player.playerToolCooldown = DateTime.Now + TimeSpan.FromSeconds((float)(player.GetToolCooldown(PlayerTools.Pickaxe) * 3));
                    player.Digs -= player.GetToolCooldown(PlayerTools.Pickaxe) * 4;
                    player.Content[5] = (byte)block;
                    for (uint cc = 0; cc < 20; cc++)//copy the content values
                    {
                        player.Content[50 + cc] = blockListContent[x, y, z, cc];//50 is past players accessible content, it is for server only
                    }

                    player.Content[50 + 21] = blockListHP[x, y, z];
                    if (block == BlockType.Explosive)//must update player explosive keys
                    {                        
                        foreach (Player p in playerList.Values)
                        {
                            int cc = p.ExplosiveList.Count;

                            int ca = 0;
                            while(ca < cc)
                            {
                                if (p.ExplosiveList[ca].X == x && p.ExplosiveList[ca].Y == y && p.ExplosiveList[ca].Z == z)
                                {
                                    player.Content[50 + 17] = (int)p.ID;
                                    p.ExplosiveList.RemoveAt(ca);//experimental
                                    break;
                                }
                                ca += 1;
                            }
                        }

                    }

                    SendContentSpecificUpdate(player,5);
                    SetBlock(x, y, z, BlockType.None, PlayerTeam.None);
                    PlaySound(sound, player.Position);
                }
            }
            else
            {//throw the block
                BlockType block = (BlockType)(player.Content[5]);
                if (block != BlockType.None)
                {
                    ushort bx = (ushort)buildPoint.X;
                    ushort by = (ushort)buildPoint.Y;
                    ushort bz = (ushort)buildPoint.Z;
                    if (blockList[bx, by, bz] == BlockType.None)
                    {
                        player.Digs -= player.GetToolCooldown(PlayerTools.Pickaxe) * 4;
                        player.playerToolCooldown = DateTime.Now + TimeSpan.FromSeconds((float)(player.GetToolCooldown(PlayerTools.Pickaxe) * 3));

                        SetBlock(bx, by, bz, block, PlayerTeam.None);
                        blockListHP[bx, by, bz] = player.Content[50 + 21];
                        player.Weight -= 10;
                        player.Content[5] = 0;
                        SendWeightUpdate(player);
                        SendContentSpecificUpdate(player, 5);
                        for (uint cc = 0; cc < 20; cc++)//copy the content values
                        {
                            blockListContent[bx, by, bz, cc] = player.Content[50 + cc];
                            if (cc == 17 && block == BlockType.Explosive)//explosive list for tnt update
                            {
                                foreach (Player p in playerList.Values)
                                {
                                    if (p.ID == (uint)(blockListContent[bx, by, bz, cc]))
                                    {
                                        //found explosive this belongs to
                                        //p.ExplosiveList.Add(new Vector3(bx,by,bz));
                                    }
                                }
                            }
                            player.Content[50 + cc] = 0;
                        }

                        blockListContent[bx, by, bz, 10] = 1;//undergoing gravity changes 
                        blockListContent[bx, by, bz, 11] = (int)((playerHeading.X*1.2)*100);//1.2 = throw strength
                        blockListContent[bx, by, bz, 12] = (int)((playerHeading.Y*1.2)*100);
                        blockListContent[bx, by, bz, 13] = (int)((playerHeading.Z*1.2)*100);
                        blockListContent[bx, by, bz, 14] = (int)((buildPoint.X) * 100);
                        blockListContent[bx, by, bz, 15] = (int)((buildPoint.Y) * 100);
                        blockListContent[bx, by, bz, 16] = (int)((buildPoint.Z) * 100);

                        blockCreatorTeam[bx, by, bz] = player.Team;
                        PlaySound(InfiniminerSound.GroundHit, player.Position);
                    }
                }
            }
        }
        //private bool LocationNearBase(ushort x, ushort y, ushort z)
        //{
        //    for (int i=0; i<MAPSIZE; i++)
        //        for (int j=0; j<MAPSIZE; j++)
        //            for (int k = 0; k < MAPSIZE; k++)
        //                if (blockList[i, j, k] == BlockType.HomeBlue || blockList[i, j, k] == BlockType.HomeRed)
        //                {
        //                    double dist = Math.Sqrt(Math.Pow(x - i, 2) + Math.Pow(y - j, 2) + Math.Pow(z - k, 2));
        //                    if (dist < 3)
        //                        return true;
        //                }
        //    return false;
        //}
        public void ThrowRope(Player player, Vector3 playerPosition, Vector3 playerHeading)
        {
            bool actionFailed = false;

            if (player.Alive == false || player.playerToolCooldown > DateTime.Now)
            {
                actionFailed = true;
            }
            else if (player.Ore > 49)
            {
                player.Ore -= 50;
                SendOreUpdate(player);
            }
            else
            {
                actionFailed = true;
            }
            // If there's no surface within range, bail.
            Vector3 hitPoint = playerPosition;
            Vector3 buildPoint = playerPosition;
            Vector3 exactPoint = playerPosition;
          
            ushort x = (ushort)buildPoint.X;
            ushort y = (ushort)buildPoint.Y;
            ushort z = (ushort)buildPoint.Z;

            if (x <= 0 || y <= 0 || z <= 0 || (int)x > MAPSIZE - 1 || (int)y > MAPSIZE - 1 || (int)z > MAPSIZE - 1)
                actionFailed = true;

            if (blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Lava || blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Water)
                actionFailed = true;

            if (actionFailed)
            {
                // Decharge the player's gun.
                //    TriggerConstructionGunAnimation(player, -0.2f);
            }
            else
            {
                player.playerToolCooldown = DateTime.Now + TimeSpan.FromSeconds((float)(player.GetToolCooldown(PlayerTools.ThrowRope)));
                // Fire the player's gun.
                //    TriggerConstructionGunAnimation(player, 0.5f);

                // Build the block.
                //hitPoint = RayCollision(playerPosition, playerHeading, 6, 25, ref hitPoint, ref buildPoint, 1);

                exactPoint.Y = exactPoint.Y + (float)0.25;//0.25 = items height

                uint ii = SetItem(ItemType.Rope, exactPoint, playerHeading, playerHeading * 5, player.Team, 0, 0);
                itemList[ii].Content[6] = (byte)player.Team;//set teamsafe
                // player.Ore -= blockCost;
                // SendResourceUpdate(player);

                // Play the sound.
                PlaySound(InfiniminerSound.ConstructionGun, player.Position);
            }


        }
        public void ThrowBomb(Player player, Vector3 playerPosition, Vector3 playerHeading)
        {
            bool actionFailed = false;
            if (!varGetB("tnt") || SiegeBuild)
                return;

            if (player.Alive == false || player.playerToolCooldown > DateTime.Now)
            {
                actionFailed = true;
            }
            else if (player.Ore > 49)
            {
                player.Ore -= 50;
                SendOreUpdate(player);
            }
            else if (player.Ore < 50)
            {
                SendPlayerOreWarning(player);
                actionFailed = true;
            }
            else
            {
                actionFailed = true;
            }
            // If there's no surface within range, bail.
            Vector3 hitPoint = playerPosition;//Vector3.Zero;
            Vector3 buildPoint = playerPosition;
            Vector3 exactPoint = playerPosition;
            //if (!RayCollision(playerPosition, playerHeading, 6, 25, ref hitPoint, ref buildPoint))
            //{
            //    actionFailed = true;
            //}
            //else
            //{
            //    exactPoint = RayCollisionExact(playerPosition, playerHeading, 6, 25, ref hitPoint, ref buildPoint);
            //}
            ushort x = (ushort)buildPoint.X;
            ushort y = (ushort)buildPoint.Y;
            ushort z = (ushort)buildPoint.Z;

            if (x <= 0 || y <= 0 || z <= 0 || (int)x > MAPSIZE - 1 || (int)y > MAPSIZE - 1 || (int)z > MAPSIZE - 1)
                actionFailed = true;

            if (blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Lava || blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Water)
                actionFailed = true;

            if (actionFailed)
            {
                // Decharge the player's gun.
            //    TriggerConstructionGunAnimation(player, -0.2f);
            }
            else
            {
                player.playerToolCooldown = DateTime.Now + TimeSpan.FromSeconds((float)(player.GetToolCooldown(PlayerTools.ThrowBomb)));
                // Fire the player's gun.
            //    TriggerConstructionGunAnimation(player, 0.5f);

                // Build the block.
                //hitPoint = RayCollision(playerPosition, playerHeading, 6, 25, ref hitPoint, ref buildPoint, 1);

                exactPoint.Y = exactPoint.Y + (float)0.25;//0.25 = items height

                uint ii = SetItem(ItemType.Bomb, exactPoint, playerHeading, playerHeading*3, player.Team, 0, 0);
                itemList[ii].Content[6] = (byte)player.Team;//set teamsafe
               // player.Ore -= blockCost;
               // SendResourceUpdate(player);

                // Play the sound.
                PlaySound(InfiniminerSound.ConstructionGun, player.Position);
            }            


        }
        public void SpecialRemove(ushort x, ushort y, ushort z)
        {
            BlockType blockType = blockList[x, y, z];
            switch (blockType)//special removes
            {
                case BlockType.Refinery:
                    {
                        if (ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] <= 32)
                            ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] -= 2;
                        else
                            ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] -= 1;
                        NetBuffer msgBuffer = netServer.CreateBuffer();
                        msgBuffer = netServer.CreateBuffer();
                        msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                        if (blockCreatorTeam[x, y, z] == PlayerTeam.Red)
                        {
                            msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                        }
                        else if (blockCreatorTeam[x, y, z] == PlayerTeam.Blue)
                        {
                            msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);
                        }

                        msgBuffer.Write(Defines.Sanitize("Ore efficency reduced to +" + ResearchComplete[(byte)blockCreatorTeam[x, y, z], (byte)Research.OreRefinery] + "!"));

                        foreach (NetConnection netConn in playerList.Keys)
                            if (netConn.Status == NetConnectionStatus.Connected)
                                if (playerList[netConn].Team == blockCreatorTeam[x, y, z])
                                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
                    }
                    break;
                //radars now processed automatically
                //case BlockType.RadarRed:
                //    foreach (Player p in playerList.Values)
                //    {
                //        if (p.Alive && p.Team == PlayerTeam.Blue)
                //        {
                //            if (p.Content[1] == 1)
                //            {
                //                p.Content[1] = 0;//goes off radar again
                //                SendPlayerContentUpdate(p, 1);
                //            }
                //        }
                //    }
                //    break;

                //case BlockType.RadarBlue:
                //    foreach (Player p in playerList.Values)
                //    {
                //        if (p.Alive && p.Team == PlayerTeam.Red)
                //        {
                //            if (p.Content[1] == 1)
                //            {
                //                p.Content[1] = 0;//goes off radar again
                //                SendPlayerContentUpdate(p, 1);
                //            }
                //        }
                //    }
                //    break;

                case BlockType.InhibitorR:
                    foreach (Player p in playerList.Values)
                    {
                        if (p.Alive && p.Team == PlayerTeam.Blue)
                        {
                            if (p.StatusEffect[1] == 1)
                            {
                                p.StatusEffect[1] = 0;//goes off radar again
                                SendStatusEffectSpecificUpdate(p, 1);
                            }
                        }
                    }
                    break;

                case BlockType.InhibitorB:
                    foreach (Player p in playerList.Values)
                    {
                        if (p.Alive && p.Team == PlayerTeam.Red)
                        {
                            if (p.StatusEffect[1] == 1)
                            {
                                p.StatusEffect[1] = 0;//goes off radar again
                                SendStatusEffectSpecificUpdate(p, 1);
                            }
                        }
                    }
                    break;

                case BlockType.ConstructionR:
                case BlockType.ConstructionB:
                    if (blockListContent[x, y, z, 0] == (byte)BlockType.ArtCaseR || blockListContent[x, y, z, 0] == (byte)BlockType.ArtCaseB)
                    {
                        if (y < MAPSIZE - 1)
                            if (blockList[x, y + 1, z] == BlockType.Vacuum)
                                blockList[x, y + 1, z] = BlockType.None;//restore vacuum to normal
                    }
                    break;

                case BlockType.ArtCaseR:
                case BlockType.ArtCaseB:
                    {
                        if (y < MAPSIZE - 1)
                            if (blockList[x, y + 1, z] == BlockType.ForceR || blockList[x, y + 1, z] == BlockType.ForceB)
                            {
                                SetBlock(x, (ushort)(y + 1), z, BlockType.None, PlayerTeam.None);//remove field
                            }

                        if (blockListContent[x, y, z, 6] > 0)
                        {
                            uint arty = (uint)(blockListContent[x, y, z, 6]);
                            itemList[arty].Content[6] = 0;//unlock arty
                            SendItemContentSpecificUpdate(itemList[(uint)(blockListContent[x, y, z, 6])], 6);

                            ArtifactTeamBonus(blockCreatorTeam[x,y,z], itemList[arty].Content[10], false);

                            if (blockList[x, y, z] == BlockType.ArtCaseB)
                            {
                                teamArtifactsBlue--;
                                SendScoreUpdate();
                            }
                            else if (blockList[x, y, z] == BlockType.ArtCaseR)
                            {
                                teamArtifactsRed--;
                                SendScoreUpdate();
                            }
                        }
                    }
                    break;
                case BlockType.Diamond:
                    {
                        SetItem(ItemType.Diamond, new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), Vector3.Zero, Vector3.Zero, PlayerTeam.None, 0, 0);
                    }
                    break;
                default:
                    break;
            }
        }
     
        public void ConstructItem(Player player, Vector3 playerPosition, Vector3 playerHeading, BlockType blockType, int range, params int[] val)
        {
            if (player.StatusEffect[1] > 0)//inhibited
                return;

            bool actionFailed = false;
          
            // If there's no surface within range, bail.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            ItemType itype = (ItemType)blockType;

            if(itype != ItemType.Target)
            if (!RayCollision(playerPosition, playerHeading, range, 25, ref hitPoint, ref buildPoint, BlockType.Water))
                actionFailed = true;

            // If the block is too expensive, bail.
            uint blockCost = (uint)ItemInformation.GetCost(itype);

            if (varGetB("sandbox"))// && blockCost <= player.OreMax)
                blockCost = 0;
            if (blockCost > player.Ore)
            {
                actionFailed = true;
                //ConsoleWrite("bc:" + blockCost + " " + itype + " " + player.Ore);
                SendPlayerOreWarning(player);
            }

            if (!allowItem[(byte)player.Team, (byte)player.Class, (byte)itype])
                actionFailed = true;

            // If there's someone there currently, bail.
            ushort x = (ushort)buildPoint.X;
            ushort y = (ushort)buildPoint.Y;
            ushort z = (ushort)buildPoint.Z;

            if (itype != ItemType.Target)
            {
                if (!actionFailed)
                    foreach (Player p in playerList.Values)
                    {
                        if ((int)p.Position.X == x && (int)p.Position.Z == z && ((int)p.Position.Y == y || (int)p.Position.Y - 1 == y))
                            actionFailed = true;
                    }

                // If it's out of bounds, bail.
                if (x <= 0 || y <= 0 || z <= 0 || (int)x > MAPSIZE - 1 || (int)y >= MAPSIZE - 1 || (int)z > MAPSIZE - 1)//y >= prevent blocks going too high on server
                    actionFailed = true;
                //if (y < (int)(player.Position.Y-1))
                //{
                //    actionFailed = true;//may not place blocks directly below yourself
                //}

                // If it's lava, don't let them build off of lava.
                if (itype != ItemType.Target)
                    if (blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Lava || blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Water || blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.TransRed || blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.TransBlue || blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Vacuum || blockList[(ushort)buildPoint.X, (ushort)buildPoint.Y, (ushort)buildPoint.Z] == BlockType.Vacuum || blockList[(ushort)buildPoint.X, (ushort)buildPoint.Y, (ushort)buildPoint.Z] == BlockType.TransRed || blockList[(ushort)buildPoint.X, (ushort)buildPoint.Y, (ushort)buildPoint.Z] == BlockType.TransBlue)
                        actionFailed = true;

                if (varGetB("sandbox") && itype == ItemType.Spikes)// && blockCost <= player.OreMax)
                {
                    SendServerMessageToPlayer("You may not place spikes in sandbox mode.", player.NetConn);
                    actionFailed = true;
                }
                else if(SiegeBuild)
                {
                    SendServerMessageToPlayer("You may not place items during this phase.", player.NetConn);
                    actionFailed = true;
                }

            }
            if (actionFailed)
            {
                // Decharge the player's gun.
                TriggerConstructionGunAnimation(player, -0.2f);
            }
            else
            {
                // Fire the player's gun.
                TriggerConstructionGunAnimation(player, 0.5f);

                // Build the block.
                // if (blockType == BlockType.Lava)
                //blockType = BlockType.Fire;

                player.Ore -= blockCost;
                SendOreUpdate(player);
                //SendResourceUpdate(player);
                switch (itype)
                {
                    case ItemType.Spikes:
                        {
                            uint icreate = SetItem(itype, new Vector3((int)buildPoint.X + 0.5f, (int)buildPoint.Y, (int)buildPoint.Z + 0.5f), player.Heading, Vector3.Zero, player.Team, 0, 0);
                            blockListAttach[(int)buildPoint.X, (int)buildPoint.Y, (int)buildPoint.Z, 0] = 2;
                            blockListAttach[(int)buildPoint.X, (int)buildPoint.Y, (int)buildPoint.Z, 1] = (int)icreate;
                            // Play the sound.
                            PlaySound(InfiniminerSound.ConstructionGun, player.Position);
                        }
                        break;

                    case ItemType.Target:
                        {
                            uint icreate = SetItem(itype, new Vector3((float)val[1] / 100, (float)val[2] / 100, (float)val[3] / 100), player.Heading, Vector3.Zero, player.Team, val[0], 0);
                            float sca = (float)val[4] / 100;

                            if (sca > 1.0f)
                                sca = 1.0f;
                            else if (sca < 0.2f)
                                sca = 0.2f;
                            itemList[icreate].Scale = sca;

                            SendItemScaleUpdate(itemList[icreate]);
                            // Play the sound.
                            PlaySound(InfiniminerSound.ConstructionGun, player.Position);
                        }
                        break;

                    case ItemType.DirtBomb:
                        {
                            uint icreate = SetItem(itype, new Vector3((int)buildPoint.X + 0.5f, (int)buildPoint.Y, (int)buildPoint.Z + 0.5f), player.Heading, Vector3.Zero, player.Team, 0, 0);
                            // Play the sound.
                            PlaySound(InfiniminerSound.ConstructionGun, player.Position);
                        }
                        break;
                }
            }
        }

        public void UseConstructionGun(Player player, Vector3 playerPosition, Vector3 playerHeading, BlockType blockType)
        {
            if (player.StatusEffect[1] > 0)//inhibited
                return;

            bool actionFailed = false;
            bool constructionRequired = false;
            // If there's no surface within range, bail.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!RayCollision(playerPosition, playerHeading, 6, 25, ref hitPoint, ref buildPoint,BlockType.Water))
                actionFailed = true;

            // If the block is too expensive, bail.
            uint blockCost = BlockInformation.GetCost(blockType);
            
            if (varGetB("sandbox"))// && blockCost <= player.OreMax)
                blockCost = 0;

            if (blockCost > player.Ore)
            {
                if (SiegeBuild == true)
                {
                    if (blockType == BlockType.ConstructionB || blockType == BlockType.ConstructionR || blockType == BlockType.ArtCaseB || blockType == BlockType.ArtCaseR)
                    {
                        SendServerMessageToPlayer("You may not place artifact safe houses in this phase.", player.NetConn);
                        actionFailed = true;
                    }
                    if (player.Team == PlayerTeam.Red)
                    {
                        if (teamOreRed < blockCost)
                        {
                            actionFailed = true;
                            SendPlayerOreWarning(player);
                        }
                    }
                    else
                    {
                        if (teamOreBlue < blockCost)
                        {
                            actionFailed = true;
                            SendPlayerOreWarning(player);
                        }
                    }
                }
                else
                {
                    actionFailed = true;
                    SendPlayerOreWarning(player);
                }
            }
            if (!allowBlock[(byte)player.Team, (byte)player.Class, (byte)blockType])
                actionFailed = true;
            // If there's someone there currently, bail.
            ushort x = (ushort)buildPoint.X;
            ushort y = (ushort)buildPoint.Y;
            ushort z = (ushort)buildPoint.Z;

            if(!actionFailed)
            foreach (Player p in playerList.Values)
            {
                if ((int)p.Position.X == x && (int)p.Position.Z == z && ((int)p.Position.Y == y || (int)p.Position.Y - 1 == y))
                    actionFailed = true;
            }

            // If it's out of bounds, bail.
            if (x <= 0 || y <= 0 || z <= 0 || (int)x > MAPSIZE - 1 || (int)y >= MAPSIZE - 1 || (int)z > MAPSIZE - 1)//y >= prevent blocks going too high on server
                actionFailed = true;

            //if (y < (int)(player.Position.Y-1))
            //{
            //    actionFailed = true;//may not place blocks directly below yourself
            //}

            // If it's lava, don't let them build off of lava.
            if (blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Lava || blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Water)
                actionFailed = true;

            if(!actionFailed)
                switch (blockType)
                {
                    case BlockType.ArtCaseR:
                    case BlockType.ArtCaseB:
                        {
                            constructionRequired = true;
                            //if (blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y + 1, (ushort)hitPoint.Z] != BlockType.None)
                            //{
                            //    actionFailed = true;
                            //}
                            //else
                            //{
                            //    SetBlock(x, (ushort)(y+1), z, BlockType.Vacuum, player.Team);//space for artifact
                            //}
                        }
                        break;
                    case BlockType.InhibitorR:
                    case BlockType.InhibitorB:
                        for (int ax = -INHIBITOR_RANGE + x; ax <= INHIBITOR_RANGE + x; ax++)
                            for (int ay = -INHIBITOR_RANGE + y; ay <= INHIBITOR_RANGE + y; ay++)
                                for (int az = -INHIBITOR_RANGE + z; az <= INHIBITOR_RANGE + z; az++)
                                {
                                    if (ax < MAPSIZE - 1 && ay < MAPSIZE - 1 && az < MAPSIZE - 1 && ax > 0 && ay > 0 && az > 0)
                                    {
                                        if (blockList[ax, ay, az] == BlockType.InhibitorR || blockList[ax, ay, az] == BlockType.InhibitorB)
                                        {
                                            SendServerMessageToPlayer("You may not place an inhibitor within the range of another.", player.NetConn);
                                            actionFailed = true;
                                            ax = INHIBITOR_RANGE + x + 1;
                                            ay = INHIBITOR_RANGE + y + 1;
                                            az = INHIBITOR_RANGE + z + 1;
                                        }
                                    }
                                }
                        break;
                    case BlockType.Maintenance:
                        for (int ax = -3 + x; ax <= 3 + x; ax++)
                            for (int ay = -3 + y; ay <= 3 + y; ay++)
                                for (int az = -3 + z; az <= 3 + z; az++)
                                {
                                    if (ax < MAPSIZE - 1 && ay < MAPSIZE - 1 && az < MAPSIZE - 1 && ax > 0 && ay > 0 && az > 0)
                                    {
                                        if (blockList[ax, ay, az] == BlockType.Maintenance)
                                        {
                                            SendServerMessageToPlayer("You may not place a maintenance array within the range of another.", player.NetConn);
                                            actionFailed = true;
                                            ax = 4 + x;
                                            ay = 4 + y;
                                            az = 4 + z;
                                        }
                                    }
                                }
                        break;
                    case BlockType.Explosive:
                        //if (player.ExplosiveList.Count > 2)
                        //{
                        //    SendServerMessageToPlayer("You may not have more than two unprimed explosives at once.", player.NetConn);
                        //}
                        //else
                        //{
                            //for (int ax = -3 + x; ax <= 3 + x; ax++)
                            //    for (int ay = -3 + y; ay <= 3 + y; ay++)
                            //        for (int az = -3 + z; az <= 3 + z; az++)
                            //        {
                            //            if (ax < MAPSIZE - 1 && ay < MAPSIZE - 1 && az < MAPSIZE - 1 && ax > 0 && ay > 0 && az > 0)
                            //            {
                            //                if (blockList[ax, ay, az] == BlockType.Explosive && blockCreatorTeam[ax,ay,az] == player.Team)
                            //                {
                            //                    SendServerMessageToPlayer("Explosives already reside here.", player.NetConn);
                            //                    actionFailed = true;
                            //                    ax = 4 + x;
                            //                    ay = 4 + y;
                            //                    az = 4 + z;
                            //                }
                            //            }
                            //        }
                        //}
                        break;
                    default:
                        break;
                }

            if (actionFailed)
            {
                // Decharge the player's gun.
                TriggerConstructionGunAnimation(player, -0.2f);
            }
            else
            {
                // Fire the player's gun.
                TriggerConstructionGunAnimation(player, 0.5f);

                // Build the block.
               // if (blockType == BlockType.Lava)
                    //blockType = BlockType.Fire;

                if (constructionRequired == true)//block changes into construction block with blocktype on content[0]
                {
                    if (blockType == BlockType.ArtCaseR || blockType == BlockType.ArtCaseB)
                    {
                        //check above for space
                        if (blockList[x, y+1, z] == BlockType.None)
                        {
                            blockList[x, y+1, z] = BlockType.Vacuum;//player cant see, dont bother updating for client
                        }
                        else
                        {
                            return;//wasnt space for the glass
                        }
                    }

                    if (player.Team == PlayerTeam.Red)
                    {
                        SetBlock(x, y, z, BlockType.ConstructionR, player.Team);
                    }
                    else if (player.Team == PlayerTeam.Blue)
                    {
                        SetBlock(x, y, z, BlockType.ConstructionB, player.Team);
                    }
                    blockListHP[x, y, z] = BlockInformation.GetHP(blockType);//base block hp
                    blockListContent[x, y, z, 0] = (byte)blockType;

                    if (blockType == BlockType.ArtCaseR || blockType == BlockType.ArtCaseB)
                    {
                        blockListContent[x, y, z, 6] = 0;
                    }
                }
                else
                {
                    switch(blockType)
                    {
                        case BlockType.Metal:
                            {
                                SetBlock(x, y, z, blockType, PlayerTeam.None);
                            }
                            break;
                        case BlockType.Refinery:
                            {
                                if (ResearchComplete[(byte)player.Team, (byte)Research.OreRefinery] < 32)
                                    ResearchComplete[(byte)player.Team, (byte)Research.OreRefinery] += 2;
                                else
                                    ResearchComplete[(byte)player.Team, (byte)Research.OreRefinery] += 1;

                                NetBuffer msgBuffer = netServer.CreateBuffer();
                                msgBuffer = netServer.CreateBuffer();
                                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                                if (player.Team == PlayerTeam.Red)
                                {
                                    msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                                }
                                else if (player.Team == PlayerTeam.Blue)
                                {
                                    msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);
                                }

                                msgBuffer.Write(Defines.Sanitize("We now have +" + ResearchComplete[(byte)player.Team, (byte)Research.OreRefinery] + " ore efficiency!"));

                                foreach (NetConnection netConn in playerList.Keys)
                                    if (netConn.Status == NetConnectionStatus.Connected)
                                        if(playerList[netConn].Team == player.Team)
                                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

                                SetBlock(x, y, z, blockType, player.Team);
                            }
                            break;
                        case BlockType.Plate:
                            {
                                SetBlock(x, y, z, blockType, player.Team);
                                blockListContent[x, y, z, 5] = (byte)BlockType.Plate;
                            }
                            break;
                        case BlockType.Explosive:
                            {
                                SetBlock(x, y, z, blockType, player.Team);
                                blockListHP[x, y, z] = 1 + ResearchComplete[(byte)player.Team, (byte)Research.Fortify] * 4;
                                //player.ExplosiveList.Add(new Vector3(x, y, z));
                            }
                            break;
                        case BlockType.SolidRed:
                            {
                                if(SiegeBuild == true)
                                {
                                    SetBlock(x, y, z, BlockType.SolidRed2, player.Team);
                                }
                                else
                                {
                                    SetBlock(x, y, z, BlockType.SolidRed, player.Team);
                                }
                            }
                            break;
                        case BlockType.SolidBlue:
                            {
                                if (SiegeBuild == true)
                                {
                                    SetBlock(x, y, z, BlockType.SolidBlue2, player.Team);
                                }
                                else
                                {
                                    SetBlock(x, y, z, BlockType.SolidBlue, player.Team);
                                }
                            }
                            break;
                        default:
                            SetBlock(x, y, z, blockType, player.Team);
                            break;
                    }
                    if (BlockInformation.GetMaxHP(blockType) > 0 && blockType != BlockType.Explosive)
                    {
                        blockListHP[x, y, z] = BlockInformation.GetHP(blockType);//base block hp
                    }
                }

                if (player.deathCount > 2)
                {
                    player.deathCount -= 2;
                    //ConsoleWrite("" + player.deathCount);
                }

                if (SiegeBuild == true)
                {
                    if (player.Team == PlayerTeam.Red)
                    {
                        teamOreRed -= (int)blockCost;
                        foreach (NetConnection netConn in playerList.Keys)
                            if (netConn.Status == NetConnectionStatus.Connected)
                                if (playerList[netConn].Team == player.Team)
                                    SendTeamOreUpdate(player);
                    }
                    else
                    {
                        teamOreBlue -= (int)blockCost;
                        foreach (NetConnection netConn in playerList.Keys)
                            if (netConn.Status == NetConnectionStatus.Connected)
                                if (playerList[netConn].Team == player.Team)
                                    SendTeamOreUpdate(player);
                    }
                }
                else
                {
                    player.Score += blockCost / 10;
                    player.Exp += blockCost / 10;
                    player.Ore -= blockCost;
                    SendOreUpdate(player);
                }
                //SendResourceUpdate(player);

                // Play the sound.
                PlaySound(InfiniminerSound.ConstructionGun, player.Position);
                
            }            
        }

        public void UseDeconstructionGun(Player player, Vector3 playerPosition, Vector3 playerHeading)
        {
            bool actionFailed = false;

            // If there's no surface within range, bail.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!RayCollisionDig(playerPosition, playerHeading, 6, 25, ref hitPoint, ref buildPoint, BlockType.Water, player.Team == PlayerTeam.Red ? BlockType.TransRed : BlockType.TransBlue))
                actionFailed = true;
            ushort x = (ushort)hitPoint.X;
            ushort y = (ushort)hitPoint.Y;
            ushort z = (ushort)hitPoint.Z;

            // If this is another team's block, bail.
            if (blockCreatorTeam[x, y, z] != player.Team)
                actionFailed = true;

            BlockType blockType = blockList[x, y, z];
            if (!(blockType == BlockType.SolidBlue ||
                blockType == BlockType.SolidRed ||
                blockType == BlockType.InhibitorR ||
                blockType == BlockType.InhibitorB ||
                blockType == BlockType.SolidBlue2 ||
                blockType == BlockType.SolidRed2 ||
                blockType == BlockType.BankBlue ||
                blockType == BlockType.BankRed ||
                blockType == BlockType.ArtCaseR ||
                blockType == BlockType.ArtCaseB ||
                blockType == BlockType.Jump ||
                blockType == BlockType.Ladder ||
                blockType == BlockType.Road ||
                blockType == BlockType.Shock ||
                blockType == BlockType.Explosive ||
                blockType == BlockType.ResearchR ||
                blockType == BlockType.ResearchB ||
                blockType == BlockType.BeaconRed ||
                blockType == BlockType.BeaconBlue ||
                blockType == BlockType.Water ||
                blockType == BlockType.TransBlue ||
                blockType == BlockType.TransRed ||
                blockType == BlockType.GlassR ||
                blockType == BlockType.GlassB ||
                blockType == BlockType.Generator ||
                blockType == BlockType.Pipe ||
                blockType == BlockType.Pump ||
                blockType == BlockType.RadarBlue ||
                blockType == BlockType.RadarRed ||
                blockType == BlockType.Maintenance ||
                blockType == BlockType.Barrel ||
                blockType == BlockType.Hinge ||
                blockType == BlockType.Lever ||
                blockType == BlockType.Plate ||
                blockType == BlockType.MedicalR ||
                blockType == BlockType.MedicalB ||
                blockType == BlockType.ConstructionR ||
                blockType == BlockType.ConstructionB ||
                blockType == BlockType.Refinery ||
                blockType == BlockType.Controller ||
                blockType == BlockType.Water ||
                blockType == BlockType.StealthBlockB ||
                blockType == BlockType.StealthBlockR ||
                blockType == BlockType.TrapB ||
                blockType == BlockType.TrapR 
                ))
                actionFailed = true;

            if (actionFailed)
            {
                // Decharge the player's gun.
                TriggerConstructionGunAnimation(player, -0.2f);
            }
            else
            {
                player.Annoying++;
                // Fire the player's gun.
                TriggerConstructionGunAnimation(player, 0.5f);

                if(!SiegeBuild)
                if (blockType == BlockType.ArtCaseB)
                {
                    if (blockListContent[x, y, z, 7] == 0)
                    {
                        SendServerMessageBlue("Artifact safe removed by " + player.Handle + ".");
                    }
                    else
                    {
                        SendServerMessageToPlayer("This artifact safe is secured, it may not be deconstructed.", player.NetConn);
                        return;
                    }
                }
                else if (blockType == BlockType.ArtCaseR)
                {
                    if (blockListContent[x, y, z, 7] == 0)
                    {
                        SendServerMessageRed("Artifact safe removed by " + player.Handle + ".");
                    }
                    else
                    {
                        SendServerMessageToPlayer("This artifact safe is secured, it may not be deconstructed.", player.NetConn);
                        return;
                    }
                }

                SpecialRemove(x, y, z);
                // Remove the block.
                if (SiegeBuild == true)
                {
                    if (player.Team == PlayerTeam.Red)
                    {
                        teamOreRed += (int)BlockInformation.GetCost(blockType);
                        foreach (NetConnection netConn in playerList.Keys)
                            if (netConn.Status == NetConnectionStatus.Connected)
                                if (playerList[netConn].Team == player.Team)
                                    SendTeamOreUpdate(player);
                    }
                    else if (player.Team == PlayerTeam.Blue)
                    {
                        teamOreBlue += (int)BlockInformation.GetCost(blockType);
                        foreach (NetConnection netConn in playerList.Keys)
                            if (netConn.Status == NetConnectionStatus.Connected)
                                if (playerList[netConn].Team == player.Team)
                                    SendTeamOreUpdate(player);
                    }
                }

                SetBlock(x, y, z, BlockType.None, PlayerTeam.None);
                PlaySound(InfiniminerSound.ConstructionGun, player.Position);
            }
        }

        public void TriggerConstructionGunAnimation(Player player, float animationValue)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TriggerConstructionGunAnimation);
            msgBuffer.Write(animationValue);
            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void UseSignPainter(Player player, Vector3 playerPosition, Vector3 playerHeading, int val, int x, int y, int z, int size)
        {
            // If there's no surface within range, bail.
            //Vector3 hitPoint = Vector3.Zero;
            //Vector3 buildPoint = Vector3.Zero;
            //if (!RayCollision(playerPosition, playerHeading, 20, 25, ref hitPoint, ref buildPoint))
            //    return;
            //ushort x = (ushort)hitPoint.X;
            //ushort y = (ushort)hitPoint.Y;
            //ushort z = (ushort)hitPoint.Z;

            ConstructItem(player, playerPosition, playerHeading, (BlockType)ItemType.Target, 30, val, x, y, z, size);
            //if (blockList[x, y, z] == BlockType.Dirt)
            //{
            //    SetBlock(x, y, z, BlockType.DirtSign, PlayerTeam.None);
            //    PlaySound(InfiniminerSound.ConstructionGun, player.Position);
            //}
            //else if (blockList[x, y, z] == BlockType.DirtSign)
            //{
            //    SetBlock(x, y, z, BlockType.Dirt, PlayerTeam.None);
            //    PlaySound(InfiniminerSound.ConstructionGun, player.Position);
            //}
        }

        public void ExplosionEffectAtPoint(float x, float y, float z, int strength, PlayerTeam team)
        {
            //SetBlock((ushort)x, (ushort)y, (ushort)z, BlockType.Fire, PlayerTeam.None);//might be better at detonate
            //blockListContent[x, y, z, 0] = 6;//fire gets stuck?
            double dist = 0.0f;
            uint damage = 0;
            damage = (uint)((strength * (20 + ResearchComplete[(byte)team, (byte)Research.Destruction] * 4)) - (dist * 20));//10 dmg per dist

            damage += (uint)(artifactActive[(byte)team, 13] * 10);//explosive artifact

            if (damage > artifactActive[(byte)team, 14] * 20)
            {
                damage -= (uint)(artifactActive[(byte)team, 14] * 20);
            }
            else
            {
                damage = 1;
            }

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected && playerList[netConn].Alive)
                {
                    if(playerList[netConn].Alive)
                    if (playerList[netConn].Team != team && playerList[netConn].StatusEffect[4] == 0 && playerList[netConn].Content[10] != 14)//14 armor artifact
                    {
                        dist = Distf(playerList[netConn].Position, new Vector3(x, y, z));
                        if (dist <= strength)//player in range of bomb on server?
                        {
                            if (playerList[netConn].Health > damage)
                            {
                                playerList[netConn].Health -= damage;
                                SendHealthUpdate(playerList[netConn]);

                                NetBuffer msgBufferB = netServer.CreateBuffer();
                                msgBufferB.Write((byte)InfiniminerMessage.PlayerSlap);
                                msgBufferB.Write(playerList[netConn].ID);//getting slapped
                                msgBufferB.Write((uint)0);//attacker
                                SendHealthUpdate(playerList[netConn]);

                                foreach (NetConnection netConnB in playerList.Keys)
                                    if (netConnB.Status == NetConnectionStatus.Connected)
                                        netServer.SendMessage(msgBufferB, netConnB, NetChannel.ReliableUnordered);
                            }
                            else
                            {
                                Player_Dead(playerList[netConn],"blew up!");
                            }
                        }
                    }
                }

            // Send off the explosion to clients.
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TriggerExplosion);
            msgBuffer.Write(new Vector3(x, y, z));
            msgBuffer.Write(strength);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
        }

        public void EffectAtPoint(Vector3 pos, uint efftype)//integer designed to be blocked inside block
        {

            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.Effect);
            msgBuffer.Write(pos);
            msgBuffer.Write(efftype);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
        }

        public void DebrisEffectAtPoint(int x, int y, int z, BlockType block, int efftype)//integer designed to be blocked inside block
        {
            //0 = hit
            //1 = block specific effect
            
            /*
             Vector3 blockPos = msgBuffer.ReadVector3();
             BlockType blockType = (BlockType)msgBuffer.ReadByte();
             uint debrisType = msgBuffer.ReadUInt32();
             */
            // Send off the explosion to clients.

            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TriggerDebris);
            msgBuffer.Write(new Vector3(x+0.5f, y+0.5f, z+0.5f));
            msgBuffer.Write((byte)(block));
            msgBuffer.Write(efftype);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
            //Or not, there's no dedicated function for this effect >:(
        }
        public void DebrisEffectAtPoint(float x, float y, float z, BlockType block, int efftype)//float is exact
        {
            //0 = hit
            //1 = block specific effect

            /*
             Vector3 blockPos = msgBuffer.ReadVector3();
             BlockType blockType = (BlockType)msgBuffer.ReadByte();
             uint debrisType = msgBuffer.ReadUInt32();
             */
            // Send off the explosion to clients.
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TriggerDebris);
            msgBuffer.Write(new Vector3(x, y, z));
            msgBuffer.Write((byte)(block));
            msgBuffer.Write(efftype);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
            //Or not, there's no dedicated function for this effect >:(
        }
        public void EarthquakeEffectAtPoint(int x, int y, int z, int strength)
        {
            // Send off the explosion to clients.
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TriggerEarthquake);
            msgBuffer.Write(new Vector3(x, y, z));
            msgBuffer.Write(strength);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
        }

        public void MaterialBombAtPoint(int x, int y, int z, PlayerTeam team, BlockType material)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)//-1
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (x + dx <= 0 || y + dy <= 0 || z + dz <= 0 || x + dx > MAPSIZE - 1 || y + dy > MAPSIZE - 1 || z + dz > MAPSIZE - 1)
                            continue;

                        if(dx == -1 && dz == -1 && dy == 1)
                            continue;

                        if (dx == 1 && dz == 1 && dy == 1)
                            continue;

                        if (dx == -1 && dz == 1 && dy == 1)
                            continue;

                        if (dx == 1 && dz == -1 && dy == 1)
                            continue;

                        if (blockList[x + dx, y + dy, z + dz] == BlockType.None)
                            SetBlock((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.Dirt, PlayerTeam.None);
                                        
                    }
        }

        public void BombAtPoint(int x, int y, int z, PlayerTeam team)
        {
            ExplosionEffectAtPoint(x, y, z, 4, team);
            int damage = 5 + ResearchComplete[(byte)team, (byte)Research.Destruction] * 2;

            damage += artifactActive[(byte)team, 13] * 10;
            int tdamage = damage;

            if (team == PlayerTeam.Red)
            {
                if (artifactActive[(byte)PlayerTeam.Blue, 14] > 0)
                {
                    tdamage -= artifactActive[(byte)PlayerTeam.Blue, 14] * 10;
                    
                    if(tdamage < 5)
                        tdamage = 5;
                }
            }
            else if(team == PlayerTeam.Blue)
            {
                if (artifactActive[(byte)PlayerTeam.Red, 14] > 0)
                {
                    tdamage -= artifactActive[(byte)PlayerTeam.Red, 14] * 10;

                    if (tdamage < 5)
                        tdamage = 5;
                }
            }
            
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = 0; dy <= 1; dy++)//-1
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (x + dx <= 0 || y + dy <= 0 || z + dz <= 0 || x + dx > MAPSIZE - 1 || y + dy > MAPSIZE - 1 || z + dz > MAPSIZE - 1)
                            continue;

                        //if (blockList[x + dx, y + dy, z + dz] == BlockType.Explosive)
                          //  if (blockCreatorTeam[x + dx, y + dy, z + dz] != team)//must hit opposing team
                           //     DetonateAtPoint(x + dx, y + dy, z + dz);
                        if(BlockInformation.GetMaxHP(blockList[x + dx, y + dy, z + dz]) > 0)//not immune block
                            if (blockCreatorTeam[x + dx, y + dy, z + dz] != team)//must hit opposing team
                            if (blockListHP[x + dx, y + dy, z + dz] > 0)
                            {
                                if (blockList[x + dx, y + dy, z + dz] == BlockType.Gold || blockList[x + dx, y + dy, z + dz] == BlockType.Diamond)
                                {//these blocks immune to explosives
                                    
                                }
                                else
                                {

                                    if (team != PlayerTeam.None)
                                    {
                                        blockListHP[x + dx, y + dy, z + dz] -= tdamage;
                                    }
                                    else
                                    {
                                        blockListHP[x + dx, y + dy, z + dz] -= damage;
                                    }

                                    if (blockListHP[x + dx, y + dy, z + dz] <= 0)
                                    {
                                        blockListHP[x + dx, y + dy, z + dz] = 0;
                                        if (blockList[x + dx, y + dy, z + dz] == BlockType.ArtCaseR)
                                            SendServerMessageRed("The enemy has breached an artifact safe!");
                                        else if (blockList[x + dx, y + dy, z + dz] == BlockType.ArtCaseB)
                                            SendServerMessageBlue("The enemy has breached an artifact safe!");
                                        SpecialRemove((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz));

                                        SetBlockDebris((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                                    }
                                    else
                                    {
                                        if (blockList[x + dx, y + dy, z + dz] == BlockType.Rock)//rock is weak to explosives
                                        {//item creation must be outside item loop
                                            SetBlockDebris((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                                        }
                                    }
                                }
                        }
                    }
        }

        public void DetonateAtPoint(int x, int y, int z, PlayerTeam team, bool remove)
        {
            int damage = 20 + ResearchComplete[(byte)team, (byte)Research.Destruction] * 10;

            damage += artifactActive[(byte)team, 13] * 10;

            int tdamage = damage;

            if (team == PlayerTeam.Red)
            {
                if (artifactActive[(byte)PlayerTeam.Blue, 14] > 0)
                {
                    tdamage -= artifactActive[(byte)PlayerTeam.Blue, 14] * 20;

                    if (tdamage < 5)
                        tdamage = 5;
                }
            }
            else if (team == PlayerTeam.Blue)
            {
                if (artifactActive[(byte)PlayerTeam.Red, 14] > 0)
                {
                    tdamage -= artifactActive[(byte)PlayerTeam.Red, 14] * 20;

                    if (tdamage < 5)
                        tdamage = 5;
                }
            }


            // Remove the block that is detonating.
            if(remove)
            SetBlock((ushort)(x), (ushort)(y), (ushort)(z), BlockType.None, PlayerTeam.None);

            // Remove this from any explosive lists it may be in.
            if(remove)
            foreach (Player p in playerList.Values)
                p.ExplosiveList.Remove(new Vector3(x, y, z));

                int radius = (int)Math.Ceiling((double)varGetI("explosionradius"));
                int size = radius * 2 + 1;
                int center = radius+1;
                //ConsoleWrite("Radius: " + radius + ", Size: " + size + ", Center: " + center);

                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = 0; dy <= 2; dy++)//-1
                        for (int dz = -2; dz <= 2; dz++)
                        {
                            if (x + dx <= 0 || y + dy <= 0 || z + dz <= 0 || x + dx > MAPSIZE - 1 || y + dy > MAPSIZE - 1 || z + dz > MAPSIZE - 1)
                                continue;

                            if (BlockInformation.GetMaxHP(blockList[x + dx, y + dy, z + dz]) > 0)//not immune block
                                if (blockCreatorTeam[x + dx, y + dy, z + dz] != team)//must hit opposing team
                                    if (blockListHP[x + dx, y + dy, z + dz] > 0)
                                    {

                                        if (blockList[x + dx, y + dy, z + dz] == BlockType.Gold || blockList[x + dx, y + dy, z + dz] == BlockType.Diamond)
                                        {//these blocks immune to explosives

                                        }
                                        else
                                        {

                                            if (team != PlayerTeam.None)
                                            {
                                                blockListHP[x + dx, y + dy, z + dz] -= tdamage;
                                            }
                                            else
                                            {
                                                blockListHP[x + dx, y + dy, z + dz] -= damage;
                                            }


                                            if (blockListHP[x + dx, y + dy, z + dz] <= 0)
                                            {
                                                blockListHP[x + dx, y + dy, z + dz] = 0;
                                                if (blockList[x + dx, y + dy, z + dz] == BlockType.ArtCaseR)
                                                    SendServerMessageRed("The enemy has breached an artifact safe!");
                                                else if (blockList[x + dx, y + dy, z + dz] == BlockType.ArtCaseB)
                                                    SendServerMessageBlue("The enemy has breached an artifact safe!");
                                                SpecialRemove((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz));

                                                SetBlockDebris((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                                            }
                                            else
                                            {
                                                if (blockList[x + dx, y + dy, z + dz] == BlockType.Rock)//rock is weak to explosives
                                                {//item creation must be outside item loop
                                                    SetBlockDebris((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                                                }
                                            }
                                        }
                                    }
                        }
                //for (int dx = -center+1; dx < center; dx++)
                //    for (int dy = -center+1; dy < center; dy++)//- was +1
                //        for (int dz = -center+1; dz < center; dz++)
                //        {
                //            if (tntExplosionPattern[dx+center-1, dy+center-1, dz+center-1]) //Warning, code duplication ahead!
                //            {
                //                // Check that this is a sane block position.
                //                if (x + dx <= 0 || y + dy <= 0 || z + dz <= 0 || x + dx > MAPSIZE - 1 || y + dy > MAPSIZE - 1 || z + dz > MAPSIZE - 1)
                //                    continue;

                //                // Chain reactions!
                //                if (blockList[x + dx, y + dy, z + dz] == BlockType.Explosive)
                //                    DetonateAtPoint(x + dx, y + dy, z + dz);

                //                if (blockList[x + dx, y + dy, z + dz] == BlockType.Rock)//rock is weak to explosives
                //                {//item creation must be outside item loop
                //                    SetBlockDebris((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                //                    continue;
                //                }

                //                if (BlockInformation.GetMaxHP(blockList[x + dx, y + dy, z + dz]) > 0)//not immune block//not weak block
                //                    if (blockCreatorTeam[x + dx, y + dy, z + dz] != team)//must hit opposing team
                //                        if (blockListHP[x + dx, y + dy, z + dz] > 0)
                //                        {

                //                            if (blockList[x + dx, y + dy, z + dz] == BlockType.Gold || blockList[x + dx, y + dy, z + dz] == BlockType.Diamond)
                //                            {//these blocks immune to explosives

                //                            }
                //                            else
                //                            {

                //                                blockListHP[x + dx, y + dy, z + dz] -= 30 + ResearchComplete[(byte)team, (byte)Research.Destruction] * 10;

                //                                if (blockListHP[x + dx, y + dy, z + dz] <= 0)
                //                                {
                //                                    blockListHP[x + dx, y + dy, z + dz] = 0;
                //                                    if (blockList[x + dx, y + dy, z + dz] == BlockType.ArtCaseR)
                //                                    SendServerMessageRed("The enemy has breached an artifact safe!");
                //                                    else if (blockList[x + dx, y + dy, z + dz] == BlockType.ArtCaseB)
                //                                    SendServerMessageBlue("The enemy has breached an artifact safe!");

                //                                    SpecialRemove((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz));

                //                                    SetBlock((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                //                                }
                //                                else
                //                                {
                //                                    if (blockList[x + dx, y + dy, z + dz] == BlockType.Rock)//rock is weak to explosives
                //                                    {
                //                                        SetBlock((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                //                                    }
                //                                }
                //                            }
                //                        }
                //            }
                //        }
            ExplosionEffectAtPoint(x, y, z, size, blockCreatorTeam[x, y, z]);
        }
        public void ArtifactTeamBonus(PlayerTeam team, int cc, bool state)
        {

            NetBuffer msgBuffer;
            string artmessage = "";

            if (state)
            {
                artifactActive[(byte)team, cc]++;
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                if (team == PlayerTeam.Red)
                    msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                else if (team == PlayerTeam.Blue)
                    msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);
                
                artmessage = "We now possess the " + ArtifactInformation.GetName(cc);
            }
            else
            {
                artifactActive[(byte)team, cc]--;
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                if (team == PlayerTeam.Red)
                    msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                else if (team == PlayerTeam.Blue)
                    msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);

                artmessage = "The " + ArtifactInformation.GetName(cc) + " has been lost";
            }

            switch(cc)
            {
                case 1://material artifact
                    if (state)
                    {
                        artmessage += ", regenerating team ore periodically";
                    }
                    else
                    {
                        artmessage += " reducing our periodic ore supply";
                    }
                    break;
                case 2://vampire artifact
                    if (state)
                    {
                        artmessage += ", giving our team minor life stealing attacks";
                    }
                    else
                    {
                        artmessage += " reducing our life stealing attacks";
                    }
                    break;
                case 3://regeneration artifact
                    if (state)
                    {
                        teamRegeneration[(byte)team] += 2;
                        artmessage += ", regenerating faster";
                    }
                    else
                    {
                        teamRegeneration[(byte)team] -= 2;
                        artmessage += " regenerating slower";
                    }
                    break;
           
                case 4://aqua
                    if (artifactActive[(byte)team, cc] < 1)
                    {
                        artmessage += " and we may no longer water breathe or dig underwater";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    else if (artifactActive[(byte)team, cc] == 1)
                    {
                        artmessage += ", we may now breathe and dig underwater";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    break;
                case 5://golden artifact
                    if (state)
                    {
                        artmessage += ", generating periodic gold deposits for our team";
                    }
                    else
                    {
                        artmessage += " reducing our periodic gold supplies";
                    }
                    break;
                case 6://storm artifact
                    if (state)
                    {
                        artmessage += ", granting immunity to any area effects caused by artifacts";
                    }
                    else
                    {
                        if(artifactActive[(byte)team, 6] == 0)
                        artmessage += " making us vulnerable to area effects caused by artifacts";
                    }
                    break;
                case 7://reflect artifact
                    if (state)
                    {
                        artmessage += ", causing our team blocks to reflect a small amount of damage";
                    }
                    else
                    {
                        if (artifactActive[(byte)team, 7] == 0)
                            artmessage += " removing our block damage reflection";
                    }
                    break;
                case 8://medical artifact
                    if (state)
                    {
                        artmessage += ", healing our teams ailments";
                    }
                    else
                    {
                        if (artifactActive[(byte)team, 8] == 0)
                            artmessage += " leaving us without ailment protection";
                    }
                    break;
                case 9://stone artifact
                    if (state)
                    {
                        artmessage += ", reducing any knockbacks";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " leaving us without knockback protection";

                        SendActiveArtifactUpdate(team, cc);
                    }
                    break;
                case 10://tremor artifact
                    if (state)
                    {
                        
                        artmessage += ", boosting our knockback strength";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " taking our knockback with it";

                        SendActiveArtifactUpdate(team, cc);
                    }
                    break;
                case 11://judgement artifact
                    if (state)
                    {

                        artmessage += ", bringing sinners to justice";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " calming our holy vengeance";

                        SendActiveArtifactUpdate(team, cc);
                    }
                    break;
                case 12://bog artifact
                    if (state)
                    {

                        artmessage += ", letting us traverse the marshes!";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " making life hard!";

                        SendActiveArtifactUpdate(team, cc);
                    }
                    break;
                case 13://explosive artifact
                    if (state)
                    {

                        artmessage += ", creating prettier explosions";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " nerfing explosives even more";

                        SendActiveArtifactUpdate(team, cc);
                    }
                    break;
                case 14://armor artifact
                    if (state)
                    {

                        artmessage += ", reducing explosive damage!";
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " making explosives look useful again!";
                    }
                    break;
                case 15://doom artifact
                    if (state)
                    {

                        artmessage += ", giving us a feeling of dread";
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " bringing joy back to life";

                    }
                    break;
                case 16://inferno artifact
                    if (state)
                    {

                        artmessage += ", showing us how to walk on lava";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " bringing back the burning sensations";

                        SendActiveArtifactUpdate(team, cc);
                    }
                    break;
                case 18://wings artifact
                    if (state)
                    {

                        artmessage += ", making gravity less harsh";
                        SendActiveArtifactUpdate(team, cc);
                    }
                    else
                    {
                        if (artifactActive[(byte)team, cc] == 0)
                            artmessage += " teaching us to look before we leap";

                        SendActiveArtifactUpdate(team, cc);
                    }
                    break;
            }

            if (artmessage != "")
            {
                artmessage += "!";
                msgBuffer.Write(Defines.Sanitize(artmessage));
                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn.Status == NetConnectionStatus.Connected && playerList[netConn].Team == team)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
            }
        }
        public void UseDetonator(Player player)
        {
            // Explosive list handling disabled for now
            return;
        }

        public void UseRemote(Player player)
        {
            if (player.Content[5] > 0)
            {
                PlayerInteract(player, (uint)(player.Content[5]), (uint)(player.Content[6]), (uint)(player.Content[7]), (uint)(player.Content[8]));
            }
            else
            {
                SendServerMessageToPlayer("Remote is not attached to anything.", player.NetConn);
            }
        }

        public void Hide(Player player)
        {
            if (player.Class == PlayerClass.Prospector && player.Content[5] == 0 && player.Content[6] > 3)
            {
                player.Content[1] = 0;
                SendPlayerContentUpdate(player, 1);
                player.Content[5] = 1;//no more sight
                SendContentSpecificUpdate(player, 5);
                SendPlayerContentUpdate(player, 5);
                SendServerMessageToPlayer("You are now hidden!", player.NetConn);

                EffectAtPoint(player.Position - Vector3.UnitY * 1.5f, 1);
            }
            else if (player.Class == PlayerClass.Prospector && player.Content[5] == 1)
            {
                // Unhiding is disabled for now
                return;
            }

        }

        public void SetRemote(Player player)
        {
            player.Content[2] = (int)player.Position.X;
            player.Content[3] = (int)player.Position.Y;
            player.Content[4] = (int)player.Position.Z;
            player.Content[5] = 0;
            player.Content[9] = 1;
            SendServerMessageToPlayer("You are now linking an object to the remote.", player.NetConn);
        }

        public void SetRemote(Player player, uint btn, uint x, uint y, uint z)
        {
                if(x > 0 && x < MAPSIZE - 1 && y > 0 && y < MAPSIZE - 1 && z > 0 && z < MAPSIZE - 1)
                {
                    player.Content[5] = (int)btn;
                    player.Content[6] = (int)x;
                    player.Content[7] = (int)y;
                    player.Content[8] = (int)z;
                    SendServerMessageToPlayer("Linked remote to action " + btn + " on " + blockList[x, y, z] + ".", player.NetConn);
               }
        }
        public bool HingeBlockTypes(BlockType block, PlayerTeam team)
        {
            switch (block)
            {
                case BlockType.SolidRed:
                case BlockType.SolidRed2:
                case BlockType.GlassR:
                    return team == PlayerTeam.Red;
                case BlockType.SolidBlue2:
                case BlockType.SolidBlue:
                case BlockType.GlassB:
                    return team == PlayerTeam.Blue;
                case BlockType.Ladder:
                    return true;
                default:
                    break;
            }
            return false;
        }
        public bool HingeBlockTypes(BlockType block)
        {
            switch (block)
            {
                case BlockType.GlassR:
                case BlockType.GlassB:
                case BlockType.Ladder:
                case BlockType.SolidRed:
                case BlockType.SolidRed2:
                case BlockType.SolidBlue2:
                case BlockType.SolidBlue:
                    return true;
                default:
                    return false;
            }
        }
        public bool Trigger(int x, int y, int z, int ox, int oy, int oz, int btn, Player player, int depth)
        {
            depth++;

            if (depth > 29)
            {
                return false;
            }

            //if object can be manipulated by levers, it should always return true if the link should remain persistent
            //if the Trigger function returns false, it will remove the link
            if (player != null)
            if (player.Content[2] > 0)//player is attempting to link something
            {
                if (player.Content[9] == 1 && player.Class == PlayerClass.Engineer)
                {
                    if (x > 0 && x < MAPSIZE - 1 && y > 0 && y < MAPSIZE - 1 && z > 0 && z < MAPSIZE - 1)
                    {
                        if (blockList[x, y, z] == BlockType.ResearchB || blockList[x, y, z] == BlockType.ResearchR || blockList[x, y, z] == BlockType.BaseBlue || blockList[x, y, z] == BlockType.BaseRed || blockList[x, y, z] == BlockType.ArtCaseR || blockList[x, y, z] == BlockType.ArtCaseB || blockList[x, y, z] == BlockType.BankRed || blockList[x, y, z] == BlockType.BankBlue || blockList[x, y, z] == BlockType.Explosive)
                        {
                            player.Content[2] = 0;
                            player.Content[3] = 0;
                            player.Content[4] = 0;
                            player.Content[5] = 0;
                            player.Content[9] = 0;
                            SendServerMessageToPlayer("The remote cannot interface with this block.", player.NetConn);                           
                        }
                        else if(blockCreatorTeam[x,y,z] == player.Team || blockCreatorTeam[x,y,z] == PlayerTeam.None)
                        {
                            player.Content[5] = (int)btn;
                            player.Content[6] = (int)x;
                            player.Content[7] = (int)y;
                            player.Content[8] = (int)z;
                            player.Content[2] = 0;
                            player.Content[3] = 0;
                            player.Content[4] = 0;
                            //player.Content[5] = 0;
                            SendServerMessageToPlayer("Linked remote to action " + btn + " on " + blockList[x, y, z] + ".", player.NetConn);
                            player.Content[9] = 0;
                        }
                        return true;
                    }
                }

                if (blockList[x, y, z] == BlockType.Explosive && player.Class != PlayerClass.Sapper)
                {
                    player.Content[2] = 0;
                    player.Content[3] = 0;
                    player.Content[4] = 0;
                    SendContentSpecificUpdate(player, 2);
                    SendContentSpecificUpdate(player, 3);
                    SendContentSpecificUpdate(player, 4);
                    SendServerMessageToPlayer("You must be a sapper to rig this.", player.NetConn);
                    return false;
                }

                if (x == player.Content[2] && y == player.Content[3] && z == player.Content[4])
                {
                    player.Content[2] = 0;
                    player.Content[3] = 0;
                    player.Content[4] = 0;
                    SendContentSpecificUpdate(player, 2);
                    SendContentSpecificUpdate(player, 3);
                    SendContentSpecificUpdate(player, 4);
                    SendServerMessageToPlayer("Cancelled link.", player.NetConn);
                    return true;
                }

                int freeslot = 9;
                int nb = 0;
                for (nb = 2; nb < 7; nb++)
                {
                    if (blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 1] == x && blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 2] == y && blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 3] == z)
                    {


                        blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6] = 0;
                        blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 1] = 0;
                        blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 2] = 0;
                        blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 3] = 0;
                        blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 4] = 0;//unlinked

                        player.Content[2] = 0;
                        player.Content[3] = 0;
                        player.Content[4] = 0;
                        SendContentSpecificUpdate(player, 2);
                        SendContentSpecificUpdate(player, 3);
                        SendContentSpecificUpdate(player, 4);

                        SendServerMessageToPlayer(blockList[x, y, z] + " was unlinked.", player.NetConn);

                        return true;
                    }
                    else if (blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6] == 0 && freeslot == 9)
                    {
                        freeslot = nb;
                        break;//makes sure that we arent reattaching links over and over
                    }
                }

                if (freeslot == 9)
                    return false;

                if (nb != 7)//didnt hit end of switch-link limit
                {//should check teams and connection to itself
                    //range check

                    if (Distf(new Vector3(x, y, z), new Vector3(player.Content[2], player.Content[3], player.Content[4])) < 10)
                    {
                        if (blockCreatorTeam[x, y, z] == player.Team || blockCreatorTeam[x, y, z] == PlayerTeam.None && BlockInformation.GetMaxHP(blockList[x, y, z]) > 0)
                        {
                            bool allow = true;

                            switch (blockList[x, y, z])
                            {
                                case BlockType.SolidRed2:
                                case BlockType.SolidBlue2:
                                    if(blockCreatorTeam[x, y, z] == PlayerTeam.None)
                                    {
                                        SendServerMessageToPlayer(blockList[player.Content[2], player.Content[3], player.Content[4]] + " will not link to " + blockList[x, y, z] + "!", player.NetConn);
                                        allow = false;
                                    }
                                    break;

                                case BlockType.ResearchR:
                                case BlockType.ResearchB:
                                case BlockType.RadarRed:
                                case BlockType.RadarBlue:
                                case BlockType.MedicalR:
                                case BlockType.MedicalB:
                                case BlockType.Maintenance:
                                case BlockType.MagmaBurst:
                                case BlockType.InhibitorB:
                                case BlockType.InhibitorR:
                                case BlockType.ConstructionB:
                                case BlockType.ConstructionR:
                                case BlockType.BeaconBlue:
                                case BlockType.BeaconRed:
                                case BlockType.ArtCaseB:
                                case BlockType.ArtCaseR:
                                   allow = false;
                                   SendServerMessageToPlayer(blockList[player.Content[2], player.Content[3], player.Content[4]] + " will not link to " + blockList[x, y, z] + "!", player.NetConn);
                                   break;
                            }
                            //Vector3 heading = new Vector3(player.Content[2], player.Content[3], player.Content[4]);
                            //heading -= new Vector3(x, y, z);
                            //heading.Normalize();
                            //if (RayCollision(new Vector3(x, y, z) + heading * 0.4f, heading, (float)(Distf(new Vector3(x, y, z), new Vector3(player.Content[2], player.Content[3], player.Content[4]))), 10, blockList[x, y, z]))
                            //{
                            if (allow == true)
                            {
                                
                                blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 1] = (int)(x);
                                blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 2] = (int)(y);
                                blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 3] = (int)(z);
                                blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6 + 4] = (int)(btn);
                                blockListContent[player.Content[2], player.Content[3], player.Content[4], nb * 6] = 100;
                                SendServerMessageToPlayer(blockList[player.Content[2], player.Content[3], player.Content[4]] + " linked action " + btn + " on " + blockList[x, y, z] + ".", player.NetConn);
                            }
                            //}
                            //else
                            //{
                            //    SendServerMessageToPlayer(blockList[player.Content[2], player.Content[3], player.Content[4]] + " was not in line of sight of " + blockList[x, y, z] + " to link!", player.NetConn);
                            //}
                        }
                        else
                        {
                            SendServerMessageToPlayer(blockList[player.Content[2], player.Content[3], player.Content[4]] + " will not link to " + blockList[x, y, z] + "!", player.NetConn);
                        }
                    }
                    else
                    {
                        SendServerMessageToPlayer(blockList[player.Content[2], player.Content[3], player.Content[4]] + " was too far away from the " + blockList[x, y, z] + " to link!", player.NetConn);
                    }
                    player.Content[2] = 0;
                    player.Content[3] = 0;
                    player.Content[4] = 0;
                    SendContentSpecificUpdate(player, 2);
                    SendContentSpecificUpdate(player, 3);
                    SendContentSpecificUpdate(player, 4);
                }
                else
                {
                    SendServerMessageToPlayer("Too overloaded to link more objects.", player.NetConn);
                    player.Content[2] = 0;
                    player.Content[3] = 0;
                    player.Content[4] = 0;
                    SendContentSpecificUpdate(player, 2);
                    SendContentSpecificUpdate(player, 3);
                    SendContentSpecificUpdate(player, 4);
                }
                return true;
            }

            //beginning of trigger actions
            if (blockList[x, y, z] == BlockType.Pipe)
            {
//                ConsoleWrite("Chain connected to src:" + blockListContent[x, y, z, 1] + " src: " + blockListContent[x, y, z, 2] + " dest: " + blockListContent[x, y, z, 4] + " Connections: " + blockListContent[x, y, z, 3]);
            }
            else if (blockList[x, y, z] == BlockType.Explosive)
            {
                if (player == null)
                {
                    if (varGetB("tnt"))
                    if (blockListContent[x, y, z, 1] == 0)
                    {
                        blockListContent[x, y, z, 1] = 31;
                        return true;
                    }
                    return false;
                }
                else
                {
                    if (varGetB("tnt"))
                    {
                        SendServerMessageToPlayer("This explosive requires use of a lever to activate.", player.NetConn);
                    }
                    else
                    {
                        SendServerMessageToPlayer("Explosives are currently deactivated on the server.", player.NetConn);
                    }
                    return false;
                }
            }
            else if (blockList[x, y, z] == BlockType.TrapR || blockList[x, y, z] == BlockType.TrapB)
            {
                if (player != null)//trap falls apart
                {
                    if (player.Team != blockCreatorTeam[x, y, z])
                        SetBlockDebris((ushort)x, (ushort)y, (ushort)z, BlockType.None, PlayerTeam.None);
                }
            }
            else if (blockList[x, y, z] == BlockType.MedicalR || blockList[x, y, z] == BlockType.MedicalB)
            {
                if (player != null)
                {
                    if (btn == 1)
                    {
                        if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.MedicalR || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.MedicalB)
                        {
                            if (blockListContent[x, y, z, 1] > 49)
                            {
                                if (player.StatusEffect[2] > 0)
                                {
                                    blockListContent[x, y, z, 1] -= 50;
                                    EffectAtPoint(player.Position - Vector3.UnitY * 1.5f, 4);
                                    player.StatusEffect[2] = -2;//bane immunity for 4 seconds
                                    player.StatusEffect[3] = 10;//healing 
                                    SendServerMessageToPlayer("The affliction has been cured!" + blockListContent[x, y, z, 1] + "/100 units remain.", player.NetConn);
                                }
                                else if (player.Health < player.HealthMax && player.StatusEffect[3] < 5)
                                {
                                    blockListContent[x, y, z, 1] -= 50;
                                    EffectAtPoint(player.Position - Vector3.UnitY * 1.5f, 4);
                                    player.StatusEffect[3] = 10;//healing 
                                    SendServerMessageToPlayer("You receive treatment! " + blockListContent[x, y, z, 1] + "/100 units remain.", player.NetConn);
                                }
                                else
                                {
                                    SendServerMessageToPlayer(blockListContent[x, y, z, 1] + "/100 units remain.", player.NetConn);
                                }
                            }
                            else
                            {
                                SendServerMessageToPlayer("The medical station is recharging. " + (50 - blockListContent[x, y, z, 1]) + " remain.", player.NetConn);
                            }
                        }
                    }
                    else if (btn == 2)
                    {
                        if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.MedicalR || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.MedicalB)
                        {
                            SendServerMessageToPlayer(blockListContent[x, y, z, 1] + "/100 units remain.", player.NetConn);
                        }
                    }
                }
            }
            else if (blockList[x, y, z] == BlockType.BeaconRed || blockList[x, y, z] == BlockType.BeaconBlue)
            {
                if (player != null)
                {
                    if (btn == 1)
                    {
                        if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.BeaconRed || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.BeaconBlue)
                        {

                        }
                    }
                }
            }
            else if (blockList[x, y, z] == BlockType.ResearchR || blockList[x, y, z] == BlockType.ResearchB)
            {
                if (player != null)
                {
                    if (btn == 1)
                    {
                        if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.ResearchR || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.ResearchB)
                        {
                            if (blockListContent[x, y, z, 0] == 0)
                            {
                                if (blockListContent[x, y, z, 1] > 0)
                                    if (player.Team == PlayerTeam.Blue && teamCashBlue > 0)
                                    {
                                        blockListContent[x, y, z, 0]++;
                                        SendServerMessageBlue(player.Handle + " started researching " + ResearchInformation.GetName((Research)blockListContent[x, y, z, 1]) + " rank " + ResearchComplete[(byte)PlayerTeam.Blue, blockListContent[x, y, z, 1]] + " (" + ResearchProgress[(byte)player.Team, blockListContent[x, y, z, 1]] + " gold required)");
                                        ResearchChange[(byte)player.Team] = blockListContent[x, y, z, 1];
                                        player.Annoying += 4;
                                    }
                                    else if (player.Team == PlayerTeam.Red && teamCashRed > 0)
                                    {
                                        blockListContent[x, y, z, 0]++;
                                        SendServerMessageRed(player.Handle + " started researching " + ResearchInformation.GetName((Research)blockListContent[x, y, z, 1]) + " rank " + ResearchComplete[(byte)PlayerTeam.Red, blockListContent[x, y, z, 1]] + " (" + ResearchProgress[(byte)player.Team, blockListContent[x, y, z, 1]] + " gold required)");
                                        ResearchChange[(byte)player.Team] = blockListContent[x, y, z, 1];
                                        player.Annoying += 4;
                                    }
                                    else if (player.Team == PlayerTeam.Blue && teamCashBlue == 0)
                                    {
                                        SendServerMessageToPlayer("Insufficient funds to begin research!", player.NetConn);
                                    }
                                    else if (player.Team == PlayerTeam.Red && teamCashRed == 0)
                                    {
                                        SendServerMessageToPlayer("Insufficient funds to begin research!", player.NetConn);
                                    }

                            }
                            else if (blockListContent[x, y, z, 0] != 0)
                            {
                                if (player.Team == PlayerTeam.Blue)
                                {
                                    SendServerMessageBlue(player.Handle + " halted research.");
                                    player.Annoying += 4;
                                }
                                else if (player.Team == PlayerTeam.Red)
                                {
                                    SendServerMessageRed(player.Handle + " halted research.");
                                    player.Annoying += 4;
                                }
                                blockListContent[x, y, z, 0] = 0;
                                ResearchChange[(byte)player.Team] = -1;
                            }
                        }
                    }
                    else if (btn == 2)
                    {
                        if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.ResearchR || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.ResearchB)
                        {
                            if (ResearchChange[(byte)player.Team] == 0 || ResearchChange[(byte)player.Team] == -1)
                            {
                                if (blockListContent[x, y, z, 1] < (byte)Research.MAXIMUM - 1)
                                {
                                    blockListContent[x, y, z, 0] = 0;
                                    blockListContent[x, y, z, 1]++;
                                    ResearchChange[(byte)player.Team] = -1;
                                    SendServerMessageToPlayer("Research topic: " + ResearchInformation.GetName((Research)blockListContent[x, y, z, 1]) + "(" + ResearchComplete[(byte)player.Team, blockListContent[x, y, z, 1]] + ") (" + ResearchProgress[(byte)player.Team, blockListContent[x, y, z, 1]] + " gold)", player.NetConn);
                                    // blockListContent[x, y, z, 2] = ResearchInformation.GetCost((Research)blockListContent[x, y, z, 1]);
                                }
                                else
                                {
                                    blockListContent[x, y, z, 0] = 0;
                                    blockListContent[x, y, z, 1] = 1;
                                    ResearchChange[(byte)player.Team] = -1;
                                    SendServerMessageToPlayer("Research topic: " + ResearchInformation.GetName((Research)blockListContent[x, y, z, 1]) + "(" + ResearchComplete[(byte)player.Team, blockListContent[x, y, z, 1]] + ") (" + ResearchProgress[(byte)player.Team, blockListContent[x, y, z, 1]] + " gold)", player.NetConn);
                                }
                            }
                            else
                            {
                                SendServerMessageToPlayer("You must pause or finish " + ResearchInformation.GetName((Research)blockListContent[x, y, z, 1]) + " before changing topic.", player.NetConn);
                            }
                        }
                    }
                    else if (btn == 3)
                    {
                        if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.ResearchR || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.ResearchB)
                        {
                            SendServerMessageToPlayer("Currently topic is " + ResearchInformation.GetName((Research)blockListContent[x, y, z, 1]) + " (" + ResearchProgress[(byte)player.Team, blockListContent[x, y, z, 1]] + " gold required)", player.NetConn);
                        }
                    }
                }
            }
            else if (blockList[x, y, z] == BlockType.ConstructionR || blockList[x, y, z] == BlockType.ConstructionB)
            {
                if (player != null)
                {
                    if (btn == 1)
                    {//" + (BlockType)blockListContent[x, y, z, 0] + "
                        SendServerMessageToPlayer("Artifact safe construction requires: " + (BlockInformation.GetMaxHP(blockList[x, y, z]) - blockListHP[x, y, z]) + " in repairs.", player.NetConn);
                    }
                }
            }
            else if (blockList[x, y, z] == BlockType.ArtCaseR || blockList[x, y, z] == BlockType.ArtCaseB)
            {
                if (player != null)
                {
                    if (blockListContent[x, y, z, 7] == 0)
                    {
                        if (btn == 1)
                        {
                            if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.ArtCaseR || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.ArtCaseB)
                                if (player.Content[10] > 0 && blockListContent[x, y, z, 6] == 0)
                                {//place artifact
                                    uint arty = SetItem(ItemType.Artifact, new Vector3(x + 0.5f, y + 1.5f, z + 0.5f), Vector3.Zero, Vector3.Zero, player.Team, player.Content[10], 0);
                                    itemList[arty].Content[6] = 1;//lock artifact in place
                                    blockListContent[x, y, z, 6] = (int)(arty);
                                    player.Content[10] = 0;
                                    SendItemContentSpecificUpdate(itemList[arty], 6);//lock item
                                    SendContentSpecificUpdate(player, 10);//inform players
                                    SendPlayerContentUpdate(player, 10);//inform activator

                                    ArtifactTeamBonus(player.Team, itemList[arty].Content[10], true);
                                    player.Annoying += 4;

                                    if (blockList[x, y, z] == BlockType.ArtCaseB)
                                    {
                                        teamArtifactsBlue++;
                                        SendScoreUpdate();
                                    }
                                    else if (blockList[x, y, z] == BlockType.ArtCaseR)
                                    {
                                        teamArtifactsRed++;
                                        SendScoreUpdate();
                                    }
                                }
                        }
                        else if (btn == 2)
                        {
                            if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.ArtCaseR || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.ArtCaseB)
                                if (player.Content[10] == 0 && blockListContent[x, y, z, 6] > 0)
                                {//retrieve artifact
                                    uint arty = (uint)(blockListContent[x, y, z, 6]);
                                    itemList[arty].Content[6] = 0;//unlock artifact in place
                                    blockListContent[x, y, z, 6] = 0;//artcase empty
                                    player.Content[10] = itemList[arty].Content[10];//player is holding the new artifact
                                    ArtifactTeamBonus(player.Team, itemList[arty].Content[10], false);
                                    itemList[arty].Disposing = true;//item gets removed

                                    SendContentSpecificUpdate(player, 10);//inform players
                                    SendPlayerContentUpdate(player, 10);//inform activator

                                    player.Annoying += 4;

                                    if (blockList[x, y, z] == BlockType.ArtCaseB)
                                    {
                                        SendServerMessageBlue(ArtifactInformation.GetName(itemList[arty].Content[10]) + " removed by " + player.Handle + ".");
                                        teamArtifactsBlue--;
                                        SendScoreUpdate();
                                    }
                                    else if (blockList[x, y, z] == BlockType.ArtCaseR)
                                    {
                                        SendServerMessageRed(ArtifactInformation.GetName(itemList[arty].Content[10]) + " removed by " + player.Handle + ".");
                                        teamArtifactsRed--;
                                        SendScoreUpdate();
                                    }
                                }
                        }
                        else if (btn == 5)//lockdown
                        {
                            if (blockListContent[x, y, z, 6] > 0)//has arty
                            {
                                blockListContent[x, y, z, 7]++;//permanently locked

                                if (player.Team == PlayerTeam.Red)
                                {
                                    SendServerMessageRed(Defines.Sanitize(player.Handle + " has locked an artifact in place."));
                                }
                                else if (player.Team == PlayerTeam.Blue)
                                {
                                    SendServerMessageBlue(Defines.Sanitize(player.Handle + " has locked an artifact in place."));
                                }

                                //if(blockListContent[x, y, z, 7] < 2)
                                //{
                                //    SendServerMessageToPlayer("Press lock again to confirm.",player.NetConn);
                                //}

                            }
                        }
                    }
                    else
                    {
                        SendServerMessageToPlayer("This artifact has been permanently secured.", player.NetConn);
                    }
                }
                return true;
            }
            else if (blockList[x, y, z] == BlockType.BaseBlue || blockList[x, y, z] == BlockType.BaseRed)
            {
                if (player != null)
                {
                    if (btn == 1)
                    {
                        if (player.Team == PlayerTeam.Red && blockList[x, y, z] == BlockType.BaseRed || player.Team == PlayerTeam.Blue && blockList[x, y, z] == BlockType.BaseBlue)
                            if (player.Content[11] > 0 && blockListContent[x, y, z, 1] == 0)
                            {//begin forge
                                player.Content[11]--;
                                player.Weight--;
                                blockListContent[x, y, z, 5] = 1;

                                player.Score += 100;
                                player.Exp += 100;

                                SendWeightUpdate(player);
                                SendContentSpecificUpdate(player, 11);

                                blockListContent[x, y, z, 1] = artifactCost;
                                NetBuffer msgBuffer = netServer.CreateBuffer();
                                msgBuffer = netServer.CreateBuffer();
                                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);

                                if (player.Team == PlayerTeam.Red)
                                    msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
                                else if (player.Team == PlayerTeam.Blue)
                                    msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);

                                msgBuffer.Write(Defines.Sanitize("The " + player.Team + " has begun forging an artifact!"));

                                foreach (NetConnection netConn in playerList.Keys)
                                    if (netConn.Status == NetConnectionStatus.Connected)
                                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
                            }
                            else if (blockListContent[x, y, z, 1] > 0)
                            {
                                if (player.Team == PlayerTeam.Red)
                                {
                                    SendServerMessageRed(Defines.Sanitize("(" + player.Handle + ") Artifact requires " + blockListContent[x, y, z, 1] * 5 + " more gold to complete."));
                                    player.Annoying += 2;
                                }
                                else if (player.Team == PlayerTeam.Blue)
                                {
                                    SendServerMessageBlue(Defines.Sanitize("(" + player.Handle + ") Artifact requires " + blockListContent[x, y, z, 1] * 5 + " more gold to complete."));
                                    player.Annoying += 2;
                                }
                            }
                    }
                    else if (btn == 2)
                    {
                        if (blockListContent[x, y, z, 1] > 0)//if active
                            if (blockListContent[x, y, z, 5] > 0)
                            {
                                player.Annoying += 2;

                                if (player.Team == PlayerTeam.Red)
                                    SendServerMessageRed("Artifact construction paused by " + player.Handle + ".");
                                else if (player.Team == PlayerTeam.Blue)
                                    SendServerMessageBlue("Artifact construction paused by " + player.Handle + ".");

                                blockListContent[x, y, z, 5] = 0;
                            }
                            else
                            {
                                player.Annoying += 2;

                                if (player.Team == PlayerTeam.Red)
                                    SendServerMessageRed("Artifact construction resumed by " + player.Handle + ".");
                                else if (player.Team == PlayerTeam.Blue)
                                    SendServerMessageBlue("Artifact construction resumed by " + player.Handle + ".");

                                blockListContent[x, y, z, 5] = 1;
                            }
                    }
                }
                return true;
            }
            else if (blockList[x, y, z] == BlockType.Lever)
            {
                if (btn == 1)
                {
                    if (player != null)
                        SendServerMessageToPlayer("You pull the lever!", player.NetConn);

                    if (blockListContent[x, y, z, 0] == 0)//not falling..even though gravity is [10]..
                    {
                        if (player != null)
                            blockListContent[x, y, z, 1] = blockListContent[x, y, z, 2];

                        for (int a = 2; a < 7; a++)
                        {
                            if (blockListContent[x, y, z, a * 6] > 0)
                            {
                                int bx = blockListContent[x, y, z, a * 6 + 1];
                                int by = blockListContent[x, y, z, a * 6 + 2];
                                int bz = blockListContent[x, y, z, a * 6 + 3];
                                int bbtn = blockListContent[x, y, z, a * 6 + 4];

                                if (Trigger(bx, by, bz, x, y, z, bbtn, null, depth) == false)
                                {
                                    //trigger returned no result, delete the link
                                    blockListContent[x, y, z, a * 6] = 0;
                                    blockListContent[x, y, z, a * 6 + 1] = 0;
                                    blockListContent[x, y, z, a * 6 + 2] = 0;
                                    blockListContent[x, y, z, a * 6 + 3] = 0;
                                    blockListContent[x, y, z, a * 6 + 4] = 0;
                                }
                            }
                        }
                    }

                }
                else if (btn == 2)
                {
                    if (player != null)//only a player can invoke this action
                    {
                        int nb = 0;
                        for (nb = 2; nb < 7; nb++)
                        {
                            if (blockListContent[x, y, z, nb * 6] == 0)
                            {
                                break;
                            }
                        }

                        if (nb != 7)//didnt hit end of switch-link limit
                        {

                            SendServerMessageToPlayer("You are now linking objects.", player.NetConn);

                            player.Content[2] = (int)(x);//player is creating a link to this switch
                            player.Content[3] = (int)(y);
                            player.Content[4] = (int)(z);
                            SendContentSpecificUpdate(player, 2);
                            SendContentSpecificUpdate(player, 3);
                            SendContentSpecificUpdate(player, 4);
                        }
                        else
                        {
                            SendServerMessageToPlayer("This lever is overloaded, you are now unlinking objects.", player.NetConn);

                            player.Content[2] = (int)(x);//player is creating a link to this switch
                            player.Content[3] = (int)(y);
                            player.Content[4] = (int)(z);
                            SendContentSpecificUpdate(player, 2);
                            SendContentSpecificUpdate(player, 3);
                            SendContentSpecificUpdate(player, 4);

                        }
                    }
                }
                else if (btn == 3)
                {
                    if (blockListContent[x, y, z, 2] > 1)
                    {
                        blockListContent[x, y, z, 2] -= 1;//decrease retrigger timer
                        if (player != null)
                            SendServerMessageToPlayer("The lever retrigger decreased to " + (blockListContent[x, y, z, 2] * 400) + " milliseconds.", player.NetConn);
                    }
                    else
                    {
                        blockListContent[x, y, z, 2] = 0;
                        SendServerMessageToPlayer("The lever no longer reactivates.", player.NetConn);
                    }
                }
                else if (btn == 4)
                {
                    blockListContent[x, y, z, 2] += 1;//increase retrigger timer
                    if (player != null)
                        SendServerMessageToPlayer("The lever retrigger increased to " + (blockListContent[x, y, z, 2] * 400) + " milliseconds.", player.NetConn);
                }
                return true;
            }
            else if (blockList[x, y, z] == BlockType.Refinery)
            {

            }
            else if (blockList[x, y, z] == BlockType.Plate)
            {
                if (btn == 1)
                {
                    if (player != null)
                    {
                        if (blockListContent[x, y, z, 6] < 1)
                        {
                            blockListContent[x, y, z, 6] = 10;
                            PlaySound(InfiniminerSound.RockFall, new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
                        }

                    }
                    //   SendServerMessageToPlayer("You stand on a pressure plate!", player.NetConn);

                    if (blockListContent[x, y, z, 0] == 0 && blockListContent[x, y, z, 1] < 1)//not falling and recharged
                    {
                        if (player != null)
                            blockListContent[x, y, z, 1] = blockListContent[x, y, z, 2];//only players will trigger the timer

                        for (int a = 2; a < 7; a++)
                        {
                            if (blockListContent[x, y, z, a * 6] > 0)
                            {
                                int bx = blockListContent[x, y, z, a * 6 + 1];
                                int by = blockListContent[x, y, z, a * 6 + 2];
                                int bz = blockListContent[x, y, z, a * 6 + 3];
                                int bbtn = blockListContent[x, y, z, a * 6 + 4];

                                if (Trigger(bx, by, bz, x, y, z, bbtn, null, depth) == false)
                                {
                                    //trigger returned no result, delete the link
                                    blockListContent[x, y, z, a * 6] = 0;
                                    blockListContent[x, y, z, a * 6 + 1] = 0;
                                    blockListContent[x, y, z, a * 6 + 2] = 0;
                                    blockListContent[x, y, z, a * 6 + 3] = 0;
                                    blockListContent[x, y, z, a * 6 + 4] = 0;
                                }
                            }
                        }
                    }

                }
                else if (btn == 2)
                {
                    if (player != null)//only a player can invoke this action
                    {
                        int nb = 0;
                        for (nb = 2; nb < 7; nb++)
                        {
                            if (blockListContent[x, y, z, nb * 6] == 0)
                            {
                                break;
                            }
                        }

                        if (nb != 7)//didnt hit end of switch-link limit
                        {

                            SendServerMessageToPlayer("You are now linking objects.", player.NetConn);

                            player.Content[2] = (int)(x);//player is creating a link to this switch
                            player.Content[3] = (int)(y);
                            player.Content[4] = (int)(z);
                            SendContentSpecificUpdate(player, 2);
                            SendContentSpecificUpdate(player, 3);
                            SendContentSpecificUpdate(player, 4);
                        }
                        else
                        {
                            SendServerMessageToPlayer("This lever is overloaded, you are now unlinking objects.", player.NetConn);

                            player.Content[2] = (int)(x);//player is creating a link to this switch
                            player.Content[3] = (int)(y);
                            player.Content[4] = (int)(z);
                            SendContentSpecificUpdate(player, 2);
                            SendContentSpecificUpdate(player, 3);
                            SendContentSpecificUpdate(player, 4);

                        }
                    }
                }
                else if (btn == 3)
                {
                    if (blockListContent[x, y, z, 2] > 1)
                    {
                        blockListContent[x, y, z, 2] -= 1;//decrease retrigger timer
                        if (player != null)
                            SendServerMessageToPlayer("The pressure plate retrigger decreased to " + (blockListContent[x, y, z, 2] * 400) + " milliseconds.", player.NetConn);
                    }
                    else
                    {
                        blockListContent[x, y, z, 2] = 0;
                        SendServerMessageToPlayer("The pressure plate now only retriggers when touched.", player.NetConn);
                    }
                }
                else if (btn == 4)
                {
                    blockListContent[x, y, z, 2] += 1;//increase retrigger timer
                    if (player != null)
                        SendServerMessageToPlayer("The pressure plate retrigger increased to " + (blockListContent[x, y, z, 2] * 400) + " milliseconds.", player.NetConn);
                }
                else if (btn == 5)
                {
                    if (player != null)
                    {

                        int attempts = 0;

                        int ax = x;
                        int ay = y;
                        int az = z;

                        while (blockList[ax, ay, az] == (BlockType)blockListContent[x, y, z, 5])
                        {
                            int a = randGen.Next(1, 4) - 2;
                            int b = randGen.Next(1, 4) - 2;
                            int c = randGen.Next(1, 4) - 2;

                            ax = x + a;
                            ay = y + b;
                            az = z + c;


                            if (ax > 0 && ax < MAPSIZE - 1 && ay > 0 && ay < MAPSIZE - 1 && az > 0 && az < MAPSIZE - 1 && blockList[ax, ay, az] != BlockType.None && blockList[ax, ay, az] != BlockType.Vacuum && blockList[ax, ay, az] != BlockType.Water && blockList[ax, ay, az] != BlockType.Lava && blockList[ax, ay, az] != BlockType.TransBlue && blockList[ax, ay, az] != BlockType.TransRed)
                            {
                            }
                            else
                            {
                                ax = x;
                                ay = y;
                                az = z;
                            }
                            attempts++;
                            if (attempts > 5)
                                break;
                        }

                        if (blockList[ax, ay, az] != (BlockType)blockListContent[x, y, z, 5])
                        {
                            blockListContent[x, y, z, 5] = (byte)blockList[ax, ay, az];//change texture

                            SetBlockTex((ushort)x, (ushort)y, (ushort)z, BlockType.Plate, (BlockType)blockListContent[x, y, z, 5], player.Team);

                            // SendServerMessageToPlayer("Changed plate appearance to: " + (BlockType)blockListContent[x, y, z, 5], player.NetConn);
                        }
                        else
                        {
                            blockListContent[x, y, z, 5] = (byte)blockList[x, y, z];//revert

                            SetBlockTex((ushort)x, (ushort)y, (ushort)z, BlockType.Plate, (BlockType)blockListContent[x, y, z, 5], player.Team);
                        }
                    }
                }
                return true;
            }
            else if (blockList[x, y, z] == BlockType.Pump)
            {
                if (btn == 1)
                {
                    if (blockListContent[x, y, z, 0] == 0)
                    {

                        if (player != null)
                            SendServerMessageToPlayer(blockList[x, y, z] + " activated.", player.NetConn);

                        blockListContent[x, y, z, 0] = 1;
                    }
                    else
                    {
                        if (player != null)
                            SendServerMessageToPlayer(blockList[x, y, z] + " deactivated.", player.NetConn);

                        blockListContent[x, y, z, 0] = 0;
                    }
                }
                else if (btn == 2)
                {
                    if (blockListContent[x, y, z, 1] < 5)//rotate
                    {
                        blockListContent[x, y, z, 1] += 1;

                        if (player != null)
                            SendServerMessageToPlayer(blockList[x, y, z] + " rotated to " + blockListContent[x, y, z, 1], player.NetConn);

                        if (blockListContent[x, y, z, 1] == 1)
                        {
                            blockListContent[x, y, z, 2] = 0;//x input
                            blockListContent[x, y, z, 3] = -1;//y input
                            blockListContent[x, y, z, 4] = 0;//z input
                            blockListContent[x, y, z, 5] = 1;//x output
                            blockListContent[x, y, z, 6] = 0;//y output
                            blockListContent[x, y, z, 7] = 0;//z output
                            //pulls from below, pumps to side
                        }
                        else if (blockListContent[x, y, z, 1] == 2)
                        {
                            blockListContent[x, y, z, 2] = 0;//x input
                            blockListContent[x, y, z, 3] = -1;//y input
                            blockListContent[x, y, z, 4] = 0;//z input
                            blockListContent[x, y, z, 5] = -1;//x output
                            blockListContent[x, y, z, 6] = 0;//y output
                            blockListContent[x, y, z, 7] = 0;//z output
                            //pulls from below, pumps to otherside
                        }
                        else if (blockListContent[x, y, z, 1] == 3)
                        {
                            blockListContent[x, y, z, 2] = 0;//x input
                            blockListContent[x, y, z, 3] = -1;//y input
                            blockListContent[x, y, z, 4] = 0;//z input
                            blockListContent[x, y, z, 5] = 0;//x output
                            blockListContent[x, y, z, 6] = 0;//y output
                            blockListContent[x, y, z, 7] = 1;//z output
                            //pulls from below, pumps to otherside
                        }
                        else if (blockListContent[x, y, z, 1] == 4)
                        {
                            blockListContent[x, y, z, 2] = 0;//x input
                            blockListContent[x, y, z, 3] = -1;//y input
                            blockListContent[x, y, z, 4] = 0;//z input
                            blockListContent[x, y, z, 5] = 0;//x output
                            blockListContent[x, y, z, 6] = 0;//y output
                            blockListContent[x, y, z, 7] = -1;//z output
                            //pulls from below, pumps to otherside
                        }
                    }
                    else
                    {
                        blockListContent[x, y, z, 1] = 0;//reset rotation

                        if (player != null)
                            SendServerMessageToPlayer(blockList[x, y, z] + " rotated to " + blockListContent[x, y, z, 1], player.NetConn);

                        blockListContent[x, y, z, 2] = 0;//x input
                        blockListContent[x, y, z, 3] = -1;//y input
                        blockListContent[x, y, z, 4] = 0;//z input
                        blockListContent[x, y, z, 5] = 0;//x output
                        blockListContent[x, y, z, 6] = 1;//y output
                        blockListContent[x, y, z, 7] = 0;//z output
                        //pulls from below, pumps straight up
                    }
                }
                return true;
            }
            else if (blockList[x, y, z] == BlockType.Barrel)
            {
                if (btn == 1)
                {
                    if (blockListContent[x, y, z, 0] == 0)
                    {
                        if (player != null)
                            SendServerMessageToPlayer("Attempting to fill..", player.NetConn);

                        blockListContent[x, y, z, 0] = 1;
                    }
                    else
                    {
                        if (player != null)
                            SendServerMessageToPlayer("Emptying..", player.NetConn);

                        blockListContent[x, y, z, 0] = 0;
                    }
                }
                else if (btn == 2)
                {
                    if (player != null)
                    {
                        if (blockListContent[x, y, z, 2] > 0)
                        {
                            if (blockListContent[x, y, z, 0] == 1)
                                SendServerMessageToPlayer("The barrel is filling, it has " + blockListContent[x, y, z, 2] + "/20 units of " + (BlockType)blockListContent[x, y, z, 1] + ".", player.NetConn);
                            else
                                SendServerMessageToPlayer("The barrel is emptying, it has " + blockListContent[x, y, z, 2] + "/20 units of " + (BlockType)blockListContent[x, y, z, 1] + ".", player.NetConn);
                        }
                        else
                        {
                            if (blockListContent[x, y, z, 0] == 1)
                                SendServerMessageToPlayer("The barrel is filling, but there are no liquids nearby.", player.NetConn);
                            else
                                SendServerMessageToPlayer("The barrel is empty.", player.NetConn);
                        }

                    }
                }
                return true;
            }
            else if (blockList[x, y, z] == BlockType.Hinge)
            {

                if (btn == 1)
                {
                    //Vector3 itemImpulse = new Vector3(x,y,z);
                    //Vector3 blockRegion = GetRegion(x,y,z);
                    //ConsoleWrite("blockr:" + blockRegion);

                    bool repairme = false;

                    if (player != null)
                        SendServerMessageToPlayer("You attempt to work the hinge!", player.NetConn);

                    if (blockListContent[x, y, z, 0] == 0)//not falling
                    {
                        bool green = true;

                        for (int a = 2; a < 7; a++)
                        {
                            int bx = blockListContent[x, y, z, a * 6 + 1];
                            int by = blockListContent[x, y, z, a * 6 + 2];
                            int bz = blockListContent[x, y, z, a * 6 + 3];

                            //if (blockList[blockListContent[x, y, z, a * 6 + 1], blockListContent[x, y, z, a * 6 + 2], blockListContent[x, y, z, a * 6 + 3]] == (BlockType)(blockListContent[x, y, z, a * 6]))
                            //{
                            //if (blockListContent[x, y, z, a * 6] == 0)
                            //{
                            //    ConsoleWrite("break at " + a);
                            //    break;
                            //}

                            if (HingeBlockTypes(blockList[blockListContent[x, y, z, a * 6 + 1], blockListContent[x, y, z, a * 6 + 2], blockListContent[x, y, z, a * 6 + 3]]) && HingeBlockTypes(blockList[bx, by, bz]))
                            {
                            }
                            else
                            {
                                blockListContent[x, y, z, a * 6] = 0;
                                blockListContent[x, y, z, a * 6 + 1] = 0;
                                blockListContent[x, y, z, a * 6 + 2] = 0;
                                blockListContent[x, y, z, a * 6 + 3] = 0;
                                break;
                            }
                            BlockType block = BlockType.None;//was pipe

                            if (blockListContent[x, y, z, a * 6] > 0)
                            {
                                
                                //int bx = blockListContent[x, y, z, a * 6 + 1];
                                //int by = blockListContent[x, y, z, a * 6 + 2];
                                //int bz = blockListContent[x, y, z, a * 6 + 3];

                                int relx = bx - x;
                                int rely = by - y;
                                int relz = bz - z;

                                //if (a == 2)
                                //{
                                //    ax = relx;
                                //    ay = rely;
                                //    az = relz;
                                //}
                                //else
                                //{
                                //    if (ax == 0)
                                //    {
                                //        if (bx != 0)
                                //        {
                                //            ConsoleWrite("axis changed x");
                                //            break;
                                //        }
                                //    }
                                //    if (ay == 0)
                                //    {
                                //        if (by != 0)
                                //        {
                                //            ConsoleWrite("axis changed y");
                                //            break;
                                //        }
                                //    }
                                //    if (az == 0)
                                //    {
                                //        if (bz != 0)
                                //        {
                                //            ConsoleWrite("axis changed z");
                                //            break;
                                //        }
                                //    }
                                //}

                                //ConsoleWrite("block: " + (BlockType)blockListContent[x, y, z, a * 6] + " a:" + a);
                                //ConsoleWrite("trublock: " + blockList[bx,by,bz]);
                               // ConsoleWrite("bx: " + bx + " by:" + by + " bz:" + bz);
                                //ConsoleWrite("relx: " + relx + " rely:" + rely + " relz:" + relz);
                                //ConsoleWrite("x: " + x + " y:" + y + " z:" + z);

                                int mod = 1;

                                if (blockListContent[x, y, z, 2] == 2 || blockListContent[x, y, z, 2] == 4)//-x & -z
                                {
                                    mod = -1;
                                }

                                if (blockListContent[x, y, z, 5] == 0)//upward
                                {
                                    if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] < 3)//+x -> +y//checking upwards clear
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +-x +y", player.NetConn);
                                        //diagonal block = blockList[x + rely, y + relx, z + relz];//x + rely * (a - 1), y + relx * (a - 1), z + relz * (a - 1)];
                                        block = blockList[x, y + (relx * mod), z];//relx*mod
                                    }
                                    else if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] > 2)//+z -> +y//checking upwards clear
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +-z +y", player.NetConn);
                                        block = blockList[x, y + (relz * mod), z];//relx*mod
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 0)//+y -> +x
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y +x",player.NetConn);
                                        block = blockList[x + rely, y, z];
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 2)//+y -> -x
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y -x", player.NetConn);
                                        block = blockList[x - rely, y, z];
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 3)//+y -> +z
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y +z", player.NetConn);
                                        block = blockList[x, y, z + rely];
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 4)//+y -> -z
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y -z", player.NetConn);

                                        block = blockList[x, y, z - rely];
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 0)//+y -> +x
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y +x",player.NetConn);
                                        block = blockList[x + rely, y, z];
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 2)//+y -> -x
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y -x", player.NetConn);
                                        block = blockList[x - rely, y, z];
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 3)//+y -> +z
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y +z", player.NetConn);
                                        block = blockList[x, y, z + rely];
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 4)//+y -> -z
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y -z", player.NetConn);

                                        block = blockList[x, y, z - rely];
                                    }
                                }
                                else if (blockListContent[x, y, z, 5] == 1)//downward
                                {
                                    if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] < 3)//+x -> -y//checking upwards clear
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +-x +y", player.NetConn);
                                        //diagonal block = blockList[x + rely, y + relx, z + relz];//x + rely * (a - 1), y + relx * (a - 1), z + relz * (a - 1)];
                                        block = blockList[x, y - (relx * mod), z];//relx*mod//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] > 2)//+z -> -y//checking upwards clear
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +-z +y", player.NetConn);
                                        block = blockList[x, y - (relz * mod), z];//relx*mod//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 0)//-y -> +x
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y +x",player.NetConn);
                                        block = blockList[x - rely, y, z];//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 2)//-y -> -x
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y -x", player.NetConn);
                                        block = blockList[x + rely, y, z];//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 3)//-y -> +z
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y +z", player.NetConn);
                                        block = blockList[x, y, z - rely];//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 4)//-y -> -z
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y -z", player.NetConn);

                                        block = blockList[x, y, z + rely];//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 0)//-y -> +x
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y +x",player.NetConn);
                                        block = blockList[x - rely, y, z];//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 2)//-y -> -x
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y -x", player.NetConn);
                                        block = blockList[x + rely, y, z];//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 3)//-y -> +z
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y +z", player.NetConn);
                                        block = blockList[x, y, z - rely];//-y
                                    }
                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 4)//-y -> -z
                                    {
                                        //if (player != null)
                                        //SendServerMessageToPlayer("gc +y -z", player.NetConn);

                                        block = blockList[x, y, z + rely];//-y
                                    }
                                }

                                if (block != BlockType.None && block != BlockType.Water && block != BlockType.Lava)
                                {
                                    //ConsoleWrite(block + " was apparently in the way! "+ a);
                                    green = false;//obstruction

                                    //if (player != null)
                                    //{
                                    //    if (blockListContent[x, y, z, 1] != 1 && blockListContent[x, y, z, 2] == 0)
                                    //        SendServerMessageToPlayer("not clear +x +y:" + (a - 1) + " " + blockList[x, y + (relx * mod), z], player.NetConn);
                                    //    if (blockListContent[x, y, z, 1] != 1 && blockListContent[x, y, z, 2] == 2)
                                    //        SendServerMessageToPlayer("not clear -x +y:" + (a - 1) + " " + blockList[x, y + (relx * mod), z], player.NetConn);
                                    //    else if (blockListContent[x, y, z, 2] == 3)
                                    //        SendServerMessageToPlayer("not clear y+z:" + (a - 1) + " " + blockList[x, y, z + rely], player.NetConn);
                                    //    else if (blockListContent[x, y, z, 2] == 4)
                                    //        SendServerMessageToPlayer("not clear y-z:" + (a - 1) + " " + blockList[x, y, z - rely], player.NetConn);
                                    //    else if (blockListContent[x, y, z, 2] == 2)
                                    //        SendServerMessageToPlayer("not clear y-x:" + (a - 1) + " " + blockList[x-rely, y, z], player.NetConn);
                                    //    else
                                    //        SendServerMessageToPlayer("not clear y+x:" + (a - 1) + " " + blockList[x+rely, y, z], player.NetConn);
                                    //}

                                }
                            }
                        }

                        if (repairme == false)
                        {
                        }
                        else
                        {
                            if (player != null)
                                SendServerMessageToPlayer("Hinge requires repair.", player.NetConn);
                        }

                        if (repairme == false)
                            if (green == true)
                            {
                                //ConsoleWrite("a");//blockListContent[x, y, z, 5] + " " + blockListContent[x, y, z, 1] + " " + blockListContent[x, y, z, 2]);
                                for (int a = 2; a < 7; a++)//7
                                {
                                    if (blockListContent[x, y, z, a * 6] > 0)
                                    {
                                        if (repairme == false)
                                            if (HingeBlockTypes(blockList[blockListContent[x, y, z, a * 6 + 1], blockListContent[x, y, z, a * 6 + 2], blockListContent[x, y, z, a * 6 + 3]]))
                                            {
                                                int bx = blockListContent[x, y, z, a * 6 + 1];//data of block about to move
                                                int by = blockListContent[x, y, z, a * 6 + 2];
                                                int bz = blockListContent[x, y, z, a * 6 + 3];
                                                BlockType blockType = blockList[bx, by, bz];

                                                //ConsoleWrite(blockListContent[x, y, z, 5] + " hp: " + blockListHP[bx, by, bz] + " " + blockListContent[x, y, z, 1] + " " + blockListContent[x, y, z, 2]);
                                                int relx = 0;
                                                int rely = 0;
                                                int relz = 0;

                                                if (blockListContent[x, y, z, 5] == 0)
                                                {
                                                    if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] == 0)// +x -> +y
                                                    {
                                                        relx = bx - x;
                                                        rely = 0;
                                                        relz = 0;

                                                        SetBlock((ushort)(x), (ushort)(by + relx), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);
                                                        blockListHP[x, by + relx, z] = blockListHP[bx, by, bz];
                                                        //ConsoleWrite("hp: " + blockListHP[bx, by, bz]);//wrong one
                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y + a - 1, z];
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y + a - 1;
                                                        blockListContent[x, y, z, a * 6 + 3] = z;

                                                        Vector3 launch = new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                                                        Vector3 strength = new Vector3(a, (6 - a), 0);

                                                        if (by < MAPSIZE - 2)
                                                        {
                                                            switch (blockList[bx, by + 1, bz])
                                                            {
                                                                case BlockType.Explosive:
                                                                    if(varGetB("tnt"))
                                                                    if (blockListContent[bx, by + 1, bz, 1] == 0)//not ticking
                                                                    {
                                                                        blockListContent[bx, by + 1, bz, 1] = 31;//starts
                                                                        Catapult((uint)bx, (uint)(by + 1), (uint)bz, strength);
                                                                    }
                                                                    break;
                                                                case BlockType.Sand:
                                                                case BlockType.Mud:
                                                                case BlockType.Grass:
                                                                case BlockType.Ore:
                                                                case BlockType.Barrel:
                                                                case BlockType.Dirt:
                                                                    Catapult((uint)bx, (uint)(by + 1), (uint)bz, strength);
                                                                    break;
                                                                default:
                                                                    break;
                                                            }
                                                        }

                                                        for (int ia = highestitem; ia >= 0; ia--)
                                                        {
                                                            if (itemList.ContainsKey((uint)ia))
                                                                if (!itemList[(uint)ia].Disposing)
                                                                {
                                                                    if ((itemList[(uint)ia].Position - launch).Length() < 1)
                                                                    {
                                                                        //ConsoleWrite("launch!");
                                                                        itemList[(uint)ia].Velocity += strength;
                                                                    }
                                                                }
                                                        }

                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +x +y", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] == 3)// +z -> +y
                                                    {
                                                        relx = 0;
                                                        rely = 0;
                                                        relz = bz - z;

                                                        SetBlock((ushort)(x), (ushort)(by + relz), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);
                                                        blockListHP[x, by + relz, z] = blockListHP[bx, by, bz];

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y + a - 1, z];
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y + a - 1;
                                                        blockListContent[x, y, z, a * 6 + 3] = z;

                                                        Vector3 launch = new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                                                        Vector3 strength = new Vector3(0, (6 - a), a);

                                                        if (by < MAPSIZE - 2)
                                                        {
                                                            switch (blockList[bx, by + 1, bz])
                                                            {
                                                                case BlockType.Explosive:
                                                                    if (blockListContent[bx, by + 1, bz, 1] == 0)//not ticking
                                                                    {
                                                                        Catapult((uint)bx, (uint)(by + 1), (uint)bz, strength);
                                                                    }
                                                                    break;
                                                                case BlockType.Sand:
                                                                case BlockType.Mud:
                                                                case BlockType.Grass:
                                                                case BlockType.Ore:
                                                                case BlockType.Barrel:
                                                                case BlockType.Dirt:
                                                                    Catapult((uint)bx, (uint)(by + 1), (uint)bz, strength);
                                                                    break;
                                                                default:
                                                                    break;
                                                            }
                                                        }

                                                        for (int ia = highestitem; ia >= 0; ia--)
                                                        {
                                                            if (itemList.ContainsKey((uint)ia))
                                                                if (!itemList[(uint)ia].Disposing)
                                                                {
                                                                    if ((itemList[(uint)ia].Position - launch).Length() < 1)
                                                                    {
                                                                        //ConsoleWrite("launch!");
                                                                        itemList[(uint)ia].Velocity += strength;
                                                                    }
                                                                }
                                                        }
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +z +y", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] == 2)// -x -> +y
                                                    {
                                                        relx = bx - x;
                                                        rely = 0;
                                                        relz = 0;

                                                        SetBlock((ushort)(x), (ushort)(by - relx), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);
                                                        blockListHP[x, by - relx, z] = blockListHP[bx, by, bz];

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y + (a - 1), z];
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y + (a - 1);
                                                        blockListContent[x, y, z, a * 6 + 3] = z;

                                                        Vector3 launch = new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                                                        Vector3 strength = new Vector3(-a, (6 - a), 0);

                                                        if (by < MAPSIZE - 2)
                                                        {
                                                            switch (blockList[bx, by + 1, bz])
                                                            {
                                                                case BlockType.Explosive:
                                                                    if (blockListContent[bx, by + 1, bz, 1] == 0)//not ticking
                                                                    {
                                                                        Catapult((uint)bx, (uint)(by + 1), (uint)bz, strength);
                                                                    }
                                                                    break;
                                                                case BlockType.Sand:
                                                                case BlockType.Mud:
                                                                case BlockType.Grass:
                                                                case BlockType.Ore:
                                                                case BlockType.Barrel:
                                                                case BlockType.Dirt:
                                                                    Catapult((uint)bx, (uint)(by + 1), (uint)bz, strength);
                                                                    break;
                                                                default:
                                                                    break;
                                                            }
                                                        }

                                                        for (int ia = highestitem; ia >= 0; ia--)
                                                        {
                                                            if (itemList.ContainsKey((uint)ia))
                                                                if (!itemList[(uint)ia].Disposing)
                                                                {
                                                                    if ((itemList[(uint)ia].Position - launch).Length() < 1)
                                                                    {
                                                                        //ConsoleWrite("launch!");
                                                                        itemList[(uint)ia].Velocity += strength;
                                                                    }
                                                                }
                                                        }
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green -x +y", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] == 4)// -z -> +y
                                                    {
                                                        relx = 0;
                                                        rely = 0;
                                                        relz = bz - z;

                                                        SetBlock((ushort)(x), (ushort)(by - relz), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);
                                                        blockListHP[x, by - relz, z] = blockListHP[bx, by, bz];

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y + (a - 1), z];
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y + (a - 1);
                                                        blockListContent[x, y, z, a * 6 + 3] = z;

                                                        Vector3 launch = new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f);
                                                        Vector3 strength = new Vector3(0, (6 - a), -a);

                                                        if (by < MAPSIZE - 2)
                                                        {
                                                            switch (blockList[bx, by + 1, bz])
                                                            {
                                                                case BlockType.Explosive:
                                                                    if (blockListContent[bx, by + 1, bz, 1] == 0)//not ticking
                                                                    {
                                                                        Catapult((uint)bx, (uint)(by + 1), (uint)bz, strength);
                                                                    }
                                                                    break;
                                                                case BlockType.Sand:
                                                                case BlockType.Mud:
                                                                case BlockType.Grass:
                                                                case BlockType.Ore:
                                                                case BlockType.Barrel:
                                                                case BlockType.Dirt:
                                                                    Catapult((uint)bx, (uint)(by + 1), (uint)bz, strength);
                                                                    break;
                                                                default:
                                                                    break;
                                                            }
                                                        }

                                                        for (int ia = highestitem; ia >= 0; ia--)
                                                        {
                                                            if (itemList.ContainsKey((uint)ia))
                                                                if (!itemList[(uint)ia].Disposing)
                                                                {
                                                                    if ((itemList[(uint)ia].Position - launch).Length() < 1)
                                                                    {
                                                                        //ConsoleWrite("launch!");
                                                                        itemList[(uint)ia].Velocity += strength;
                                                                    }
                                                                }
                                                        }
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green -z +y", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 0)// +y -> +x
                                                    {
                                                        relx = 0;
                                                        rely = by - y;
                                                        relz = 0;

                                                        if (blockList[bx + rely, y, z] == BlockType.Water || blockList[bx + rely, y, z] == BlockType.Lava)
                                                        {//water in our way
                                                            if (blockList[bx + rely, y + 1, z] == BlockType.None)
                                                            {//push water up one
                                                                SetBlock((ushort)(bx + rely), (ushort)(y + 1), (ushort)(z), blockList[bx + rely, y, z], PlayerTeam.None);
                                                                blockListContent[bx + rely, y + 1, z, 1] = blockListContent[bx + rely, y, z, 1];//copy temperature
                                                                blockListContent[bx + rely, y + 1, z, 2] = blockListContent[bx + rely, y, z, 2];//copy blocks future type
                                                            }
                                                        }

                                                        SetBlock((ushort)(bx + rely), (ushort)(y), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);
                                                        blockListHP[bx + rely, y, z] = blockListHP[bx, by, bz];

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x + a - 1, y, z];
                                                        blockListContent[x, y, z, a * 6 + 1] = x + a - 1;
                                                        blockListContent[x, y, z, a * 6 + 2] = y;
                                                        blockListContent[x, y, z, a * 6 + 3] = z;
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +y +x", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 3)// +y -> +z
                                                    {
                                                        relx = 0;
                                                        rely = by - y;
                                                        relz = 0;

                                                        if (blockList[x, y, bz + rely] == BlockType.Water || blockList[x, y, bz + rely] == BlockType.Lava)
                                                        {//water in our way
                                                            if (blockList[x, y + 1, bz + rely] == BlockType.None)
                                                            {//push water up one
                                                                SetBlock((ushort)(x), (ushort)(y + 1), (ushort)(bz + rely), blockList[x, y, bz + rely], PlayerTeam.None);
                                                                blockListContent[x, y + 1, bz + rely, 1] = blockListContent[x, y, bz + rely, 1];//copy temperature
                                                                blockListContent[x, y + 1, bz + rely, 2] = blockListContent[x, y, bz + rely, 2];//copy blocks future type
                                                            }
                                                        }
                                                        SetBlock((ushort)(x), (ushort)(y), (ushort)(bz + rely), blockType, blockCreatorTeam[bx, by, bz]);
                                                        blockListHP[x, y, bz + rely] = blockListHP[bx, by, bz];

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y, z + a - 1];
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y;
                                                        blockListContent[x, y, z, a * 6 + 3] = z + a - 1;
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +y +z", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 2)// +y -> -x
                                                    {
                                                        relx = 0;
                                                        rely = by - y;
                                                        relz = 0;

                                                        if (blockList[bx - rely, y, z] == BlockType.Water || blockList[bx - rely, y, z] == BlockType.Lava)
                                                        {//water in our way
                                                            if (blockList[bx - rely, y + 1, z] == BlockType.None)
                                                            {//push water up one
                                                                SetBlock((ushort)(bx - rely), (ushort)(y + 1), (ushort)(z), blockList[bx - rely, y, z], PlayerTeam.None);
                                                                blockListContent[bx - rely, y + 1, z, 1] = blockListContent[bx - rely, y, z, 1];//copy temperature
                                                                blockListContent[bx - rely, y + 1, z, 2] = blockListContent[bx - rely, y, z, 2];//copy blocks future type
                                                            }
                                                        }

                                                        SetBlock((ushort)(bx - rely), (ushort)(y), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);
                                                        blockListHP[bx - rely, y, z] = blockListHP[bx, by, bz];

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x - (a - 1), y, z];
                                                        blockListContent[x, y, z, a * 6 + 1] = x - (a - 1);
                                                        blockListContent[x, y, z, a * 6 + 2] = y;
                                                        blockListContent[x, y, z, a * 6 + 3] = z;
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +y -x", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 4)// +y -> -z
                                                    {
                                                        relx = 0;
                                                        rely = by - y;
                                                        relz = 0;

                                                        if (blockList[x, y, bz - rely] == BlockType.Water || blockList[x, y, bz - rely] == BlockType.Lava)
                                                        {//water in our way
                                                            if (blockList[x, y + 1, bz - rely] == BlockType.None)
                                                            {//push water up one
                                                                SetBlock((ushort)(x), (ushort)(y + 1), (ushort)(bz - rely), blockList[x, y, bz - rely], PlayerTeam.None);
                                                                blockListContent[x, y + 1, bz - rely, 1] = blockListContent[x, y, bz - rely, 1];//copy temperature
                                                                blockListContent[x, y + 1, bz - rely, 2] = blockListContent[x, y, bz - rely, 2];//copy blocks future type
                                                            }
                                                        }

                                                        SetBlock((ushort)(x), (ushort)(y), (ushort)(bz - rely), blockType, blockCreatorTeam[bx, by, bz]);
                                                        blockListHP[x, y, bz - rely] = blockListHP[bx, by, bz];

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y, z - (a - 1)];
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y;
                                                        blockListContent[x, y, z, a * 6 + 3] = z - (a - 1);
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +y -z", player.NetConn);
                                                    }
                                                    //setblockdebris for visible changes
                                                    SetBlock((ushort)(bx), (ushort)(by), (ushort)(bz), BlockType.None, PlayerTeam.None);
                                                }
                                                else if (blockListContent[x, y, z, 5] == 1)//downwards
                                                {
                                                    if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] == 0)// +x -> -y
                                                    {
                                                        relx = bx - x;
                                                        rely = 0;
                                                        relz = 0;

                                                        SetBlock((ushort)(x), (ushort)(by - relx), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);//-y
                                                        blockListHP[x, by - relx, z] = blockListHP[bx, by, bz];//-y

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y - (a - 1), z];//-y //()
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y - (a - 1);//-y //();
                                                        blockListContent[x, y, z, a * 6 + 3] = z;

                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +x +y", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] == 3)// +z -> -y
                                                    {
                                                        relx = 0;
                                                        rely = 0;
                                                        relz = bz - z;

                                                        SetBlock((ushort)(x), (ushort)(by - relz), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);//y
                                                        blockListHP[x, by - relz, z] = blockListHP[bx, by, bz];//-y

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y - (a - 1), z];//-y
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y - (a - 1);//y
                                                        blockListContent[x, y, z, a * 6 + 3] = z;

                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +z +y", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] == 2)// -x -> -y
                                                    {
                                                        relx = bx - x;
                                                        rely = 0;
                                                        relz = 0;

                                                        SetBlock((ushort)(x), (ushort)(by + relx), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);//-y
                                                        blockListHP[x, by + relx, z] = blockListHP[bx, by, bz];//-y

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y - (a - 1), z];//-y
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y - (a - 1);//-y
                                                        blockListContent[x, y, z, a * 6 + 3] = z;

                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green -x +y", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 1 && blockListContent[x, y, z, 2] == 4)// -z -> -y
                                                    {
                                                        relx = 0;
                                                        rely = 0;
                                                        relz = bz - z;

                                                        SetBlock((ushort)(x), (ushort)(by + relz), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);//-y
                                                        blockListHP[x, by + relz, z] = blockListHP[bx, by, bz];//-y

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y - (a - 1), z];//-y
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y - (a - 1);//-y
                                                        blockListContent[x, y, z, a * 6 + 3] = z;

                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green -z +y", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 0)// -y -> +x
                                                    {
                                                        relx = 0;
                                                        rely = by - y;
                                                        relz = 0;

                                                        if (blockList[bx - rely, y, z] == BlockType.Water || blockList[bx - rely, y, z] == BlockType.Lava)
                                                        {//water in our way
                                                            if (blockList[bx - rely, y + 1, z] == BlockType.None)
                                                            {//push water up one
                                                                SetBlock((ushort)(bx - rely), (ushort)(y + 1), (ushort)(z), blockList[bx - rely, y, z], PlayerTeam.None);
                                                                blockListContent[bx - rely, y + 1, z, 1] = blockListContent[bx - rely, y, z, 1];//copy temperature
                                                                blockListContent[bx - rely, y + 1, z, 2] = blockListContent[bx - rely, y, z, 2];//copy blocks future type
                                                            }
                                                        }

                                                        SetBlock((ushort)(bx - rely), (ushort)(y), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);//-y
                                                        blockListHP[bx - rely, y, z] = blockListHP[bx, by, bz];//-y

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x + (a - 1), y, z];//-y
                                                        blockListContent[x, y, z, a * 6 + 1] = x + (a - 1);//-y
                                                        blockListContent[x, y, z, a * 6 + 2] = y;
                                                        blockListContent[x, y, z, a * 6 + 3] = z;
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +y +x", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 3)// -y -> +z
                                                    {
                                                        relx = 0;
                                                        rely = by - y;
                                                        relz = 0;

                                                        if (blockList[x, y, bz - rely] == BlockType.Water || blockList[x, y, bz - rely] == BlockType.Lava)
                                                        {//water in our way
                                                            if (blockList[x, y + 1, bz - rely] == BlockType.None)
                                                            {//push water up one
                                                                SetBlock((ushort)(x), (ushort)(y + 1), (ushort)(bz - rely), blockList[x, y, bz - rely], PlayerTeam.None);
                                                                blockListContent[x, y + 1, bz - rely, 1] = blockListContent[x, y, bz - rely, 1];//copy temperature
                                                                blockListContent[x, y + 1, bz - rely, 2] = blockListContent[x, y, bz - rely, 2];//copy blocks future type
                                                            }
                                                        }
                                                        SetBlock((ushort)(x), (ushort)(y), (ushort)(bz - rely), blockType, blockCreatorTeam[bx, by, bz]);//-y
                                                        blockListHP[x, y, bz - rely] = blockListHP[bx, by, bz];//-y

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y, z + (a - 1)];//-y
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y;
                                                        blockListContent[x, y, z, a * 6 + 3] = z + (a - 1);//-y
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +y +z", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 2)// -y -> -x
                                                    {
                                                        relx = 0;
                                                        rely = by - y;
                                                        relz = 0;
                                                        //watercheck?
                                                        if (blockList[bx + rely, y, z] == BlockType.Water || blockList[bx + rely, y, z] == BlockType.Lava)
                                                        {//water in our way
                                                            if (blockList[bx + rely, y + 1, z] == BlockType.None)
                                                            {//push water up one
                                                                SetBlock((ushort)(bx + rely), (ushort)(y + 1), (ushort)(z), blockList[bx + rely, y, z], PlayerTeam.None);
                                                                blockListContent[bx + rely, y + 1, z, 1] = blockListContent[bx + rely, y, z, 1];//copy temperature
                                                                blockListContent[bx + rely, y + 1, z, 2] = blockListContent[bx + rely, y, z, 2];//copy blocks future type
                                                            }
                                                        }

                                                        SetBlock((ushort)(bx + rely), (ushort)(y), (ushort)(z), blockType, blockCreatorTeam[bx, by, bz]);//-y
                                                        blockListHP[bx + rely, y, z] = blockListHP[bx, by, bz];//-y

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x - (a - 1), y, z];//-y ?
                                                        blockListContent[x, y, z, a * 6 + 1] = x - (a - 1);//-y
                                                        blockListContent[x, y, z, a * 6 + 2] = y;
                                                        blockListContent[x, y, z, a * 6 + 3] = z;
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +y -x", player.NetConn);
                                                    }
                                                    else if (blockListContent[x, y, z, 1] == 0 && blockListContent[x, y, z, 2] == 4)// -y -> -z
                                                    {
                                                        relx = 0;
                                                        rely = by - y;
                                                        relz = 0;

                                                        if (blockList[x, y, bz + rely] == BlockType.Water || blockList[x, y, bz + rely] == BlockType.Lava)
                                                        {//water in our way
                                                            if (blockList[x, y + 1, bz + rely] == BlockType.None)
                                                            {//push water up one
                                                                SetBlock((ushort)(x), (ushort)(y + 1), (ushort)(bz + rely), blockList[x, y, bz + rely], PlayerTeam.None);
                                                                blockListContent[x, y + 1, bz + rely, 1] = blockListContent[x, y, bz + rely, 1];//copy temperature
                                                                blockListContent[x, y + 1, bz + rely, 2] = blockListContent[x, y, bz + rely, 2];//copy blocks future type
                                                            }
                                                        }

                                                        SetBlock((ushort)(x), (ushort)(y), (ushort)(bz + rely), blockType, blockCreatorTeam[bx, by, bz]);//-y
                                                        blockListHP[x, y, bz + rely] = blockListHP[bx, by, bz];//-y

                                                        blockListContent[x, y, z, a * 6] = (byte)blockList[x, y, z - (a - 1)];//-y
                                                        blockListContent[x, y, z, a * 6 + 1] = x;
                                                        blockListContent[x, y, z, a * 6 + 2] = y;
                                                        blockListContent[x, y, z, a * 6 + 3] = z - (a - 1);//-y
                                                        //if (player != null)
                                                        //SendServerMessageToPlayer("green +y -z", player.NetConn);
                                                    }
                                                    //setblockdebris for visible changes
                                                    SetBlock((ushort)(bx), (ushort)(by), (ushort)(bz), BlockType.None, PlayerTeam.None);
                                                }
                                            }
                                    }
                                    else
                                    {
                                        blockListContent[x, y, z, a * 6] = 0;//clear block out
                                        blockListContent[x, y, z, a * 6 + 1] = 0;
                                        blockListContent[x, y, z, a * 6 + 2] = 0;
                                        blockListContent[x, y, z, a * 6 + 3] = 0;
                                        repairme = true;
                                        //if (player != null)
                                        //SendServerMessageToPlayer("Empty requires repair on " + a, player.NetConn); 
                                    }
                                }

                                if (blockListContent[x, y, z, 1] == 1)//swap between +x -> +y to +x
                                    blockListContent[x, y, z, 1] = 0;//blockListContent[x, y, z, 2];
                                else
                                    blockListContent[x, y, z, 1] = 1;//revert to its original position
                            }
                            else
                            {
                                if (player != null)
                                    SendServerMessageToPlayer("It's jammed!", player.NetConn);
                            }
                    }

                }
                else if (btn == 2)
                {
                    if (blockListContent[x, y, z, 1] != 1 && (blockListContent[x, y, z, 5] == 0 && blockList[x, y + 1, z] != BlockType.None || blockListContent[x, y, z, 5] == 1 && blockList[x, y - 1, z] != BlockType.None))//checks hinge vert / if it has a block
                    {
                        //SendServerMessageToPlayer("The hinge must returned to horizontal position.", player.NetConn);
                        blockListContent[x, y, z, 2] += 1;
                        if (blockListContent[x, y, z, 2] > 4)
                            blockListContent[x, y, z, 2] = 0;

                        if (blockListContent[x, y, z, 2] == 1)//1 is not a viable direction to set
                            blockListContent[x, y, z, 2] = 2;

                        if (player != null)
                        {
                            string direction = "";
                            if (blockListContent[x, y, z, 2] == 0) //+x
                                direction = "North";
                            else if (blockListContent[x, y, z, 2] == 2) //-x
                                direction = "South";
                            else if (blockListContent[x, y, z, 2] == 3) //+z
                                direction = "East";
                            else if (blockListContent[x, y, z, 2] == 4) //-z
                                direction = "West";

                            SendServerMessageToPlayer("The hinge was rotated to face " + direction + ".", player.NetConn);
                        }

                        if (blockListContent[x, y, z, 5] == 0)
                        {
                            for (int a = 2; a < 7; a++)
                            {
                                if (blockListContent[x, y, z, a * 6] > 0)
                                {
                                    if (blockListContent[x, y, z, 2] == 0) //+x
                                        DebrisEffectAtPoint(x + a, y, z, BlockType.Highlight, 1);
                                    else if (blockListContent[x, y, z, 2] == 2) //-x
                                        DebrisEffectAtPoint(x - a, y, z, BlockType.Highlight, 1);
                                    else if (blockListContent[x, y, z, 2] == 3) //+z
                                        DebrisEffectAtPoint(x, y, z + a, BlockType.Highlight, 1);
                                    else if (blockListContent[x, y, z, 2] == 4) //-z
                                        DebrisEffectAtPoint(x, y, z - a, BlockType.Highlight, 1);
                                }
                            }
                        }
                        else
                        {
                            for (int a = 2; a < 7; a++)
                            {
                                if (blockListContent[x, y, z, a * 6] > 0)
                                {
                                    if (blockListContent[x, y, z, 2] == 0)
                                        DebrisEffectAtPoint(x + a, y, z, BlockType.Highlight, 1);
                                    else if (blockListContent[x, y, z, 2] == 2)
                                        DebrisEffectAtPoint(x - a, y, z, BlockType.Highlight, 1);
                                    else if (blockListContent[x, y, z, 2] == 3)
                                        DebrisEffectAtPoint(x, y, z + a, BlockType.Highlight, 1);
                                    else if (blockListContent[x, y, z, 2] == 4)
                                        DebrisEffectAtPoint(x, y, z - a, BlockType.Highlight, 1);
                                }
                            }
                        }
                        //rotate without changing anything
                        return true;
                    }

                    blockListContent[x, y, z, 2] += 1;
                    if (blockListContent[x, y, z, 2] > 4)
                        blockListContent[x, y, z, 2] = 0;

                    if (blockListContent[x, y, z, 2] == 1)//1 is not a viable direction to set
                        blockListContent[x, y, z, 2] = 2;

                    //blockListContent[x, y, z, 2] = 3;//2 ;//-x -> +
                    blockListContent[x, y, z, 1] = 1;

                    if (player != null)
                    {
                        string direction = "";
                        if (blockListContent[x, y, z, 2] == 0) //+x
                            direction = "North";
                        else if (blockListContent[x, y, z, 2] == 2) //-x
                            direction = "South";
                        else if (blockListContent[x, y, z, 2] == 3) //+z
                            direction = "East";
                        else if (blockListContent[x, y, z, 2] == 4) //-z
                            direction = "West";

                        SendServerMessageToPlayer("The hinge was rotated to face " + direction + ".", player.NetConn);
                    }

                    PlayerTeam team = PlayerTeam.None;
                    if (player != null)
                        team = player.Team;

                    int br = 8;

                    for (int a = 2; a < 7; a++)//7
                    {
                        if (blockListContent[x, y, z, 2] == 0)
                        {
                            if (!HingeBlockTypes(blockList[x + a - 1, y, z], team))
                            {
                                br = a;
                                break;
                            }
                            else
                            {
                                if (blockCreatorTeam[x + a - 1, y, z] != team)
                                {
                                    br = a;
                                    break;
                                }
                            }

                            blockListContent[x, y, z, a * 6] = (byte)blockList[x + a - 1, y, z];
                            blockListContent[x, y, z, a * 6 + 1] = x + a - 1;
                            blockListContent[x, y, z, a * 6 + 2] = y;
                            blockListContent[x, y, z, a * 6 + 3] = z;

                            DebrisEffectAtPoint((float)(blockListContent[x, y, z, a * 6 + 1]), (float)(blockListContent[x, y, z, a * 6 + 2]), (float)(blockListContent[x, y, z, a * 6 + 3]), BlockType.Highlight, 1);
                            //if (player != null)
                            //    SendServerMessageToPlayer("lposx:" + (a - 1) + " " + blockList[blockListContent[x, y, z, a * 6 + 1], blockListContent[x, y, z, a * 6 + 2], blockListContent[x, y, z, a * 6 + 3]], player.NetConn);

                        }
                        else if (blockListContent[x, y, z, 2] == 2)
                        {
                            if (!HingeBlockTypes(blockList[x - (a - 1), y, z], team))
                            {
                                br = a;
                                break;
                            }
                            else
                            {
                                if (blockCreatorTeam[x - (a - 1), y, z] != team)
                                {
                                    br = a;
                                    break;
                                }
                            }

                            blockListContent[x, y, z, a * 6] = (byte)blockList[x - (a - 1), y, z];
                            blockListContent[x, y, z, a * 6 + 1] = x - (a - 1);
                            blockListContent[x, y, z, a * 6 + 2] = y;
                            blockListContent[x, y, z, a * 6 + 3] = z;

                            DebrisEffectAtPoint((float)(blockListContent[x, y, z, a * 6 + 1]), (float)(blockListContent[x, y, z, a * 6 + 2]), (float)(blockListContent[x, y, z, a * 6 + 3]), BlockType.Highlight, 1);
                            //if (player != null)
                            //    SendServerMessageToPlayer("lnegx:" + (a - 1) + " " + blockList[blockListContent[x, y, z, a * 6 + 1], blockListContent[x, y, z, a * 6 + 2], blockListContent[x, y, z, a * 6 + 3]], player.NetConn);

                        }
                        else if (blockListContent[x, y, z, 2] == 3)
                        {
                            if (!HingeBlockTypes(blockList[x, y, z + a - 1], team))
                            {
                                br = a;
                                break;
                            }
                            else
                            {
                                if (blockCreatorTeam[x, y, z + a - 1] != team)
                                {
                                    br = a;
                                    break;
                                }
                            }

                            blockListContent[x, y, z, a * 6] = (byte)blockList[x, y, z + a - 1];
                            blockListContent[x, y, z, a * 6 + 1] = x;
                            blockListContent[x, y, z, a * 6 + 2] = y;
                            blockListContent[x, y, z, a * 6 + 3] = z + a - 1;

                            DebrisEffectAtPoint((float)(blockListContent[x, y, z, a * 6 + 1]), (float)(blockListContent[x, y, z, a * 6 + 2]), (float)(blockListContent[x, y, z, a * 6 + 3]), BlockType.Highlight, 1);
                            //if (player != null)
                            //    SendServerMessageToPlayer("lposz:" + (a - 1) + " " + blockList[blockListContent[x, y, z, a * 6 + 1], blockListContent[x, y, z, a * 6 + 2], blockListContent[x, y, z, a * 6 + 3]], player.NetConn);

                        }
                        else if (blockListContent[x, y, z, 2] == 4)
                        {
                            if (!HingeBlockTypes(blockList[x, y, z - (a - 1)], team))
                            {
                                br = a;
                                break;
                            }
                            else
                            {
                                if (blockCreatorTeam[x, y, z - (a - 1)] != team)
                                {
                                    br = a;
                                    break;
                                }
                            }

                            blockListContent[x, y, z, a * 6] = (byte)blockList[x, y, z - (a - 1)];
                            blockListContent[x, y, z, a * 6 + 1] = x;
                            blockListContent[x, y, z, a * 6 + 2] = y;
                            blockListContent[x, y, z, a * 6 + 3] = z - (a - 1);

                            DebrisEffectAtPoint((float)(blockListContent[x, y, z, a * 6 + 1]), (float)(blockListContent[x, y, z, a * 6 + 2]), (float)(blockListContent[x, y, z, a * 6 + 3]), BlockType.Highlight, 1);

                            //if (player != null)
                            //    SendServerMessageToPlayer("lnegz:" + (a - 1) + " " + blockList[blockListContent[x, y, z, a * 6 + 1], blockListContent[x, y, z, a * 6 + 2], blockListContent[x, y, z, a * 6 + 3]], player.NetConn);

                        }
                    }
                    if (br < 7)//clear rest of data
                    {
                        for (int b = br; br < 7; br++)
                        {
                            blockListContent[x, y, z, b * 6] = 0;
                            blockListContent[x, y, z, b * 6 + 1] = 0;
                            blockListContent[x, y, z, b * 6 + 2] = 0;
                            blockListContent[x, y, z, b * 6 + 3] = 0;
                        }
                    }
                }
                else if (btn == 3)
                {
                    if (blockListContent[x, y, z, 1] == 1)
                    {
                        if (blockListContent[x, y, z, 5] == 0)
                        {
                            blockListContent[x, y, z, 5] = 1;//down

                            if (player != null)
                                SendServerMessageToPlayer("The hinge now swings downward.", player.NetConn);
                        }
                        else if (blockListContent[x, y, z, 5] == 1)
                        {
                            blockListContent[x, y, z, 5] = 0;//up

                            if (player != null)
                                SendServerMessageToPlayer("The hinge now swings upward.", player.NetConn);
                        }
                    }
                    else
                    {
                        if (player != null)
                            SendServerMessageToPlayer("The hinge must return to its horizontal state first.", player.NetConn);
                    }

                }
                return true;
            }

            if (blockList[x, y, z] != BlockType.None && blockList[x, y, z] != BlockType.Water && blockList[x, y, z] != BlockType.Lava && blockList[x, y, z] != BlockType.Lever && blockList[x, y, z] != BlockType.Plate && player == null)
            {
                //activated by a lever?
                Vector3 originVector = new Vector3(x, y, z);
                Vector3 destVector = new Vector3(ox, oy, oz);

                Vector3 finalVector = destVector - originVector;
                finalVector.Normalize();
                blockListContent[x, y, z, 10] = 1;
                blockListContent[x, y, z, 11] = (int)(finalVector.X * 100);
                blockListContent[x, y, z, 12] = (int)(finalVector.Y * 100) + 50;
                blockListContent[x, y, z, 13] = (int)(finalVector.Z * 100);
                blockListContent[x, y, z, 14] = x * 100;
                blockListContent[x, y, z, 15] = y * 100;
                blockListContent[x, y, z, 16] = z * 100;

                if (blockList[ox, oy, oz] == BlockType.Lever)
                {
                    for (int a = 1; a < 7; a++)
                    {
                        if (blockListContent[ox, oy, oz, a * 6] > 0)
                        {
                            if (blockListContent[ox, oy, oz, a * 6 + 1] == x && blockListContent[ox, oy, oz, a * 6 + 2] == y && blockListContent[ox, oy, oz, a * 6 + 3] == z)
                            {
                                return false;//this removes link from switch
                            }
                        }
                    }
                }
                else if (blockList[ox, oy, oz] == BlockType.Plate)
                {
                    for (int a = 1; a < 7; a++)
                    {
                        if (blockListContent[ox, oy, oz, a * 6] > 0)
                        {
                            if (blockListContent[ox, oy, oz, a * 6 + 1] == x && blockListContent[ox, oy, oz, a * 6 + 2] == y && blockListContent[ox, oy, oz, a * 6 + 3] == z)
                            {
                                return false;//this removes link from switch
                            }
                        }
                    }
                }
            }
            return false;
        }

        public void ResearchRecalculate(PlayerTeam team, int cc)
        {
            if (cc == 1)//modifying maximum hp
            {
                foreach (Player p in playerList.Values)
                    if (p.Team == team)
                    {
                        p.HealthMax += 5;// (uint)(ResearchComplete[(byte)team, cc] * 20);
                        SendResourceUpdate(p);
                    }
            }
            else if (cc == 2)
            {
                foreach (Player p in playerList.Values)
                    if (p.Team == team)
                    {
                        p.WeightMax += 1;// (uint)(ResearchComplete[(byte)team, cc]);
                        p.OreMax += 10;// (uint)(ResearchComplete[(byte)team, cc] * 20);
                        SendResourceUpdate(p);
                    }
            }
            else if (cc == 3)
            {
                teamRegeneration[(byte)team]++;
            }

           // SendResourceUpdate(p);
        }

        public void PlayerInteract(Player player, uint btn, uint x, uint y, uint z)
        {
            Trigger((int)(x), (int)(y), (int)(z), 0, 0, 0, (int)(btn), player, 0);
            //we're not sending players origin or range checking currently
        }

        public void DepositOre(Player player)
        {
            if (SiegeBuild == false)
            {
                int depositAmount = Math.Min((int)player.OreMax, (int)player.Ore);
                player.Ore -= (uint)depositAmount;

                if (player.Team == PlayerTeam.Red)
                {
                    teamOreRed = Math.Min(teamOreRed + depositAmount, 9999);
                    if (teamOreRed > 30)//prevents spammy
                        OreMessage[(byte)PlayerTeam.Red] = false;

                }
                else
                {
                    teamOreBlue = Math.Min(teamOreBlue + depositAmount, 9999);
                    if (teamOreBlue > 30)//prevents spammy
                        OreMessage[(byte)PlayerTeam.Blue] = false;
                }
            }
        }

        public void WithdrawOre(Player player)
        {
            if (SiegeBuild == false)
            {
                if (player.Team == PlayerTeam.Red)
                {
                    int withdrawAmount = Math.Min((int)player.OreMax - (int)player.Ore, Math.Min((int)player.OreMax, teamOreRed));
                    if (withdrawAmount > 0)
                    {
                        player.Ore += (uint)withdrawAmount;
                        teamOreRed -= withdrawAmount;
                    }
                }
                else
                {
                    int withdrawAmount = Math.Min((int)player.OreMax - (int)player.Ore, Math.Min((int)player.OreMax, teamOreBlue));
                    if (withdrawAmount > 0)
                    {
                        player.Ore += (uint)withdrawAmount;
                        teamOreBlue -= withdrawAmount;
                    }
                }
            }
        }

        public void GetNewHighestItem()
        {
            highestitem = 0;
            foreach (uint hi in itemIDList)
            {
                if (hi > highestitem)
                    highestitem = (int)hi;
            }
        }

        public void DeleteItem(uint ID)
        {
            SendSetItem(ID);
            itemList.Remove(ID);
            itemIDList.Remove(ID);
            if (ID == highestitem)
            {
                GetNewHighestItem();
            }
        }

        public void GetItem(Player player,uint ID)
        {
            if (player.Alive)
            {    
                foreach (KeyValuePair<uint, Item> bPair in itemList)//itemList[ID] for speed?
                {
                    if (bPair.Value.ID == ID && bPair.Value.Disposing == false)
                    {

                        if (Distf((player.Position - Vector3.UnitY * 0.5f), bPair.Value.Position) < 1.2)
                        {
                            if (bPair.Value.Type == ItemType.Ore)
                            {
                                while (player.Ore < player.OreMax)
                                {
                                    if (bPair.Value.Content[5] > 0)
                                    {
                                        player.Ore += 10;//add to players ore
                                        bPair.Value.Content[5] -= 1;//take away content of item
                                        bPair.Value.Content[1] = 0;//reset decay timer
                                        if (player.Ore >= player.OreMax)
                                        {
                                            player.Ore = player.OreMax;//exceeded weight
                                            SendOreUpdate(player);
                                            SendCashUpdate(player);
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        SendOreUpdate(player);//run out of item content
                                        SendCashUpdate(player);
                                        
                                        break;
                                    }
                                }

                                if (bPair.Value.Content[5] > 0)//recalc scale if item still has content
                                {
                                    bPair.Value.Scale = 0.5f + (float)(bPair.Value.Content[5]) * 0.05f;
                                    SendItemScaleUpdate(bPair.Value);
                                }
                                else//removing item, no content left
                                {
                                    itemList[ID].Disposing = true;
                                    SendSetItem(ID);
                                    itemList.Remove(ID);
                                    itemIDList.Remove(ID);
                                    if (ID == highestitem)
                                    {
                                        GetNewHighestItem();
                                    }
                                }
                            }
                            else if (bPair.Value.Type == ItemType.Gold)
                            {
                                while (player.Weight < player.WeightMax)
                                {
                                    if (bPair.Value.Content[5] > 0)
                                    {
                                        player.Weight += 1;
                                        player.Cash += 10;
                                        bPair.Value.Content[5] -= 1;

                                        if(player.Weight >= player.WeightMax)
                                        {
                                            player.Weight = player.WeightMax;
                                            SendWeightUpdate(player);
                                            SendCashUpdate(player); 
                                            break;
                                        }
                                    }
                                    else//item out of content
                                    {
                                        SendWeightUpdate(player);
                                        SendCashUpdate(player); 
                                        break;
                                    }
                                }

                                if (bPair.Value.Content[5] > 0)//recalc scale if item remains
                                {
                                    bPair.Value.Scale = 0.5f + (float)(bPair.Value.Content[5]) * 0.1f;
                                    SendItemScaleUpdate(bPair.Value);
                                }
                                else//item out of content
                                {
                                    itemList[ID].Disposing = true;
                                    SendSetItem(ID);
                                    itemList.Remove(ID);
                                    itemIDList.Remove(ID);
                                    if (ID == highestitem)
                                    {
                                        GetNewHighestItem();
                                    }
                                }
                            }
                            else if(bPair.Value.Type == ItemType.Artifact)
                            {
                                if (player.Content[10] == 0 && itemList[ID].Content[6] == 0)//[6] = locked//empty artifact slot
                                {
                                    player.Content[10] = (int)(itemList[ID].Content[10]);//artifact type
                                    SendContentSpecificUpdate(player, 10);//tell player 
                                    SendPlayerContentUpdate(player, 10);//tell everyone else
                                    itemList[ID].Disposing = true;
                                    SendSetItem(ID);
                                    itemList.Remove(ID);
                                    itemIDList.Remove(ID);
                                    if (ID == highestitem)
                                    {
                                        GetNewHighestItem();
                                    }
                                }
                            }
                            else if (bPair.Value.Type == ItemType.Diamond)
                            {
                                if (player.Weight < player.WeightMax)
                                { 
                                    player.Weight += 1;   
                                    SendWeightUpdate(player);
                                    player.Content[11] += 1;//shardcount
                                    SendContentSpecificUpdate(player, 11);//tell player 
                                    SendPlayerContentUpdate(player, 11);//tell everyone else
                                    itemList[ID].Disposing = true;
                                    SendSetItem(ID);
                                    itemList.Remove(ID);
                                    itemIDList.Remove(ID);
                                    if (ID == highestitem)
                                    {
                                        GetNewHighestItem();
                                    }
                                    SendServerMessageToPlayer("You now possess a powerstone to fuel our forge!", player.NetConn);
                                }
                            }
                            else
                            {
                                //just remove this unknown item
                                itemList[ID].Disposing = true;
                                SendSetItem(ID);
                                itemList.Remove(ID);
                                itemIDList.Remove(ID);
                                if (ID == highestitem)
                                {
                                    GetNewHighestItem();
                                }
                            }

                            PlaySound(InfiniminerSound.CashDeposit, player.Position);
                        }
                        return;
                    }
                }
            }
        }
        public void DepositCash(Player player)
        {
            if (player.Cash <= 0)
                return;

            if (SiegeBuild == false)
            {
                player.Score += player.Cash;
                player.Exp += player.Cash;

                if (player.Team == PlayerTeam.Red)
                    teamCashRed += player.Cash;
                else
                    teamCashBlue += player.Cash;
                // SendServerMessage("SERVER: " + player.Handle + " HAS EARNED $" + player.Cash + " FOR THE " + GetTeamName(player.Team) + " TEAM!");

                PlaySound(InfiniminerSound.CashDeposit, player.Position);
                //ConsoleWrite("DEPOSIT_CASH: " + player.Handle + ", " + player.Cash);

                player.Cash = 0;
                player.Weight = (uint)(player.Content[11]);//weight is now only powerstones on hand

                if (player.Class == PlayerClass.Miner)
                    if (player.Content[5] > 0)
                        player.Weight += 10;

                SendWeightUpdate(player);
                SendCashUpdate(player);

                foreach (Player p in playerList.Values)
                {
                    SendTeamCashUpdate(p);
                }
            }
        }

        public string GetTeamName(PlayerTeam team)
        {
            switch (team)
            {
                case PlayerTeam.Red:
                    return "RED";
                case PlayerTeam.Blue:
                    return "BLUE";
            }
            return "";
        }

        public void SendServerMessageToPlayer(string message, NetConnection conn)
        {
            if (conn.Status == NetConnectionStatus.Connected)
            {
                NetBuffer msgBuffer = netServer.CreateBuffer();
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                msgBuffer.Write((byte)ChatMessageType.SayAll);
                msgBuffer.Write(Defines.Sanitize(message));

                netServer.SendMessage(msgBuffer, conn, NetChannel.ReliableInOrder3);
            }
        }

        public void SendServerMessage(string message)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
            msgBuffer.Write((byte)ChatMessageType.SayAll);
            msgBuffer.Write(Defines.Sanitize(message));
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
        }

        public void SendServerMessageBlue(string message)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
            msgBuffer.Write((byte)ChatMessageType.SayBlueTeam);
            msgBuffer.Write(Defines.Sanitize(message));

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    if(playerList[netConn].Team == PlayerTeam.Blue)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
        }

        public void SendServerMessageRed(string message)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
            msgBuffer.Write((byte)ChatMessageType.SayRedTeam);
            msgBuffer.Write(Defines.Sanitize(message));
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    if (playerList[netConn].Team == PlayerTeam.Red)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
        }
        // Lets a player know about their resources.
        public void SendResourceUpdate(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ResourceUpdate);
            msgBuffer.Write((uint)player.Ore);
            msgBuffer.Write((uint)player.Cash);
            msgBuffer.Write((uint)player.Weight);
            msgBuffer.Write((uint)player.OreMax);
            msgBuffer.Write((uint)player.WeightMax);
            msgBuffer.Write((uint)(player.Team == PlayerTeam.Red ? teamOreRed : teamOreBlue));
            msgBuffer.Write((uint)teamCashRed);
            msgBuffer.Write((uint)teamCashBlue);
            msgBuffer.Write((uint)teamArtifactsRed);
            msgBuffer.Write((uint)teamArtifactsBlue);
            msgBuffer.Write((uint)winningCashAmount);
            msgBuffer.Write((uint)player.Health);
            msgBuffer.Write((uint)player.HealthMax);
           // msgBuffer.Write((int)player.Content[5]);
            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void SendTeamCashUpdate(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TeamCashUpdate);
            msgBuffer.Write((uint)teamCashRed);
            msgBuffer.Write((uint)teamCashBlue);
            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void SendTeamOreUpdate(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TeamOreUpdate);
            if (player.Team == PlayerTeam.Red)
                msgBuffer.Write((uint)teamOreRed);
            else if (player.Team == PlayerTeam.Blue)
                msgBuffer.Write((uint)teamOreBlue);
            else
                return;
            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }
        public void SendContentUpdate(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ContentUpdate);

            for(int a = 0;a < 50; a++)
            msgBuffer.Write((int)(player.Content[a]));

            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void SendHealthUpdate(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.HealthUpdate);
            msgBuffer.Write(player.Health);

            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void SendWeightUpdate(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.WeightUpdate);
            msgBuffer.Write(player.Weight);

            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void SendOreUpdate(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.OreUpdate);
            msgBuffer.Write(player.Ore);

            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void SendCashUpdate(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.CashUpdate);
            msgBuffer.Write(player.Cash);

            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void SendStatusEffectSpecificUpdate(Player p, int cc)
        {
            if (p.NetConn.Status != NetConnectionStatus.Connected)
                return;

            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.StatusEffectUpdate);
            msgBuffer.Write(p.ID);
            msgBuffer.Write(cc);
            msgBuffer.Write(p.StatusEffect[cc]);

            netServer.SendMessage(msgBuffer, p.NetConn, NetChannel.ReliableInOrder1);  
        }

        public void SendStatusEffectUpdate(Player p, int cc)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.StatusEffectUpdate);
            msgBuffer.Write(p.ID);
            msgBuffer.Write(cc);
            msgBuffer.Write(p.StatusEffect[cc]);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder1);
        }

        public void SendActiveArtifactUpdate(PlayerTeam team, int cc)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ActiveArtifactUpdate);
            msgBuffer.Write((byte)team);
            msgBuffer.Write(cc);
            msgBuffer.Write(artifactActive[(byte)team,cc]);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder1);
        }

        public void SendItemUpdate(Item i)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ItemUpdate);
            msgBuffer.Write(i.ID);
            msgBuffer.Write(i.Position);

            foreach (NetConnection netConn in playerList.Keys)
               if (netConn.Status == NetConnectionStatus.Connected)
                   netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder1);
        }

        public void SendScoreUpdate()
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ScoreUpdate);
            msgBuffer.Write(teamArtifactsRed);
            msgBuffer.Write(teamArtifactsBlue);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder1);
        }

        public void SendItemContentSpecificUpdate(Item i, int cc)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ItemContentSpecificUpdate);
            msgBuffer.Write(i.ID);
            msgBuffer.Write(cc);
            msgBuffer.Write(i.Content[cc]);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder1);
        }
        public void SendItemScaleUpdate(Item i)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ItemScaleUpdate);
            msgBuffer.Write(i.ID);
            msgBuffer.Write(i.Scale);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder1);
        }

        public void SendContentSpecificUpdate(Player player, int s)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ContentSpecificUpdate);
            msgBuffer.Write((int)(s));
            msgBuffer.Write((int)(player.Content[s]));

            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder1);
        }

        public void SendPlayerPosition(Player player)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerPosition);
            msgBuffer.Write(player.Position);
            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableUnordered);
        }

        public void SendPlayerVelocity(Player player, Vector3 velo)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerVelocity);
            msgBuffer.Write(velo);
            netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableUnordered);
        }
        List<MapSender> mapSendingProgress = new List<MapSender>();

        public void TerminateFinishedThreads()
        {
            List<MapSender> mapSendersToRemove = new List<MapSender>();
            foreach (MapSender ms in mapSendingProgress)
            {
                if (ms.finished)
                {
                    ms.stop();
                    mapSendersToRemove.Add(ms);
                }
            }
            foreach (MapSender ms in mapSendersToRemove)
            {
                mapSendingProgress.Remove(ms);
            }
        }

        public void SendCurrentMap(NetConnection client)
        {
            MapSender ms = new MapSender(client, this, netServer, MAPSIZE, playerList[client].compression);
            mapSendingProgress.Add(ms);
        }

        /*public void SendCurrentMapB(NetConnection client)
        {
            Debug.Assert(MAPSIZE == 64, "The BlockBulkTransfer message requires a map size of 64.");
            
            for (byte x = 0; x < MAPSIZE; x++)
                for (byte y=0; y<MAPSIZE; y+=16)
                {
                    NetBuffer msgBuffer = netServer.CreateBuffer();
                    msgBuffer.Write((byte)InfiniminerMessage.BlockBulkTransfer);
                    msgBuffer.Write(x);
                    msgBuffer.Write(y);
                    for (byte dy=0; dy<16; dy++)
                        for (byte z = 0; z < MAPSIZE; z++)
                            msgBuffer.Write((byte)(blockList[x, y+dy, z]));
                    if (client.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, client, NetChannel.ReliableUnordered);
                }
        }*/
        public void Auth_Slap(Player p, uint playerId)
        {
            if (p.Digs >= p.GetToolCooldown(PlayerTools.Pickaxe))
            {
                p.Digs -= p.GetToolCooldown(PlayerTools.Pickaxe);
                p.LastHit = DateTime.Now;

                foreach (Player pt in playerList.Values)
                {
                    if (pt.ID == playerId)
                    {
                        if (p.Content[10] == 8)//medical 
                        {
                            if (pt.Alive)
                            {
                                if (pt.Team == p.Team)
                                {
                                    if (pt.Health < pt.HealthMax)//heal friendly
                                    {
                                        pt.Health += 10 + (uint)(ResearchComplete[(byte)p.Team, (byte)Research.Fortify] * 2);
                                        p.Score += 2;
                                        p.Exp += 2;
                                        if (pt.Health > pt.HealthMax)
                                            pt.Health = pt.HealthMax;

                                        SendHealthUpdate(pt);
                                        EffectAtPoint(pt.Position - Vector3.UnitY * 1.5f, 4);
                                    }
                                }
                                else if (pt.Team != p.Team)//bane enemy
                                {
                                    if(p.StatusEffect[4] == 0)
                                    if (pt.StatusEffect[2] < 200 && artifactActive[(byte)pt.Team, 8] == 0)
                                    {
                                        if (pt.StatusEffect[2] == 0)
                                        {
                                            SendServerMessageToPlayer("You have been infected!", pt.NetConn);
                                        }
                                        pt.LastHit = DateTime.Now;
                                        p.Score += 1;
                                        p.Exp += 1;
                                        PlaySound(InfiniminerSound.Slap, pt.Position);
                                        pt.StatusEffect[2] += 20 + ResearchComplete[(byte)p.Team, (byte)Research.Destruction] * 4;
                                        EffectAtPoint(pt.Position - Vector3.UnitY * 1.5f, 2);
                                    }
                                    else if (artifactActive[(byte)pt.Team, 8] == 0)
                                    {
                                        pt.StatusEffect[2] = 200;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (pt.Team != p.Team && pt.Alive && pt.StatusEffect[4] == 0)
                                if (Distf(p.Position, pt.Position) < 4.0f)//slap in range
                                {
                                    uint damage = 0;
                                    if (p.Content[10] == 11)//judgement
                                    {
                                        damage = 10 + (uint)(ResearchComplete[(byte)p.Team, (byte)Research.Destruction]);
                                        damage += pt.HealthMax / 10;
                                    }
                                    else
                                    {
                                        damage = 10 + (uint)(ResearchComplete[(byte)p.Team, (byte)Research.Destruction]);
                                    }

                                    damage += (uint)(artifactActive[(byte)p.Team, 11]*2);//judgement boost

                                    if (pt.Health > damage)
                                    {
                                        pt.Health -= damage;
                                        NetBuffer msgBuffer = netServer.CreateBuffer();
                                        msgBuffer.Write((byte)InfiniminerMessage.PlayerSlap);
                                        msgBuffer.Write(playerId);//getting slapped
                                        msgBuffer.Write(p.ID);//attacker
                                        SendHealthUpdate(pt);
                                        p.Score += 1;
                                        p.Exp += 1;
                                        pt.LastHit = DateTime.Now;

                                        foreach (NetConnection netConn in playerList.Keys)
                                            if (netConn.Status == NetConnectionStatus.Connected)
                                                netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);

                                        if (p.Content[10] == 2)//vampiric personal
                                        {
                                            p.Health += 5 + (uint)(ResearchComplete[(byte)p.Team, (byte)Research.Destruction] / 2);
                                            if (p.Health > p.HealthMax)
                                                p.Health = p.HealthMax;

                                            SendHealthUpdate(p);
                                        }
                                        if (pt.Content[10] == 7)//reflection personal
                                        {
                                            if (p.Health > damage + (uint)(ResearchComplete[(byte)p.Team, (byte)Research.Destruction]/2))
                                            {
                                                p.Health -= damage + (uint)(ResearchComplete[(byte)p.Team, (byte)Research.Destruction]/2);
                                                SendHealthUpdate(p);
                                                p.LastHit = DateTime.Now;
                                            }
                                            else
                                            {
                                                pt.Score += 10;
                                                pt.Exp += 10;
                                                Player_Dead(p, "slapped themselves silly!");
                                            }
                                        }
                                        if (p.Content[10] == 15)//doom
                                        {
                                            if (artifactActive[(byte)pt.Team, 8] > 0 && pt.StatusEffect[6] == 0)
                                            {
                                                if (randGen.Next(20) == 1)
                                                {
                                                    SendServerMessageToPlayer("Medical science has saved the day!", pt.NetConn);
                                                }
                                            }
                                            else if (pt.StatusEffect[6] == 0)
                                            {
                                                pt.StatusEffect[6] = randGen.Next(15, 30);
                                                SendServerMessageToPlayer("You have been doomed! Sooner or later..", pt.NetConn);
                                            }
                                            
                                        }
                                        else if (p.Content[10] == 16)//inferno
                                        {
                                            if (artifactActive[(byte)pt.Team, 8] > 0 && pt.StatusEffect[7] == 0)
                                            {
                                                if (randGen.Next(20) == 1)
                                                {
                                                    SendServerMessageToPlayer("Medical science has saved the day!", pt.NetConn);
                                                }
                                            }
                                            else if (pt.StatusEffect[7] == 0)
                                            {
                                                pt.StatusEffect[7] = 20;
                                                SendServerMessageToPlayer("You have been set aflame!", pt.NetConn);
                                            }
                                        }
                                        if (pt.StatusEffect[3] > 0)
                                        {
                                            pt.StatusEffect[3] = 0;
                                            SendServerMessageToPlayer("Treatment lost!", pt.NetConn);
                                        }
                                        if (artifactActive[(byte)p.Team, 2] != 0)//vampiric team
                                        {
                                            p.Health += (uint)artifactActive[(byte)p.Team, 2] * 2;
                                            if (p.Health > p.HealthMax)
                                                p.Health = p.HealthMax;

                                            SendHealthUpdate(p);
                                        }
                                    }
                                    else
                                    {
                                        p.Score += 10;
                                        p.Exp += 10;

                                        if (p.Content[10] == 11)//judgement
                                        {
                                            Player_Dead(pt, "was judged!");//slapped to death
                                        }
                                        else
                                        {
                                            Player_Dead(pt, "was slapped down!");//slapped to death
                                        }
                                    }

                                }
                        }
                        break;
                    }
                }
            }
        }

        public void SendPlayerPing(uint playerId)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerPing);
            msgBuffer.Write(playerId);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
        }

        public void SendPlayerUpdate(Player player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerUpdate);
            msgBuffer.Write((uint)player.ID);
            msgBuffer.Write(player.Position);
            msgBuffer.Write(player.Heading);
            msgBuffer.Write((byte)player.Tool);

            if (player.QueueAnimationBreak)
            {
                player.QueueAnimationBreak = false;
                msgBuffer.Write(false);
            }
            else
                msgBuffer.Write(player.UsingTool);

            msgBuffer.Write((ushort)player.Score);
            msgBuffer.Write((ushort)player.Health);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.UnreliableInOrder1);
        }

        public void SendSetBeacon(Vector3 position, string text, PlayerTeam team)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.SetBeacon);
            msgBuffer.Write(position);
            msgBuffer.Write(text);
            msgBuffer.Write((byte)team);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
        }

        public void SendPlayerContentUpdate(Player p, int cc)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerContentUpdate);
            msgBuffer.Write(p.ID);
            msgBuffer.Write(cc);
            msgBuffer.Write(p.Content[cc]);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    if (playerList[netConn] != p)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
        }

        public void SendPlayerOreWarning(Player p)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.OreWarning);

                if (p.NetConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, p.NetConn, NetChannel.ReliableInOrder2);
        }

        public void SendSetItem(uint id, ItemType iType, Vector3 position, PlayerTeam team, Vector3 heading)//update player joined also
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.SetItem);
            msgBuffer.Write((byte)iType);
            msgBuffer.Write(id);
            msgBuffer.Write(position);
            msgBuffer.Write((byte)team);
            msgBuffer.Write(heading);
            msgBuffer.Write(itemList[id].Content[1]);
            msgBuffer.Write(itemList[id].Content[2]);
            msgBuffer.Write(itemList[id].Content[3]);
            msgBuffer.Write(itemList[id].Content[10]);
            
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
        }
        public void SendSetItem(uint id)//empty item with no heading
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.SetItemRemove);
            msgBuffer.Write(id);

            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
        }
        public void SendPlayerJoined(Player player, bool resume)
        {
            if (player.Team == PlayerTeam.None)
            {
                if (varGetI("siege") > 0 && varGetI("siege") != 4)
                {
                    player.Team = PlayerTeam.Red;
                }
                else
                {
                    int redteam = 0;
                    int blueteam = 0;
                    foreach (Player p in playerList.Values)
                    {
                        if (p.Team == PlayerTeam.Red && p != player)
                        {
                            redteam++;
                        }
                        else if (p.Team == PlayerTeam.Blue && p != player)
                        {
                            blueteam++;
                        }
                    }

                    if (redteam <= blueteam || redteam == 0)
                    {
                        player.Team = PlayerTeam.Red;
                    }
                    else
                    {
                        player.Team = PlayerTeam.Blue;
                    }
                }
            }
            NetBuffer msgBuffer;
            int nameExists = 0;
            // Let this player know about other players.
            if(!resume)
            foreach (Player p in playerList.Values)//name conflicts
            {
                if (p.Handle.ToUpper() == player.Handle.ToUpper() && p != player)
                {
                    nameExists++;
                }
            }
            if (nameExists > 0)
            {
                int num = 0;
                bool nameExist = true;
                string origHandle = player.Handle;

                while (nameExist)
                {
                    num++;
                    nameExist = false;
                    player.Handle = origHandle + "(" + num + ")"; 
                    foreach (Player p in playerList.Values)
                    {
                        if (player.Handle.ToUpper() == p.Handle.ToUpper() && p != player)
                        {
                            nameExist = true;
                            break;
                        }

                    }
                }

                player.Handle = origHandle + "(" + num + ")";
            }

            foreach (Player p in playerList.Values)
            {
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerJoined);
                msgBuffer.Write((uint)p.ID);
                msgBuffer.Write(p.Handle);
                msgBuffer.Write(p == player);
                msgBuffer.Write(p.Alive);
                if (player.NetConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder2);

                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerSetTeam);
                msgBuffer.Write((uint)p.ID);
                msgBuffer.Write((byte)p.Team);
                if (player.NetConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder2);

                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerSetClass);
                msgBuffer.Write((uint)p.ID);
                msgBuffer.Write((byte)p.Class);
                if (player.NetConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder2);
            }

            // Let this player know about active (aqua/water) artifacts.
            if (artifactActive[(byte)PlayerTeam.Blue, 4] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Blue, 4);
            if (artifactActive[(byte)PlayerTeam.Red, 4] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Red, 4);
            if (artifactActive[(byte)PlayerTeam.Red, 9] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Red, 9);
            if (artifactActive[(byte)PlayerTeam.Blue, 9] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Blue, 9);
            if (artifactActive[(byte)PlayerTeam.Red, 10] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Red, 10);
            if (artifactActive[(byte)PlayerTeam.Blue, 10] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Blue, 10);
            if (artifactActive[(byte)PlayerTeam.Red, 12] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Red, 12);
            if (artifactActive[(byte)PlayerTeam.Blue, 12] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Blue, 12);
            if (artifactActive[(byte)PlayerTeam.Red, 16] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Red, 16);
            if (artifactActive[(byte)PlayerTeam.Blue, 16] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Blue, 16);
            if (artifactActive[(byte)PlayerTeam.Red, 18] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Red, 18);
            if (artifactActive[(byte)PlayerTeam.Blue, 18] > 0)
                SendActiveArtifactUpdate(PlayerTeam.Blue, 18);
            //send active artifacts

            // Let this player know about all placed beacons and items.
            foreach (KeyValuePair<uint, Item> bPair in itemList)
            {
                Vector3 position = bPair.Value.Position;
                Vector3 heading = bPair.Value.Heading;
                
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.SetItem);
                msgBuffer.Write((byte)(bPair.Value.Type));
                msgBuffer.Write(bPair.Key);
                msgBuffer.Write(position);
                msgBuffer.Write((byte)bPair.Value.Team);
                msgBuffer.Write(heading);
                msgBuffer.Write(itemList[bPair.Key].Content[1]);
                msgBuffer.Write(itemList[bPair.Key].Content[2]);
                msgBuffer.Write(itemList[bPair.Key].Content[3]);
                msgBuffer.Write(itemList[bPair.Key].Content[10]);

                if (player.NetConn.Status == NetConnectionStatus.Connected)
                {
                    netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder2);

                    if (itemList[bPair.Key].Content[6] > 0)
                        SendItemContentSpecificUpdate(bPair.Value, 6);

                    if (itemList[bPair.Key].Scale != 0.5f)
                        SendItemScaleUpdate(itemList[bPair.Key]);
                }


            }

            foreach (KeyValuePair<Vector3, Beacon> bPair in beaconList)
            {
                Vector3 position = bPair.Key;
                position.Y += 1; //fixme
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.SetBeacon);
                msgBuffer.Write(position);
                msgBuffer.Write(bPair.Value.ID);
                msgBuffer.Write((byte)bPair.Value.Team);

                if (player.NetConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder2);
            }

            // Let other players know about this player.
            if (!resume)
            {
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerJoined);
                msgBuffer.Write((uint)player.ID);
                msgBuffer.Write(player.Handle);
                msgBuffer.Write(false);
                msgBuffer.Write(player.Alive);

                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn != player.NetConn && netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);

                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.PlayerSetTeam);
                msgBuffer.Write((uint)player.ID);
                msgBuffer.Write((byte)player.Team);
                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn != player.NetConn && netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);

                ConsoleWrite("AUTO TEAM: " + player.Handle + ", " + player.Team);

                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                msgBuffer.Write((byte)(player.Team == PlayerTeam.Red ? ChatMessageType.SayRedTeam : ChatMessageType.SayBlueTeam));
                msgBuffer.Write(player.Handle + " has joined the " + player.Team + " team!");
                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);

                SendPlayerRespawn(player);
            }
            player.Digs = 3.0f;

            // Send this out just incase someone is joining at the last minute.
            if (winningTeam != PlayerTeam.None)
                BroadcastGameOver();

            // Send out a chat message.
            if (!resume)
            {
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                msgBuffer.Write((byte)ChatMessageType.SayAll);
                msgBuffer.Write(player.Handle + " has joined the adventure!");
                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
            }
            else
            {
                msgBuffer = netServer.CreateBuffer();
                msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
                msgBuffer.Write((byte)ChatMessageType.SayAll);
                if (player.StatusEffect[6] > 0)
                {
                    msgBuffer.Write(player.Handle + " has rejoined the doomed adventure!");
                }
                else
                {
                    msgBuffer.Write(player.Handle + " has rejoined the adventure!");
                }
                foreach (NetConnection netConn in playerList.Keys)
                    if (netConn.Status == NetConnectionStatus.Connected)
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
            }
        }

        public void BroadcastGameOver()
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.GameOver);
            msgBuffer.Write((byte)winningTeam);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);     
        }

        public void SendPlayerLeft(Player player, string reason)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerLeft);
            msgBuffer.Write((uint)player.ID);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn != player.NetConn && netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);

            // Send out a chat message.
            msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.ChatMessage);
            msgBuffer.Write((byte)ChatMessageType.SayAll);
            msgBuffer.Write(player.Handle + " " + reason);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder3);
        }

        public void SendPlayerSetTeam(Player player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerSetTeam);
            msgBuffer.Write((uint)player.ID);
            msgBuffer.Write((byte)player.Team);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
        }

        public void SendPlayerSetClass(Player player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerSetClass);
            msgBuffer.Write((uint)player.ID);
            msgBuffer.Write((byte)player.Class);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)// && playerList[netConn].Team == player.Team)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
        }

        public void SendPlayerDead(Player player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerDead);
            msgBuffer.Write((uint)player.ID);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
        }

        public void SendPlayerRespawn(Player player)
        {
            if (player.NetConn.Status == NetConnectionStatus.Connected)
            {
                if (player.Alive && player.Team != PlayerTeam.None)//player isnt dead yet!
                {
                    SendPlayerAlive(player);
                    SendResourceUpdate(player);
                }
                else if (!player.Alive && player.Team != PlayerTeam.None && player.respawnTimer < DateTime.Now)
                {
                    //create respawn script
                    // Respawn a few blocks above a safe position above altitude 0.
                    bool positionFound = false;

                    // Try 20 times; use a potentially invalid position if we fail.
                    for (int i = 0; i < 30; i++)
                    {
                        // Pick a random starting point.

                        Vector3 startPos = new Vector3(randGen.Next(basePosition[player.Team].X - 2, basePosition[player.Team].X + 2), randGen.Next(basePosition[player.Team].Y - 1, basePosition[player.Team].Y + 25), randGen.Next(basePosition[player.Team].Z - 2, basePosition[player.Team].Z + 2));

                        // See if this is a safe place to drop.
                        //for (startPos.Y = 63; startPos.Y >= 54; startPos.Y--)
                        //{
                        BlockType blockType = BlockAtPoint(startPos);
                        if (blockType == BlockType.Vacuum && BlockAtPoint(startPos - Vector3.UnitY * 1.0f) == BlockType.Vacuum)
                        {
                            // We have found a valid place to spawn, so spawn a few above it.
                            player.Position = startPos;// +Vector3.UnitY * 5;
                            positionFound = true;
                            break;
                        }
                        // }

                        // If we found a position, no need to try anymore!
                        if (positionFound)
                            break;
                    }
                    // If we failed to find a spawn point, drop randomly.
                    if (!positionFound)
                    {
                        player.Position = new Vector3(randGen.Next(2, 62), 66, randGen.Next(2, 62));
                        ConsoleWrite("player had no space to spawn");
                    }

                    // Drop the player on the middle of the block, not at the corner.
                    player.Position += new Vector3(0.5f, 0, 0.5f);
                    //
                    player.FallBuffer = 40;
                    player.Content[11] = 0;
                    player.rTouch = DateTime.Now;
                    player.rTouching = true;
                    player.rCount = 0;
                    player.rUpdateCount = 0;
                    player.rSpeedCount = 0;
                    NetBuffer msgBuffer = netServer.CreateBuffer();
                    msgBuffer.Write((byte)InfiniminerMessage.PlayerRespawn);
                    msgBuffer.Write(player.Position);
                    netServer.SendMessage(msgBuffer, player.NetConn, NetChannel.ReliableInOrder3);
                }
            }
        }
        public void SendPlayerAlive(Player player)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlayerAlive);
            msgBuffer.Write((uint)player.ID);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableInOrder2);
        }

        public void PlaySound(InfiniminerSound sound, Vector3 position)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlaySound);
            msgBuffer.Write((byte)sound);
            msgBuffer.Write(true);
            msgBuffer.Write(position);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                    netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
        }

        public void PlaySoundForEveryoneElse(InfiniminerSound sound, Vector3 position, Player p)
        {
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.PlaySound);
            msgBuffer.Write((byte)sound);
            msgBuffer.Write(true);
            msgBuffer.Write(position);
            foreach (NetConnection netConn in playerList.Keys)
                if (netConn.Status == NetConnectionStatus.Connected)
                {
                    if (playerList[netConn] != p)
                    {
                        netServer.SendMessage(msgBuffer, netConn, NetChannel.ReliableUnordered);
                    }
                }
        }

        Thread updater;
        bool updated = true;
        private CancellationTokenSource updaterCts = new CancellationTokenSource();

        public void CommitUpdate()
        {
            try
            {
                if (updated)
                {
                    if (updater != null && !updater.IsAlive)
                    {
                        updaterCts.Cancel();
                        updater?.Join(1000); // Wait up to 1 second for thread to finish
                        updaterCts = new CancellationTokenSource();
                    }
                    updated = false;
                    updater = new Thread(() => RunUpdateThread(updaterCts.Token));
                    updater.Start();
                }
            }
            catch { }
        }

        public void RunUpdateThread(CancellationToken ct)
        {
            if (!updated && !ct.IsCancellationRequested)
            {
                Dictionary<string, string> postDict = new Dictionary<string, string>();
                postDict["name"] = "[IF] "+varGetS("name");
                postDict["game"] = "INFINIMINER";
                postDict["player_count"] = "" + playerList.Keys.Count;
                postDict["player_capacity"] = "" + varGetI("maxplayers");
                postDict["extra"] = GetExtraInfo();

                lastServerListUpdate = DateTime.Now;

                try
                {
                    if (!ct.IsCancellationRequested)
                    {
                        HttpRequest.Post(serverListURL + "/post", postDict);
                        ConsoleWrite("PUBLIC LIST: UPDATING SERVER LISTING");
                    }
                }
                catch (Exception)
                {
                    ConsoleWrite("PUBLIC LIST: ERROR CONTACTING SERVER");
                }

                updated = true;
            }
        }
    }
}