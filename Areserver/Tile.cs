using System;
using System.Reflection;

namespace Areserver
{
    public class Tile
    {
        public int TileID
        {
            get
            {
                Attribute attr = Attribute.GetCustomAttribute(this.GetType(), typeof(TileMetaAttribute));
                int id = ((TileMetaAttribute)attr).Id;
                return id;
            }
        }

        /// <summary>
        /// Makes a new tile whose type is specified by id.
        /// </summary>
        /// <returns>A new subclass of Tile with default params.</returns>
        /// <param name="id">Integer identifier associated with the block type.</param>
        public static Tile ConstructFromID(int id)
        {
            Assembly thisAsm = Assembly.GetExecutingAssembly();
            foreach (Type t in thisAsm.GetTypes())
            {
                //get this type's attributes
                object[] attrs = t.GetCustomAttributes(typeof(TileMetaAttribute), false);
                foreach (var attr in attrs)
                {
                    if (attr.GetType() == typeof(TileMetaAttribute))
                    {
                        int thisTypesId = ((TileMetaAttribute)attr).Id;
                        if (thisTypesId == id)
                        {
                            //we've found the blocktype, now construct
                            ConstructorInfo ctor = t.GetConstructor(new Type[] { });
                            object newObj = ctor.Invoke(new object[] { });
                            return (Tile)newObj;
                        }
                    }
                }
            }
            return null;
        }
    }

    [TileMetaAttribute(Id: 0)]
    class WoodTile : Tile
    {
    }
}
