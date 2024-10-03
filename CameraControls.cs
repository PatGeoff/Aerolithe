using Nikon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public void takePicture()
        {
            device.Capture();
        }
        void device_ImageReady(NikonDevice sender, NikonImage image)
        {
            //MessageBox.Show("imageReady");
            try
            {

                if (image.Type == NikonImageType.Jpeg)
                {
                    using (MemoryStream memoryStream = new MemoryStream(image.Buffer))
                    {
                        // Downsize and display the image in a PictureBox
                        ImageResizer.DownsizeImageToFitPictureBox(picBox_imageFond, memoryStream);
                        ImageResizer.DownsizeImageToFitPictureBox(picBox_pictureTaken, memoryStream);
                        
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("device_ImageReady exception" + ex.Message);
            }



            /*SaveFileDialog dialog = new SaveFileDialog();

            dialog.Filter = (image.Type == NikonImageType.Jpeg) ?
                "Jpeg Image (*.jpg)|*.jpg" :
                "Nikon NEF (*.nef)|*.nef";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                using (FileStream stream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write))
                {
                    stream.Write(image.Buffer, 0, image.Buffer.Length);
                }
            }*/


            // MemoryStream memStream = new MemoryStream(image.Buffer);
            // picBox_imageFond.Image = Image.FromStream(memStream);
            // Image im = Image.FromStream(memStream);

            //// textBox_Error.Text = im.Width.ToString() + " : " + im.Height.ToString();


            // while (true)
            // {
            //     try
            //     {
            //         device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus);
            //     }
            //     catch (NikonException ex)
            //     {
            //         if (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
            //         {
            //             continue;
            //         }
            //         else
            //         {
            //             throw;
            //         }
            //     }

            //     break;
            // }
            // device.LiveViewEnabled = true;
            // liveViewTimer.Start();



        }
    }
   
}
