using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Reg;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Nikon;
using SharpOSC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using Timer = System.Windows.Forms.Timer;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public NikonManager manager;
        public NikonDevice device;
        public NikonRange driveStep;
        private Timer liveViewTimer;
        public Image capturedImage;
        public NikonPreview preview;
        private NikonLiveViewImage imageView = null;
        private Mat foreground, background, substractionResult, mask = null;
        public double oldFocusValue;
        public double blurrynessAmount = 0;
        public double blurrynessAmountMask = 0;
        private bool liveViewStatus = false;
        private Image liveViewCompositedImage;
        private readonly object imageLock = new object();
        public Bitmap maskBitmapLive;
        public bool maskFreeze = false;
        


        // Taille du rendu histogramme (pixels)
        private static readonly Size _histRenderSize = new Size(256, 120);

        // Paramètres d'histogramme (256 bins sur [0..256))
        private static readonly int[] _histBins = new[] { 256 };
        private static readonly RangeF[] _histRange = new[] { new RangeF(0, 256) };



        private bool isProcessing = false;
        private byte[] lutData;
        private float lastGammaValue = -1;



        public void CamSetup()
        {
            // Initialize live view timer
            liveViewTimer = new Timer();
            liveViewTimer.Tick += new EventHandler(LiveViewTimer_Tick);
            liveViewTimer.Interval = 1000 / 10;  // 1000 / 30 = 30 images seconde. 1000 /10 = 10 images seconde

            AppendTextToConsoleNL("liveViewTimer initialisé");
            // Initialize Nikon manager

            var exeDir = Path.GetDirectoryName(Environment.ProcessPath)!;
            var md3Path = Path.Combine(AppContext.BaseDirectory, "MyResources", "NikonLibs", "Type0022.md3");
            manager = new NikonManager(md3Path);

            manager.DeviceAdded += new DeviceAddedDelegate(manager_DeviceAdded);
            manager.DeviceRemoved += new DeviceRemovedDelegate(manager_DeviceRemoved);
            AppendTextToConsoleNL("NIkon Type0022.md3 et dll initialisés");

        }

        void manager_DeviceAdded(NikonManager? sender, NikonDevice device)
        {
            this.device = device;

            try
            {
                device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_SaveMedia, (uint)eNkMAIDSaveMedia.kNkMAIDSaveMedia_SDRAM);

                device.ImageReady += new ImageReadyDelegate(device_ImageReady);
                device.CaptureComplete += new CaptureCompleteDelegate(device_CaptureComplete);
                device.Progress += new ProgressDelegate(OnNikonProgress);
                AppendTextToConsoleNL("Nikon delegates initialisés");
                deviceLoaded();
            }
            catch (NikonException ex)
            {
                Console.WriteLine("NikonException: " + ex.Message);
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
            }
        }

        void deviceLoaded()
        {
            AppendTextToConsoleNL("Une Nikon a été trouvée");
            if (!device.LiveViewEnabled)
            {
                device.LiveViewEnabled = true;


            }
            liveViewTimer.Start();

            driveStep = device.GetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep);    
            lbl_driveStepMin.Text = driveStep.Min.ToString();
            lbl_driveStepMax.Text = driveStep.Max.ToString();
            hScrollBar_driveStep.Minimum = (int)driveStep.Min;
            hScrollBar_driveStep.Maximum = (int)driveStep.Max;
            //GetAperture();

            GetImageType();
            //GetExposureStatus();
            //GetExposureModes();

            GetImageSize();

            //GetAfcPriority();
            GetShutterSpeed();
            GetLiveViewSize();
            GetFocusMode();
            //GetAFMode();
            //GetLiveViewAFMode();
            //GetFocusAreaMode();


            //GetIso();
            //GetWB();
            //SetFrameDim();
            //InitializeCrosshairPosition();
            device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_SaveMedia, (uint)eNkMAIDSaveMedia.kNkMAIDSaveMedia_SDRAM);
        }

        void manager_DeviceRemoved(NikonManager sender, NikonDevice device)
        {
            this.device = null;

            // Stop live view timer
            liveViewTimer.Stop();

            // Clear live view picture
            picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
        }


        private void UpdateGammaLUT()
        {
            float gammaValue = trackBar_Gamma.Value / 10.0f;
            if (gammaValue != lastGammaValue)
            {
                lutData = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    double normalized = i / 255.0;
                    double corrected = Math.Pow(normalized, gammaValue);
                    lutData[i] = (byte)(Math.Min(255, corrected * 255.0));
                }
                lastGammaValue = gammaValue;
            }
        }


        async void LiveViewTimer_Tick(object? sender, EventArgs e)
        {

            if (isProcessing) return;
            isProcessing = true;

            try
            {
                if (!chkBox_liveView.Checked) chkBox_liveView.Checked = true;

                imageView = device.GetLiveViewImage();
                if (device.LiveViewEnabled && imageView != null && imageView.JpegBuffer.Length > 0)
                {
                    liveViewStatus = true;

                    UpdateGammaLUT();

                    Mat background = new Mat();

                    using (MemoryStream stream = new MemoryStream(imageView.JpegBuffer))
                    {
                        CvInvoke.Imdecode(imageView.JpegBuffer, ImreadModes.Color, background);
                        //_ = Task.Run(() =>
                        //{
                        //    try
                        //    {
                        //        AfficheHistogramme(background, picBox_Histogramme); // ton PictureBox de luminance
                        //    }
                        //    finally
                        //    {

                        //    }                           
                        //});

                        float gammaValue = trackBar_Gamma.Value / 10.0f;


                        using (Mat lut = new Mat(1, 256, DepthType.Cv8U, 1))
                        {
                            System.Runtime.InteropServices.Marshal.Copy(lutData, 0, lut.DataPointer, lutData.Length);
                            CvInvoke.LUT(background, lut, background);
                        }

                        // Calcule et affichage du masque
                        if (!maskFreeze) maskBitmapLive = await BrightnessMaskFromBytes(imageView.JpegBuffer, hScrollBar_liveMaskThresh.Value, false);
                        else if (maskBitmapLive == null) maskBitmapLive = await BrightnessMaskFromBytes(imageView.JpegBuffer, hScrollBar_liveMaskThresh.Value, false);

                        picBox_liveMaskLum.Image = maskBitmapLive;


                        var localBitmap = (Bitmap)maskBitmapLive.Clone();

                        // Centrage du masque
                        _ = Task.Run(async () =>
                        {
                            await CalculeDuCentrageAsync(localBitmap, 1);
                            localBitmap.Dispose();
                            lbl_Centrage.Invoke(new Action(() =>
                            {
                                lbl_Centrage.Text = $"Offset X={offsets.offsetX:F2}, Y={offsets.offsetY:F2}, Dépasse: {offsets.hasBlackOnBorder}";
                            }));
                        });

                      

                        using (var sourceImage = background.ToImage<Bgr, byte>())
                        using (var maskGray = maskBitmapLive.ToImage<Gray, byte>())
                        using (var resizedMask = maskGray.Resize(sourceImage.Width, sourceImage.Height, Emgu.CV.CvEnum.Inter.Linear))
                        {


                            using (var invertedMask = resizedMask.Not())
                            using (var maskBgr = invertedMask.Convert<Bgr, byte>())
                            {
                               //sourceImage._And(maskBgr);
                               //picBox_MLMask.Image = sourceImage.ToBitmap();
                            }

                            // ✅ Calcul de la carte de netteté locale
                            Mat grayImage = background.ToImage<Gray, byte>().Mat;

                            int blockSize = trackBar_blobCount.Value * 16;

                            double[,] sharpnessGrid = ComputeSharpnessGrid(grayImage, blockSize);
                            //double[,] sharpnessGrid = ComputeSharpnessGridMasked(grayImage, resizedMask.Mat, blockSize, 0.5);

                            // Tu peux stocker cette carte dans une variable globale ou l'utiliser dans AutomaticFocusMapping()
                            currentLiveViewFocusMap = new FocusMap
                            {
                                FocusPosition = focusStackStepVar, // <-----  @(*#%&(@&$(&$  ----   ICI 
                                SharpnessGrid = sharpnessGrid
                            };

                            // Création de l'image avec les blocs flous
                            Image<Bgr, byte> overlayImage = background.ToImage<Bgr, byte>();


                            for (int y = 0; y < sharpnessGrid.GetLength(0); y++)
                            {
                                for (int x = 0; x < sharpnessGrid.GetLength(1); x++)
                                {
                                    double sharpness = sharpnessGrid[y, x];
                                    blurThreshold = (double)trackBar_blurThreshold.Value;
                                    if (sharpness >= blurThreshold)
                                    {
                                        Rectangle rect = new Rectangle(x * blockSize, y * blockSize, blockSize, blockSize);
                                        overlayImage.Draw(rect, new Bgr(Color.LimeGreen), 1); // contour verte pour les zones nettes
                                    }

                                }
                            }

                            blurredBlocks = sharpnessGrid.Cast<double>().Count(v => v >= blurThreshold);
                            lbl_blobCount.Text = blurredBlocks.ToString();


                            // Affichage selon l'état de la checkbox
                            if (projet.ViewSharpnessOverlay)
                            {
                                picBox_LiveView_Main.Image = overlayImage.ToBitmap();
                            }
                            else
                            {
                                picBox_LiveView_Main.Image = background.ToImage<Bgr, Byte>().ToBitmap();
                            }
                        }
                        lbl_LiveViewStreamSize.Text = $"LiveView Width: {background.Width} Height: {background.Height};";

                        Mat histSource = background.Clone();

                      


                        background.Dispose();
                    }

                }
                else
                {
                    liveViewStatus = false;
                    picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
                }
            }
            catch (NikonException ex)
            {
                Console.WriteLine("NikonException: " + ex.Message);
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
            }
            finally
            {
                isProcessing = false;
            }
        }



        private void ShowValueHistogramHSV(Mat bgrSource, PictureBox targetLum)
        {
            try
            {
                using (var small = new Mat())
                {
                    double scale = 320.0 / bgrSource.Width;
                    int newW = 320;
                    int newH = Math.Max(1, (int)Math.Round(bgrSource.Height * scale));
                    CvInvoke.Resize(bgrSource, small, new Size(newW, newH), 0, 0, Inter.Linear);

                   // CvInvoke.CalcHist(bgrSource, new )
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HSV value histogram error: {ex.Message}");
            }
        }



        private void device_CaptureComplete(NikonDevice device, int data)
        {
            //AppendTextToConsoleNL("Capture Complétée");
        }
        private void OnNikonProgress(NikonDevice sender, eNkMAIDDataObjType type, int done, int total)
        {
            int percent = (int)((done / (float)total) * 100);

            if (progressBar_ImageSave.InvokeRequired)
            {
                progressBar_ImageSave.Invoke(new Action(() =>
                {
                    progressBar_ImageSave.Value = Math.Min(percent, 100);

                    // Remise à zéro une fois le transfert terminé
                    if (done >= total)
                    {
                        progressBar_ImageSave.Value = 0;
                    }
                }));
            }
            else
            {
                progressBar_ImageSave.Value = Math.Min(percent, 100);

                if (done >= total)
                {
                    progressBar_ImageSave.Value = 0;
                }
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

        private void GetImageType()
        {
            NikonEnum imgType = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_CompressionLevel);
            for (int i = 0; i < imgType.Length; i++)
            {
                comboBox_ImageType.Items.Add(imgType[i].ToString());
            }
            comboBox_ImageType.SelectedIndex = imgType.Index;
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
            comboBox_TailleLiveView.SelectedIndex = liveviewSize.Index;
            if (liveviewSize != null)
            {
                liveviewSize.Index = 2;
                device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize, liveviewSize);
                Task.Delay(100);
                liveviewSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize);
                comboBox_TailleLiveView.SelectedIndex = liveviewSize.Index;
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

        private void GetAfcPriority()
        {
            NikonEnum focusModes = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_AFcPriority);
            for (int i = 0; i < focusModes.Length; i++)
            {
                comboBox_AfcPriority.Items.Add(focusModes[i].ToString());
            }
            comboBox_AfcPriority.SelectedIndex = focusModes.Index;
        }

        private void GetFocusAreaMode()
        {
            NikonEnum focusAreaModes = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_FocusAreaMode);
            for (int i = 0; i < focusAreaModes.Length; i++)
            {
                comboBox_FocusAeraMode.Items.Add(focusAreaModes[i].ToString());
            }
            comboBox_FocusAeraMode.SelectedIndex = focusAreaModes.Index;
        }

        private void GetFocusMode()
        {

            var focusMode = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_FocusMode);

            switch (focusMode)
            {
                case 0:
                    lbl_AFMode.Text = "MF";
                    break;
                case 1:
                    lbl_AFMode.Text = "AF-S";
                    break;
                case 2:
                    lbl_AFMode.Text = "AF-C";
                    break;
                case 3:
                    lbl_AFMode.Text = "AF-F";
                    break;

                default:
                    break;
            }
        }

        private void GetAFMode()
        {
            var focusMode = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AFMode);
            comboBox_AFMode.Items.Add("AF-S");
            comboBox_AFMode.Items.Add("AF-C");
            comboBox_AFMode.Items.Add("MF fixed");
            comboBox_AFMode.Items.Add("MF selected");
            comboBox_AFMode.SelectedIndex = (int)focusMode;
        }



        //private void GetLiveViewAFMode()
        //{
        //    var liveViewAfMode = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewAF);
        //    comboBox_LiveViewAFMode.Items.Add("Face priority");
        //    comboBox_LiveViewAFMode.Items.Add("Wide area");
        //    comboBox_LiveViewAFMode.Items.Add("Normal area");
        //    comboBox_LiveViewAFMode.Items.Add("Subject tracking");
        //    comboBox_LiveViewAFMode.Items.Add("Spot area");
        //    comboBox_LiveViewAFMode.SelectedIndex = liveViewAfMode.Index;

        //}

        #endregion

    }

}