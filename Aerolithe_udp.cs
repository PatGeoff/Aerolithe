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
        }


        private async Task UdpSendActuatorMessageAsync(string message)
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

        public async void UdpSendStepperMessageAsync(string message)
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
       

        public async Task UdpSendScissorLiftMessageAsync(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    await client.SendAsync(bytes, bytes.Length, new IPEndPoint(scissorLiftIpAddress, scissorLiftPort));
                    AppendTextToConsoleNL("Message sent to Scissor Lift with message:"  + message);
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
                        AppendTextToConsoleNL(message);
                        checkMessage(message);
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


        private void checkMessage(string message)
        {
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
            // Waveshare
            if (message.Contains("ok waveshare"))
            {
                waveshareAlive = true;
                picBox_waveshareCom.Image = Resources.crochet;
                //Debug.WriteLine("Reçu un OK du Waveshare");
            }
            else
            {
                waveshareAlive = false;
                picBox_waveshareCom.Image = Resources.echec;
                //Debug.WriteLine("RIEN reçu du from ESP32");

            }
            if (message.Contains("Message de Table Tournante: Position atteinte"))
            {
                turntablePositionReached = true;
            }
            if (message.Contains("Encoder"))
            {
                string[] parts = message.Split(',');
                if (parts[1] == "0") // 0 = StepperMotor Linéaire de la caméra
                {
                    int value = int.Parse(parts[2]);
                    encoderRotationStepper(value);

                }
                if (parts[1] == "1") // 2 = Scissor Lift
                {
                    int value = int.Parse(parts[2]);
                    encoderRotationLift(value);

                }
                if (parts[1] == "2") // 2 = Table Tounante
                {
                    int value = int.Parse(parts[2]);
                    encoderRotationTurnTable(value);

                }
                if (parts[1] == "3") // 2 = Actuateur Linéaire
                {
                    int value = int.Parse(parts[2]);
                    encoderRotationActuateur(value);

                }
            }
        }



        public void UdpChecker()
        {
            udpClient = new UdpClient();
            checkTimer = new System.Timers.Timer(60000); // Set interval to 60 seconds (60000 ms)
            checkTimer.Elapsed += CheckDevices;
            checkTimer.AutoReset = true;
            checkTimer.Enabled = true;
        }

        public void CheckCommunication()
        {

            DateTime now = DateTime.Now;
            lbl_lastTimePing.Text = now.ToString();
            esp32Alive = false;
            waveshareAlive = false;
            picBox_waveshareCom.Image = Resources.echec;
            picBox_esp32Com.Image = Resources.echec;
            AppendTextToConsoleNL("tentative de communication avec le ESP32 dans la boîte blanche");
            //txtBox_Console.Text += "tentative de communication avec le ESP32 dans la boîte blanche" + Environment.NewLine;
            UdpSendStepperMessageAsync("status");
            Thread.Sleep(200);
            AppendTextToConsoleNL("tentative de communication avec le ESP32 dans la table tournante");
            //txtBox_Console.Text += "tentative de communication avec le ESP32 dans la table tournante" + Environment.NewLine;
            UdpSendTurnTableMessageAsync("status");
        }
        private void CheckDevices(object? sender, ElapsedEventArgs e)
        {
            CheckCommunication();
            Ping();
        }

    }
}
