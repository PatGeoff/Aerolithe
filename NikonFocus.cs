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

namespace Aerolithe
{


    public partial class Aerolithe : Form
    {
        private TaskCompletionSource<bool> focusReadyTcs;

        public Stopwatch sw;
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
            try
            {
                focusReadyTcs = new TaskCompletionSource<bool>();
                await Task.Run(() => NikonAutofocus());
                focusStackStepVar = 0;
                UpdateFocusStepVarLbl(focusStackStepVar);
                //MessageBox.Show("image capturée");
            }
            catch (Exception ex)
            {
                if (!device.LiveViewEnabled)
                {
                    device.LiveViewEnabled = true;
                    await Task.Delay(100);
                    liveViewTimer.Start();
                }
                throw new Exception("Autofocus failed due to an error: " + ex.Message);
            }

        }

        public async Task NikonAutofocus()
        {
            //var sw = Stopwatch.StartNew();
            //Debug.WriteLine($"[NikonAutoFocus()] lancement à : {sw.ElapsedMilliseconds} ms");
            try
            {
                if (device.LiveViewEnabled)
                {
                    device.LiveViewEnabled = false;
                }

                await Task.Delay(100);
            }
            catch (Exception)
            {

                MessageBox.Show("Si la caméra n'est pas allumée, la fermer et la rallumer");
            }


            bool focusCompleted = false;
            int essai = 0;
            while (!focusCompleted)
            {
                try
                {
                    device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus);
                    focusCompleted = true; // Set focusCompleted to true only if no exception occurs
                    focusStackStepVar = 0;
                    UpdateFocusStepVarLbl(focusStackStepVar);
                }
                catch (NikonException ex)
                {
                    // AppendTextToConsoleNL(ex.Message);
                    if (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
                        AppendTextToConsoleNL(ex.Message);
                        await Task.Delay(100); // Wait before retrying
                        continue; // Retry autofocus
                    }
                    else
                    {

                        if (!device.LiveViewEnabled)
                        {
                            device.LiveViewEnabled = true;
                            await Task.Delay(100);
                            liveViewTimer.Start();
                        }
                        //throw new Exception("Autofocus failed due to an error: " + ex.Message);
                        //AppendTextToConsoleNL("Impossible de faire le focus, erreur: " + ex.Message);
                    }
                }
                essai += 1;
                if (essai > 10) break;
            }
            //Debug.WriteLine($"[NikonAutoFocus()] avant de remettre le liveView  : {sw.ElapsedMilliseconds} ms");
            if (!device.LiveViewEnabled)
            {
                device.LiveViewEnabled = true;
                await Task.Delay(100);
                liveViewTimer.Start();
            }

            //focusStackStepVar = 0;

            UpdateFocusStepVarLbl(focusStackStepVar);

            //Debug.WriteLine($"[NikonAutoFocus()] fin à : {sw.ElapsedMilliseconds} ms");

        }

        private async Task UpdateFocusStepVarLbl(int position)
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

        private async Task DisplayBlurGraph(Dictionary<int, (int steps, int blurBlocks)> blurDataDict)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => DisplayBlurGraph(blurDataDict)));
                return;
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
        }


        public async Task AutomaticFocusRoutine()
        {
            if (_stopRequested) return;
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
            
            var blurDataDict = new Dictionary<int, (int steps, int blurBlocks)>();

            int maxTargetDown = 0;
            int maxUpperPosition = 0;
            int maxTargetUp = -1;

            // maxTargetDown = le focusStackStepVar le plus bas (négatif) où il y a eu une détection. Plus bas que ça c'est flou
            // maxTargetUp = le focusStackStepVar le plus haut où il ya détection. En haut de ça c'est flou
            // maxUpperPosition = le focusStackStepVar le plus haut où la caméra s'est rendue, très probablement 4 steps de plus que maxTargetUp
            // Delta = le range entre les deux maxTargetDown et maxTargetUp

            ////////////////////////////////////////////////
            //  Première passe : reculer (non stockée dans blurDataDict) ////////////////////////////////////////////////

            ManualFocus(1, stepSize * iterations);
            focusStackStepVar = iterations * -1;
            UpdateFocusStepVarLbl(focusStackStepVar);

            AppendTextToConsoleNL("focusStackStepVar = " + focusStackStepVar.ToString());
            await Task.Delay(500);

            if (_stopRequested) return;

            //  Reculer davantage si flou encore détecté
            while (blurredBlocks >= minDetect && !_stopRequested)
            {
                if (_stopRequested) return;
                ManualFocus(1, stepSize);
                focusStackStepVar -= 1;
                UpdateFocusStepVarLbl(focusStackStepVar);                
                await Task.Delay(delayTime);
            }

            maxTargetDown = focusStackStepVar;

            //AppendTextToConsoleNL("focusStackStepVar = " + focusStackStepVar.ToString());
            //AppendTextToConsoleNL($"maxTargetDown = {maxTargetDown}");

            await Task.Delay(200);


            int blurConsecutiveCount = 0;
            int i = 0;

            ////////////////////////////////////////////////
            //  Deuxième passe : On monte jusqu'à ce qu'on ait 4 flous consécutifs (stockée dans blurDataDict) ////////////////////////////////////////////////
            
            while (!_stopRequested)
            {
                ManualFocus(0, stepSize);
                await Task.Delay(delayTime);

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

                focusStackStepVar += 1;
                UpdateFocusStepVarLbl(focusStackStepVar);

                if (blurConsecutiveCount >= 4 && focusStackStepVar > 0)
                {
                    //AppendTextToConsoleNL("Arrêt anticipé : 4 flous consécutifs détectés.");
                    break;
                }
                i++;
            }


            //AppendTextToConsoleNL("-- maxTargetUp = " + maxTargetUp.ToString());


            maxUpperPosition = focusStackStepVar;
            // delta = nombre de steps maximum
            delta = maxTargetUp + Math.Abs(maxTargetDown);
            //AppendTextToConsoleNL("-- maxUpperPosition = " + maxUpperPosition.ToString());
            //AppendTextToConsoleNL($"delta ({delta.ToString()}) =  steps entre max up et max down à {stepSize} stepSize");

            await Task.Delay(500);

            ////////////////////////////////////////////////
            // Troisième passe : retour à la première détection    ////////////////////////////////////////////////
            

            if (_stopRequested) return;
            //int returnToStartSteps = (maxUpperPosition - maxTargetUp) / 2;
            //ManualFocus(1, returnToStartSteps * stepSize);


            AppendTextToConsoleNL("stepSize = " + stepSize.ToString());
            AppendTextToConsoleNL("delta = " + delta.ToString());
            int steps = (int)(delta * stepSize * 0.75);
            AppendTextToConsoleNL(steps.ToString());

            ManualFocus(1,steps);
            await Task.Delay(500);
            focusStackStepVar = maxTargetUp;
            UpdateFocusStepVarLbl(maxTargetUp);

            if (blurredBlocks < minDetect)
            {
                while (blurredBlocks < minDetect && !_stopRequested)
                {
                    if (_stopRequested) break;
                    ManualFocus(0, stepSize);
                    await Task.Delay(delayTime * 5);
                }
            }

            // Ajustement fin
            while (blurredBlocks > minDetect*2 && !_stopRequested)
            {
                if (_stopRequested) break;
                ManualFocus(1, stepSize);
                await Task.Delay(delayTime * 5);
            }

            //_DebugContinue = false;
            //await WaitForDebugContinue();

            // Reculer de 1 pour revenir au point net
            ManualFocus(0, stepSize);
            focusStackStepVar = 0;
            UpdateFocusStepVarLbl(focusStackStepVar);
            await Task.Delay(delayTime);


            // 📊 Affichage du graphique
            await DisplayBlurGraph(blurDataDict);
            
        }

     

        public async Task AutomaticFocusThenCapture(int focusIterations)
        {
            
            int newStepSize = stepSize;
            Invoke(new Action(() =>
            {
                btn_stopAutomaticFocusCapture.Visible = true;
                btn_stopAutomaticFocusCapture.Enabled = true;
            }));

            if (checkBox_SeqFocusStack.Checked || checkBox_StackAuto.Checked)
            {
                try
                {
                    string[] imageFiles = Directory.GetFiles(projet.TempImageFolderPath, "*.*")
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
                //AppendTextToConsoleNL("blurredBlocks = " + blurredBlocks.ToString() + "  minDetect = " + minDetect.ToString());
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
                        await takePictureAsync(); 
                        await Task.Delay(400);
                        ManualFocus(0, newStepSize);
                        await Task.Delay(delayTime);
                        focusStackStepVar += 1;
                        UpdateFocusStepVarLbl(focusStackStepVar);
                    }
                    catch (Exception e)
                    {
                        AppendTextToConsoleNL(e.Message);
                    }
                    Debug.WriteLine("itération " + i.ToString() + " blurredBLocks: " + blurredBlocks.ToString());
                }

                iterationsCompletees += 1;

   
            }
            
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