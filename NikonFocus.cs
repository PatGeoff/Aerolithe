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
        public int focusStackStepVar = 0;
        private bool _stopRequested = false;
        public double currentCrosshairX = 0;
        public ScottPlot.Plottables.Crosshair crosshair;
        public ScottPlot.WinForms.FormsPlot formsPlot;


        public async Task nikonDoFocus()
        {
            try
            {
                focusReadyTcs = new TaskCompletionSource<bool>();
                await Task.Run(() => NikonAutofocus());
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
            if (device.LiveViewEnabled)
            {
                device.LiveViewEnabled = false;
                await Task.Delay(100);
                // liveViewTimer.Stop();
            }

            bool focusCompleted = false;
            int essai = 0;
            while (!focusCompleted)
            {
                try
                {
                    device.Start(eNkMAIDCapability.kNkMAIDCapability_AutoFocus);
                    focusCompleted = true; // Set focusCompleted to true only if no exception occurs
                }
                catch (NikonException ex)
                {
                    // AppendTextToConsoleNL(ex.Message);
                    if (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
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

            focusStackStepVar = 0;
            focusStackStepVar = 0;

            if (lbl_focusStepsVar.InvokeRequired)
            {
                lbl_focusStepsVar.Invoke(new Action(() =>
                {
                    lbl_focusStepsVar.Text = focusStackStepVar.ToString();
                }));
            }
            else
            {
                lbl_focusStepsVar.Text = focusStackStepVar.ToString();
            }

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
            formsPlot.Plot.Title($"<<   X: {currentCrosshairX:F0}   >> ");
        }


        public async Task AutomaticFocusMapping()
        {
            int iterationCount = 25;
            int stepSize = (int)hScrollBar_driveStep.Value;
            int position = 0;
            int delayTime = 75;
            List<FocusMap> focusMaps = new List<FocusMap>();

            await NikonAutofocus();

            // Première passe : reculer (non stockée)    btn_focusMinus_Click:  ManualFocus(1,stepSize)
            for (int i = 0; i < iterationCount; i++)
            {
                position -= 1;
                ManualFocus(1, stepSize);
                await Task.Delay(delayTime);
                UpdateFocusStepVarLbl(position);
            }


            // Deuxième passe : avancer (stockée)        btn_focusPlus_Click: ManualFocus(0,stepSize) 
            for (int i = -iterationCount; i <= iterationCount; i++)
            {
                position += 1;
                ManualFocus(0, stepSize);
                await Task.Delay(delayTime);
                
                UpdateFocusStepVarLbl(position);
            }

            // Troisième passe : reculer (non stockée)    btn_focusMinus_Click:  ManualFocus(1,stepSize)
            for (int i = 0; i < iterationCount; i++)
            {
                position -= 1;
                ManualFocus(1, stepSize);
                await Task.Delay(delayTime);
                UpdateFocusStepVarLbl(position);
            }
        }

        public async Task AutomaticFocusRoutine()
        {

            await NikonAutofocus();


            int iterationCount = 25;
            int stepSize = (int)hScrollBar_driveStep.Value;
            int position = 0;
            int delayTime = 75;

            var blurDataDict = new Dictionary<int, (double blur, string direction)>();

            // Première passe : reculer (non stockée)    btn_focusMinus_Click:  ManualFocus(1,stepSize)
            for (int i = 0; i < iterationCount; i++)  
            {
                position -= 1;
                ManualFocus(1, stepSize);
                await Task.Delay(delayTime);
                UpdateFocusStepVarLbl(position);
            }


            // Deuxième passe : avancer (stockée)        btn_focusPlus_Click: ManualFocus(0,stepSize) 
            for (int i = -iterationCount; i <= iterationCount; i++) 
            {
                position += 1;
                ManualFocus(0, stepSize);
                await Task.Delay(delayTime);
                blurDataDict[i] = (blurrynessAmountMask, $"{stepSize}");
                UpdateFocusStepVarLbl(position);
            }

            // Troisième passe : reculer (non stockée)    btn_focusMinus_Click:  ManualFocus(1,stepSize)
            for (int i = 0; i < iterationCount; i++)
            {
                position -= 1;
                ManualFocus(1, stepSize);
                await Task.Delay(delayTime);
                UpdateFocusStepVarLbl(position);
            }

            position = 0;

            double maxDelta = 0;
            double secondMaxDelta = 0;
            int bestIndex = 0;
            int secondBestIndex = 0;

            var sortedKeys = blurDataDict.Keys.OrderByDescending(k => k).ToList();

            for (int i = 1; i < sortedKeys.Count; i++)
            {
                int prev = sortedKeys[i - 1];
                int curr = sortedKeys[i];
                double delta = Math.Abs(blurDataDict[curr].blur - blurDataDict[prev].blur);

                if (delta > maxDelta)
                {
                    // Décale le meilleur vers le second
                    secondMaxDelta = maxDelta;
                    secondBestIndex = bestIndex;

                    maxDelta = delta;
                    bestIndex = curr;
                }
                else if (delta > secondMaxDelta)
                {
                    secondMaxDelta = delta;
                    secondBestIndex = curr;
                }
            }

            // Ajustement : si bestIndex > secondBestIndex, bestIndex = i - 1
            if (bestIndex > secondBestIndex)
            {
                bestIndex = sortedKeys[sortedKeys.IndexOf(bestIndex) - 1];
            }
            else
            {
                secondBestIndex = sortedKeys[sortedKeys.IndexOf(secondBestIndex) - 1];
            }


            AppendTextToConsoleNL($"Focus optimal détecté à la position : {bestIndex} avec un delta de flou de {maxDelta:F4}");

            // Retour à la position optimale
            int stepsToReturn = Math.Abs(bestIndex);
            bool goForward = bestIndex > 0;

            for (int i = 0; i < stepsToReturn; i++)
            {

                if (goForward)
                {
                    position += 1;
                    ManualFocus(1, stepSize);
                    UpdateFocusStepVarLbl(position);
                }

                else
                {
                    position -= 1;
                    UpdateFocusStepVarLbl(position);
                    ManualFocus(0, stepSize);
                }

                int focusStepCount = Math.Abs(bestIndex - secondBestIndex);

                textBox_nbrFocusSteps.Text = focusStepCount.ToString();

                //position = 0;

                //if (lbl_focusStepsVar.InvokeRequired)
                //{
                //    lbl_focusStepsVar.Invoke(new Action(() =>
                //    {
                //        lbl_focusStepsVar.Text = position.ToString();
                //    }));
                //}
                //else
                //{
                //    lbl_focusStepsVar.Text = position.ToString();
                //}
                await Task.Delay(100);
            }

           
            // 📊 Affichage du graphique
            formsPlot = new ScottPlot.WinForms.FormsPlot
            {
                Dock = DockStyle.Fill
            };

            tabControl3.TabPages["tabPage15"].Controls.Clear();
            tabControl3.TabPages["tabPage15"].Controls.Add(formsPlot);
            tabControl3.SelectedTab = tabControl3.TabPages["tabPage15"];


            // Préparation des données
            var blurDataList = blurDataDict.ToList();
            double[] xs = blurDataList.Select(pair => (double)pair.Key).ToArray();
            double[] ys = blurDataList.Select(pair => pair.Value.blur).ToArray();

            // Tracé principal
            formsPlot.Plot.Add.Scatter(xs, ys);

            // Styles
            formsPlot.Plot.DataBackground.Color = Colors.DarkGray;

            // Initialisation du crosshair
            currentCrosshairX = bestIndex; // ou une autre valeur initiale
            crosshair = formsPlot.Plot.Add.Crosshair(currentCrosshairX, blurDataDict[bestIndex].blur);
            UpdateCrosshairTitle();

            // Interaction souris
            formsPlot.MouseMove += (s, e) =>
            {
                Pixel mousePixel = new(e.X, e.Y);
                Coordinates mouseCoordinates = formsPlot.Plot.GetCoordinates(mousePixel);

                currentCrosshairX = Math.Round(mouseCoordinates.X);
                crosshair.X = currentCrosshairX;
                UpdateCrosshairTitle();
                formsPlot.Refresh();
            };

            // Titre et axes
            formsPlot.Plot.Title("Évolution du flou (seconde passe)");
            formsPlot.Plot.Axes.Bottom.Label.Text = "Position relative";
            formsPlot.Plot.Axes.Left.Label.Text = "Niveau de flou";

            // Marqueurs verticaux
            var line = formsPlot.Plot.Add.VerticalLine(bestIndex);
            line.Color = ScottPlot.Colors.Red;
            var line2 = formsPlot.Plot.Add.VerticalLine(secondBestIndex);
            line2.Color = ScottPlot.Colors.Red;

            // Texte au-dessus des points optimaux
            formsPlot.Plot.Add.Text($"Optimal: {bestIndex}", bestIndex, blurDataDict[bestIndex].blur + 0.05);
            formsPlot.Plot.Add.Text($"Optimal: {secondBestIndex}", secondBestIndex, blurDataDict[secondBestIndex].blur + 0.05);
            formsPlot.Refresh();


        }


        public async Task AutomaticFocusThenCapture()
        {
            Invoke(new Action(() =>
            {
                btn_stopAutomaticFocusCapture.Visible = true;
                btn_stopAutomaticFocusCapture.Enabled = true;
            }));

            _stopRequested = false;

            if (int.TryParse(textBox_nbrFocusSteps.Text, out int steps))
            {
                int stepSize = (int)hScrollBar_driveStep.Value;
                int delayTime = 200;

                if (checkBox_StackAuto.Checked)
                {
                    try
                    {
                        string[] imageFiles = Directory.GetFiles(imagesFolderPath, "*.*")
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

                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AppendTextToConsoleNL(e.Message);
                    }
                                      
                }

                for (int i = 0; i < steps; i++)
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

                    await takePictureAsync();
                    await Task.Delay(delayTime);
                    ManualFocus(0, stepSize);
                    await Task.Delay(delayTime);
                }

                if (!_stopRequested)
                {
                    Invoke(new Action(() =>
                    {
                        tabControl4.SelectedTab = tabPage17;
                    }));
                    await MakeFocusStackSerie();
                }

                Invoke(new Action(async () =>
                {
                    btn_stopAutomaticFocusCapture.Visible = false;
                    btn_stopAutomaticFocusCapture.Enabled = false;
                    if (int.TryParse(textBox_nbrFocusSteps.Text, out int stepBack))
                    {
                        stepBack = stepBack / 2;
                        for (int i = 0; i <= stepBack; i++)
                        {
                            ManualFocus(1, stepSize);                            
                            if (lbl_focusStepsVar.InvokeRequired)
                            {
                                lbl_focusStepsVar.Invoke(new Action(() =>
                                {
                                    lbl_focusStepsVar.Text = i.ToString();
                                }));
                            }
                            else
                            {
                                lbl_focusStepsVar.Text = i.ToString();
                            }
                            await Task.Delay(100);
                        }
                    }
                    if (checkBox_ApplyMaskStackedImage.Checked)
                    {
                        PostFocusStackMask();
                    }
                   
                }));

               

            }
        }


    }

}