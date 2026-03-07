using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        FocusMap? currentLiveViewFocusMap;

        class FocusMap
        {
            public int FocusPosition;
            public double[,] SharpnessGrid; // [row, col] sharpness score
        }

        //double[,] ComputeSharpnessGrid(Mat image, int blockSize = 32)
        //{
        //    int rows = image.Height / blockSize;
        //    int cols = image.Width / blockSize;
        //    double[,] grid = new double[rows, cols];

        //    for (int y = 0; y < rows; y++)
        //    {
        //        for (int x = 0; x < cols; x++)
        //        {
        //            Rectangle roi = new Rectangle(x * blockSize, y * blockSize, blockSize, blockSize);
        //            Mat block = new Mat(image, roi);
        //            grid[y, x] = ComputeLaplacianVariance(block);
        //        }
        //    }

        //    return grid;
        //}

        double[,] ComputeSharpnessGrid(Mat image, int blockSize = 32)
        {
            int rows = image.Height / blockSize;
            int cols = image.Width / blockSize;
            double[,] grid = new double[rows, cols];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Rectangle roi = new Rectangle(x * blockSize, y * blockSize, blockSize, blockSize);
                    using Mat block = new Mat(image, roi);
                    grid[y, x] = ComputeLaplacianVariance(block);
                }
            }

            return grid;
        }

        Mat MakeMaskedGray(Mat grayImage, Mat binaryMask)
        {
            // grayImage : 8UC1
            if (grayImage.NumberOfChannels != 1 || grayImage.Depth != Emgu.CV.CvEnum.DepthType.Cv8U)
                throw new ArgumentException("grayImage doit être 8UC1");

            // 1) Normaliser le masque en 8UC1
            Mat mask8u = new Mat();
            if (binaryMask.NumberOfChannels != 1 || binaryMask.Depth != Emgu.CV.CvEnum.DepthType.Cv8U)
            {
                CvInvoke.CvtColor(binaryMask, mask8u, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            }
            else
            {
                mask8u = binaryMask.Clone();
            }

            // 2) Resize du masque à la taille de l'image, en 'Nearest' pour rester binaire
            if (mask8u.Size != grayImage.Size)
            {
                Mat resized = new Mat();
                CvInvoke.Resize(mask8u, resized, grayImage.Size, 0, 0, Emgu.CV.CvEnum.Inter.Nearest);
                mask8u.Dispose();
                mask8u = resized;
            }

            // 3) Binarisation dure (blanc=zones gardées)
            CvInvoke.Threshold(mask8u, mask8u, 127, 255, Emgu.CV.CvEnum.ThresholdType.Binary);

            // 4) Appliquer le masque : I_masked = I & mask
            Mat maskedGray = new Mat();
            CvInvoke.BitwiseAnd(grayImage, grayImage, maskedGray, mask8u);

            mask8u.Dispose();
            return maskedGray;
        }


        //double[,] ComputeSharpnessGridMasked(Mat grayImage, Mat binaryMask, int blockSize = 32, double minCoverage = 0.5)
        //{
        //    if (grayImage.NumberOfChannels != 1 || grayImage.Depth != Emgu.CV.CvEnum.DepthType.Cv8U)
        //        throw new ArgumentException("grayImage doit être 8UC1");

        //    // S'assurer que le mask est 8UC1, même taille, binaire
        //    Mat mask8u = new Mat();
        //    if (binaryMask.NumberOfChannels != 1 || binaryMask.Depth != Emgu.CV.CvEnum.DepthType.Cv8U)
        //    {
        //        // Si jamais c'est une image couleur
        //        CvInvoke.CvtColor(binaryMask, mask8u, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
        //    }
        //    else
        //    {
        //        mask8u = binaryMask.Clone();
        //    }

        //    if (mask8u.Size != grayImage.Size)
        //    {
        //        // Redimensionner le masque au nearest pour ne pas "grisouiller" les bords
        //        Mat resized = new Mat();
        //        CvInvoke.Resize(mask8u, resized, grayImage.Size, 0, 0, Emgu.CV.CvEnum.Inter.Nearest);
        //        mask8u.Dispose();
        //        mask8u = resized;
        //    }

        //    // Binariser dur au cas où (blanc=ROI)
        //    CvInvoke.Threshold(mask8u, mask8u, 127, 255, Emgu.CV.CvEnum.ThresholdType.Binary);

        //    int cols = (int)Math.Ceiling(grayImage.Width / (double)blockSize);
        //    int rows = (int)Math.Ceiling(grayImage.Height / (double)blockSize);

        //    double[,] grid = new double[rows, cols];

        //    for (int by = 0; by < rows; by++)
        //    {
        //        int y = by * blockSize;
        //        int bh = Math.Min(blockSize, grayImage.Height - y);

        //        for (int bx = 0; bx < cols; bx++)
        //        {
        //            int x = bx * blockSize;
        //            int bw = Math.Min(blockSize, grayImage.Width - x);

        //            var roi = new Rectangle(x, y, bw, bh);

        //            using (Mat block = new Mat(grayImage, roi))
        //            using (Mat maskROI = new Mat(mask8u, roi))
        //            {
        //                int maskedPixels = CvInvoke.CountNonZero(maskROI);
        //                double coverage = maskedPixels / (double)(bw * bh);

        //                if (maskedPixels == 0 || coverage < minCoverage)
        //                {
        //                    grid[by, bx] = double.NaN; // bloc ignoré
        //                    continue;
        //                }

        //                // Laplacien
        //                using (var lap = new Mat())
        //                {
        //                    CvInvoke.Laplacian(block, lap, Emgu.CV.CvEnum.DepthType.Cv64F, 3, 1, 0, Emgu.CV.CvEnum.BorderType.Default);

        //                    // Variance = (stddev)^2, calculée sous masque
        //                    var mean = new Emgu.CV.Structure.MCvScalar();
        //                    var stddev = new Emgu.CV.Structure.MCvScalar();
        //                    CvInvoke.MeanStdDev(lap, ref mean, ref stddev, maskROI);

        //                    grid[by, bx] = stddev.V0 * stddev.V0;
        //                }
        //            }
        //        }
        //    }

        //    mask8u.Dispose();
        //    return grid;
        //}

        double ComputeLaplacianVariance(Mat block)
        {
            Mat laplacian = new Mat();
            CvInvoke.Laplacian(block, laplacian, DepthType.Cv64F);
            MCvScalar mean = new MCvScalar(), stddev = new MCvScalar();
            CvInvoke.MeanStdDev(laplacian, ref mean, ref stddev);
            return stddev.V0 * stddev.V0;
        }

        (int start, int end) FindFocusLimits(List<FocusMap> maps, double threshold, double minSharpBlockRatio)
        {
            int start = -1, end = -1;

            for (int i = 0; i < maps.Count; i++)
            {
                var grid = maps[i].SharpnessGrid;
                int totalBlocks = grid.GetLength(0) * grid.GetLength(1);
                int sharpBlocks = 0;

                foreach (var val in grid)
                {
                    if (val > threshold)
                        sharpBlocks++;
                }

                double ratio = (double)sharpBlocks / totalBlocks;

                if (ratio >= minSharpBlockRatio)
                {
                    if (start == -1)
                        start = maps[i].FocusPosition;

                    end = maps[i].FocusPosition;
                }
            }

            return (start, end);
        }

    }



}

