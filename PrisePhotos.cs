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
        private int imageIncr = 0;
        public CancellationTokenSource cancellationTokenSource;


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
            await Task.Delay(200);


            int[] serieId = { int.Parse(txtBox_nbrImg5deg.Text), int.Parse(txtBox_nbrImg25deg.Text), int.Parse(txtBox_nbrImg45deg.Text) };
            int[] paddingNbr = { int.Parse(txtBox_seqPad1.Text), int.Parse(txtBox_seqPad2.Text), int.Parse(txtBox_seqPad3.Text) };

            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForTurntablePositionAsync(0, cancellationToken);
            await Task.Delay(200);
            int divider = 4096 / serieId[serie];

            await ResetIncrementation();

            AppendTextToConsoleNL("Série " + (serie+1).ToString() + ": " + serieId[serie].ToString() + " photos");
            try
            {
                for (int i = 1; i <= serieId[serie]; i++)
                {
                    PreparationDossierDestTemp();
                    cancellationToken.ThrowIfCancellationRequested();
                    int degres = (i - 1) * divider;
                    ttTargetPosition = degres;
                    await UdpSendTurnTableMessageAsync($"turntable,{degres},{turntableSpeed}");
                    await WaitForTurntablePositionAsync(degres, cancellationToken);
                    AppendTextToConsoleNL("nouvelle position de la table est atteinte");
                    AppendTextToConsoleNL($"photo {i}/{serieId[serie]} à {degres} degrés d'écart");
                    projet.ImageIncrement = i - 1 + paddingNbr[serie];
                    PreparationNomImage();
                    if (checkBox_SeqFocusStack.Checked)
                    {
                        try
                        {
                            await AutomaticFocusRoutine();
                            AppendTextToConsoleNL("Delta = " + delta.ToString());
                            await AutomaticFocusThenCapture(delta);
                        }
                        catch (Exception e)
                        {
                            AppendTextToConsoleNL(e.Message);
                            //throw;
                            return;
                        }



                        if (checkBox_SeqFocusStack.Checked || checkBox_StackAuto.Checked)
                        {
                            AppendTextToConsoleNL("Focus Stack lancé");
                            MakeFocusStackSerie();
                            // appliquer le masque si nécessaire. 
                            if (checkBox_ApplyMaskStackedImage.Checked)
                            {
                                //Invoke(new Action(async () =>
                                //{
                                //    btn_stopAutomaticFocusCapture.Visible = false;
                                //    btn_stopAutomaticFocusCapture.Enabled = false;

                                //    if (int.TryParse(textBox_nbrFocusSteps.Text, out int stepBack))
                                //    {
                                //        stepBack = stepBack / 2;
                                //        for (int i = 0; i <= stepBack; i++)
                                //        {
                                //            ManualFocus(1, stepSize);
                                //            if (lbl_focusStepsVar.InvokeRequired)
                                //            {
                                //                lbl_focusStepsVar.Invoke(new Action(() =>
                                //                {
                                //                    lbl_focusStepsVar.Text = i.ToString();
                                //                }));
                                //            }
                                //            else
                                //            {
                                //                lbl_focusStepsVar.Text = i.ToString();
                                //            }
                                //            await Task.Delay(100);
                                //        }
                                //    }
                                //    if (checkBox_ApplyMaskStackedImage.Checked)
                                //    {
                                //        PostFocusStackMask();
                                //    }

                                //}));
                                await AppliquerMasqueEtFocusStepAsync();
                            }
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
            Stopwatch sw = Stopwatch.StartNew();
            int essai = 0;
            bool focusReussi = false;

            while (essai < 3 && !focusReussi)
            {
                try
                {

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

        private async Task SequencePrisePhotoTotale(CancellationToken cancellationToken)
        {
            await UdpSendActuatorMessageAsync("actuator 5");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForActuatorPosition(5, cancellationToken);
            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
            currentSequence = 0;
            tokenSource = new CancellationTokenSource();
            await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            //PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            AppendTextToConsoleNL("Séquence 1 terminée");

            await UdpSendActuatorMessageAsync("actuator 25");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForActuatorPosition(25, cancellationToken);
            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
            currentSequence = 1;
            tokenSource = new CancellationTokenSource();
            await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            //PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            AppendTextToConsoleNL("Séquence 2 terminée");

            await UdpSendActuatorMessageAsync("actuator 45");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForActuatorPosition(45, cancellationToken);
            AppendTextToConsoleNL("L'angle de l'actuateur est de " + actuatorAngle.ToString());
            currentSequence = 2;
            tokenSource = new CancellationTokenSource();
            await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            //PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            AppendTextToConsoleNL("Séquence 3 terminée");
        }


        private async Task WaitForTurntablePositionAsync(int targetPosition, CancellationToken cancellationToken)
        {
            int tolerance = 10;
            int maxRetries = 100;
            int retryCount = 0;
            bool positionReached = false;

            while (retryCount < maxRetries)
            {
                await getTurntablePosFromWaveshare();

                // Mise à jour des labels via Invoke
                if (lbl_ttCurrentPos.InvokeRequired)
                {
                    lbl_ttCurrentPos.Invoke(new Action(() =>
                    {
                        lbl_ttCurrentPos.Text = "Table Tournante: " + turntablePosition.ToString() + " / " + ttTargetPosition.ToString();
                    }));
                }
                else
                {
                    lbl_ttCurrentPos.Text = "Table Tournante: " + turntablePosition.ToString() + " / " + ttTargetPosition.ToString();
                }

                //if (lbl_ttTargetPos.InvokeRequired)
                //{
                //    lbl_ttTargetPos.Invoke(new Action(() =>
                //    {
                //        lbl_ttTargetPos.Text = targetPosition.ToString();
                //    }));
                //}
                //else
                //{
                //    lbl_ttTargetPos.Text = targetPosition.ToString();
                //}

                if (turntablePosition >= targetPosition - tolerance && turntablePosition <= targetPosition + tolerance)
                {
                    positionReached = true;
                    break;
                }

                await Task.Delay(50, cancellationToken);
                retryCount++;
            }

            string message = positionReached
                ? "La table tournante a atteint sa position."
                : "La table tournante n'a pas atteint sa position après 100 essais.";

            AppendTextToConsoleNL(message);
        }


        private async Task WaitForActuatorPosition(int targetPosition, CancellationToken cancellationToken)
        {
            int tolerance = 1;
            int maxRetries = 100; // Maximum number of retries to avoid infinite loop
            int retryCount = 0;
            bool positionReached = false;

            while (retryCount < maxRetries)
            {
                await getActuatorAngleFromEsp32();
                if (actuatorAngle >= targetPosition - tolerance && actuatorAngle <= targetPosition + tolerance)
                {
                    positionReached = true;
                    break;
                }
                await Task.Delay(50, cancellationToken); // Adjust delay as needed
                AppendTextToConsoleNL("Angle de l'actuateur: " + actuatorAngle.ToString() + " ----> " + targetPosition.ToString() + " (cible)");
                retryCount++;
            }

            if (positionReached)
            {
                AppendTextToConsoleNL("L'actuateur a atteint sa position.");
            }
            else
            {
                AppendTextToConsoleNL("L'actuateur n'a pas atteint sa position après 100 essais.");
            }
        }

       
    }
}
