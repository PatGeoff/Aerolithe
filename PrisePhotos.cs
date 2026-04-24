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
using static Emgu.CV.DISOpticalFlow;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private int actuatorDelay1 = 5000; // secondes
        private int actuatorDelay2 = 9000; // secondes
        public int delayTimePhotoShoot = 1000;
        private bool working = false;
        private int _Elevation = 5;
        private int _Serie = 0;
        private int totalPhotos = 0;
        private string[] angleStr = { "5", "25", "45" };
        private int[] angleIndexes = [5, 25, 45];

        private Stopwatch _stopwatch = new Stopwatch();
        private CancellationTokenSource _cts;
        private bool photoPourMesure = false;
        private TaskCompletionSource<bool>? _pendingMiniatureTcs;
      
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
                    await NikonAutofocus();
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

            //Stopwatch sw = Stopwatch.StartNew();
            try
            {
                ManualFocus(1, 1); // Sert seulement pour rendre takePictureAsync plus rapide. Débloque la Nikon et la photo se télécharge en 1.5s au de 15 secondes ??
                await Task.Delay(200);
                await takePictureAsync();
                await Task.Delay(200);
                ManualFocus(1, 1); // Sert seulement pour rendre takePictureAsync plus rapide. Débloque la Nikon et la photo se télécharge en 1.5s au de 15 secondes ??
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur dans EssayerPrendrePhotoAsync :: takePictureAsync avec {ex.Message}");
            }
            
            
            //sw.Stop();
            //string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");

            //AppendTextToConsoleNL("La Capture de l'image a pris à la Nikon" + tempsMs + " secondes");

        }
 
        private async Task SequencePrisePhotoTotale(CancellationToken cancellationToken)
        {
            AppendTextToConsoleNL("SequencePrisePhotoTotale");
            AppendTextToConsoleNL($"projet.Serie = {projet.Serie}, projet.RotationSerieIncrement = {projet.RotationSerieIncrement}, projet.Cote = {projet.Cote}");

            _stopwatch.Start();
            _ = UpdateTimerAsync(cancellationToken); // Timer en parallèle

            // projet.Serie = 0, 1 ou 2 ---> (5,25,45)
            for (int i = projet.Serie; i < angleIndexes.Length; i++)
            {
                // Au début de chaque loop on s'assure que le maskFreeze soit false
                maskFreeze = false;
                if (btn_freezeMask.InvokeRequired)
                {
                    btn_freezeMask.Invoke(new Action(() =>
                    {
                        btn_freezeMask.Invoke(() => btn_freezeMask.Text = maskFreeze ? "" : "");
                    }));
                }

                if (_stopRequested) return;

                AppendTextToConsoleNL($"i = {i + 1} et angleIndexes.Length = {angleIndexes.Length}");

                try
                {
                    projet.Serie = i;
                    SavePrefsSettings();
                    // i = (0-2), angle = (5, 25, 45), rotation = (0 à x) mais pas 0-360, plutôt 0 à 4096/nombre de photos
                    await SequencePrisePhotoIndividuelleActuateurAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL($"Erreur à * SequencePrisePhotoTotale:  {ex.Message}");
                }
            }
        }

        private async Task SequencePrisePhotoIndividuelleActuateurAsync(CancellationToken ct)
        {
            // serie = projet.Serie = 0, 1 ou 2
            int angle = angleIndexes[projet.Serie];
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
            await PrisePhotoSequenceAsync(tokenSource.Token);
            if (_stopRequested) return;

            AppendTextToConsoleNL($"Séquence {+1} terminée");
        }

        private async Task PrisePhotoSequenceAsync(CancellationToken cancellationToken)
        {
            if (_stopRequested)
            {

                return;
            }
            maskFreeze = false;
            if (btn_freezeMask.InvokeRequired)
            {
                btn_freezeMask.Invoke(new Action(() =>
                {
                    btn_freezeMask.Invoke(() => btn_freezeMask.Text = "");
                }));
            }

            //if (appSettings.ProjectPath == null)
            //{
            //    SavePrefsSettings();  // Demande à setter le projet

            //}
            _stopwatch.Start();
            _ = UpdateTimerAsync(cancellationToken); // Timer en parallèle


            await UdpSendTurnTableMessageAsync($"turntable,150,{turntableSpeed}");
            await Task.Delay(800);

            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForTurntablePositionAsync(0);
            await Task.Delay(800);

            int[] paddingNbr = { appSettings.Padding5Deg, appSettings.Padding25Deg, appSettings.Padding45Deg };
            serieId = [appSettings.NbrImg5Deg, appSettings.NbrImg25Deg, appSettings.NbrImg45Deg];


            int divider = 0;
            try
            {
                divider = 4096 / serieId[projet.Serie];

            }
            catch (Exception e)
            {
                AppendTextToConsoleNL(e.Message);
                //AppendTextToConsoleNL("S'assurer qu'il y a bien un nombre d'image valide aux séquences 1, 2 et 3 dans l'onglet Caméra/Automation");
            }

            await ResetFocusIncrementationAndName();


            AppendTextToConsoleNL("Série " + (projet.Serie + 1).ToString() + "/" + serieId[projet.Serie].ToString());

            // serie = 0,1,2
            // serieId[serie] = 20,14,14 (exemple)
            // rotationSerieIncrementDepart = entre 0 et 20 (par exemple) 

            try
            {
                // de 0 à 13 si on a 14 images dans serieId[2] (par exemple) 
                for (int i = projet.RotationSerieIncrement; i <= serieId[projet.Serie] - 1; i++)
                {

                    // On a commencé avec i = projet.RotationSerieIncrement mais à chaque tour i +=1 donc on devra sauvegarder i dans projet.RotationSerieIncrement
                    projet.RotationSerieIncrement = i;
                    SavePrefsSettings();


                    PreparationDossierDestTemp();

                    if (_stopRequested) return;

                    maskFreeze = false;
                    if (btn_freezeMask.InvokeRequired)
                    {
                        btn_freezeMask.Invoke(new Action(() =>
                        {
                            btn_freezeMask.Invoke(() => btn_freezeMask.Text = maskFreeze ? "" : "");
                        }));
                    }


                    int degresActuelTableTournante = i * divider;
                    ttTargetPosition = degresActuelTableTournante;

                    await UdpSendTurnTableMessageAsync($"turntable,{degresActuelTableTournante},{turntableSpeed}");

                    if (_stopRequested) return;

                    await WaitForTurntablePositionAsync(degresActuelTableTournante);
                   

                    try
                    {
                        await nikonDoFocus();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: NikonDoFocus: {ex.Message}");
                    }


                    calculerCentre = true;
                    await Task.Delay(200); // délai avant la routine ?? 

                    try
                    {
                        await RoutineAutoCentrage();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: RoutineAutoCentrage: {ex.Message}");
                    }

                    // Autocentrage terminé.
                    // Table tournante en postion
                    // Actuateur en position

                    if (_stopRequested) return;
                    AppendTextToConsoleNL($"photo {(i + 1)}/{serieId[projet.Serie]} à {degresActuelTableTournante}°");



                    if (lbl_CoteSerie.InvokeRequired)
                    {
                        lbl_CoteSerie.Invoke(new Action(() =>
                        {
                            string cote = (projet.Cote == 0) ? "A" : "B";
                            lbl_CoteSerie.Text = cote;
                            lbl_ElevSerie.Text = $"{actuatorAngle}°";
                            lbl_RotSerie.Text = $"{i + 1}/{serieId[projet.Serie]}";
                        }));
                    }

                    // Prise de la photo pour la mesure du volume au besoin

                    if (projet.SaveImageForMesurements)
                    {
                        try
                        {
                            miniaturesTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            AppendTextToConsoleNL($"[Thread PrisePhotoSequenceAsync :: SaveMesurementImage] Invoke Required Thread# {Thread.CurrentThread.ManagedThreadId} -> is Thread same as UI? {(!this.InvokeRequired).ToString()}");

                            await SaveMesurementImage();                            
                            await miniaturesTcs.Task;
                            AppendTextToConsoleNL("miniaturesTcs.Task = True, on passe à la série de photo");
                        }
                        catch (Exception ex)
                        {

                            AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: SaveMesurementImage:  {ex.Message}");
                        }

                    }

                    // En haut d'ici, projet.FocusSerieIncrement devrait toujours être zéro. 
                    // À partir d'ici on va incrémenter projet.FocusSerieIncrement avec AutomaticFocusThenCapture

                    AppendTextToConsoleNL($"projet.FocusStackEnabled?: {projet.FocusStackEnabled}");
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
                            //MessageBox.Show(e.Message);
                            AppendTextToConsoleNL("Erreur: " + e.Message);

                        }
                    }
                    else
                    {
                       
                           AppendTextToConsoleNL("Prise de photo sans focus stack (EssayerPrendrePhotoAsync()");
                           this.BeginInvoke(new Action(async () =>
                           {
                               if (_pendingMiniatureTcs != null)
                                   throw new InvalidOperationException("Une capture est déjà en cours.");

                               _pendingMiniatureTcs =
                                   new TaskCompletionSource<bool>(
                                       TaskCreationOptions.RunContinuationsAsynchronously);
                               

                               AppendTextToConsoleNL($"[Thread PrisePhotoSequenceAsync :: creation de _pendingMiniatureTcs sur le thread # {Thread.CurrentThread.ManagedThreadId}]  Thread du UI? {(!this.InvokeRequired).ToString()}");

                               await takePictureAsync();                             


                               await _pendingMiniatureTcs.Task;
                               AppendTextToConsoleNL($"[Thread PrisePhotoSequenceAsync :: await _pendingMiniatureTcs.Task] a été setté à True par AfficherMiniatures sur le thread # {Thread.CurrentThread.ManagedThreadId}]  Thread du UI? {(!this.InvokeRequired).ToString()}");


                               AppendTextToConsoleNL("Ici");
                               projet.FocusSerieIncrement += 1;
                               SavePrefsSettings();
                               await Task.Delay(300);
                              
                           }));




                        //if (btn_takePicture.InvokeRequired)
                        //{
                        //    btn_takePicture.Invoke(new Action(() =>
                        //    {
                        //        btn_takePicture.PerformClick();
                        //    }));
                        //}
                        //else
                        //{
                        //    btn_takePicture.PerformClick();
                        //}

                        //try
                        //{
                        //    await EssayerPrendrePhotoAsync(degresActuelTableTournante);

                        //}
                        //catch (Exception ex)
                        //{
                        //    AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: EssayerPrendrePhotoAsync: {ex.Message}");
                        //}

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

        private async Task<bool> WaitForTurntablePositionAsync(int targetPos, int tolerance = 80, int timeoutMs = 10000, int checkInterval = 100)
        {
            AppendTextToConsoleNL("WaitForTurntablePositionAsync");

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

                await Task.Delay(500, token); // rafraîchit chaque seconde
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
