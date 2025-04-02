using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using Emgu.CV.Util;

// RÉFÉRENCES: https://stackoverflow.com/questions/62866191/emgucv-crop-detected-shape-automatically
// Autre ref: https://stackoverflow.com/questions/35460986/morphological-operations-on-image

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        
        private async Task getBackgroundImage()
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

        private Bitmap applyMaskToPicture(Mat mask, Mat picture)
        {
            if (mask == null) {
                return null;
            }
            Mat resultat = new Mat();
            try
            {
                // Resize the mask to match the dimensions of the picture
                Mat resizedMask = new Mat();
                CvInvoke.Resize(mask, resizedMask, new Size(picture.Width, picture.Height));

                // Apply the resized mask to the picture
                CvInvoke.BitwiseAnd(picture, picture, resultat, mask: resizedMask);
                
            }
            catch (Exception ex)
            {
                txtBox_Console.Text += $"picture channels: {picture.NumberOfChannels}" + Environment.NewLine + $"mask channels: {mask.NumberOfChannels}" + Environment.NewLine + ex.ToString();
            }

            return resultat.ToImage<Bgr, Byte>().ToBitmap();
        }

        private async Task CalculateBlurriness(MemoryStream memoryStream)
        {
            //AppendTextToConsoleNL("ici");
            // Convert MemoryStream to byte array
            byte[] byteArray = memoryStream.ToArray();
            //AppendTextToConsoleNL("Byte array length: " + byteArray.Length);

            // Convert byte array to Mat using VectorOfByte
            using (VectorOfByte buf2 = new VectorOfByte(byteArray))
            {
                Mat frame = new Mat();
                CvInvoke.Imdecode(buf2, ImreadModes.Color, frame);
                //AppendTextToConsoleNL("Frame decoded: " + !frame.IsEmpty);

                if (!frame.IsEmpty)
                {
                    // Convert frame to grayscale
                    Mat gray = new Mat();
                    CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);
                    //AppendTextToConsoleNL("Converted to grayscale");

                    // Apply Laplacian filter
                    Mat laplacian = new Mat();
                    CvInvoke.Laplacian(gray, laplacian, DepthType.Cv64F);
                    //AppendTextToConsoleNL("Laplacian applied");

                    // Calculate mean and standard deviation
                    Mat mean = new Mat();
                    Mat stddev = new Mat();
                    CvInvoke.MeanStdDev(laplacian, mean, stddev);
                    //AppendTextToConsoleNL("Mean and standard deviation calculated");

                    // Retrieve the standard deviation value
                    double[] stddevValues = new double[stddev.Rows * stddev.Cols];
                    stddev.CopyTo(stddevValues);
                    if (stddevValues.Length > 0)
                    {
                        double variance = Math.Pow(stddevValues[0], 2);
                        //AppendTextToConsoleNL($"Variance: {variance}");
                        blurrynessAmount = variance;
                        // Update the label on the UI thread
                        lbl_bluriness.Invoke((MethodInvoker)(() =>
                        {
                            lbl_bluriness.Text = variance.ToString();
                            //AppendTextToConsoleNL("Label updated");
                        }));
                    }
                    else
                    {
                        AppendTextToConsoleNL("Failed to retrieve standard deviation values");
                    }
                }
                else
                {
                    AppendTextToConsoleNL("Frame is empty");
                }
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
                //lbl_blur_Laplacian.Text = (Math.Truncate(blurAmount * 1000) / 1000).ToString();
                double blurAmount_S = CalculateBlur_Sobel(imageMat);
                //lbl_blur_Sobel.Text = (Math.Truncate(blurAmount_S * 1000) / 1000).ToString();
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
                byte[] imageBytes = stream.ToArray();
                CvInvoke.Imdecode(imageBytes, ImreadModes.Color, foreground);
                //pictureBox_applyBlur.Image = foreground.ToImage<Bgr, Byte>().ToBitmap();
                Mat result = new Mat();
                result = background - foreground;
                //Mat result = foreground.AbsDiff(background);
                pictureBox_imageSoustraction.Image = result.ToImage<Bgr, Byte>().ToBitmap();
                
                //calculerFlou(result);
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
