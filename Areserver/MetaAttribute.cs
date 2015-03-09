using System;

namespace Areserver
{
    public abstract class MetaAttribute : System.Attribute
    {
        public int Id { get; private set; }

        public MetaAttribute(int Id)
        {
            this.Id = Id;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TileMetaAttribute : MetaAttribute
    {
        public TileMetaAttribute(int Id)
            : base(Id)
        {
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class WallMetaAttribute : MetaAttribute
    {
        public WallMetaAttribute(int Id)
            : base(Id)
        {
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class CommandAttribute : System.Attribute
    {
        public string Name { get; private set; }
        public string Help { get; private set; }

        public CommandAttribute(string name, string help)
        {
            this.Name = name;
            this.Help = help;
        }
    }
}

