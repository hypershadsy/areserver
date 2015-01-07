using System;
using System.Collections.Generic;
using Lidgren.Network;
using System.Threading;
using System.Linq;

namespace Areserver
{
    public class Server //TODO string.format where needed
    {
        const int MapWidth = 20;
        const int MapHeight = 20;

        private static string commandBuffer = string.Empty;
        private static NetServer server;
        private static Dictionary<long, string> dNames;
        private static Dictionary<long, bool> dLife;
        private static Tile[,] dMap;

        public static void Main(string[] args)
        {
            Out("Welcome to the Areserver");
            SetupReadLine();
            InitLocalData();
            SetupServer();

            while (true)
            {
                HandleMessages();
                System.Threading.Thread.Sleep(8);
            }
        }

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
                    bool hasName = dNames.ContainsKey(hisUID);
                    string hisName = hasName ? dNames[hisUID] : "<noname>";
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
            else
            {
                Out("unrecognized cmd");
            }
        }

        private static void InitLocalData()
        {
            dNames = new Dictionary<long, string>();
            dLife = new Dictionary<long, bool>();
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

        private static void HandleMessages()
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

            //tell him about everyone else
            foreach (var conn in server.Connections)
            {
                if (conn == msg.SenderConnection) //don't need to know about myself
                    continue;
                NetOutgoingMessage outMsgPlayerList = server.CreateMessage();
                outMsgPlayerList.Write("JOIN");
                outMsgPlayerList.Write(conn.RemoteUniqueIdentifier);
                server.SendMessage(outMsgPlayerList, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
            }

            //send lots of names
            foreach (var kv in dNames)
            {
                NetOutgoingMessage outMsgOtherName = server.CreateMessage();
                outMsgOtherName.Write("NAME");
                outMsgOtherName.Write(kv.Key); //long uid
                outMsgOtherName.Write(kv.Value); //string name
                server.SendMessage(outMsgOtherName, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
            }

            //send lots of lifes
            foreach (var kv in dLife)
            {
                NetOutgoingMessage outMsgOtherLife = server.CreateMessage();
                outMsgOtherLife.Write("LIFE");
                outMsgOtherLife.Write(kv.Key);
                outMsgOtherLife.Write(kv.Value);
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
            dNames.Remove(msg.SenderConnection.RemoteUniqueIdentifier);
            dLife.Remove(msg.SenderConnection.RemoteUniqueIdentifier);
        }

        private static void HandleGameMessage(NetIncomingMessage msg)
        {
            string type = msg.ReadString();
            long senderUid = msg.SenderConnection.RemoteUniqueIdentifier;

            if (type == "POS")
            {
                float newX = msg.ReadFloat();
                float newY = msg.ReadFloat();

                NetOutgoingMessage outMsg = server.CreateMessage();
                outMsg.Write("POS");
                outMsg.Write(senderUid);
                outMsg.Write(newX);
                outMsg.Write(newY);

                server.SendToAll(outMsg, msg.SenderConnection, NetDeliveryMethod.Unreliable, 0);
            }
            else if (type == "LIFE")
            { //TODO: NOT BOOL!!
                bool status = msg.ReadBoolean();
                Out(string.Format("LIFE: {0}: {1}", senderUid, status));

                //save value
                dLife[senderUid] = status;

                //inform everyone else about his pining for the fjords
                NetOutgoingMessage outMsgLife = server.CreateMessage();
                outMsgLife.Write("LIFE");
                outMsgLife.Write(senderUid);
                outMsgLife.Write(status);
                server.SendToAll(outMsgLife, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
            }
            else if (type == "CHAT")
            {
                string message = msg.ReadString();

                if (message.StartsWith("/setname "))
                {
                    string newName = message.Substring("/setname ".Length);
                    Out(string.Format("NAME: {0}", newName));

                    //save name in dict
                    dNames[senderUid] = newName;

                    //inform everyone else about his name changes
                    NetOutgoingMessage outMsg = server.CreateMessage();
                    outMsg.Write("NAME");
                    outMsg.Write(senderUid);
                    outMsg.Write(newName);
                    server.SendToAll(outMsg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0);
                }
                else
                {
                    Out(string.Format("CHAT: {0}: {1}", senderUid, message));

                    //send the chat to ALL clients
                    NetOutgoingMessage outMsg = server.CreateMessage();
                    outMsg.Write("CHAT");
                    outMsg.Write(senderUid);
                    outMsg.Write(message);

                    server.SendToAll(outMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
                }
            }
            else if (type == "BUILD")
            {
                int buildX = msg.ReadInt32();
                int buildY = msg.ReadInt32();
                int buildType = msg.ReadInt32();
                Out(string.Format("BUILD: {0}: at ({1},{2}) {3}", senderUid, buildX, buildY, buildType));

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
                outMsg.Write(senderUid);
                outMsg.Write(buildX);
                outMsg.Write(buildY);
                outMsg.Write(buildType);
                server.SendToAll(outMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
            }
            else if (type == "FIRE")
            {
                float fireX = msg.ReadFloat();
                float fireY = msg.ReadFloat();
                float fireAngle = msg.ReadFloat();
                float fireSpeed = msg.ReadFloat();
                Out(string.Format("FIRE: {0}: at ({1},{2}) ang={3} speed={4}",
                                  senderUid, fireX, fireY, fireAngle, fireSpeed));

                //TODO: save?

                //send the fire to ALL clients
                NetOutgoingMessage outMsg = server.CreateMessage();
                outMsg.Write("FIRE");
                outMsg.Write(senderUid);
                outMsg.Write(fireX);
                outMsg.Write(fireY);
                outMsg.Write(fireAngle);
                outMsg.Write(fireSpeed);
                server.SendToAll(outMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        private static void Out(string what)
        {
            Console.WriteLine("\r{0}", what);
            Console.Write(commandBuffer);
        }
    }
}
