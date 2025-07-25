using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;

namespace Nebulyn.System.Derivatives.Graphics
{
    public interface ICanvas
    {
        public uint Width               { get; }
        public uint Height              { get; }
        public ColorDepth ColorDepth    { get; }

        public void DrawImage(Image img,int X,int Y);
        public void DrawPoint(Color color,int X, int Y);
        public void DrawRectangle(Color color, int X, int Y, int W, int H);
        public void DrawFilledRectangle(Color color, int X, int Y, int W, int H);
        public void DrawString(string Text,PCScreenFont Font, Color color, int X, int Y);
        public void Display();
    }
}
