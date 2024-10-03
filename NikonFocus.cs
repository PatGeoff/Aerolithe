using Nikon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public void NikonAutofocus(int index)
        {
            if (device.LiveViewEnabled)
            {
                device.LiveViewEnabled = false;
                liveViewTimer.Stop();
            }

            while (true)
            {
                try
                {
                    
                    device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus);
                    pictureBox_validationE2.Image = Properties.Resources.crochet;
                    ApplyButtonStyle(buttonLabelPairs[1], false);
                    ApplyButtonStyle(buttonLabelPairs[2], true);
                }
                catch (NikonException ex)
                {
                    if (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
                        continue;
                    }
                    else
                    {
                        MessageBox.Show("Impossible de faire le focus");
                        pictureBox_validationE2.Image = Properties.Resources.echec;
                        break;
                    }
                }

                break;
            }
            device.LiveViewEnabled = true;
            liveViewTimer.Start();
            liveViewState = true;

        }



    }
}