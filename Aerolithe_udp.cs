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

        public async Task UdpSendLiftStepperNema23MessageAsync(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(stepperLiftNema23IpAddress, stepperLiftNema23Port));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public async Task udpSendScissorData(int vitesse, int position)
        {
            string message = $"lift moveto {vitesse},{position}";
            await UdpSendScissorLiftMessageAsync(message);
        }
        public async Task udpSendScissorData(int vitesse)
        {
            string message = $"lift movespeed {vitesse}";
            await UdpSendScissorLiftMessageAsync(message);
        }

        public async Task UdpSendScissorLiftMessageAsync(string message)
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
            await UdpSendLiftStepperNema23MessageAsync(message);
            //AppendTextToConsoleNL(message);
        }

        public async Task udpSendStepperLiftNema23MotorData(int vitesse) // valeurs 
        {
            string message = $"stepmotor movespeed {vitesse}";
            await UdpSendLiftStepperNema23MessageAsync(message);
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
            //AppendTextToConsoleNL("là");
            //AppendTextToConsoleNL("Message Reçu: " + message);
            #region liftVerticalMotor
            if (message.Contains("Stepper Lift: topLimitPressed"))
            {
                AppendTextToConsoleNL("Stepper Lift: top LimitPressed");
            }
            if (message.Contains("Stepper Lift: bottomLimitPressed"))
            {
                AppendTextToConsoleNL("Stepper Lift: bottom LimitPressed");
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

            if (message.Contains("FarLimitSwitchPressed")) { 
                cameraRailFarLimitSwitchPressed = true;
                //AppendTextToConsoleNL("FarLimitSwitchPressed = True");
            }
            if (message.Contains("FarLimitSwitchReleased")) {
                cameraRailFarLimitSwitchPressed = false;
                //AppendTextToConsoleNL("FarLimitSwitchPressed = False");
            } 
            if (message.Contains("NearLimitSwitchPressed")) {
                cameraRailNearLimitSwitchPressed = true;
                //AppendTextToConsoleNL("NearLimitSwitchPressed = True");
            }
            if (message.Contains("NearLimitSwitchReleased")) {
                cameraRailNearLimitSwitchPressed = false;
                //AppendTextToConsoleNL("NearLimitSwitchPressed = False");
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
                        await udpSendScissorData(int.Parse(firstArg) * 10);
                        break;
                    case "lift_Nema23_osc_fader":
                        await udpSendStepperLiftNema23MotorData(int.Parse(firstArg) * 2000);
                        break;
                    case "lift_JogWheel_osc_fader":
                        await udpSendStepperLiftNema23MotorData(int.Parse(secondArg) * 2000);
                        await udpSendScissorData(int.Parse(firstArg) * 10);
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
      

       

    }
}
