using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timer = System.Windows.Forms.Timer;
using Nikon;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV;

using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using System.IO;
using Emgu.CV.Reg;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private NikonManager manager;
        private NikonDevice device;
        private NikonImage nkImage;
        private NikonRange driveStep;
        private Timer liveViewTimer;
        private Image capturedImage;
        private NikonPreview preview;
        private NikonLiveViewImage imageView = null;
        private Mat foreground, background, substractionResult, mask = null;

        public void CamSetup()
        {            
            // Initialize live view timer
            liveViewTimer = new Timer();
            liveViewTimer.Tick += new EventHandler(liveViewTimer_Tick);
            liveViewTimer.Interval = 1000 / 30;

            // Initialize Nikon manager
            manager = new NikonManager("Type0022.md3");
            manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);
            manager.DeviceRemoved += new DeviceRemovedDelegate(manager_DeviceRemoved);
        }

        void manager_DeviceAdded(NikonManager? sender, NikonDevice device)
        {
            this.device = device;
            // Set the device name
            //label_name.Text = device.Name;

            // Enable buttons
            //ToggleButtons(true);
            device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_SaveMedia, (uint)eNkMAIDSaveMedia.kNkMAIDSaveMedia_SDRAM);
            // Hook up device capture events
            device.ImageReady += new ImageReadyDelegate(device_ImageReady);
            //device.ThumbnailReady += new ThumbnailReadyDelegate(device_ThumbnailReady);
            //device.CaptureComplete += new CaptureCompleteDelegate(device_CaptureComplete);
            //device.PreviewReady += new PreviewReadyDelegate(device_PreviewReady);

            deviceLoaded();
        }

        void deviceLoaded()
        {
            
            if (device.LiveViewEnabled == false)
            {
                device.LiveViewEnabled = true;
                liveViewTimer.Start();
                
            }
            else
            {
                liveViewTimer.Start();
            }

           
            //SetCrosshair();
            //GetFocusRange();
            //textBox_Error.Text = "À venir";
            //GetFocusMode();
            //GetAperture();
            //GetShutterSpeed();
            //GetImageType();
            //GetExposureStatus();
            //GetImageSize();
            //GetIso();
            //GetWB();
            //SetFrameDim();
            //InitializeCrosshairPosition();
            //device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_SaveMedia, (uint)eNkMAIDSaveMedia.kNkMAIDSaveMedia_SDRAM);
        }

        void manager_DeviceRemoved(NikonManager sender, NikonDevice device)
        {
            this.device = null;

            // Stop live view timer
            liveViewTimer.Stop();

            // Clear device name
            //label_name.Text = "No Camera";

            // Disable buttons
            //ToggleButtons(false);

            // Clear live view picture
            picBox_LiveView_Main.Image = null;
        }

        void liveViewTimer_Tick(object? sender, EventArgs e)
        {
            // Get live view image

            try
            {
                imageView = device.GetLiveViewImage();

            }
            catch (NikonException)
            {
                liveViewTimer.Stop();

            }

            if (device.LiveViewEnabled)
            {
                background = new Mat();
                // Convertit le LiveCapture en stream
                MemoryStream stream = new MemoryStream(imageView.JpegBuffer);
                // Convertit le stream en byte array
                byte[] imageBytes = stream.ToArray();
                // Convertit le byte array en Mat
                CvInvoke.Imdecode(imageBytes, ImreadModes.Color, background);

                picBox_LiveView_Main.Image = background.ToImage<Bgr, Byte>().ToBitmap();
                if (background == null) MessageBox.Show("calisse");
                //backgroundSubstraction(stream);
                //calculerFlou();
            }
        }

       
    }

}