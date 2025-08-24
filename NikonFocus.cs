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

            // Analyse du meilleur delta de flou
            double maxDelta = 0;
            int bestIndex = 0;
            var sortedKeys = blurDataDict.Keys.OrderBy(k => k).ToList();

            for (int i = 1; i < sortedKeys.Count; i++)
            {
                int prev = sortedKeys[i - 1];
                int curr = sortedKeys[i];
                double delta = Math.Abs(blurDataDict[curr].blur - blurDataDict[prev].blur);

                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    bestIndex = curr;
                }
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
            //line.LineStyle = ScottPlot.LineStyle.Dash;


            // Texte au-dessus du point optimal
            formsPlot.Plot.Add.Text($"Optimal: {bestIndex}", bestIndex, blurDataDict[bestIndex].blur + 0.05);

            formsPlot.Refresh();


        }



    }



}