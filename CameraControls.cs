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

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private TaskCompletionSource<bool> imageReadyTcs;
        public async Task takePictureAsync()
        {
            imageReadyTcs = new TaskCompletionSource<bool>();
            await Task.Run(() => device.Capture());
            //MessageBox.Show("image capturée");
        }

        private FlowLayoutPanel currentSequenceFlowLayoutPanel;
        private int currentSequence = 1;

        void device_ImageReady(NikonDevice sender, NikonImage image)
        {
            //MessageBox.Show("ici");
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

                        //// Create a new sequence FlowLayoutPanel if needed
                        //if (currentSequenceFlowLayoutPanel == null || currentSequenceFlowLayoutPanel.BackColor != borderColor)
                        //{
                        //    currentSequenceFlowLayoutPanel = CreateSequenceFlowLayoutPanel(borderColor);
                        //    SetFlowLayoutPanelWidth(currentSequenceFlowLayoutPanel, nombreImages5Degres, 200);
                        //    SetFlowLayoutPanelWidth(flowLayoutPanel1, nombreImages5Degres, 200);
                        //    flowLayoutPanel1.Invoke((MethodInvoker)delegate
                        //    {
                        //        flowLayoutPanel1.Controls.Add(currentSequenceFlowLayoutPanel);
                        //    });
                        //}
                                               

                        //// Add the PictureBox to the current sequence FlowLayoutPanel
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


        private void ManualFocus(double newFocusValue){

            driveStep.Value = newFocusValue;
            if (driveStep.Value > 0 && driveStep.Value < trkBar_focus.Maximum)
            {
                device.SetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep, driveStep);
                // Drive focus based on the direction
                if (newFocusValue > oldFocusValue)
                {
                    // Drive focus towards infinity
                    device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_ClosestToInfinity);
                    AppendTextToConsoleNL($"setting Drive Step to newFocusValue = {newFocusValue.ToString()} with kNkMAIDMFDrive_ClosestToInfinity... oldFocusValue = {oldFocusValue.ToString()}");
                }
                else
                {
                    // Drive focus towards close

                    device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_InfinityToClosest);
                    AppendTextToConsoleNL($"setting Drive Step to newFocusValue = {newFocusValue.ToString()} with kNkMAIDMFDrive_InfinityToClosest... oldFocusValue = {oldFocusValue.ToString()}");
                }

                // Update old focus value
                oldFocusValue = newFocusValue;
            }
            
        }
    }
}
