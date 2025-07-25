using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cosmos.System.Graphics;
using Nebulyn.System.Derivatives.Graphics;
using StarDustCosmos;

namespace Nebulyn
{
    public static class Globals
    {
        public static ICanvas Canvas        { get; set; }
        public static Terminal Terminal     { get; set; }
        public static long Ticks            { get; set; }
    }
}
