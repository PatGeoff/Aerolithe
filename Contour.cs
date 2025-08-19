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

        //private readonly InferenceSession _session;

       //public async Task<Bitmap> RunU2NetFromMemoryStreamAsync(MemoryStream stream)
       // {
       //     return await Task.Run(() =>
       //     {
       //         using Bitmap original = new Bitmap(stream);
       //         using Bitmap resized = new Bitmap(original, new Size(320, 320));

       //         DenseTensor<float> inputTensor = new DenseTensor<float>(new[] { 1, 3, 320, 320 });

       //         for (int y = 0; y < 319; y++)
       //         {
       //             for (int x = 0; x < 319; x++)
       //             {
       //                 Color pixel = resized.GetPixel(x, y);
       //                 inputTensor[0, 0, y, x] = pixel.R / 255f;
       //                 inputTensor[0, 1, y, x] = pixel.G / 255f;
       //                 inputTensor[0, 2, y, x] = pixel.B / 255f;
       //             }
       //         }

       //         var inputs = new List<NamedOnnxValue>
       // {
       //     NamedOnnxValue.CreateFromTensor("input.1", inputTensor)
       // };

       //         using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
       //         DenseTensor<float> outputTensor = (DenseTensor<float>)results.First().AsTensor<float>();

       //         // Create grayscale bitmap directly from output tensor
       //         Bitmap bitmap = new Bitmap(1024, 1024, PixelFormat.Format8bppIndexed);

       //         // Set grayscale palette
       //         ColorPalette palette = bitmap.Palette;
       //         for (int i = 0; i < 256; i++)
       //         {
       //             palette.Entries[i] = Color.FromArgb(i, i, i);
       //         }
       //         bitmap.Palette = palette;

       //         // Lock bitmap data
       //         BitmapData data = bitmap.LockBits(
       //             new Rectangle(0, 0, 320, 320),
       //             ImageLockMode.WriteOnly,
       //             PixelFormat.Format8bppIndexed);

       //         unsafe
       //         {
       //             byte* ptr = (byte*)data.Scan0;
       //             int stride = data.Stride;

       //             for (int y = 0; y < 320; y++)
       //             {
       //                 for (int x = 0; x < 320; x++)
       //                 {
       //                     float value = outputTensor[0, 0, y, x];
       //                     byte gray = (byte)(Math.Clamp(value, 0f, 1f) * 255f);
       //                     ptr[y * stride + x] = gray;
       //                 }
       //             }
       //         }

       //         bitmap.UnlockBits(data);
       //         return bitmap;
       //     });
       // }





        //private async Task GetBackgroundImage(string bgImage)
        //    // Étant donné que ceci n'est exécuté qu'une fois sur demande, le background est fixé à l'image prise à cet instant
        //{
        //    try
        //    {
        //        if (!device.LiveViewEnabled)
        //        {
        //            device.LiveViewEnabled = true;
        //            while (!device.LiveViewEnabled) { }
        //        }
        //        else
        //        {                    
                    
        //            //// Convertit le LiveCapture en stream
        //            using (MemoryStream stream = new MemoryStream(imageView.JpegBuffer))
        //            {
        //                string name = bgImage + ".jpg";
        //                string outputPath = Path.Combine(projectDirectory, name);                        
        //                await SaveStreamAsJpegWithProgress(stream, outputPath, null, false);
        //                prefs.bgImage
        //                WritePrefs(bgImage, outputPath);
        //                LoadPrefs(); // Assure que l'image loadée est bien l'image qu'on vient d'écrire dans pref.

        //                //    byte[] imageBytes = stream.ToArray();
        //                //    // Convertit le byte array en Mat

        //                //    CvInvoke.Imdecode(imageBytes, ImreadModes.Color, background);

        //                //    picBox_imageFond_1.Image = background.ToImage<Bgr, Byte>().ToBitmap();

        //                //    if (background == null) MessageBox.Show("Impossible de saisir l'image");

        //            }

        //    }
        //    }
        //    catch (Exception e)
        //    {

        //        AppendTextToConsoleNL(e.Message);
        //    }
            
        //}
        private async Task BackgroundSubtraction(MemoryStream stream)
        {

            
            if (applyMaskToLiveView)
            {               
                if (background != null)
                {
                    using (Mat foreground = new Mat())
                    using (Mat result = new Mat())
                    {
                        byte[] imageBytes = stream.ToArray();
                        CvInvoke.Imdecode(imageBytes, ImreadModes.Color, foreground);
                        CvInvoke.Subtract(background, foreground, result);
                        //Mat result = foreground.AbsDiff(background);
                        Debug.WriteLine("applying mask");
                        CreateMask(result, foreground);
                        //pictureBox_imageMasquage.Image = mask.ToImage<Bgr, Byte>().ToBitmap();

                        //applyMaskToPicture(foreground);
                        //pictureBox_imageSoustraction.Image = applyMaskToPicture(foreground).ToImage<Bgr, Byte>().ToBitmap();

                     
                        picBox_LiveView_Main.Image = applyMaskToPicture(foreground).ToImage<Bgr, Byte>().ToBitmap();
                        
                    }
                }               
            }
            Task.Run(async () => await CalculDuFlou(stream));
        }

        private Bitmap BlobMaskingLiveView(MemoryStream stream, int maskOffset = 0)
        {
            // Load image from stream into Mat
            Mat image;
            using (Bitmap bitmap = new Bitmap(stream))
            {
                image = bitmap.ToMat();
            }

            // Convert to grayscale
            Mat gray = new Mat();
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);

            // Apply adaptive thresholding for better blob detection
            Mat binary = new Mat();
            CvInvoke.AdaptiveThreshold(gray, binary, 255, AdaptiveThresholdType.MeanC, ThresholdType.BinaryInv,11, 2);

            // Find contours
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            Mat hierarchy = new Mat();
            CvInvoke.FindContours(binary, contours, hierarchy, RetrType.External, ChainApproxMethod.ChainApproxSimple);

            // Create filled mask
            Mat mask = new Mat(binary.Rows, binary.Cols, DepthType.Cv8U, 1);
            mask.SetTo(new MCvScalar(0));
            for (int i = 0; i < contours.Size; i++)
            {
                CvInvoke.DrawContours(mask, contours, i, new MCvScalar(255), -1);
            }

            // Grow or shrink the mask
            if (maskOffset != 0)
            {
                Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));
                if (maskOffset > 0)
                {
                    CvInvoke.Dilate(mask, mask, kernel, new Point(-1, -1), maskOffset, BorderType.Default, new MCvScalar(0));
                }
                else
                {
                    CvInvoke.Erode(mask, mask, kernel, new Point(-1, -1), -maskOffset, BorderType.Default, new MCvScalar(0));
                }
            }

            // Apply the mask to the original image
            Mat maskedImage = new Mat();
            CvInvoke.BitwiseAnd(image, image, maskedImage, mask);

            // Return the masked image as a Bitmap
            return maskedImage.ToBitmap();
        }

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


        private Mat CreateMask(Mat stream, Mat foreground)
        {
            using (Mat hsvImg = new Mat())
            using (Mat binaryMask = new Mat())
            using (Mat invertedMask = new Mat())
            {
                // Conversion de BGR à HSV 
                CvInvoke.CvtColor(stream, hsvImg, Emgu.CV.CvEnum.ColorConversion.Bgr2Hsv);

                try
                {
                    // Définir le range pour séparer le min et max du masque    
                    MCvScalar lower = new MCvScalar(int.Parse(textBox_lowerB_x.Text), int.Parse(textBox_lowerB_y.Text), int.Parse(textBox_lowerB_z.Text));
                    MCvScalar upper = new MCvScalar(int.Parse(textBox_upperB_x.Text), int.Parse(textBox_upperB_y.Text), int.Parse(textBox_upperB_z.Text));

                    // Créer le masque binaire en utilisant le range de couleur
                    CvInvoke.InRange(hsvImg, new ScalarArray(lower), new ScalarArray(upper), binaryMask);

                    // Inverser le masque
                    mask = 255 - binaryMask;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }

                return invertedMask;
            }
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
        //private Bitmap applyMaskToPicture(Mat picture)
        //{
        //    if (mask == null || mask.Rows == 0 || mask.Cols == 0)
        //    {
        //        throw new InvalidOperationException("Mask is not properly loaded or is empty.");
        //    }

        //    if (picture == null || picture.Rows == 0 || picture.Cols == 0)
        //    {
        //        throw new InvalidOperationException("Picture is not properly loaded or is empty.");
        //    }

        //    Mat resultat = new Mat();
        //    try
        //    {
        //        // Calculate the choke factor based on the slider value
        //        int chokeValue = trkbar_chokeMask.Value; // Adjusted for -10 to +10 range

        //        // Convert the mask to a binary image
        //        Mat grayMask = new Mat();
        //        CvInvoke.CvtColor(mask, grayMask, ColorConversion.Bgr2Gray);
        //        CvInvoke.Threshold(grayMask, grayMask, 128, 255, ThresholdType.Binary);

        //        // Apply morphological operations to expand or retract the mask
        //        Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(Math.Abs(chokeValue), Math.Abs(chokeValue)), new Point(-1, -1));
        //        if (chokeValue > 0)
        //        {
        //            CvInvoke.Dilate(grayMask, grayMask, kernel, new Point(-1, -1), chokeValue, BorderType.Constant, new MCvScalar(0));
        //        }
        //        else if (chokeValue < 0)
        //        {
        //            CvInvoke.Erode(grayMask, grayMask, kernel, new Point(-1, -1), -chokeValue, BorderType.Constant, new MCvScalar(0));
        //        }

        //        // Apply the modified mask to the picture
        //        Mat blackMaskedPicture = new Mat();
        //        CvInvoke.BitwiseAnd(picture, picture, blackMaskedPicture, mask: grayMask);

        //        // Create an inverted mask where white areas become black and black areas become white
        //        Mat invertedMask = new Mat();
        //        CvInvoke.BitwiseNot(grayMask, invertedMask);

        //        // Apply the inverted mask to keep white areas as colored
        //        CvInvoke.BitwiseOr(blackMaskedPicture, picture, resultat, mask: invertedMask);
        //    }
        //    catch (Exception ex)
        //    {
        //        AppendTextToConsoleSL($"picture channels: {picture.NumberOfChannels}" + Environment.NewLine + $"mask channels: {mask.NumberOfChannels}" + Environment.NewLine + ex.ToString());
        //    }

        //    return resultat.ToImage<Bgr, Byte>().ToBitmap();
        //}


        private void createMaskLiveView(Mat stream, Mat foreground)
        {
            //conversion de BGR à HSV 
            Mat hsvImg = new Mat();
            CvInvoke.CvtColor(stream, hsvImg, Emgu.CV.CvEnum.ColorConversion.Bgr2Hsv);
            //// Préparation du masque binaire
            Mat binaryMask = new Mat();
            try
            {
                //// Définir le range pour séparer le min et max du masque    
                try
                {
                    MCvScalar lower = new MCvScalar(int.Parse(textBox_lowerB_x.Text), int.Parse(textBox_lowerB_y.Text), int.Parse(textBox_lowerB_z.Text));
                    MCvScalar upper = new MCvScalar(int.Parse(textBox_upperB_x.Text), int.Parse(textBox_upperB_y.Text), int.Parse(textBox_upperB_z.Text));
                    //// créer le masque binaire en utilisant le range de couleur
                    CvInvoke.InRange(hsvImg, new ScalarArray(lower), new ScalarArray(upper), binaryMask);
                    //// inverser le masque
                    Mat invertedMask = 255 - binaryMask;

                    picBox_LiveView_Main.Image = invertedMask.ToImage<Bgr, Byte>().ToBitmap();

                    //applyMask(binaryMask, foreground);
                }
                catch (Exception)
                {

                    throw;
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

       

        private void applyMask()
        {
            //Mat newColorMask = new Mat();
            try
            {
                Mat newColorMask = new Mat();
                CvInvoke.CvtColor(mask, newColorMask, ColorConversion.Gray2Bgr);
                Mat resultat = new Mat();
                CvInvoke.BitwiseAnd(foreground, foreground, resultat, mask: newColorMask);
                //pictureBox_imageSoustraction.Image = resultat.ToImage<Bgr, Byte>().ToBitmap();

                //calculerFlou(resultat);
            }
            catch (Exception ex)
            {

                txtBox_Console.Text += $"foreground channels: {foreground.NumberOfChannels}" + Environment.NewLine + $"mask channels: {mask.NumberOfChannels}" + Environment.NewLine + ex.ToString();

            }


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
                            lbl_bluriness.Text = variance.ToString();
                            //AppendTextToConsoleNL("Label updated");
                        }));
                        lbl_blurinessView.Invoke((MethodInvoker)(() =>
                        {
                            lbl_blurinessView.Text = variance.ToString("F3");
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
                //MemoryStream stream = new MemoryStream(imageView.JpegBuffer);

                // Convert the image to a byte array
                Image image = picBox_LiveView_Main.Image;
                byte[] imageBytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormat.Jpeg); // Save the image to the MemoryStream in JPEG format
                    imageBytes = ms.ToArray(); // Convert the MemoryStream to a byte array
                }


                // Convertit le stream en byte array
                //byte[] imageBytes = stream.ToArray();
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

        //public void calculerFlou(Image image)
        //{
        //    // Set live view image on picture box
        //    if (imageView != null)
        //    {
        //        MemoryStream ms = new MemoryStream();
        //        image.Save(ms, image.RawFormat);

        //        byte[] imageBytes = ms.ToArray();
        //        Mat imageMat = new Mat();
        //        // Convertit le byte array en Mat
        //        CvInvoke.Imdecode(imageBytes, ImreadModes.Color, imageMat);
        //        // On calcule le Flou
        //        try
        //        {
        //            double blurAmount = calculateBlur_Laplacian(imageMat);
        //            //label_flou_L.Text = (Math.Truncate(blurAmount * 1000) / 1000).ToString();
        //            double blurAmount_S = CalculateBlur_Sobel(imageMat);
        //            //label_flou_S.Text = (Math.Truncate(blurAmount_S * 1000) / 1000).ToString();
        //        }
        //        catch (Exception ex)
        //        {
        //            //label_flou_L.Text = ex.ToString();
        //        }
        //    }
        //}

        //public void calculerFlou(Mat imageMat)
        //{
        //    // Set live view image on picture box

        //    try
        //    {
        //        double blurAmount = calculateBlur_Laplacian(imageMat);
        //        //lbl_blur_Laplacian.Text = (Math.Truncate(blurAmount * 1000) / 1000).ToString();
        //        double blurAmount_S = CalculateBlur_Sobel(imageMat);
        //        //lbl_blur_Sobel.Text = (Math.Truncate(blurAmount_S * 1000) / 1000).ToString();
        //    }
        //    catch (Exception ex)
        //    {
        //        //textBox_Error.Text = ex.ToString();
        //    }

        //}

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


        // public async Task backgroundSubstractionOnImage()
        //{
        //    // Take the picture asynchronously
        //    await takePictureAsync();

        //    // Wait for the image to be ready
        //    await imageReadyTcs.Task;

        //}
        public void MasqueAvecPixels()
        {
            Bitmap original = (Bitmap)picBox_SharpImage.Image;
            Bitmap mask = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);

            int threshold = hScrollBar_ThresholdMaskValue.Value;
            Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);

            BitmapData originalData = original.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData maskData = mask.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int stride = originalData.Stride;
            int bytes = stride * original.Height;
            byte[] pixelBuffer = new byte[bytes];
            byte[] resultBuffer = new byte[bytes];

            System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, pixelBuffer, 0, bytes);

            for (int y = 0; y < original.Height; y++)
            {
                int rowOffset = y * stride;
                for (int x = 0; x < original.Width; x++)
                {
                    int index = rowOffset + x * 3;

                    byte b = pixelBuffer[index];
                    byte g = pixelBuffer[index + 1];
                    byte r = pixelBuffer[index + 2];

                    int brightness = (int)(r * 0.299 + g * 0.587 + b * 0.114);
                    byte color = (brightness < threshold) ? (byte)0 : (byte)255;

                    resultBuffer[index] = color;
                    resultBuffer[index + 1] = color;
                    resultBuffer[index + 2] = color;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(resultBuffer, 0, maskData.Scan0, bytes);

            original.UnlockBits(originalData);
            mask.UnlockBits(maskData);

            picBox_SharpImageMask.Image = mask;
        }

    }

}
