using System;

namespace Areserver
{
    public class Wall
    {
        public bool LeftFacing { get; set; }

        public Wall(bool leftFacing)
        {
            this.LeftFacing = leftFacing;
        }
    }

    class RedBrickWall : Wall
    {
        public RedBrickWall(bool leftFacing)
            : base(leftFacing)
        {
        }
    }
}