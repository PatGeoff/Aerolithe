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
using System.Windows.Forms;
using System.Drawing.Text;

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
        private int[] angleIndexes = [ 5, 25, 45 ];

        private Stopwatch _stopwatch = new Stopwatch();
        private CancellationTokenSource _cts;


        private async Task PrisePhotoSequenceAsync(CancellationToken cancellationToken, int serie, int rotationDepart)
        {
            maskFreeze = false;
            if (btn_freezeMask.InvokeRequired)
            {
                btn_freezeMask.Invoke(new Action(() =>
                {
                    btn_freezeMask.Invoke(() => btn_freezeMask.Text = maskFreeze ? "" : "");
                }));
            }

            if (appSettings.ProjectPath == null)
            {
                SavePrefsSettings();  // Demande à setter le projet
                if (appSettings.ProjectPath == null)
                {
                    MessageBox.Show("Pour faire la prise de photo en séquence, il faut définir un projet");
                    return;
                }
            }
            _stopwatch.Start();
            _ = UpdateTimerAsync(cancellationToken); // Timer en parallèle
            
            await RoutineCalibration();
            await UdpSendTurnTableMessageAsync($"turntable,150,{turntableSpeed}");
            await Task.Delay(800);


            int[] paddingNbr = { int.Parse(txtBox_seqPad1.Text), int.Parse(txtBox_seqPad2.Text), int.Parse(txtBox_seqPad3.Text) };
            serieId = [appSettings.NbrImg5Deg, appSettings.NbrImg25Deg, appSettings.NbrImg45Deg];

            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForTurntablePositionAsync(0);
            await Task.Delay(800);
            int divider = 0;
            try
            {
                divider = 4096 / serieId[serie];
            }
            catch (Exception e)
            {
                AppendTextToConsoleNL(e.Message);
                AppendTextToConsoleNL("S'assurer qu'il y a bien un nombre d'image valide aux séquences 1, 2 et 3 dans l'onglet Caméra/Automation");
            }

            await ResetIncrementation();

            AppendTextToConsoleNL("Série " + (serie + 1).ToString() + "/" + serieId[serie].ToString());
            try
            {
                for (int i = rotationDepart; i <= serieId[serie]; i++)
                {
                    if (_stopRequested) return;

                    maskFreeze = false;
                    if (btn_freezeMask.InvokeRequired)
                    {
                        btn_freezeMask.Invoke(new Action(() =>
                        {
                            btn_freezeMask.Invoke(() => btn_freezeMask.Text = maskFreeze ? "" : "");
                        }));
                    }


                    oldImgIncr = projet.FocusSerieIncrement = 0;
                    _Serie = i;
                    projet.Serie = _Serie;
                    projet.Save(appSettings.ProjectPath);
                    
                    if (_stopRequested) return;

                    PreparationDossierDestTemp();
                    int degres = i * divider;
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

                    if (lbl_CoteSerie.InvokeRequired)
                    {
                        lbl_CoteSerie.Invoke(new Action(() =>
                        {
                            string cote = (projet.Cote == 0) ? "A" : "B";
                            lbl_CoteSerie.Text = cote;
                            lbl_ElevSerie.Text = $"{angleStr[serie]}°";
                            lbl_RotSerie.Text = $"{i}/{serieId[serie]}";
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
            _stopwatch.Stop();
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

        private async Task SequencePrisePhotoIndividuelleAsync(CancellationToken ct, int serie, int angle, int rotation)
        {
            AppendTextToConsoleNL($"actuator {angle}");
            await UdpSendActuatorMessageAsync($"actuator {angle}");
            if (_stopRequested) return;

            await WaitForActuator(angle);
            if (_stopRequested) return;

            await Task.Delay(1000);

            //await RoutineCalibration();
            //if (_stopRequested) return;

            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());

            tokenSource = new CancellationTokenSource();
            await PrisePhotoSequenceAsync(tokenSource.Token, serie, rotation);
            if (_stopRequested) return;
            

            AppendTextToConsoleNL($"Séquence { + 1} terminée");


        }

        private async Task SequencePrisePhotoTotale(CancellationToken cancellationToken, int serieIndex, int rotation)
        {
            _stopwatch.Start();
            _ = UpdateTimerAsync(cancellationToken); // Timer en parallèle


            for (int i = serieIndex; i < angleIndexes.Length; i++)
            {
                maskFreeze = false;
                if (btn_freezeMask.InvokeRequired)
                {
                    btn_freezeMask.Invoke(new Action(() =>
                    {
                        btn_freezeMask.Invoke(() => btn_freezeMask.Text = maskFreeze ? "" : "");
                    }));
                }

                if (_stopRequested) return;
                AppendTextToConsoleNL($"i = {i} et angleIndexes.Length = {angleIndexes.Length}");
                try
                {
                    await SequencePrisePhotoIndividuelleAsync(cancellationToken, i,  angleIndexes[i], rotation);
                    projet.Serie = i;
                    projet.Save(appSettings.ProjectPath);
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL($"Erreur à * SequencePrisePhotoTotale:  {ex.Message}");
                }
            }
        }

        //private async Task SequencePrisePhotoTotale(CancellationToken cancellationToken)
        //{
        //    _stopwatch.Start();
        //    _ = UpdateTimerAsync(cancellationToken); // Timer en parallèle

        //    await UdpSendActuatorMessageAsync("actuator 5");
        //    if (_stopRequested) return;
        //    await WaitForActuator(5);
        //    if (_stopRequested) return;
        //    await Task.Delay(1000);
        //    await RoutineCalibration();
        //    if (_stopRequested) return;
        //    cancellationToken.ThrowIfCancellationRequested();
        //    AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
        //    currentSequence = 0;
        //    tokenSource = new CancellationTokenSource();
        //    if (_stopRequested) return;
        //    await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
        //    if (_stopRequested) return;
        //    AppendTextToConsoleNL("Séquence 1 terminée");
        //    await Task.Delay(1000);
        //    if (_stopRequested) return;

        //    await UdpSendActuatorMessageAsync("actuator 25");
        //    if (_stopRequested) return;
        //    await WaitForActuator(25);
        //    if (_stopRequested) return;
        //    await Task.Delay(1000);
        //    await RoutineCalibration();
        //    cancellationToken.ThrowIfCancellationRequested();
        //    //await WaitForActuatorPosition(25, cancellationToken);
        //    AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
        //    currentSequence = 1;
        //    tokenSource = new CancellationTokenSource();
        //    await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
        //    if (_stopRequested) return;
        //    AppendTextToConsoleNL("Séquence 2 terminée");

        //    await Task.Delay(1000);
        //    await UdpSendActuatorMessageAsync("actuator 45");
        //    if (_stopRequested) return;
        //    await WaitForActuator(45);
        //    if (_stopRequested) return;
        //    await Task.Delay(1000);
        //    await RoutineCalibration(); 
        //    cancellationToken.ThrowIfCancellationRequested();
        //    AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
        //    currentSequence = 2;
        //    tokenSource = new CancellationTokenSource();
        //    await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
        //    if (_stopRequested) return;
        //    AppendTextToConsoleNL("Séquence 3 terminée");
        //    _cts?.Cancel();
        //    _stopwatch.Stop();
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





        private async Task UpdateTimerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_stopwatch.IsRunning)
                {
                    TimeSpan elapsed = _stopwatch.Elapsed;
                    string formatted = $"{elapsed.Hours:D2}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";

                    // Invoke pour mise à jour UI
                    if (!token.IsCancellationRequested)
                    {
                        this.Invoke((Action)(() => lbl_timer.Text = formatted));
                    }
                }

                await Task.Delay(1000, token); // rafraîchit chaque seconde
            }
        }

        private async Task StartTimer()
        {
            _cts?.Cancel(); // stoppe l'ancien timer s'il existe
            _cts = new CancellationTokenSource();
            _stopwatch.Start();
            await Task.Run(() => UpdateTimerAsync(_cts.Token));
        }

        private async Task PauseTimer()
        {
            if (_stopwatch.IsRunning)
                _stopwatch.Stop();
            else
                _stopwatch.Start();

        }


        private async Task StopTimer()
        {
            _cts?.Cancel();
            _stopwatch.Reset();

            // Mise à jour du label via Invoke pour thread-safe
            if (lbl_timer.InvokeRequired)
            {
                lbl_timer.Invoke((Action)(() => lbl_timer.Text = ""));
            }
            else
            {
                lbl_timer.Text = "";
            }
        }

    }
}
