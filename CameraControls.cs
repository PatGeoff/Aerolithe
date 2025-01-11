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

                        // Create a new PictureBox
                        PictureBox pictureBox = CreatePictureBox(resizedImage);

                        // Set border color based on the sequence
                        Color borderColor = GetBorderColor(currentSequence);

                        // Create a new sequence FlowLayoutPanel if needed
                        if (currentSequenceFlowLayoutPanel == null || currentSequenceFlowLayoutPanel.BackColor != borderColor)
                        {
                            currentSequenceFlowLayoutPanel = CreateSequenceFlowLayoutPanel(borderColor);
                            SetFlowLayoutPanelWidth(currentSequenceFlowLayoutPanel, nombreImages5Degres, 200);
                            SetFlowLayoutPanelWidth(flowLayoutPanel1, nombreImages5Degres, 200);
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

    }
}
