using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    internal class CustomFlowLayoutPanel : FlowLayoutPanel
    {
        private bool isDragging = false;
        private Point startPoint;
        private int scrollStart;

        public CustomFlowLayoutPanel()
        {
            this.MouseDown += CustomFlowLayoutPanel_MouseDown;
            this.MouseMove += CustomFlowLayoutPanel_MouseMove;
            this.MouseUp += CustomFlowLayoutPanel_MouseUp;
        }


        private void CustomFlowLayoutPanel_MouseDown(object sender, MouseEventArgs e)
        {
            isDragging = true;
            startPoint = e.Location;
            scrollStart = this.DisplayRectangle.X;
            Console.WriteLine($"MouseDown: startPoint={startPoint}, scrollStart={scrollStart}");
        }

        private void CustomFlowLayoutPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                int delta = startPoint.X - e.X;
                int newScrollPosition = scrollStart - delta;
                this.SetDisplayRectLocation(newScrollPosition, this.DisplayRectangle.Y);
                this.Invalidate(); // Refresh the panel to update the display
                Console.WriteLine($"MouseMove: delta={delta}, newScrollPosition={newScrollPosition}");
            }
        }

        private void CustomFlowLayoutPanel_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            Console.WriteLine("MouseUp: Dragging stopped");
        }

    }
}


