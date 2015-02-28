using System;
using Lidgren.Network;

namespace Areserver
{
    public static class Command
    {
        [CommandAttribute("test", "Test")]
        public static void Test(string[] args)
        {
            Server.Out("Your garbage is\nworking perfectly.");
        }

        [CommandAttribute("list", "List all players")]
        public static void List(string[] args)
        {
            Server.Out(string.Format("Players: {0}", Server.server.Connections.Count));
            foreach (var ply in Server.server.Connections)
            {
                long hisUID = ply.RemoteUniqueIdentifier;
                string hisName = Server.GetPlayerFromUID(hisUID).Name;
                Server.Out(string.Format("Player {0} {1}", hisUID, hisName));
            }
        }

        [CommandAttribute("say", "Address the peasants")]
        public static void Say(string[] args)
        {
            string message = string.Join(" ", args);
            Server.Out(string.Format("Console: {0}", message));

            NetOutgoingMessage sayMsg = Server.server.CreateMessage();
            sayMsg.Write("CHAT");
            sayMsg.Write((long)0);
            sayMsg.Write(message);
            Server.server.SendToAll(sayMsg, NetDeliveryMethod.ReliableOrdered);
        }

        [CommandAttribute("clearmap", "Clear the map")]
        public static void ClearMap(string[] args)
        {
            //for every tile
            for (int y = 0; y < Server.MapHeight; y++)
            {
                for (int x = 0; x < Server.MapWidth; x++)
                {
                    //if it's not grass tile
                    if (Server.dMap[x, y].GetType() != typeof(GrassTile))
                    {
                        //set for myself
                        Server.dMap[x, y] = Tile.ConstructFromID(0);

                        //send message
                        NetOutgoingMessage buildMsg = Server.server.CreateMessage();
                        buildMsg.Write("BUILD");
                        buildMsg.Write((long)0);
                        buildMsg.Write(x);
                        buildMsg.Write(y);
                        buildMsg.Write(0);
                        Server.server.SendToAll(buildMsg, null, NetDeliveryMethod.ReliableOrdered, 0);
                    }
                }
            }
        }

        [CommandAttribute("exit", "Close the server in a panic, no cleanup")]
        public static void Exit(string[] args)
        {
            Environment.Exit(0);
        }
    }
}

