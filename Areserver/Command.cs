using System;
using Lidgren.Network;
using System.Reflection;

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
            sayMsg.Write(0L);
            sayMsg.Write(message);
            Server.server.SendToAll(sayMsg, NetDeliveryMethod.ReliableOrdered);
        }

        [CommandAttribute("clearmap", "Clear the map")]
        public static void ClearMap(string[] args)
        {
            //reset map
            Server.GenerateMap();

            //send changes to everyone
            NetOutgoingMessage mapState = Server.server.CreateMessage();
            Server.AppendMapSnapshot(mapState);
            Server.server.SendToAll(mapState, NetDeliveryMethod.ReliableOrdered);
        }

        [CommandAttribute("exit", "Close the server in a panic, no cleanup")]
        public static void Exit(string[] args)
        {
            Environment.Exit(0);
        }

        [CommandAttribute("kick", "Get some idiot off your server")]
        public static void Kick(string[] args)
        {
            long uid = long.Parse(args[0]);
            Player plr = Server.GetPlayerFromUID(uid);

            if (plr != null)
            {
                plr.KickFromServer("Kicked");
                Server.Out("Kicked");
            }
            else
            {
                Server.Out("Could not find player with UID");
            }
        }

        [CommandAttribute("help", "List all commands")]
        public static void Help(string[] args)
        {
            MethodInfo[] props = typeof(Command).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);

            if (args.Length == 1) //details mode
            {
                //try to find command with this name, print its help
                bool found = false;
                foreach (MethodInfo prop in props)
                {
                    foreach (object attr in prop.GetCustomAttributes(true))
                    {
                        CommandAttribute cmdAttr = attr as CommandAttribute;
                        if (cmdAttr != null && cmdAttr.Name == args[0])
                        {
                            found = true;
                            Server.Out(string.Format("{0} - {1}", cmdAttr.Name, cmdAttr.Help));
                        }
                    }
                }
                if (!found)
                {
                    Server.Out(string.Format("Command {0} not found.", args[0]));
                }
            }
            else //list mode
            {
                //print each command along with its help
                foreach (MethodInfo prop in props)
                {
                    foreach (object attr in prop.GetCustomAttributes(true))
                    {
                        CommandAttribute cmdAttr = attr as CommandAttribute;
                        if (cmdAttr != null)
                        {
                            Server.Out(string.Format("{0} - {1}", cmdAttr.Name, cmdAttr.Help));
                        }
                    }
                }
            }
        }
    }
}

