using Nikon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private TaskCompletionSource<bool> imageReadyTcs;
        public async Task takePictureAsync()
        {
            imageReadyTcs = new TaskCompletionSource<bool>();
            await Task.Run(() => device.Capture());
        }

        private FlowLayoutPanel currentSequenceFlowLayoutPanel;
        private int currentSequence = 1;

        void device_ImageReady(NikonDevice sender, NikonImage image)
        {
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

                        // Calculate the position for the new PictureBox
                        int pictureBoxWidth = 200;
                        int pictureBoxHeight = 132;

                        PictureBox pictureBox = new PictureBox
                        {
                            Name = $"img_{flowLayoutPanel1.Controls.Count:D3}",
                            Image = resizedImage,
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Width = pictureBoxWidth,
                            Height = pictureBoxHeight,
                            Margin = new Padding(0), // Increase margin to add more space between PictureBoxes
                            Padding = new Padding(0), // Set padding to zero
                            BorderStyle = BorderStyle.FixedSingle // Adjust border style as needed
                        };

                        // Set border color based on the sequence
                        Color borderColor = Color.Transparent;
                        switch (currentSequence)
                        {
                            case 1:
                                borderColor = Color.Green;
                                break;
                            case 2:
                                borderColor = Color.Orange;
                                break;
                            case 3:
                                borderColor = Color.Purple;
                                break;
                        }

                        //// Create a new sequence FlowLayoutPanel if needed
                        //if (currentSequenceFlowLayoutPanel == null || currentSequenceFlowLayoutPanel.BackColor != borderColor)
                        //{
                        //    currentSequenceFlowLayoutPanel = CreateSequenceFlowLayoutPanel(borderColor);
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
                        // Create a new sequence FlowLayoutPanel if needed
                        if (currentSequenceFlowLayoutPanel == null || currentSequenceFlowLayoutPanel.BackColor != borderColor)
                        {
                            currentSequenceFlowLayoutPanel = CreateSequenceFlowLayoutPanel(borderColor);
                            flowLayoutPanel1.Invoke((MethodInvoker)delegate
                            {
                                flowLayoutPanel1.Controls.Add(currentSequenceFlowLayoutPanel);
                            });
                        }

                        // Add the PictureBox to the current sequence FlowLayoutPanel
                        currentSequenceFlowLayoutPanel.Invoke((MethodInvoker)delegate
                        {
                            currentSequenceFlowLayoutPanel.Controls.Add(pictureBox);

                            // Calculate the total width of the PictureBox controls
                            int totalWidth = 0;
                            foreach (Control control in currentSequenceFlowLayoutPanel.Controls)
                            {
                                totalWidth += control.Width;
                            }

                            // Adjust the width of the FlowLayoutPanel if needed
                            if (totalWidth > currentSequenceFlowLayoutPanel.Width)
                            {
                                currentSequenceFlowLayoutPanel.Width = totalWidth;
                            }
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




    }
}
