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
        private int _Serie = 0;
        private int[] angleIndexes = new int[] { 5, 25, 45 };

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
                await ManualFocusAsync(1, 1); // Sert seulement pour rendre takePictureAsync plus rapide. Débloque la Nikon et la photo se télécharge en 1.5s au de 15 secondes ??
                await Task.Delay(200);
                await takePictureAsync();
                await Task.Delay(200);
                await ManualFocusAsync(1, 1); // Sert seulement pour rendre takePictureAsync plus rapide. Débloque la Nikon et la photo se télécharge en 1.5s au de 15 secondes ??
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur dans EssayerPrendrePhotoAsync :: takePictureAsync avec {ex.Message}");
                _stopRequested = true;
                throw;
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
            int startingSerie = projet.Serie;

            // projet.Serie = 0, 1 ou 2 ---> (5,25,45)
            for (int i = projet.Serie; i < angleIndexes.Length; i++)
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

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
                    if (i != startingSerie)
                    {
                        projet.RotationSerieIncrement = 0;
                        projet.FocusSerieIncrement = 0;
                    }
                    SavePrefsSettings();
                    // i = (0-2), angle = (5, 25, 45), rotation = (0 à x) mais pas 0-360, plutôt 0 à 4096/nombre de photos
                    await SequencePrisePhotoIndividuelleActuateurAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _stopRequested = true;
                    AppendTextToConsoleNL($"Erreur à * SequencePrisePhotoTotale:  {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                    return;
                }
            }
        }

        private void ShowSequenceErrorMessage(Exception ex)
        {
            int serieAffichee = projet.Serie + 1;
            int angle = projet.Serie >= 0 && projet.Serie < angleIndexes.Length ? angleIndexes[projet.Serie] : -1;
            int[] paddingNbr = { appSettings.Padding5Deg, appSettings.Padding25Deg, appSettings.Padding45Deg };
            int rotationAffichee = projet.RotationSerieIncrement;
            if (rotationAffichee <= 0 && projet.Serie >= 0 && projet.Serie < paddingNbr.Length)
            {
                rotationAffichee = paddingNbr[projet.Serie];
            }

            string message = $"Une erreur est survenue à la série {serieAffichee} ({angle}°), rotation {rotationAffichee}." +
                $"{Environment.NewLine}{Environment.NewLine}Message d'erreur: {ex.Message}";

            void showMessage() => MessageBox.Show(
                this,
                message,
                "Erreur pendant la séquence",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            if (InvokeRequired)
            {
                Invoke(new Action(showMessage));
            }
            else
            {
                showMessage();
            }
        }

        private void ShowMeasurementSequenceErrorMessage(Exception ex)
        {
            string angle = projet.ForcedMesurementActuatorAngle?.ToString() ?? "?";
            string index = projet.ForcedMesurementIndex.HasValue ? (projet.ForcedMesurementIndex.Value + 1).ToString() : "?";

            string message = $"Une erreur est survenue pendant la séquence d'images de mesure à {angle}°, image {index}." +
                $"{Environment.NewLine}{Environment.NewLine}Message d'erreur: {ex.Message}";

            void showMessage() => MessageBox.Show(
                this,
                message,
                "Erreur pendant les images de mesure",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            if (InvokeRequired)
            {
                Invoke(new Action(showMessage));
            }
            else
            {
                showMessage();
            }
        }

        private async Task SequencePrisePhotoIndividuelleActuateurAsync(CancellationToken ct)
        {
            // serie = projet.Serie = 0, 1 ou 2
            int angle = angleIndexes[projet.Serie];
            AppendTextToConsoleNL($"actuator {angle}");
            await UdpSendActuatorMessageAsync($"actuator {angle}");
            if (_stopRequested) return;

            await WaitIfSequencePausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            await WaitForActuator(angle, ct);
            if (_stopRequested) return;

            await WaitIfSequencePausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            await Task.Delay(1000, ct);

            //await RoutineCalibration();
            //if (_stopRequested) return;

            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());

            await WaitIfSequencePausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            await PrisePhotoSequenceAsync(ct);
            if (_stopRequested) return;

            AppendTextToConsoleNL($"Séquence {+1} terminée");
        }

        private async Task SequenceTotaleImageMesuresAsync(CancellationToken cancellationToken)
        {
            AppendTextToConsoleNL("SequenceTotaleImageMesuresAsync");
            _stopwatch.Start();
            _ = UpdateTimerAsync(cancellationToken);

            try
            {
                await PriseImagesMesurePourActuateurAsync(5, projet.Mesurements5deg, cancellationToken);
                if (_stopRequested) return;

                await PriseImagesMesurePourActuateurAsync(25, projet.Mesurements25deg, cancellationToken);
                if (_stopRequested) return;

                await PriseImagesMesurePourActuateurAsync(45, projet.Mesurements45deg, cancellationToken);
            }
            finally
            {
                projet.ForcedMesurementActuatorAngle = null;
                projet.ForcedMesurementIndex = null;
            }
        }

        private async Task PriseImagesMesurePourActuateurAsync(int actuatorTarget, int imageCount, CancellationToken cancellationToken)
        {
            if (imageCount <= 0)
            {
                AppendTextToConsoleNL($"Séquence mesure {actuatorTarget}° ignorée: nombre d'images invalide ({imageCount}).");
                return;
            }

            AppendTextToConsoleNL($"Images de mesure à {actuatorTarget}°: {imageCount} images");
            await UdpSendActuatorMessageAsync($"actuator {actuatorTarget}");
            if (_stopRequested) return;

            await WaitIfSequencePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await WaitForActuator(actuatorTarget, cancellationToken);
            if (_stopRequested) return;

            await WaitIfSequencePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(1000, cancellationToken);

            for (int i = 0; i < imageCount; i++)
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (_stopRequested) return;

                int turntableTarget = (int)Math.Round(i * (4096.0 / imageCount));
                ttTargetPosition = turntableTarget;
                projet.RotationSerieIncrement = i;
                projet.ForcedMesurementActuatorAngle = actuatorTarget;
                projet.ForcedMesurementIndex = i;

                AppendTextToConsoleNL($"Image de mesure {actuatorTarget}° {(i + 1)}/{imageCount} - table {turntableTarget}/4096");
                UpdateSequenceStatusLabels(actuatorTarget, i + 1, imageCount);

                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await UdpSendTurnTableMessageAsync($"turntable,{turntableTarget},{turntableSpeed}");
                if (_stopRequested) return;

                await WaitForTurntablePositionAsync(turntableTarget, cancellationToken: cancellationToken);
                if (_stopRequested) return;

                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (projet.AutoCentrage)
                {
                    try
                    {
                        await WaitIfSequencePausedAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        await nikonDoFocus();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PriseImagesMesurePourActuateurAsync :: NikonDoFocus: {ex.Message}");
                        _stopRequested = true;
                        throw;
                    }

                    calculerCentre = true;
                    await Task.Delay(200, cancellationToken);

                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await RoutineAutoCentrage();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PriseImagesMesurePourActuateurAsync :: RoutineAutoCentrage: {ex.Message}");
                        _stopRequested = true;
                        throw;
                    }

                    if (_stopRequested) return;
                }

                try
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    var measurementMiniaturesTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    miniaturesTcs = measurementMiniaturesTcs;
                    await SaveMesurementImage();
                    await measurementMiniaturesTcs.Task;
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL($"Erreur PriseImagesMesurePourActuateurAsync :: SaveMesurementImage: {ex.Message}");
                    _stopRequested = true;
                    throw;
                }
                finally
                {
                    projet.ForcedMesurementActuatorAngle = null;
                    projet.ForcedMesurementIndex = null;
                }
            }
        }

        private void UpdateSequenceStatusLabels(int angle, int currentRotation, int totalRotations)
        {
            Action updateAction = () =>
            {
                string cote = projet.Cote == 0 ? "A" : "B";
                lbl_CoteSerie.Text = cote;
                lbl_ElevSerie.Text = $"{angle}°";
                lbl_RotSerie.Text = $"{currentRotation}/{totalRotations}";
            };

            if (lbl_CoteSerie.InvokeRequired)
            {
                lbl_CoteSerie.Invoke(updateAction);
            }
            else
            {
                updateAction();
            }
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


            await WaitIfSequencePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await UdpSendTurnTableMessageAsync($"turntable,150,{turntableSpeed}");
            await Task.Delay(800, cancellationToken);

            await WaitIfSequencePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForTurntablePositionAsync(0, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await WaitIfSequencePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(800, cancellationToken);

            int[] paddingNbr = { appSettings.Padding5Deg, appSettings.Padding25Deg, appSettings.Padding45Deg };
            serieId = new int[] { appSettings.NbrImg5Deg, appSettings.NbrImg25Deg, appSettings.NbrImg45Deg };


            int divider = 0;
            try
            {
                divider = 4096 / serieId[projet.Serie];

            }
            catch (Exception e)
            {
                AppendTextToConsoleNL(e.Message);
                throw new InvalidOperationException("Nombre d'images invalide pour la série courante.", e);
                //AppendTextToConsoleNL("S'assurer qu'il y a bien un nombre d'image valide aux séquences 1, 2 et 3 dans l'onglet Caméra/Automation");
            }

            await ResetFocusIncrementationAndName();


            AppendTextToConsoleNL("Série " + (projet.Serie + 1).ToString() + "/" + serieId[projet.Serie].ToString());

            // serie = 0,1,2
            // serieId[serie] = 20,14,14 (exemple)
            // rotationSerieIncrementDepart = entre 0 et 20 (par exemple) 

            try
            {
                int paddingDepart = paddingNbr[projet.Serie];
                int localStartIndex = projet.RotationSerieIncrement > paddingDepart
                    ? projet.RotationSerieIncrement - paddingDepart
                    : 0;

                // i est l'index physique local de rotation. RotationSerieIncrement reste le numéro global pour nommer/sauver.
                for (int i = localStartIndex; i <= serieId[projet.Serie] - 1; i++)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    projet.RotationSerieIncrement = paddingDepart + i;
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

                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    await UdpSendTurnTableMessageAsync($"turntable,{degresActuelTableTournante},{turntableSpeed}");

                    if (_stopRequested) return;

                    await WaitForTurntablePositionAsync(degresActuelTableTournante, cancellationToken: cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                   

                    try
                    {
                        await WaitIfSequencePausedAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        await nikonDoFocus();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: NikonDoFocus: {ex.Message}");
                        _stopRequested = true;
                        throw;
                    }


                    calculerCentre = true;
                    await Task.Delay(200, cancellationToken); // délai avant la routine ?? 

                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await RoutineAutoCentrage();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: RoutineAutoCentrage: {ex.Message}");
                        _stopRequested = true;
                        throw;
                    }

                    // Autocentrage terminé.
                    // Table tournante en postion
                    // Actuateur en position

                    if (_stopRequested) return;
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendTextToConsoleNL($"photo {(i + 1)}/{serieId[projet.Serie]} à {degresActuelTableTournante}°");
                    UpdateSequenceStatusLabels(angleIndexes[projet.Serie], i + 1, serieId[projet.Serie]);

                    // Prise de la photo pour la mesure du volume au besoin

                    if (projet.SaveImageForMesurements)
                    {
                        try
                        {
                            await WaitIfSequencePausedAsync(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

                            var measurementMiniaturesTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            miniaturesTcs = measurementMiniaturesTcs;
                            AppendTextToConsoleNL($"[Thread PrisePhotoSequenceAsync :: SaveMesurementImage] Invoke Required Thread# {Thread.CurrentThread.ManagedThreadId} -> is Thread same as UI? {(!this.InvokeRequired).ToString()}");

                            await SaveMesurementImage();                            
                            await measurementMiniaturesTcs.Task;
                            AppendTextToConsoleNL("miniaturesTcs.Task = True, on passe à la série de photo");
                        }
                        catch (Exception ex)
                        {

                            AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: SaveMesurementImage:  {ex.Message}");
                            throw;
                        }

                    }

                    // En haut d'ici, projet.FocusSerieIncrement devrait toujours être zéro. 
                    // À partir d'ici on va incrémenter projet.FocusSerieIncrement avec AutomaticFocusThenCapture

                    AppendTextToConsoleNL($"projet.FocusStackEnabled?: {projet.FocusStackEnabled}");
                    if (projet.FocusStackEnabled)
                    {
                        try
                        {
                            await WaitIfSequencePausedAsync(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

                            await AutomaticFocusRoutine();
                            if (_stopRequested) return;

                            await WaitIfSequencePausedAsync(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

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
                            throw;

                        }
                    }
                    else
                    {
                       
                           await WaitIfSequencePausedAsync(cancellationToken);
                           cancellationToken.ThrowIfCancellationRequested();

                           AppendTextToConsoleNL("Prise de photo sans focus stack");
                           await CaptureImageAndWaitForMiniatureAsync();
                           cancellationToken.ThrowIfCancellationRequested();
                           AppendTextToConsoleNL($"[Thread PrisePhotoSequenceAsync :: miniature reçue] thread # {Thread.CurrentThread.ManagedThreadId}]  Thread du UI? {(!this.InvokeRequired).ToString()}");
                           projet.FocusSerieIncrement += 1;
                           SavePrefsSettings();
                           await Task.Delay(300, cancellationToken);




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
                    if (i < serieId[projet.Serie] - 1)
                    {
                        try
                        {
                            await WaitIfSequencePausedAsync(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

                            await IncrementImgSeq();
                        }
                        catch (Exception ex)
                        {
                            AppendTextToConsoleNL($"Erreur PrisePhotoSequenceAsync :: IncrementImgSeq: {ex.Message}");
                            throw;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($" Erreur dans la séquence : {ex.Message}");
                throw;
            }
            if (serieId[projet.Serie] > 0)
            {
                UpdateSequenceStatusLabels(angleIndexes[projet.Serie], serieId[projet.Serie], serieId[projet.Serie]);
            }
            _stopwatch.Stop();
        }

        private async Task<bool> WaitForTurntablePositionAsync(
            int targetPos,
            int tolerance = 80,
            int timeoutMs = 10000,
            int checkInterval = 250,
            CancellationToken cancellationToken = default)
        {
            AppendTextToConsoleNL("WaitForTurntablePositionAsync");

            var startTime = DateTime.UtcNow;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (_stopRequested) return false;

                // Vérifie la position actuelle
                if (Math.Abs(turntablePosition - targetPos) <= tolerance)
                {
                    AppendTextToConsoleNL($"Position atteinte : {turntablePosition}/4096 (cible : {targetPos}/4096)");
                    return true;
                }

                int? reportedPosition = await RequestTurntablePositionAsync(TimeSpan.FromMilliseconds(500));
                if (reportedPosition.HasValue && Math.Abs(reportedPosition.Value - targetPos) <= tolerance)
                {
                    AppendTextToConsoleNL($"Position atteinte : {reportedPosition.Value}/4096 (cible : {targetPos}/4096)");
                    return true;
                }

                await Task.Delay(checkInterval, cancellationToken);
            }

            AppendTextToConsoleNL($"Timeout : position actuelle {turntablePosition}/4096, cible {targetPos}/4096");
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

        private Task PauseTimer()
        {
            if (_stopwatch.IsRunning)
                _stopwatch.Stop();
            else
                _stopwatch.Start();

            return Task.CompletedTask;
        }

        private Task StopTimer()
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

            return Task.CompletedTask;
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
