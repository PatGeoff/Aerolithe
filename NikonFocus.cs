using Nikon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScottPlot.WinForms;
using ScottPlot;
using System.Security.Permissions;
using ScottPlot.Plottables;
using ScottPlot.Statistics;
using Emgu.CV;

namespace Aerolithe
{


    public partial class Aerolithe : Form
    {
        public int focusStackStepVar = 0; // Garde compte des steps de driveSteps effectué. 
        public int iterations = 24;  // Nombre de steps à effectuer 
        private bool _stopRequested = false;
        public double currentCrosshairX = 0;
        public ScottPlot.Plottables.Crosshair crosshair;
        public ScottPlot.WinForms.FormsPlot formsPlot;
        public int delayTime = 100;
        public int minDetect = 5;
        double blurThreshold = 100.0;
        public int blurredBlocks = 0;
        public int stepSize = 0;
        public int delta = 0;
        int maxNbrPicturesAllowed = 15;

        private enum AutomaticFocusResult
        {
            Success,
            MaskUnavailable,
            Cancelled
        }

        public async Task nikonDoFocus()
        {
            //AppendTextToConsoleNL("- nikonDoFocus");
            try
            {
                await NikonAutofocus();
                await Task.Delay(300);
                focusStackStepVar = 0;
                UpdateFocusStepVarLbl(focusStackStepVar);
                //MessageBox.Show("image capturée");
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Autofocus Nikon ignoré: " + ex.Message);

                try
                {
                    if (!device.LiveViewEnabled)
                    {
                        device.LiveViewEnabled = true;
                        await Task.Delay(100);
                        liveViewTimer.Start();
                    }
                }
                catch (Exception liveViewEx)
                {
                    AppendTextToConsoleNL("Impossible de relancer le live view après autofocus ignoré: " + liveViewEx.Message);
                }
            }

        }

        public async Task NikonAutofocus()
        {
            //AppendTextToConsoleNL("- NikonAutofocus");
            //var sw = Stopwatch.StartNew();
            //Debug.WriteLine($"[NikonAutoFocus()] lancement à : {sw.ElapsedMilliseconds} ms");
            await RunExclusiveNikonOperationAsync(async () =>
            {
                await Task.Delay(200);

                await ExecuteNikonCommandWithBusyRetryAsync(
                    () => device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus),
                    "Autofocus Nikon");

                focusStackStepVar = 0;
                UpdateFocusStepVarLbl(focusStackStepVar);
            }, pauseLiveView: true);

            //focusStackStepVar = 0;

            UpdateFocusStepVarLbl(focusStackStepVar);

            //Debug.WriteLine($"[NikonAutoFocus()] fin à : {sw.ElapsedMilliseconds} ms");

        }

        private void UpdateFocusStepVarLbl(int position)
        {
            if (lbl_focusStepsVar.InvokeRequired)
            {
                lbl_focusStepsVar.Invoke(new Action(() =>
                {
                    lbl_focusStepsVar.Text = position.ToString();
                }));
            }
            else
            {
                lbl_focusStepsVar.Text = position.ToString();
            }
        }

        private void UpdateCrosshairTitle()
        {
            double currentY = crosshair.Y;
            formsPlot.Plot.Title($"<<   X: {currentCrosshairX:F0}   |   Y: {currentY:F2}   >>");
        }

        private Task DisplayBlurGraph(Dictionary<int, (int steps, int blurBlocks)> blurDataDict)
        {
            if (_stopRequested || blurDataDict.Count == 0)
            {
                return Task.CompletedTask;
            }

            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayBlurGraph(blurDataDict)));
                return Task.CompletedTask;
            }

            formsPlot = new ScottPlot.WinForms.FormsPlot { Dock = DockStyle.Fill };

            if (aerolitheTabControl5.InvokeRequired)
            {
                aerolitheTabControl5.Invoke(new Action(() =>
                {
                    aerolitheTabControl5.TabPages["tabPage2"].Controls.Clear();
                    aerolitheTabControl5.TabPages["tabPage2"].Controls.Add(formsPlot);
                    aerolitheTabControl5.SelectedTab = aerolitheTabControl5.TabPages["tabPage2"];
                }));
            }
            else
            {
                aerolitheTabControl5.TabPages["tabPage2"].Controls.Clear();
                aerolitheTabControl5.TabPages["tabPage2"].Controls.Add(formsPlot);
                aerolitheTabControl5.SelectedTab = aerolitheTabControl5.TabPages["tabPage2"];
            }



            var blurDataList = blurDataDict.ToList();
            int[] xs = blurDataList.Select(pair => pair.Value.steps).ToArray();
            int[] ys = blurDataList.Select(pair => pair.Value.blurBlocks).ToArray();

            if (_stopRequested || xs.Length == 0 || ys.Length == 0)
            {
                return Task.CompletedTask;
            }

            formsPlot.Plot.Add.Scatter(xs, ys);

            int[] thresholdIndices = ys
                .Select((val, idx) => new { val, idx })
                .Where(p => p.val >= minDetect)
                .Select(p => p.idx)
                .ToArray();

            if (thresholdIndices.Length > 0)
            {
                double firstTransitionX = xs[thresholdIndices.Min()];
                double lastTransitionX = xs[thresholdIndices.Max()];

                formsPlot.Plot.Add.VerticalLine(firstTransitionX).Color = ScottPlot.Colors.Orange;
                formsPlot.Plot.Add.VerticalLine(lastTransitionX).Color = ScottPlot.Colors.Orange;
            }

            formsPlot.Plot.DataBackground.Color = Colors.DarkGray;

            currentCrosshairX = xs[thresholdIndices.Length > 0 ? thresholdIndices.Min() : 0];
            crosshair = formsPlot.Plot.Add.Crosshair(currentCrosshairX, ys[thresholdIndices.Length > 0 ? thresholdIndices.Min() : 0]);
            UpdateCrosshairTitle();

            formsPlot.MouseMove += (s, e) =>
            {
                Pixel mousePixel = new(e.X, e.Y);
                Coordinates mouseCoordinates = formsPlot.Plot.GetCoordinates(mousePixel);

                currentCrosshairX = Math.Round(mouseCoordinates.X);
                crosshair.X = currentCrosshairX;

                int closestIndex = Array.FindIndex(xs, x => x == currentCrosshairX);
                if (closestIndex >= 0 && closestIndex < ys.Length)
                    crosshair.Y = ys[closestIndex];

                UpdateCrosshairTitle();
                formsPlot.Refresh();
            };

            formsPlot.Plot.Title("Évolution du flou (seconde passe)");
            formsPlot.Plot.Axes.Bottom.Label.Text = "Position relative";
            formsPlot.Plot.Axes.Left.Label.Text = "Nombre de blocs nets";

            formsPlot.Refresh();
            return Task.CompletedTask;
        }


        private async Task<AutomaticFocusResult> AutomaticFocusRoutine(CancellationToken cancellationToken = default)
        {
            Mat uiClone = null;

            try
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                Invoke(new Action(() =>
                {
                    int threshold = ClampMaskThreshold(GetMaskThresholdSetting(appSettings.MaskAlgorithmIndex));
                    hScrollBar_liveMaskThresh.Value = threshold;
                    lbl_maskAmount.Text = threshold.ToString();
                }));

                AppendTextToConsoleNL("AutomaticFocusRoutine");
                if (_stopRequested) return AutomaticFocusResult.Cancelled;

                // Bouton STOP visible
                if (btn_stopAutomaticFocusCapture.InvokeRequired)
                {
                    btn_stopAutomaticFocusCapture.Invoke(new Action(() =>
                    {
                        btn_stopAutomaticFocusCapture.Visible = true;
                        btn_stopAutomaticFocusCapture.Enabled = true;
                    }));
                }
                else
                {
                    btn_stopAutomaticFocusCapture.Visible = true;
                    btn_stopAutomaticFocusCapture.Enabled = true;
                }

                await NikonAutofocus();
                if (_stopRequested) return AutomaticFocusResult.Cancelled;

                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(600, cancellationToken);
                if (_stopRequested) return AutomaticFocusResult.Cancelled;


                // Clone du masque Live
                lock (_maskLock)
                {
                    if (maskMatLive == null || maskMatLive.IsEmpty)
                    {
                        AppendTextToConsoleNL("AutomaticFocusRoutine: masque live nul ou vide.");
                        return AutomaticFocusResult.MaskUnavailable;
                    }

                    uiClone = maskMatLive.Clone();
                }

                if (!IsMaskUsableForAutomaticFocus(uiClone))
                {
                    maskFreeze = false;
                    int originalThresh = 20;
                    Invoke(new Action(() =>
                    {
                        btn_freezeMask.Text = "";
                        originalThresh = hScrollBar_liveMaskThresh.Value;
                    }));

                    using Mat stableMask = await TryFindStableAutomaticFocusMaskAsync(originalThresh, cancellationToken);

                    if (_stopRequested) return AutomaticFocusResult.Cancelled;

                    if (stableMask == null || stableMask.IsEmpty)
                    {
                        Invoke(new Action(() =>
                        {
                            hScrollBar_liveMaskThresh.Value = originalThresh;
                            lbl_maskAmount.Text = originalThresh.ToString();
                        }));

                        AppendTextToConsoleNL("AutomaticFocusRoutine: aucune valeur de seuil n'a donné un masque stable et utilisable.");
                        return AutomaticFocusResult.MaskUnavailable;
                    }

                    uiClone.Dispose();
                    uiClone = stableMask.Clone();

                    // Succès
                    lock (_maskLock)
                    {
                        var oldMask = maskMatLive;
                        maskMatLive = uiClone.Clone();
                        oldMask?.Dispose();
                    }
                    maskFreeze = true;
                    Invoke(new Action(() =>
                    {
                        btn_freezeMask.Text = "";
                    }));
                }

                else
                {
                    lock (_maskLock)
                    {
                        var oldMask = maskMatLive;
                        maskMatLive = uiClone.Clone();
                        oldMask?.Dispose();
                    }
                    maskFreeze = true;
                    Invoke(new Action(() =>
                    {
                        btn_freezeMask.Text = "";
                    }));
                }


                RepairProjectSaveTargetIfPossible();
                if (string.IsNullOrWhiteSpace(appSettings?.ProjectPath)
                    || string.IsNullOrWhiteSpace(projet?.ImageFolderPath)
                    || string.IsNullOrWhiteSpace(projet?.ImageNameBase))
                {
                    AppendTextToConsoleNL("AutomaticFocusRoutine: aucun projet valide n'est chargé. Sauvegarde du masque impossible.");
                    MessageBox.Show(
                        this,
                        "Veuillez ouvrir ou créer un projet avant de lancer la routine de focus automatique.",
                        "Projet manquant",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return AutomaticFocusResult.MaskUnavailable;
                }

                string maskOutputPath = projet.GetMaskFullImagePath();
                if (string.IsNullOrWhiteSpace(maskOutputPath))
                {
                    AppendTextToConsoleNL("AutomaticFocusRoutine: chemin de masque invalide. Sauvegarde du masque impossible.");
                    return AutomaticFocusResult.MaskUnavailable;
                }

                // ====== SAUVEGARDE DU MASQUE ======
                // Source volontaire: ce qui est affiché dans picBox_liveMaskLum au moment de la sauvegarde.
                await SaveDisplayedMaskAsPngAsync(maskOutputPath);



                var blurDataDict = new Dictionary<int, (int steps, int blurBlocks)>();

                int maxTargetDown = 0;
                int maxUpperPosition = 0;
                int maxTargetUp = -1;

                // ====== 1) Première passe : reculer ======
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await ManualFocusAsync(1, stepSize * iterations);
                focusStackStepVar = iterations * -1;
                UpdateFocusStepVarLbl(focusStackStepVar);

                AppendTextToConsoleNL("focusStackStepVar = " + focusStackStepVar);
                await Task.Delay(500, cancellationToken);
                if (_stopRequested) return AutomaticFocusResult.Cancelled;

                // Reculer jusqu'à ce que flou disparaît
                while (blurredBlocks >= minDetect && !_stopRequested)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_stopRequested) return AutomaticFocusResult.Cancelled;
                    await ManualFocusAsync(1, stepSize);
                    focusStackStepVar--;
                    UpdateFocusStepVarLbl(focusStackStepVar);
                    await Task.Delay(delayTime, cancellationToken);
                }

                maxTargetDown = focusStackStepVar;
                await Task.Delay(200, cancellationToken);

                int blurConsecutiveCount = 0;
                int i = 0;

                // ====== 2) Deuxième passe : monter ======
                while (!_stopRequested)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    await ManualFocusAsync(0, stepSize);
                    await Task.Delay(delayTime, cancellationToken);

                    if (blurredBlocks >= minDetect)
                    {
                        blurDataDict[i] = (focusStackStepVar, blurredBlocks);
                        maxTargetUp = focusStackStepVar;
                        blurConsecutiveCount = 0;
                    }
                    else
                    {
                        blurConsecutiveCount++;
                    }

                    focusStackStepVar++;
                    UpdateFocusStepVarLbl(focusStackStepVar);

                    if (blurConsecutiveCount >= 4 && focusStackStepVar > 0)
                        break;

                    i++;
                }

                if (_stopRequested) return AutomaticFocusResult.Cancelled;

                maxUpperPosition = focusStackStepVar;
                delta = maxTargetUp + Math.Abs(maxTargetDown);

                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(500, cancellationToken);
                if (_stopRequested) return AutomaticFocusResult.Cancelled;

                // ====== 3) Retour au point net ======
                int steps = (int)(delta * stepSize * 0.75);
                AppendTextToConsoleNL($"stepSize={stepSize}, delta={delta}, steps={steps}");

                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await ManualFocusAsync(1, steps);
                await Task.Delay(500, cancellationToken);
                if (_stopRequested) return AutomaticFocusResult.Cancelled;

                focusStackStepVar = maxTargetUp;
                UpdateFocusStepVarLbl(focusStackStepVar);

                if (blurredBlocks < minDetect)
                {
                    while (blurredBlocks < minDetect && !_stopRequested)
                    {
                        await WaitIfSequencePausedAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        await ManualFocusAsync(0, stepSize);
                        await Task.Delay(delayTime * 5, cancellationToken);
                    }
                }

                while (blurredBlocks > minDetect * 2 && !_stopRequested)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    await ManualFocusAsync(1, stepSize);
                    await Task.Delay(delayTime * 5, cancellationToken);
                }

                if (_stopRequested) return AutomaticFocusResult.Cancelled;

                await ManualFocusAsync(0, stepSize);
                focusStackStepVar = 0;
                UpdateFocusStepVarLbl(focusStackStepVar);
                await Task.Delay(delayTime, cancellationToken);
                if (_stopRequested) return AutomaticFocusResult.Cancelled;

                await DisplayBlurGraph(blurDataDict);

                AppendTextToConsoleNL($"AutomaticFocusRoutine terninée");
                return AutomaticFocusResult.Success;
            }
            finally
            {
                uiClone?.Dispose();

                void HideStopButton()
                {
                    btn_stopAutomaticFocusCapture.Visible = false;
                    btn_stopAutomaticFocusCapture.Enabled = false;
                }

                if (btn_stopAutomaticFocusCapture.InvokeRequired)
                {
                    btn_stopAutomaticFocusCapture.Invoke((Action)HideStopButton);
                }
                else
                {
                    HideStopButton();
                }
            }
        }

        private IEnumerable<int> GetAutomaticFocusMaskThresholdCandidates(int originalThresh)
        {
            originalThresh = ClampMaskThreshold(originalThresh);

            if (appSettings.MaskAlgorithmIndex == 0)
            {
                return Enumerable.Range(0, 256).Select(offset => 255 - offset);
            }

            return Enumerable
                .Range(0, originalThresh + 1)
                .Select(offset => originalThresh - offset)
                .Concat(Enumerable.Range(originalThresh + 1, 255 - originalThresh));
        }

        private async Task<Mat> TryFindStableAutomaticFocusMaskAsync(int originalThresh, CancellationToken cancellationToken)
        {
            if (imageView?.JpegBuffer == null || imageView.JpegBuffer.Length == 0)
            {
                AppendTextToConsoleNL("AutomaticFocusRoutine: image live indisponible pour tester les seuils.");
                return null;
            }

            byte[] jpegSnapshot = imageView.JpegBuffer.ToArray();
            IEnumerable<int> thresholdCandidates = GetAutomaticFocusMaskThresholdCandidates(originalThresh);
            bool[] invertCandidates = appSettings.MaskAlgorithmIndex == 1
                ? new[] { false, true }
                : new[] { false };

            foreach (int t in thresholdCandidates)
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (_stopRequested) break;

                Invoke(new Action(() =>
                {
                    hScrollBar_liveMaskThresh.Value = t;
                    lbl_maskAmount.Text = t.ToString();
                }));

                foreach (bool invert in invertCandidates)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_stopRequested) break;

                    using Mat testMask = await BrightnessMaskFromBytesMat(jpegSnapshot, t, invert);
                    if (!IsMaskUsableForAutomaticFocus(testMask))
                    {
                        continue;
                    }

                    bool stayedValid = true;
                    var start = DateTime.Now;

                    while ((DateTime.Now - start).TotalMilliseconds < 700 && !_stopRequested)
                    {
                        await WaitIfSequencePausedAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        await Task.Delay(50, cancellationToken);

                        using Mat testMask2 = await BrightnessMaskFromBytesMat(jpegSnapshot, t, invert);
                        if (!IsMaskUsableForAutomaticFocus(testMask2))
                        {
                            stayedValid = false;
                            break;
                        }
                    }

                    if (stayedValid)
                    {
                        return await BrightnessMaskFromBytesMat(jpegSnapshot, t, invert);
                    }
                }
            }

            foreach (bool invert in invertCandidates)
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                using Mat autoMask = await BrightnessMaskFromBytesMat(jpegSnapshot, -1, invert);
                if (IsMaskUsableForAutomaticFocus(autoMask))
                {
                    return autoMask.Clone();
                }
            }

            return null;
        }

        private bool IsMaskUsableForAutomaticFocus(Mat mask)
        {
            if (mask == null || mask.IsEmpty || IsMatAllBlack(mask))
            {
                return false;
            }

            using var gray = new Mat();
            if (mask.NumberOfChannels == 1)
            {
                mask.CopyTo(gray);
            }
            else
            {
                CvInvoke.CvtColor(
                    mask,
                    gray,
                    mask.NumberOfChannels == 3
                        ? Emgu.CV.CvEnum.ColorConversion.Bgr2Gray
                        : Emgu.CV.CvEnum.ColorConversion.Bgra2Gray);
            }

            int whitePixels = CvInvoke.CountNonZero(gray);
            int totalPixels = Math.Max(1, gray.Rows * gray.Cols);
            double whiteRatio = whitePixels / (double)totalPixels;

            return whiteRatio >= 0.003 && whiteRatio <= 0.45;
        }

        //public async Task AutomaticFocusRoutine()
        //{
        //    AppendTextToConsoleNL("- AutomaticFocusRoutine");
        //    if (_stopRequested) return;
        //    if (btn_stopAutomaticFocusCapture.InvokeRequired)
        //    {
        //        btn_stopAutomaticFocusCapture.Invoke(new Action(() =>
        //        {
        //            btn_stopAutomaticFocusCapture.Visible = true;
        //            btn_stopAutomaticFocusCapture.Enabled = true;
        //        }));
        //    }
        //    else
        //    {
        //        btn_stopAutomaticFocusCapture.Visible = true;
        //        btn_stopAutomaticFocusCapture.Enabled = true;
        //    }

        //    await NikonAutofocus();
        //    if (_stopRequested) return;

        //    await Task.Delay(700);



        //    // Donner un CLONE au PictureBox pour éviter tout conflit/Dispose
        //    //var uiClone = (Bitmap)maskBitmapLive.Clone();
        //    Mat uiClone = maskMatLive.Clone();

        //    // Check si c'est “tout noir” ---
        //    if (IsMatAllBlack(uiClone))
        //    {
        //        blackMaskAttempts++;
        //        maskFreeze = false;

        //        if (btn_freezeMask.InvokeRequired)
        //        {
        //            btn_freezeMask.Invoke(new Action(() =>
        //            {
        //                btn_freezeMask.Invoke(() => btn_freezeMask.Text = maskFreeze ? "" : "");
        //            }));
        //        }

        //        try
        //        {
        //            // Routine pour changer la valeur du threshold pour avoir un masque


        //            // Première action : tenter un autofocus                
        //            await nikonDoFocus();                    

        //        }
        //        catch (Exception ex)
        //        {
        //            System.Diagnostics.Debug.WriteLine($"[Focus] nikonDoFocus failed: {ex}");
        //        }

        //        if (blackMaskAttempts >= 2)
        //        {
        //            // Deuxième fois d’affilée → message d’erreur et reset du compteur
        //            blackMaskAttempts = 0;
        //            MessageBox.Show(
        //                this,
        //                "Le masque en direct est entièrement noir après tentative d’autofocus.\n" +
        //                "Vérifiez la mise au point, l’éclairage, ou les seuils du masque.",
        //                "Erreur - Masque noir",
        //                MessageBoxButtons.OK,
        //                MessageBoxIcon.Error
        //            );
        //        }

        //        // On sort tôt du pipeline pour laisser le prochain tick relire une image
        //        goto AfterMaskBlackValidation;
        //    }
        //    else
        //    {
        //        // Si c’est bon (pas noir), on reset le compteur
        //        blackMaskAttempts = 0;
        //        // On freeze le frame
        //        maskFreeze = true;
        //        if (btn_freezeMask.InvokeRequired)
        //        {
        //            btn_freezeMask.Invoke(new Action(() =>
        //            {
        //                btn_freezeMask.Invoke(() => btn_freezeMask.Text = maskFreeze ? "" : "");
        //            }));
        //        }
        //    }

        //    AfterMaskBlackValidation:;


        //        //await SaveBitmapAsJpeg(maskBitmapLive,projet.GetMaskFullImagePath());
        //        //await SaveMaskAsPngNoTransparency(maskBitmapLive, projet.GetMaskFullImagePath());
        //        await SaveMaskAsPngNoTransparency(maskMatLive, projet.GetMaskFullImagePath());


        //    var blurDataDict = new Dictionary<int, (int steps, int blurBlocks)>();

        //    int maxTargetDown = 0;
        //    int maxUpperPosition = 0;
        //    int maxTargetUp = -1;

        //    // maxTargetDown = le focusStackStepVar le plus bas (négatif) où il y a eu une détection. Plus bas que ça c'est flou
        //    // maxTargetUp = le focusStackStepVar le plus haut où il ya détection. En haut de ça c'est flou
        //    // maxUpperPosition = le focusStackStepVar le plus haut où la caméra s'est rendue, très probablement 4 steps de plus que maxTargetUp
        //    // Delta = le range entre les deux maxTargetDown et maxTargetUp

        //    ////////////////////////////////////////////////
        //    //  Première passe : reculer (non stockée dans blurDataDict) ////////////////////////////////////////////////

        //    ManualFocus(1, stepSize * iterations);
        //    focusStackStepVar = iterations * -1;
        //    UpdateFocusStepVarLbl(focusStackStepVar);

        //    AppendTextToConsoleNL("focusStackStepVar = " + focusStackStepVar.ToString());
        //    await Task.Delay(500);

        //    if (_stopRequested) return;

        //    //  Reculer davantage si flou encore détecté
        //    while (blurredBlocks >= minDetect && !_stopRequested)
        //    {
        //        if (_stopRequested) return;
        //        ManualFocus(1, stepSize);
        //        focusStackStepVar -= 1;
        //        UpdateFocusStepVarLbl(focusStackStepVar);
        //        await Task.Delay(delayTime);
        //    }

        //    maxTargetDown = focusStackStepVar;

        //    //AppendTextToConsoleNL("focusStackStepVar = " + focusStackStepVar.ToString());
        //    //AppendTextToConsoleNL($"maxTargetDown = {maxTargetDown}");

        //    await Task.Delay(200);


        //    int blurConsecutiveCount = 0;
        //    int i = 0;

        //    ////////////////////////////////////////////////
        //    //  Deuxième passe : On monte jusqu'à ce qu'on ait 4 flous consécutifs (stockée dans blurDataDict) ////////////////////////////////////////////////

        //    while (!_stopRequested)
        //    {
        //        ManualFocus(0, stepSize);
        //        await Task.Delay(delayTime);

        //        if (blurredBlocks >= minDetect)
        //        {
        //            blurDataDict[i] = (focusStackStepVar, blurredBlocks);
        //            maxTargetUp = focusStackStepVar;
        //            blurConsecutiveCount = 0;
        //        }
        //        else
        //        {
        //            blurConsecutiveCount++;

        //        }

        //        focusStackStepVar += 1;
        //        UpdateFocusStepVarLbl(focusStackStepVar);

        //        if (blurConsecutiveCount >= 4 && focusStackStepVar > 0)
        //        {
        //            //AppendTextToConsoleNL("Arrêt anticipé : 4 flous consécutifs détectés.");
        //            break;
        //        }
        //        i++;
        //    }


        //    //AppendTextToConsoleNL("-- maxTargetUp = " + maxTargetUp.ToString());


        //    maxUpperPosition = focusStackStepVar;
        //    // delta = nombre de steps maximum
        //    delta = maxTargetUp + Math.Abs(maxTargetDown);
        //    //AppendTextToConsoleNL("-- maxUpperPosition = " + maxUpperPosition.ToString());
        //    //AppendTextToConsoleNL($"delta ({delta.ToString()}) =  steps entre max up et max down à {stepSize} stepSize");

        //    await Task.Delay(500);

        //    ////////////////////////////////////////////////
        //    // Troisième passe : retour à la première détection    ////////////////////////////////////////////////


        //    if (_stopRequested) return;
        //    //int returnToStartSteps = (maxUpperPosition - maxTargetUp) / 2;
        //    //ManualFocus(1, returnToStartSteps * stepSize);


        //    //AppendTextToConsoleNL("stepSize = " + stepSize.ToString());
        //    //AppendTextToConsoleNL("delta = " + delta.ToString());
        //    int steps = (int)(delta * stepSize * 0.75);
        //    AppendTextToConsoleNL("stepSize = " + stepSize.ToString() + ", delta = " + delta.ToString() + ", steps = " + steps.ToString());

        //    ManualFocus(1, steps);
        //    await Task.Delay(500);
        //    focusStackStepVar = maxTargetUp;
        //    UpdateFocusStepVarLbl(maxTargetUp);

        //    if (blurredBlocks < minDetect)
        //    {
        //        while (blurredBlocks < minDetect && !_stopRequested)
        //        {
        //            if (_stopRequested) break;
        //            ManualFocus(0, stepSize);
        //            await Task.Delay(delayTime * 5);
        //        }
        //    }

        //    // Ajustement fin
        //    while (blurredBlocks > minDetect * 2 && !_stopRequested)
        //    {
        //        if (_stopRequested) break;
        //        ManualFocus(1, stepSize);
        //        await Task.Delay(delayTime * 5);
        //    }

        //    //_DebugContinue = false;
        //    //await WaitForDebugContinue();

        //    // Reculer de 1 pour revenir au point net
        //    ManualFocus(0, stepSize);
        //    focusStackStepVar = 0;
        //    UpdateFocusStepVarLbl(focusStackStepVar);
        //    await Task.Delay(delayTime);


        //    // 📊 Affichage du graphique
        //    await DisplayBlurGraph(blurDataDict);

        //}



        public async Task AutomaticFocusThenCapture(int focusIterations, CancellationToken cancellationToken = default)
        {
            if (_stopRequested) return;

            await WaitIfSequencePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            int newStepSize = stepSize;
            Invoke(new Action(() =>
            {
                btn_stopAutomaticFocusCapture.Visible = true;
                btn_stopAutomaticFocusCapture.Enabled = true;
            }));

            
            if (projet.FocusStackEnabled)
            {
                try
                {
                    string[] imageFiles = Directory.GetFiles(projet.GetTempImageFolderPath(), "*.*")
                           .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                          file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                          file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                           .ToArray();

                    foreach (string imageFile in imageFiles)
                    {
                        try
                        {
                            File.Delete(imageFile);
                        }
                        catch (Exception ex)
                        {
                            AppendTextToConsoleNL(ex.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    AppendTextToConsoleNL(e.Message);
                }

            }
                       

            if (focusIterations > maxNbrPicturesAllowed)
            {
                newStepSize = stepSize * focusIterations / maxNbrPicturesAllowed;
                focusIterations = maxNbrPicturesAllowed;
            }


            AppendTextToConsoleNL(focusIterations.ToString() + " photos seront prises à " + newStepSize.ToString() + " steps  (Settings / Détection flou / Nbr de photo max)");

            int iterationsCompletees = 0;

            for (int i = 0; i < focusIterations; i++)
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (_stopRequested) return;
                if (lbl_StackSerie.InvokeRequired)
                {

                    lbl_StackSerie.Invoke(new Action(() =>
                    {
                        lbl_StackSerie.Text = $"{i}/{focusIterations}";
                    }));
                }
                AppendTextToConsoleNL("blurredBlocks = " + blurredBlocks.ToString() + "  minDetect = " + minDetect.ToString());

                // Au départ seulement, on tente de revenir dans une zone utilisable.
                // Ensuite, la routine doit prendre le nombre de photos calculé, même si
                // le compteur de netteté varie pendant le balayage.
                for (int j = 0; i == 0 && j <= 3; j++)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_stopRequested) return;
                    if (blurredBlocks < minDetect)
                    {
                        // Reculer de 1 pour revenir au point net
                        Debug.WriteLine($"Ajustement du focus pour atteindre {minDetect}. En ce moment blurredBlocks = {blurredBlocks}");
                        await ManualFocusAsync(1, stepSize);
                        focusStackStepVar = 0;
                        UpdateFocusStepVarLbl(focusStackStepVar);
                        await Task.Delay(delayTime, cancellationToken);
                    }
                    else
                    {
                        break;
                    }
                }


                if (i == 0 && blurredBlocks < minDetect)
                {
                    AppendTextToConsoleNL("Capture focus stack annulée: impossible de retrouver une zone nette au départ. blurredBlocks = " + blurredBlocks.ToString() + " et minDetect = " + minDetect.ToString());
                    _stopRequested = true;
                    return;
                }

                if (!await WaitForFocusStackSharpnessAsync(cancellationToken))
                {
                    AppendTextToConsoleNL(
                        $"Focus stack arrêté avant la photo {i + 1}/{focusIterations}: netteté insuffisante " +
                        $"(blurredBlocks = {blurredBlocks}, minDetect = {minDetect}).");
                    break;
                }

                if (_stopRequested)
                {
                    Invoke(new Action(() =>
                    {
                        MessageBox.Show("Capture automatique interrompue.");
                        btn_stopAutomaticFocusCapture.Visible = false;
                        btn_stopAutomaticFocusCapture.Enabled = false;
                    }));

                    return;
                }

                try
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    await takePictureAsync();
                    AppendTextToConsoleNL("photo prise... onto the next :)");
                    await Task.Delay(600, cancellationToken);

                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    await ManualFocusAsync(0, newStepSize);
                    await Task.Delay(delayTime, cancellationToken);
                    focusStackStepVar += 1;
                    UpdateFocusStepVarLbl(focusStackStepVar);
                    projet.FocusSerieIncrement += 1;
                    SavePrefsSettings();
                }
                catch (Exception e)
                {
                    AppendTextToConsoleNL(e.Message);
                    _stopRequested = true;
                    throw;
                }

                Debug.WriteLine("itération " + i.ToString() + " blurredBLocks: " + blurredBlocks.ToString());


                iterationsCompletees += 1;
            }

            projet.FocusSerieIncrement = 0;
            SavePrefsSettings();

            if (!_stopRequested)
            {
                Invoke(new Action(() =>
                {
                    aerolitheTabControl2.SelectTab("tabPage20");
                    aerolitheTabControl3.SelectTab("tabPage26");
                    //tabControl4.SelectedTab = tabPage17;
                }));
            }

        }

        private async Task<bool> WaitForFocusStackSharpnessAsync(CancellationToken cancellationToken)
        {
            int stableSharpSamples = 0;
            int sampleDelayMs = Math.Max(150, delayTime);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(700, sampleDelayMs * 4));

            while (DateTime.UtcNow < deadline)
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (_stopRequested)
                {
                    return false;
                }

                if (blurredBlocks >= minDetect)
                {
                    stableSharpSamples++;
                    if (stableSharpSamples >= 2)
                    {
                        return true;
                    }
                }
                else
                {
                    stableSharpSamples = 0;
                }

                await Task.Delay(sampleDelayMs, cancellationToken);
            }

            return false;
        }

    }

}
