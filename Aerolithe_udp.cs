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
using System.Threading;



namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private bool waveshareAlive = false;
        public bool actuatorPositionReached = false;
        public int rotaryEncoderStepperMotorValue = 0;
        public bool rotaryEncoderSteperMotorTriggered = false;
        private System.Timers.Timer _oscTimer;
        private string _lastOscMessage;
        private CancellationTokenSource? _actuatorAnglePollingCts;
        private readonly object _autoCenterCommandLock = new();
        private Task? _autoCenterCommandTask;
        private readonly object _deviceStatusProbeLock = new();
        private readonly Dictionary<IPAddress, TaskCompletionSource<bool>> _deviceStatusProbes = new();


        public void InitializeUdpClient()
        {
            try
            {
                udpClient = new UdpClient(localPort); // Initialize UdpClient
                AppendTextToConsoleNL($"UDP listener démarré sur 0.0.0.0:{localPort}");
                udpClientOSC = new UdpClient(localPortOSC);
                AppendTextToConsoleNL($"UDP OSC listener démarré sur 0.0.0.0:{localPortOSC}");
                Task.Run(() => listenUDP());
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur InitializeUdpClient(): {ex.Message}");
                MessageBox.Show($"Error initializing UDP client: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Initialize the timer
            _oscTimer = new System.Timers.Timer(150); // 500 milliseconds
            _oscTimer.Elapsed += OnOscTimerElapsed;
            _oscTimer.AutoReset = false; // Ensure the timer only runs once per interval
        }


        public async Task UdpSendActuatorMessageAsync(string message)
        {
            AppendNetworkConsoleMessage($"UDP envoyé à Actuator ({actuatorIpAddress}:{actuatorPort}): {message}");
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    if (ShouldSendActuatorSpeedBeforeCommand(message))
                    {
                        int speed = ClampActuatorSpeed(appSettings.ActuatorSpeed);
                        appSettings.ActuatorSpeed = speed;
                        string speedMessage = $"actuator speed, {speed.ToString(CultureInfo.InvariantCulture)}";
                        byte[] speedBytes = Encoding.UTF8.GetBytes(speedMessage);
                        AppendNetworkConsoleMessage($"UDP envoyé à Actuator ({actuatorIpAddress}:{actuatorPort}): {speedMessage}");
                        await client.SendAsync(speedBytes, speedBytes.Length, new IPEndPoint(actuatorIpAddress, actuatorPort));
                        await Task.Delay(75);
                    }

                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(actuatorIpAddress, actuatorPort));
                }

                StartActuatorAnglePolling(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool ShouldSendActuatorSpeedBeforeCommand(string message)
        {
            if (!message.StartsWith("actuator", StringComparison.OrdinalIgnoreCase)) return false;
            if (message.StartsWith("actuator angle", StringComparison.OrdinalIgnoreCase)) return false;
            if (message.StartsWith("actuator speed", StringComparison.OrdinalIgnoreCase)) return false;
            if (message.StartsWith("actuator stop", StringComparison.OrdinalIgnoreCase)) return false;
            if (message.StartsWith("actuator calibration", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private async Task SendActuatorAngleRequestAsync()
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes("actuator angle");
                using (UdpClient client = new UdpClient())
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(actuatorIpAddress, actuatorPort));
                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur lecture angle actuateur: {ex.Message}");
            }
        }

        private void StartActuatorAnglePolling(string message)
        {
            if (!message.StartsWith("actuator", StringComparison.OrdinalIgnoreCase)) return;
            if (message.StartsWith("actuator angle", StringComparison.OrdinalIgnoreCase)) return;

            _actuatorAnglePollingCts?.Cancel();

            if (message.Contains("stop", StringComparison.OrdinalIgnoreCase)) return;

            _actuatorAnglePollingCts = new CancellationTokenSource();
            var token = _actuatorAnglePollingCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    DateTime stopAt = DateTime.UtcNow.AddSeconds(20);
                    while (!token.IsCancellationRequested && DateTime.UtcNow < stopAt)
                    {
                        await SendActuatorAngleRequestAsync();
                        await Task.Delay(400, token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }

        private async Task UdpSendTurnTableMessageAsync(string message)
        {
            AppendNetworkConsoleMessage($"UDP envoyé à Table tournante ({turntableIpAddress}:{turntablePort}): {message}");
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                if (udpClient != null)
                {
                    await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(turntableIpAddress, turntablePort));
                }
                else
                {
                    using UdpClient client = new UdpClient(localPort);
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
            AppendNetworkConsoleMessage($"UDP envoyé à Caméra linéaire ({stepperCameraIpAddress}:{stepperCameraPort}): {message}");
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
            AppendNetworkConsoleMessage($"UDP envoyé à Lift vertical ({liftVerticalIpAddress}:{liftVerticalPort}): {message}");
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
            AppendNetworkConsoleMessage($"UDP envoyé à Lift horizontal ({scissorLiftIpAddress}:{scissorLiftPort}): {message}");
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

        private void AppendNetworkConsoleMessage(string message)
        {
            if (_networkConsoleMessagesEnabled)
            {
                AppendTextToConsoleNL(message);
            }
        }

        private void AppendOscConsoleMessage(string message)
        {
            if (_oscConsoleMessagesEnabled)
            {
                AppendTextToConsoleNL(message);
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

            while (!_shutdownStarted)
            {
                try
                {
                    while (!_shutdownStarted)
                    {
                        UdpReceiveResult result = await udpClient.ReceiveAsync();
                        string message = Encoding.UTF8.GetString(result.Buffer);
                        //Debug.WriteLine($"Received message from {result.RemoteEndPoint}: {message}");
                        if (_networkConsoleMessagesEnabled)
                        {
                            AppendTextToConsoleNL($"UDP reçu de {result.RemoteEndPoint}: {message}");
                        }
                        TryCompleteDeviceStatusProbe(message, result.RemoteEndPoint);
                        CheckMessage(message, result.RemoteEndPoint);
                    }
                }
                catch (ObjectDisposedException) when (_shutdownStarted)
                {
                    return;
                }
                catch (SocketException) when (_shutdownStarted)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (_shutdownStarted)
                    {
                        return;
                    }
                    Debug.WriteLine($"Exception: {ex.Message}");
                }

            }

        }

        private async Task ListenForOSCMessages()
        {
            AppendTextToConsoleNL("ListenForOSCMessages() lancé");
            while (!_shutdownStarted)
            {
                try
                {
                    while (!_shutdownStarted)
                    {
                        UdpReceiveResult result = await udpClientOSC.ReceiveAsync();

                        var oscPacket = OscPacket.GetPacket(result.Buffer);
                        if (oscPacket is OscMessage oscMessage)
                        {
                            var args = oscMessage.Arguments.ToArray();
                            var arguments = string.Join(", ", args.Select(a => a.ToString()));

                            string message = oscMessage.Address + "#" + arguments;
                            AppendOscConsoleMessage($"OSC reçu de {result.RemoteEndPoint}: {message}");

                            _ = CheckOSCMessage(message);
                        }
                    }
                }
                catch (ObjectDisposedException) when (_shutdownStarted)
                {
                    return;
                }
                catch (SocketException) when (_shutdownStarted)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (_shutdownStarted)
                    {
                        return;
                    }
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
        private void CheckMessage(string message, IPEndPoint? remoteEndPoint)
        {
            ////AppendTextToConsoleNL("là");
            //AppendTextToConsoleNL("Message Reçu: " + message);
            #region liftVerticalMotor
            if (message.Contains("Lift Moteur Vertical: TopLimitPressed"))
            {
                AppendTextToConsoleNL("Lift Moteur Vertical: TopLimitPressed");
            }
            if (message.Contains("Lift Moteur Vertical: TopLimitReleased"))
            {
                AppendTextToConsoleNL("Lift Moteur Vertical: TopLimitReleased");
            }
            if (message.Contains("Lift Moteur Vertical: BottomLimitPressed"))
            {
                AppendTextToConsoleNL("Lift Moteur Vertical: BottomLimitPressed");
            }
            if (message.Contains("Lift Moteur Vertical: BottomLimitReleased"))
            {
                AppendTextToConsoleNL("Lift Moteur Vertical: BottomLimitReleased");
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
            bool fromHorizontalLift = IsMessageFrom(remoteEndPoint, scissorLiftIpAddress);
            bool fromVerticalLift = IsMessageFrom(remoteEndPoint, liftVerticalIpAddress);

            //if (message.Contains("Message de Table Tournante: Position atteinte"))
            //{
            //    turntablePositionReached = true;
            //}
            if (TryReadTurntablePositionMessage(message, out int receivedTurntablePosition))
            {
                UpdateTurntablePositionFromUdp(receivedTurntablePosition);
            }

            if (message.Contains("FarLimitSwitchPressed"))
            {
                if (fromHorizontalLift)
                {
                    AppendTextToConsoleNL("Lift Horizontal - Droite (FarLimitSwitchPressed) = True");
                }
                else if (fromVerticalLift)
                {
                    AppendTextToConsoleNL("Lift Vertical - Max (FarLimitSwitchPressed) = True");
                }
                else
                {
                    cameraRailFarLimitSwitchPressed = true;
                    AppendTextToConsoleNL("Caméra linéaire - FarLimitSwitchPressed = True");
                }
            }
            if (message.Contains("FarLimitSwitchReleased"))
            {
                if (fromHorizontalLift)
                {
                    AppendTextToConsoleNL("Lift Horizontal - Droite (FarLimitSwitchPressed) = False");
                }
                else if (fromVerticalLift)
                {
                    AppendTextToConsoleNL("Lift Vertical - Max (FarLimitSwitchPressed) = False");
                }
                else
                {
                    cameraRailFarLimitSwitchPressed = false;
                    AppendTextToConsoleNL("Caméra linéaire - FarLimitSwitchPressed = False");
                }
            }
            if (message.Contains("NearLimitSwitchPressed"))
            {
                if (fromHorizontalLift)
                {
                    AppendTextToConsoleNL("Lift Horizontal - Gauche (NearLimitSwitchPressed) = True");
                }
                else if (fromVerticalLift)
                {
                    AppendTextToConsoleNL("Lift Vertical - Min (NearLimitSwitchPressed) = True");
                }
                else
                {
                    cameraRailNearLimitSwitchPressed = true;
                    AppendTextToConsoleNL("Caméra linéaire - Près (NearLimitSwitchPressed) = True");
                }
            }
            if (message.Contains("NearLimitSwitchReleased"))
            {
                if (fromHorizontalLift)
                {
                    AppendTextToConsoleNL("Lift Horizontal - Gauche (NearLimitSwitchPressed) = False");
                }
                else if (fromVerticalLift)
                {
                    AppendTextToConsoleNL("Lift Vertical - Min (NearLimitSwitchPressed) = False");
                }
                else
                {
                    cameraRailNearLimitSwitchPressed = false;
                    AppendTextToConsoleNL("Caméra linéaire - Loin (NearLimitSwitchPressed) = False");
                }
            }
            if (TryParseStepperSwitchState(message, out bool nearPressed, out bool farPressed))
            {
                if (fromHorizontalLift)
                {
                    _espLiftHorizontalLeftSwitchPressed = nearPressed;
                    _espLiftHorizontalRightSwitchPressed = farPressed;
                    _liftHorizontalSwitchStateTcs?.TrySetResult(true);
                }
                else if (fromVerticalLift)
                {
                    _espLiftVerticalMinSwitchPressed = nearPressed;
                    _espLiftVerticalMaxSwitchPressed = farPressed;
                    _liftVerticalSwitchStateTcs?.TrySetResult(true);
                }
                else
                {
                    cameraRailNearLimitSwitchPressed = nearPressed;
                    cameraRailFarLimitSwitchPressed = farPressed;
                    _linearSwitchStateTcs?.TrySetResult((nearPressed, farPressed));
                }
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
                _actuatorAngleTcs?.TrySetResult(actuatorAngle);

            }


            #endregion

        }

        private static bool IsMessageFrom(IPEndPoint? remoteEndPoint, IPAddress expectedAddress)
        {
            return remoteEndPoint?.Address.Equals(expectedAddress) == true;
        }

        private void TryCompleteDeviceStatusProbe(string message, IPEndPoint? remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                return;
            }

            bool isStatusResponse =
                message.Contains("ok esp32", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("status ok", StringComparison.OrdinalIgnoreCase);

            if (!isStatusResponse)
            {
                return;
            }

            lock (_deviceStatusProbeLock)
            {
                if (_deviceStatusProbes.TryGetValue(remoteEndPoint.Address, out var probe))
                {
                    probe.TrySetResult(true);
                }
            }
        }

        private static bool TryParseStepperSwitchState(string message, out bool nearPressed, out bool farPressed)
        {
            nearPressed = false;
            farPressed = false;

            if (!message.Contains("StepperSwitchState", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = message.Split(",");
            if (parts.Length < 3)
            {
                return false;
            }

            return TryParseInputPullupSwitchValue(parts[1], out nearPressed)
                && TryParseInputPullupSwitchValue(parts[2], out farPressed);
        }

        private static bool TryParseInputPullupSwitchValue(string value, out bool pressed)
        {
            string normalizedValue = value.Trim();
            if (normalizedValue == "0")
            {
                pressed = true;
                return true;
            }

            if (normalizedValue == "1")
            {
                pressed = false;
                return true;
            }

            return bool.TryParse(normalizedValue, out pressed);
        }
        private async Task CheckOSCMessage(string message)
        {
            try
            {
                string[] parts = message.Split('#', 2);
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                {
                    return;
                }

                string address = parts[0]
                    .Trim()
                    .Trim('/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault() ?? string.Empty;

                string normalizedAddress = address.Trim().ToLowerInvariant();
                string[] args = parts.Length > 1
                    ? parts[1].Split(',', StringSplitOptions.TrimEntries)
                    : Array.Empty<string>();

                string firstArg = args.Length > 0 ? args[0] : "0";
                string secondArg = args.Length > 1 ? args[1] : "0";

                int firstValue = ParseOscInt(firstArg);
                int secondValue = ParseOscInt(secondArg);

                AppendOscConsoleMessage($"OSC traité: {address}, args='{string.Join(", ", args)}'");

                if (!IsOscAutoCenterCommand(normalizedAddress)
                    && !IsOscActuatorMoveCommand(normalizedAddress)
                    && !IsOscCalibrationCommand(normalizedAddress))
                {
                    RequestStopAutoCenterBeforeManualCommand();
                }

                switch (normalizedAddress)
                {
                    case "camera_osc_centrage_btn":
                    case "camera_osc_autocentrage_btn":
                    case "camera_osc_auto_centrage_btn":
                        AppendOscConsoleMessage("OSC: auto-centrage demandé.");
                        btn_LiftAutoCenterRoutine_Click(this, EventArgs.Empty);
                        break;

                    case "camera_osc_calibration_btn":
                    case "camera_osc_auto_calibration_btn":
                    case "camera_osc_autocalibration_btn":
                    case "camera_osc_auto_calibration":
                        AppendOscConsoleMessage("OSC: calibration automatique demandée.");
                        _stopRequested = false;
                        cancelAutoCentrage = false;
                        await RunAutoCenterCommandAsync(() => RoutineCalibration());
                        break;

                    case "camera_osc_autofocus_btn":
                    case "camera_osc_autofocus":
                    case "autofocus":
                        AppendOscConsoleMessage("OSC: autofocus demandé.");
                        await nikonDoFocus();
                        break;

                    case "camera_osc_motor_fader":
                        await udpSendCameraLinearMotorData(firstValue * 2000);
                        break;

                    case "btn_drivestep":
                        driveStep.Value = double.Parse(firstArg, CultureInfo.InvariantCulture);
                        device.SetRange(eNkMAIDCapability.kNkMAIDCapability_MFDriveStep, driveStep);
                        break;

                    case "btn_camera_osc_drivestep":
                        if (firstValue > 0)
                        {
                            device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_ClosestToInfinity);
                        }
                        else if (firstValue < 0)
                        {
                            device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MFDrive, (uint)eNkMAIDMFDrive.kNkMAIDMFDrive_InfinityToClosest);
                        }
                        break;

                    case "lift_osc_horizontal_fader":
                        await udpSendLiftHorizontalData(firstValue * 10);
                        break;

                    case "lift_nema23_osc_fader":
                        await udpSendLiftVerticalMotorData(firstValue * 2000);
                        break;

                    case "lift_jogwheel_osc_fader":
                        await udpSendLiftVerticalMotorData(secondValue * 2000);
                        await udpSendLiftHorizontalData(firstValue * 10);
                        break;

                    case "tabletournante_osc_fader":
                        await UdpSendTurnTableMessageAsync($"turntable,{firstArg},{turntableSpeed}");
                        break;

                    case "actuator_osc_5_btn":
                        await UdpSendActuatorMessageAsync("actuator 5");
                        StartManualActuatorAutoCenterTracking(5);
                        break;

                    case "actuator_osc_25_btn":
                        await UdpSendActuatorMessageAsync("actuator 25");
                        StartManualActuatorAutoCenterTracking(25);
                        break;

                    case "actuator_osc_45_btn":
                        await UdpSendActuatorMessageAsync("actuator 45");
                        StartManualActuatorAutoCenterTracking(45);
                        break;

                    case "actuator_osc_up_btn":
                        await UdpSendActuatorMessageAsync("actuator up");
                        StartManualActuatorAutoCenterTracking();
                        break;

                    case "actuator_osc_down_btn":
                        await UdpSendActuatorMessageAsync("actuator down");
                        StartManualActuatorAutoCenterTracking();
                        break;

                    case "actuator_osc_stop_btn":
                        await UdpSendActuatorMessageAsync("actuator stop");
                        break;

                    default:
                        AppendOscConsoleMessage($"OSC ignoré: adresse inconnue '{address}', args='{string.Join(", ", args)}'");
                        break;
                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur OSC: {ex.Message}");
            }
        }

        private static int ParseOscInt(string value)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                return intValue;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                return (int)Math.Round(doubleValue);
            }

            return 0;
        }

        private static bool IsOscAutoCenterCommand(string normalizedAddress)
        {
            return normalizedAddress == "camera_osc_centrage_btn"
                || normalizedAddress == "camera_osc_autocentrage_btn"
                || normalizedAddress == "camera_osc_auto_centrage_btn";
        }

        private static bool IsOscCalibrationCommand(string normalizedAddress)
        {
            return normalizedAddress == "camera_osc_calibration_btn"
                || normalizedAddress == "camera_osc_auto_calibration_btn"
                || normalizedAddress == "camera_osc_autocalibration_btn"
                || normalizedAddress == "camera_osc_auto_calibration";
        }

        private static bool IsOscActuatorMoveCommand(string normalizedAddress)
        {
            return normalizedAddress == "actuator_osc_5_btn"
                || normalizedAddress == "actuator_osc_25_btn"
                || normalizedAddress == "actuator_osc_45_btn"
                || normalizedAddress == "actuator_osc_up_btn"
                || normalizedAddress == "actuator_osc_down_btn";
        }

        private async Task RunAutoCenterCommandAsync(Func<Task> action)
        {
            Task task;
            lock (_autoCenterCommandLock)
            {
                if (_autoCenterCommandTask is { IsCompleted: false })
                {
                    AppendOscConsoleMessage("OSC: auto-centrage déjà en cours, commande ignorée.");
                    return;
                }

                task = action();
                _autoCenterCommandTask = task;
            }

            try
            {
                await task;
            }
            finally
            {
                lock (_autoCenterCommandLock)
                {
                    if (ReferenceEquals(_autoCenterCommandTask, task))
                    {
                        _autoCenterCommandTask = null;
                    }
                }
            }
        }

        private void RequestStopAutoCenterBeforeManualCommand()
        {
            Task? autoCenterTask;
            lock (_autoCenterCommandLock)
            {
                autoCenterTask = _autoCenterCommandTask;
            }

            if (autoCenterTask == null || autoCenterTask.IsCompleted)
            {
                return;
            }

            AppendOscConsoleMessage("OSC: commande manuelle reçue, annulation de l'auto-centrage en cours.");
            cancelAutoCentrage = true;
        }

        private void displayVerticalLiftData()
        {
            AppendTextToConsoleNL(
                $"Lift Vertical - Position: {appSettings.VerticalLiftCurrentPos}, " +
                $"Maximum: {appSettings.VerticalLiftMaxPos}, " +
                $"Défaut: {appSettings.VerticalLiftDefaultPos}");
        }

        private async Task<(bool NearPressed, bool FarPressed, bool Received)> RequestCameraLinearSwitchStateAsync(int timeoutMs = 800)
        {
            _linearSwitchStateTcs = new TaskCompletionSource<(bool NearPressed, bool FarPressed)>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<(bool NearPressed, bool FarPressed)> switchStateTask = _linearSwitchStateTcs.Task;

            try
            {
                await UdpSendCameraLinearMessageAsync("stepmotor switchState");
                Task completedTask = await Task.WhenAny(switchStateTask, Task.Delay(timeoutMs));

                if (completedTask == switchStateTask)
                {
                    var switchState = await switchStateTask;
                    return (switchState.NearPressed, switchState.FarPressed, true);
                }

                return (cameraRailNearLimitSwitchPressed, cameraRailFarLimitSwitchPressed, false);
            }
            finally
            {
                if (ReferenceEquals(_linearSwitchStateTcs?.Task, switchStateTask))
                {
                    _linearSwitchStateTcs = null;
                }
            }
        }

        private async Task GetLinearSwitchesStateFromLinear()
        {
            var switchState = await RequestCameraLinearSwitchStateAsync();
            string source = switchState.Received ? "réponse ESP32" : "dernier état connu (timeout)";

            AppendTextToConsoleNL(
                $"Caméra linéaire ({source}) - NearLimitSwitchPressed = {switchState.NearPressed}, " +
                $"FarLimitSwitchPressed = {switchState.FarPressed}");

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

        private bool UpdateNetworkPingState(string deviceName, bool pingOk)
        {
            return pingOk;
        }

        private int GetDeviceUdpPort(string deviceName)
        {
            return deviceName switch
            {
                "Stepper Camera" => stepperCameraPort,
                "Turntable" => turntablePort,
                "Actuator" => actuatorPort,
                "Lift Vertical" => liftVerticalPort,
                "Scissor Lift" => scissorLiftPort,
                _ => localPort
            };
        }

        private async Task<bool> ProbeDeviceStatusAsync(string deviceName, IPAddress address, int timeoutMs = 300, int attempts = 2)
        {
            var probe = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_deviceStatusProbeLock)
            {
                _deviceStatusProbes[address] = probe;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes("status");
                var endPoint = new IPEndPoint(address, GetDeviceUdpPort(deviceName));

                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    await udpClient.SendAsync(bytes, bytes.Length, endPoint);

                    Task completedTask = await Task.WhenAny(probe.Task, Task.Delay(timeoutMs));
                    if (completedTask == probe.Task && await probe.Task)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                lock (_deviceStatusProbeLock)
                {
                    if (_deviceStatusProbes.TryGetValue(address, out var currentProbe) && ReferenceEquals(currentProbe, probe))
                    {
                        _deviceStatusProbes.Remove(address);
                    }
                }
            }
        }

        // === Met à jour le bouton d'alerte selon l'état global ===
        private void UpdateWarningButton(bool allConnected)
        {
            void SetUi()
            {
                // Texte: "!" en rouge si au moins un NON connecté, sinon vide
                btn_WarningPing.Text = allConnected ? string.Empty : "";
                btn_WarningPing.ForeColor = allConnected ? SystemColors.ControlText : Color.Red;

                // Optionnel: style supplémentaire
                // btn_WarningPing.Font = new Font(btn_WarningPing.Font, allConnected ? FontStyle.Regular : FontStyle.Bold);
                // btn_WarningPing.BackColor = allConnected ? SystemColors.Control : Color.MistyRose;
            }

            if (btn_WarningPing.InvokeRequired)
                btn_WarningPing.Invoke(new Action(SetUi));
            else
                SetUi();
        }

        private async Task<Dictionary<string, bool>> PingAllDevicesAsync()
        {
            var tasks = devices.Select(async dev =>
            {
                bool udpOk = await ProbeDeviceStatusAsync(dev.Name, dev.Address);
                bool isConnected = UpdateNetworkPingState(dev.Name, udpOk);
                UpdateStatusLabel(dev.Name, isConnected);
                return new KeyValuePair<string, bool>(dev.Name, isConnected);
            }).ToArray();

            KeyValuePair<string, bool>[] results = await Task.WhenAll(tasks);
            var statuses = results.ToDictionary(result => result.Key, result => result.Value, StringComparer.OrdinalIgnoreCase);

            bool allConnected = statuses.Values.All(isConnected => isConnected);
            UpdateWarningButton(allConnected);

            return statuses;
        }

        private static bool TryReadTurntablePositionMessage(string message, out int position)
        {
            position = 0;

            if (!message.Contains("position", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = message.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            return int.TryParse(parts[1], out position);
        }

        private void UpdateTurntablePositionFromUdp(int position)
        {
            turntablePosition = Math.Clamp(position, 0, 4096);

            void UpdateUi()
            {
                trkBar_turntable.Value = Math.Clamp(turntablePosition, trkBar_turntable.Minimum, trkBar_turntable.Maximum);
                lbl_turntablePosition.Text = turntablePosition.ToString() + "/ 4096";
                lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
                lbl_ttCurrentPos.Text = "Table Tournante: " + turntablePosition.ToString() + " / " + ttTargetPosition.ToString();
            }

            if (trkBar_turntable.InvokeRequired)
            {
                trkBar_turntable.BeginInvoke((Action)UpdateUi);
            }
            else
            {
                UpdateUi();
            }

            _turntablePositionTcs?.TrySetResult(turntablePosition);
        }

        // === Ping de tous les appareils + mise à jour des labels et du bouton ===
        public async Task<bool> PingAll()
        {
            Dictionary<string, bool> statuses = await PingAllDevicesAsync();
            return statuses.Values.All(isConnected => isConnected);
        }

        private async Task<bool> ConfirmNetworkBeforeSequenceAsync()
        {
            Dictionary<string, bool> statuses = await PingAllDevicesAsync();
            if (statuses.Values.All(isConnected => isConnected))
            {
                return true;
            }

            string disconnectedDevices = string.Join(
                Environment.NewLine,
                statuses
                    .Where(status => !status.Value)
                    .Select(status => $"- {status.Key}"));

            string message =
                "Le réseau Aérolithe ne semble pas connecté ou certains appareils ne répondent pas au ping." +
                Environment.NewLine + Environment.NewLine +
                "Appareils non connectés:" +
                Environment.NewLine +
                disconnectedDevices +
                Environment.NewLine + Environment.NewLine +
                "Voulez-vous continuer quand même?";

            DialogResult result = MessageBox.Show(
                this,
                message,
                "Réseau non connecté",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.None);

            return result == DialogResult.Yes;
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
