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

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private FlowLayoutPanel currentSequenceFlowLayoutPanel;
        private int currentSequence = 0;
        private TaskCompletionSource<bool> imageReadyTcs;
        private int lastPercent = -1;
        

        public async Task takePictureAsync()
        {
            //AppendTextToConsoleNL("prise de photo");
            imageReadyTcs = new TaskCompletionSource<bool>();
            try
            {
                AppendTextToConsoleNL("device capture");
                device.Capture();
                AppendTextToConsoleNL("capture done");
            }
            catch (Exception e)
            {
                AppendTextToConsoleNL("takePictureAsync Error message: " +e.Message);
                return;
            }

            //AppendTextToConsoleNL("ici");
            //MessageBox.Show("image capturée");
        }

        void device_ImageReady(NikonDevice sender, NikonImage image)
        {
            FlowLayoutPanel[] flowLatoutPanels = { flowLayoutPanel1, flowLayoutPanel2, flowLayoutPanel3 };
           
            AppendTextToConsoleNL("image capturée, sauvegarde de l'image");
            try
            {
                if (image.Type == NikonImageType.Jpeg)
                {
                    using (MemoryStream memoryStream = new MemoryStream(image.Buffer))
                    {
                        string nomImage = "";
                        try
                        {
                            
                            // Test if the image buffer is valid
                            Image testImage = Image.FromStream(memoryStream);
                            // Save image if needed
                            if (chkBox_savePicture.Checked)
                            {
                                if (imagesFolderPath != null && imageNameBase!= null && imageIncr!= null)
                                {
                                    try

                                    {
                                        //MessageBox.Show(imageNameBase + "_" + imageIncr + ".jpg");
                                        // Reset the stream position to the beginning
                                        memoryStream.Position = 0;
                                        nomImage = imageNameBase + "_" + imageIncr + ".jpg";
                                        string outputPath = Path.Combine(imagesFolderPath, nomImage);
                                        //MessageBox.Show(outputPath);
                                        AppendTextToConsoleNL("Sauvegarde de la photo. Ceci peut prendre quelques secondes");
                                        //SaveStreamAsJpeg(memoryStream, outputPath);
                                        var savingProgress = new Progress<int>(percent =>
                                        {
                                            if (percent != lastPercent)
                                            {
                                                AppendTextToConsoleNL($"Sauvegarde: {percent}%");
                                                Lbl_SaveProgress.Text = $"Sauvegarde: {percent}%";
                                                lastPercent = percent; // Update the last percentage value

                                            }
                                        });

                                        SaveStreamAsJpegWithProgress(memoryStream, outputPath, savingProgress);
                                        Lbl_SaveProgress.Text = "";

                                        try
                                        {
                                            string imagePath = outputPath;

                                            using (Image originalImage = Image.FromFile(imagePath))
                                            {
                                                Image resizedImage = ResizeImage(originalImage, (int)(flowLayoutPanel1.Height*1.5), flowLayoutPanel1.Height - 30); // Resize to desired dimensions

                                                PictureBox pictureBox = new PictureBox();
                                                pictureBox.Image = resizedImage;
                                                pictureBox.Size = new Size((int)(flowLayoutPanel1.Height * 1.5), flowLayoutPanel1.Height);
                                                pictureBox.SizeMode = PictureBoxSizeMode.Zoom;

                                                flowLatoutPanels[currentSequence].Controls.Add(pictureBox);
                                            }

                                        }

                                        catch (Exception ex)
                                        {
                                            AppendTextToConsoleNL($"An error occurred: {ex.Message}");
                                        }


                                        // Reset the memory stream position after testing



                                        //memoryStream.Position = 0;
                                        //Image resizedImage = ImageResizer.DownsizeImageToFitPictureBox(picBox_pictureTaken, memoryStream);
                                        //if (resizedImage != null)
                                        //{
                                        //    picBox_pictureTaken.Image = resizedImage;
                                        //    //// Create a new PictureBox
                                        //    PictureBox pictureBox = CreatePictureBox(flowLatoutPanels[currentSequence], resizedImage);

                                        //    // Set border color based on the sequence
                                        //    Color borderColor = GetBorderColor(currentSequence);

                                        //    // Create a ToolTip instance
                                        //    ToolTip toolTip = new ToolTip();

                                        //    // Set the tooltip text
                                        //    toolTip.SetToolTip(pictureBox, nomImage);


                                        //    flowLatoutPanels[currentSequence].Controls.Add(pictureBox);



                                        //}
                                        //else
                                        //{
                                        //    MessageBox.Show("Failed to resize the image.");
                                        //    return;
                                        //}






                                    }

                                    catch (Exception ex)
                                    {
                                        AppendTextToConsoleNL("NikonException: " + ex.Message);
                                        throw;

                                    }
                                }

                                else
                                {
                                    MessageBox.Show("Il faut sélectionner un dossier d'images");
                                    return;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Invalid image buffer: " + ex.Message);
                            return;
                        }

                        


                        // Downsize and display the image in a PictureBox
                        
                        //var resizingProgress = new Progress<int>(percent =>
                        //{
                        //    AppendTextToConsoleNL($"Progress: {percent}%");
                        //});

                        //var resizedImage = ImageResizer.DownsizeImageToFitPictureBox(picBox_pictureTaken, memoryStream, resizingProgress);

                       


                        //// Create a new sequence FlowLayoutPanel if needed
                        //if (currentSequenceFlowLayoutPanel == null || currentSequenceFlowLayoutPanel.BackColor != borderColor)
                        //{
                        //    int seq1 = int.Parse(txtBox_nbrImg5deg.Text);
                        //    int seq2 = int.Parse(txtBox_nbrImg25deg.Text);
                        //    int seq3 = int.Parse(txtBox_nbrImg45deg.Text);

                        //    int maxSeq = Math.Max(seq1, Math.Max(seq2, seq3));

                        //    currentSequenceFlowLayoutPanel = CreateSequenceFlowLayoutPanel(borderColor);
                        //    SetFlowLayoutPanelWidth(currentSequenceFlowLayoutPanel, maxSeq, 200);
                        //    SetFlowLayoutPanelWidth(flowLayoutPanel1, maxSeq, 200);
                        //    flowLayoutPanel1.Invoke((MethodInvoker)delegate
                        //    {
                        //        flowLayoutPanel1.Controls.Add(currentSequenceFlowLayoutPanel);
                        //    });
                        //}


                        // Add the PictureBox to the current sequence FlowLayoutPanel
                        //currentSequenceFlowLayoutPanel.Invoke((MethodInvoker)delegate
                        //{
                        //    currentSequenceFlowLayoutPanel.Controls.Add(pictureBox);
                        //});

                        // Signal that the image is ready
                        imageReadyTcs?.TrySetResult(true);

                       
                    }
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




        public void SaveStreamAsJpegWithProgress(Stream imageStream, string outputPath, IProgress<int> progress)
        {
            AppendTextToConsoleNL($"Saving image to {outputPath}");

            // Create an Image object from the stream
            Image image = Image.FromStream(imageStream);

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

                        // Report progress
                        int percentComplete = (int)((totalBytesRead * 100) / totalLength);
                        progress.Report(percentComplete);
                    }
                }
            }
        }


        private PictureBox CreatePictureBox(FlowLayoutPanel flowlayoutpanel, Image image)
        {
            return new PictureBox
            {
                Name = $"img_{flowlayoutpanel.Controls.Count:D3}",
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                Height = flowlayoutpanel.Height,
                Width = (int)(Height / 1.5),
                Margin = new Padding(0),
                Padding = new Padding(0),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private Color GetBorderColor(int sequence)
        {
            return sequence switch
            {
                1 => Color.Green,
                2 => Color.Orange,
                3 => Color.Purple,
                _ => Color.Transparent
            };
        }
        private void SetFlowLayoutPanelWidth(FlowLayoutPanel panel, int nombreImages, int pictureBoxWidth)
        {
            // Calculate the total width needed for the FlowLayoutPanel
            int totalWidth = nombreImages * pictureBoxWidth;

            // Set the width of the FlowLayoutPanel
            panel.Width = totalWidth;
        }


        private void ManualFocus(int up, double newFocusValue){

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
        //    int maxStep = int.Parse(lbl_driveStepMax.Text);
        //    int driveStepVal = 30; // Initial drive step value (never zero)
        //    int direction = 1; // Initial direction (1 for one way, 0 for the other)
        //    int moveCount = 0; // Counter for moves in the current direction
        //    double highestBlurryness = 0.0; // Track the highest blurryness amount
        //    int bestStep = 0; // Track the best step position
        //    int initialPosition = 0; // Assume initial position is 0
        //    double initialBlurryness = blurrynessAmount;
        //    int moveBadCount = 0;

        //    if (device.LiveViewEnabled == false)
        //    {
        //        device.LiveViewEnabled = true;
        //        liveViewTimer.Start();
        //    }
        //    AppendTextToConsoleNL("Début du programme de focus");
        //    // Move up by 100 steps
        //    while (moveCount < 50)
        //    {
        //        ManualFocus(direction, driveStepVal);
        //        await Task.Delay(100); // Adjust delay as needed to match live view timer interval
        //        double currentBlurryness = blurrynessAmount; // Assume blurrynessAmount is updated by liveViewTimer

        //        if (currentBlurryness > highestBlurryness)
        //        {
        //            highestBlurryness = currentBlurryness;
        //            bestStep = moveCount * driveStepVal;
        //        } 
        //        if (currentBlurryness < initialBlurryness)
        //        {
        //            if (moveBadCount > 4)
        //            {
        //                break;
        //            }
        //            moveBadCount++;
        //        }
        //        else
        //        {
        //            moveBadCount = 0; // Reset the counter if a good move is found
        //        }

        //        moveCount++;
        //    }
        //    AppendTextToConsoleNL("fin du premier tour");
        //    AppendTextToConsoleNL($"Flou Max: {highestBlurryness} et bestSetp: {bestStep}");



        //    // Return to the initial position

        //    ManualFocus(0, moveCount * driveStepVal);

        //    Task.Delay(1000);
        //    moveCount = 0;
        //    while (moveCount < 50)
        //    {
        //        ManualFocus(0, driveStepVal);
        //        await Task.Delay(100); // Adjust delay as needed to match live view timer interval
        //        double currentBlurryness = blurrynessAmount; // Assume blurrynessAmount is updated by liveViewTimer

        //        if (currentBlurryness > highestBlurryness)
        //        {
        //            highestBlurryness = currentBlurryness;
        //            bestStep = moveCount * driveStepVal;
        //            //AppendTextToConsoleNL($"Flou Max: {highestBlurryness} et bestSetp: {bestStep}");
        //        }

        //        moveCount++;
        //    }
        //    AppendTextToConsoleNL("Fin du deuxième tour");
        //    AppendTextToConsoleNL($"Flou Max: {highestBlurryness} et bestSetp: {bestStep}");

        //       //Back or origin in order to move to the highestBluryness amount 
        //    ManualFocus(1, moveCount * driveStepVal);

        //    //ManualFocus(1, bestStep);


        //}

        private async Task AutomaticMFocus()
        {
            int maxStep = int.Parse(lbl_driveStepMax.Text); // Define maxStep based on your device's maximum allowable steps
            int initialDriveStepVal = 200; // Larger initial drive step value
            int minDriveStepVal = 1; // Minimum drive step value for precise adjustments
            int driveStepVal = initialDriveStepVal; // Start with larger steps
            int direction = 1; // Initial direction (1 for forward, -1 for backward)
            double highestBlurryness = 0.0; // Track the highest blurryness amount
            int bestStep = 0; // Track the best step position
            int moveBadCount = 0;
            int currentStep = 0;
            int bufferSize = 5; // Size of the buffer to track recent blurryness values
            Queue<double> blurrynessBuffer = new Queue<double>();
            int iterationCount = 0; // Track the number of iterations
            int maxIterations = 100; // Set a maximum number of iterations to avoid infinite loop

            if (!device.LiveViewEnabled)
            {
                device.LiveViewEnabled = true;
                liveViewTimer.Start();
            }
            AppendTextToConsoleNL("Début du programme de focus");

            while (iterationCount < maxIterations)
            {
                ManualFocus(direction, driveStepVal);
                await Task.Delay(50); // Adjust delay to 50ms for quicker response
                double currentBlurryness = blurrynessAmount; // Assume blurrynessAmount is updated by liveViewTimer

                // Add current blurryness to buffer
                blurrynessBuffer.Enqueue(currentBlurryness);
                if (blurrynessBuffer.Count > bufferSize)
                {
                    blurrynessBuffer.Dequeue(); // Maintain buffer size
                }

                // Calculate average blurryness from buffer
                double averageBlurryness = blurrynessBuffer.Average();

                if (averageBlurryness > highestBlurryness)
                {
                    highestBlurryness = averageBlurryness;
                    bestStep = currentStep;
                }
                else if (averageBlurryness < highestBlurryness)
                {
                    direction *= -1; // Change direction when average blurryness decreases
                }

                if (averageBlurryness < highestBlurryness)
                {
                    if (moveBadCount > 10) // Adjust moveBadCount to 10
                    {
                        direction *= -1; // Change direction if too many bad moves
                        moveBadCount = 0; // Reset bad move counter
                    }
                    else
                    {
                        moveBadCount++;
                    }
                }
                else
                {
                    moveBadCount = 0; // Reset the counter if a good move is found
                }

                currentStep += direction * driveStepVal;

                // Gradually decrease the drive step value
                driveStepVal = Math.Max(minDriveStepVal, driveStepVal - 10);

                iterationCount++; // Increment iteration count

                // Break condition to avoid infinite loop
                if (Math.Abs(currentStep) > maxStep)
                {
                    break;
                }
            }
            AppendTextToConsoleNL("Fin du programme de focus");
            AppendTextToConsoleNL($"Flou Max: {highestBlurryness} et bestStep: {bestStep}");

            // Move to the position with the highest blurryness amount
            ManualFocus(direction, bestStep);
            await Task.Delay(1000); // Allow time for the move to complete

            // Verify the final position
            double finalBlurryness = blurrynessAmount;
            AppendTextToConsoleNL($"Final Blurryness: {finalBlurryness}");
            if (finalBlurryness < highestBlurryness)
            {
                AppendTextToConsoleNL("Adjusting to the best step position");
                ManualFocus(direction, bestStep - currentStep); // Adjust to the best step position
            }
        }





       



    }
}
