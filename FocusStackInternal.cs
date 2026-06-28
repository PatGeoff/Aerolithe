using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Drawing.Imaging;

namespace Aerolithe
{
    public partial class Aerolithe
    {
        private async Task<bool> RunInternalFocusStackAsync(string[] imagePaths, string outputImage)
        {
            return await Task.Run(() =>
            {
                string[] existingImages = imagePaths
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (existingImages.Length == 0)
                {
                    AppendTextToConsoleNL("Focus stack interne: aucune image source.", Color.Red);
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputImage) ?? ".");

                using var first = CvInvoke.Imread(existingImages[0], ImreadModes.Color);
                if (first.IsEmpty)
                {
                    AppendTextToConsoleNL("Focus stack interne: première image illisible.", Color.Red);
                    return false;
                }

                int width = first.Width;
                int height = first.Height;
                var loadedImages = new List<InternalFocusSourceImage>(existingImages.Length);
                var colorImages = new List<Image<Bgr, byte>>(existingImages.Length);
                var scoreImages = new List<Image<Gray, byte>>(existingImages.Length);

                try
                {
                    foreach (string path in existingImages)
                    {
                        using Mat loaded = CvInvoke.Imread(path, ImreadModes.Color);
                        if (loaded.IsEmpty)
                        {
                            AppendTextToConsoleNL("Focus stack interne: image ignorée car illisible: " + Path.GetFileName(path), Color.Orange);
                            continue;
                        }

                        Mat color = new();
                        if (loaded.Width != width || loaded.Height != height)
                        {
                            CvInvoke.Resize(loaded, color, new Size(width, height), 0, 0, Inter.Linear);
                        }
                        else
                        {
                            color = loaded.Clone();
                        }

                        using Mat gray = new();
                        CvInvoke.CvtColor(color, gray, ColorConversion.Bgr2Gray);
                        using Image<Gray, byte> grayImage = gray.ToImage<Gray, byte>();
                        Rectangle objectBounds = FindObjectBounds(grayImage);
                        double meanFocusScore = ComputeMeanFocusScore(gray);
                        loadedImages.Add(new InternalFocusSourceImage(path, color, objectBounds, meanFocusScore));
                    }

                    if (loadedImages.Count == 0)
                    {
                        AppendTextToConsoleNL("Focus stack interne: aucune image valide.", Color.Red);
                        return false;
                    }

                    int baseImageIndex = FindBestGlobalFocusSourceImage(loadedImages);
                    InternalFocusSourceImage baseImage = loadedImages[baseImageIndex];
                    AppendTextToConsoleNL($"Focus stack interne: image de base #{baseImageIndex} ({Path.GetFileName(baseImage.Path)}).");

                    for (int i = 0; i < loadedImages.Count; i++)
                    {
                        Mat aligned = AlignFocusSourceToBase(loadedImages[i], baseImage, width, height, i == baseImageIndex);
                        using Mat gray = new();
                        using Mat laplacian = new();
                        using Mat sharpness = new();
                        CvInvoke.CvtColor(aligned, gray, ColorConversion.Bgr2Gray);
                        CvInvoke.Laplacian(gray, laplacian, DepthType.Cv16S, 3);
                        CvInvoke.ConvertScaleAbs(laplacian, sharpness, 1.0, 0.0);
                        CvInvoke.GaussianBlur(sharpness, sharpness, new Size(17, 17), 0);

                        colorImages.Add(aligned.ToImage<Bgr, byte>());
                        scoreImages.Add(sharpness.ToImage<Gray, byte>());
                        aligned.Dispose();
                    }

                    using Image<Bgr, byte> output = FuseFocusStackSelective(colorImages, scoreImages, width, height, baseImageIndex);
                    using Bitmap bitmap = output.ToBitmap();
                    SaveJpeg(bitmap, outputImage, quality: 95L);
                    return File.Exists(outputImage);
                }
                finally
                {
                    foreach (InternalFocusSourceImage image in loadedImages)
                    {
                        image.Color.Dispose();
                    }

                    foreach (Image<Bgr, byte> image in colorImages)
                    {
                        image.Dispose();
                    }

                    foreach (Image<Gray, byte> image in scoreImages)
                    {
                        image.Dispose();
                    }
                }
            });
        }

        private sealed record InternalFocusSourceImage(
            string Path,
            Mat Color,
            Rectangle ObjectBounds,
            double MeanFocusScore);

        private static int FindBestGlobalFocusSourceImage(IReadOnlyList<InternalFocusSourceImage> images)
        {
            int bestIndex = 0;
            double bestScore = double.MinValue;
            for (int i = 0; i < images.Count; i++)
            {
                double score = images[i].MeanFocusScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static Mat AlignFocusSourceToBase(
            InternalFocusSourceImage source,
            InternalFocusSourceImage baseImage,
            int width,
            int height,
            bool isBaseImage)
        {
            if (isBaseImage)
            {
                return source.Color.Clone();
            }

            if (TryAlignFocusSourceByHomography(source.Color, baseImage.Color, width, height, out Mat? alignedByHomography) &&
                alignedByHomography != null)
            {
                return alignedByHomography;
            }

            return AlignFocusSourceByBounds(source, baseImage, width, height);
        }

        private static bool TryAlignFocusSourceByHomography(Mat source, Mat baseImage, int width, int height, out Mat? aligned)
        {
            aligned = null;

            using Mat sourceGray = new();
            using Mat baseGray = new();
            CvInvoke.CvtColor(source, sourceGray, ColorConversion.Bgr2Gray);
            CvInvoke.CvtColor(baseImage, baseGray, ColorConversion.Bgr2Gray);

            using ORB detector = new(1500);
            using VectorOfKeyPoint sourceKeypoints = new();
            using VectorOfKeyPoint baseKeypoints = new();
            using Mat sourceDescriptors = new();
            using Mat baseDescriptors = new();

            detector.DetectAndCompute(sourceGray, null, sourceKeypoints, sourceDescriptors, false);
            detector.DetectAndCompute(baseGray, null, baseKeypoints, baseDescriptors, false);

            if (sourceDescriptors.IsEmpty || baseDescriptors.IsEmpty || sourceKeypoints.Size < 12 || baseKeypoints.Size < 12)
            {
                return false;
            }

            using BFMatcher matcher = new(DistanceType.Hamming);
            using VectorOfVectorOfDMatch knnMatches = new();
            matcher.KnnMatch(sourceDescriptors, baseDescriptors, knnMatches, 2, null);

            MKeyPoint[] sourcePoints = sourceKeypoints.ToArray();
            MKeyPoint[] basePoints = baseKeypoints.ToArray();
            List<PointF> matchedSourcePoints = new();
            List<PointF> matchedBasePoints = new();

            for (int i = 0; i < knnMatches.Size; i++)
            {
                using VectorOfDMatch matchPair = knnMatches[i];
                if (matchPair.Size < 2)
                {
                    continue;
                }

                MDMatch[] matches = matchPair.ToArray();
                if (matches[0].Distance >= matches[1].Distance * 0.75)
                {
                    continue;
                }

                if (matches[0].QueryIdx < 0 || matches[0].QueryIdx >= sourcePoints.Length ||
                    matches[0].TrainIdx < 0 || matches[0].TrainIdx >= basePoints.Length)
                {
                    continue;
                }

                matchedSourcePoints.Add(sourcePoints[matches[0].QueryIdx].Point);
                matchedBasePoints.Add(basePoints[matches[0].TrainIdx].Point);
            }

            if (matchedSourcePoints.Count < 12)
            {
                return false;
            }

            using VectorOfPointF sourceVector = new(matchedSourcePoints.ToArray());
            using VectorOfPointF baseVector = new(matchedBasePoints.ToArray());
            using Mat homography = CvInvoke.FindHomography(sourceVector, baseVector, RobustEstimationAlgorithm.Ransac, 3);
            if (homography.IsEmpty)
            {
                return false;
            }

            aligned = new Mat();
            CvInvoke.WarpPerspective(
                source,
                aligned,
                homography,
                new Size(width, height),
                Inter.Linear,
                Warp.Default,
                BorderType.Constant,
                new MCvScalar(0, 0, 0));

            return true;
        }

        private static Mat AlignFocusSourceByBounds(
            InternalFocusSourceImage source,
            InternalFocusSourceImage baseImage,
            int width,
            int height)
        {
            if (source.ObjectBounds.Width <= 0 || source.ObjectBounds.Height <= 0 ||
                baseImage.ObjectBounds.Width <= 0 || baseImage.ObjectBounds.Height <= 0)
            {
                return source.Color.Clone();
            }

            double scaleX = baseImage.ObjectBounds.Width / (double)source.ObjectBounds.Width;
            double scaleY = baseImage.ObjectBounds.Height / (double)source.ObjectBounds.Height;
            double scale = Math.Clamp((scaleX + scaleY) * 0.5, 0.92, 1.08);

            double sourceCenterX = source.ObjectBounds.Left + source.ObjectBounds.Width * 0.5;
            double sourceCenterY = source.ObjectBounds.Top + source.ObjectBounds.Height * 0.5;
            double baseCenterX = baseImage.ObjectBounds.Left + baseImage.ObjectBounds.Width * 0.5;
            double baseCenterY = baseImage.ObjectBounds.Top + baseImage.ObjectBounds.Height * 0.5;

            double translateX = baseCenterX - sourceCenterX * scale;
            double translateY = baseCenterY - sourceCenterY * scale;

            using Matrix<double> transform = new(2, 3);
            transform[0, 0] = scale;
            transform[0, 1] = 0;
            transform[0, 2] = translateX;
            transform[1, 0] = 0;
            transform[1, 1] = scale;
            transform[1, 2] = translateY;

            Mat aligned = new();
            CvInvoke.WarpAffine(
                source.Color,
                aligned,
                transform,
                new Size(width, height),
                Inter.Linear,
                Warp.Default,
                BorderType.Constant,
                new MCvScalar(0, 0, 0));

            return aligned;
        }

        private static Rectangle FindObjectBounds(Image<Gray, byte> grayImage)
        {
            byte[,,] data = grayImage.Data;
            int width = grayImage.Width;
            int height = grayImage.Height;
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            const byte threshold = 8;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (data[y, x, 0] <= threshold)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return maxX < minX || maxY < minY
                ? Rectangle.Empty
                : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        private static double ComputeMeanFocusScore(Mat gray)
        {
            using Mat laplacian = new();
            using Mat sharpness = new();
            CvInvoke.Laplacian(gray, laplacian, DepthType.Cv16S, 3);
            CvInvoke.ConvertScaleAbs(laplacian, sharpness, 1.0, 0.0);
            MCvScalar mean = CvInvoke.Mean(sharpness);
            return mean.V0;
        }

        private static Image<Bgr, byte> FuseFocusStackSelective(
            IReadOnlyList<Image<Bgr, byte>> colorImages,
            IReadOnlyList<Image<Gray, byte>> scoreImages,
            int width,
            int height,
            int baseImageIndex)
        {
            var output = new Image<Bgr, byte>(width, height);
            byte[,,] outputData = output.Data;
            int imageCount = colorImages.Count;
            const double backgroundThreshold = 5.0;
            const double minimumScoreGain = 5.0;
            const double scoreGainRatio = 1.18;
            byte[,,] baseColor = colorImages[baseImageIndex].Data;
            byte[,,] baseScore = scoreImages[baseImageIndex].Data;
            bool[,] foregroundMask = BuildForegroundMask(baseColor, width, height, backgroundThreshold);
            bool[,] stableForegroundMask = ErodeMask(foregroundMask, width, height, radius: 3);
            byte[,] selectedIndices = new byte[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!foregroundMask[y, x] || !stableForegroundMask[y, x])
                    {
                        selectedIndices[y, x] = (byte)baseImageIndex;
                        continue;
                    }

                    double baseB = baseColor[y, x, 0];
                    double baseG = baseColor[y, x, 1];
                    double baseR = baseColor[y, x, 2];
                    double selectedScore = baseScore[y, x, 0];
                    int selectedIndex = baseImageIndex;

                    for (int i = 0; i < imageCount; i++)
                    {
                        if (i == baseImageIndex)
                        {
                            continue;
                        }

                        byte[,,] color = colorImages[i].Data;
                        byte[,,] score = scoreImages[i].Data;

                        double b = color[y, x, 0];
                        double g = color[y, x, 1];
                        double r = color[y, x, 2];
                        double luminance = 0.0722 * b + 0.7152 * g + 0.2126 * r;
                        if (luminance <= backgroundThreshold)
                        {
                            continue;
                        }

                        double candidateScore = score[y, x, 0];
                        if (candidateScore >= selectedScore * scoreGainRatio + minimumScoreGain)
                        {
                            selectedScore = candidateScore;
                            selectedIndex = i;
                        }
                    }

                    selectedIndices[y, x] = (byte)selectedIndex;
                }
            }

            selectedIndices = SmoothSelectionMap(
                selectedIndices,
                foregroundMask,
                stableForegroundMask,
                width,
                height,
                imageCount,
                radius: 2,
                passes: 2);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!foregroundMask[y, x])
                    {
                        outputData[y, x, 0] = 0;
                        outputData[y, x, 1] = 0;
                        outputData[y, x, 2] = 0;
                    }
                    else
                    {
                        byte[,,] selectedColor = colorImages[selectedIndices[y, x]].Data;
                        outputData[y, x, 0] = selectedColor[y, x, 0];
                        outputData[y, x, 1] = selectedColor[y, x, 1];
                        outputData[y, x, 2] = selectedColor[y, x, 2];
                    }
                }
            }

            return output;
        }

        private static bool[,] BuildForegroundMask(byte[,,] color, int width, int height, double backgroundThreshold)
        {
            bool[,] mask = new bool[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double b = color[y, x, 0];
                    double g = color[y, x, 1];
                    double r = color[y, x, 2];
                    double luminance = 0.0722 * b + 0.7152 * g + 0.2126 * r;
                    mask[y, x] = luminance > backgroundThreshold;
                }
            }

            return mask;
        }

        private static bool[,] ErodeMask(bool[,] mask, int width, int height, int radius)
        {
            bool[,] eroded = new bool[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[y, x])
                    {
                        continue;
                    }

                    bool keep = true;
                    for (int yy = Math.Max(0, y - radius); yy <= Math.Min(height - 1, y + radius) && keep; yy++)
                    {
                        for (int xx = Math.Max(0, x - radius); xx <= Math.Min(width - 1, x + radius); xx++)
                        {
                            if (!mask[yy, xx])
                            {
                                keep = false;
                                break;
                            }
                        }
                    }

                    eroded[y, x] = keep;
                }
            }

            return eroded;
        }

        private static byte[,] SmoothSelectionMap(
            byte[,] selectedIndices,
            bool[,] foregroundMask,
            bool[,] stableForegroundMask,
            int width,
            int height,
            int imageCount,
            int radius,
            int passes)
        {
            byte[,] current = selectedIndices;
            for (int pass = 0; pass < passes; pass++)
            {
                byte[,] next = (byte[,])current.Clone();
                int[] counts = new int[Math.Max(imageCount, 1)];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!foregroundMask[y, x] || !stableForegroundMask[y, x])
                        {
                            continue;
                        }

                        Array.Clear(counts, 0, counts.Length);
                        int totalCount = 0;
                        for (int yy = Math.Max(0, y - radius); yy <= Math.Min(height - 1, y + radius); yy++)
                        {
                            for (int xx = Math.Max(0, x - radius); xx <= Math.Min(width - 1, x + radius); xx++)
                            {
                                if (!foregroundMask[yy, xx] || !stableForegroundMask[yy, xx])
                                {
                                    continue;
                                }

                                counts[current[yy, xx]]++;
                                totalCount++;
                            }
                        }

                        int bestIndex = current[y, x];
                        int bestCount = counts[bestIndex];
                        for (int i = 0; i < counts.Length; i++)
                        {
                            if (counts[i] > bestCount)
                            {
                                bestIndex = i;
                                bestCount = counts[i];
                            }
                        }

                        if (bestIndex != current[y, x] && totalCount > 0 && bestCount >= Math.Ceiling(totalCount * 0.6))
                        {
                            next[y, x] = (byte)bestIndex;
                        }
                    }
                }

                current = next;
            }

            return current;
        }

        private static void SaveJpeg(Bitmap bitmap, string outputPath, long quality)
        {
            ImageCodecInfo? jpegCodec = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

            if (jpegCodec == null)
            {
                bitmap.Save(outputPath, ImageFormat.Jpeg);
                return;
            }

            using EncoderParameters encoderParameters = new(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            bitmap.Save(outputPath, jpegCodec, encoderParameters);
        }
    }
}
