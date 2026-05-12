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

                bool focusCompleted = false;
                int essai = 0;

                while (!focusCompleted)
                {
                    try
                    {
                        device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus);
                        focusCompleted = true;
                        focusStackStepVar = 0;
                        UpdateFocusStepVarLbl(focusStackStepVar);
                    }
                    catch (NikonException ex) when (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
                        AppendTextToConsoleNL(ex.Message);
                        await Task.Delay(200);
                        continue;
                    }

                    essai += 1;
                    if (essai > 10) break;
                }
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

            if (tabControl3.InvokeRequired)
            {
                tabControl3.Invoke(new Action(() =>
                {
                    tabControl3.TabPages["tabPage15"].Controls.Clear();
                    tabControl3.TabPages["tabPage15"].Controls.Add(formsPlot);
                    tabControl3.SelectedTab = tabControl3.TabPages["tabPage15"];
                }));
            }
            else
            {
                tabControl3.TabPages["tabPage15"].Controls.Clear();
                tabControl3.TabPages["tabPage15"].Controls.Add(formsPlot);
                tabControl3.SelectedTab = tabControl3.TabPages["tabPage15"];
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


        public async Task AutomaticFocusRoutine(CancellationToken cancellationToken = default)
        {
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
                if (_stopRequested) return;

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
                if (_stopRequested) return;

                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(600, cancellationToken);
                if (_stopRequested) return;


                // Clone du masque Live
                Mat uiClone;
                lock (_maskLock)
                {
                    if (maskMatLive == null || maskMatLive.IsEmpty)
                    {
                        AppendTextToConsoleNL("AutomaticFocusRoutine: masque live nul ou vide.");
                        return;
                    }

                    uiClone = maskMatLive.Clone();
                }

                // ===============================
                // 1) TEST DU MASQUE NOIR
                // ===============================


                if (IsMatAllBlack(uiClone))
                {
                    maskFreeze = false;
                    int originalThresh = 20;
                    Invoke(new Action(() =>
                    {
                        btn_freezeMask.Text = "";
                        originalThresh = hScrollBar_liveMaskThresh.Value;
                    }));

                    bool foundValidMask = false;
                    IEnumerable<int> thresholdCandidates = GetAutomaticFocusMaskThresholdCandidates(originalThresh);
                    bool[] invertCandidates = appSettings.MaskAlgorithmIndex == 0
                        ? new[] { false }
                        : new[] { false, true };

                    foreach (int t in thresholdCandidates)
                    {
                        await WaitIfSequencePausedAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (foundValidMask || _stopRequested) break;

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

                            // Première acquisition du masque
                            Mat testMask = await BrightnessMaskFromBytesMat(
                                imageView.JpegBuffer,
                                t,
                                invert
                            );

                            // Si déjà noir → on passe au mode/seuil suivant
                            if (!IsMaskUsableForAutomaticFocus(testMask))
                            {
                                testMask.Dispose();
                                continue;
                            }

                            // Sinon → le masque est non-noir → on surveille 1 seconde
                            bool stayedValidFor1s = true;
                            var start = DateTime.Now;

                            while ((DateTime.Now - start).TotalMilliseconds < 700 && !_stopRequested)
                            {
                                await WaitIfSequencePausedAsync(cancellationToken);
                                cancellationToken.ThrowIfCancellationRequested();

                                await Task.Delay(50, cancellationToken); // petite attente pour éviter trop de CPU

                                Mat testMask2 = await BrightnessMaskFromBytesMat(
                                    imageView.JpegBuffer,
                                    t,
                                    invert
                                );

                                // Si ça redevient noir avant la fin → t ne convient pas
                                if (!IsMaskUsableForAutomaticFocus(testMask2))
                                {
                                    stayedValidFor1s = false;
                                    testMask2.Dispose();
                                    break;
                                }

                                testMask2.Dispose();
                            }

                            testMask.Dispose();

                            if (_stopRequested) return;

                            if (stayedValidFor1s)
                            {
                                // Valeur validée : stable pendant 1 seconde
                                Mat finalMask = await BrightnessMaskFromBytesMat(
                                    imageView.JpegBuffer,
                                    t,
                                    invert
                                );

                                uiClone?.Dispose();
                                uiClone = finalMask.Clone();
                                finalMask.Dispose();

                                foundValidMask = true;
                                break;
                            }
                        }
                    }

                    // Rien trouvé
                    if (!foundValidMask && !_stopRequested)
                    {
                        foreach (bool invert in invertCandidates)
                        {
                            await WaitIfSequencePausedAsync(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

                            Mat autoMask = await BrightnessMaskFromBytesMat(
                                imageView.JpegBuffer,
                                -1,
                                invert
                            );

                            if (IsMaskUsableForAutomaticFocus(autoMask))
                            {
                                uiClone?.Dispose();
                                uiClone = autoMask.Clone();
                                foundValidMask = true;
                            }

                            autoMask.Dispose();

                            if (foundValidMask)
                            {
                                break;
                            }
                        }
                    }

                    if (_stopRequested) return;

                    if (!foundValidMask)
                    {
                        Invoke(new Action(() =>
                        {
                            hScrollBar_liveMaskThresh.Value = originalThresh;
                        }));

                        MessageBox.Show(
                            this,
                            "Aucune valeur de seuil n’a donné un masque stable et utilisable.\n" +
                            "Vérifiez l’éclairage, la mise au point ou la luminosité.",
                            "Erreur - Masque impossible",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );

                        return;
                    }

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


                // ====== SAUVEGARDE DU MASQUE ======
                // Source volontaire: ce qui est affiché dans picBox_liveMaskLum au moment de la sauvegarde.
                await SaveDisplayedMaskAsPngAsync(projet.GetMaskFullImagePath());



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
                if (_stopRequested) return;

                // Reculer jusqu'à ce que flou disparaît
                while (blurredBlocks >= minDetect && !_stopRequested)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_stopRequested) return;
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

                if (_stopRequested) return;

                maxUpperPosition = focusStackStepVar;
                delta = maxTargetUp + Math.Abs(maxTargetDown);

                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(500, cancellationToken);
                if (_stopRequested) return;

                // ====== 3) Retour au point net ======
                int steps = (int)(delta * stepSize * 0.75);
                AppendTextToConsoleNL($"stepSize={stepSize}, delta={delta}, steps={steps}");

                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await ManualFocusAsync(1, steps);
                await Task.Delay(500, cancellationToken);
                if (_stopRequested) return;

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

                if (_stopRequested) return;

                await ManualFocusAsync(0, stepSize);
                focusStackStepVar = 0;
                UpdateFocusStepVarLbl(focusStackStepVar);
                await Task.Delay(delayTime, cancellationToken);
                if (_stopRequested) return;

                await DisplayBlurGraph(blurDataDict);

                AppendTextToConsoleNL($"AutomaticFocusRoutine terninée");
            }
            finally
            {
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

                // essayer de faire l'autofocus jusqu'à 3 fois, sinon on quitte
                for (int j = 0; j <= 3; j++)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_stopRequested) return;
                    if (i == 0 && blurredBlocks < minDetect)
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


                if (blurredBlocks >= minDetect)
                {
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
                        //await miniaturesTcs.Task;
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
                }
                else
                {
                    AppendTextToConsoleNL("On a un problème de comparaison entre \n         blurredBlocks = " + blurredBlocks.ToString() + " et  minDetect = " + minDetect.ToString());
                }


                iterationsCompletees += 1;
            }

            projet.FocusSerieIncrement = 0;
            SavePrefsSettings();

            if (!_stopRequested)
            {
                Invoke(new Action(() =>
                {
                    tabControl4.SelectedTab = tabPage17;
                }));
            }

        }

    }

}
