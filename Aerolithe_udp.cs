// Aerollithe_udp.cs

using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;
using Emgu.CV;
using System.Timers;
using Aerolithe.Properties;
using SharpOSC;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Nikon;
using System.Windows.Forms.VisualStyles;
using System.Drawing;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;



namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private System.Timers.Timer checkTimer;
        private bool esp32Alive = false;
        private bool waveshareAlive = false;
        public bool actuatorPositionReached = false;
        public int rotaryEncoderStepperMotorValue = 0;
        public bool rotaryEncoderSteperMotorTriggered = false;
        private System.Timers.Timer _oscTimer;
        private string _lastOscMessage;


        public void InitializeUdpClient()
        {
            try
            {
                udpClient = new UdpClient(localPort); // Initialize UdpClient
                udpClientOSC = new UdpClient(localPortOSC);
                Task.Run(() => listenUDP());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing UDP client: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Initialize the timer
            _oscTimer = new System.Timers.Timer(150); // 500 milliseconds
            _oscTimer.Elapsed += OnOscTimerElapsed;
            _oscTimer.AutoReset = false; // Ensure the timer only runs once per interval
        }


        public async Task UdpSendActuatorMessageAsync(string message)
        {
            AppendTextToConsoleNL("* UdpSendActuatorMessageAsync");
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(actuatorIpAddress, actuatorPort));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task UdpSendTurnTableMessageAsync(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(turntableIpAddress, turntablePort));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public async Task UdpSendCameraLinearMessageAsync(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(stepperCameraIpAddress, stepperCameraPort));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public async Task UdpSendLiftVerticalMessageAsync(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(liftVerticalIpAddress, liftVerticalPort));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

     
        public async Task udpSendLiftHorizontalData(int vitesse)
        {
            int speedFactor = 100;  // Pour le nema17 seulement
            string message = $"stepmotor movespeed {vitesse * speedFactor}";
            await UdpSendLiftHorizontalMessageAsync(message);
        }

        public async Task UdpSendLiftHorizontalMessageAsync(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(scissorLiftIpAddress, scissorLiftPort));
                    //AppendTextToConsoleNL("Aero --> ScissorLift: " + message);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public async Task udpSendCameraLinearMotorData(int vitesse, int position) // valeurs 
        {
            string message = $"stepmotor moveto {vitesse},{position}";
            await UdpSendCameraLinearMessageAsync(message);
        }

        public async Task udpSendCameraLinearMotorData(int vitesse) // valeurs 
        {
            string message = $"stepmotor movespeed {vitesse}";
            await UdpSendCameraLinearMessageAsync(message);
            //AppendTextToConsoleNL(message);

        }

        public async Task udpSendStepperLiftNema23MotorData(int vitesse, int position) // valeurs 
        {
            string message = $"stepmotor moveto {vitesse},{position}";
            await UdpSendLiftVerticalMessageAsync(message);
            //AppendTextToConsoleNL(message);
        }

        public async Task udpSendLiftVerticalMotorData(int vitesse) // valeurs 
        {
            string message = $"stepmotor movespeed {vitesse}";
            await UdpSendLiftVerticalMessageAsync(message);
            //AppendTextToConsoleNL(message);

        }

        public async Task listenUDP()
        {
            // Start listening for messages in the background
            Task listenMessagesTask = Task.Run(() => ListenForMessages());

            // Optional delay to stagger the start of the second listener
            await Task.Delay(20);

            // Start listening for OSC messages in the background
            Task listenOSCMessagesTask = Task.Run(() => ListenForOSCMessages());

            // Optionally await both if you want to wait for them to complete
            // await Task.WhenAll(listenMessagesTask, listenOSCMessagesTask);
        }

        private async Task ListenForMessages()
        {

            while (true)
            {
                try
                {
                    while (true)
                    {
                        UdpReceiveResult result = await udpClient.ReceiveAsync();
                        string message = Encoding.UTF8.GetString(result.Buffer);
                        //Debug.WriteLine($"Received message from {result.RemoteEndPoint}: {message}");
                        //AppendTextToConsoleNL($"Received message from {result.RemoteEndPoint}: {message}");
                        CheckMessage(message);
                    }
                }
                catch (Exception ex)
                {

                    Debug.WriteLine($"Exception: {ex.Message}");
                }

            }

        }

        private async Task ListenForOSCMessages()
        {
            AppendTextToConsoleNL("ListenForOSCMessages() lancé");
            while (true)
            {
                try
                {
                    while (true)
                    {
                        UdpReceiveResult result = await udpClientOSC.ReceiveAsync();

                        var oscPacket = OscPacket.GetPacket(result.Buffer);
                        if (oscPacket is OscMessage oscMessage)
                        {
                            var args = oscMessage.Arguments.ToArray();
                            var arguments = string.Join(", ", args.Select(a => a.ToString()));

                            string message = oscMessage.Address + "#" + arguments;
                            CheckOSCMessage(message);
                            //AppendTextToConsoleNL(message);
                        }
                    }
                }
                catch (Exception ex)
                {

                    Debug.WriteLine($"Exception: {ex.Message}");
                }

            }

        }

        private void StepperMotorSetMaxValueEnableTrkbar(long position)
        {
            if (stepperCameraMotor_trkbar.InvokeRequired)
            {
                stepperCameraMotor_trkbar.Invoke(new Action(() => stepperCameraMotor_trkbar.Enabled = true));
                stepperCameraMotor_trkbar.Invoke(new Action(() => stepperCameraMotor_trkbar.Maximum = (int)position));
                txtBox_Console.Invoke(new Action(() => txtBox_Console.Text += "calibration done, trakcbar enabled" + Environment.NewLine));
                stepperCurrentPosition = stepperMaxPositionValue / 2;
                stepperCameraMotor_trkbar.Invoke(new Action(() => stepperCameraMotor_trkbar.Value = stepperCurrentPosition));
                udpSendCameraLinearMotorData(4000, stepperCurrentPosition);
            }
        }

        private void OnOscTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Sanitize the input to remove hidden characters


        }
        private async Task CheckMessage(string message)
        {
            ////AppendTextToConsoleNL("là");
            //AppendTextToConsoleNL("Message Reçu: " + message);
            #region liftVerticalMotor
            if (message.Contains("Lift Moteur Vertical: TopLimitPressed"))
            {
                AppendTextToConsoleNL("Lift Moteur Vertical: TopLimitPressed");
            }
            if (message.Contains("Lift Moteur Vertical: BottomLimitPressed"))
            {
                AppendTextToConsoleNL("Lift Moteur Vertical: BottomLimitPressed");
            }
            if (message.Contains("Stepper lift data:"))
            {
                var data = message.Split(":")[1].Split(",");
                appSettings.VerticalLiftCurrentPos = int.Parse(data[0]);
                appSettings.VerticalLiftMaxPos = int.Parse(data[1]);
                appSettings.VerticalLiftDefaultPos = int.Parse(data[2]);
                displayVerticalLiftData();
            }
            if (message.Contains("Stepper Lift Max Position:"))
            {
                AppendTextToConsoleNL("Lift (Max position verticale): " + message.Split(":")[1]);
            }
            #endregion
            #region stepMotor
            if (message.Contains("calibration done, steppermotor maxPosition: "))
            {
                // Extract the part of the message after "steppermotor far position = "
                string positionString = message.Substring(message.IndexOf("calibration done, steppermotor maxPosition: ") + "calibration done, steppermotor maxPosition: ".Length);

                // Try to parse the extracted substring to a double
                if (long.TryParse(positionString, out long position))
                {
                    string txt = "stepper motor far value set to " + position.ToString();
                    AppendTextToConsoleNL(txt);
                    StepperMotorSetMaxValueEnableTrkbar(position);
                    // Successfully parsed the position
                    Debug.WriteLine($"Parsed position: {position}");
                }
                else
                {
                    // Handle the case where parsing fails
                    Debug.WriteLine("Failed to parse the position.");
                }
            }
            // ESP32

            //if (message.Contains("Message de Table Tournante: Position atteinte"))
            //{
            //    turntablePositionReached = true;
            //}
            if (message.Contains("position"))
            {
                string[] parts = message.Split(',');

                turntablePosition = int.Parse(parts[1].Trim());

                //AppendTextToConsoleNL($"Position de la table tournante reçue: {turntablePosition.ToString()}");
                if (trkBar_turntable.InvokeRequired)
                {
                    if (turntablePosition < 0 || turntablePosition > 4096) turntablePosition = 0;

                    trkBar_turntable.Invoke(new Action(() => trkBar_turntable.Value = turntablePosition));
                }
                if (lbl_turntablePosition.InvokeRequired)
                {
                    lbl_turntablePosition.Invoke(new Action(() => lbl_turntablePosition.Text = trkBar_turntable.Value.ToString() + "/ 4096"));
                    lbl_turntablePosition.Invoke(new Action(() => lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés"));
                }

                _turntablePositionTcs?.SetResult(turntablePosition); // vers Aerolithe.cs/getTurntablePosFromWaveshare()
            }

            if (message.Contains("FarLimitSwitchPressed"))
            {
                cameraRailFarLimitSwitchPressed = true;
                AppendTextToConsoleNL("FarLimitSwitchPressed = True");
            }
            if (message.Contains("FarLimitSwitchReleased"))
            {
                cameraRailFarLimitSwitchPressed = false;
                AppendTextToConsoleNL("FarLimitSwitchPressed = False");
            }
            if (message.Contains("NearLimitSwitchPressed"))
            {
                cameraRailNearLimitSwitchPressed = true;
                AppendTextToConsoleNL("NearLimitSwitchPressed = True");
            }
            if (message.Contains("NearLimitSwitchReleased"))
            {
                cameraRailNearLimitSwitchPressed = false;
                AppendTextToConsoleNL("NearLimitSwitchPressed = False");
            }
            if (message.Contains("StepperSwitchState"))
            {
                string[] msg = message.Split(",");
                cameraRailNearLimitSwitchPressed = (msg[1] == "0") ? false : true;

            }

            #endregion
            #region Actuator
            if (message.Contains("actuator_angle"))
            {
                //AppendTextToConsoleNL("ici");
                string[] parts = message.Split(',');
                actuatorAngle = double.Parse(parts[1].Trim());
                lbl_actuatorAngle.Invoke((MethodInvoker)(() =>
                {
                    lbl_actuatorAngle.Text = actuatorAngle.ToString();
                    lbl_actatorAngle_2.Text = "Actuateur:  " + actuatorAngle.ToString() + " degrés";
                }));
                //await AppendTextToConsoleNL($"Angle de l'actuateur: {actuatorAngle.ToString()}");
                _actuatorAngleTcs?.SetResult(actuatorAngle);

            }


            #endregion

        }
        private async Task CheckOSCMessage(string message)
        {

            #region OSC

            if (message.Contains("OSC"))
            {
                //AppendTextToConsoleNL(message);
                string[] parts = message.Split('#');
                string address = parts[0].Split("/")[1];

                string[] args = parts[1].Split(",");
                string firstArg = args[0];
                string secondArg = "";
                if (args.Length > 1)
                {
                    secondArg = args[1];
                }


                switch (address)
                {
                    case "camera_osc_centrage_btn":
                        await RoutineAutoCentrage();
                        break;
                    case "camera_osc_autofocus_btn":
                        await NikonAutofocus();
                        break;
                    case "camera_osc_motor_fader":
                        udpSendCameraLinearMotorData(int.Parse(firstArg) * 2000);
                        break;
                    case "btn_drivestep":
                        driveStep.Value = double.Parse(firstArg);
                        device.SetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep, driveStep);
                        break;
                    case "btn_camera_osc_drivestep":
                        if (int.Parse(firstArg) > 0)
                        {
                            device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_ClosestToInfinity);
                        }
                        else if (int.Parse(firstArg) < 0)
                        {
                            device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_InfinityToClosest);
                        }
                        break;
                    case "lift_osc_horizontal_fader":
                        await udpSendLiftHorizontalData(int.Parse(firstArg) * 10);
                        break;
                    case "lift_Nema23_osc_fader":
                        await udpSendLiftVerticalMotorData(int.Parse(firstArg) * 2000);
                        break;
                    case "lift_JogWheel_osc_fader":
                        await udpSendLiftVerticalMotorData(int.Parse(secondArg) * 2000);
                        await udpSendLiftHorizontalData(int.Parse(firstArg) * 10);
                        break;
                    case "tableTournante_osc_fader":
                        await UdpSendTurnTableMessageAsync($"turntable,{firstArg},{turntableSpeed}");
                        break;
                    case "actuator_osc_5_btn":
                        await UdpSendActuatorMessageAsync("actuator 5");
                        break;
                    case "actuator_osc_25_btn":
                        await UdpSendActuatorMessageAsync("actuator 25");
                        break;
                    case "actuator_osc_45_btn":
                        await UdpSendActuatorMessageAsync("actuator 45");
                        break;
                    case "actuator_osc_up_btn":
                        await UdpSendActuatorMessageAsync("actuator up");
                        break;
                    case "actuator_osc_down_btn":
                        await UdpSendActuatorMessageAsync("actuator down");
                        break;
                    case "actuator_osc_stop_btn":
                        await UdpSendActuatorMessageAsync("actuator stop");
                        break;
                    default: break;

                }


            }


            #endregion
        }

        private void displayVerticalLiftData()
        {
            //AppendTextToConsoleNL(
            //    $"Lift Vertical\n    - Position actuelle: {appSettings.VerticalLiftCurrentPos}\n    - Position maximale: {appSettings.VerticalLiftMaxPos}\n    - Position définie par défaut: {appSettings.VerticalLiftDefaultPos}"
            //);

            if (lbl_VerticalLiftPosition.InvokeRequired)
            {
                lbl_VerticalLiftPosition.Invoke((MethodInvoker)(() =>
                {
                    lbl_VerticalLiftPosition.Text = "Position:      " + appSettings.VerticalLiftCurrentPos.ToString();
                    lbl_VerticalLiftMaxPos.Text = "Maximum:         " + appSettings.VerticalLiftMaxPos.ToString();
                    lbl_VerticalLiftDefaultPos.Text = "Défaut:      " + appSettings.VerticalLiftDefaultPos.ToString();
                }));
            }
            else
            {
                lbl_VerticalLiftPosition.Text = "Position:  " + appSettings.VerticalLiftCurrentPos.ToString();
                lbl_VerticalLiftMaxPos.Text = "Maximum: " + appSettings.VerticalLiftMaxPos.ToString();
                lbl_VerticalLiftDefaultPos.Text = "Défaut: " + appSettings.VerticalLiftDefaultPos.ToString();
            }
        }

        private async Task GetLinearSwitchesStateFromLinear()
        {
            await UdpSendCameraLinearMessageAsync("stepmotor switchState");
            AppendTextToConsoleNL($"NearLimitSwitchPressed = {cameraRailNearLimitSwitchPressed}, cameraRailFarLimitSwitchPressed = {cameraRailFarLimitSwitchPressed}");

        }


        public static class NetworkChecks
        {
            /// <summary>
            /// Vérifie si l'interface Wi‑Fi est connectée au SSID "Aérolithe" et si son IPv4 est 192.168.2.4.
            /// Retourne (ok, details) de manière asynchrone pour ne pas bloquer l'UI.
            /// </summary>
            public static async Task<(bool ok, string details)> IsOnAerolitheWifiAsync(CancellationToken ct = default)
            {
                // 1) Récupérer SSID via netsh (async)
                string ssid = await GetCurrentWifiSsidViaNetshAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(ssid))
                {
                    return (false, "Impossible d'obtenir le SSID Wi‑Fi (netsh). Carte non connectée ou permissions insuffisantes.");
                }

                // Comparaison SSID (exacte, sensible aux accents, insensible à la casse)
                bool onAerolitheSsid =
                    string.Compare(ssid.Trim(), "Aérolithe", CultureInfo.InvariantCulture,
                                   CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

                // 2) Récupérer l'IPv4 de l'interface Wi‑Fi (Wireless80211) sans bloquer l'UI
                var (wifiIf, ip) = await Task.Run(() =>
                {
                    var iface = NetworkInterface.GetAllNetworkInterfaces()
                                .FirstOrDefault(ni =>
                                    ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                                    ni.OperationalStatus == OperationalStatus.Up);

                    IPAddress? ipv4 = iface?.GetIPProperties()
                                            ?.UnicastAddresses
                                            ?.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                            ?.Address;
                    return (iface, ipv4);
                }, ct).ConfigureAwait(false);

                if (wifiIf == null)
                    return (false, $"SSID actuel: {ssid} ; aucune interface Wi‑Fi UP détectée.");
                if (ip == null)
                    return (false, $"SSID actuel: {ssid} ; aucune adresse IPv4 sur l'interface {wifiIf.Name}.");

                bool ipMatch = ip.Equals(IPAddress.Parse("192.168.2.4"));
                string details = $"SSID actuel: {ssid} ; Interface: {wifiIf.Name} ; IPv4: {ip}.";

                return (onAerolitheSsid && ipMatch, details);
            }

            /// <summary>
            /// Appelle 'netsh wlan show interfaces' et parse le SSID de l'interface actuellement connectée (async).
            /// </summary>
            private static async Task<string> GetCurrentWifiSsidViaNetshAsync(CancellationToken ct)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "wlan show interfaces",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using var p = Process.Start(psi);
                    if (p == null) return string.Empty;

                    // Lecture asynchrone du flux de sortie
                    string output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

                    // Permet d’annuler proprement si le token est déclenché
                    if (!p.HasExited)
                        p.WaitForExit();

                    // Regex multi-langue (FR/EN) sur le SSID
                    var ssidRegexes = new[]
                    {
                new Regex(@"^\s*SSID\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant),
                new Regex(@"^\s*Nom\s+SSID\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)
            };

                    foreach (var rx in ssidRegexes)
                    {
                        var m = rx.Match(output);
                        if (m.Success)
                        {
                            var ssid = m.Groups[1].Value.Trim();
                            // Si Windows ajoute "(1)" pour distinguer, tu peux nettoyer ici selon ton besoin
                            return ssid;
                        }
                    }

                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }



        // === Ping asynchrone d'un hôte ===
        public static async Task<bool> PingHostAsync(IPAddress host, int timeoutMs = 1000)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, timeoutMs);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        // === Met à jour un label (texte + couleur) en respectant le thread UI ===
        private void UpdateStatusLabel(string deviceName, bool isConnected)
        {
            if (!_labelMap.TryGetValue(deviceName, out var lbl))
                return; // Si pas de label défini, on ignore.

            var text = isConnected ? "Connecté" : "NON Connecté";
            var color = isConnected ? Color.ForestGreen : Color.Firebrick;

            if (lbl.InvokeRequired)
            {
                lbl.Invoke(new Action(() =>
                {
                    lbl.Text = text;
                    lbl.ForeColor = color;
                }));
            }
            else
            {
                lbl.Text = text;
                lbl.ForeColor = color;
            }
        }

        // === Met à jour le bouton d'alerte selon l'état global ===
        private void UpdateWarningButton(bool allConnected)
        {
            void SetUi()
            {
                // Texte: "!" en rouge si au moins un NON connecté, sinon vide
                btn_WarningPing.Text = allConnected ? string.Empty : "!";
                btn_WarningPing.ForeColor = allConnected ? SystemColors.ControlText : Color.Firebrick;

                // Optionnel: style supplémentaire
                // btn_WarningPing.Font = new Font(btn_WarningPing.Font, allConnected ? FontStyle.Regular : FontStyle.Bold);
                // btn_WarningPing.BackColor = allConnected ? SystemColors.Control : Color.MistyRose;
            }

            if (btn_WarningPing.InvokeRequired)
                btn_WarningPing.Invoke(new Action(SetUi));
            else
                SetUi();
        }

        // === Ping de tous les appareils + mise à jour des labels et du bouton ===
        public async Task PingAll()
        {
            // Lance les pings en parallèle
            var tasks = devices.Select(async dev =>
            {
                bool ok = await PingHostAsync(dev.Address, timeoutMs: 1000); // garde le timeout similaire
                UpdateStatusLabel(dev.Name, ok);
                return ok; // On retourne l'état pour calculer l'état global
            }).ToArray();

            // Attendre tous les résultats
            bool[] results = await Task.WhenAll(tasks);
            bool allConnected = results.All(r => r);

            // Met à jour le bouton d'alerte
            UpdateWarningButton(allConnected);
        }

        // === Boucle périodique (async) pour pinger toutes les X secondes ===
        public void StartAutoPingLoop(TimeSpan? period = null)
        {
            var interval = period ?? TimeSpan.FromSeconds(60);

            // Annule une boucle existante si nécessaire
            _autoPingCts?.Cancel();
            _autoPingCts = new CancellationTokenSource();
            var token = _autoPingCts.Token;

            // Tâche fire-and-forget (capturée par le CTS)
            _ = Task.Run(async () =>
            {
                try
                {
                    // .NET 6+ : PeriodicTimer
                    var timer = new PeriodicTimer(interval);

                    // Ping immédiat au démarrage
                    await PingAll();

                    // Ping périodique
                    while (await timer.WaitForNextTickAsync(token))
                    {
                        await PingAll();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Arrêt attendu
                }
                catch (Exception ex)
                {
                    // Si tu veux loguer l'erreur :
                    // AppendTextToConsoleNL($"AutoPing error: {ex.Message}");
                    Debug.WriteLine($"AutoPing error: {ex.Message}");
                }
            }, token);
        }



    }


}
