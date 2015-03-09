using System;
using System.Collections.Generic;
using Lidgren.Network;
using System.Threading;
using System.Linq;
using System.Reflection;

namespace Areserver
{
    public class Server
    {
        public static string commandBuffer = string.Empty;
        public static NetServer server;

        public const int MapWidth = 20;
        public const int MapHeight = 20;
        public static List<Actor> dActors;
        public static Tile[,] dMap;

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
            Player thisPlayer = new Player(msg.SenderConnection);
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
                case "NAME":
                    HandleNAME(msg);
                    break;
            }
        }

        public static Player GetPlayerFromUID(long uid)
        {
            foreach (var actor in dActors)
            {
                if (actor.GetType() != typeof(Player))
                    throw new Exception("FIXME found an actor that's not a player");
                Player plr = (Player)actor;
                if (plr.UID == uid)
                {
                    return plr;
                }
            }
            return null;
        }

        #region HandleX
        static void HandlePOS(NetIncomingMessage msg)
        {
            int newX = msg.ReadInt32();
            int newY = msg.ReadInt32();

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
                    var inpC = (char)Console.Read();

                    //corner cases: \n, \r, \t, \0, \b
                    switch (inpC)
                    {
                        case '\n': //command done, do it (enter key linux)
                        case '\r': //(enter key windows)
                            string cmd = commandBuffer;
                            commandBuffer = string.Empty;
                            HandleCommand(cmd);
                            break;
                        case '\t': //do nothing TODO: tab completion
                            break;
                        case '\0': //erase a char (backspace monodevelop, gnome-terminal)
                        case '\b': //(no known platforms use this)
                            //cygwin sshd, mintty, win32 all line-buffer, so BS is not ever encountered
                            if (commandBuffer == string.Empty) //ignore when line already cleared
                                break;
                            commandBuffer = commandBuffer.Substring(0, commandBuffer.Length - 1);
                            RedrawCommandBuffer();
                            break;
                        default:   //add it, because regular char
                            commandBuffer += inpC;
                            break;
                    }
                }
            });
            t.Start();
        }

        private static void RedrawCommandBuffer()
        {
            //redraw the command buffer
            var seventyNineSpaces = string.Concat(Enumerable.Repeat(" ", Console.BufferWidth - 1));
            Console.Write("\r");
            Console.Write(seventyNineSpaces);
            Console.Write("\r");
            Console.Write(commandBuffer);
        }

        private static void HandleCommand(string thisCmd)
        {
            string cmdName;
            string[] cmdArgs;
            string[] cmdArgsAll = thisCmd.Split(' ');
            cmdArgs = new string[cmdArgsAll.Length - 1];
            Array.Copy(cmdArgsAll, 1, cmdArgs, 0, cmdArgs.Length);
            cmdName = cmdArgsAll[0];

            if (!ExecCommand(cmdName, cmdArgs))
            {
                Out("unrecognized cmd");
            }
        }

        private static bool ExecCommand(string cmdName, string[] cmdArgs)
        {
            MethodInfo[] props = typeof(Command).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
            bool found = false;
            foreach (MethodInfo prop in props)
            {
                foreach (object attr in prop.GetCustomAttributes(true))
                {
                    CommandAttribute cmdAttr = attr as CommandAttribute;
                    if (cmdAttr != null && cmdAttr.Name == cmdName)
                    {
                        found = true;
                        prop.Invoke(null, new object[] {
                            cmdArgs
                        });
                    }
                }
            }

            return found;
        }

        public static void Out(string what)
        {
            Console.WriteLine("\r{0}", what);
            Console.Write(commandBuffer);
        }
        #endregion
    }
}
