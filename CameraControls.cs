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

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private FlowLayoutPanel currentSequenceFlowLayoutPanel;
        private int currentSequence = 0;
        private TaskCompletionSource<bool> imageReadyTcs;
        private int lastPercent = -1;
        private Size panelSize = new Size(250, 200);
        

        public async Task takePictureAsync()
        {
            //AppendTextToConsoleNL("prise de photo");
            imageReadyTcs = new TaskCompletionSource<bool>();
            try
            {
                //AppendTextToConsoleNL("Prise de photo");
                device.Capture();
                AppendTextToConsoleNL("Photo prise ... en traitement");
            }
            catch (Exception e)
            {
                //AppendTextToConsoleNL("takePictureAsync Error message: " +e.Message);
                return;
            }

            //AppendTextToConsoleNL("ici");
            //MessageBox.Show("image capturée");
        }

        void device_ImageReady(NikonDevice sender, NikonImage image)
        {
            //CustomFlowLayoutPanel[] flowLayoutPanels = { customFlowLayoutPanel1, customFlowLayoutPanel1, customFlowLayoutPanel1 };
           
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
                                                lastPercent = percent; // Update the last percentage value
                                            }

                                        });
                                        SaveStreamAsJpegWithProgress(memoryStream, outputPath, savingProgress, true);

                                        try
                                        {
                                            string imagePath = outputPath;

                                            using (Image originalImage = Image.FromFile(imagePath))
                                            {
                                                Image resizedImage = ResizeImage(originalImage, 150,100); // Resize to desired dimensions


                                                Panel borderPanel = new Panel();
                                                borderPanel.Size = panelSize; // Adjust size as needed
                                                borderPanel.BorderStyle = BorderStyle.FixedSingle; // This adds the border

                                                // Create a panel to hold the label and picture box
                                                TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();

                                                tableLayoutPanel.ColumnCount = 1;
                                                tableLayoutPanel.RowCount = 2;
                                                tableLayoutPanel.Dock = DockStyle.Fill;
                                                tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
                                                tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
                                                


                                                // Create and configure the label
                                                Label label = new Label();
                                                label.Text = nomImage;
                                                //label.Size = new Size(150, 30); // Adjust size as needed
                                                label.TextAlign = ContentAlignment.MiddleCenter;
                                                label.ForeColor = Color.White;
                                                label.Dock = DockStyle.Fill;
                                                label.Font = new Font(label.Font.FontFamily, 6);

                                                // Create and configure the picture box
                                                PictureBox pictureBox = new PictureBox();
                                                pictureBox.Image = resizedImage;
                                                //pictureBox.Size = new Size(150, 100);
                                                pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                                                pictureBox.Dock = DockStyle.Fill;
                                                ToolTip toolTip = new ToolTip();
                                                toolTip.SetToolTip(pictureBox, nomImage);

                                                pictureBox.Click += (sender, e) =>
                                                {
                                                    try
                                                    {
                                                        Process.Start("explorer.exe", $"/select,\"{imagePath}\"");
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        MessageBox.Show($"Failed to open image: {ex.Message}");
                                                    }
                                                };

                                                // Add the label and picture box to the panel

                                                tableLayoutPanel.Controls.Add( label, 0,0 );
                                                tableLayoutPanel.Controls.Add( pictureBox, 0, 1 );
                                                borderPanel.Controls.Add(tableLayoutPanel);

                                                // Add the panel to the flow layout panel
                                                flowLayoutPanel1.Controls.Add(borderPanel);
                                                flowLayoutPanel1.ScrollControlIntoView(borderPanel);
                                            }
                                        }


                                        catch (Exception ex)
                                        {
                                            //AppendTextToConsoleNL($"An error occurred: {ex.Message}");
                                        }
                                    }

                                    catch (Exception ex)
                                    {
                                        //AppendTextToConsoleNL("NikonException: " + ex.Message);
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




        public async Task SaveStreamAsJpegWithProgress(Stream imageStream, string outputPath, IProgress<int>? progress, bool addBrush)
        {
            AppendTextToConsoleNL($"Saving image to {outputPath}");

            // Create an Image object from the stream
            Image image = Image.FromStream(imageStream);


            if (customPen.IsVisible & addBrush)
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

                        if (progress != null) {
                            // Report progress
                            int percentComplete = (int)((totalBytesRead * 100) / totalLength);
                            progress.Report(percentComplete);
                        }
                        
                    }
                }
            }
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
