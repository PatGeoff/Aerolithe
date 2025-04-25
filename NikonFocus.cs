using Nikon;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
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
                if (!device.LiveViewEnabled)
                {
                    device.LiveViewEnabled = true;
                    await Task.Delay(100);
                    liveViewTimer.Start();
                }
                throw new Exception("Autofocus failed due to an error: " + ex.Message);                
            }
           
        }

       

        public async Task NikonAutofocus()
        {
            // AppendTextToConsoleNL("Live view status before autofocus: " + device.LiveViewEnabled);
            if (device.LiveViewEnabled)
            {
                device.LiveViewEnabled = false;
                await Task.Delay(100);
                // liveViewTimer.Stop();
            }

            bool focusCompleted = false;
            while (!focusCompleted)
            {
                try
                {
                    device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus);
                    focusCompleted = true; // Set focusCompleted to true only if no exception occurs
                }
                catch (NikonException ex)
                {
                    // AppendTextToConsoleNL(ex.Message);
                    if (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
                        await Task.Delay(100); // Wait before retrying
                        continue; // Retry autofocus
                    }
                    else
                    {
                         AppendTextToConsoleNL("Impossible de faire le focus");
                        if (!device.LiveViewEnabled)
                        {
                            device.LiveViewEnabled = true;
                            await Task.Delay(100);
                            liveViewTimer.Start();
                        }
                        throw new Exception("Autofocus failed due to an error: " + ex.Message);
                        
                    }
                }
            }

            // AppendTextToConsoleNL("Focus complété");
            // AppendTextToConsoleNL("Live view status after autofocus: " + liveViewStatus);
            if (!device.LiveViewEnabled)
            {
                device.LiveViewEnabled = true;
                await Task.Delay(100);
                liveViewTimer.Start();
            }
            // AppendTextToConsoleNL("Live view status after autofocus: " + liveViewStatus);
        }

    }
}