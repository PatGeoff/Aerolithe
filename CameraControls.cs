using Nikon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV;
using System.Drawing.Imaging;
using Emgu.CV.Util;
using System.Diagnostics;
using System.Drawing.Text;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private FlowLayoutPanel currentSequenceFlowLayoutPanel;
        private int currentSequence = 0;
        private TaskCompletionSource<bool> imageReadyTcs;
        private int lastPercent = -1;
        private Size panelSize = new Size(250, 200);
        private int oldImgIncr = -1;


        public async Task takePictureAsync()
        {
            //Initialize the TaskCompletionSource for each image capture
            imageReadyTcs = new TaskCompletionSource<bool>();

            if (picBox_holdOn.InvokeRequired || progressBar_ImageSave.InvokeRequired)
            {
                picBox_holdOn.Invoke(new Action(() =>
                {
                    picBox_holdOn.Visible = true;
                    progressBar_ImageSave.Value = 0;
                }));
            }
            else
            {
                picBox_holdOn.Visible = true;
            }

            
            try
            {
                device.Capture();
                await imageReadyTcs.Task;
                AppendTextToConsoleNL("Photo prise ... en téléchargement vers le pc");
            }
            catch (Exception e)
            {
                AppendTextToConsoleNL("takePictureAsync Error message: " +e.Message);
                return;
            }
        }

        void device_ImageReady(NikonDevice sender, NikonImage image)
        {
            try
            {
                if (image.Type == NikonImageType.Jpeg)
                {
                   
                    AppendTextToConsoleNL("Image importée, traitement en cours...");
                    try
                    {
                        using (var memoryStream = new MemoryStream(image.Buffer))
                        using (var originalBitmap = new Bitmap(memoryStream))
                        {
                            Bitmap finalBitmap;

                            if (chkBox_applyMask.Checked)
                            {
                                var sourceImage = originalBitmap.ToImage<Bgr, byte>();
                                var maskGray = maskBitmapLive.ToImage<Gray, byte>();
                                //AppendTextToConsoleNL("Dimensions du LiveView (Masque): " + maskGray.Width.ToString() + " x " + maskGray.Height.ToString());
                                //AppendTextToConsoleNL("Dimensions de l'image capturée: " + sourceImage.Width.ToString() + " x " + sourceImage.Height.ToString());
                                var resizedMask = maskGray.Resize(sourceImage.Width, sourceImage.Height, Emgu.CV.CvEnum.Inter.Linear);
                               
                                var invertedMask = resizedMask.Not();
                                var maskBgr = invertedMask.Convert<Bgr, byte>();

                                sourceImage._And(maskBgr);
                                finalBitmap = sourceImage.ToBitmap();

                                // Libération manuelle
                                maskGray.Dispose();
                                resizedMask.Dispose();
                                invertedMask.Dispose();
                                maskBgr.Dispose();
                                sourceImage.Dispose();
                            }
                            else
                            {
                                finalBitmap = new Bitmap(originalBitmap);
                            }

                            picBox_pictureTaken.Image?.Dispose();
                            picBox_pictureTaken.Image = finalBitmap;



                            if (imageReadyTcs != null && !imageReadyTcs.Task.IsCompleted)
                            {
                                imageReadyTcs.SetResult(true);
                            }



                            // Sauvegarde si activée
                            //if (chkBox_savePicture.Checked && imagesFolderPath != null && imageNameBase != null && imageIncr != null)
                            if (chkBox_savePicture.Checked && imagesFolderPath != null && imageNameBase != null)
                            {

                                if (imageIncr == oldImgIncr)
                                {
                                    imageIncr++;
                                }
                                oldImgIncr = imageIncr;

                                string nomImage = imageNameBase + "_" + imageIncr + ".jpg";
                                string outputPath = Path.Combine(imagesFolderPath, nomImage);

                                AppendTextToConsoleNL("Sauvegarde de la photo. Ceci peut prendre quelques secondes...");

                                using (var saveStream = new MemoryStream())
                                {
                                    finalBitmap.Save(saveStream, ImageFormat.Jpeg);
                                    saveStream.Position = 0;

                                    var savingProgress = new Progress<int>(percent =>
                                    {
                                        if (percent != lastPercent)
                                        {
                                            Invoke((MethodInvoker)(() =>
                                            {
                                                progressBar_ImageSave.Value = percent;
                                            }));
                                            lastPercent = percent;
                                        }
                                    });
                                    SaveStreamAsJpegWithProgress(saveStream, outputPath, savingProgress, true).GetAwaiter().GetResult(); ;
                                   

                                    if (picBox_holdOn.InvokeRequired || progressBar_ImageSave.InvokeRequired)
                                    {
                                        picBox_holdOn.Invoke(new Action(() =>
                                        {
                                            picBox_holdOn.Visible = false;
                                            progressBar_ImageSave.Value = 0;
                                        }));
                                    }
                                    else
                                    {
                                        picBox_holdOn.Visible = false;
                                    }
                                }

                                // Affichage miniature
                                try
                                {
                                    using (Image originalImage = Image.FromFile(outputPath))
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
                                            BackColor = Color.Black,
                                            ForeColor = Color.White,
                                            Margin = new Padding(0),
                                            FlatStyle = FlatStyle.Flat
                                        };

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
                                                    string imagePath = Path.Combine(imagesFolderPath, nomImage);
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
                                            Text = nomImage,
                                            TextAlign = ContentAlignment.MiddleCenter,
                                            ForeColor = Color.White,
                                            Dock = DockStyle.Fill,
                                            Font = new Font(FontFamily.GenericSansSerif, 8)
                                        };

                                        PictureBox pictureBox = new PictureBox
                                        {
                                            Image = resizedImage,
                                            SizeMode = PictureBoxSizeMode.Zoom,
                                            Dock = DockStyle.Fill
                                        };

                                        new ToolTip().SetToolTip(pictureBox, nomImage);

                                        string imagePath = Path.Combine(imagesFolderPath, nomImage); // nomImage = label.Text


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
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AppendTextToConsoleNL($"Erreur lors de l'affichage miniature : {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Erreur lors du traitement de l'image : " + ex.Message);
                    }
                }
                else
                {
                    MessageBox.Show("Il y a une erreur, l'image doit être du type jpeg. Aller dans l'onglet Settings/Caméra/Type D'images et choisir Jpeg dans le menu déroulant");
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("device_ImageReady exception: " + ex.Message);
                imageReadyTcs?.TrySetException(ex);
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


        public async Task SaveStreamAsJpegWithProgress(Stream imageStream, string outputPath, IProgress<int>? progress, bool addBrush)
        {
            //AppendTextToConsoleNL($"Saving image to {outputPath}");

            // Create an Image object from the stream
            Image image = Image.FromStream(imageStream);


            if (customPen.IsVisible && addBrush)
            {

                // Create a Graphics object from the image
                using (Graphics g = Graphics.FromImage(image))
                {
                    // Calculate the scaling ratio
                    float scaleX = (float)image.Width / pnl_DrawingLiveView.Width;
                    float scaleY = (float)image.Height / pnl_DrawingLiveView.Height;

                    // Scale the brush drawing to the image ratio
                    int scaledY = (int)(startY * scaleY);
                    int scaledHeight = (int)((pnl_DrawingLiveView.Height - startY) * scaleY);

                    // Draw the brush on the image
                    using (Brush brush = new SolidBrush(Color.White)) // Zero transparency white brush
                    {
                        g.FillRectangle(brush, 0, scaledY, image.Width, scaledHeight);
                    }
                }

            }

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

                        if (progress != null)
                        {
                            // Report progress
                            int percentComplete = (int)((totalBytesRead * 100) / totalLength);
                            progress.Report(percentComplete);
                        }

                    }
                }
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
                // if (ex.Message Z= eNkMAIDResult.kNkMAIDResult_DeviceBusy) 
            }


        }



        //private async Task AutomaticMFocus()
        //{
        //    int maxStep = int.Parse(lbl_driveStepMax.Text); // Define maxStep based on your device's maximum allowable steps
        //    int initialDriveStepVal = 200; // Larger initial drive step value
        //    int minDriveStepVal = 1; // Minimum drive step value for precise adjustments
        //    int driveStepVal = initialDriveStepVal; // Start with larger steps
        //    int direction = 1; // Initial direction (1 for forward, -1 for backward)
        //    double highestBlurryness = 0.0; // Track the highest blurryness amount
        //    int bestStep = 0; // Track the best step position
        //    int moveBadCount = 0;
        //    int currentStep = 0;
        //    int bufferSize = 5; // Size of the buffer to track recent blurryness values
        //    Queue<double> blurrynessBuffer = new Queue<double>();
        //    int iterationCount = 0; // Track the number of iterations
        //    int maxIterations = 100; // Set a maximum number of iterations to avoid infinite loop

        //    if (!device.LiveViewEnabled)
        //    {
        //        device.LiveViewEnabled = true;
        //        liveViewTimer.Start();
        //    }
        //    AppendTextToConsoleNL("Début du programme de focus");

        //    while (iterationCount < maxIterations)
        //    {
        //        ManualFocus(direction, driveStepVal);
        //        await Task.Delay(50); // Adjust delay to 50ms for quicker response
        //        double currentBlurryness = blurrynessAmount; // Assume blurrynessAmount is updated by liveViewTimer

        //        // Add current blurryness to buffer
        //        blurrynessBuffer.Enqueue(currentBlurryness);
        //        if (blurrynessBuffer.Count > bufferSize)
        //        {
        //            blurrynessBuffer.Dequeue(); // Maintain buffer size
        //        }

        //        // Calculate average blurryness from buffer
        //        double averageBlurryness = blurrynessBuffer.Average();

        //        if (averageBlurryness > highestBlurryness)
        //        {
        //            highestBlurryness = averageBlurryness;
        //            bestStep = currentStep;
        //        }
        //        else if (averageBlurryness < highestBlurryness)
        //        {
        //            direction *= -1; // Change direction when average blurryness decreases
        //        }

        //        if (averageBlurryness < highestBlurryness)
        //        {
        //            if (moveBadCount > 10) // Adjust moveBadCount to 10
        //            {
        //                direction *= -1; // Change direction if too many bad moves
        //                moveBadCount = 0; // Reset bad move counter
        //            }
        //            else
        //            {
        //                moveBadCount++;
        //            }
        //        }
        //        else
        //        {
        //            moveBadCount = 0; // Reset the counter if a good move is found
        //        }

        //        currentStep += direction * driveStepVal;

        //        // Gradually decrease the drive step value
        //        driveStepVal = Math.Max(minDriveStepVal, driveStepVal - 10);

        //        iterationCount++; // Increment iteration count

        //        // Break condition to avoid infinite loop
        //        if (Math.Abs(currentStep) > maxStep)
        //        {
        //            break;
        //        }
        //    }
        //    AppendTextToConsoleNL("Fin du programme de focus");
        //    AppendTextToConsoleNL($"Flou Max: {highestBlurryness} et bestStep: {bestStep}");

        //    // Move to the position with the highest blurryness amount
        //    ManualFocus(direction, bestStep);
        //    await Task.Delay(1000); // Allow time for the move to complete

        //    // Verify the final position
        //    double finalBlurryness = blurrynessAmount;
        //    AppendTextToConsoleNL($"Final Blurryness: {finalBlurryness}");
        //    if (finalBlurryness < highestBlurryness)
        //    {
        //        AppendTextToConsoleNL("Adjusting to the best step position");
        //        ManualFocus(direction, bestStep - currentStep); // Adjust to the best step position
        //    }
        //}


        private async Task AutomaticMFocus()
        {
            int initialDriveStepVal = 200;
            if (!device.LiveViewEnabled)
            {
                device.LiveViewEnabled = true;
                liveViewTimer.Start();
            }
            AppendTextToConsoleNL("Début du programme de focus");
        }






    }
}
