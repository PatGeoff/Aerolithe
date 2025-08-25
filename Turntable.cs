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
    }
}
