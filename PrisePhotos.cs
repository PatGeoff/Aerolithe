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
        private int actuatorDelay1 = 5000;
        private int actuatorDelay2 = 8000;
        private int turntableDelay = 2000;
        private bool working = false;
        public CancellationTokenSource cancellationTokenSource;


        private async Task PrisePhotoSequenceAsync(CancellationToken cancellationToken)
        {
            int total = 0;
            await UdpSendActuatorMessageAsync("actuator 5");
            await Task.Delay(5000, cancellationToken);
            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            await Task.Delay(100, cancellationToken);
            int divider = 4096 / nombreImages5Degres;
            for (int i = 0; i < nombreImages5Degres; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendTextToConsole($"photo {i}/{nombreImages5Degres}");
                int degres = i * divider;
                await UdpSendTurnTableMessageAsync($"turntable,{degres},{turntableSpeed}");
                AppendTextToConsole($"prise de photo #{total}");
                total += 1;
                await Task.Delay(1000, cancellationToken);
            }
            divider = 4096 / nombreImages25Degres;
            await UdpSendActuatorMessageAsync("actuator 25");
            await Task.Delay(3000, cancellationToken);
            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            await Task.Delay(1000, cancellationToken);
            for (int i = 0; i < nombreImages25Degres; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendTextToConsole($"photo {i}/{nombreImages25Degres}");
                int degres = i * divider;
                await UdpSendTurnTableMessageAsync($"turntable,{degres},{turntableSpeed}");
                AppendTextToConsole($"prise de photo #{total}");
                total += 1;
                await Task.Delay(1000, cancellationToken);
            }
            divider = 4096 / nombreImages45Degres;
            await UdpSendActuatorMessageAsync("actuator 45");
            await Task.Delay(3000, cancellationToken);
            await UdpSendTurnTableMessageAsync($"turntable,0,{turntableSpeed}");
            await Task.Delay(1000, cancellationToken);
            for (int i = 0; i < nombreImages45Degres; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendTextToConsole($"photo {i}/{nombreImages45Degres}");
                int degres = i * divider;
                await UdpSendTurnTableMessageAsync($"turntable,{degres},{turntableSpeed}");
                AppendTextToConsole($"prise de photo #{total}");
                total += 1;
                await Task.Delay(1000, cancellationToken);
            }
            AppendTextToConsole("prise de photo terminée");
        }


    }
}
