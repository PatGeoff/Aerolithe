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
        private int currentSequence = 1;
        private TaskCompletionSource<bool> imageReadyTcs;
        
        
        public async Task takePictureAsync()
        {
            //AppendTextToConsoleNL("prise de photo");
            imageReadyTcs = new TaskCompletionSource<bool>();
            try
            {
                await Task.Run(() => device.Capture());
            }
            catch (Exception e)
            {
                AppendTextToConsoleNL(e.Message);
                return;
            }
            
            
            //MessageBox.Show("image capturée");
        }

        void device_ImageReady(NikonDevice sender, NikonImage image)
        {
            AppendTextToConsoleNL("image capturée, sauvegarde de l'image");
            try
            {
                if (image.Type == NikonImageType.Jpeg)
                {
                    using (MemoryStream memoryStream = new MemoryStream(image.Buffer))
                    {
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
                                        string nomImage = imageNameBase + "_" + imageIncr + ".jpg";
                                        string outputPath = Path.Combine(imagesFolderPath, nomImage);
                                        //MessageBox.Show(outputPath);
                                        AppendTextToConsoleNL("Sauvegarde de la photo. Ceci peut prendre quelques secondes");
                                        SaveStreamAsJpeg(memoryStream, outputPath);

                                    }
                                    catch (Exception)
                                    {
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

                        // Reset the memory stream position after testing
                        memoryStream.Position = 0;
                       
                       
                        // Downsize and display the image in a PictureBox
                        var resizedImage = ImageResizer.DownsizeImageToFitPictureBox(picBox_pictureTaken, memoryStream);

                        if (resizedImage != null)
                        {
                            picBox_pictureTaken.Image = resizedImage;
                        }
                        else
                        {
                            MessageBox.Show("Failed to resize the image.");
                            return;
                        }

                        // Create a new PictureBox
                        PictureBox pictureBox = CreatePictureBox(resizedImage);

                        // Set border color based on the sequence
                        Color borderColor = GetBorderColor(currentSequence);

                        // Create a new sequence FlowLayoutPanel if needed
                        if (currentSequenceFlowLayoutPanel == null || currentSequenceFlowLayoutPanel.BackColor != borderColor)
                        {
                            int seq1 = int.Parse(txtBox_nbrImg5deg.Text);
                            int seq2 = int.Parse(txtBox_nbrImg25deg.Text);
                            int seq3 = int.Parse(txtBox_nbrImg45deg.Text);

                            int maxSeq = Math.Max(seq1, Math.Max(seq2, seq3));

                            currentSequenceFlowLayoutPanel = CreateSequenceFlowLayoutPanel(borderColor);
                            SetFlowLayoutPanelWidth(currentSequenceFlowLayoutPanel, maxSeq, 200);
                            SetFlowLayoutPanelWidth(flowLayoutPanel1, maxSeq, 200);
                            flowLayoutPanel1.Invoke((MethodInvoker)delegate
                            {
                                flowLayoutPanel1.Controls.Add(currentSequenceFlowLayoutPanel);
                            });
                        }


                        // Add the PictureBox to the current sequence FlowLayoutPanel
                        currentSequenceFlowLayoutPanel.Invoke((MethodInvoker)delegate
                        {
                            currentSequenceFlowLayoutPanel.Controls.Add(pictureBox);
                        });

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

    
        public async Task  SaveStreamAsJpeg(Stream imageStream, string outputPath)
        {
            AppendTextToConsoleNL($"sauvegarde de l'image dans {outputPath}");

            // Create an Image object from the stream
            Image image = Image.FromStream(imageStream);

            // Save the image as a JPEG file
            image.Save(outputPath, ImageFormat.Jpeg);
        }


        private PictureBox CreatePictureBox(Image image)
        {
            return new PictureBox
            {
                Name = $"img_{flowLayoutPanel1.Controls.Count:D3}",
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 200,
                Height = 132,
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
            if (up == 1)
            {
                //Drive focus towards infinity
                device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_ClosestToInfinity);
                AppendTextToConsoleNL($"setting Drive Step to newFocusValue = {newFocusValue.ToString()} with kNkMAIDMFDrive_ClosestToInfinity... oldFocusValue = {oldFocusValue.ToString()}");
            }
            else
            {
                device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_InfinityToClosest);
                AppendTextToConsoleNL($"setting Drive Step to newFocusValue = {newFocusValue.ToString()} with kNkMAIDMFDrive_InfinityToClosest... oldFocusValue = {oldFocusValue.ToString()}");
            }

        }

        private async Task AutomaticMFocus()
        {
            int maxStep = int.Parse(lbl_driveStepMax.Text);
            int driveStepVal = 5; // Initial drive step value (never zero)
            int direction = 1; // Initial direction (1 for one way, 0 for the other)
            int moveCount = 0; // Counter for moves in the current direction
            double highestBlurryness = double.MinValue; // Track the highest blurryness amount
            int bestStep = 0; // Track the best step position
            int initialPosition = 0; // Assume initial position is 0

            if (device.LiveViewEnabled == false)
            {
                device.LiveViewEnabled = true;
                liveViewTimer.Start();
            }

            while (moveCount < 100)
            {
                // Perform manual focus adjustment
                ManualFocus(direction, driveStepVal);

                // Wait for the live view timer to tick and calculate blurryness
                await Task.Delay(35); // Adjust delay as needed to match live view timer interval
                double currentBlurryness = blurrynessAmount; // Assume blurrynessAmount is updated by liveViewTimer

                if (currentBlurryness > highestBlurryness)
                {
                    // Update highest blurryness and best step position
                    highestBlurryness = currentBlurryness;
                    bestStep = moveCount * driveStepVal * (direction == 1 ? 1 : -1);
                }

                moveCount++;

                if (moveCount == 100)
                {
                    // Return to the initial position
                    ManualFocus(direction == 1 ? 0 : 1, moveCount * driveStepVal);
                    moveCount = 0;
                    direction = direction == 1 ? 0 : 1; // Change direction
                }

                // Ensure driveStepVal is never zero
                driveStepVal = Math.Max(1, driveStepVal - 1);
            }

            // Move to the best step position
            ManualFocus(bestStep > 0 ? 1 : 0, Math.Abs(bestStep));

            //liveViewTimer.Stop();
        }


    }
}
