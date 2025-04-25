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
            await UdpSendTurnTableMessageAsync($"turntable,150,{turntableSpeed}");
            Task.Delay(200);

            int[] serieId = { int.Parse(txtBox_nbrImg5deg.Text), int.Parse(txtBox_nbrImg25deg.Text), int.Parse(txtBox_nbrImg45deg.Text) };
            int[] paddingNbr = { int.Parse(txtBox_seqPad1.Text), int.Parse(txtBox_seqPad2.Text), int.Parse(txtBox_seqPad3.Text) };

            if (projectPath == null)
            {
                {
                    SaveProject();  // Demande à setter le projet
                }
                if (projectPath == null)
                {
                    return;  // Cancel la prise de photo si le projet n'est pas setté parce que Cancel a été choisi
                }
            }
            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForTurntablePositionAsync(0, cancellationToken);
            AppendTextToConsoleNL("position de la table est de " + turntablePosition.ToString() );
            await Task.Delay(1000);

            int divider = 4096 / serieId[serie];

            try
            {
                for (int i = 1; i <= serieId[serie]; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    AppendTextToConsoleNL($"photo {i}/{serieId[serie]}");
                    int degres = i * divider;
                    imageIncr = i-1 + paddingNbr[serie];
                    

                    // Initialize the TaskCompletionSource for each image capture
                    imageReadyTcs = new TaskCompletionSource<bool>();

                    int essai = 0;
                    bool success = false;

                    while (essai < 3 && !success)
                    {
                        try
                        {
                            await NikonAutofocus();
                            await PhotoSuccess(imageNameBase + "_" + imageIncr + ".jpg", degres, true);
                            AppendTextToConsoleNL("Focus effectué avec succès, prochaine étape: prise de photo");
                            await takePictureAsync();
                            // Wait for the image to be ready
                            AppendTextToConsoleNL("En attente de la Nikon");
                            await imageReadyTcs.Task;
                            success = true; // Set success to true if everything goes well
                        }
                        catch (Exception e)
                        {
                            //AppendTextToConsoleNL(e.Message);
                            essai += 1;
                            if (essai >= 3)
                            {
                                AppendTextToConsoleNL($"Focus a échoué pour la photo {i}/{serieId[serie]}, passe cette photo.");
                                await PhotoSuccess(imageNameBase + "_" + imageIncr + ".jpg", degres, false);
                            }
                        }
                    }


                    // Send the turntable command immediately after the image is ready
                    await UdpSendTurnTableMessageAsync($"turntable,{degres},{turntableSpeed}");
                    await WaitForTurntablePositionAsync(degres, cancellationToken);
                    AppendTextToConsoleNL("nouvelle position de la table est atteinte");
                }
            }
            catch (OperationCanceledException)
            {
                AppendTextToConsoleNL("Photo sequence cancelled.");
            }
        }

        private async Task WaitForTurntablePositionAsync(int targetPosition, CancellationToken cancellationToken)
        {
            int tolerance = 10;
            int maxRetries = 100; // Maximum number of retries to avoid infinite loop
            int retryCount = 0;
            bool positionReached = false;

            while (retryCount < maxRetries)
            {
                await getTurntablePosFromWaveshare();
                if (turntablePosition >= targetPosition - tolerance && turntablePosition <= targetPosition + tolerance)
                {
                    positionReached = true;
                    break;
                }
                await Task.Delay(50, cancellationToken); // Adjust delay as needed
                AppendTextToConsoleNL("Position Table Tournante: " + turntablePosition.ToString() + " ----> " + targetPosition.ToString() + " (cible)");
                retryCount++;
            }

            if (positionReached)
            {
                AppendTextToConsoleNL("La table tourante a atteint sa position.");
            }
            else
            {
                AppendTextToConsoleNL("La table tourante n'a pas atteint sa position après 100 essais.");
            }
        }


    }
}
