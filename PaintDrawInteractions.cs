using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {



        private bool isDrawing = false;
        private int startY;
        private int currentY;
        private CustomPen customPen;
        private CustomBrush customBrush;



        private void DrawBlackBelowLine(int y, Graphics g)
        {
            using (Brush brush = customBrush.GetBrush())
            {
                g.FillRectangle(brush, 0 , y, pnl_DrawingLiveView.Width , pnl_DrawingLiveView.Height - y);
            }
        }



        private void SetupPen()
        {
            customPen = new CustomPen(Color.Red, 2.0f, 128, false); // Example: semi-transparent black pen
            customBrush = new CustomBrush(Color.White, 180, false);
        }



        public class CustomPen
        {
            public Color Color { get; set; }
            public float Width { get; set; }
            public int Transparency { get; set; }
            public bool IsVisible { get; set; }



            public CustomPen(Color color, float width, int transparency, bool isVisible)
            {
                Color = color;
                Width = width;
                Transparency = transparency;
                IsVisible = isVisible;
            }

            public Pen GetPen()
            {
                Color transparentColor = Color.FromArgb(Transparency, Color);
                return new Pen(transparentColor, Width);
            }
        }



        public class CustomBrush
        {
            public Color Color { get; set; }
            public int Transparency { get; set; }
            public bool IsVisible { get; set; }

            public CustomBrush(Color color, int transparency, bool isVisible)
            {
                Color = color;
                Transparency = transparency;
                IsVisible = isVisible;
            }

            public Brush GetBrush()
            {
                Color transparentColor = Color.FromArgb(Transparency, Color);
                return new SolidBrush(transparentColor);
            }
        }



    }
}
