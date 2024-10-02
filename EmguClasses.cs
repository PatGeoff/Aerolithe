using Emgu.CV.CvEnum;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Aerolithe
{
    public class ImageResizer
    {
        public static Mat MemoryStreamToMat(MemoryStream memoryStream)
        {
            // Load the image from MemoryStream
            Mat mat = new Mat();
            CvInvoke.Imdecode(memoryStream.ToArray(), ImreadModes.Color, mat);
            return mat;
        }

        public static Mat ResizeMat(Mat mat, int width, int height)
        {
            Mat resizedMat = new Mat();
            CvInvoke.Resize(mat, resizedMat, new Size(width, height), 0, 0, Inter.Linear);
            return resizedMat;
        }

        public static Bitmap MatToBitmap(Mat mat)
        {
            return mat.ToBitmap();
        }

        public static void DisplayImageInPictureBox(PictureBox pictureBox, Bitmap bitmap)
        {
            pictureBox.Image?.Dispose();
            pictureBox.Image = bitmap;
        }

        public static void DownsizeImageToFitPictureBox(PictureBox pictureBox, MemoryStream memoryStream)
        {
            // Convert MemoryStream to Mat
            Mat originalMat = MemoryStreamToMat(memoryStream);

            // Resize the Mat to fit the PictureBox
            Mat resizedMat = ResizeMat(originalMat, pictureBox.Width, pictureBox.Height);

            // Convert the Mat to Bitmap
            Bitmap bitmap = MatToBitmap(resizedMat);

            // Display the Bitmap in the PictureBox
            DisplayImageInPictureBox(pictureBox, bitmap);
        }
    }
}
