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

        double[,] ComputeSharpnessGridMaskedROI(Mat grayImage, Mat safeMask, int blockSize, double minCoverage = 0.8)
        {
            int rows = grayImage.Height / blockSize;
            int cols = grayImage.Width / blockSize;
            double[,] grid = new double[rows, cols];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Rectangle roi = new Rectangle(x * blockSize, y * blockSize, blockSize, blockSize);

                    using var grayRoi = new Mat(grayImage, roi);
                    using var maskRoi = new Mat(safeMask, roi);

                    // Couverture: proportion de pixels non-zéro dans le bloc
                    int nz = CvInvoke.CountNonZero(maskRoi);
                    double coverage = nz / (double)(blockSize * blockSize);
                    if (coverage < minCoverage)
                    {
                        grid[y, x] = double.NaN; // ou 0, selon ton choix
                        continue;
                    }

                    // Laplacien sur l'image **non masquée**
                    using var lap = new Mat();
                    CvInvoke.Laplacian(grayRoi, lap, DepthType.Cv64F);

                    // Stats **masquées** (ignore les pixels hors ROI)
                    MCvScalar mean = new MCvScalar(), stddev = new MCvScalar();
                    CvInvoke.MeanStdDev(lap, ref mean, ref stddev, maskRoi); // <— clé: stats pondérées

                    grid[y, x] = stddev.V0 * stddev.V0; // variance
                }
            }
            return grid;
        }



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
                    //using Mat block = new Mat(MakeMaskedGray(image, maskBitmapLive.ToMat()), roi);
                    grid[y, x] = ComputeLaplacianVariance(block);
                }
            }

            return grid;
        }

        double ComputeLaplacianVariance(Mat block)
        {
            Mat laplacian = new Mat();
            CvInvoke.Laplacian(block, laplacian, DepthType.Cv64F);
            MCvScalar mean = new MCvScalar(), stddev = new MCvScalar();
            CvInvoke.MeanStdDev(laplacian, ref mean, ref stddev);
            return stddev.V0 * stddev.V0;
        }

    }



}

