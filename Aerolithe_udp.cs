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

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public void InitializeUdpClient()
        {
            try
            {
                udpClient = new UdpClient(localPort); // Initialize UdpClient
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing UDP client: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

       
      


        public void udpSendStepperMessage(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    client.Send(bytes, bytes.Length, new IPEndPoint(stepperIpAddress, stepperPort));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void udpSendTurnTableMessage(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    client.Send(bytes, bytes.Length, new IPEndPoint(turntableIpAddress, turntablePort));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void udpSendScissorLiftMessage(string message)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                using (UdpClient client = new UdpClient()) // Use a new UdpClient for sending
                {
                    client.Send(bytes, bytes.Length, new IPEndPoint(scissorLiftIpAddress, scissorLiftPort));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending UDP message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void udpSendStepperMotorData(int vitesse, int position)
        {
            string message = $"stepmotor moveto {vitesse},{position},0";
            udpSendStepperMessage(message);
        }
             

        public void listenUDP()
        {
            Task.Run(() => ListenForMessages());
        }

        private async Task ListenForMessages()
        {
            try
            {
                while (true)
                {
                    UdpReceiveResult result = await udpClient.ReceiveAsync();
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    Debug.WriteLine($"Received message from {result.RemoteEndPoint}: {message}");
                    AppendTextToConsole(message);
                    checkMessage(message);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        private void AppendTextToConsole(string message)
        {
            if (txtBox_Console.InvokeRequired)
            {
                txtBox_Console.Invoke(new Action(() => txtBox_Console.Text += message + Environment.NewLine));
            }
            else
            {
                txtBox_Console.Text += message + Environment.NewLine;
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
                    AppendTextToConsole(txt);
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
        }

      
    }
}
