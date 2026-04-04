using Emgu.CV.XImgproc;
using Nikon;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

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
        private int _Serie = 0;
        private int totalPhotos = 0;
        private bool calibrationInitialeLift = false;
        private string[] angleStr = { "5", "25", "45" };
        private int[] angleIndexes = [5, 25, 45];

        private Stopwatch _stopwatch = new Stopwatch();
        private CancellationTokenSource _cts;
        private bool photoPourMesure = false;


        private async Task PrisePhotoSequenceAsync(CancellationToken cancellationToken, int serie, int rotationDepart)
        {
            maskFreeze = false;
            if (btn_freezeMask.InvokeRequired)
            {
                btn_freezeMask.Invoke(new Action(() =>
                {
                    btn_freezeMask.Invoke(() => btn_freezeMask.Text = "");
                }));
            }

            if (appSettings.ProjectPath == null)
            {
                SavePrefsSettings();  // Demande à setter le projet
                
            }
            _stopwatch.Start();
            _ = UpdateTimerAsync(cancellationToken); // Timer en parallèle

           
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

            // De 0 à 360 degrés sur la table tournante

            try
            {
                for (int i = rotationDepart; i <= serieId[serie] - 1; i++)
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
                    SavePrefsSettings();                   

                    PreparationDossierDestTemp();
                    int degres = i * divider;
                    ttTargetPosition = degres;

                    await UdpSendTurnTableMessageAsync($"turntable,{degres},{turntableSpeed}");
                    if (_stopRequested) return;

                    bool positionOk = await WaitForTurntablePositionAsync(degres);

                    try
                    {
                        await nikonDoFocus();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: NikonDoFocus: {ex.Message}");
                    }
                   

                    if (!positionOk)
                    {
                        // Gérer le cas où la position n'est pas atteinte
                        AppendTextToConsoleNL(" La table n'a pas atteint la position, on continue ou on stop ?");
                    }
                    calculerCentre = true;
                    await Task.Delay(1000); // délai avant la routine ?? 
                    try
                    {
                        await RoutineAutoCentrage();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: RoutineAutoCentrage: {ex.Message}");
                    }
                    

                    if (_stopRequested) return;
                    AppendTextToConsoleNL($"photo {(i + 1)}/{serieId[serie]} à {degres}°");

                    if (lbl_CoteSerie.InvokeRequired)
                    {
                        lbl_CoteSerie.Invoke(new Action(() =>
                        {
                            string cote = (projet.Cote == 0) ? "A" : "B";
                            lbl_CoteSerie.Text = cote;
                            lbl_ElevSerie.Text = $"{angleStr[serie]}°";
                            lbl_RotSerie.Text = $"{i + 1}/{serieId[serie]}";
                        }));
                    }

                    //projet.FocusSerieIncrement = i - 1 + paddingNbr[serie];
                    projet.FocusSerieIncrement = 0;
                    try
                    {
                        PreparationNomImage();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: PreparationNomImage: {ex.Message}");
                        
                    }
                   

                    if (projet.SaveImageForMesurements)
                    {
                        try
                        {
                            await SaveMesurementImage();
                        }
                        catch (Exception ex)
                        {

                            AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: SaveMesurementImage:  {ex.Message}");
                        }
                        
                    }


                    if (projet.FocusStackEnabled)
                    {
                        try
                        {
                            await AutomaticFocusRoutine();
                            if (_stopRequested) return;

                            await AutomaticFocusThenCapture(delta);

                            AppendTextToConsoleNL("Focus Stack lancé");

                            _ = Task.Run(() => MakeFocusStackSerie());


                            if (flowLayoutPanel1.InvokeRequired)
                            {
                                flowLayoutPanel1.Invoke(new Action(() => { flowLayoutPanel1.Controls.Clear(); }));
                            }
                        }
                        catch (Exception e)
                        {
                            AppendTextToConsoleNL(e.Message);                           
                            
                        }
                    }
                    else
                    {
                        try
                        {
                            await EssayerPrendrePhotoAsync(degres);

                        }
                        catch (Exception ex)
                        {
                            AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: EssayerPrendrePhotoAsync: {ex.Message}");
                        }

                    }
                    //AppendTextToConsoleNL("Séquence #" + (i+1).ToString() + " terminée");
                    try
                    {
                        await IncrementImgSeq();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: IncrementImgSeq: {ex.Message}");
                    }
                   
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
            if (_stopRequested) return;

            int essai = 0;
            bool focusReussi = false;

            while (essai < 3 && !focusReussi)
            {
                try
                {
                    if (_stopRequested) return;
                    //await NikonAutofocus();
                    //await Task.Delay(1000); // laisse le temps au device.Capture de ne plus être "busy"
                    //AppendTextToConsoleNL("Focus effectué avec succès");
                    focusReussi = true;
                }
                catch (Exception e)
                {
                    essai++;
                    AppendTextToConsoleNL($"Essai {essai} échoué : {e.Message}");
                }
            }
            
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await takePictureAsync();
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur dans EssayerPrendrePhotoAsync :: takePictureAsync avec {ex.Message}");
            }
            
            await Task.Delay(400);
            try
            {
                ManualFocus(1, 1); // Sert seulement pour rendre takePictureAsync plus rapide. Débloque la Nikon et la photo se télécharge en 1.5s au de 15 secondes ??
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur dans EssayerPrendrePhotoAsync :: ManualFocus (1, 1)  avec  {ex.Message}");
            }
           
            sw.Stop();
            string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");
            AppendTextToConsoleNL("La Capture de l'image a pris à la Nikon" + tempsMs + " secondes");

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

            AppendTextToConsoleNL($"Séquence {+1} terminée");
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
                    await SequencePrisePhotoIndividuelleAsync(cancellationToken, i, angleIndexes[i], rotation);
                    projet.Serie = i;
                    projet.Save(appSettings.ProjectPath);
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL($"Erreur à * SequencePrisePhotoTotale:  {ex.Message}");
                }
            }
        }



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

    public class tempData
    {
        public bool mask { get; set; } = true;
        public bool focusStack { get; set; } = true;
        public bool freeze { get; set; } = false;

        public tempData()
        {

        }
    }
}
