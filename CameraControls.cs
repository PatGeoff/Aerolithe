using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
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
        private FlowLayoutPanel currentSequenceFlowLayoutPanel;
        private TaskCompletionSource<bool> imageReadyTcs;
        private TaskCompletionSource<bool> miniaturesTcs;
        private Size panelSize = new Size(250, 200);



        public static Task InvokeOnUIAsync(Control control, Func<Task> action)
        {
            if (control.InvokeRequired)
            {
                var tcs = new TaskCompletionSource();

                control.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await action();
                        tcs.SetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));

                return tcs.Task;
            }
            else
            {
                return action();
            }
        }


        private async void takePictureAsyncSimple()
        {
            miniaturesTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            AppendTextToConsoleNL($"[Thread takePictureAsyncSimple] Invoke Required Thread# {Thread.CurrentThread.ManagedThreadId} -> is Thread same as UI? {(!this.InvokeRequired).ToString()}");

            Stopwatch sw = Stopwatch.StartNew();
            await takePictureAsync();  // attend que imageReadyTcs soit résolu   
            sw.Stop();
            string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");
            AppendTextToConsoleNL($"photo prise en {tempsMs} secondes");
        }

        public async Task takePictureAsync()
        {

            timing.StartTimer();

            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        AppendTextToConsoleNL($"[Thread takePictureAsync] Invoke Required Thread# {Thread.CurrentThread.ManagedThreadId} -> is Thread same as UI? {(!this.InvokeRequired).ToString()}");
                        try
                        {
                            device.Capture();
                        }
                        catch (Exception)
                        {
                            AppendTextToConsoleNL("La Nikon n'est pas active");
                        }
                        
                    }));
                }
                else
                {
                    try
                    {
                        device.Capture();
                        AppendTextToConsoleNL($"[Thread takePictureAsync] no Invoke Required Thread# {Thread.CurrentThread.ManagedThreadId} -> is Thread same as UI? {(!this.InvokeRequired).ToString()}");

                    }
                    catch (Exception)
                    {
                        AppendTextToConsoleNL("La Nikon n'est pas active");
                    }
                }

;
                AppendTextToConsoleNL("Capture de l'image par la Nikon ...");
                //AppendTextToConsoleNL(projet.GetImageFullPath());
                
            }
            catch (NikonException ex)
            {
                if (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                {
                    AppendTextToConsoleNL(ex.Message);
                    await Task.Delay(200); // Wait before retrying
                }
                throw;
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


                if (projet.SaveImageToDisk && (
                    projet.ImageFolderPath == null
                    || projet.ImageNameBase == null
                    || projet.GetMesurementsFullImagePath() == null
                    || projet.FocusStackFolderName == null))
                {
                    AppendTextToConsoleNL($"Un des dossiers n'existe pas:" +
                        $"\nprojet.ImageFolderPath = {projet.ImageFolderPath}" +
                        $"\nprojet.ImageNameBase = {projet.ImageNameBase}" +
                        $"\nprojet.MesurementsFolderPath = {projet.GetMesurementsFullImagePath()}" +
                        $"\nprojet.FocusStackFolderName = {projet.FocusStackFolderName}" +
                        $"\net Finalement projet.SaveImageToDisk = {projet.SaveImageToDisk}"
                        );
                    return; // ← quitte device_ImageReady
                }


                Bitmap finalBitmap = null;

                try
                {
                    finalBitmap = await Task.Run(() =>
                    {
                        using (var memoryStream = new MemoryStream(image.Buffer))
                        using (var originalBitmap = new Bitmap(memoryStream))
                        {
                            projet.PictureWidth = originalBitmap.Width;
                            projet.PictureHeight = originalBitmap.Height;
                            projet.Save(appSettings.ProjectPath); // -- <


                            var processedBitmap = projet.ApplyMask ? ApplyMask(originalBitmap) : new Bitmap(originalBitmap);

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


                }
                catch (Exception ex)
                {
                    Invoke(() => MessageBox.Show("Erreur lors du traitement de l'image : " + ex.Message));
                }
            }
            catch (Exception ex)
            {
                Invoke(() => MessageBox.Show("device_ImageReady exception: " + ex.Message));
                imageReadyTcs?.TrySetException(ex);
            }

           
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
        private Bitmap ApplyMask(Bitmap originalBitmap)
        {
            var sourceImage = originalBitmap.ToImage<Bgr, byte>();
            //var maskGray = maskBitmapLive.ToImage<Gray, byte>();
            var maskGray = maskMatLive.ToImage<Gray, byte>();
            var resizedMask = maskGray.Resize(projet.PictureWidth, projet.PictureHeight, Emgu.CV.CvEnum.Inter.Linear);

            //var invertedMask = resizedMask.Not();
            var invertedMask = resizedMask;
            var maskBgr = invertedMask.Convert<Bgr, byte>();

            sourceImage._And(maskBgr);
            var finalBitmap = sourceImage.ToBitmap();

            // Libération
            maskGray.Dispose();
            resizedMask.Dispose();
            invertedMask.Dispose();
            maskBgr.Dispose();
            sourceImage.Dispose();

            return finalBitmap;
        }

        public async Task SaveStreamAsJpegWithProgress(Stream imageStream, string outputPath)
        {
            // Create an Image object from the stream
            Image image = System.Drawing.Image.FromStream(imageStream);

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
                await InvokeOnUIAsync(this, takePictureAsync);

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
            }

            AppendTextToConsoleNL(timing.ElapsedTime.TotalSeconds.ToString("F2"));

            //  await saveImageForMesurementRemettre(); est effectué dans affichierMiniatures

        }

        public async Task SaveMaskAsPngTransparentBlack(Mat maskSrc, string outputPathPng)
        {
            if (maskSrc == null || maskSrc.IsEmpty)
            {
                AppendTextToConsoleNL("ERREUR (PNG transparent): Mat source nul ou vide.");
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

                    // Optionnel : binariser si tu veux forcer à {0,255} (décommenter si nécessaire)
                    // CvInvoke.Threshold(grayMask, grayMask, 127, 255, ThresholdType.Binary);

                    // 2) Redimensionner en "Nearest" pour préserver 0/255
                    using var resizedMask = new Mat();
                    CvInvoke.Resize(
                        grayMask,
                        resizedMask,
                        new System.Drawing.Size(projet.PictureWidth, projet.PictureHeight),
                        0, 0,
                        Inter.Nearest
                    );

                    // 3) Construire l'image BGRA :
                    //    - RGB = 255 (blanc) pour les pixels opaques
                    //    - A   = masque (0 = transparent, 255 = opaque)
                    using var whiteBgr = new Image<Bgr, byte>(
                        resizedMask.Cols, resizedMask.Rows,
                        new Bgr(255, 255, 255)
                    );

                    using var bgrMat = whiteBgr.Mat;
                    using var bgra = new Mat();

                    // Split BGR
                    var bgrChannels = bgrMat.Split(); // [0]=B, [1]=G, [2]=R

                    try
                    {
                        // S'assurer que l'alpha est 8UC1
                        using var alpha = new Mat();
                        if (resizedMask.Depth == DepthType.Cv8U && resizedMask.NumberOfChannels == 1)
                        {
                            resizedMask.CopyTo(alpha);
                        }
                        else
                        {
                            CvInvoke.Normalize(resizedMask, alpha, 0, 255, NormType.MinMax, DepthType.Cv8U);
                        }

                        // Merge en BGRA (4 canaux)
                        using var vm = new VectorOfMat(bgrChannels[0], bgrChannels[1], bgrChannels[2], alpha);
                        CvInvoke.Merge(vm, bgra);

                        // 4) Sauvegarde en .png (préserve l'alpha)
                        CvInvoke.Imwrite(outputPathPng, bgra);
                    }
                    finally
                    {
                        foreach (var ch in bgrChannels) ch.Dispose();
                    }
                });

                AppendTextToConsoleNL($"Masque PNG sauvegardé (noir transparent): {outputPathPng}");
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("ERREUR (PNG transparent):");
                AppendTextToConsoleNL(ex.Message);
            }
        }


        private void ManualFocus(int up, double newFocusValue)
        {

            driveStep.Value = newFocusValue;
            device.SetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep, driveStep);
            try
            {
                if (up == 1)
                {
                    //Drive focus towards infinity
                    device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_ClosestToInfinity);
                    //AppendTextToConsoleNL($"setting Drive Step to newFocusValue = {newFocusValue.ToString()} with kNkMAIDMFDrive_ClosestToInfinity... oldFocusValue = {oldFocusValue.ToString()}");
                }
                else
                {
                    device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_InfinityToClosest);
                    //AppendTextToConsoleNL($"setting Drive Step to newFocusValue = {newFocusValue.ToString()} with kNkMAIDMFDrive_InfinityToClosest... oldFocusValue = {oldFocusValue.ToString()}");
                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL(ex.Message);
            }
        }


    }


    public class Timing
    {
        private System.Timers.Timer _timer;
        private Stopwatch _stopwatch;

        public TimeSpan ElapsedTime => _stopwatch?.Elapsed ?? TimeSpan.Zero;

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

            _stopwatch?.Stop();
        }


    }



}
