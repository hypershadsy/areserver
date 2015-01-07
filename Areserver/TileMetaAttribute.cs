using System;

namespace Areserver
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TileMetaAttribute : System.Attribute
    {
        public int Id { get; private set; }

        public TileMetaAttribute(int Id)
        {
            this.Id = Id;
        }
    }
}

