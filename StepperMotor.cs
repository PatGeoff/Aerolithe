// StepperMotor.cs

// When we start the app, the camera will either be the furthest, triggering the far limit switch,
// or the closest and triggering the near limit switch, or somewhere in between.
// I order to move the camera manually, a full calibration needs to be done. Then the trackbar will be enabled and also scanning will be enabled.
// The calibration will first trigger the near limit swithc to establish a zero position a move to the far limit switch to query the max position.
// If the far limit switch was already triggered, it will go to the near limit switch and calculate the steps required to do so and set a zero. Then it will go to the middle.
// If the near limit switch was already triggered, it will first establish a zero and go to the far limit switch and go to the middle.
// If none of the switches are triggered, it will go to the near limit switch first, then to the far limit switch and then to the middle.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public int stepperMaxPositionValue = 0;
        public int stepperCurrentPosition = 0;
              

    }
}
