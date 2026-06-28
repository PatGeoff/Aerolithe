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
                RequestSequenceStop("EssayerPrendrePhotoAsync: " + ex.Message);
                throw;
            }
            
            
            //sw.Stop();
            //string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");

            //AppendTextToConsoleNL("La Capture de l'image a pris à la Nikon" + tempsMs + " secondes");

        }
 
        private async Task SequencePrisePhotoTotale(CancellationToken cancellationToken, bool promptAfterInitialFiveDegreeMove = false)
        {
            AppendTextToConsoleNL("SequencePrisePhotoTotale");
            AppendTextToConsoleNL($"projet.Serie = {projet.Serie}, projet.RotationSerieIncrement = {projet.RotationSerieIncrement}, projet.Cote = {projet.Cote}");

            _stopwatch.Start();
            _ = UpdateTimerAsync(cancellationToken); // Timer en parallèle
            int startingSerie = projet.Serie;

            try
            {
                await PromptAutoCentrageBeforeTotalSequenceAsync(cancellationToken);
                await BeginAutoExposureTotalSequenceAsync(cancellationToken);

                // projet.Serie = 0, 1 ou 2 ---> (5,25,45)
                for (int i = projet.Serie; i < angleIndexes.Length; i++)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    // Au début de chaque loop on s'assure que le maskFreeze soit false
                    SetMaskFreeze(false);

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
                        await SequencePrisePhotoIndividuelleActuateurAsync(
                            cancellationToken,
                            promptAfterInitialFiveDegreeMove && startingSerie == 0 && i == 0);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        RequestSequenceStop("SequencePrisePhotoTotale: " + ex.Message);
                        _lastSequenceErrorMessage = ex.Message;
                        AppendTextToConsoleNL($"Erreur à * SequencePrisePhotoTotale:  {ex.Message}");
                        ShowSequenceErrorMessage(ex);
                        return;
                    }
                }
            }
            finally
            {
                await RestoreAutoExposureOriginalShutterAsync(
                    cancellationToken.IsCancellationRequested || _stopRequested ? "annulation/arrêt" : "fin de séquence");
            }
        }

        private void ShowSequenceErrorMessage(Exception ex)
        {
            if (_manualSequenceCancellationRequested) return;

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

            AppendTextToConsoleNL(message, Color.Red);
        }

        private void ShowMeasurementSequenceErrorMessage(Exception ex)
        {
            if (_manualSequenceCancellationRequested) return;

            string angle = projet.ForcedMesurementActuatorAngle?.ToString() ?? "?";
            string index = projet.ForcedMesurementIndex.HasValue ? (projet.ForcedMesurementIndex.Value + 1).ToString() : "?";

            string message = $"Une erreur est survenue pendant la séquence d'images de mesure à {angle}°, image {index}." +
                $"{Environment.NewLine}{Environment.NewLine}Message d'erreur: {ex.Message}";

            AppendTextToConsoleNL(message, Color.Red);
        }

        private async Task SequencePrisePhotoIndividuelleActuateurAsync(CancellationToken ct, bool promptAfterActuatorMoveToFiveDegrees = false)
        {
            // serie = projet.Serie = 0, 1 ou 2
            int angle = angleIndexes[projet.Serie];
            int imageCount = GetPhotoCountForCurrentSerie();
            if (imageCount == 0)
            {
                RegisterSequencePhotoSeries(projet.Serie, angle, 0, ignored: true);
                AppendTextToConsoleNL($"Série {projet.Serie + 1} ({angle}°) ignorée: nombre de photos à 0. Actuateur et auto-centrage non exécutés.");
                UpdateSequenceStatusLabels(angle, 0, 0);
                return;
            }

            bool shouldPromptAfterActuatorMove = false;
            if (promptAfterActuatorMoveToFiveDegrees && angle == 5)
            {
                double angleBeforeMove = await RequestActuatorAngleAsync(TimeSpan.FromSeconds(2), ct) ?? actuatorAngle;
                shouldPromptAfterActuatorMove = !IsActuatorNearTarget(angleBeforeMove, 5);
                AppendTextToConsoleNL($"Angle actuateur avant routine totale: {angleBeforeMove:0.##}°");
            }

            AppendTextToConsoleNL($"actuator {angle}");
            await UdpSendActuatorMessageAsync($"actuator {angle}");
            if (_stopRequested) return;

            await WaitIfSequencePausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            bool actuatorReachedTarget = await WaitForActuator(angle, ct);
            if (_stopRequested) return;

            if (shouldPromptAfterActuatorMove && actuatorReachedTarget)
            {
                AppendTextToConsoleNL("Routine totale en pause après positionnement de l'actuateur à 5°.", Color.Orange);
                bool continueSequence = await ShowTotalSequenceReadyPromptAsync(ct);
                if (!continueSequence)
                {
                    StopSequences();
                    throw new OperationCanceledException(ct);
                }
            }

            await WaitIfSequencePausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            await Task.Delay(1000, ct);

            //await RoutineCalibration();
            //if (_stopRequested) return;

            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());

            await WaitIfSequencePausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            await CalibrateAutoExposureForCurrentSequenceAngleAsync(angle, ct);

            await PrisePhotoSequenceAsync(ct);
            if (_stopRequested) return;

            AppendTextToConsoleNL($"Séquence {+1} terminée");
        }

        private int GetPhotoCountForCurrentSerie()
        {
            int[] imageCounts = { appSettings.NbrImg5Deg, appSettings.NbrImg25Deg, appSettings.NbrImg45Deg };
            return projet.Serie >= 0 && projet.Serie < imageCounts.Length
                ? Math.Max(0, imageCounts[projet.Serie])
                : 0;
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

                bool turntableReached = await MoveTurntableIfNeededAsync(turntableTarget, cancellationToken);
                if (_stopRequested) return;
                if (!turntableReached)
                {
                    string message = $"Séquence mesure arrêtée: table tournante non confirmée à {turntableTarget}/4096 pour {actuatorTarget}° image {(i + 1)}/{imageCount}.";
                    AppendTextToConsoleNL(message, Color.Red);
                    RequestSequenceStop(message);
                    throw new TimeoutException(message);
                }

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
                        RequestSequenceStop("PriseImagesMesurePourActuateurAsync NikonDoFocus: " + ex.Message);
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
                    RequestSequenceStop("PriseImagesMesurePourActuateurAsync SaveMesurementImage: " + ex.Message);
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
            SetMaskFreeze(false);

            //if (appSettings.ProjectPath == null)
            //{
            //    SavePrefsSettings();  // Demande à setter le projet

            //}
            _stopwatch.Start();
            _ = UpdateTimerAsync(cancellationToken); // Timer en parallèle

            serieId = new int[] { appSettings.NbrImg5Deg, appSettings.NbrImg25Deg, appSettings.NbrImg45Deg };
            if (serieId[projet.Serie] == 0)
            {
                RegisterSequencePhotoSeries(projet.Serie, angleIndexes[projet.Serie], 0, ignored: true);
                AppendTextToConsoleNL($"Série {projet.Serie + 1} ignorée: nombre de photos à 0.");
                return;
            }

            RegisterSequencePhotoSeries(projet.Serie, angleIndexes[projet.Serie], serieId[projet.Serie], ignored: false);

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
                bool useSpecificResumeOverride =
                    _specificResumeLocalRotationOverride.HasValue &&
                    _specificResumeImageNumberOverride.HasValue &&
                    _specificResumeLocalRotationOverride.Value >= 0 &&
                    _specificResumeLocalRotationOverride.Value < serieId[projet.Serie];

                int localStartIndex = useSpecificResumeOverride
                    ? _specificResumeLocalRotationOverride!.Value
                    : projet.RotationSerieIncrement > paddingDepart
                        ? projet.RotationSerieIncrement - paddingDepart
                        : 0;

                int imageNumberStart = useSpecificResumeOverride
                    ? _specificResumeImageNumberOverride!.Value
                    : paddingDepart + localStartIndex;

                // i est l'index physique local de rotation. RotationSerieIncrement reste le numéro global pour nommer/sauver.
                for (int i = localStartIndex; i <= serieId[projet.Serie] - 1; i++)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    projet.RotationSerieIncrement = imageNumberStart + (i - localStartIndex);
                    SavePrefsSettings();


                    PreparationDossierDestTemp();

                    if (_stopRequested) return;

                    SetMaskFreeze(false);


                    int degresActuelTableTournante = i * divider;
                    ttTargetPosition = degresActuelTableTournante;

                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    bool turntableReached = await MoveTurntableIfNeededAsync(degresActuelTableTournante, cancellationToken);
                    if (_stopRequested || !turntableReached) return;
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
                        RequestSequenceStop("PrisePhotoSequenceAsync NikonDoFocus: " + ex.Message);
                        throw;
                    }


                    if (_stopRequested) return;
                    cancellationToken.ThrowIfCancellationRequested();
                    AppendTextToConsoleNL($"photo {(i + 1)}/{serieId[projet.Serie]} à {degresActuelTableTournante}°");
                    UpdateSequenceStatusLabels(angleIndexes[projet.Serie], i + 1, serieId[projet.Serie]);

                    // La sauvegarde automatique d'image de mesure par rotation a été retirée.
                    // Le bouton manuel btn_saveImageForMesurements reste disponible pour une photo ponctuelle.

                    // En haut d'ici, projet.FocusSerieIncrement devrait toujours être zéro. 
                    // À partir d'ici on va incrémenter projet.FocusSerieIncrement avec AutomaticFocusThenCapture

                    AppendTextToConsoleNL($"projet.FocusStackEnabled?: {projet.FocusStackEnabled}");
                    if (projet.FocusStackEnabled)
                    {
                        try
                        {
                            await WaitIfSequencePausedAsync(cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

                            bool focusStackCaptured = false;
                            bool skipRotationAlreadyHandled = false;
                            try
                            {
                                for (int focusAttempt = 1; focusAttempt <= 2; focusAttempt++)
                                {
                                    if (focusAttempt == 2)
                                    {
                                        ApplyTemporaryBlurThresholdForRetry();
                                    }

                                    AutomaticFocusResult focusResult = await AutomaticFocusRoutine(cancellationToken);
                                    if (_stopRequested) return;

                                    if (focusResult == AutomaticFocusResult.Cancelled)
                                    {
                                        return;
                                    }

                                    if (focusResult == AutomaticFocusResult.MaskUnavailable)
                                    {
                                        MarkSequencePhotoFailed(projet.Serie, angleIndexes[projet.Serie]);
                                        AppendTextToConsoleNL($"Rotation {i + 1}/{serieId[projet.Serie]} à {degresActuelTableTournante}° ignorée: masque stable indisponible.");

                                        if (i < serieId[projet.Serie] - 1)
                                        {
                                            await WaitIfSequencePausedAsync(cancellationToken);
                                            cancellationToken.ThrowIfCancellationRequested();
                                            await IncrementImgSeq();
                                        }

                                        focusStackCaptured = false;
                                        skipRotationAlreadyHandled = true;
                                        break;
                                    }

                                    await WaitIfSequencePausedAsync(cancellationToken);
                                    cancellationToken.ThrowIfCancellationRequested();

                                    focusStackCaptured = await AutomaticFocusThenCapture(delta, cancellationToken);
                                    if (focusStackCaptured)
                                    {
                                        break;
                                    }

                                    AppendTextToConsoleNL($"Capture focus stack échouée à la rotation {i + 1}/{serieId[projet.Serie]} après l'essai {focusAttempt}/2.", Color.Orange);
                                }
                            }
                            finally
                            {
                                ClearTemporaryBlurThresholdOverride();
                            }

                            if (skipRotationAlreadyHandled)
                            {
                                continue;
                            }

                            if (!focusStackCaptured)
                            {
                                MarkSequencePhotoFailed(projet.Serie, angleIndexes[projet.Serie]);
                                AppendTextToConsoleNL($"Rotation {i + 1}/{serieId[projet.Serie]} à {degresActuelTableTournante}° ignorée après 2 essais de focus stack.", Color.Red);

                                if (i < serieId[projet.Serie] - 1)
                                {
                                    await WaitIfSequencePausedAsync(cancellationToken);
                                    cancellationToken.ThrowIfCancellationRequested();
                                    await IncrementImgSeq();
                                }

                                continue;
                            }

                            AppendTextToConsoleNL("Focus Stack lancé");

                            _ = RunMakeFocusStackSerieAsync();


                            if (flowLayoutPanel1.InvokeRequired)
                            {
                                flowLayoutPanel1.Invoke(new Action(ClearThumbnailControls));
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
                    MarkSequencePhotoSucceeded(projet.Serie, angleIndexes[projet.Serie]);
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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                MarkSequencePhotoFailed(projet.Serie, angleIndexes[projet.Serie]);
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
            AppendNetworkConsoleMessage("WaitForTurntablePositionAsync");

            var startTime = DateTime.UtcNow;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                await WaitIfSequencePausedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (_stopRequested) return false;

                // Vérifie la position actuelle
                if (IsTurntableAtPosition(turntablePosition, targetPos, tolerance))
                {
                    AppendNetworkConsoleMessage($"Position atteinte : {turntablePosition}/4096 (cible : {targetPos}/4096)");
                    return true;
                }

                int? reportedPosition = await RequestTurntablePositionAsync(TimeSpan.FromMilliseconds(500));
                if (reportedPosition.HasValue && IsTurntableAtPosition(reportedPosition.Value, targetPos, tolerance))
                {
                    AppendNetworkConsoleMessage($"Position atteinte : {reportedPosition.Value}/4096 (cible : {targetPos}/4096)");
                    return true;
                }

                await Task.Delay(checkInterval, cancellationToken);
            }

            AppendNetworkConsoleMessage($"Timeout : position actuelle {turntablePosition}/4096, cible {targetPos}/4096");
            return false;
        }

        private async Task<bool> MoveTurntableIfNeededAsync(
            int targetPos,
            CancellationToken cancellationToken,
            int tolerance = 80)
        {
            await WaitIfSequencePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (_stopRequested) return false;

            if (IsTurntableAtPosition(turntablePosition, targetPos, tolerance))
            {
                AppendNetworkConsoleMessage($"Table tournante déjà à la cible : {turntablePosition}/4096 (cible : {targetPos}/4096)");
                return true;
            }

            int? reportedPosition = await RequestTurntablePositionAsync(TimeSpan.FromMilliseconds(500));
            if (reportedPosition.HasValue && IsTurntableAtPosition(reportedPosition.Value, targetPos, tolerance))
            {
                AppendNetworkConsoleMessage($"Table tournante déjà à la cible : {reportedPosition.Value}/4096 (cible : {targetPos}/4096)");
                return true;
            }

            await UdpSendTurnTableMessageAsync($"turntable,{targetPos},{turntableSpeed}");
            if (_stopRequested) return false;

            return await WaitForTurntablePositionAsync(targetPos, tolerance: tolerance, cancellationToken: cancellationToken);
        }

        private static bool IsTurntableAtPosition(int currentPos, int targetPos, int tolerance)
        {
            const int fullTurn = 4096;

            int normalizedCurrent = ((currentPos % fullTurn) + fullTurn) % fullTurn;
            int normalizedTarget = ((targetPos % fullTurn) + fullTurn) % fullTurn;
            int directDistance = Math.Abs(normalizedCurrent - normalizedTarget);
            int wrappedDistance = fullTurn - directDistance;

            return Math.Min(directDistance, wrappedDistance) <= tolerance;
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
            {
                _stopwatch.Stop();
            }
            else
            {
                EnsureStandaloneTimerUpdateLoop();
                _stopwatch.Start();
            }

            return Task.CompletedTask;
        }

        private void EnsureStandaloneTimerUpdateLoop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _ = UpdateTimerAsync(_cts.Token);
        }

        private async Task RunMakeFocusStackSerieAsync()
        {
            try
            {
                await Task.Run(() => MakeFocusStackSerie());
            }
            catch (Exception ex)
            {
                BeginInvoke((Action)(() => AppendTextToConsoleNL("Erreur FocusStack: " + ex.Message)));
            }
        }

        private Task StopTimer()
        {
            _cts?.Cancel();
            _stopwatch.Reset();

            // Mise à jour du label via Invoke pour thread-safe
            if (lbl_timer.InvokeRequired)
            {
                lbl_timer.Invoke((Action)(() => lbl_timer.Text = "00h 00m 00s"));
            }
            else
            {
                lbl_timer.Text = "00h 00m 00s";
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
