using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

// RÉFÉRENCES: https://stackoverflow.com/questions/62866191/emgucv-crop-detected-shape-automatically
// Autre ref: https://stackoverflow.com/questions/35460986/morphological-operations-on-image

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        
        private void getBackgroundImage()
        {
            if (!device.LiveViewEnabled)
            {
                device.LiveViewEnabled = true;
                while (!device.LiveViewEnabled) { }                
            }
            else
            {
                background = new Mat();
                // Convertit le LiveCapture en stream
                using (MemoryStream stream = new MemoryStream(imageView.JpegBuffer))
                {
                    byte[] imageBytes = stream.ToArray();
                    // Convertit le byte array en Mat
                    CvInvoke.Imdecode(imageBytes, ImreadModes.Color, background);
                    picBox_imageFond.Image = background.ToImage<Bgr, Byte>().ToBitmap();
                    if (background == null) MessageBox.Show("Impossible de saisir l'image");
                }
            }
        }


        private void backgroundSubstraction()
        {
            if (background != null)
            {
                //MessageBox.Show("ici"); 
                // Convertit le LiveCapture en stream
                using (MemoryStream stream = new MemoryStream(imageView.JpegBuffer))
                {
                    Mat foreground = new Mat();
                    //stream = new MemoryStream(imageView.JpegBuffer);
                    byte[] imageBytes = stream.ToArray();
                    CvInvoke.Imdecode(imageBytes, ImreadModes.Color, foreground);
                    //pictureBox_applyBlur.Image = foreground.ToImage<Bgr, Byte>().ToBitmap();
                    Mat result = new Mat();
                    result = background - foreground;
                    //Mat result = foreground.AbsDiff(background);
                    pictureBox_imageMasquage.Image = result.ToImage<Bgr, Byte>().ToBitmap();
                    calculerFlou(result);
                    createMask(result, foreground);
                }

            }
        }

        private void createMask(Mat stream, Mat foreground)
        {
            //conversion de BGR à HSV 
            Mat hsvImg = new Mat();
            CvInvoke.CvtColor(stream, hsvImg, Emgu.CV.CvEnum.ColorConversion.Bgr2Hsv);
            //// Préparation du masque binaire
            Mat binaryMask = new Mat();
            try
            {
                //// Définir le range pour séparer le min et max du masque                
                MCvScalar lower = new MCvScalar(Int16.Parse(textBox_lowB_x.Text), Int16.Parse(textBox_lowB_y.Text), Int16.Parse(textBox_lowB_z.Text));
                MCvScalar upper = new MCvScalar(Int16.Parse(textBox_upperB_x.Text), Int16.Parse(textBox_upperB_y.Text), Int16.Parse(textBox_upperB_z.Text));

                //// créer le masque binaire en utilisant le range de couleur
                CvInvoke.InRange(hsvImg, new ScalarArray(lower), new ScalarArray(upper), binaryMask);
                //// inverser le masque
                Mat invertedMask = 255 - binaryMask;

                pictureBox_imageMasquage.Image = invertedMask.ToImage<Bgr, Byte>().ToBitmap();

                applyMask(binaryMask, foreground);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

        private void applyMask(Mat mask, Mat foreground)
        {
            //Mat newColorMask = new Mat();
            try
            {

                //CvInvoke.CvtColor(mask, newColorMask, ColorConversion.Gray2Bgr);
                Mat resultat = new Mat();
                CvInvoke.BitwiseAnd(foreground, foreground, resultat, mask: mask);
                pictureBox_imageSoustraction.Image = resultat.ToImage<Bgr, Byte>().ToBitmap();
                //calculerFlou(resultat);
            }
            catch (Exception ex)
            {

                txtBox_Console.Text += $"foreground channels: {foreground.NumberOfChannels}" + Environment.NewLine + $"mask channels: {mask.NumberOfChannels}" + Environment.NewLine + ex.ToString();

            }


        }
        public void calculerFlou()
        {
            // Set live view image on picture box
            if (imageView != null)
            {
                // Convertit le LiveCapture en stream
                MemoryStream stream = new MemoryStream(imageView.JpegBuffer);
                // Convertit le stream en byte array
                byte[] imageBytes = stream.ToArray();
                Mat imageMat = new Mat();
                // Convertit le byte array en Mat
                CvInvoke.Imdecode(imageBytes, ImreadModes.Color, imageMat);
                // On calcule le Flou
                try
                {
                    double blurAmount = calculateBlur_Laplacian(imageMat);
                    //label_flou_L.Text = (Math.Truncate(blurAmount * 1000) / 1000).ToString();
                    double blurAmount_S = CalculateBlur_Sobel(imageMat);
                    //label_flou_S.Text = (Math.Truncate(blurAmount_S * 1000) / 1000).ToString();
                }
                catch (Exception ex)
                {
                    //label_flou_L.Text = ex.ToString();
                }
            }
        }
        public void calculerFlou(Image image)
        {
            // Set live view image on picture box
            if (imageView != null)
            {
                MemoryStream ms = new MemoryStream();
                image.Save(ms, image.RawFormat);

                byte[] imageBytes = ms.ToArray();
                Mat imageMat = new Mat();
                // Convertit le byte array en Mat
                CvInvoke.Imdecode(imageBytes, ImreadModes.Color, imageMat);
                // On calcule le Flou
                try
                {
                    double blurAmount = calculateBlur_Laplacian(imageMat);
                    //label_flou_L.Text = (Math.Truncate(blurAmount * 1000) / 1000).ToString();
                    double blurAmount_S = CalculateBlur_Sobel(imageMat);
                    //label_flou_S.Text = (Math.Truncate(blurAmount_S * 1000) / 1000).ToString();
                }
                catch (Exception ex)
                {
                    //label_flou_L.Text = ex.ToString();
                }
            }
        }

        public void calculerFlou(Mat imageMat)
        {
            // Set live view image on picture box

            try
            {
                double blurAmount = calculateBlur_Laplacian(imageMat);
                //label_flou_L_M.Text = (Math.Truncate(blurAmount * 1000) / 1000).ToString();
                double blurAmount_S = CalculateBlur_Sobel(imageMat);
                //label_flou_S_M.Text = (Math.Truncate(blurAmount_S * 1000) / 1000).ToString();
            }
            catch (Exception ex)
            {
                //textBox_Error.Text = ex.ToString();
            }

        }
        public double calculateBlur_Laplacian(Mat image)
        {
            Mat gray = new Mat();
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);

            Mat laplacian = new Mat();
            CvInvoke.Laplacian(gray, laplacian, DepthType.Cv16S);

            Mat absLaplacian = new Mat();
            CvInvoke.ConvertScaleAbs(laplacian, absLaplacian, 1.0, 0.0);

            MCvScalar scalar = CvInvoke.Mean(absLaplacian);

            return scalar.V0;
        }
        public double CalculateBlur_Sobel(Mat image)
        {
            Mat gray = new Mat();
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);

            Mat dx = new Mat();
            Mat dy = new Mat();

            CvInvoke.Sobel(gray, dx, DepthType.Cv64F, 1, 0);
            CvInvoke.Sobel(gray, dy, DepthType.Cv64F, 0, 1);

            Mat magnitude = new Mat();
            CvInvoke.CartToPolar(dx, dy, magnitude, new Mat(), true);

            MCvScalar scalar = CvInvoke.Mean(magnitude);

            return scalar.V0;
        }
        private void backgroundSubstraction(MemoryStream stream)
        {

            if (background != null)
            {

                Mat foreground = new Mat();
                //stream = new MemoryStream(imageView.JpegBuffer);
                byte[] imageBytes = stream.ToArray();
                CvInvoke.Imdecode(imageBytes, ImreadModes.Color, foreground);
                //pictureBox_applyBlur.Image = foreground.ToImage<Bgr, Byte>().ToBitmap();
                Mat result = new Mat();
                result = background - foreground;
                //Mat result = foreground.AbsDiff(background);
                pictureBox_imageSoustraction.Image = result.ToImage<Bgr, Byte>().ToBitmap();

                calculerFlou(result);
                createMask(result, foreground);

            }

        }

         public async Task backgroundSubstractionOnImage()
        {
            // Take the picture asynchronously
            await takePictureAsync();

            // Wait for the image to be ready
            await imageReadyTcs.Task;

        }
    }
}
