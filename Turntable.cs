using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe: Form 
    {
        private int turntableDelay = 7000; // secondes
        public int turntableSpeed = 350;
        public int turntablePosition = 1000;
        public double actuatorAngle = 0.0;
        public int previousPos = 0;
        public int turntableIncrement = 15;
        public int ttTargetPosition = 0;

        public void AvanceTableTournateDeg()
        {
            int incr = (int)(4096 / turntableIncrement);
            if (trkBar_turntable.Value + incr > trkBar_turntable.Maximum)
            {
                ttTargetPosition = trkBar_turntable.Maximum;
                TurnTableRotation(trkBar_turntable.Maximum);
            }
              
            else
            {
               ttTargetPosition = trkBar_turntable.Value += incr;
               TurnTableRotation(ttTargetPosition);
            }
               

            lbl_turntablePosition.Text = trkBar_turntable.Value.ToString() + " / 4096";
            lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
            turntablePosition = trkBar_turntable.Value;
        }
        public void ReculeTableTournanteDeg()
        {
            int incr = (int)(4096 / turntableIncrement);
            if (trkBar_turntable.Value - incr < trkBar_turntable.Minimum) {
                ttTargetPosition = trkBar_turntable.Minimum;
                TurnTableRotation(ttTargetPosition);
                
            }
            else
            {
                ttTargetPosition = trkBar_turntable.Value -= incr;
                TurnTableRotation(ttTargetPosition);
            }
                
            

            lbl_turntablePosition.Text = trkBar_turntable.Value.ToString() + " / 4096";
            lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
            turntablePosition = trkBar_turntable.Value;
        }
    }

   
}
