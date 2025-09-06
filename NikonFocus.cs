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

namespace Aerolithe
{
    

    public partial class Aerolithe : Form
    {
        private TaskCompletionSource<bool> focusReadyTcs;

        public Stopwatch sw;
        public int focusStackStepVar = 0;
        private bool _stopRequested = false;

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


        public async Task AutomaticFocusRoutine()
        {
            int iterationCount = 25;
            int stepSize = (int)hScrollBar_driveStep.Value;
            int position = 0;
            int delayTime = 50;

            var blurDataDict = new Dictionary<int, (double blur, string direction)>();

            // Première passe : avancer (non stockée)
            for (int i = 0; i < iterationCount; i++)
            {
                ManualFocus(1, stepSize);
                await Task.Delay(delayTime);
                //AppendTextToConsoleNL($"Avance {i}: {blurrynessAmountMask:F4}");
            }

            // Deuxième passe : reculer (stockée)
            for (int i = -iterationCount; i <= iterationCount; i++)
            {
                ManualFocus(0, stepSize);
                await Task.Delay(delayTime);
                blurDataDict[i] = (blurrynessAmountMask, $"{stepSize}");
                //AppendTextToConsoleNL($"Recule {i}: {blurrynessAmountMask:F4}");
            }

            // Troisième passe : avancer (non stockée)
            for (int i = 0; i < iterationCount; i++)
            {
                ManualFocus(1, stepSize);
                await Task.Delay(delayTime);
                //AppendTextToConsoleNL($"Avance {i}: {blurrynessAmountMask:F4}");
            }

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

            //// Retour à la position optimale
            //int stepsToReturn = Math.Abs(bestIndex);
            //bool goForward = bestIndex < 0;

            //for (int i = 0; i < stepsToReturn; i++)
            //{
            //    if (goForward)
            //        ManualFocus(1, stepSize);
            //    else
            //        ManualFocus(0, stepSize);

            //    await Task.Delay(100);
            //}

            //AppendTextToConsoleNL($"Retour effectué en direction {(goForward ? "avant" : "arrière")} sur {stepsToReturn} pas.");

            // 📊 Affichage du graphique
            var formsPlot = new ScottPlot.WinForms.FormsPlot
            {
                Dock = DockStyle.Fill
            };

            tabControl3.TabPages["tabPage15"].Controls.Clear();
            tabControl3.TabPages["tabPage15"].Controls.Add(formsPlot);

            // Préparation des données
            var blurDataList = blurDataDict.ToList();
            double[] xs = blurDataList.Select(pair => (double)pair.Key).ToArray();
            double[] ys = blurDataList.Select(pair => pair.Value.blur).ToArray();


            // Tracé principal
            formsPlot.Plot.Add.Scatter(xs, ys);

            // Styles
            formsPlot.Plot.DataBackground.Color = Colors.DarkGray;

            var crosshair = formsPlot.Plot.Add.Crosshair(2, 5);

            formsPlot.MouseMove += (s, e) =>
            {
                Pixel mousePixel = new(e.X, e.Y);
                Coordinates mouseCoordinates = formsPlot.Plot.GetCoordinates(mousePixel);

                double snappedX = Math.Round(mouseCoordinates.X);
                crosshair.X = snappedX;

                

                formsPlot.Plot.Title($"<<   X: {snappedX:F0}   >> ");
                formsPlot.Refresh();
            };



            // Titre et axes
            formsPlot.Plot.Title("Évolution du flou (seconde passe)");
            formsPlot.Plot.Axes.Bottom.Label.Text = "Position relative";
            formsPlot.Plot.Axes.Left.Label.Text = "Niveau de flou";

            // Marqueur vertical pointillé pour le focus optimal
            var line = formsPlot.Plot.Add.VerticalLine(bestIndex);
            line.Color = ScottPlot.Colors.Red;
            var line2 = formsPlot.Plot.Add.VerticalLine(secondBestIndex);
            line2.Color = ScottPlot.Colors.Red;
            //line.LineStyle = ScottPlot.LineStyle.Dash;


            // Texte au-dessus du point optimal
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
                   
                    //string folder = txtBox_nomImages.Text + textBox_imgIncr.Text;
                    //imagesFolderPath = Path.Combine(imagesFolderPath, folder);
                    //// Vérifie si le dossier existe, sinon le crée
                    //if (!Directory.Exists(imagesFolderPath))
                    //{
                    //    Directory.CreateDirectory(imagesFolderPath);
                    //    AppendTextToConsoleNL("Dossier " + imagesFolderPath + " créé");
                    //}
                    //else
                    //{

                    //    string[] imageFiles = Directory.GetFiles(imagesFolderPath, "*.*")
                    //            .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    //                           file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    //                           file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    //            .ToArray();

                    //    foreach (string imageFile in imageFiles)
                    //    {
                    //        try
                    //        {
                    //            File.Delete(imageFile);
                    //        }
                    //        catch (Exception ex)
                    //        {

                    //        }
                    //    }
                    //}
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