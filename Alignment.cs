using Emgu.CV;
using Emgu.CV.Util;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public bool calculerCentre = false;
        private (double offsetX, double offsetY, Rectangle boundingBox, bool hasBlackOnBorder) offsets;
        bool cancelAutoCentrage = false;
        bool cameraRailFarLimitSwitchPressed = false;
        bool cameraRailNearLimitSwitchPressed = false;

        private async Task<(double offsetX, double offsetY, Rectangle boundingBox, bool hasBlackOnBorder)> CalculeDuCentrageAsync(Bitmap maskBitmap)
        {
            return await Task.Run(() =>
            {
                int width = maskBitmap.Width;
                int height = maskBitmap.Height;

                int minX = width, maxX = 0, minY = height, maxY = 0;
                bool hasBlackOnBorder = false;

                var rect = new Rectangle(0, 0, width, height);
                var bmpData = maskBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, maskBitmap.PixelFormat);

                try
                {
                    int bytesPerPixel = Image.GetPixelFormatSize(maskBitmap.PixelFormat) / 8;
                    int stride = bmpData.Stride;
                    int totalBytes = stride * height;
                    byte[] pixelBuffer = new byte[totalBytes];

                    System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, pixelBuffer, 0, totalBytes);

                    for (int y = 0; y < height; y++)
                    {
                        int rowStart = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int index = rowStart + x * bytesPerPixel;

                            if (index + 2 >= pixelBuffer.Length) break;

                            byte b = pixelBuffer[index];
                            byte g = pixelBuffer[index + 1];
                            byte r = pixelBuffer[index + 2];

                            if (r == 0 && g == 0 && b == 0) // pixel noir
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;

                                // Vérifier si pixel noir touche un bord
                                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                                    hasBlackOnBorder = true;
                            }
                        }
                    }
                }
                finally
                {
                    maskBitmap.UnlockBits(bmpData);
                }

                if (maxX == 0 && maxY == 0)
                    return (0, 0, Rectangle.Empty, hasBlackOnBorder);

                double centerX = (minX + maxX) / 2.0;
                double centerY = (minY + maxY) / 2.0;

                double imgCenterX = width / 2.0;
                double imgCenterY = height / 2.0;

                double offsetX = centerX - imgCenterX;
                double offsetY = centerY - imgCenterY;

                Rectangle boundingBox = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                offsets.offsetX = offsetX; 
                offsets.offsetY = offsetY;
                return (offsetX, offsetY, boundingBox, hasBlackOnBorder);
            });
        }

        private async Task RoutineAutoCentrage(int timeoutMs = 8000)
        {
            AppendTextToConsoleNL("- RoutineAutoCentrage");
            cancelAutoCentrage = false;


            const double tolerance = 5.0;
            const int minStep = 2;
            const int maxStep = 50;
            const int delayMs = 300; // petit délai pour laisser le mouvement se faire

            var startTime = DateTime.Now;

            // Corriger X
            Debug.WriteLine($"cancelAutoCentrage = {cancelAutoCentrage}, stopRequested = {_stopRequested}");
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs && !cancelAutoCentrage && !_stopRequested)
            {

                if (cancelAutoCentrage)
                {
                    Debug.WriteLine("Centrage annulé.");
                    udpSendStepperLiftNema23MotorData(0);
                    udpSendScissorData(0);
                    udpSendCameraLinearMotorData(0);
                    calculerCentre = false;
                    if (chkBox_CalculerCentrage.InvokeRequired)
                    {
                        chkBox_CalculerCentrage.Invoke(new Action(() =>
                        {
                            chkBox_CalculerCentrage.Checked = false;
                            chkBox_CalculerCentrage.Text = "";
                        }));
                    }
                    else
                    {
                        chkBox_CalculerCentrage.Checked = false;
                        chkBox_CalculerCentrage.Text = "";
                    }
                    return;
                }


                Debug.WriteLine("Routine Auto Centrage démarrée");

                var offsetX = offsets.offsetX;
                var offsetY = offsets.offsetY;

                //AppendTextToConsoleNL($"{offsetX}, {offsetY}");

                if (Math.Abs(offsetX) <= tolerance && Math.Abs(offsetY) <= tolerance)
                {
                    Debug.WriteLine("Centrage terminé: En deça de la tolérance");
                    AppendTextToConsoleNL("Centrage terminé: En deça de la tolérance");
                    break;
                }

                int dynamicStepX = (int)Math.Clamp(Math.Abs(offsetX) * 0.3, minStep, maxStep);
                int stepX = offsetX > 0 ? dynamicStepX : -dynamicStepX;

                udpSendScissorData(stepX);

                Debug.WriteLine($"Move X: {stepX} (offsetX={offsetX})");




                int dynamicStepY = (int)Math.Clamp(Math.Abs(offsetY) * 0.3, minStep, maxStep);
                int stepY = offsetY > 0 ? dynamicStepY : -dynamicStepY;

                udpSendStepperLiftNema23MotorData(stepY * 100);

                Debug.WriteLine($"Move Y: {stepY} (offsetY={offsetY})");

                //if (offsets.hasBlackOnBorder && !cameraRailFarLimitSwitchPressed)
                //{
                //    udpSendCameraLinearMotorData(-2000);
                //}
                //else if (cameraRailNearLimitSwitchPressed == false)
                //{
                //    while (!offsets.hasBlackOnBorder)
                //        udpSendCameraLinearMotorData(0);
                //}

                await Task.Delay(delayMs);
            }


            udpSendStepperLiftNema23MotorData(0);
            udpSendScissorData(0);
            udpSendCameraLinearMotorData(0);
            calculerCentre = false;
            if (chkBox_CalculerCentrage.InvokeRequired)
            {
                chkBox_CalculerCentrage.Invoke(new Action(() =>
                {
                    chkBox_CalculerCentrage.Checked = false;
                    chkBox_CalculerCentrage.Text = "";
                }));
            }
            else
            {
                chkBox_CalculerCentrage.Checked = false;
                chkBox_CalculerCentrage.Text = "";
            }
            AppendTextToConsoleNL("Routine Auto Centrage terminée");
            Debug.WriteLine("Routine terminée (timeout ou centrage).");

        }

        //private async Task RoutineAutoCentrage(int timeoutMs = 8000)
        //{
        //    AppendTextToConsoleNL("- RoutineAutoCentrage");
        //    cancelAutoCentrage = false;
        //    calculerCentre = true;

        //    const double tolerance = 5.0;
        //    const int minStep = 2;
        //    const int maxStep = 50;
        //    const int delayMs = 300; // petit délai pour laisser le mouvement se faire

        //    var startTime = DateTime.Now;

        //    // Corriger X
        //    Debug.WriteLine($"cancelAutoCentrage = {cancelAutoCentrage}, stopRequested = {_stopRequested}");
        //    while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs && !cancelAutoCentrage && !_stopRequested)
        //    {

        //        if (cancelAutoCentrage)
        //        {
        //            Debug.WriteLine("Centrage annulé.");
        //            udpSendStepperLiftNema23MotorData(0);
        //            udpSendScissorData(0);
        //            udpSendCameraLinearMotorData(0);
        //            calculerCentre = false;
        //            if (chkBox_CalculerCentrage.InvokeRequired)
        //            {
        //                chkBox_CalculerCentrage.Invoke(new Action(() =>
        //                {
        //                    chkBox_CalculerCentrage.Checked = false;
        //                    chkBox_CalculerCentrage.Text = "";
        //                }));
        //            }
        //            else
        //            {
        //                chkBox_CalculerCentrage.Checked = false;
        //                chkBox_CalculerCentrage.Text = "";
        //            }
        //            return;
        //        }



        //        Debug.WriteLine("Routine Auto Centrage démarrée");

        //        var offsetX = offsets.offsetX;
        //        var offsetY = offsets.offsetY;

        //        if (Math.Abs(offsetX) <= tolerance && Math.Abs(offsetY) <= tolerance)
        //        {
        //            Debug.WriteLine("Centrage terminé: En deça de la tolérance");
        //            AppendTextToConsoleNL("Centrage terminé: En deça de la tolérance");
        //            break;
        //        }

        //        int dynamicStepX = (int)Math.Clamp(Math.Abs(offsetX) * 0.3, minStep, maxStep);
        //        int stepX = offsetX > 0 ? dynamicStepX : -dynamicStepX;

        //        udpSendScissorData(stepX);

        //        Debug.WriteLine($"Move X: {stepX} (offsetX={offsetX})");




        //        int dynamicStepY = (int)Math.Clamp(Math.Abs(offsetY) * 0.3, minStep, maxStep);
        //        int stepY = offsetY > 0 ? dynamicStepY : -dynamicStepY;

        //        udpSendStepperLiftNema23MotorData(stepY * 100);

        //        Debug.WriteLine($"Move Y: {stepY} (offsetY={offsetY})");

        //        if (offsets.hasBlackOnBorder && !cameraRailFarLimitSwitchPressed)
        //        {
        //            udpSendCameraLinearMotorData(-2000);
        //        }
        //        else if (cameraRailNearLimitSwitchPressed == false)
        //        {
        //            while (!offsets.hasBlackOnBorder)
        //                udpSendCameraLinearMotorData(2000);
        //        }

        //        await Task.Delay(delayMs);
        //    }


        //    udpSendStepperLiftNema23MotorData(0);
        //    udpSendScissorData(0);
        //    udpSendCameraLinearMotorData(0);
        //    calculerCentre = false;
        //    if (chkBox_CalculerCentrage.InvokeRequired)
        //    {
        //        chkBox_CalculerCentrage.Invoke(new Action(() =>
        //        {
        //            chkBox_CalculerCentrage.Checked = false;
        //            chkBox_CalculerCentrage.Text = "";
        //        }));
        //    }
        //    else
        //    {
        //        chkBox_CalculerCentrage.Checked = false;
        //        chkBox_CalculerCentrage.Text = "";
        //    }
        //    AppendTextToConsoleNL("Routine Auto Centrage terminée");
        //    Debug.WriteLine("Routine terminée (timeout ou centrage).");

        //}



        //private async Task RoutineAutoCentrage(int timeoutMs = 8000)
        //{
        //    AppendTextToConsoleNL("- RoutineAutoCentrage");
        //    cancelAutoCentrage = false;
        //    calculerCentre = true;
        //    const double tolerance = 5.0;
        //    const int minStep = 2;
        //    const int maxStep = 50;
        //    const int delayMs = 300;
        //    const int cameraMoveDurationMs = 3000;
        //    const int forwardSpeed = 2000;
        //    const int backwardSpeed = -2000;

        //    var startTime = DateTime.Now;

        //    // ---- Phase 1 : Positionner la caméra ----
        //    Debug.WriteLine("Phase 1: Positionnement caméra");
        //    var cameraStartTime = DateTime.Now;
        //    while ((DateTime.Now - cameraStartTime).TotalMilliseconds < timeoutMs / 2 &&
        //           !cancelAutoCentrage && !_stopRequested)
        //    {
        //        if (!offsets.hasBlackOnBorder && !cameraRailNearLimitSwitchPressed)
        //        {
        //            udpSendCameraLinearMotorData(forwardSpeed);
        //            await Task.Delay(cameraMoveDurationMs);
        //            udpSendCameraLinearMotorData(0);
        //            Debug.WriteLine("Avance caméra");
        //        }
        //        else
        //        {
        //            if (offsets.hasBlackOnBorder && !cameraRailFarLimitSwitchPressed)
        //            {
        //                udpSendCameraLinearMotorData(backwardSpeed);
        //                await Task.Delay(cameraMoveDurationMs);
        //                udpSendCameraLinearMotorData(0);
        //                Debug.WriteLine("Recul caméra (ajustement)");
        //            }

        //            break;
        //        }
        //        await Task.Delay(delayMs);
        //    }

        //    // ---- Phase 2 : Centrage XY ----
        //    Debug.WriteLine("Phase 2: Centrage XY");
        //    var xyStartTime = DateTime.Now;
        //    while ((DateTime.Now - xyStartTime).TotalMilliseconds < timeoutMs / 2 &&
        //           !cancelAutoCentrage && !_stopRequested)
        //    {
        //        // Recalcul offsets à chaque boucle
        //        var offsetX = offsets.offsetX;
        //        var offsetY = offsets.offsetY;
        //        Debug.WriteLine($"{offsetX}:{offsetY}");
        //        if (Math.Abs(offsetX) <= tolerance && Math.Abs(offsetY) <= tolerance)
        //        {
        //            Debug.WriteLine($"XY centré (offsetX={offsetX}, offsetY={offsetY})");
        //            break;
        //        }

        //        int dynamicStepX = (int)Math.Clamp(Math.Abs(offsetX) * 0.3, minStep, maxStep);
        //        int stepX = offsetX > 0 ? dynamicStepX : -dynamicStepX;
        //        udpSendScissorData(stepX);

        //        int dynamicStepY = (int)Math.Clamp(Math.Abs(offsetY) * 0.3, minStep, maxStep);
        //        int stepY = offsetY > 0 ? dynamicStepY : -dynamicStepY;
        //        udpSendStepperLiftNema23MotorData(stepY * 100);

        //        Debug.WriteLine($"Move X: {stepX}, Move Y: {stepY} (offsetX={offsetX}, offsetY={offsetY})");

        //        await Task.Delay(delayMs);
        //    }

        //    // ---- Phase 3 : Ajustement final Z ----
        //    Debug.WriteLine("Phase 3: Ajustement final caméra");
        //    if (offsets.hasBlackOnBorder && !cameraRailFarLimitSwitchPressed)
        //    {
        //        udpSendCameraLinearMotorData(backwardSpeed);
        //        await Task.Delay(cameraMoveDurationMs);
        //        udpSendCameraLinearMotorData(0);
        //        Debug.WriteLine("Recul final caméra");
        //    }

        //    // Stop moteurs
        //    udpSendStepperLiftNema23MotorData(0);
        //    udpSendScissorData(0);
        //    udpSendCameraLinearMotorData(0);
        //    calculerCentre = false;
        //    ResetCentrageCheckbox();
        //    AppendTextToConsoleNL("Routine Auto Centrage terminée");
        //    Debug.WriteLine("Routine terminée.");
        //    await NikonAutofocus();
        //}

        private void ResetCentrageCheckbox()
        {
            if (chkBox_CalculerCentrage.InvokeRequired)
            {
                chkBox_CalculerCentrage.Invoke(new Action(() =>
                {
                    chkBox_CalculerCentrage.Checked = false;
                    chkBox_CalculerCentrage.Text = "";
                }));
            }
            else
            {
                chkBox_CalculerCentrage.Checked = false;
                chkBox_CalculerCentrage.Text = "";
            }
        }

    }
}
