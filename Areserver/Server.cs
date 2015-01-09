using System;
using System.Collections.Generic;
using Lidgren.Network;
using System.Threading;
using System.Linq;

namespace Areserver
{
    public class Server
    {
        const int MapWidth = 20;
        const int MapHeight = 20;
        private static string commandBuffer = string.Empty;
        private static NetServer server;

        private static List<Actor> dActors;
        private static Tile[,] dMap;

        public static void Main(string[] args)
        {
            Out("Welcome to the Areserver");
            SetupReadLine();
            InitLocalData();
            SetupServer();

            while (true)
            {
                HandleLidgrenMessages();
                System.Threading.Thread.Sleep(8);
            }
        }

        private static void InitLocalData()
        {
            dActors = new List<Actor>();
            dMap = new Tile[MapWidth, MapHeight];
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    dMap[x, y] = new GrassTile();
                }
            }
        }

        private static void SetupServer()
        {
            NetPeerConfiguration config = new NetPeerConfiguration("ares");
            config.MaximumConnections = 32;
            config.Port = 12345;
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            server = new NetServer(config);
            server.Start();
        }

        private static void HandleLidgrenMessages()
        {
            NetIncomingMessage msg;
            while ((msg = server.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.ErrorMessage:
                        Out(msg.ReadString());
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var newStatus = (NetConnectionStatus)msg.ReadByte();
                        if (newStatus == NetConnectionStatus.Connected)
                        {
                            OnConnect(msg);
                        }
                        else if (newStatus == NetConnectionStatus.Disconnected)
                        {
                            OnDisconnect(msg);
                        }
                        break;
                    case NetIncomingMessageType.Data:
                        HandleGameMessage(msg);
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
					    //TODO: ping? pong!
                        break;
                    default:
                        Out(string.Format("Unhandled type: {0}", msg.MessageType));
                        break;
                }
                server.Recycle(msg);
            }
        }

        private static void OnConnect(NetIncomingMessage msg)
        {
            //tell everyone else he joined
            {
                NetOutgoingMessage outMsg = server.CreateMessage();
                outMsg.Write("JOIN");
                outMsg.Write(msg.SenderConnection.RemoteUniqueIdentifier);
                server.SendToAll(outMsg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            }

            Out(string.Format("JOIN: {0}", msg.SenderConnection.RemoteUniqueIdentifier));

            InformNewbieState(msg);

            //intial data finished sending; add him to the player list, tag his Player for easy access
            Player thisPlayer = new Player();
            thisPlayer.UID = msg.SenderConnection.RemoteUniqueIdentifier;
            dActors.Add(thisPlayer);
            msg.SenderConnection.Tag = thisPlayer;
        }

        private static void InformNewbieState(NetIncomingMessage msg)
        {
            //send lots of JOIN, NAME, LIFE
            foreach (var actor in dActors) //not using server.Connections
            {
                Player plr = (Player)actor;
                NetOutgoingMessage outMsgPlayerList = server.CreateMessage();
                outMsgPlayerList.Write("JOIN");
                outMsgPlayerList.Write(plr.UID);
                server.SendMessage(outMsgPlayerList, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);

                NetOutgoingMessage outMsgOtherName = server.CreateMessage();
                outMsgOtherName.Write("NAME");
                outMsgOtherName.Write(plr.UID); //long uid
                outMsgOtherName.Write(plr.Name); //string name
                server.SendMessage(outMsgOtherName, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);

                NetOutgoingMessage outMsgOtherLife = server.CreateMessage();
                outMsgOtherLife.Write("LIFE");
                outMsgOtherLife.Write(plr.UID);
                outMsgOtherLife.Write(plr.Life);
                server.SendMessage(outMsgOtherLife, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
            }

            //send lots of builds
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    Tile tileHere = dMap[x, y];
                    NetOutgoingMessage outMsgBuildData = server.CreateMessage();
                    outMsgBuildData.Write("BUILD");
                    outMsgBuildData.Write(tileHere.OwnerUID);
                    outMsgBuildData.Write(x);
                    outMsgBuildData.Write(y);
                    outMsgBuildData.Write(tileHere.TileID);
                    server.SendMessage(outMsgBuildData, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                }
            }
        }

        private static void OnDisconnect(NetIncomingMessage msg)
        {
            NetOutgoingMessage outMsg = server.CreateMessage();
            outMsg.Write("PART");
            outMsg.Write(msg.SenderConnection.RemoteUniqueIdentifier);

            server.SendToAll(outMsg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            Out(string.Format("PART: {0}", msg.SenderConnection.RemoteUniqueIdentifier));

            //remove datas
            dActors.Remove((Player)msg.SenderConnection.Tag);
        }

        private static void HandleGameMessage(NetIncomingMessage msg)
        {
            string type = msg.ReadString();

            switch (type)
            {
                case "POS":
                    HandlePOS(msg);
                    break;
                case "LIFE":
                    HandleLIFE(msg);
                    break;
                case "CHAT":
                    HandleCHAT(msg);
                    break;
                case "BUILD":
                    HandleBUILD(msg);
                    break;
                case "FIRE":
                    HandleFIRE(msg);
                    break;
                case "NAME":
                    HandleNAME(msg);
                    break;
            }
        }

        private static Player GetPlayerFromUID(long uid)
        {
            foreach (var actor in dActors)
            {
                if (actor.GetType() != typeof(Player))
                    throw new Exception("(Actors & ~Players) are dumb");
                Player plr = (Player)actor;
                if (plr.UID == uid)
                {
                    return plr;
                }
            }
            throw new Exception("not found");
        }

        #region HandleX
        static void HandlePOS(NetIncomingMessage msg)
        {
            float newX = msg.ReadFloat();
            float newY = msg.ReadFloat();

            //save position
            Player plr = GetPlayerFromUID(msg.SenderConnection.RemoteUniqueIdentifier);
            plr.X = newX;
            plr.Y = newY;

            //inform everyone about position change
            NetOutgoingMessage outMsg = server.CreateMessage();
            outMsg.Write("POS");
            outMsg.Write(msg.SenderConnection.RemoteUniqueIdentifier);
            outMsg.Write(newX);
            outMsg.Write(newY);
            server.SendToAll(outMsg, msg.SenderConnection, NetDeliveryMethod.Unreliable, 0);
        }

        static void HandleLIFE(NetIncomingMessage msg)
        {
            //no longer boolean
            int newHp = msg.ReadInt32();
            Out(string.Format("LIFE: {0}: {1}", msg.SenderConnection.RemoteUniqueIdentifier, newHp));

            //save value
            GetPlayerFromUID(msg.SenderConnection.RemoteUniqueIdentifier).Life = newHp;

            //inform ALL clients about his pining for the fjords
            NetOutgoingMessage outMsgLife = server.CreateMessage();
            outMsgLife.Write("LIFE");
            outMsgLife.Write(msg.SenderConnection.RemoteUniqueIdentifier);
            outMsgLife.Write(newHp);
            server.SendToAll(outMsgLife, null, NetDeliveryMethod.ReliableOrdered, 0);
        }

        static void HandleCHAT(NetIncomingMessage msg)
        {
            string message = msg.ReadString();
            Out(string.Format("CHAT: {0}: {1}", msg.SenderConnection.RemoteUniqueIdentifier, message));

            //send the chat to ALL clients
            NetOutgoingMessage outMsg = server.CreateMessage();
            outMsg.Write("CHAT");
            outMsg.Write(msg.SenderConnection.RemoteUniqueIdentifier);
            outMsg.Write(message);
            server.SendToAll(outMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
        }

        static void HandleBUILD(NetIncomingMessage msg)
        {
            int buildX = msg.ReadInt32();
            int buildY = msg.ReadInt32();
            int buildType = msg.ReadInt32();
            Out(string.Format("BUILD: {0}: at ({1},{2}) {3}", msg.SenderConnection.RemoteUniqueIdentifier, buildX, buildY, buildType));
            //save block in array
            var newTile = Tile.ConstructFromID(buildType);
            if (newTile == null)
            {
                Out("BAD TILE ID");
                return;
            }
            dMap[buildX, buildY] = newTile;
            //send the build to ALL clients
            NetOutgoingMessage outMsg = server.CreateMessage();
            outMsg.Write("BUILD");
            outMsg.Write(msg.SenderConnection.RemoteUniqueIdentifier);
            outMsg.Write(buildX);
            outMsg.Write(buildY);
            outMsg.Write(buildType);
            server.SendToAll(outMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
        }

        static void HandleFIRE(NetIncomingMessage msg)
        {
            float fireX = msg.ReadFloat();
            float fireY = msg.ReadFloat();
            float fireAngle = msg.ReadFloat();
            float fireSpeed = msg.ReadFloat();
            Out(string.Format("FIRE: {0}: at ({1},{2}) ang={3} speed={4}", msg.SenderConnection.RemoteUniqueIdentifier, fireX, fireY, fireAngle, fireSpeed));
            //TODO: save?

            //send the fire to ALL clients
            NetOutgoingMessage outMsg = server.CreateMessage();
            outMsg.Write("FIRE");
            outMsg.Write(msg.SenderConnection.RemoteUniqueIdentifier);
            outMsg.Write(fireX);
            outMsg.Write(fireY);
            outMsg.Write(fireAngle);
            outMsg.Write(fireSpeed);
            server.SendToAll(outMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
        }

        static void HandleNAME(NetIncomingMessage msg)
        {
            string newName = msg.ReadString();
            string oldName = GetPlayerFromUID(msg.SenderConnection.RemoteUniqueIdentifier).Name;
            Out(string.Format("NAME: {0} changed {1}", oldName, newName));

            //save name in dict
            GetPlayerFromUID(msg.SenderConnection.RemoteUniqueIdentifier).Name = newName;

            //inform ALL clients about his name change
            NetOutgoingMessage outMsg = server.CreateMessage();
            outMsg.Write("NAME");
            outMsg.Write(msg.SenderConnection.RemoteUniqueIdentifier);
            outMsg.Write(newName);
            server.SendToAll(outMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
        }
        #endregion

        #region Console Commands
        private static void SetupReadLine()
        {
            Thread t = new Thread(() => {
                while (true)
                {
                    ConsoleKeyInfo inp = Console.ReadKey(true);
                    //corner cases: \n, \t, \b, \0
                    switch (inp.KeyChar)
                    {
                        case '\n': //command done, do it (linux)
                        case '\r': //(windows)
                            string cmd = commandBuffer;
                            commandBuffer = string.Empty;
                            Console.Write('\n');
                            HandleCommand(cmd);
                            break;
                        case '\t': //do nothing TODO: tab completion
                            break;
                        case '\0': //do nothing, because not a real key
                            break;
                        case '\b': //erase a char
                            if (commandBuffer == string.Empty) //ignore when line already cleared
                                break;
                            commandBuffer = commandBuffer.Substring(0, commandBuffer.Length - 1);
                            RedrawCommandBuffer();
                            break;
                        default:   //add it, because regular char
                            commandBuffer += inp.KeyChar;
                            Console.Write(inp.KeyChar);
                            break;
                    }
                }
            });
            t.Start();
        }

        private static void RedrawCommandBuffer()
        {
            //redraw the command buffer
            var seventyNineSpaces = string.Concat(Enumerable.Repeat(" ", 79));
            Console.Write("\r");
            Console.Write(seventyNineSpaces);
            Console.Write("\r");
            Console.Write(commandBuffer);
        }

        private static void HandleCommand(string thisCmd)
        {
            if (thisCmd == "test")
            {
                Out("Your garbage is\nworking perfectly.");
            }
            else if (thisCmd == "list")
            {
                Out(string.Format("Players: {0}", server.Connections.Count));
                foreach (var ply in server.Connections)
                {
                    long hisUID = ply.RemoteUniqueIdentifier;
                    string hisName = GetPlayerFromUID(hisUID).Name;
                    Out(string.Format("Player {0} {1}", hisUID, hisName));
                }
            }
            else if (thisCmd.StartsWith("say "))
            {
                string message = thisCmd.Substring("say ".Length);
                Out(string.Format("Console: {0}", message));

                NetOutgoingMessage sayMsg = server.CreateMessage();
                sayMsg.Write("CHAT");
                sayMsg.Write((long)0);
                sayMsg.Write(message);
                server.SendToAll(sayMsg, NetDeliveryMethod.ReliableOrdered);
            }
            else if (thisCmd == "clearmap")
            {
                //for every tile
                for (int y = 0; y < MapHeight; y++)
                {
                    for (int x = 0; x < MapWidth; x++)
                    {
                        //if it's not grass tile
                        if (dMap[x, y].GetType() != typeof(GrassTile))
                        {
                            //set for myself
                            dMap[x, y] = Tile.ConstructFromID(0);

                            //send message
                            NetOutgoingMessage buildMsg = server.CreateMessage();
                            buildMsg.Write("BUILD");
                            buildMsg.Write((long)0);
                            buildMsg.Write(x);
                            buildMsg.Write(y);
                            buildMsg.Write(0);
                            server.SendToAll(buildMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
                        }
                    }
                }
            }
            else
            {
                Out("unrecognized cmd");
            }
        }

        private static void Out(string what)
        {
            Console.WriteLine("\r{0}", what);
            Console.Write(commandBuffer);
        }
        #endregion
    }
}
