using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
        private int turntableDelay = 7000; // secondes
        public int turntableSpeed = 500;
        public int turntablePosition = 0;
        public int delayTimePhotoShoot = 2000; 
        private bool working = false;
        public CancellationTokenSource cancellationTokenSource;


        private async Task PrisePhotoSequenceAsync(CancellationToken cancellationToken)
        {
            
            turntablePositionReached = false;
            actuatorPositionReached = false;
            int total = 0;
            await UdpSendActuatorMessageAsync("actuator 5");
            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            await WaitForTargetPositionConfirmation(turntableDelay, "En attente de la table tournante ", cancellationToken);
            int divider = 4096 / nombreImages5Degres;

            currentSequence = 1; // Sequence 1
            for (int i = 1; i < nombreImages5Degres; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendTextToConsoleNL($"photo {i}/{nombreImages5Degres}");
                int degres = i * divider;

                // Initialize the TaskCompletionSource for each image capture
                imageReadyTcs = new TaskCompletionSource<bool>();

                // Take the picture asynchronously
                await takePictureAsync();

                // Wait for the image to be ready
                await imageReadyTcs.Task;

                
                // Send the turntable command immediately after the image is ready
                await UdpSendTurnTableMessageAsync($"turntable,{degres},{turntableSpeed}");

                // Add a delay after the image is ready
                await Task.Delay(delayTimePhotoShoot); // Delay for x seconds

                AppendTextToConsoleNL($"prise de photo #{total}");
                total += 1;

                // Wait for the turntable to reach the target position
                await WaitForTargetPositionConfirmation(turntableDelay, "En attente de la table tournante ", cancellationToken);
            }

            AppendTextToConsoleNL($"Série 1 terminée");
            // Continue with the rest of your sequences...
        }







        private async Task CountdownWithDelay(int delay, string text, CancellationToken cancellationToken)
        {
            string timestamp = $"{DateTime.Now:HH:mm:ss:ff} - ";
            AppendFormattedText(timestamp, Color.Gray);
            AppendTextToConsoleSL(text);
            for (int i = delay; i > 0; i--)
            {
                AppendTextToConsoleSL($"{i} ... ");
                await Task.Delay(1000, cancellationToken); // Wait for 1 second
            }
            AppendTextToConsoleSL(" 0" + Environment.NewLine);
        }

        private async Task WaitForTargetPositionConfirmation(int time, string text, CancellationToken cancellationToken)
        {
            string timestamp = $"{DateTime.Now:HH:mm:ss:ff} - ";
            AppendFormattedText(timestamp, Color.Gray);
            AppendTextToConsoleSL(text);

            int elapsed = 0;
            while (elapsed < time)
            {
                if (turntablePositionReached || actuatorPositionReached)
                {
                    break;
                }
                AppendTextToConsoleSL($"{(time - elapsed) / 1000} ... ");
                await Task.Delay(1000, cancellationToken); // Wait for 1 second
                elapsed += 1000;
            }
            if (elapsed >= time)
            {
                //AppendFormattedText(timestamp, Color.Gray);
                //AppendTextToConsoleSL("Time elapsed without reaching position." + Environment.NewLine);
                //MessageBox.Show("Impossible de continuer, la table tournante ne répond pas");
                cancellationTokenSource.Cancel();
            }
        }


        private FlowLayoutPanel CreateSequenceFlowLayoutPanel(Color borderColor)
        {
            FlowLayoutPanel sequenceFlowLayoutPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(1),
                Padding = new Padding(0), // Add padding to make the border appear larger
                BackColor = borderColor
            };
            return sequenceFlowLayoutPanel;
        }



    }
}
