using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using Nebulyn.System.Derivatives.Graphics;

namespace Nebulyn.System.Core.Temps
{
    public class CGSCanvas : ICanvas
    {
        public uint Width               { get; }
        public uint Height              { get; }
        public ColorDepth ColorDepth    { get; }

        public Canvas canv;

        public CGSCanvas()
        {
            canv = FullScreenCanvas.GetFullScreenCanvas();
            Width = canv.Mode.Width;
            Height = canv.Mode.Height;
            ColorDepth = canv.Mode.ColorDepth;
        }

        public void DrawImage(Image img, int X, int Y) => canv.DrawImage(img, X, Y);

        public void DrawPoint(Color color, int X, int Y) => canv.DrawPoint(color, X, Y);
        public void DrawRectangle(Color color, int X, int Y, int W, int H) => canv.DrawRectangle(color, X, Y, W, H);
        public void DrawFilledRectangle(Color color, int X, int Y, int W, int H) => canv.DrawFilledRectangle(color, X, Y, W, H);
        public void DrawString(string Text, PCScreenFont Font, Color color, int X, int Y) => canv.DrawString(Text, Font, color, X, Y);
        public void Display() => canv.Display();
    }
}
