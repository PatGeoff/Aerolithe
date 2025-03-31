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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public NikonManager manager;
        public NikonDevice device;
        public NikonImage nkImage;
        public NikonRange driveStep;
        private Timer liveViewTimer;
        public Image capturedImage;
        public NikonPreview preview;
        private NikonLiveViewImage imageView = null;
        private Mat foreground, background, substractionResult, mask = null;
        public double oldFocusValue;

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

            try
            {
                // Set the device name
                //label_name.Text = device.Name;

                // Enable buttons
                //ToggleButtons(true);
                device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_SaveMedia, (uint)eNkMAIDSaveMedia.kNkMAIDSaveMedia_SDRAM);

                // Hook up device capture events
                device.ImageReady += new ImageReadyDelegate(device_ImageReady);
                AppendTextToConsoleNL("device_ImageReady delegate added");
                //device.ThumbnailReady += new ThumbnailReadyDelegate(OnThumbnailReady);
                //device.CaptureComplete += new CaptureCompleteDelegate(device_CaptureComplete);
                //device.PreviewReady += new PreviewReadyDelegate(device_PreviewReady);


                deviceLoaded();
            }
            catch (NikonException ex)
            {
                // Handle Nikon-specific exceptions
                Console.WriteLine("NikonException: " + ex.Message);
                // Display placeholder image
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Console.WriteLine("Exception: " + ex.Message);
                // Display placeholder image
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
            }
        }

        void deviceLoaded()
        {

            if (!device.LiveViewEnabled)
            {
                device.LiveViewEnabled = true;


            }
            liveViewTimer.Start();

            // Get the drivestep range
            driveStep = device.GetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep);
            AppendTextToConsoleNL($"Drive Step Range: Min={driveStep.Min}, Max={driveStep.Max}");
            trkBar_focus.Maximum = (int)driveStep.Max;
            lbl_focusRangeMax.Text = driveStep.Max.ToString();
            //SetCrosshair();
            //GetFocusRange();
            //textBox_Error.Text = "À venir";
            //GetFocusMode();
            //GetAperture();
            
            //GetImageType();
            //GetExposureStatus();
            GetExposureModes();
            GetImageSize();
            GetLiveViewSize();
            GetShutterSpeed();
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

            // Clear live view picture
            picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
        }

        void liveViewTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Attempt to get the live view image
                imageView = device.GetLiveViewImage();

                if (device.LiveViewEnabled && imageView != null && imageView.JpegBuffer.Length > 0)
                {
                    using (Mat background = new Mat())
                    {
                        // Convertit le LiveCapture en stream
                        using (MemoryStream stream = new MemoryStream(imageView.JpegBuffer))
                        {
                            // Convertit le stream en byte array
                            byte[] imageBytes = stream.ToArray();
                            // Convertit le byte array en Mat
                            CvInvoke.Imdecode(imageBytes, ImreadModes.Color, background);
                            picBox_LiveView_Main.Image = background.ToImage<Bgr, Byte>().ToBitmap();
                            backgroundSubstraction(stream);
                            calculerFlou();
                        }
                    }
                }
                else
                {
                    // Display placeholder image if live view is not enabled or image is invalid
                    picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
                }
            }
            catch (NikonException ex)
            {
                // Handle Nikon-specific exceptions
                Console.WriteLine("NikonException: " + ex.Message);
                // Display placeholder image
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Console.WriteLine("Exception: " + ex.Message);
                // Display placeholder image
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
            }
        }



        #region CAMERA INFO
        private void GetImageSize()
        {
            NikonEnum imgSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ImageSize);
            for (int i = 0; i < imgSize.Length; i++)
            {
                comboBox_TaillePhotos.Items.Add(imgSize[i].ToString());
            }
            comboBox_TaillePhotos.SelectedIndex = imgSize.Index;
        }

        private void GetShutterSpeed()
        {
            NikonEnum exposureTime = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed);
            for (int i = 0; i < exposureTime.Length; i++)
            {
                comboBox_shutterTime.Items.Add(exposureTime[i].ToString());
            }
            comboBox_shutterTime.SelectedIndex = exposureTime.Index;
        }

        private void GetLiveViewSize()
        {
            NikonEnum liveviewSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize);
            comboBox_TailleLiveView.Items.Add("640 x 424");
            comboBox_TailleLiveView.Items.Add("1280 x 720");
            comboBox_TailleLiveView.Items.Add("1920 x 1080");
            switch (liveviewSize.ToString())
            {
                case "0":
                    comboBox_TailleLiveView.SelectedIndex = 0;
                    break;
                case "1":
                    comboBox_TailleLiveView.SelectedIndex = 1;
                    break;
                case "2":
                    comboBox_TailleLiveView.SelectedIndex = 2;
                    break;
            }

        }

       private void GetExposureModes()
        {
            NikonEnum modeSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ExposureMode);            
            comboBox_ExpoMode.Items.Add("Programmed Auto (P)");
            comboBox_ExpoMode.Items.Add("Shutter Priority (S)");            
            comboBox_ExpoMode.Items.Add("Aperture Priority (A)");
            comboBox_ExpoMode.Items.Add("Manual (M)");
            comboBox_ExpoMode.SelectedIndex = modeSize.Index;
        }

        #endregion

    }

}