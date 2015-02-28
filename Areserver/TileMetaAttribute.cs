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

