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
using System.Drawing;
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
        private NikonLiveViewImage? imageView = null;
        public double oldFocusValue;
        public double blurrynessAmount = 0;
        public double blurrynessAmountMask = 0;
        private readonly object imageLock = new object();
        public Bitmap maskBitmapLive;
        public bool maskFreeze = false;
        public bool liveViewStatus = false;
        private bool _isInitializingCameraSettings = false;

        private Mat maskMatLive;       // remplace maskBitmapLive
        private readonly object _maskLock = new object(); // si tu veux un lock simple

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
            LoadCameraSettings();
            EnsureLiveViewSize1920x1080();
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
        // Le System.Windows.Form.Timer sur lequel LiveViewTimer_Tick s'exécute est sur le trhead principal donc pas besoin de Invoke dans les appels aux contrôles du UI.
        async void LiveViewTimer_Tick(object? sender, EventArgs e)
        {
            if (isProcessing || _nikonOperationInProgress || device == null) return;
            isProcessing = true;

            try
            {
                if (!projet.LiveViewEnabled)
                {
                    projet.LiveViewEnabled = true;
                    projet.Save(appSettings.ProjectPath);
                }

                imageView = device.GetLiveViewImage();
                if (device.LiveViewEnabled && imageView != null && imageView.JpegBuffer.Length > 0)
                {
                    liveViewStatus = true;

                    // -- LUT Gamma (supposé remplir 'lutData' via UpdateGammaLUT) --
                    UpdateGammaLUT();
                    float gammaValue = trackBar_Gamma.Value / 10.0f;

                    using (var background = new Mat())
                    {
                        // 1) Decode JPEG -> Mat couleur
                        CvInvoke.Imdecode(imageView.JpegBuffer, ImreadModes.Color, background);

                        // 2) Applique LUT gamma (8 bits)
                        using (var lut = new Mat(1, 256, DepthType.Cv8U, 1))
                        {
                            System.Runtime.InteropServices.Marshal.Copy(lutData, 0, lut.DataPointer, lutData.Length);
                            CvInvoke.LUT(background, lut, background); // in-place OK
                        }

                        // 3) Masque luminosité (pipeline Mat)
                        Mat maskMatForThisFrame = null;
                        Mat maskMatForLiveProcessing = null;
                        bool usingFrozenMask = false;
                        try
                        {
                            if (!maskFreeze || maskMatLive == null)
                            {
                                maskMatForThisFrame = await BrightnessMaskFromBytesMat(
                                    imageView.JpegBuffer,
                                    hScrollBar_liveMaskThresh.Value,
                                    invert: false
                                );
                            }
                            else
                            {
                                usingFrozenMask = true;
                                lock (_maskLock)
                                {
                                    maskMatForThisFrame = maskMatLive?.Clone();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LiveMask] BrightnessMaskFromBytesMat error: {ex}");
                            maskMatForThisFrame = null;
                        }

                        // 3b) Si pas de masque -> Nettoyage de l'UI du masque et on saute
                        if (maskMatForThisFrame == null || maskMatForThisFrame.IsEmpty)
                        {
                            maskMatForThisFrame?.Dispose();
                            maskMatForThisFrame = null;
                            this.BeginInvoke(new Action(() =>
                            {
                                var oldUi = picBox_liveMaskLum.Image;
                                picBox_liveMaskLum.Image = null;
                                oldUi?.Dispose();
                            }));
                            goto AfterMaskWork;
                        }

                        bool maskIsAllBlack = IsMatAllBlack(maskMatForThisFrame);

                        if (!maskIsAllBlack)
                        {
                            maskMatForLiveProcessing = usingFrozenMask
                                ? maskMatForThisFrame.Clone()
                                : CreateBinaryMaskWithInset(maskMatForThisFrame, GetCurrentMaskShrink());
                            maskIsAllBlack = IsMatAllBlack(maskMatForLiveProcessing);
                        }

                        // 4) Remplacer le masque global avec le même masque que celui affiché.
                        if (!usingFrozenMask && !maskIsAllBlack)
                        {
                            lock (_maskLock)
                            {
                                var oldMat = maskMatLive;
                                maskMatLive = maskMatForLiveProcessing.Clone();
                                oldMat?.Dispose();
                            }
                        }
                        else if (!usingFrozenMask && maskIsAllBlack)
                        {
                            lock (_maskLock)
                            {
                                var oldMat = maskMatLive;
                                maskMatLive = null;
                                oldMat?.Dispose();
                            }

                            offsets.offsetX = 0;
                            offsets.offsetY = 0;
                            offsets.hasBlackOnBorder = false;
                            offsets.hasForeground = false;
                            offsets.boundingBox = Rectangle.Empty;
                        }

                        try
                        {
                            using var bmpTemp = (maskMatForLiveProcessing ?? maskMatForThisFrame).ToBitmap();
                            var uiClone = (Bitmap)bmpTemp.Clone();

                            this.BeginInvoke(new Action(() =>
                            {
                                var prevUi = picBox_liveMaskLum.Image;
                                picBox_liveMaskLum.Image = uiClone;
                                prevUi?.Dispose();
                            }));
                        }
                        catch (Exception)
                        {
                            maskMatForThisFrame.Dispose();
                            maskMatForLiveProcessing?.Dispose();
                            maskMatForThisFrame = null;
                            maskMatForLiveProcessing = null;
                            AppendTextToConsoleNL($"Erreur LiveViewTimer_Tick :: this.BeginInvoke(new Action(() => using var bmpTemp ...");
                        }
                       

                    AfterMaskWork:
                        ;

                        // 5) Centrage en tâche de fond — on clone le Mat pour éviter Dispose concurrent
                        Mat localMaskMat = null;
                        lock (_maskLock)
                        {
                            if (maskMatLive != null && !maskMatLive.IsEmpty && IsAutoCenterOffsetTrackingActive())
                            {
                                localMaskMat = maskMatLive.Clone();
                            }
                        }

                        if (localMaskMat != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    // Si CalculeDuCentrageAsync attend un Bitmap :
                                    using var localBitmap = localMaskMat.ToBitmap();
                                    await CalculeDuCentrageAsync(localBitmap, 1);

                                    lbl_Centrage.Invoke(new Action(() =>
                                    {
                                        lbl_Centrage.Text = $"Offset X={offsets.offsetX:F2}, Y={offsets.offsetY:F2}, Dépasse: {offsets.hasBlackOnBorder}, Présent: {offsets.hasForeground}";
                                    }));
                                }
                                finally
                                {
                                    localMaskMat.Dispose();
                                }
                            });
                        }

                        // 6) Pipeline de netteté / overlay avec le masque courant (Mat)
                        if (maskMatForLiveProcessing != null && !maskMatForLiveProcessing.IsEmpty)
                        {
                            using (var sourceImage = background.ToImage<Bgr, byte>()) // si tu as besoin de Image<> pour d'autres opérations
                            using (var maskGrayImg = maskMatForLiveProcessing.ToImage<Gray, byte>())
                            using (var resizedMaskImg = maskGrayImg.Width != sourceImage.Width || maskGrayImg.Height != sourceImage.Height
                                                         ? maskGrayImg.Resize(sourceImage.Width, sourceImage.Height, Emgu.CV.CvEnum.Inter.Nearest)
                                                         : maskGrayImg.Copy())
                            using (var grayForFocus = new Mat())
                            using (var safeMask = new Mat()) // Mat final pour ComputeSharpnessGridMaskedROI
                            {
                                // Inversion/convert éventuelles → si besoin d'un masque inversé pour dessin:
                                // using (var invertedMask = resizedMaskImg.Not())
                                // using (var maskBgr = invertedMask.Convert<Bgr, byte>()) { /* ... */ }

                                // Image -> Mat pour la carte de netteté
                                CvInvoke.CvtColor(background, grayForFocus, ColorConversion.Bgr2Gray);

                                resizedMaskImg.Mat.CopyTo(safeMask);

                                int blockSize = trackBar_blobCount.Value * 8;
                                double[,] sharpnessGrid = ComputeSharpnessGridMaskedROI(grayForFocus, safeMask, blockSize, 0.8);

                                currentLiveViewFocusMap = new FocusMap
                                {
                                    FocusPosition = focusStackStepVar,  // ICI : ta position courante
                                    SharpnessGrid = sharpnessGrid
                                };

                                // 7) Overlay des blocs nets
                                using (var overlayImage = background.ToImage<Bgr, byte>()) // copie pour dessin
                                {
                                    for (int y = 0; y < sharpnessGrid.GetLength(0); y++)
                                    {
                                        for (int x = 0; x < sharpnessGrid.GetLength(1); x++)
                                        {
                                            double sharpness = sharpnessGrid[y, x];
                                            blurThreshold = (double)trackBar_blurThreshold.Value;
                                            if (sharpness >= blurThreshold)
                                            {
                                                Rectangle rect = new Rectangle(x * blockSize, y * blockSize, blockSize, blockSize);
                                                overlayImage.Draw(rect, new Bgr(Color.LimeGreen), 1);
                                            }
                                        }
                                    }

                                    // Compte des blocs au-dessus du seuil
                                    blurredBlocks = sharpnessGrid.Cast<double>().Count(v => v >= blurThreshold);
                                    lbl_blobCount.Text = blurredBlocks.ToString();

                                    // 8) Affichage principal
                                    var prevMain = picBox_LiveView_Main.Image;
                                    if (projet.ViewSharpnessOverlay)
                                    {
                                        // On génère un Bitmap frais et on remplace l'ancien
                                        using var overlayBmp = overlayImage.ToBitmap();
                                        picBox_LiveView_Main.Image = (Bitmap)overlayBmp.Clone(); // clone optionnel si tu veux standardiser
                                    }
                                    else
                                    {
                                        using var bgBmp = background.ToImage<Bgr, byte>().ToBitmap();
                                        picBox_LiveView_Main.Image = (Bitmap)bgBmp.Clone(); // clone optionnel
                                    }
                                    prevMain?.Dispose();
                                }
                            }
                        }
                        else
                        {
                            // Pas de masque → afficher simplement le background
                            var prevMain = picBox_LiveView_Main.Image;
                            using var bgBmp = background.ToImage<Bgr, byte>().ToBitmap();
                            picBox_LiveView_Main.Image = (Bitmap)bgBmp.Clone();
                            prevMain?.Dispose();
                        }

                        maskMatForThisFrame?.Dispose();
                        maskMatForLiveProcessing?.Dispose();
                    } // end using background
                }
                else
                {
                    liveViewStatus = false;
                    var prevMain = picBox_LiveView_Main.Image;
                    picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
                    prevMain?.Dispose();
                }
            }
            catch (NikonException ex)
            {
                Console.WriteLine("NikonException: " + ex.Message);
                var prevMain = picBox_LiveView_Main.Image;
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
                prevMain?.Dispose();
            }
            finally
            {
                isProcessing = false;
            }
        }



        private static bool IsMatAllBlack(Mat mat)
        {
            if (mat == null || mat.IsEmpty) return true;

            using var gray = new Mat();
            if (mat.NumberOfChannels == 1)
                mat.CopyTo(gray);
            else
                CvInvoke.CvtColor(mat, gray,
                    mat.NumberOfChannels == 3 ? ColorConversion.Bgr2Gray : ColorConversion.Bgra2Gray);

            double minVal = 0, maxVal = 0;
            // Pour 2D, 2 éléments suffisent. Pour N-dim, dimensionner selon le nombre d’axes.
            int[] minLoc = new int[gray.Dims >= 1 ? gray.Dims : 2];
            int[] maxLoc = new int[minLoc.Length];

            // Certaines versions attendent 'ref' sur les scalaires, et des tableaux pour les localisations :
            CvInvoke.MinMaxIdx(gray, out minVal, out maxVal, minLoc, maxLoc, null);

            if (maxVal <= 0.0) return true;

            using var binary = new Mat();
            CvInvoke.Threshold(gray, binary, 1, 255, ThresholdType.Binary);

            int whitePixels = CvInvoke.CountNonZero(binary);
            int totalPixels = Math.Max(1, gray.Rows * gray.Cols);
            double whiteRatio = whitePixels / (double)totalPixels;

            return whiteRatio < 0.003;
        }



       
        private void OnNikonProgress(NikonDevice sender, eNkMAIDDataObjType type, int done, int total)
        {
            //int percent = (int)((done / (float)total) * 100);

            //if (progressBar_ImageSave.InvokeRequired)
            //{
            //    progressBar_ImageSave.Invoke(new Action(() =>
            //    {
            //        progressBar_ImageSave.Value = Math.Min(percent, 100);

            //        // Remise à zéro une fois le transfert terminé
            //        if (done >= total)
            //        {
            //            progressBar_ImageSave.Value = 0;
            //        }
            //    }));
            //}
            //else
            //{
            //    progressBar_ImageSave.Value = Math.Min(percent, 100);

            //    if (done >= total)
            //    {
            //        progressBar_ImageSave.Value = 0;
            //    }
            //}
        }


        #region CAMERA INFO
        private void LoadCameraSettings()
        {
            if (device == null) return;

            _isInitializingCameraSettings = true;
            try
            {
                TryLoadCameraSetting("Type d'image", GetImageType);
                TryLoadCameraSetting("Dimensions de l'image", GetImageSize);
                TryLoadCameraSetting("Shutter Speed", GetShutterSpeed);
                TryLoadCameraSetting("Dimensions du liveView", GetLiveViewSize);
                TryLoadCameraSetting("Mode d'exposition", GetExposureModes);
                TryLoadCameraSetting("Focus Mode", GetFocusMode);
                TryLoadCameraSetting("AF Mode", GetAFMode);
                TryLoadCameraSetting("AFcPriority", GetAfcPriority);
                TryLoadCameraSetting("FocusAreaMode", GetFocusAreaMode);
                TryLoadCameraSetting("LiveView AF Mode", GetLiveViewAFMode);
            }
            finally
            {
                _isInitializingCameraSettings = false;
            }
        }

        private void TryLoadCameraSetting(string settingName, Action loadAction)
        {
            try
            {
                loadAction();
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Lecture setting caméra ignorée ({settingName}): {ex.Message}");
            }
        }

        private void PopulateEnumComboBox(System.Windows.Forms.ComboBox comboBox, NikonEnum values)
        {
            comboBox.BeginUpdate();
            try
            {
                comboBox.Items.Clear();
                for (int i = 0; i < values.Length; i++)
                {
                    comboBox.Items.Add(values[i].ToString());
                }

                if (values.Index >= 0 && values.Index < comboBox.Items.Count)
                {
                    comboBox.SelectedIndex = values.Index;
                }
            }
            finally
            {
                comboBox.EndUpdate();
            }
        }

        private void PopulateFixedComboBox(System.Windows.Forms.ComboBox comboBox, int selectedIndex, params string[] values)
        {
            comboBox.BeginUpdate();
            try
            {
                comboBox.Items.Clear();
                comboBox.Items.AddRange(values.Cast<object>().ToArray());
                if (selectedIndex >= 0 && selectedIndex < comboBox.Items.Count)
                {
                    comboBox.SelectedIndex = selectedIndex;
                }
            }
            finally
            {
                comboBox.EndUpdate();
            }
        }

        private void GetImageSize()
        {
            NikonEnum imgSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ImageSize);
            PopulateEnumComboBox(comboBox_TaillePhotos, imgSize);

            projet.PictureWidth = int.Parse((imgSize[imgSize.Index].ToString().Split("*")[0].Substring(2)));
            projet.PictureHeight = int.Parse(imgSize[imgSize.Index].ToString().Split("*")[1][..^1]);
        }

        private void GetImageType()
        {
            NikonEnum imgType = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_CompressionLevel);
            PopulateEnumComboBox(comboBox_ImageType, imgType);
        }


        private void GetShutterSpeed()
        {
            NikonEnum exposureTime = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed);
            PopulateEnumComboBox(comboBox_shutterTime, exposureTime);
        }

        private void GetLiveViewSize()
        {
            NikonEnum liveviewSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize);
            PopulateFixedComboBox(comboBox_TailleLiveView, liveviewSize.Index, "640 x 424", "1280 x 720", "1920 x 1080");
        }

        private void EnsureLiveViewSize1920x1080()
        {
            const int targetIndex = 2;
            const string targetLabel = "1920 x 1080";

            if (device == null) return;

            try
            {
                NikonEnum liveviewSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize);
                if (targetIndex >= liveviewSize.Length)
                {
                    AppendTextToConsoleNL("LiveView 1920 x 1080 non disponible sur cette caméra.");
                    return;
                }

                if (liveviewSize.Index != targetIndex)
                {
                    liveviewSize.Index = targetIndex;
                    device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize, liveviewSize);
                    AppendTextToConsoleNL("LiveView réglé à 1920 x 1080.");
                }

                if (comboBox_TailleLiveView.Items.Count > targetIndex)
                {
                    _isInitializingCameraSettings = true;
                    try
                    {
                        comboBox_TailleLiveView.SelectedIndex = targetIndex;
                    }
                    finally
                    {
                        _isInitializingCameraSettings = false;
                    }
                }
                else if (!comboBox_TailleLiveView.Items.Contains(targetLabel))
                {
                    comboBox_TailleLiveView.Items.Add(targetLabel);
                    comboBox_TailleLiveView.SelectedItem = targetLabel;
                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Impossible de régler le LiveView à 1920 x 1080: {ex.Message}");
            }
        }

        private void GetExposureModes()
        {
            NikonEnum modeSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ExposureMode);
            PopulateFixedComboBox(comboBox_ExpoMode, modeSize.Index, "Programmed Auto (P)", "Shutter Priority (S)", "Aperture Priority (A)", "Manual (M)");
        }

        private void GetAfcPriority()
        {
            NikonEnum focusModes = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_AFcPriority);
            PopulateEnumComboBox(comboBox_AfcPriority, focusModes);
        }

        private void GetFocusAreaMode()
        {
            NikonEnum focusAreaModes = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_FocusAreaMode);
            PopulateEnumComboBox(comboBox_FocusAeraMode, focusAreaModes);
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
            PopulateFixedComboBox(comboBox_AFMode, (int)focusMode, "AF-S", "AF-C", "MF fixed", "MF selected");
        }



        private void GetLiveViewAFMode()
        {
            var liveViewAfMode = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewAF);
            if (liveViewAfMode == null)
            {
                AppendTextToConsoleNL("Lecture setting caméra ignorée (LiveView AF Mode): capacité non disponible");
                return;
            }

            PopulateFixedComboBox(comboBox_LiveViewAFMode, liveViewAfMode.Index, "Face priority", "Wide area", "Normal area", "Subject tracking", "Spot area");
        }

        #endregion

    }

}
