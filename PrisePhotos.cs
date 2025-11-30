using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using Emgu.CV.XImgproc;
using System.Threading;
using System.CodeDom;
using System.Diagnostics;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private int nombreImages5Degres = 20;
        private int nombreImages25Degres = 14;
        private int nombreImages45Degres = 14;
        private int actuatorDelay1 = 5000; // secondes
        private int actuatorDelay2 = 9000; // secondes
        public int delayTimePhotoShoot = 1000;
        private bool working = false;
        public CancellationTokenSource cancellationTokenSource;
        private int _Elevation = 5;
        private int _Rotation = 0;
        private int _Serie = 0;
        private int totalPhotos = 0;
        private bool calibrationInitialeLift = false;
        private string[] angleStr = { "5", "25", "45" };

        private async Task PrisePhotoSequenceAsync(CancellationToken cancellationToken, int serie)
        {
            if (appSettings.ProjectPath == null)
            {
                SavePrefsSettings();  // Demande à setter le projet
                if (appSettings.ProjectPath == null)
                {
                    MessageBox.Show("Pour faire la prise de photo en séquence, il faut définir un projet");
                    return;
                }
            }
             
            await UdpSendTurnTableMessageAsync($"turntable,150,{turntableSpeed}");
            await Task.Delay(800);

            
            int[] paddingNbr = { int.Parse(txtBox_seqPad1.Text), int.Parse(txtBox_seqPad2.Text), int.Parse(txtBox_seqPad3.Text) };
            serieId = [appSettings.NbrImg5Deg, appSettings.NbrImg25Deg, appSettings.NbrImg45Deg ];

            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForTurntablePositionAsync(0);
            await Task.Delay(800);
            int divider = 4096 / serieId[serie];

            await ResetIncrementation();

            AppendTextToConsoleNL("Série " + (serie).ToString() + "/" + serieId[serie].ToString());
            try
            {
                for (int i = 1; i <= serieId[serie]; i++)
                {
                    if (_stopRequested) return;
                   
                    oldImgIncr = projet.FocusSerieIncrement = 0;
                    projet.Save(appSettings.ProjectPath);
                    _Serie = i;
                    if (_stopRequested) return;

                    PreparationDossierDestTemp();
                    int degres = (i - 1) * divider;
                    ttTargetPosition = degres;
                    _Rotation = degres;
                    await UdpSendTurnTableMessageAsync($"turntable,{degres},{turntableSpeed}");
                    if (_stopRequested) return;

                    bool positionOk = await WaitForTurntablePositionAsync(degres);

                    if (!positionOk)
                    {
                        // Gérer le cas où la position n'est pas atteinte
                        AppendTextToConsoleNL(" La table n'a pas atteint la position, on continue ou on stop ?");
                    }
                    calculerCentre = true;
                    await Task.Delay(1000); // délai avant la routine
                    await RoutineAutoCentrage();

                    if (_stopRequested) return;
                    AppendTextToConsoleNL($"photo {i}/{serieId[serie]} à {degres}°");

                    if (lbl_ProgressDisplay.InvokeRequired)
                    {
                        lbl_ProgressDisplay.Invoke(new Action(() => {
                            string cote = (projet.Cote == 0) ? "A" : "B"; 
                            lbl_ProgressDisplay.Text = $"{cote}({angleStr[serie]}°)  {i}/{serieId[serie]}";
                        }));
                    }

                    //projet.FocusSerieIncrement = i - 1 + paddingNbr[serie];
                    projet.FocusSerieIncrement = 0;
                    PreparationNomImage();
                    if (checkBox_SeqFocusStack.Checked)
                    {
                        try
                        {
                            await AutomaticFocusRoutine();
                            if (_stopRequested) return;

                            await AutomaticFocusThenCapture(delta);
                            if (checkBox_SeqFocusStack.Checked || checkBox_StackAuto.Checked)
                            {
                                AppendTextToConsoleNL("Focus Stack lancé");

                                _ = Task.Run(() => MakeFocusStackSerie());
                               
                            }
                            if (flowLayoutPanel1.InvokeRequired)
                            {
                                flowLayoutPanel1.Invoke(new Action(() => { flowLayoutPanel1.Controls.Clear(); }));
                            }
                        }
                        catch (Exception e)
                        {
                           AppendTextToConsoleNL(e.Message);
                            //throw;
                            return;
                        }

                    }
                    else
                    {
                        await EssayerPrendrePhotoAsync(degres);
                    }
                    AppendTextToConsoleNL("Séquence #" + i.ToString() + " terminée");
                    await IncrementImgSeq();
                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($" Erreur dans la séquence : {ex.Message}");
            }
 
        }

        private async Task AppliquerMasqueEtFocusStepAsync()
        {
            btn_stopAutomaticFocusCapture.Visible = false;
            btn_stopAutomaticFocusCapture.Enabled = false;

            if (int.TryParse(textBox_nbrFocusSteps.Text, out int stepBack))
            {
                stepBack = stepBack / 2;
                for (int i = 0; i <= stepBack; i++)
                {
                    if (_stopRequested) return;
                    ManualFocus(1, stepSize);
                    lbl_focusStepsVar.Invoke(new Action(() =>
                    {
                        lbl_focusStepsVar.Text = i.ToString();
                    }));
                    await Task.Delay(100);
                }
            }

            if (checkBox_ApplyMaskStackedImage.Checked)
            {
                PostFocusStackMask();
            }
        }

        private async Task EssayerPrendrePhotoAsync(int degres)
        {
            if (!_stopRequested)
            {
                Stopwatch sw = Stopwatch.StartNew();
                int essai = 0;
                bool focusReussi = false;

                while (essai < 3 && !focusReussi)
                {
                    try
                    {
                        if (_stopRequested) return;
                        await NikonAutofocus();
                        await Task.Delay(1000); // laisse le temps au device.Capture de ne plus être "busy"
                        AppendTextToConsoleNL("Focus effectué avec succès");
                        focusReussi = true;
                    }
                    catch (Exception e)
                    {
                        essai++;
                        AppendTextToConsoleNL($"Essai {essai} échoué : {e.Message}");
                        //if (essai >= 3)
                        //{
                        //    await PhotoSuccess(projet.ImageNameFull, degres, false, "-");

                        //}
                    }
                }

                sw.Stop();
                string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");
                AppendTextToConsoleNL("Elapsed: " + tempsMs);
                sw.Start();
                await takePictureAsync();
                sw.Stop();
                tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");
                AppendTextToConsoleNL("Image sauvegardée");
                //await PhotoSuccess(projet.ImageNameFull, degres, true, tempsMs);
            }

        }

        private async Task SequencePrisePhotoTotale(CancellationToken cancellationToken)
        {
            await UdpSendActuatorMessageAsync("actuator 5");
            if (_stopRequested) return;
            await WaitForActuator(5);
            if (_stopRequested) return;
            await Task.Delay(1000);
            if (_stopRequested) return;
            cancellationToken.ThrowIfCancellationRequested();
            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
            currentSequence = 0;
            tokenSource = new CancellationTokenSource();
            if (_stopRequested) return;
            await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            if (_stopRequested) return;
            AppendTextToConsoleNL("Séquence 1 terminée");
            await Task.Delay(1000);
            if (_stopRequested) return;

            await UdpSendActuatorMessageAsync("actuator 25");
            if (_stopRequested) return;
            await WaitForActuator(25);
            if (_stopRequested) return;
            await Task.Delay(1000);

            cancellationToken.ThrowIfCancellationRequested();
            //await WaitForActuatorPosition(25, cancellationToken);
            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
            currentSequence = 1;
            tokenSource = new CancellationTokenSource();
            await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            if (_stopRequested) return;
            AppendTextToConsoleNL("Séquence 2 terminée");

            await Task.Delay(1000);
            await UdpSendActuatorMessageAsync("actuator 45");
            if (_stopRequested) return;
            await WaitForActuator(45);
            if (_stopRequested) return;
            await Task.Delay(1000);

            cancellationToken.ThrowIfCancellationRequested();
            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
            currentSequence = 2;
            tokenSource = new CancellationTokenSource();
            await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            if (_stopRequested) return;
            AppendTextToConsoleNL("Séquence 3 terminée");
        }


        //private async Task WaitForTurntablePositionAsync(int targetPosition, CancellationToken cancellationToken)
        //{
        //    AppendTextToConsoleNL("- WaitForTurntablePositionAsync");
        //    int tolerance = 10;
        //    int maxRetries = 100;
        //    int retryCount = 0;
        //    bool positionReached = false;

        //    while (retryCount < maxRetries)
        //    {
        //        if (_stopRequested) return;
        //        await getTurntablePosFromWaveshare();

        //        // Mise à jour des labels via Invoke
        //        if (lbl_ttCurrentPos.InvokeRequired)
        //        {
        //            lbl_ttCurrentPos.Invoke(new Action(() =>
        //            {
        //                lbl_ttCurrentPos.Text = "Table Tournante: " + turntablePosition.ToString() + " / " + ttTargetPosition.ToString();
        //            }));
        //        }
        //        else
        //        {
        //            lbl_ttCurrentPos.Text = "Table Tournante: " + turntablePosition.ToString() + " / " + ttTargetPosition.ToString();
        //        }


        //        if (turntablePosition >= targetPosition - tolerance && turntablePosition <= targetPosition + tolerance)
        //        {
        //            positionReached = true;
        //            break;
        //        }

        //        await Task.Delay(50, cancellationToken);
        //        retryCount++;
        //    }

        //    string message = positionReached
        //        ? "La table tournante a atteint sa position."
        //        : "La table tournante n'a pas atteint sa position après 100 essais.";

        //    AppendTextToConsoleNL(message);
        //}

        private async Task<bool> WaitForTurntablePositionAsync(int targetPos, int tolerance = 80, int timeoutMs = 10000, int checkInterval = 100)
        {
            AppendTextToConsoleNL("- WaitForTurntablePositionAsync");

            var startTime = DateTime.UtcNow;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                if (_stopRequested) return false;

                // Vérifie la position actuelle
                if (Math.Abs(turntablePosition - targetPos) <= tolerance)
                {
                    AppendTextToConsoleNL($"Position atteinte : {turntablePosition}° (cible : {targetPos}°)");
                    return true;
                }

                await Task.Delay(checkInterval);
            }

            AppendTextToConsoleNL($"Timeout : position actuelle {turntablePosition}°, cible {targetPos}°");
            return false;
        }
    }
}
