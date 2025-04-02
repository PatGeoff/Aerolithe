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
        private TaskCompletionSource<bool> focusReadyTcs;

        public async Task nikonDoFocus()
        {
            try
            {
                focusReadyTcs = new TaskCompletionSource<bool>();
                await Task.Run(() => NikonAutofocus());
                //MessageBox.Show("image capturée");
            }
            catch (Exception ex)
            {
                throw new Exception("Autofocus failed due to an error: " + ex.Message);
                
            }
           
        }
        public async Task NikonAutofocus()
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
                    AppendTextToConsoleNL("Focus complété");
                    // Signal that the image is ready
                    
                    break;
                }
                catch (NikonException ex)
                {
                    if (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
                        continue;
                    }
                    else
                    {
                        //MessageBox.Show("Impossible de faire le focus");
                        AppendTextToConsoleNL("impossible de faire le focus");
                        //pictureBox_validationE2.Image = Properties.Resources.echec;
                        throw new Exception("Autofocus failed due to an error: " + ex.Message);
                    }
                }            
                
            }
            focusReadyTcs?.TrySetResult(true);
            device.LiveViewEnabled = true;
            await Task.Delay(100);
            liveViewTimer.Start();
            


        }
       
       
    }
}