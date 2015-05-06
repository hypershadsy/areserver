using System;
using System.Reflection;

namespace Areserver
{
    public class Wall
    {
        public bool LeftFacing { get; set; }

        public int WallID
        {
            get
            {
                Attribute attr = Attribute.GetCustomAttribute(this.GetType(), typeof(WallMetaAttribute));
                int id = ((WallMetaAttribute)attr).Id;
                return id;
            }
        }

        /// <summary>
        /// Makes a new wall whose type is specified by id.
        /// </summary>
        /// <returns>A new subclass of Wall with default params.</returns>
        /// <param name="id">Integer identifier associated with the block type.</param>
        public static Wall ConstructFromID(int id, bool leftFacing)
        {
            Assembly thisAsm = Assembly.GetExecutingAssembly();
            foreach (Type t in thisAsm.GetTypes())
            {
                //get this type's attributes
                object[] attrs = t.GetCustomAttributes(typeof(WallMetaAttribute), false);
                foreach (var attr in attrs)
                {
                    if (attr.GetType() == typeof(WallMetaAttribute))
                    {
                        int thisTypesId = ((WallMetaAttribute)attr).Id;
                        if (thisTypesId == id)
                        {
                            //we've found the blocktype, now construct
                            ConstructorInfo ctor = t.GetConstructor(new Type[] { });
                            object newObj = ctor.Invoke(new object[] { leftFacing });
                            return (Wall)newObj;
                        }
                    }
                }
            }
            return null;
        }

        public Wall(bool leftFacing)
        {
            this.LeftFacing = leftFacing;
        }
    }

    [WallMetaAttribute(0)]
    class RedBrickWall : Wall
    {
        public RedBrickWall(bool leftFacing)
            : base(leftFacing)
        {
        }
    }

    class Door : Wall
    {
        public Door(bool leftFacing)
            : base(leftFacing)
        {
        }
    }

    [WallMetaAttribute(1)]
    class WoodDoor : Door
    {
        public WoodDoor(bool leftFacing)
            : base(leftFacing)
        {
        }
    }
}