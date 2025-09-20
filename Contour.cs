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
using System.Drawing.Imaging;
using System.Diagnostics;


//using Microsoft.ML.OnnxRuntime;
//using Microsoft.ML.OnnxRuntime.Tensors;


// RÉFÉRENCES: https://stackoverflow.com/questions/62866191/emgucv-crop-detected-shape-automatically
// Autre ref: https://stackoverflow.com/questions/35460986/morphological-operations-on-image

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private bool applyMaskToLiveView = false;

        private Bitmap BrightnessMaskFromStream(MemoryStream stream, int threshold = 100)
        {
            // Decode JPEG stream into Mat
            byte[] imageBytes = stream.ToArray();
            Mat image = new Mat();            
            CvInvoke.Imdecode(imageBytes, ImreadModes.Color, image);

            // Convert to grayscale
            Mat gray = new Mat();
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);

            // Apply binary threshold
            Mat binary = new Mat();
            CvInvoke.Threshold(gray, binary, threshold, 255, ThresholdType.Binary);

            return binary.ToBitmap();
        }
             
        private Bitmap applyMaskToPicture(Mat picture)
        {
            if (mask == null)
            {
                return null;
            }

            Mat resultat = new Mat();
            try
            {
                // Resize the mask to match the dimensions of the picture
                Mat resizedMask = new Mat();
                CvInvoke.Resize(mask, resizedMask, new Size(picture.Width, picture.Height));

                // Ensure mask is of the same type as the picture
                if (resizedMask.Depth != picture.Depth)
                {
                    resizedMask.ConvertTo(resizedMask, picture.Depth);
                }

                // Apply the mask to the picture to make black areas transparent
                Mat blackMaskedPicture = new Mat();
                CvInvoke.BitwiseAnd(picture, picture, blackMaskedPicture, mask: resizedMask);

                // Create an inverted mask where white areas become black and black areas become white
                Mat invertedMask = new Mat();
                CvInvoke.BitwiseNot(resizedMask, invertedMask);

                // Apply the inverted mask to keep white areas as colored
                CvInvoke.BitwiseOr(blackMaskedPicture, picture, resultat, mask: invertedMask);
            }
            catch (Exception ex)
            {
                AppendTextToConsoleSL($"picture channels: {picture.NumberOfChannels}" + Environment.NewLine + $"mask channels: {mask.NumberOfChannels}" + Environment.NewLine + ex.ToString());
            }

            return resultat.ToImage<Bgr, Byte>().ToBitmap();
        }
      
        private async Task CalculDuFlou(MemoryStream memoryStream)
        {
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
                    //ColorConversion colorConversion = (ColorConversion)comboBox_EmguConversion.SelectedIndex;
                    //CvInvoke.CvtColor(frame, gray, colorConversion);
                    //AppendTextToConsoleNL("Converted to grayscale");
                    CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);

                    //Mat convertedFrame = new Mat();
                    //int indx = comboBox_EmguColor.SelectedIndex;

                    //switch (indx)
                    //{
                    //    case 0: // Red channel
                    //        CvInvoke.ExtractChannel(frame, convertedFrame, 2); // 2 is the index for the red channel
                    //        break;
                    //    case 1: // Green channel
                    //        CvInvoke.ExtractChannel(frame, convertedFrame, 1); // 1 is the index for the green channel
                    //        break;
                    //    case 2: // Blue channel
                    //        CvInvoke.ExtractChannel(frame, convertedFrame, 0); // 0 is the index for the blue channel
                    //        break;
                    //    case 3: // Grayscale
                    //        CvInvoke.CvtColor(frame, convertedFrame, ColorConversion.Bgr2Gray);
                    //        break;
                    //    default:
                    //        CvInvoke.CvtColor(frame, convertedFrame, ColorConversion.Bgr2Gray); // Default to grayscale
                    //        break;
                    //}
                    //Mat hsv = new Mat();
                    //CvInvoke.CvtColor(convertedFrame, hsv, selectedConversion);

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
                            lbl_bluriness.Text = variance.ToString("F2");
                            //AppendTextToConsoleNL("Label updated");
                        }));
                       
                    }
                    else
                    {
                        //AppendTextToConsoleNL("Failed to retrieve standard deviation values");
                    }
                }
                else
                {
                    //AppendTextToConsoleNL("Frame is empty");
                }
            }
        }

        private async Task CalculDuFlouFromImage(Image<Bgr, byte> image)
        {
            

            try
            {
                // Convertir en niveaux de gris
                using (Mat gray = new Mat())
                {
                    CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);

                    // Appliquer le filtre Laplacien
                    using (Mat laplacian = new Mat())
                    {
                        CvInvoke.Laplacian(gray, laplacian, DepthType.Cv64F);

                        // Calculer la moyenne et l'écart-type
                        using (Mat mean = new Mat())
                        using (Mat stddev = new Mat())
                        {
                            CvInvoke.MeanStdDev(laplacian, mean, stddev);

                            double[] stddevValues = new double[stddev.Rows * stddev.Cols];
                            stddev.CopyTo(stddevValues);

                            if (stddevValues.Length > 0)
                            {
                                double variance = Math.Pow(stddevValues[0], 2);
                                blurrynessAmountMask = variance;

                                lbl_blurinessMask.Invoke((MethodInvoker)(() =>
                                {
                                    lbl_blurinessMask.Text = variance.ToString("F2");
                                }));

                               
                            }
                            else
                            {
                                //AppendTextToConsoleNL("Échec du calcul de l'écart-type.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur dans CalculDuFlouFromImage : " + ex.Message);
            }
        }

        public void MasqueAvecPixels()
        {
    //        Bitmap original = (Bitmap)picBox_SharpImage.Image;
    //        Bitmap mask = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);

    //        int threshold = hScrollBar_ThresholdMaskValue.Value;
    //        Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);

    //        BitmapData originalData = original.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    //        BitmapData maskData = mask.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

    //        int stride = originalData.Stride;
    //        int bytes = stride * original.Height;
    //        byte[] pixelBuffer = new byte[bytes];
    //        byte[] resultBuffer = new byte[bytes];

    //        System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, pixelBuffer, 0, bytes);

    //        for (int y = 0; y < original.Height; y++)
    //        {
    //            int rowOffset = y * stride;
    //            for (int x = 0; x < original.Width; x++)
    //            {
    //                int index = rowOffset + x * 3;

    //                byte b = pixelBuffer[index];
    //                byte g = pixelBuffer[index + 1];
    //                byte r = pixelBuffer[index + 2];

    //                int brightness = (int)(r * 0.299 + g * 0.587 + b * 0.114);
    //                byte color = (brightness < threshold) ? (byte)0 : (byte)255;

    //                resultBuffer[index] = color;
    //                resultBuffer[index + 1] = color;
    //                resultBuffer[index + 2] = color;
    //            }
    //        }

    //        System.Runtime.InteropServices.Marshal.Copy(resultBuffer, 0, maskData.Scan0, bytes);

    //        original.UnlockBits(originalData);
    //        mask.UnlockBits(maskData);

    //        picBox_SharpImageMask.Image = mask;
       }

        public void PostFocusStackMask()
        {
            if (File.Exists(focusStackOutputPath))
            {
                using (var originalBitmap = new Bitmap(focusStackOutputPath))
                {
                    Bitmap finalBitmap;

                    var sourceImage = originalBitmap.ToImage<Emgu.CV.Structure.Bgr, byte>();
                    var maskGray = maskBitmapLive.ToImage<Emgu.CV.Structure.Gray, byte>();

                    var resizedMask = maskGray.Resize(sourceImage.Width, sourceImage.Height, Emgu.CV.CvEnum.Inter.Linear);
                    var invertedMask = resizedMask.Not();
                    var maskBgr = invertedMask.Convert<Emgu.CV.Structure.Bgr, byte>();

                    sourceImage._And(maskBgr);
                    finalBitmap = sourceImage.ToBitmap();

                    // Libération
                    maskGray.Dispose();
                    resizedMask.Dispose();
                    invertedMask.Dispose();
                    maskBgr.Dispose();
                    sourceImage.Dispose();

                    // Sauvegarde de l'image finale
                    string directory = Path.GetDirectoryName(focusStackOutputPath);
                    string filenameWithoutExt = Path.GetFileNameWithoutExtension(focusStackOutputPath);
                    string extension = Path.GetExtension(focusStackOutputPath);
                    string newFilePath = Path.Combine(directory, $"{filenameWithoutExt}_Mask{extension}");

                    finalBitmap.Save(newFilePath);

                    // Affichage dans le PictureBox
                    picBox_FocusStackedImage.Image?.Dispose();
                    picBox_FocusStackedImage.Image = finalBitmap;
                    stackedImageInBuffer = true;
                    //AppendTextToConsoleNL("stackedImageInBuffer = " + stackedImageInBuffer);
                }
            }

        }
    }

}
