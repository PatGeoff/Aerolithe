using Emgu.CV;
using Emgu.CV.Util;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public bool calculerCentre = false;
        private int _autoCenterOffsetTrackingRequests = 0;
        private long _autoCenterOffsetsVersion = 0;
        private OffsetsData offsets = new OffsetsData();
        bool cancelAutoCentrage = false;
        bool cameraRailFarLimitSwitchPressed = false;
        bool cameraRailNearLimitSwitchPressed = false;


        private async Task CalculeDuCentrageAsync(Bitmap maskBitmap, int polarity /* 0: noir, 1: blanc */)
        {
            await Task.Run(() =>
            {
                int width = maskBitmap.Width;
                int height = maskBitmap.Height;

                int minX = width, maxX = -1, minY = height, maxY = -1;
                bool hasFgOnBorder = false;

                var rect = new Rectangle(0, 0, width, height);
                var bmpData = maskBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, maskBitmap.PixelFormat);

                try
                {
                    int bpp = Image.GetPixelFormatSize(maskBitmap.PixelFormat) / 8;
                    int stride = bmpData.Stride;
                    int totalBytes = stride * height;
                    byte[] pixelBuffer = new byte[totalBytes];

                    System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, pixelBuffer, 0, totalBytes);

                    bool fgIsBlack = (polarity == 0);

                    for (int y = 0; y < height; y++)
                    {
                        int rowStart = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int index = rowStart + x * bpp;
                            if (index + 2 >= pixelBuffer.Length) break;

                            byte b = pixelBuffer[index + 0];
                            byte g = pixelBuffer[index + 1];
                            byte r = pixelBuffer[index + 2];
                            // Si ARGB (bpp == 4), alpha = pixelBuffer[index + 3] (inutile ici)

                            bool isForeground =
                                fgIsBlack
                                ? (r == 0 && g == 0 && b == 0)
                                : (r == 255 && g == 255 && b == 255);

                            if (!isForeground) continue;

                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;

                            if (!hasFgOnBorder)
                            {
                                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                                    hasFgOnBorder = true;
                            }
                        }
                    }
                }
                finally
                {
                    maskBitmap.UnlockBits(bmpData);
                }

                if (maxX < 0 || maxY < 0) // Aucun pixel foreground
                {
                    offsets.offsetX = 0;
                    offsets.offsetY = 0;
                    offsets.hasBlackOnBorder = false; // ou renommer en hasFgOnBorder
                    offsets.hasForeground = false;
                    Interlocked.Increment(ref _autoCenterOffsetsVersion);
                }
                else
                {
                    double centerX = (minX + maxX) / 2.0;
                    double centerY = (minY + maxY) / 2.0;

                    double imgCenterX = width / 2.0;
                    double imgCenterY = height / 2.0;

                    offsets.offsetX = centerX - imgCenterX;
                    offsets.offsetY = centerY - imgCenterY;
                    offsets.hasBlackOnBorder = hasFgOnBorder; // garde le champ existant
                    offsets.hasForeground = true;
                    offsets.boundingBox = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
                    Interlocked.Increment(ref _autoCenterOffsetsVersion);
                }
            });
        }

        private void ResetAutoCenterOffsets()
        {
            offsets.offsetX = 0;
            offsets.offsetY = 0;
            offsets.hasBlackOnBorder = false;
            offsets.hasForeground = false;
            offsets.boundingBox = Rectangle.Empty;
            Interlocked.Increment(ref _autoCenterOffsetsVersion);
        }

        private void BeginAutoCenterOffsetTracking()
        {
            Interlocked.Increment(ref _autoCenterOffsetTrackingRequests);
            calculerCentre = true;
        }

        private void EndAutoCenterOffsetTracking()
        {
            if (Interlocked.Decrement(ref _autoCenterOffsetTrackingRequests) <= 0)
            {
                Interlocked.Exchange(ref _autoCenterOffsetTrackingRequests, 0);
                calculerCentre = false;
            }
        }

        private bool IsAutoCenterOffsetTrackingActive()
        {
            return Volatile.Read(ref _autoCenterOffsetTrackingRequests) > 0;
        }


        private async Task RoutineLineareReculerHorsCadre(CancellationToken cancellationToken = default)
        {
            bool commandSent = false;
            while (!cancelAutoCentrage && !_stopRequested && !cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var switchState = await RequestCameraLinearSwitchStateAsync(250);
                if (switchState.FarPressed)
                {
                    udpSendCameraLinearMotorData(0);
                    break;
                }

                // offsets est mis à jour par LiveViewTimer_Tick en parallèle
                if (offsets.hasBlackOnBorder)
                {
                    if (!commandSent)
                    {
                        udpSendCameraLinearMotorData(-2000);
                        commandSent = true;
                    }
                }
                else
                {
                    udpSendCameraLinearMotorData(0);
                    break;
                }

                await Task.Delay(50, cancellationToken); // Libère le thread UI et laisse LiveViewTimer tourner
            }
        }
        private async Task RoutineCalibrationLineareNearest()
        {
            AppendTextToConsoleNL("RoutineCalibrationLinearNearest");
            AppendTextToConsoleNL($"cancelAutoCentrage = {cancelAutoCentrage}, stopRequested = {_stopRequested}, offsets.hasBlackOnBorder = {offsets.hasBlackOnBorder}");
            bool commandSent = false;

            while (!cancelAutoCentrage && !_stopRequested)
            {
                var switchState = await RequestCameraLinearSwitchStateAsync(250);
                if (switchState.NearPressed)
                {
                    udpSendCameraLinearMotorData(0);
                    break;
                }

                // offsets est mis à jour par LiveViewTimer_Tick en parallèle
                if (!offsets.hasBlackOnBorder)
                {
                    if (!commandSent)
                    {
                        udpSendCameraLinearMotorData(2000);
                        commandSent = true;
                    }
                }
                else
                {
                    udpSendCameraLinearMotorData(0);
                    AppendTextToConsoleNL($"cameraRailNearLimitSwitchPressed = {cameraRailNearLimitSwitchPressed} et offsets.hasBlackOnBorder = {offsets.hasBlackOnBorder}");
                    AppendTextToConsoleNL("RoutineCalibrationLinearNearest terminée");
                    break;
                }


                await Task.Delay(50); // Libère le thread UI et laisse LiveViewTimer tourner
            }
            AppendTextToConsoleNL($"cancelAutoCentrage = {cancelAutoCentrage}, stopRequested = {_stopRequested}, offsets.hasBlackOnBorder = {offsets.hasBlackOnBorder},cameraRailNearLimitSwitchPressed = {cameraRailNearLimitSwitchPressed}, cameraRailFarLimitSwitchPressed = {cameraRailFarLimitSwitchPressed}");
            AppendTextToConsoleNL("RoutineCalibrationLinearNearest terminée");
        }

        private async Task RoutineAutoCentrage(int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            //if (!offsets.hasBlackOnBorder && !cancelAutoCentrage) {

            //    RoutineCalibrationLineareNearest();
            //}

            //AppendTextToConsoleNL("RoutineAutoCentrage");
            BeginAutoCenterOffsetTracking();
            cancelAutoCentrage = false;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                long startOffsetsVersion = Interlocked.Read(ref _autoCenterOffsetsVersion);
                ResetAutoCenterOffsets();
                var waitForMaskStart = DateTime.Now;
                while ((Interlocked.Read(ref _autoCenterOffsetsVersion) <= startOffsetsVersion || !offsets.hasForeground)
                       && !_stopRequested
                       && !cancelAutoCentrage
                       && !cancellationToken.IsCancellationRequested
                       && (DateTime.Now - waitForMaskStart).TotalMilliseconds < 3000)
                {
                    await Task.Delay(100, cancellationToken);
                }

                double kP = 0.3; // proportionnel
                const double tolerance = 5.0;
                const int minStep = 2;
                const int maxStep = 50;
                const int delayMs = 150; // petit délai pour laisser le mouvement se faire

                var startTime = DateTime.Now;

                // Corriger X
                //Debug.WriteLine($"cancelAutoCentrage = {cancelAutoCentrage}, stopRequested = {_stopRequested}");


                while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs && !cancelAutoCentrage && !_stopRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();


                    // Debug.WriteLine("Routine Auto Centrage démarrée");

                    if (!offsets.hasForeground)
                    {
                        AppendTextToConsoleNL("Auto-centrage annulé: aucun objet détecté dans le masque.");
                        break;
                    }

                    var offsetX = offsets.offsetX;
                    var offsetY = offsets.offsetY;

                    //AppendTextToConsoleNL($"{offsetX}, {offsetY}");

                    if (Math.Abs(offsetX) <= tolerance && Math.Abs(offsetY) <= tolerance)
                    {
                        Debug.WriteLine("Centrage terminé: En deça de la tolérance");
                        //AppendTextToConsoleNL("Centrage terminé: En deça de la tolérance");
                        break;
                    }

                    int dynamicStepX = (int)Math.Clamp(Math.Abs(offsetX) * kP, minStep, maxStep);
                    int stepX = offsetX > 0 ? dynamicStepX : -dynamicStepX;

                    udpSendLiftHorizontalData(stepX);

                    //Debug.WriteLine($"Move X: {stepX} (offsetX={offsetX})");


                    int dynamicStepY = (int)Math.Clamp(Math.Abs(offsetY) * kP, minStep, maxStep);
                    int stepY = offsetY > 0 ? dynamicStepY : -dynamicStepY;

                    udpSendLiftVerticalMotorData(stepY * 100);

                    Debug.WriteLine($"Move Y: {stepY} (offsetY={offsetY})");

                    if (offsets.hasBlackOnBorder) await RoutineLineareReculerHorsCadre(cancellationToken);

                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            finally
            {
                EndAutoCenterOffsetTracking();
                udpSendLiftVerticalMotorData(0);
                udpSendLiftHorizontalData(0);
                udpSendCameraLinearMotorData(0);

                //AppendTextToConsoleNL("Routine Auto Centrage terminée");
                Debug.WriteLine("Routine terminée (timeout ou centrage).");
            }

        }

        private async Task AutoCentrageStepPendantActuateurAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!offsets.hasForeground) return;

            const double tolerance = 20.0;
            const double kPX = 0.35;
            const int minStep = 2;
            const int maxStepX = 40;

            var offsetX = offsets.offsetX;
            var offsetY = offsets.offsetY;

            if (AutoCentrageActuateurDansTolerance(tolerance))
            {
                udpSendLiftVerticalMotorData(0);
                udpSendLiftHorizontalData(0);
                udpSendCameraLinearMotorData(0);
                return;
            }

            int dynamicStepX = (int)Math.Clamp(Math.Abs(offsetX) * kPX, minStep, maxStepX);
            int stepX = offsetX > 0 ? dynamicStepX : -dynamicStepX;
            udpSendLiftHorizontalData(stepX);

            int dynamicStepY = CalculateActuatorVerticalStep(Math.Abs(offsetY), minStep);
            int stepY = offsetY > 0 ? dynamicStepY : -dynamicStepY;
            udpSendLiftVerticalMotorData(stepY * 100);

            if (offsets.hasBlackOnBorder)
            {
                await RoutineLineareReculerHorsCadre(cancellationToken);
            }

            await Task.Delay(150, cancellationToken);
            udpSendLiftHorizontalData(0);
            udpSendCameraLinearMotorData(0);
        }

        private static int CalculateActuatorVerticalStep(double absOffsetY, int minStep)
        {
            const double baseGain = 0.65;
            const double catchUpThreshold = 55.0;
            const double catchUpGain = 1.15;
            const int firmwareEffectiveMaxStepY = 80;

            double step = absOffsetY * baseGain;
            if (absOffsetY > catchUpThreshold)
            {
                step += (absOffsetY - catchUpThreshold) * catchUpGain;
            }

            return (int)Math.Clamp(step, minStep, firmwareEffectiveMaxStepY);
        }

        private bool AutoCentrageActuateurDansTolerance(double tolerance = 20.0)
        {
            return offsets.hasForeground
                && !offsets.hasBlackOnBorder
                && Math.Abs(offsets.offsetX) <= tolerance
                && Math.Abs(offsets.offsetY) <= tolerance;
        }

        private async Task FinishAutoCentragePendantActuateurAsync(int timeoutMs = 3000)
        {
            if (!ShouldAutoCenterDuringActuatorMove()) return;

            var startTime = DateTime.Now;
            while (!_stopRequested
                   && offsets.hasForeground
                   && !AutoCentrageActuateurDansTolerance()
                   && (DateTime.Now - startTime).TotalMilliseconds <= timeoutMs)
            {
                await AutoCentrageStepPendantActuateurAsync();
                await Task.Delay(150);
            }

            udpSendLiftVerticalMotorData(0);
            udpSendLiftHorizontalData(0);
            udpSendCameraLinearMotorData(0);
        }

        private async Task RunAutoCentrageContinuPendantActuateurAsync(CancellationToken token)
        {
            AppendTextToConsoleNL("Auto-centrage continu pendant actuateur démarré");
            BeginAutoCenterOffsetTracking();
            cancelAutoCentrage = false;

            try
            {
                while (!token.IsCancellationRequested && !_stopRequested)
                {
                    if (offsets.hasForeground)
                    {
                        await AutoCentrageStepPendantActuateurAsync(token);
                    }
                    else
                    {
                        udpSendLiftVerticalMotorData(0);
                        udpSendLiftHorizontalData(0);
                        udpSendCameraLinearMotorData(0);
                        await Task.Delay(150, token);
                    }

                    await Task.Delay(50, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                EndAutoCenterOffsetTracking();
                udpSendLiftVerticalMotorData(0);
                udpSendLiftHorizontalData(0);
                udpSendCameraLinearMotorData(0);
                AppendTextToConsoleNL("Auto-centrage continu pendant actuateur arrêté");
            }
        }

        private async Task RoutineCalibration()
        {
            try
            {

	                if (isCalibrating) return;
	                isCalibrating = true;
	                await nikonDoFocus();
	                if (ShouldRunAutoCentrageDuringCalibration())
	                {
	                    await RoutineAutoCentrage();
	                }
	                else
	                {
	                    AppendTextToConsoleNL("Auto-centrage de calibration ignoré: désactivé dans Caméra/Automation.");
	                }

	                await RoutineCalibrationLineareNearest();
	                await nikonDoFocus();
	                if (offsets.hasBlackOnBorder)
	                {
	                    await RoutineLineareReculerHorsCadre();
	                    await nikonDoFocus();
	                }
	                if (ShouldRunAutoCentrageDuringCalibration())
	                {
	                    await RoutineAutoCentrage();
	                }
	                else
	                {
	                    AppendTextToConsoleNL("Auto-centrage final de calibration ignoré: désactivé dans Caméra/Automation.");
	                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur: " + ex.ToString());
            }
            finally
            {
                isCalibrating = false;
            }

	        }

	        private bool ShouldRunAutoCentrageDuringCalibration()
	        {
	            return !appSettings.CalibrationAutoCentrage;
	        }
	
	        public class OffsetsData
        {
            public double offsetX;
            public double offsetY;
            public Rectangle boundingBox;
            public bool hasBlackOnBorder;
            public bool hasForeground;
        }


    }


}
