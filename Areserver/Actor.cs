using System;
using Lidgren.Network;

namespace Areserver
{
    public class Actor
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int Z { get; set; }

        public string Name { get; set; }

        public int Life { get; set; }

        public Actor()
        {
            X = int.MinValue;
            Y = int.MinValue;
            Z = int.MinValue;
            Name = "Cactus Fantastico";
            Life = 100;
        }
    }

    public class Player : Actor
    {
        public long UID { get; set; }
        public NetConnection Connection { get; set; }

        public Player(NetConnection connection)
            : base()
        {
            UID = -1;
            this.Connection = connection;
        }

        public void KickFromServer(string reason)
        {
            Connection.Disconnect(reason);
        }
    }
}

