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
                    CvInvoke.Threshold(norm, bin, 0, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);
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


                /////////////


                //// 7ter) GAP SEALER — reconstruction morphologique (robuste aux grands gaps)
                //int seal = Math.Max(1, (int)Math.Round(Math.Max(w, h) * 0.006)); // ~0.6%
                //seal = Math.Min(seal, 25);
                //if ((seal & 1) == 0) seal++; // noyau impair

                //using var kSeal = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(seal, seal), new Point(-1, -1));

                //// 7ter.a) Épaissir le trait pour obturer les fuites
                //using var edges = new Mat();
                //CvInvoke.Dilate(finalMask, edges, kSeal, new Point(-1, -1), 1, BorderType.Reflect, default);

                //// 7ter.b) Complément : zones libres = 255, trait = 0
                //using var comp = new Mat();
                //CvInvoke.BitwiseNot(edges, comp);

                //// *** Correctif clé : pad 1 px pour garantir la connexité de l’extérieur ***
                //using var padded = new Mat();
                //CvInvoke.CopyMakeBorder(comp, padded, 1, 1, 1, 1, BorderType.Constant, new MCvScalar(0));

                //// 7ter.c) Flood fill depuis (0,0) dans l’image paddée (extérieur)
                //Rectangle bboxIgnore;
                //CvInvoke.FloodFill(
                //    padded,
                //    null,
                //    new Point(0, 0),
                //    new MCvScalar(0),  // on peint l’extérieur en 0
                //    out bboxIgnore,
                //    new MCvScalar(0),
                //    new MCvScalar(0),
                //    Connectivity.EightConnected,
                //    FloodFillType.Default
                //);

                //// Recadrer le padding pour revenir à la taille d’origine
                //var roi = new Rectangle(1, 1, w, h);
                //using var ff = new Mat(padded, roi);

                //// 8) Les pixels restés à 255 sont l’intérieur fermé
                //using var solid = new Mat();
                //CvInvoke.Threshold(ff, solid, 254, 255, ThresholdType.Binary);

                //// 9) Rétraction (optionnelle) pour compenser la dilatation
                //CvInvoke.Erode(solid, solid, kSeal, new Point(-1, -1), 1, BorderType.Reflect, default);

                //// 10) Inversion éventuelle selon 'invert'
                //if (invert) CvInvoke.BitwiseNot(solid, solid);

                //return solid.ToBitmap();


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
                using (var originalBitmap = new Bitmap(focusStackOutputPath))
                {
                    Bitmap finalBitmap;

                    var sourceImage = originalBitmap.ToImage<Emgu.CV.Structure.Bgr, byte>();
                    var maskGray = maskBitmapLive.ToImage<Emgu.CV.Structure.Gray, byte>();

                    var resizedMask = maskGray.Resize(sourceImage.Width, sourceImage.Height, Emgu.CV.CvEnum.Inter.Linear);
                    var invertedMask = resizedMask;
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
