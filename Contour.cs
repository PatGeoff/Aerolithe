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

        //private Bitmap BrightnessMaskFromStream(MemoryStream stream, int threshold = 100)
        //{
        //    // Decode JPEG stream into Mat
        //    byte[] imageBytes = stream.ToArray();
        //    Mat image = new Mat();            
        //    CvInvoke.Imdecode(imageBytes, ImreadModes.Color, image);

        //    // Convert to grayscale
        //    Mat gray = new Mat();
        //    CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);

        //    // Apply binary threshold
        //    Mat binary = new Mat();
        //    CvInvoke.Threshold(gray, binary, threshold, 255, ThresholdType.Binary);

        //    return binary.ToBitmap();
        //}


        private Bitmap BrightnessMaskFromBytes(byte[] jpegBuffer, int threshold = 100)
        {
            // Decode JPEG buffer directly into Mat
            Mat image = new Mat();
            CvInvoke.Imdecode(jpegBuffer, ImreadModes.Color, image);

            // Convert to grayscale
            Mat gray = new Mat();
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);

            // Apply binary threshold
            Mat binary = new Mat();
            CvInvoke.Threshold(gray, binary, threshold, 255, ThresholdType.Binary);

            return binary.ToBitmap();
        }

        private async Task<Mat> BrightnessMaskFromBytesMat(
    byte[] jpegBuffer,
    int threshold = 100,
    bool invert = false)
        {
            return appSettings.MaskAlgorithmIndex switch
            {
                2 => await AdaptiveBackgroundMaskFromBytesMat(jpegBuffer, threshold, invert),
                1 => await EdgeAssistedBrightnessMaskFromBytesMat(jpegBuffer, threshold, invert),
                _ => await CorrectedBrightnessMaskFromBytesMat(jpegBuffer, threshold, invert),
            };
        }

        private async Task<Mat> CorrectedBrightnessMaskFromBytesMat(
    byte[] jpegBuffer,
    int threshold = 100,
    bool invert = false)
        {
            if (jpegBuffer == null || jpegBuffer.Length == 0)
                throw new ArgumentException("jpegBuffer est nul ou vide.", nameof(jpegBuffer));

            return await Task.Run(() =>
            {
                using var color = new Mat();
                CvInvoke.Imdecode(jpegBuffer, ImreadModes.Color, color);
                if (color.IsEmpty)
                    throw new InvalidOperationException("Échec du décodage JPEG.");

                int h = color.Rows;
                int w = color.Cols;

                using var gray = new Mat();
                CvInvoke.CvtColor(color, gray, ColorConversion.Bgr2Gray);

                using var smooth = new Mat();
                CvInvoke.GaussianBlur(gray, smooth, new Size(5, 5), 0);

                using var otsuMask = new Mat();
                double otsu = CvInvoke.Threshold(smooth, otsuMask, 0, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);

                using var darkMask = new Mat();
                if (threshold >= 0)
                {
                    int thresholdOffset = Math.Max(-80, Math.Min(120, threshold - 20));
                    double effectiveThreshold = Math.Max(5, Math.Min(250, otsu + thresholdOffset));
                    CvInvoke.Threshold(smooth, darkMask, effectiveThreshold, 255, ThresholdType.BinaryInv);
                }
                else
                {
                    otsuMask.CopyTo(darkMask);
                }

                using var hsv = new Mat();
                CvInvoke.CvtColor(color, hsv, ColorConversion.Bgr2Hsv);
                using var hsvChannels = new VectorOfMat();
                CvInvoke.Split(hsv, hsvChannels);
                using var saturation = hsvChannels[1];
                using var value = hsvChannels[2];

                using var satMask = new Mat();
                CvInvoke.Threshold(saturation, satMask, 18, 255, ThresholdType.Binary);

                using var notTooBright = new Mat();
                CvInvoke.Threshold(value, notTooBright, 230, 255, ThresholdType.BinaryInv);

                using var textureMask = new Mat();
                CvInvoke.BitwiseAnd(satMask, notTooBright, textureMask);

                using var bin = new Mat();
                CvInvoke.BitwiseOr(darkMask, textureMask, bin);

                int openKernelSize = Math.Max(3, (int)Math.Round(Math.Max(w, h) * 0.004));
                openKernelSize = Math.Min(openKernelSize, 11);
                if ((openKernelSize & 1) == 0) openKernelSize++;

                int closeKernelSize = Math.Max(9, (int)Math.Round(Math.Max(w, h) * 0.012));
                closeKernelSize = Math.Min(closeKernelSize, 35);
                if ((closeKernelSize & 1) == 0) closeKernelSize++;

                using var kOpen = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(openKernelSize, openKernelSize), new Point(-1, -1));
                using var kClose = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(closeKernelSize, closeKernelSize), new Point(-1, -1));
                CvInvoke.MorphologyEx(bin, bin, MorphOp.Open, kOpen, new Point(-1, -1), 1, BorderType.Reflect, default);
                CvInvoke.MorphologyEx(bin, bin, MorphOp.Close, kClose, new Point(-1, -1), 2, BorderType.Reflect, default);

                using var solid = SolidMaskFromBestComponent(bin, w, h);
                if (invert) CvInvoke.BitwiseNot(solid, solid);

                return solid.Clone();
            });
        }

        private async Task<Mat> RawBrightnessMaskFromBytesMat(
            byte[] jpegBuffer,
            int threshold = 100,
            bool invert = false)
        {
            if (jpegBuffer == null || jpegBuffer.Length == 0)
                throw new ArgumentException("jpegBuffer est nul ou vide.", nameof(jpegBuffer));

            return await Task.Run(() =>
            {
                using var gray = new Mat();
                CvInvoke.Imdecode(jpegBuffer, ImreadModes.Grayscale, gray);
                if (gray.IsEmpty)
                    throw new InvalidOperationException("Échec du décodage JPEG.");

                using var smooth = new Mat();
                CvInvoke.GaussianBlur(gray, smooth, new Size(3, 3), 0);

                using var bin = new Mat();
                if (threshold < 0)
                {
                    CvInvoke.Threshold(smooth, bin, 0, 255, (invert ? ThresholdType.BinaryInv : ThresholdType.Binary) | ThresholdType.Otsu);
                }
                else
                {
                    threshold = Math.Max(0, Math.Min(255, threshold));
                    CvInvoke.Threshold(smooth, bin, threshold, 255, invert ? ThresholdType.BinaryInv : ThresholdType.Binary);
                }

                using var k3 = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));
                CvInvoke.MorphologyEx(bin, bin, MorphOp.Open, k3, new Point(-1, -1), 1, BorderType.Reflect, default);
                CvInvoke.MorphologyEx(bin, bin, MorphOp.Close, k3, new Point(-1, -1), 2, BorderType.Reflect, default);

                return SolidMaskFromBestComponent(bin, gray.Cols, gray.Rows);
            });
        }

        private async Task<Mat> EdgeAssistedBrightnessMaskFromBytesMat(
            byte[] jpegBuffer,
            int threshold = 100,
            bool invert = false)
        {
            if (jpegBuffer == null || jpegBuffer.Length == 0)
                throw new ArgumentException("jpegBuffer est nul ou vide.", nameof(jpegBuffer));

            return await Task.Run(() =>
            {
                using var color = new Mat();
                CvInvoke.Imdecode(jpegBuffer, ImreadModes.Color, color);
                if (color.IsEmpty)
                    throw new InvalidOperationException("Échec du décodage JPEG.");

                int h = color.Rows;
                int w = color.Cols;

                using var gray = new Mat();
                CvInvoke.CvtColor(color, gray, ColorConversion.Bgr2Gray);

                using var smooth = new Mat();
                CvInvoke.GaussianBlur(gray, smooth, new Size(5, 5), 0);

                using var lumBin = new Mat();
                if (threshold < 0)
                {
                    CvInvoke.Threshold(smooth, lumBin, 0, 255, (invert ? ThresholdType.BinaryInv : ThresholdType.Binary) | ThresholdType.Otsu);
                }
                else
                {
                    threshold = Math.Max(0, Math.Min(255, threshold));
                    CvInvoke.Threshold(smooth, lumBin, threshold, 255, invert ? ThresholdType.BinaryInv : ThresholdType.Binary);
                }

                using var edges = new Mat();
                CvInvoke.Canny(smooth, edges, 40, 120);

                int edgeKernelSize = Math.Max(3, (int)Math.Round(Math.Max(w, h) * 0.004));
                edgeKernelSize = Math.Min(edgeKernelSize, 15);
                if ((edgeKernelSize & 1) == 0) edgeKernelSize++;

                using var kEdge = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(edgeKernelSize, edgeKernelSize), new Point(-1, -1));
                CvInvoke.Dilate(edges, edges, kEdge, new Point(-1, -1), 1, BorderType.Reflect, default);

                using var combined = new Mat();
                CvInvoke.BitwiseOr(lumBin, edges, combined);

                using var hsv = new Mat();
                CvInvoke.CvtColor(color, hsv, ColorConversion.Bgr2Hsv);
                using var hsvChannels = new VectorOfMat();
                CvInvoke.Split(hsv, hsvChannels);
                using var saturation = hsvChannels[1];
                using var satBin = new Mat();
                CvInvoke.Threshold(saturation, satBin, 35, 255, ThresholdType.Binary);

                using var kSat = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(5, 5), new Point(-1, -1));
                CvInvoke.MorphologyEx(satBin, satBin, MorphOp.Close, kSat, new Point(-1, -1), 2, BorderType.Reflect, default);
                CvInvoke.BitwiseOr(combined, satBin, combined);

                int closeKernelSize = Math.Max(7, (int)Math.Round(Math.Max(w, h) * 0.012));
                closeKernelSize = Math.Min(closeKernelSize, 31);
                if ((closeKernelSize & 1) == 0) closeKernelSize++;

                using var kClose = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(closeKernelSize, closeKernelSize), new Point(-1, -1));
                CvInvoke.MorphologyEx(combined, combined, MorphOp.Close, kClose, new Point(-1, -1), 2, BorderType.Reflect, default);

                int openKernelSize = Math.Max(5, (int)Math.Round(Math.Max(w, h) * 0.007));
                openKernelSize = Math.Min(openKernelSize, 17);
                if ((openKernelSize & 1) == 0) openKernelSize++;

                using var kOpen = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(openKernelSize, openKernelSize), new Point(-1, -1));
                CvInvoke.MorphologyEx(combined, combined, MorphOp.Open, kOpen, new Point(-1, -1), 1, BorderType.Reflect, default);

                return SolidMaskFromBestComponent(combined, w, h);
            });
        }

        private async Task<Mat> AdaptiveBackgroundMaskFromBytesMat(
            byte[] jpegBuffer,
            int threshold = 100,
            bool invert = false)
        {
            if (jpegBuffer == null || jpegBuffer.Length == 0)
                throw new ArgumentException("jpegBuffer est nul ou vide.", nameof(jpegBuffer));

            return await Task.Run(() =>
            {
                using var color = new Mat();
                CvInvoke.Imdecode(jpegBuffer, ImreadModes.Color, color);
                if (color.IsEmpty)
                    throw new InvalidOperationException("Échec du décodage JPEG.");

                int h = color.Rows;
                int w = color.Cols;

                using var lab = new Mat();
                CvInvoke.CvtColor(color, lab, ColorConversion.Bgr2Lab);

                int border = Math.Max(8, (int)Math.Round(Math.Min(w, h) * 0.04));
                border = Math.Min(border, Math.Max(1, Math.Min(w, h) / 4));

                using var borderMask = Mat.Zeros(h, w, DepthType.Cv8U, 1);
                CvInvoke.Rectangle(borderMask, new Rectangle(0, 0, w, border), new MCvScalar(255), -1);
                CvInvoke.Rectangle(borderMask, new Rectangle(0, h - border, w, border), new MCvScalar(255), -1);
                CvInvoke.Rectangle(borderMask, new Rectangle(0, 0, border, h), new MCvScalar(255), -1);
                CvInvoke.Rectangle(borderMask, new Rectangle(w - border, 0, border, h), new MCvScalar(255), -1);

                MCvScalar background = CvInvoke.Mean(lab, borderMask);

                using var channels = new VectorOfMat();
                CvInvoke.Split(lab, channels);
                using var lDiff = new Mat();
                using var aDiff = new Mat();
                using var bDiff = new Mat();
                CvInvoke.AbsDiff(channels[0], new ScalarArray(background.V0), lDiff);
                CvInvoke.AbsDiff(channels[1], new ScalarArray(background.V1), aDiff);
                CvInvoke.AbsDiff(channels[2], new ScalarArray(background.V2), bDiff);

                using var colorDistance = new Mat();
                CvInvoke.AddWeighted(lDiff, 1.0, aDiff, 1.2, 0.0, colorDistance);
                CvInvoke.AddWeighted(colorDistance, 1.0, bDiff, 1.2, 0.0, colorDistance);

                using var candidate = new Mat();
                if (threshold < 0)
                {
                    CvInvoke.Threshold(colorDistance, candidate, 0, 255, ThresholdType.Binary | ThresholdType.Otsu);
                }
                else
                {
                    threshold = Math.Max(0, Math.Min(255, threshold));
                    double distanceThreshold = Math.Max(8.0, Math.Min(95.0, 10.0 + threshold * 0.45));
                    CvInvoke.Threshold(colorDistance, candidate, distanceThreshold, 255, ThresholdType.Binary);
                }

                CvInvoke.Rectangle(candidate, new Rectangle(0, 0, w, border), new MCvScalar(0), -1);
                CvInvoke.Rectangle(candidate, new Rectangle(0, h - border, w, border), new MCvScalar(0), -1);
                CvInvoke.Rectangle(candidate, new Rectangle(0, 0, border, h), new MCvScalar(0), -1);
                CvInvoke.Rectangle(candidate, new Rectangle(w - border, 0, border, h), new MCvScalar(0), -1);

                int openKernelSize = Math.Max(3, (int)Math.Round(Math.Max(w, h) * 0.004));
                openKernelSize = Math.Min(openKernelSize, 13);
                if ((openKernelSize & 1) == 0) openKernelSize++;

                int closeKernelSize = Math.Max(9, (int)Math.Round(Math.Max(w, h) * 0.014));
                closeKernelSize = Math.Min(closeKernelSize, 39);
                if ((closeKernelSize & 1) == 0) closeKernelSize++;

                using var kOpen = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(openKernelSize, openKernelSize), new Point(-1, -1));
                using var kClose = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(closeKernelSize, closeKernelSize), new Point(-1, -1));
                CvInvoke.MorphologyEx(candidate, candidate, MorphOp.Open, kOpen, new Point(-1, -1), 1, BorderType.Reflect, default);
                CvInvoke.MorphologyEx(candidate, candidate, MorphOp.Close, kClose, new Point(-1, -1), 2, BorderType.Reflect, default);

                using var solid = SolidMaskFromBestComponent(candidate, w, h);
                if (invert) CvInvoke.BitwiseNot(solid, solid);

                return solid.Clone();
            });
        }

        private static Mat SolidMaskFromBestComponent(Mat binaryMask, int w, int h)
        {
            using var labels = new Mat();
            using var stats = new Mat();
            using var centroids = new Mat();
            int n = CvInvoke.ConnectedComponentsWithStats(binaryMask, labels, stats, centroids, LineType.EightConnected, DepthType.Cv32S);

            if (n <= 1)
            {
                using var filled = FillByExternalContours(binaryMask);
                return filled.Clone();
            }

            var statsArr = stats.GetData();
            var centArr = centroids.GetData();
            int bestIdx = SelectBestObjectComponent(statsArr, centArr, n, w, h);

            using var finalMask = new Mat();
            CvInvoke.InRange(labels, new ScalarArray(bestIdx), new ScalarArray(bestIdx), finalMask);

            using var filledBest = FillByExternalContours(finalMask);
            using var solidBest = FillMaskHoles(filledBest);
            return solidBest.Clone();
        }

        private static int SelectBestObjectComponent(Array statsArr, Array centArr, int componentCount, int imageWidth, int imageHeight)
        {
            int bestIdx = 1;
            double bestScore = double.NegativeInfinity;
            double targetX = imageWidth * 0.5;
            double targetY = imageHeight * 0.43;
            int fallbackIdx = 1;
            double fallbackScore = double.NegativeInfinity;

            for (int i = 1; i < componentCount; i++)
            {
                int left = (int)statsArr.GetValue(i, (int)ConnectedComponentsTypes.Left);
                int top = (int)statsArr.GetValue(i, (int)ConnectedComponentsTypes.Top);
                int width = (int)statsArr.GetValue(i, (int)ConnectedComponentsTypes.Width);
                int height = (int)statsArr.GetValue(i, (int)ConnectedComponentsTypes.Height);
                int area = (int)statsArr.GetValue(i, (int)ConnectedComponentsTypes.Area);

                if (area <= 0 || width <= 0 || height <= 0)
                    continue;

                double ccx = (double)centArr.GetValue(i, 0);
                double ccy = (double)centArr.GetValue(i, 1);
                double widthRatio = width / (double)imageWidth;
                double heightRatio = height / (double)imageHeight;
                double bottom = top + height;
                bool touchesBottom = bottom >= imageHeight - 2;
                bool touchesSide = left <= 1 || left + width >= imageWidth - 2;

                bool likelyTurntable =
                    (widthRatio > 0.45 && heightRatio < 0.35 && ccy > imageHeight * 0.45) ||
                    (touchesBottom && widthRatio > 0.30) ||
                    (touchesSide && ccy > imageHeight * 0.55);

                bool touchesFrameTooMuch =
                    left <= 1 ||
                    top <= 1 ||
                    left + width >= imageWidth - 2 ||
                    bottom >= imageHeight - 2;

                double dx = Math.Abs(ccx - targetX);
                double dy = Math.Abs(ccy - targetY);
                double compactness = Math.Min(width, height) / (double)Math.Max(width, height);
                double score = Math.Sqrt(area) * 12.0
                               + compactness * 250.0
                               - dx * 1.4
                               - dy * 1.1;

                if (score > fallbackScore)
                {
                    fallbackScore = score;
                    fallbackIdx = i;
                }

                if (likelyTurntable && componentCount > 2)
                {
                    continue;
                }

                if (likelyTurntable)
                    score -= 20000.0;

                if (touchesFrameTooMuch)
                    score -= 4000.0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }

            return bestScore > double.NegativeInfinity ? bestIdx : fallbackIdx;
        }

        private static Mat FillMaskHoles(Mat binaryMask)
        {
            int h = binaryMask.Rows;
            int w = binaryMask.Cols;

            using var mask = new Mat();
            binaryMask.CopyTo(mask);

            using var exterior = new Mat();
            CvInvoke.BitwiseNot(mask, exterior);

            using var floodFilled = exterior.Clone();
            Rectangle bbox;
            CvInvoke.FloodFill(
                floodFilled,
                null,
                new Point(0, 0),
                new MCvScalar(0),
                out bbox,
                new MCvScalar(0),
                new MCvScalar(0),
                Connectivity.EightConnected,
                FloodFillType.Default);

            using var holes = new Mat();
            CvInvoke.Threshold(floodFilled, holes, 254, 255, ThresholdType.Binary);

            var filled = new Mat();
            CvInvoke.BitwiseOr(mask, holes, filled);
            return filled;
        }

        private async Task<Bitmap> BrightnessMaskFromBytes(
            byte[] jpegBuffer,
            int threshold = 100,
            bool invert = false)
        {
            if (jpegBuffer == null || jpegBuffer.Length == 0)
                throw new ArgumentException("jpegBuffer est nul ou vide.", nameof(jpegBuffer));

            return await Task.Run(() =>
            {
                // 1) Decode en niveaux de gris (1 canal)
                using var gray = new Mat();
                CvInvoke.Imdecode(jpegBuffer, ImreadModes.Grayscale, gray);
                if (gray.IsEmpty)
                    throw new InvalidOperationException("Échec du décodage JPEG.");

                int h = gray.Rows;
                int w = gray.Cols;

                // 2) Correction d’illumination (flou large) pour neutraliser ombres/coins gris
                int k = Math.Max(31, (int)(Math.Max(w, h) * 0.03));  // ~3% de la grande dimension
                if ((k & 1) == 0) k++;                                // kernel impair
                using var illum = new Mat();
                CvInvoke.GaussianBlur(gray, illum, new Size(k, k), k * 0.5);

                using var norm = new Mat();
                CvInvoke.Subtract(gray, illum, norm);
                CvInvoke.Normalize(norm, norm, 0, 255, NormType.MinMax, DepthType.Cv8U);

                // 3) Petit lissage pour calmer le bruit
                CvInvoke.GaussianBlur(norm, norm, new Size(3, 3), 0);

                // 4) Seuillage
                //    - threshold < 0 → Otsu auto (objet sombre → BinaryInv)
                //    - sinon manuel selon 'invert'
                using var bin = new Mat();
                if (threshold < 0)
                {
                    CvInvoke.Threshold(norm, bin, 0, 255, (invert ? ThresholdType.BinaryInv : ThresholdType.Binary) | ThresholdType.Otsu);
                }
                else
                {
                    threshold = Math.Max(0, Math.Min(255, threshold));
                    var t = invert ? ThresholdType.BinaryInv : ThresholdType.Binary;
                    CvInvoke.Threshold(norm, bin, threshold, 255, t);
                }

                // 5) Morphologie légère pour lisser (coût faible)
                using var k3 = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));
                CvInvoke.MorphologyEx(bin, bin, MorphOp.Open, k3, new Point(-1, -1), 1, BorderType.Reflect, default);
                CvInvoke.MorphologyEx(bin, bin, MorphOp.Close, k3, new Point(-1, -1), 1, BorderType.Reflect, default);

                // 6) Connected Components: garder le meilleur blob (aire - pénalité distance centre)
                using var labels = new Mat();      // CV_32S
                using var stats = new Mat();       // CV_32S
                using var centroids = new Mat();   // CV_64F
                int n = CvInvoke.ConnectedComponentsWithStats(bin, labels, stats, centroids, LineType.EightConnected, DepthType.Cv32S);

                // Si rien trouvé (n<=1 → seulement le fond), on tente quand même de remplir via contours externes
                if (n <= 1)
                {
                    using var filled = FillByExternalContours(bin);
                    if (invert) CvInvoke.BitwiseNot(filled, filled);
                    return filled.ToBitmap();
                }

                int bestIdx = -1;
                double bestScore = double.NegativeInfinity;
                float cxImg = w / 2f;
                float cyImg = h / 2f;

                var statsArr = stats.GetData();
                var centArr = centroids.GetData();

                for (int i = 1; i < n; i++) // 0 = fond
                {
                    int area = (int)statsArr.GetValue(i, (int)ConnectedComponentsTypes.Area);
                    double ccx = (double)centArr.GetValue(i, 0);
                    double ccy = (double)centArr.GetValue(i, 1);
                    double dist2 = (ccx - cxImg) * (ccx - cxImg) + (ccy - cyImg) * (ccy - cyImg);

                    // Score simple & peu coûteux
                    double score = area - 0.1 * dist2;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIdx = i;
                    }
                }

                // 7) Masque du meilleur label (plein 0/255)
                using var finalMask = new Mat();
                CvInvoke.InRange(labels, new ScalarArray(bestIdx), new ScalarArray(bestIdx), finalMask);


                // Stats du meilleur composant
                int left = (int)statsArr.GetValue(bestIdx, (int)ConnectedComponentsTypes.Left);
                int top = (int)statsArr.GetValue(bestIdx, (int)ConnectedComponentsTypes.Top);
                int width = (int)statsArr.GetValue(bestIdx, (int)ConnectedComponentsTypes.Width);
                int height = (int)statsArr.GetValue(bestIdx, (int)ConnectedComponentsTypes.Height);

                int right = left + width - 1;
                int bottom = top + height - 1;

                // 7bis) Fermer contre les bords du frame si touchés
                // (on "coud" le masque sur le bord pour que le contour devienne fermable)
                if (left <= 0)
                    CvInvoke.Line(finalMask, new Point(0, top), new Point(0, bottom), new MCvScalar(255), 1);

                if (right >= w - 1)
                    CvInvoke.Line(finalMask, new Point(w - 1, top), new Point(w - 1, bottom), new MCvScalar(255), 1);

                if (top <= 0)
                    CvInvoke.Line(finalMask, new Point(left, 0), new Point(right, 0), new MCvScalar(255), 1);

                if (bottom >= h - 1)
                    CvInvoke.Line(finalMask, new Point(left, h - 1), new Point(right, h - 1), new MCvScalar(255), 1);


                // 7ter) GAP SEALER — reconstruction morphologique (robuste aux grands gaps)
                int seal = Math.Max(1, (int)Math.Round(Math.Max(w, h) * 0.006)); // ~0.6% de la grande dim.
                seal = Math.Min(seal, 25);
                if ((seal & 1) == 0) seal++; // noyau impair

                using var kSeal = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(seal, seal), new Point(-1, -1));

                // 7ter.a) Épaissir le trait pour obturer les fuites
                using var edges = new Mat();
                CvInvoke.Dilate(finalMask, edges, kSeal, new Point(-1, -1), 1, BorderType.Reflect, default);

                // 7ter.b) Complément : fond blanc, trait noir
                using var comp = new Mat();
                CvInvoke.BitwiseNot(edges, comp);

                // 7ter.c) Flood fill depuis le bord pour marquer l'extérieur
                using var ff = comp.Clone(); // CV_8U
                Rectangle _bbox;
                CvInvoke.FloodFill(
                    ff,
                    null,               // mask nul (OpenCV construit en interne)
                    new Point(0, 0),    // seed sur le bord (assure-toi que (0,0) est background)
                    new MCvScalar(0),   // on peint l'extérieur en 0
                    out _bbox,
                    new MCvScalar(0),   // loDiff=0 → correspondance stricte
                    new MCvScalar(0),   // upDiff=0
                    Connectivity.EightConnected,     // <-- paramètre séparé
                    FloodFillType.Default            // <-- paramètre séparé
                );

                // 8) Les pixels restés à 255 dans 'ff' sont l'intérieur fermé
                using var solid = new Mat();
                CvInvoke.Threshold(ff, solid, 254, 255, ThresholdType.Binary);

                // 9) (Optionnel) Retract pour compenser la dilatation
                CvInvoke.Erode(solid, solid, kSeal, new Point(-1, -1), 1, BorderType.Reflect, default);

                // 10) Inversion éventuelle selon 'invert'
                if (invert) CvInvoke.BitwiseNot(solid, solid);

                return solid.ToBitmap();


               


            });
        }



        private static Mat FillByExternalContours(Mat binaryMask /* CV_8U, 0/255 */)
        {
            int h = binaryMask.Rows, w = binaryMask.Cols;

            using var contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(binaryMask, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

            var filled = Mat.Zeros(h, w, DepthType.Cv8U, 1); // sortie

            if (contours.Size > 0)
            {
                // Dessine tous les contours externes en « rempli » (thickness=-1)
                CvInvoke.DrawContours(filled, contours, -1, new MCvScalar(255), thickness: -1, lineType: LineType.EightConnected);
            }
            else
            {
                // Aucun contour → copie brute (évite de renvoyer un noir si bin valait qqch)
                binaryMask.CopyTo(filled);
            }

            return filled;
        }



        public void PostFocusStackMask()
        {
            if (File.Exists(focusStackOutputPath))
            {
                string maskPath = projet.GetMaskFullImagePath();
                if (!File.Exists(maskPath))
                {
                    MessageBox.Show("Masque introuvable : " + maskPath);
                    AppendTextToConsoleNL("PostFocusStackMask: masque introuvable : " + maskPath);
                    return;
                }

                using (var originalBitmap = new Bitmap(focusStackOutputPath))
                using (var savedMask = LoadSavedMaskAsGrayMat(maskPath))
                {
                    Bitmap finalBitmap = ApplyMask(originalBitmap, savedMask);

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
                    AppendTextToConsoleNL("PostFocusStackMask: masque sauvegardé appliqué : " + maskPath);
                }
            }

        }

        private Mat LoadSavedMaskAsGrayMat(string maskPath)
        {
            using var mask = CvInvoke.Imread(maskPath, ImreadModes.Unchanged);
            if (mask == null || mask.IsEmpty)
            {
                throw new InvalidOperationException("Masque sauvegardé nul ou vide : " + maskPath);
            }

            var gray = new Mat();

            if (mask.NumberOfChannels == 4)
            {
                using var channels = new VectorOfMat();
                CvInvoke.Split(mask, channels);
                channels[3].CopyTo(gray);
            }
            else if (mask.NumberOfChannels == 3)
            {
                CvInvoke.CvtColor(mask, gray, ColorConversion.Bgr2Gray);
            }
            else
            {
                mask.CopyTo(gray);
            }

            return gray;
        }
    }

}
