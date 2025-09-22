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
        public int delayTime = 50;
        public int minDetect = 5;
        public int iterations = 24;
        double blurThreshold = 100.0;
        public int blurredBlocks = 0;
        public int stepSize = 0;
        public int delta = 0;


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

        public async Task AutomaticFocusRoutine()
        {

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
            stepSize = (int)hScrollBar_driveStep.Value;
            var blurDataDict = new Dictionary<int, (int steps, int blurBlocks)>();

            int maxTargetDown = 0;
            int maxTargetUp = 0;
            int firstDetectionIndex = -1;

            // 🟠 Première passe : reculer (non stockée)
            int initialBackSteps = iterations;
            ManualFocus(1, stepSize * initialBackSteps);
            focusStackStepVar -= initialBackSteps;
            UpdateFocusStepVarLbl(focusStackStepVar);
            await Task.Delay(500);

            // 🔁 Reculer davantage si flou encore détecté
            while (blurredBlocks >= minDetect && !_stopRequested)
            {
                ManualFocus(1, stepSize);
                focusStackStepVar -= 1;
                UpdateFocusStepVarLbl(focusStackStepVar);
                await Task.Delay(delayTime);
            }

            maxTargetDown = focusStackStepVar;
            //AppendTextToConsoleNL($"maxTargetDown = {maxTargetDown}");

            await Task.Delay(500);

            // 🟢 Deuxième passe : avancer (stockée)
            int returnSteps = Math.Abs(maxTargetDown) + iterations;
            //AppendTextToConsoleNL($"returnSteps = {returnSteps}");

            for (int i = 0; i < returnSteps; i++)
            {
                if (_stopRequested)
                {
                    AppendTextToConsoleNL("_stopRequested");
                    break;
                }
                ManualFocus(0, stepSize);
                await Task.Delay(delayTime);

                blurDataDict[i] = (focusStackStepVar, blurredBlocks);
                UpdateFocusStepVarLbl(focusStackStepVar);

                if (blurredBlocks >= minDetect && firstDetectionIndex == -1)
                    firstDetectionIndex = focusStackStepVar;

                focusStackStepVar += 1;
            }

            // 🔁 Avancer davantage si flou encore détecté

            while (blurredBlocks >= minDetect && !_stopRequested)
            {
                ManualFocus(0, stepSize);
                await Task.Delay(delayTime);

                blurDataDict[blurDataDict.Count] = (focusStackStepVar, blurredBlocks);
                UpdateFocusStepVarLbl(focusStackStepVar);

                if (firstDetectionIndex == -1)
                    firstDetectionIndex = focusStackStepVar;

                focusStackStepVar += 1;
            }

            maxTargetUp = focusStackStepVar;
            delta = maxTargetUp + Math.Abs(maxTargetDown);
            //AppendTextToConsoleNL($"maxTargetUp = {maxTargetUp}");
            //AppendTextToConsoleNL($"delta = {delta}");
            //AppendTextToConsoleNL($"Première détection à = {firstDetectionIndex}");

            await Task.Delay(500);

            // 🔵 Troisième passe : retour à la première détection          

            int returnToStartSteps = (maxTargetUp - firstDetectionIndex) / 2;
            ManualFocus(1, returnToStartSteps * stepSize);
            UpdateFocusStepVarLbl(focusStackStepVar);
            await Task.Delay(500);

            // Ajustement fin
            while (blurredBlocks > minDetect*2 && !_stopRequested)
            {
                ManualFocus(1, stepSize);
                await Task.Delay(delayTime * 10);
            }

            // Reculer de 1 pour revenir au point net
            ManualFocus(0, stepSize);
            focusStackStepVar = 0;
            UpdateFocusStepVarLbl(focusStackStepVar);
            await Task.Delay(delayTime);


            // 📊 Affichage du graphique
            await DisplayBlurGraph(blurDataDict);
            
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


        public async Task AutomaticFocusThenCapture(int focusIterations)
        {
            int newStepSize = stepSize;
            AppendTextToConsoleNL("AutomaticFocusThenCapture(" + focusIterations.ToString() + ")");
            Invoke(new Action(() =>
            {
                btn_stopAutomaticFocusCapture.Visible = true;
                btn_stopAutomaticFocusCapture.Enabled = true;
            }));

            _stopRequested = false;

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

                        }
                    }
                }
                catch (Exception e)
                {
                    AppendTextToConsoleNL(e.Message);
                }

            }

            if (focusIterations > 20)
            {
                newStepSize = stepSize * focusIterations / 20;
                focusIterations = 20;
                AppendTextToConsoleNL("stepSize = " + stepSize.ToString() + "  newStepSize = " + newStepSize.ToString());
            }

            for (int i = 0; i < focusIterations; i++)
            {
                AppendTextToConsoleNL("blurredBlocks = " + blurredBlocks.ToString() + "  minDetect = " + minDetect.ToString());
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
                }
                else // continuer l'autofocus sans prendre de photo
                {
                    AppendTextToConsoleNL("il y a un problène avec la détection. ");
                    ManualFocus(0, newStepSize);
                    await Task.Delay(delayTime);
                    focusStackStepVar += 1;
                    UpdateFocusStepVarLbl(focusStackStepVar);
                }
               
            }

            if (!_stopRequested)
            {
                Invoke(new Action(() =>
                {
                    tabControl4.SelectedTab = tabPage17;
                }));
            }

            if (checkBox_SeqFocusStack.Checked || checkBox_StackAuto.Checked)
            {
                AppendTextToConsoleNL("Focus Stack lancé");
                await MakeFocusStackSerie();
            }


            // appliquer le masque si nécessaire. 
            if (checkBox_ApplyMaskStackedImage.Checked)
            {
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
        //public async Task AutomaticFocusThenCapture()
        //{
        //    Invoke(new Action(() =>
        //    {
        //        btn_stopAutomaticFocusCapture.Visible = true;
        //        btn_stopAutomaticFocusCapture.Enabled = true;
        //    }));

        //    _stopRequested = false;

        //    if (int.TryParse(textBox_nbrFocusSteps.Text, out int steps))
        //    {
        //        int stepSize = (int)hScrollBar_driveStep.Value;


        //        if (checkBox_StackAuto.Checked)
        //        {
        //            try
        //            {
        //                string[] imageFiles = Directory.GetFiles(imagesFolderPath, "*.*")
        //                       .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        //                                      file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        //                                      file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        //                       .ToArray();

        //                foreach (string imageFile in imageFiles)
        //                {
        //                    try
        //                    {
        //                        File.Delete(imageFile);
        //                    }
        //                    catch (Exception ex)
        //                    {

        //                    }
        //                }
        //            }
        //            catch (Exception e)
        //            {
        //                AppendTextToConsoleNL(e.Message);
        //            }

        //        }

        //        for (int i = 0; i < steps; i++)
        //        {
        //            if (_stopRequested)
        //            {
        //                Invoke(new Action(() =>
        //                {
        //                    MessageBox.Show("Capture automatique interrompue.");
        //                    btn_stopAutomaticFocusCapture.Visible = false;
        //                    btn_stopAutomaticFocusCapture.Enabled = false;
        //                }));
        //                return;
        //            }

        //            await takePictureAsync();
        //            await Task.Delay(delayTime);
        //            ManualFocus(0, stepSize);
        //            await Task.Delay(delayTime);
        //        }

        //        if (!_stopRequested)
        //        {
        //            Invoke(new Action(() =>
        //            {
        //                tabControl4.SelectedTab = tabPage17;
        //            }));
        //            await MakeFocusStackSerie();
        //        }

        //        Invoke(new Action(async () =>
        //        {
        //            btn_stopAutomaticFocusCapture.Visible = false;
        //            btn_stopAutomaticFocusCapture.Enabled = false;
        //            if (int.TryParse(textBox_nbrFocusSteps.Text, out int stepBack))
        //            {
        //                stepBack = stepBack / 2;
        //                for (int i = 0; i <= stepBack; i++)
        //                {
        //                    ManualFocus(1, stepSize);
        //                    if (lbl_focusStepsVar.InvokeRequired)
        //                    {
        //                        lbl_focusStepsVar.Invoke(new Action(() =>
        //                        {
        //                            lbl_focusStepsVar.Text = i.ToString();
        //                        }));
        //                    }
        //                    else
        //                    {
        //                        lbl_focusStepsVar.Text = i.ToString();
        //                    }
        //                    await Task.Delay(100);
        //                }
        //            }
        //            if (checkBox_ApplyMaskStackedImage.Checked)
        //            {
        //                PostFocusStackMask();
        //            }

        //        }));



        //    }
        //}

        //public async Task AutomaticFocusThenCapture()
        //{
        //    Invoke(new Action(() =>
        //    {
        //        btn_stopAutomaticFocusCapture.Visible = true;
        //        btn_stopAutomaticFocusCapture.Enabled = true;
        //    }));

        //    _stopRequested = false;

        //    if (int.TryParse(textBox_nbrFocusSteps.Text, out int steps))
        //    {
        //        int stepSize = (int)hScrollBar_driveStep.Value;
        //        int delayTime = 200;

        //        if (checkBox_StackAuto.Checked)
        //        {
        //            try
        //            {
        //                string[] imageFiles = Directory.GetFiles(imagesFolderPath, "*.*")
        //                       .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        //                                      file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        //                                      file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        //                       .ToArray();

        //                foreach (string imageFile in imageFiles)
        //                {
        //                    try
        //                    {
        //                        File.Delete(imageFile);
        //                    }
        //                    catch (Exception ex)
        //                    {

        //                    }
        //                }
        //            }
        //            catch (Exception e)
        //            {
        //                AppendTextToConsoleNL(e.Message);
        //            }

        //        }

        //        for (int i = 0; i < steps; i++)
        //        {
        //            if (_stopRequested)
        //            {
        //                Invoke(new Action(() =>
        //                {
        //                    MessageBox.Show("Capture automatique interrompue.");
        //                    btn_stopAutomaticFocusCapture.Visible = false;
        //                    btn_stopAutomaticFocusCapture.Enabled = false;
        //                }));
        //                return;
        //            }

        //            await takePictureAsync();
        //            await Task.Delay(delayTime);
        //            ManualFocus(0, stepSize);
        //            await Task.Delay(delayTime);
        //        }

        //        if (!_stopRequested)
        //        {
        //            Invoke(new Action(() =>
        //            {
        //                tabControl4.SelectedTab = tabPage17;
        //            }));
        //            await MakeFocusStackSerie();
        //        }

        //        Invoke(new Action(async () =>
        //        {
        //            btn_stopAutomaticFocusCapture.Visible = false;
        //            btn_stopAutomaticFocusCapture.Enabled = false;
        //            if (int.TryParse(textBox_nbrFocusSteps.Text, out int stepBack))
        //            {
        //                stepBack = stepBack / 2;
        //                for (int i = 0; i <= stepBack; i++)
        //                {
        //                    ManualFocus(1, stepSize);
        //                    if (lbl_focusStepsVar.InvokeRequired)
        //                    {
        //                        lbl_focusStepsVar.Invoke(new Action(() =>
        //                        {
        //                            lbl_focusStepsVar.Text = i.ToString();
        //                        }));
        //                    }
        //                    else
        //                    {
        //                        lbl_focusStepsVar.Text = i.ToString();
        //                    }
        //                    await Task.Delay(100);
        //                }
        //            }
        //            if (checkBox_ApplyMaskStackedImage.Checked)
        //            {
        //                PostFocusStackMask();
        //            }

        //        }));



        //    }
        //}
    }

}