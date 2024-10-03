//Aerolithe.cs

using Aerolithe.Properties;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Windows.Forms;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public readonly IPAddress stepperIpAddress = IPAddress.Parse("192.168.2.11");
        //public readonly IPAddress turntableIpAddress = IPAddress.Parse("192.168.2.12");
        public readonly IPAddress turntableIpAddress = IPAddress.Parse("192.168.2.14");
        public readonly IPAddress scissorLiftIpAddress = IPAddress.Parse("192.168.2.11");
        public readonly int stepperPort = 44455;    // Port sur lequel on envoie les messages UDP au ESP32 du stepper motor et de l'actuateur
        //public readonly int turntablePort = 44466;  // Port sur lequel on reçoit les messages UDP au ESP32 de la table tournante
        public readonly int turntablePort = 44455;  // Port sur lequel on reçoit les messages UDP au ESP32 WaveShare de la table tournante
        public readonly int scissorLiftPort = 44477;  // Port sur lequel on reçoit les messages UDP au ESP32 du lift
        public readonly int localPort = 55544;      // Port sur lequel on reçoit les messages UDP

        private UdpClient udpClient;

        public Aerolithe()
        {
            InitializeComponent();
            CamSetup();
            ButtonSetup();
            InitializeUdpClient();
            listenUDP();
            Ping();
            CheckCommunication();
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            udpClient?.Close();
            base.OnFormClosing(e);
        }


        #region PROCÉDURE TAB
        private void btn_Validation_Click(object sender, EventArgs e)
        {
            string message = "1- Est-ce que les deux tables tournantes sont branchées et que leur lumière est verte?" + Environment.NewLine +
                             "    si non, s'assurer que la borne wifi Aerolithe est bien allumée." + Environment.NewLine +
                             "2- Est-ce que l'ordinateur est connecté sur le réseau Aerolithe? " + Environment.NewLine +
                             "    si non, il lui faut une adresse statique du genre 192.168.2.15, subnet 255.255.255.0, router 192.168.2.1" + Environment.NewLine +
                             "3- Est ce que la lumière des bras motorisés est allumée?" + Environment.NewLine +
                             "    si non, s'assurer qu'ils soient allumés." + Environment.NewLine +
                             "4- Est-ce que les caméras sont bien ouvertes? Elles doivent être en mode photo" + Environment.NewLine +
                             "5- Est-ce que la lentille est en mode M/A et que l'appareil est en mode AF?"
                             ;
            string caption = "Vérification Initiale";
            MessageBoxButtons buttons = MessageBoxButtons.OK;
            MessageBoxIcon icon = MessageBoxIcon.Question;

            //MessageBox.Show(message, caption, buttons, icon);

            AutoCloseMessageBox.ShowPressClose(message, 1130, 440);

            pictureBox_validationE1.Image = Properties.Resources.crochet;
            ApplyButtonStyle(buttonLabelPairs[0], false);
            ApplyButtonStyle(buttonLabelPairs[1], true);
        }
        private void btnAutofocus_Click(object sender, EventArgs e)
        {
            AutoCloseMessageBox.ShowAutoClose("Placer la météorite au centre de la table tournante et appuyer sur OK ci-bas", 650, 180, 3000);
            NikonAutofocus(1);
        }

        private void btn_imageFond_Click(object sender, EventArgs e)
        {
            picBox_imageFond.Image = Properties.Resources.load;
            DialogResult result = AutoCloseMessageBox.ShowPressClose("Enlever la météorite, allumer la lumière de la table tournante et appuyer sur le bouton OK ci-bas", 650, 180);
            if (result == DialogResult.OK)
            {
                getBackgroundImage();
                //    // ICI on prend la photo de background
                //    getBackgroundImage();

                //    DialogResult resultat = AutoCloseMessageBox.ShowPressClose("Remettre la météorite au centre et appuyer sur OK ci-bas", 650, 180);
                //    if (resultat == DialogResult.OK)
                //    {
                //        ApplyButtonStyle(buttonLabelPairs[2], false);
                //        ApplyButtonStyle(buttonLabelPairs[3], true);
                //        pictureBox_validationE3.Image = Properties.Resources.crochet;                    
                //    }
            }
            //backgroundSubstraction();

        }
        #endregion
        #region CAMÉRA TAB
        private void btn_toggleLiveView_Click(object sender, EventArgs e)
        {
            if (liveViewState == true)
            {
                liveViewState = false;
                lbl_LiveViewState.Text = "LiveView OFF";
                device.LiveViewEnabled = false;
            }
            else
            {
                liveViewState = true;
                lbl_LiveViewState.Text = "LiveView ON";
                device.LiveViewEnabled = true;
            }
        }
        private void btn_Autofocus_StepperTab_MouseClick(object sender, MouseEventArgs e)
        {
            NikonAutofocus(1);
        }
        #endregion
        #region LINÉAIRE TAB
        private void btn_setStepperZeroPosition(object sender, EventArgs e)
        {
            udpSendStepperMessage("stepmotor setZero");
        }

        private void stepperMotor_trkbar_MouseUp(object sender, MouseEventArgs e)
        {

            int position = stepperMotor_trkbar.Value;
            int speed = 4000;
            lbl_position.Text = position.ToString();
            udpSendStepperMotorData(speed, position);  // Speed, acceleration, position
        }
        private void stepperCalibration_btn_Click(object sender, EventArgs e)
        {
            txtBox_Console.Text += "Calibrating" + Environment.NewLine;
            udpSendStepperMessage("stepmotor calibration");
            picBox_CalibrationCheck.Image = Properties.Resources.crochet;
            stepperMotor_trkbar.Enabled = true;
            stepperMotor_trkbar.Value = 0;
            udpSendStepperMessage("stepmotor calibration");

        }
        private void btn_stepperGetPosition_Click(object sender, EventArgs e)
        {
            udpSendStepperMessage("stepmotor getstepperposition");
        }
        private void btn_StopLinearMotor_Click(object sender, EventArgs e)
        {
            udpSendStepperMessage("stepmotor stop");
        }
        #endregion
        #region TABLE TOURNANTE TAB
        private void trkBar_turntable_Scroll_1(object sender, EventArgs e)
        {
            int val = trkBar_turntable.Value;
            string message = "turntable pos " + val.ToString();
            lbl_trkbar_TableTournante.Text = trkBar_turntable.Value.ToString();
            udpSendTurnTableMessage(message);
        }


        private void btn_allerA_Click(object sender, EventArgs e)
        {
            try
            {
                int val = txtBox_allerA.TabIndex;
                string message = "turntable pos " + val.ToString();
                lbl_trkbar_TableTournante.Text = trkBar_turntable.Value.ToString();
                udpSendTurnTableMessage(message);

            }
            catch (Exception)
            {

                throw;
            }

        }

        private void trkBar_turntable_MouseUp(object sender, MouseEventArgs e)
        {
            int val = trkBar_turntable.Value;
            string message = "turntable pos " + val.ToString();
            udpSendTurnTableMessage(message);
        }
        #endregion
        #region ÉLÉVATEUR TAB
        private void trkBar_Lift_Scroll(object sender, EventArgs e)
        {
            int val = trkBar_Lift.Value;
            udpSendScissorLiftMessage(val.ToString());
        }

        private void btn_stopLiftStepper_Click(object sender, EventArgs e)
        {
            int val = 0;
            udpSendScissorLiftMessage(val.ToString());
        }
        #endregion
        #region ACTUATEUR

        private void btn_actuator_0_Click(object sender, EventArgs e)
        {
            udpSendStepperMessage("actuator zero");
        }

        private void btn_actuator_5_Click(object sender, EventArgs e)
        {
            udpSendStepperMessage("actuator 5");
        }

        private void btn_actuator_25_Click(object sender, EventArgs e)
        {
            udpSendStepperMessage("actuator 25");
        }

        private void btn_actuator_45_Click(object sender, EventArgs e)
        {
            udpSendStepperMessage("actuator 45");
        }
        #endregion
        #region MAIN FORM
        private void btn_clearConsole_Click(object sender, EventArgs e)
        {
            txtBox_Console.Clear();
        }
        #endregion


        private void btn_takePicture_Click(object sender, EventArgs e)
        {
            takePicture();
        }
        #region PREFERENCES
        private void btn_pingAll_Click(object sender, EventArgs e)
        {
            Ping();
        }

        private void Ping()
        {
            string host = "192.168.2.1";
            bool result = PingHost(host);
            txtBox_Console.Text += ($"Ping --> routeur: {(result ? "succès" : "échec")}" + Environment.NewLine);
            if (result) {
                picBox_routerPing.Image = Resources.crochet;
            }
            else {
                picBox_routerPing.Image = Resources.echec;
            }
            Thread.Sleep(200);
            host = "192.168.2.14";
            result = PingHost(host);
            if (result)
            {
                picBox_wavesharePing.Image = Resources.crochet;
            }
            else
            {
                picBox_wavesharePing.Image = Resources.echec;
            }
            txtBox_Console.Text += ($"Ping --> table tournante: {(result ? "succès" : "échec")}" + Environment.NewLine);
            Thread.Sleep(200);
            host = "192.168.2.11";
            result = PingHost(host);
            if (result)
            {
                picBox_esp32Ping.Image = Resources.crochet;
            }
            else
            {
                picBox_esp32Ping.Image = Resources.echec;
            }
            txtBox_Console.Text += ($"Ping --> le reste des équipements: {(result ? "succès" : "échec")}" + Environment.NewLine);
        }


        public static bool PingHost(string host)
        {
            try
            {
               
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(host);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ping failed: {ex.Message}");
                return false;
            }
        }
        private void btn_communicationUDP_Click(object sender, EventArgs e)
        {
            CheckCommunication();
        }
        private void btn_esp32StepperFullSetup_Click(object sender, EventArgs e)
        {
            udpSendStepperMessage("fullSetup");
        }
        #endregion

        // Bout de viarge! Ça marche tu?


    }
}