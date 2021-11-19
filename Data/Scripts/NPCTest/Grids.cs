using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI;

namespace Stollie.NPC_Test
{
    public struct Grids
    {
        public Grids(IMyCubeGrid grid, IMyCockpit cockpit, int seatCount)
        {
            Grid = grid;
            Cockpit = cockpit;
            SeatCount = seatCount;
        }

        public IMyCubeGrid Grid { get; }
        public IMyCockpit Cockpit { get; }

        public int SeatCount { get; }
    }
}
