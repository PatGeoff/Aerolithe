using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Nikon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using static Emgu.CV.DISOpticalFlow;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private TaskCompletionSource<bool>? imageReadyTcs;
        private TaskCompletionSource<int>? captureCompleteTcs;
        private TaskCompletionSource<bool>? miniaturesTcs;
        private Size panelSize = new Size(250, 200);
        private readonly SemaphoreSlim _nikonOperationLock = new(1, 1);
        private volatile bool _nikonOperationInProgress;



        public static Task InvokeOnUIAsync(Control control, Func<Task> action)
        {
            if (control.InvokeRequired)
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                control.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await action();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }));

                return tcs.Task;
            }
            else
            {
                return action();
            }
        }

        public static Task InvokeOnUIAsync(Control control, Action action)
        {
            if (control.InvokeRequired)
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                control.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        action();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }));

                return tcs.Task;
            }

            action();
            return Task.CompletedTask;
        }

        private async Task RunExclusiveNikonOperationAsync(Func<Task> action, bool pauseLiveView = false)
        {
            await _nikonOperationLock.WaitAsync();

            bool resumeLiveView = false;

            try
            {
                _nikonOperationInProgress = true;

                if (pauseLiveView)
                {
                    await InvokeOnUIAsync(this, () =>
                    {
                        if (device != null && device.LiveViewEnabled)
                        {
                            resumeLiveView = projet.LiveViewEnabled;
                            liveViewTimer.Stop();
                            device.LiveViewEnabled = false;
                        }
                    });
                }

                await InvokeOnUIAsync(this, action);
            }
            finally
            {
                try
                {
                    if (pauseLiveView && resumeLiveView)
                    {
                        await InvokeOnUIAsync(this, () =>
                        {
                            if (device != null && projet.LiveViewEnabled && !device.LiveViewEnabled)
                            {
                                device.LiveViewEnabled = true;
                                liveViewTimer.Start();
                            }
                        });
                    }
                }
                finally
                {
                    _nikonOperationInProgress = false;
                    _nikonOperationLock.Release();
                }
            }
        }

        private async Task CaptureImageAndWaitForMiniatureAsync()
        {
            if (_pendingMiniatureTcs != null)
                throw new InvalidOperationException("Une capture est déjà en cours.");

            var miniatureTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingMiniatureTcs = miniatureTcs;

            try
            {
                AppendTextToConsoleNL($"[Thread CaptureImageAndWaitForMiniatureAsync] Thread# {Thread.CurrentThread.ManagedThreadId} -> UI? {(!this.InvokeRequired).ToString()}");
                await ManualFocusAsync(1, 1);
                await Task.Delay(200);
                await takePictureAsync();
                await Task.Delay(200);
                await ManualFocusAsync(1, 1);
                await miniatureTcs.Task;
            }
            finally
            {
                if (ReferenceEquals(_pendingMiniatureTcs, miniatureTcs))
                {
                    _pendingMiniatureTcs = null;
                }
            }
        }


        private async void takePictureAsyncSimple()
        {
            try
            {
                miniaturesTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                AppendTextToConsoleNL($"[Thread takePictureAsyncSimple] Invoke Required Thread# {Thread.CurrentThread.ManagedThreadId} -> is Thread same as UI? {(!this.InvokeRequired).ToString()}");

                Stopwatch sw = Stopwatch.StartNew();
                await takePictureAsync();  // attend que imageReadyTcs soit résolu   
                sw.Stop();
                string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");
                AppendTextToConsoleNL($"photo prise en {tempsMs} secondes");
            }
            catch (Exception ex)
            {
                _stopRequested = true;
                AppendTextToConsoleNL($"Erreur takePictureAsyncSimple: {ex.Message}");
                MessageBox.Show(
                    this,
                    $"Une erreur caméra est survenue pendant la prise de photo.{Environment.NewLine}{Environment.NewLine}Message d'erreur: {ex.Message}",
                    "Erreur caméra",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public async Task takePictureAsync()
        {
            if (imageReadyTcs != null)
            {
                throw new InvalidOperationException("Une capture est déjà en cours.");
            }

            var currentImageReadyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var currentCaptureCompleteTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            imageReadyTcs = currentImageReadyTcs;
            captureCompleteTcs = currentCaptureCompleteTcs;

            try
            {
                timing.StartTimer();

                await InvokeOnUIAsync(this, () =>
                {
                    AppendTextToConsoleNL($"[Thread takePictureAsync] Thread# {Thread.CurrentThread.ManagedThreadId} -> UI? {(!this.InvokeRequired).ToString()}");
                    device.Capture();
                    AppendTextToConsoleNL("Capture de l'image par la Nikon ...");
                });

                var completedTask = await Task.WhenAny(
                    currentImageReadyTcs.Task,
                    Task.Delay(TimeSpan.FromSeconds(20)));

                if (completedTask != currentImageReadyTcs.Task)
                {
                    if (currentCaptureCompleteTcs.Task.IsCompletedSuccessfully)
                    {
                        AppendTextToConsoleNL($"CaptureComplete reçu, mais pas de ImageReady. data={currentCaptureCompleteTcs.Task.Result}");
                    }

                    throw new TimeoutException("Timeout en attente de device_ImageReady après Capture().");
                }

                await currentImageReadyTcs.Task;
            }
            catch (NikonException ex) when (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
            {
                AppendTextToConsoleNL(ex.Message);
                await Task.Delay(200);
                throw;
            }
            catch (Exception)
            {
                AppendTextToConsoleNL("La Nikon n'est pas active");
                throw;
            }
            finally
            {
                if (ReferenceEquals(imageReadyTcs, currentImageReadyTcs))
                {
                    imageReadyTcs = null;
                }

                if (ReferenceEquals(captureCompleteTcs, currentCaptureCompleteTcs))
                {
                    captureCompleteTcs = null;
                }
            }
        }


        private async void device_ImageReady(NikonDevice sender, NikonImage image)
        {
            

            timing.StopTimer();
            AppendTextToConsoleNL($"device_ImageReady: {timing.ElapsedTime.TotalSeconds:F3} secondes");



            try
            {
                if (image.Type != NikonImageType.Jpeg)
                {
                    Invoke(() => MessageBox.Show("L'image doit être du type JPEG. Vérifiez les paramètres de la caméra."));
                    return;
                }


                if (!ProjectSaveTargetIsReady())
                {
                    AppendTextToConsoleNL($"Un des dossiers n'existe pas:" +
                        $"\nprojet.ImageFolderPath = {projet.ImageFolderPath}" +
                        $"\nprojet.ImageNameBase = {projet.ImageNameBase}" +
                        $"\nprojet.MesurementsFolderPath = {projet.GetMesurementsFullImagePath()}" +
                        $"\nprojet.FocusStackFolderName = {projet.FocusStackFolderName}" +
                        $"\net Finalement projet.SaveImageToDisk = {projet.SaveImageToDisk}"
                        );
                }


                Bitmap finalBitmap = null;

                int maskThreshold = GetCurrentMaskThreshold();

                try
                {
                    finalBitmap = await Task.Run(async () =>
                    {
                        using (var memoryStream = new MemoryStream(image.Buffer))
                        using (var originalBitmap = new Bitmap(memoryStream))
                        {
                            projet.PictureWidth = originalBitmap.Width;
                            projet.PictureHeight = originalBitmap.Height;
                            if (!string.IsNullOrWhiteSpace(appSettings.ProjectPath))
                            {
                                projet.Save(appSettings.ProjectPath); // -- <
                            }

                            if (!ProjectSaveTargetIsReady())
                            {
                                AppendTextToConsoleNL("Aucun projet valide n'est ouvert. Image reçue et affichée, mais non sauvegardée.");
                                return new Bitmap(originalBitmap);
                            }

                            Bitmap processedBitmap;
                            Mat registeredMask = null;
                            try
                            {
                                AppendTextToConsoleNL(
                                    $"MASK SAVE STATE: ApplyMask={projet.ApplyMask}, FocusStack={projet.FocusStackEnabled}, photoPourMesure={photoPourMesure}, maskFreeze={maskFreeze}, SaveImageForMesurements={projet.SaveImageForMesurements}, threshold={maskThreshold}, algo={appSettings.MaskAlgorithmIndex}"
                                );

                                if (projet.ApplyMask && !projet.FocusStackEnabled)
                                {
                                    string savedMaskPath = projet.GetMaskFullImagePath();
                                    try
                                    {
                                        if (File.Exists(savedMaskPath))
                                        {
                                            registeredMask = LoadSavedMaskAsGrayMat(savedMaskPath);
                                            AppendTextToConsoleNL("Masque sauvegardé appliqué à l'image : " + savedMaskPath);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        AppendTextToConsoleNL("Masque sauvegardé non disponible, utilisation du masque live: " + ex.Message);
                                        registeredMask?.Dispose();
                                        registeredMask = null;
                                    }

                                    if (registeredMask == null)
                                    {
                                        lock (_maskLock)
                                        {
                                            if (maskMatLive != null && !maskMatLive.IsEmpty)
                                            {
                                                registeredMask = maskMatLive.Clone();
                                            }
                                        }

                                        if (registeredMask != null)
                                        {
                                            AppendTextToConsoleNL("Masque live appliqué à l'image sans écrire de fichier masque.");
                                        }
                                        else
                                        {
                                            AppendTextToConsoleNL("Aucun masque sauvegardé ou live disponible. Image sauvegardée sans masque.");
                                        }
                                    }

                                    processedBitmap = registeredMask != null
                                        ? ApplyMask(originalBitmap, registeredMask)
                                        : new Bitmap(originalBitmap);
                                }
                                else
                                {
                                    processedBitmap = projet.ApplyMask ? ApplyMask(originalBitmap) : new Bitmap(originalBitmap);
                                }
                            }
                            finally
                            {
                                registeredMask?.Dispose();
                            }

                            // faire un projet.SavePictures  ???
                            // Sauvegarde si activée                          

                            AppendTextToConsoleNL("device_ImageReady :: PreparationDossierDestTemp");

                            PreparationDossierDestTemp();

                            using (var saveStream = new MemoryStream())
                            {

                                processedBitmap.Save(saveStream, ImageFormat.Jpeg);
                                saveStream.Position = 0;
                                try
                                {
                                    // projet.SaveImageForMesurements pour prendre automatiquement une image pour mesure à chaque angle. Lorsque l'image sera prise à cet effet, photoPourMesure sera true pour un instant. 
                                    // photoPourMesure pour prendre une seule photo via le bouton Prendre une photo
                                    // Dans les deux cas, au moment où la photo est prise, le focusstack et le masque sont disablés. 
                                    // On peut enregistrer l'image dans projet.GetMesurementsFullImagePath()

                                    if (photoPourMesure)
                                    {
                                        Stopwatch sw = Stopwatch.StartNew();
                                        string iMes = projet.GetMesurementsFullImagePath();                                        
                                        string iName = projet.GetMesurementImageNameFull();
                                        Invoke(() => AppendTextToConsoleNL("Sauvegarde de la photo pour mesure " + iName + " ..."));
                                        AppendTextToConsoleNL("device_ImageReady :: SaveStreamAsJpegWithProgress");
                                        SaveStreamAsJpegWithProgress(saveStream, iMes);
                                        sw.Stop();
                                        string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");
                                        AppendTextToConsoleNL($"téléchargée en {tempsMs} secondes");
                                        Invoke(() => AfficherMiniatures(projet.ImageNameBase, iMes, panelSize));
                                        saveImageForMesurementRemettre();
                                    }

                                    else

                                    {
                                        string iName = projet.FocusStackEnabled ? projet.GetImageNameFull() : projet.GetImageNameFullNoFS();
                                        string iPath = projet.FocusStackEnabled ? projet.GetImageFullPath() : projet.GetImageFullPathNoFS();

                                        Stopwatch sw = Stopwatch.StartNew();
                                        Invoke(() => AppendTextToConsoleNL("Sauvegarde de " + iName + " ..."));

                                        AppendTextToConsoleNL("device_ImageReady :: SaveStreamAsJpegWithProgress");
                                        SaveStreamAsJpegWithProgress(saveStream, iPath);

                                        sw.Stop();
                                        string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");

                                        AppendTextToConsoleNL($"téléchargée en {tempsMs} secondes");

                                        AppendTextToConsoleNL("device_ImageReady :: AfficherMiniatures");
                                        Invoke(() => AfficherMiniatures(projet.ImageNameBase, iPath, panelSize));
                                        
                                    }
                                    

                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(ex.Message);
                                }
                            }

                            
                            return processedBitmap;
                        }
                    });


                    // Mise à jour de l'UI
                    Invoke(() =>
                    {
                        picBox_pictureTaken.Image?.Dispose();
                        picBox_pictureTaken.Image = finalBitmap;
                    });

                    // Signale que TakePictureAsync est terminée, le await dans les autres méthodes devrait s'exécuter
                    if (imageReadyTcs != null)
                    {
                        AppendTextToConsoleNL("device_ImageReady :: imageReadyTcs.TrySetResult(true)");                        
                        imageReadyTcs.TrySetResult(true);
                    }

                    if (!ProjectSaveTargetIsReady())
                    {
                        _pendingMiniatureTcs?.TrySetResult(true);
                        miniaturesTcs?.TrySetResult(true);
                    }


                }
                catch (Exception ex)
                {
                    imageReadyTcs?.TrySetException(ex);
                    _pendingMiniatureTcs?.TrySetException(ex);
                    miniaturesTcs?.TrySetException(ex);
                    Invoke(() => MessageBox.Show("Erreur lors du traitement de l'image : " + ex.Message));
                }
            }
            catch (Exception ex)
            {
                Invoke(() => MessageBox.Show("device_ImageReady exception: " + ex.Message));
                imageReadyTcs?.TrySetException(ex);
                _pendingMiniatureTcs?.TrySetException(ex);
                miniaturesTcs?.TrySetException(ex);
            }

           
        }


        private bool ProjectSaveTargetIsReady()
        {
            if (!projet.SaveImageToDisk)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(appSettings.ProjectPath)
                && !string.IsNullOrWhiteSpace(projet.ImageFolderPath)
                && !string.IsNullOrWhiteSpace(projet.ImageNameBase)
                && !string.IsNullOrWhiteSpace(projet.FocusStackFolderName)
                && !string.IsNullOrWhiteSpace(projet.GetMesurementsFolderpath());
        }


        private void AfficherMiniatures(string nomImage, string imagePath, Size panelSize)
        {
            AppendTextToConsoleNL("AfficherMiniatures");
            string nomImageModifie = Path.GetFileName(imagePath).Split(".")[0];
            try
            {
                using (Image originalImage = System.Drawing.Image.FromFile(imagePath))
                {
                    Image resizedImage = ResizeImage(originalImage, 150, 100);

                    Panel borderPanel = new Panel
                    {
                        Size = panelSize,
                        BorderStyle = BorderStyle.FixedSingle
                    };

                    TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
                    {
                        ColumnCount = 2,
                        RowCount = 2,
                        Dock = DockStyle.Fill
                    };

                    // Ajout des colonnes
                    tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 85F)); // Pour le label
                    tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F)); // Pour le bouton

                    // Ajout des lignes
                    tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F)); // Ligne du label + bouton
                    tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Ligne de l'image

                    Button deleteButton = new Button
                    {
                        Text = "X",
                        Dock = DockStyle.Fill,
                        Font = new Font(FontFamily.GenericSansSerif, 6),
                        BackColor = Color.FromArgb(100, 30, 30, 30),
                        ForeColor = Color.Red,
                        Margin = new Padding(0),
                        FlatStyle = FlatStyle.Flat
                    };

                    deleteButton.FlatAppearance.BorderSize = 0;
                    deleteButton.FlatAppearance.BorderColor = Color.Black;

                    deleteButton.Click += (s, e) =>
                    {
                        var result = MessageBox.Show(
                            "Voulez-vous aussi supprimer le fichier sur le disque ?",
                            "Suppression de l'image",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );

                        if (result == DialogResult.Yes)
                        {
                            try
                            {
                                if (File.Exists(imagePath))
                                {
                                    File.Delete(imagePath);
                                }

                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Erreur lors de la suppression du fichier : {ex.Message}");
                            }
                        }

                        flowLayoutPanel1.Controls.Remove(borderPanel);
                        borderPanel.Dispose();
                    };


                    Label label = new Label
                    {
                        Text = nomImageModifie,
                        TextAlign = ContentAlignment.MiddleRight, // aligné à droite
                        ForeColor = Color.White,
                        Dock = DockStyle.Fill,
                        Font = new Font(FontFamily.GenericSansSerif, 7)
                    };

                    if (photoPourMesure)
                    {
                        label.ForeColor = Color.Orange;
                    }
                    else
                    {
                        label.ForeColor = Color.White;
                    }

                    PictureBox pictureBox = new PictureBox
                    {
                        Image = resizedImage,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Dock = DockStyle.Fill
                    };


                    new ToolTip().SetToolTip(pictureBox, imagePath);


                    pictureBox.Click += (sender, e) =>
                    {
                        try
                        {
                            ImageViewerForm viewer = new ImageViewerForm(imagePath);
                            viewer.Show();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Erreur à l'ouverture de l'image : {ex.Message}");
                        }
                    };

                    tableLayoutPanel.Controls.Add(label, 0, 0);
                    tableLayoutPanel.Controls.Add(deleteButton, 1, 0);
                    tableLayoutPanel.SetColumnSpan(pictureBox, 2); // image sur toute la largeur
                    tableLayoutPanel.Controls.Add(pictureBox, 0, 1);

                    borderPanel.Controls.Add(tableLayoutPanel);

                    flowLayoutPanel1.Controls.Add(borderPanel);
                    flowLayoutPanel1.ScrollControlIntoView(borderPanel);

                    //AppendTextToConsoleNL($"[Thread AfficherMiniatures ::  miniaturesTcs.TrySetResult(true)]  sur le thread # {Thread.CurrentThread.ManagedThreadId}]  Thread du UI? {(!this.InvokeRequired).ToString()}");

                    //bool success = miniaturesTcs.TrySetResult(true);
                    //AppendTextToConsoleNL($"miniaturesTcs? {success}");

                    AppendTextToConsoleNL($"[Thread AfficherMiniatures :: _pendingMiniatureTcs?.TrySetResult(true)] sur le thread # {Thread.CurrentThread.ManagedThreadId}]  Thread du UI? {(!this.InvokeRequired).ToString()}");

                    _pendingMiniatureTcs?.TrySetResult(true);
                    _pendingMiniatureTcs = null;
                    miniaturesTcs?.TrySetResult(true);
                    miniaturesTcs = null;

                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur lors de l'affichage miniature : {ex.Message}");
            }

          
        }

        private Image ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var wrapMode = new System.Drawing.Imaging.ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
        private int GetCurrentMaskThreshold()
        {
            try
            {
                if (hScrollBar_liveMaskThresh.InvokeRequired)
                {
                    return (int)hScrollBar_liveMaskThresh.Invoke(new Func<int>(() => hScrollBar_liveMaskThresh.Value));
                }

                return hScrollBar_liveMaskThresh.Value;
            }
            catch
            {
                return ClampMaskThreshold(GetMaskThresholdSetting(appSettings.MaskAlgorithmIndex));
            }
        }

        private async Task<Mat> BuildRegisteredMaskFromCapturedJpegAsync(byte[] jpegBuffer, int threshold)
        {
            foreach (var candidate in new[]
            {
                (Threshold: threshold, Invert: false),
                (Threshold: threshold, Invert: true),
                (Threshold: -1, Invert: false),
                (Threshold: -1, Invert: true)
            })
            {
                Mat mask = await BrightnessMaskFromBytesMat(jpegBuffer, candidate.Threshold, candidate.Invert);
                if (!IsMatAllBlack(mask))
                {
                    return mask;
                }

                mask.Dispose();
            }

            throw new InvalidOperationException("Impossible de générer un masque enregistré avec l'image capturée.");
        }

        private Bitmap ApplyMask(Bitmap originalBitmap)
        {
            Mat maskClone = null;
            try
            {
                lock (_maskLock)
                {
                    if (maskMatLive == null || maskMatLive.IsEmpty)
                    {
                        throw new InvalidOperationException("Masque live nul ou vide.");
                    }

                    maskClone = maskMatLive.Clone();
                }

                return ApplyMask(originalBitmap, maskClone);
            }
            finally
            {
                maskClone?.Dispose();
            }
        }

        private Bitmap ApplyMask(Bitmap originalBitmap, Mat maskMat)
        {
            using var sourceImage = originalBitmap.ToImage<Bgr, byte>();
            using var maskGray = maskMat.ToImage<Gray, byte>();
            using var resizedMask = maskGray.Resize(sourceImage.Width, sourceImage.Height, Emgu.CV.CvEnum.Inter.Nearest);
            using var binaryMask = CreateBinaryMaskWithInset(resizedMask.Mat);

            using var sourceMat = sourceImage.Mat;
            using var masked = Mat.Zeros(sourceMat.Rows, sourceMat.Cols, sourceMat.Depth, sourceMat.NumberOfChannels);
            sourceMat.CopyTo(masked, binaryMask);

            int nonZero = CvInvoke.CountNonZero(binaryMask);
            AppendTextToConsoleNL($"ApplyMask: pixels masque={nonZero}/{binaryMask.Rows * binaryMask.Cols}");

            return masked.ToBitmap();
        }

        private static Mat CreateBinaryMaskWithInset(Mat mask)
        {
            var binaryMask = new Mat();
            CvInvoke.Threshold(mask, binaryMask, 1, 255, ThresholdType.Binary);

            using var kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));
            CvInvoke.Erode(binaryMask, binaryMask, kernel, new Point(-1, -1), 1, BorderType.Constant, new MCvScalar(0));

            return binaryMask;
        }

        public void SaveStreamAsJpegWithProgress(Stream imageStream, string outputPath)
        {
            // Create an Image object from the stream
            using Image image = System.Drawing.Image.FromStream(imageStream);

            // Save the image to a temporary stream
            using (MemoryStream tempStream = new MemoryStream())
            {
                image.Save(tempStream, ImageFormat.Jpeg);
                tempStream.Position = 0;

                // Get the total length of the stream
                long totalLength = tempStream.Length;
                byte[] buffer = new byte[4096];
                int bytesRead;
                long totalBytesRead = 0;

                // Open the output file stream
                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    while ((bytesRead = tempStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                    }
                }
            }
        }

        public async Task SaveMesurementImage()
        {
            AppendTextToConsoleNL("SaveMesurementImage");
            AppendTextToConsoleNL($"[Thread SaveMesurementImage] Invoke Required Thread# {Thread.CurrentThread.ManagedThreadId} -> is Thread same as UI? {(!this.InvokeRequired).ToString()}");

            await nikonDoFocus();
            await Task.Delay(200);
            await saveImageForMesurementEnable();
            await Task.Delay(200);

            try
            {
                // Ce micro-déplacement débloque souvent la Nikon et réduit fortement
                // le délai avant ImageReady dans ce projet.
                await ManualFocusAsync(1, 1);
                await Task.Delay(200);
                await takePictureAsync();
                await Task.Delay(200);
                await ManualFocusAsync(1, 1);

                //this.BeginInvoke(new Action(async () =>
                //{
                //    try
                //    {

                //        await takePictureAsync();
                //    }
                //    catch (Exception ex)
                //    {
                //        AppendTextToConsoleNL(ex.Message);
                //    }
                //}));


            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
                AppendTextToConsoleNL($"Erreur SaveMesurementImage :: takePictureAsync:  {ex.Message}");
                _stopRequested = true;
                throw;
            }

            AppendTextToConsoleNL(timing.ElapsedTime.TotalSeconds.ToString("F2"));

            //  await saveImageForMesurementRemettre(); est effectué dans affichierMiniatures

        }

        public async Task SaveMaskAsPngTransparentBlack(Mat maskSrc, string outputPathPng)
        {
            if (maskSrc == null || maskSrc.IsEmpty)
            {
                AppendTextToConsoleNL("ERREUR (PNG masque): Mat source nul ou vide.");
                return;
            }

            try
            {
                // S'assurer que le dossier existe
                var dir = Path.GetDirectoryName(outputPathPng);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await Task.Run(() =>
                {
                    // 1) Obtenir un masque 8UC1 (Gray, 0..255).
                    //    - Si le Mat est couleur: le convertir en Gray.
                    //    - Si la profondeur n'est pas 8 bits: normaliser/convertir vers 0..255.
                    using var grayMask = new Mat();

                    // Déterminer si multi-canaux
                    int channels = maskSrc.NumberOfChannels;
                    DepthType depth = maskSrc.Depth;

                    if (channels == 1)
                    {
                        // Déjà mono-canal
                        if (depth == DepthType.Cv8U)
                        {
                            // Copie directe en 8UC1
                            maskSrc.CopyTo(grayMask);
                        }
                        else
                        {
                            // Convertir la profondeur vers 8 bits (normalisation)
                            using var tmp = new Mat();
                            // Normaliser la plage min..max vers 0..255 pour éviter les saturations injustifiées
                            CvInvoke.Normalize(maskSrc, tmp, 0, 255, NormType.MinMax, DepthType.Cv8U);
                            tmp.CopyTo(grayMask);
                        }
                    }
                    else
                    {
                        // Convertir en niveaux de gris
                        using var grayAnyDepth = new Mat();
                        CvInvoke.CvtColor(maskSrc, grayAnyDepth, ColorConversion.Bgr2Gray); // supposition BGR par défaut
                        if (grayAnyDepth.Depth == DepthType.Cv8U)
                        {
                            grayAnyDepth.CopyTo(grayMask);
                        }
                        else
                        {
                            using var tmp = new Mat();
                            CvInvoke.Normalize(grayAnyDepth, tmp, 0, 255, NormType.MinMax, DepthType.Cv8U);
                            tmp.CopyTo(grayMask);
                        }
                    }

                    // 2) Redimensionner en "Nearest" pour préserver 0/255
                    using var resizedMask = new Mat();
                    CvInvoke.Resize(
                        grayMask,
                        resizedMask,
                        new System.Drawing.Size(projet.PictureWidth, projet.PictureHeight),
                        0, 0,
                        Inter.Nearest
                    );

                    // 3) Sauvegarder un masque simple noir/blanc sans alpha.
                    // Un PNG avec alpha peut paraître blanc partout dans certains viewers
                    // et peut être relu comme blanc partout si l'alpha est ignoré.
                    using var binaryMask = CreateBinaryMaskWithInset(resizedMask);
                    int nonZero = CvInvoke.CountNonZero(binaryMask);
                    AppendTextToConsoleNL($"SaveMask: pixels masque={nonZero}/{binaryMask.Rows * binaryMask.Cols}");

                    CvInvoke.Imwrite(outputPathPng, binaryMask);
                });

                AppendTextToConsoleNL($"Masque PNG sauvegardé (noir/blanc): {outputPathPng}");
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("ERREUR (PNG masque):");
                AppendTextToConsoleNL(ex.Message);
            }
        }

        private async Task SaveDisplayedMaskAsPngAsync(string outputPathPng)
        {
            Bitmap displayedMask;

            if (picBox_liveMaskLum.InvokeRequired)
            {
                displayedMask = (Bitmap)picBox_liveMaskLum.Invoke(new Func<Bitmap>(() =>
                {
                    if (picBox_liveMaskLum.Image == null)
                        return null;

                    return new Bitmap(picBox_liveMaskLum.Image);
                }));
            }
            else
            {
                if (picBox_liveMaskLum.Image == null)
                {
                    AppendTextToConsoleNL("Aucun masque affiché dans picBox_liveMaskLum. Sauvegarde du masque ignorée.");
                    return;
                }

                displayedMask = new Bitmap(picBox_liveMaskLum.Image);
            }

            if (displayedMask == null)
            {
                AppendTextToConsoleNL("Aucun masque affiché dans picBox_liveMaskLum. Sauvegarde du masque ignorée.");
                return;
            }

            using (displayedMask)
            using (var maskImage = displayedMask.ToImage<Gray, byte>())
            using (var maskMat = maskImage.Mat.Clone())
            {
                await SaveMaskAsPngTransparentBlack(maskMat, outputPathPng);
            }

            AppendTextToConsoleNL("Masque sauvegardé depuis picBox_liveMaskLum : " + outputPathPng);
        }


        private async Task ManualFocusAsync(int up, double newFocusValue)
        {
            await RunExclusiveNikonOperationAsync(() =>
            {
                driveStep.Value = newFocusValue;
                device.SetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep, driveStep);

                try
                {
                    if (up == 1)
                    {
                        device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_ClosestToInfinity);
                    }
                    else
                    {
                        device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_InfinityToClosest);
                    }
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL(ex.Message);
                    throw;
                }

                return Task.CompletedTask;
            });
        }

        private void device_CaptureComplete(NikonDevice sender, int data)
        {
            AppendTextToConsoleNL($"device_CaptureComplete: data={data}");
            captureCompleteTcs?.TrySetResult(data);
        }


    }


    public class Timing
    {
        private System.Timers.Timer? _timer;
        private Stopwatch _stopwatch = new();

        public TimeSpan ElapsedTime => _stopwatch.Elapsed;

        public void StartTimer()
        {
            _stopwatch = Stopwatch.StartNew();

            _timer = new System.Timers.Timer(500);
            _timer.AutoReset = true;
            _timer.Start();
        }

        public void StopTimer()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            _stopwatch.Stop();
        }


    }



}
