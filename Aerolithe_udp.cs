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


namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private System.Timers.Timer checkTimer;
        private bool esp32Alive = false;
        private bool waveshareAlive = false;
        public bool turntablePositionReached = false;
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
                listenUDP();

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


        private async Task UdpSendActuatorMessageAsync(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    AppendTextToConsoleNL("sent " + message + " to Actuator");
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

        public async Task UdpSendStepperMessageAsync(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(stepperIpAddress, stepperPort));
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
                    AppendTextToConsoleNL("Aero --> ScissorLift: " + message);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
            
        public async Task UdpSendM5MessageAsync(string position)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(position);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(M5ipAddress, M5Port));
                    AppendTextToConsoleNL("Message sent to M5 Dial:" + position);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void udpSendStepperMotorData(int vitesse, int position) // valeurs 
        {
            string message = $"stepmotor moveto {vitesse},{position}";
            UdpSendStepperMessageAsync(message);
        }

        public void udpSendStepperMotorData(int vitesse) // valeurs 
        {
            string message = $"stepmotor movespeed {vitesse}";
            UdpSendStepperMessageAsync(message);
            AppendTextToConsoleNL(message);

        }

        public void udpSendM5Data(int position)
        {

        }

        public void listenUDP()
        {
            Task.Run(() => ListenForMessages());
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
                        Debug.WriteLine($"Received message from {result.RemoteEndPoint}: {message}");
                        //AppendTextToConsoleNL(message);
                        await CheckMessage(message);
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
            if (stepperMotor_trkbar.InvokeRequired)
            {
                stepperMotor_trkbar.Invoke(new Action(() => stepperMotor_trkbar.Enabled = true));
                stepperMotor_trkbar.Invoke(new Action(() => stepperMotor_trkbar.Maximum = (int)position));
                txtBox_Console.Invoke(new Action(() => txtBox_Console.Text += "calibration done, trakcbar enabled" + Environment.NewLine));
                stepperCurrentPosition = stepperMaxPositionValue / 2;
                stepperMotor_trkbar.Invoke(new Action(() => stepperMotor_trkbar.Value = stepperCurrentPosition));
                udpSendStepperMotorData(4000, stepperCurrentPosition);
            }
        }


        public string SanitizeString(string input)
        {
            string[] hiddenChars = new string[] { "\u200B", "\u200C", "\u200D", "\uFEFF", "\u00A0", "\u00AD", "\u200E", "\u200F", "\u202A", "\u202B", "\u202D", "\u202E", "\u202C", "\u2028", "\u2029" };
            foreach (string hiddenChar in hiddenChars)
            {
                input = input.Replace(hiddenChar, "");
            }
            return input;
        }


        static bool IsOscMessage(string message)
        {
            // Check if the first character of the message is a forward slash
            return !string.IsNullOrEmpty(message) && message[0] == '/';
        }

        private void OnOscTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Sanitize the input to remove hidden characters
            string sanitizedMessage = SanitizeString(_lastOscMessage);
            AppendTextToConsoleNL(sanitizedMessage);
            
        }
        private async Task CheckMessage(string message)
        {

            //AppendTextToConsoleNL("Message Reçu: " + message);
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
            if (message.Contains("ok esp32"))
            {
                esp32Alive = true;
                picBox_esp32Com.Image = Resources.crochet;
                //Debug.WriteLine("Reçu un OK du ESP32");
            }
            else
            {
                esp32Alive = false;
                picBox_esp32Com.Image = Resources.echec;
                //Debug.WriteLine("RIEN reçu du ESP32");
            }
            #endregion
            #region Waveshare
            // Waveshare
            if (message.Contains("waveshare --> status ok"))
            {
                waveshareAlive = true;                
                picBox_waveshareCom.Image = Resources.crochet;
                //Debug.WriteLine("Reçu un OK du Waveshare");
            }           
            if (message.Contains("Message de Table Tournante: Position atteinte"))
            {
                turntablePositionReached = true;
            }

            if (message.Contains("waveshare -> position"))
            {
                string[] parts = message.Split(',');

                
                turntablePosition = int.Parse(parts[1].Trim());
                if (trkBar_turntable.InvokeRequired)
                {
                    if (turntablePosition == - 1) turntablePosition = 0;
                    trkBar_turntable.Invoke(new Action(() => trkBar_turntable.Value = turntablePosition));
                }
                if (lbl_turntablePosition.InvokeRequired)
                {
                    lbl_turntablePosition.Invoke(new Action(() => lbl_turntablePosition.Text = trkBar_turntable.Value.ToString()));
                }
                _turntablePositionTcs?.SetResult(turntablePosition); // vers Aerolithe.cs/getTurntablePosFromWaveshare()


            }
            #endregion
            #region M5

            if (message.Contains("Encoder"))   
            {
                string[] parts = message.Split(',');
                if (parts[1] == "0") // 0 = Actuateur Linéaire
                {
                    int value = int.Parse(parts[2]);
                    encoderRotationActuateur(value);

                }
                if (parts[1] == "1") // 1 = Scissor Lift
                {
                    int value = int.Parse(parts[2]);
                    Task.Run(async () => await encoderRotationLift(value));

                }
                if (parts[1] == "2") // 2 = Table Tounante
                {
                    int value = int.Parse(parts[2]);                    
                    encoderRotationTurnTable(value);


                }
                if (parts[1] == "3") // 3 = Caméra
                {
                    int value = int.Parse(parts[2]);
                    encoderRotationStepper(value);

                }
            }
            #endregion
            #region OSC
            if (IsOscMessage(message))
            {
                
                // Store the last OSC message
                _lastOscMessage = message.Replace("\u200B", "").Replace("\u200C", "").Replace("\u200D", "");

                // Restart the timer
                _oscTimer.Stop();
                _oscTimer.Start();
            }
            #endregion
        }



        public void UdpChecker()
        {
            udpClient = new UdpClient();
            checkTimer = new System.Timers.Timer(60000); // Set interval to 60 seconds (60000 ms)
            checkTimer.Elapsed += CheckDevices;
            checkTimer.AutoReset = true;
            checkTimer.Enabled = true;
        }

        public async Task CheckCommunication()
        {

            DateTime now = DateTime.Now;
            lbl_lastTimePing.Text = now.ToString();
            esp32Alive = false;
            waveshareAlive = false;
            await UdpSendStepperMessageAsync("status");
            Thread.Sleep(500);
            await UdpSendTurnTableMessageAsync("status");
            Thread.Sleep(500);
        }
        private void CheckDevices(object? sender, ElapsedEventArgs e)
        {
            Task.Run(async() => await CheckCommunication());
            Ping();
        }

    }
}
