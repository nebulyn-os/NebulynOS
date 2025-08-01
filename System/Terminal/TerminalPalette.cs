using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Terminal
{
    public struct TerminalPalette
    {
        public Color FGDefault;
        public Color FGBlack;
        public Color FGRed;
        public Color FGGreen;
        public Color FGYellow;
        public Color FGBlue;
        public Color FGMagenta;
        public Color FGCyan;
        public Color FGWhite;

        public Color BGDefault;
        public Color BGBlack;
        public Color BGRed;
        public Color BGGreen;
        public Color BGYellow;
        public Color BGBlue;
        public Color BGMagenta;
        public Color BGCyan;
        public Color BGWhite;

        public static TerminalPalette Default { get; } = new()
        {
            FGDefault = Color.LightGray,
            FGBlack = Color.Black,
            FGRed = Color.Red,
            FGGreen = Color.Green,
            FGYellow = Color.Yellow,
            FGBlue = Color.Blue,
            FGMagenta = Color.Magenta,
            FGCyan = Color.Cyan,
            FGWhite = Color.White,

            BGDefault = Color.Transparent,
            BGBlack = Color.Black,
            BGRed = Color.Red,
            BGGreen = Color.Green,
            BGYellow = Color.Yellow,
            BGBlue = Color.Blue,
            BGMagenta = Color.Magenta,
            BGCyan = Color.Cyan,
            BGWhite = Color.White
        };

        public Color GetFromAnsiNumber(int number)
        {
            return number switch
            {
                30 => FGBlack,
                31 => FGRed,
                32 => FGGreen,
                33 => FGYellow,
                34 => FGBlue,
                35 => FGMagenta,
                36 => FGCyan,
                37 => FGWhite,
                39 => FGDefault,

                40 => BGBlack,
                41 => BGRed,
                42 => BGGreen,
                43 => BGYellow,
                44 => BGBlue,
                45 => BGMagenta,
                46 => BGCyan,
                47 => BGWhite,
                49 => BGDefault,
                _ => throw new ArgumentOutOfRangeException(nameof(number), "Invalid ANSI color number")
            };
        }

        public void SetFromAnsiNumber(int number,Color col)
        {
            switch (number)
            {
                case 30:
                    FGBlack = col;
                    break;
                case 31:
                    FGRed = col;
                    break;
                case 32:
                    FGGreen = col;
                    break;
                case 33:
                    FGYellow = col;
                    break;
                case 34:
                    FGBlue = col;
                    break;
                case 35:
                    FGMagenta = col;
                    break;
                case 36:
                    FGCyan = col;
                    break;
                case 37:
                    FGWhite = col;
                    break;
                case 39:
                    FGDefault = col;
                    break;

                case 40:
                    BGBlack = col;
                    break;
                case 41:
                    BGRed = col;
                    break;
                case 42:
                    BGGreen = col;
                    break;
                case 43:
                    BGYellow = col;
                    break;
                case 44:
                    BGBlue = col;
                    break;
                case 45:
                    BGMagenta = col;
                    break;
                case 46:
                    BGCyan = col;
                    break;
                case 47:
                    BGWhite = col;
                    break;
                case 49:
                    BGDefault = col;
                    break;
            }
        }
    }
}
