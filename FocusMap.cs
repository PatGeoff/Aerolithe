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
                    Mat block = new Mat(image, roi);
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

