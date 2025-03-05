//Aerolithe.cs

using Aerolithe.Properties;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Nikon;
using System.Drawing;
using System.Threading;
using Emgu.CV.XPhoto;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static Emgu.Util.Platform;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public readonly IPAddress stepperIpAddress = IPAddress.Parse("192.168.2.11");
        public readonly IPAddress turntableIpAddress = IPAddress.Parse("192.168.2.12");
        public readonly IPAddress scissorLiftIpAddress = IPAddress.Parse("192.168.2.13");
        public readonly IPAddress M5ipAddress = IPAddress.Parse("192.168.2.6");
        public readonly IPAddress actuatorIpAddress = IPAddress.Parse("192.168.2.15");
        public readonly int stepperPort = 44455;    // Port sur lequel on envoie les messages UDP au ESP32 du stepper motor et de l'actuateur
        public readonly int turntablePort = 44466;  // Port sur lequel on reçoit les messages UDP au ESP32 de la table tournante
        public readonly int scissorLiftPort = 44477;  // Port sur lequel on reçoit les messages UDP au ESP32 du lift
        public readonly int M5Port = 44488;
        public readonly int localPort = 55544;      // Port sur lequel on reçoit les messages UDP
        public readonly int actuatorPort = 44499;



        private UdpClient udpClient;
        private TaskCompletionSource<int> _turntablePositionTcs;

        private bool calibrationDone = true;

        public Aerolithe()
        {
            InitializeComponent();
            this.KeyDown += new KeyEventHandler(Form1_KeyDown);
            this.KeyPreview = true; // Ensure the form receives key events
            picBox_LiveView_Main.Image = Properties.Resources.camera_offline; // Mettre ça ici parce que Visual Studio fait chier 
            CamSetup();
            ButtonSetup();
            InitializeUdpClient();
            listenUDP();
            Ping();
            SetupMainFlowLayoutPanel();
            Task.Run(async () => await InitializeAsync());
        }

        private async Task InitializeAsync()
        {
            await CheckCommunication(); // Wait for CheckCommunication to complete

            if (waveshareAlive)
            {
                await getTurntablePosFromWaveshare(); // Wait for getTurntablePosFromWaveshare to complete
            }
            else
            {
                MessageBox.Show("Il y a un problème avec la table tournante.\n" +
                                "Assurez-vous que le controlleur Waveshare est bien branché\n" +
                                "Ensuite, redémarrez l'application.");
            }
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
            DialogResult result = AutoCloseMessageBox.ShowPressClose("Enlever la météorite, allumer la lumière de la table tournante et appuyer sur le bouton OK ci-bas", 650, 180);
            if (result == DialogResult.OK)
            {
                Task.Run(async () => await getBackgroundImage());
                pictureBox_validationE3.Image = Properties.Resources.crochet;
                ApplyButtonStyle(buttonLabelPairs[2], false);
                ApplyButtonStyle(buttonLabelPairs[3], true);
            }
        }




        private async void btn_DemarrerPrisePhotos_Click(object sender, EventArgs e)
        {

            btn_DemarrerPrisePhotos.BackColor = Color.FromArgb(30, 30, 30);
            btn_DemarrerPrisePhotos.ForeColor = Color.White;
            // Show the cancel button and disable the start button
            btn_cancelSequence.Visible = true;
            btn_DemarrerPrisePhotos.Visible = false;
            if (working)
            {
                MessageBox.Show("La prise de photo est déjà en cours");
                return;
            }

            working = true;
            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await PrisePhotoSequenceAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                AppendTextToConsoleNL("La prise de photos a été cancellée");
            }
            finally
            {
                working = false;
                // Hide the cancel button and enable the start button
                btn_cancelSequence.Visible = false;
                btn_DemarrerPrisePhotos.Visible = true;
            }

        }

        private void btn_cancelSequence_Click(object sender, EventArgs e)
        {
            if (working)
            {
                cancellationTokenSource.Cancel();
            }
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
        private void btn_Autofocus_StepperTab_Click(object sender, EventArgs e)
        {
            NikonAutofocus(1);
        }

        private async void btn_takePicture_Click(object sender, EventArgs e)
        {
            if (projectPath == null)
            {
                SaveProject();  // Demande à setter le projet
            }
            if (projectPath == null)
            {
                return;  // Cancel la prise de photo si le projet n'est pas setté parce que Cancel a été choisi
            }
            await takePictureAsync();
        }

        #endregion

        #region LINÉAIRE TAB
        private void btn_setStepperZeroPosition(object sender, EventArgs e)
        {
            UdpSendStepperMessageAsync("stepmotor setZero");
        }

        private void btn_setStepperMaxPosition(object sender, EventArgs e)
        {
            UdpSendStepperMessageAsync("stepmotor setMaxPos");
        }

        private void stepperMotor_trkbar_MouseUp(object sender, MouseEventArgs e)
        {
            if (calibrationDone)
            {
                int position = stepperMotor_trkbar.Value;
                int speed = 4000;
                lbl_position.Text = position.ToString();
                udpSendStepperMotorData(speed, position);  // Speed, acceleration, position
            }

        }
        private void stepperCalibration_btn_Click(object sender, EventArgs e)
        {
            AppendTextToConsoleNL("Calibrating" + Environment.NewLine);
            UdpSendStepperMessageAsync("stepmotor calibration");
            picBox_CalibrationCheck.Image = Properties.Resources.crochet;
            stepperMotor_trkbar.Enabled = true;
            stepperMotor_trkbar.Value = 30000;
            UdpSendStepperMessageAsync("stepmotor calibration");
            calibrationDone = true;

        }
        private void btn_stepperGetPosition_Click(object sender, EventArgs e)
        {
            UdpSendStepperMessageAsync("stepmotor getstepperposition");
        }
        private void btn_StopLinearMotor_Click(object sender, EventArgs e)
        {
            UdpSendStepperMessageAsync("stepmotor stop");
        }

        public void encoderRotationStepper(int speed)
        {
            if (calibrationDone)
            {

                int position = 0;
                int newSpeed = speed * 400;


                // Use Invoke to safely access the stepperMotor_trkbar.Value
                stepperMotor_trkbar.Invoke(new Action(() =>
                {
                    position = stepperMotor_trkbar.Value;
                }));

                //AppendTextToConsoleNL(position.ToString());
                //AppendTextToConsoleNL($"sending {speed} (newSpeed: {newSpeed}) to stepper trackbar, Current position: {position}");

                udpSendStepperMotorData(newSpeed);
            }
        }


        #endregion

        #region TABLE TOURNANTE TAB


        private void encoderRotationTurnTable(int position)
        {
            turntablePosition += position * 8;
            turntablePosition = Math.Clamp(turntablePosition, 0, 4096);
            TurnTableRotation(turntablePosition);
        }

        private void TurnTableRotation(int position) // Envoie la valeur au ESP32
        {
            AppendTextToConsoleNL("Aero: turnTablePosition = " + turntablePosition.ToString());
            string message = "turntable," + position.ToString() + "," + turntableSpeed;
            if (trkBar_turntable.InvokeRequired)
            {
                trkBar_turntable.Invoke(new Action(() => trkBar_turntable.Value = turntablePosition));
            }

            AppendTextToConsoleNL("Aero --> Table Tournate: " + message);
            Task.Run(async () => await UdpSendTurnTableMessageAsync(message));
        }

        private void btn_queryTurntablePos_Click(object sender, EventArgs e)
        {
            AppendTextToConsoleNL("Aero: Position demandée à la table tournante. Si plus rien ne répond c'est que la communication est perdue");
            Task.Run(async () => await getTurntablePosFromWaveshare());
        }

        private void trkBar_turntable_MouseUp(object sender, MouseEventArgs e)
        {
            TurnTableRotation(trkBar_turntable.Value);
            turntablePosition = trkBar_turntable.Value;
        }
        private void trkBar_turntable_ValueChanged(object sender, EventArgs e)
        {
            lbl_turntablePosition.Text = trkBar_turntable.Value.ToString();

        }
        private async Task getTurntablePosFromWaveshare()  // Demande la position et attend une réponse du waveshare avant de continuer. 
        {
            if (waveshareAlive)
            {
                _turntablePositionTcs = new TaskCompletionSource<int>();
                await UdpSendTurnTableMessageAsync("Aerolithe_Asks_GetPosition");
                turntablePosition = await _turntablePositionTcs.Task;
                if (trkBar_turntable.InvokeRequired)
                {
                    trkBar_turntable.Invoke(new Action(() =>
                    {
                        trkBar_turntable.Value = turntablePosition;
                        lbl_turntablePosition.Text = turntablePosition.ToString();
                    }));
                }
                else
                {
                    trkBar_turntable.Value = turntablePosition;
                    lbl_turntablePosition.Text = turntablePosition.ToString();
                }
            }
            else { MessageBox.Show("Il y a un problème avec la table tournante.\nAssurez-vous que le controlleur Waveshare est bien branché"); }
        }

        #endregion

        #region ÉLÉVATEUR TAB

        private void btn_liftMaxDown_Click(object sender, EventArgs e)
        {
            UdpSendStepperMessageAsync("lift setZero");
            trkBar_Lift.Value = 500;
        }
        private void btn_liftMaxUp_Click(object sender, EventArgs e)
        {

        }

        private void trkBar_Lift_MouseUp(object sender, MouseEventArgs e)
        {
            int val = trkBar_Lift.Value;
            UdpSendScissorLiftMessageAsync("lift position " + val.ToString());

        }

        private void btn_printLiftPositionConsole_Click(object sender, EventArgs e)
        {
            UdpSendScissorLiftMessageAsync("lift getPosition");
        }

        private async Task encoderRotationLift(int speed)
        {

            int newSpeed = speed * 600;


            // Use Invoke to safely access the stepperMotor_trkbar.Value
            //stepperMotor_trkbar.Invoke(new Action(() =>
            //{
            //    position = stepperMotor_trkbar.Value;
            //}));

            //AppendTextToConsoleNL(newSpeed.ToString());
            //AppendTextToConsoleNL($"sending {speed} (newSpeed: {newSpeed}) to stepper trackbar, Current position: {position}");

            await udpSendScissorData(newSpeed);

        }

        #endregion

        #region ACTUATEUR


        private void btn_actuator_5_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator 5");
        }

        private void btn_actuator_25_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator 25");
        }

        private void btn_actuator_45_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator 45");
        }

        public void encoderRotationActuateur(int position)
        {
            if (position == 4)
            {
                AppendTextToConsoleNL("Actuateur à 5 degrés");
                UdpSendActuatorMessageAsync("actuator 5");
                UdpSendActuatorMessageAsync("actuator 45");
            }
            else if (position == 8)
            {
                AppendTextToConsoleNL("Actuateur à 25 degrés");
                UdpSendActuatorMessageAsync("actuator 25");
                UdpSendActuatorMessageAsync("actuator 45");
            }
            else if (position == 12)
            {
                AppendTextToConsoleNL("Actuateur à 45 degrés");
                UdpSendActuatorMessageAsync("actuator 45");
                UdpSendActuatorMessageAsync("actuator 45");
            }


        }

        private void trkBar_Actuator_MouseUp(object sender, MouseEventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator move " + trkBar_Actuator.Value.ToString());
        }

        private void btn_Actuator_Down_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator down");
        }

        private void btn_Actuator_Up_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator up");
        }
        private void btn_actoatorCalibration_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator calibration");
        }

        #endregion

        #region MAIN FORM
        private void btn_clearConsole_Click(object sender, EventArgs e)
        {
            txtBox_Console.Clear();
        }


        private void AppendTextToConsoleNL(string message) // New Line
        {
            string timestamp = $"{DateTime.Now:HH:mm:ss:ff} - ";

            if (txtBox_Console.InvokeRequired)
            {
                Debug.WriteLine("Invoke required");
                txtBox_Console.Invoke(new Action(() =>
                {
                    AppendFormattedText(timestamp, Color.Gray);
                    AppendFormattedText(message + Environment.NewLine, txtBox_Console.ForeColor);
                    ScrollToBottom();
                    //Debug.WriteLine("Message appended via Invoke");
                }));
            }
            else
            {
                AppendFormattedText(timestamp, Color.Gray);
                AppendFormattedText(message + Environment.NewLine, txtBox_Console.ForeColor);
                //Debug.WriteLine("Message appended directly");
                ScrollToBottom();
            }
        }

        private void AppendTextToConsoleSL(string message) // Single Line
        {

            if (txtBox_Console.InvokeRequired)
            {
                Debug.WriteLine("Invoke required");
                txtBox_Console.Invoke(new Action(() =>
                {
                    AppendFormattedText(message, txtBox_Console.ForeColor);
                    ScrollToBottom();
                    //Debug.WriteLine("Message appended via Invoke");
                }));
            }
            else
            {
                AppendFormattedText(message, txtBox_Console.ForeColor);
                //Debug.WriteLine("Message appended directly");
                ScrollToBottom();
            }
        }

        private async Task UpdateConsoleMessageAsync(string message, CancellationToken token) // Permet de printer à chaque 2 secondes jusqu'à ce que le token soit envoyé
        {
            while (!token.IsCancellationRequested)
            {
                AppendTextToConsoleSL(message);
                try
                {
                    await Task.Delay(2000, token);
                }
                catch (TaskCanceledException)
                {
                    // Task was canceled, exit the loop
                    AppendTextToConsoleSL("Task was canceled.");
                    break;
                }
                message += ".";
            }
            AppendTextToConsoleSL("Exiting UpdateConsoleMessageAsync.");
        }


        private void AppendFormattedText(string text, Color color)
        {
            txtBox_Console.SelectionStart = txtBox_Console.TextLength;
            txtBox_Console.SelectionLength = 0;

            txtBox_Console.SelectionColor = color;
            txtBox_Console.AppendText(text);
            txtBox_Console.SelectionColor = txtBox_Console.ForeColor;
        }

        private void ScrollToBottom()
        {
            txtBox_Console.SelectionStart = txtBox_Console.Text.Length;
            txtBox_Console.ScrollToCaret();
        }
        private void Aerolithe_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }
        private void picBox_LiveView_Main_DoubleClick(object sender, EventArgs e)
        {
            if (picBox_LiveView_Main.Image != null)
            {
                MessageBox.Show(picBox_LiveView_Main.Image.Width.ToString() + " * " + picBox_LiveView_Main.Image.Height.ToString());
            }
        }

        private void SetupMainFlowLayoutPanel()
        {
            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.WrapContents = false;
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {

            if ((e.Control || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) && e.KeyCode == Keys.S)
            {

                SaveProject();

            }
        }
        private void btn_setProject_Click(object sender, EventArgs e)
        {
            SaveProject();
        }
        private void btn_projectSetup_Click(object sender, EventArgs e)
        {
            if (btn_goToProjectFolder.Enabled)
            {
                OpenExplorerAtProjectPath(projectPath);
            }
        }

        private void btn_setImageFolder_Click(object sender, EventArgs e)
        {
            SetImageFolder();
        }

        private void btn_goToImageFolder_Click(object sender, EventArgs e)
        {
            if (btn_goToImageFolder.Enabled)
            {
                OpenExplorerAtProjectPath(imagesFolderPath);
            }
        }



        private void OpenExplorerAtProjectPath(string folder)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                if (folder.Contains("."))
                {
                    folder = Path.GetDirectoryName(projectPath);

                }
                string argument = "/select, \"" + folder + "\"";

                Process.Start("explorer.exe", argument);

            }
            else
            {
                MessageBox.Show("Project path is not set.");
            }
        }
        #endregion 

        #region PREFERENCES

        private void Ping()
        {

            //string host = "192.168.2.1";
            //bool result = PingHost(host);
            //AppendTextToConsoleNL($"Ping --> routeur: {(result ? "succès" : "échec")}");
            ////txtBox_Console.Text += ($"Ping --> routeur: {(result ? "succès" : "échec")}" + Environment.NewLine);
            //if (result)
            //{
            //    picBox_routerPing.Image = Resources.crochet;
            //}
            //else
            //{
            //    picBox_routerPing.Image = Resources.echec;
            //}
            //Thread.Sleep(200);
            //host = "192.168.2.14";
            //result = PingHost(host);
            //if (result)
            //{
            //    picBox_wavesharePing.Image = Resources.crochet;
            //}
            //else
            //{
            //    picBox_wavesharePing.Image = Resources.echec;
            //}
            //AppendTextToConsoleNL($"Ping --> table tournante: {(result ? "succès" : "échec")}");
            ////txtBox_Console.Text += ($"Ping --> table tournante: {(result ? "succès" : "échec")}" + Environment.NewLine);
            //Thread.Sleep(200);
            //host = "192.168.2.11";
            //result = PingHost(host);
            //if (result)
            //{
            //    picBox_esp32Ping.Image = Resources.crochet;
            //}
            //else
            //{
            //    picBox_esp32Ping.Image = Resources.echec;
            //}
            //AppendTextToConsoleNL($"Ping --> le reste des équipements: {(result ? "succès" : "échec")}");
            ////txtBox_Console.Text += ($"Ping --> le reste des équipements: {(result ? "succès" : "échec")}" + Environment.NewLine);
        }


        private bool PingHost(string nameOrAddress)
        {
            bool pingable = false;
            Ping pinger = new Ping();

            try
            {
                PingReply reply = pinger.Send(nameOrAddress);
                pingable = reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                // Discard PingExceptions and return false;
            }

            return pingable;
        }

        private void btn_communicationUDP_Click(object sender, EventArgs e)
        {
            //Ping();
            Task.Run(async () => await CheckCommunication());

        }

        private void btn_esp32Reset_Click(object sender, EventArgs e)
        {
            UdpSendStepperMessageAsync("fullSetup");
        }
        private void comboBox_TaillePhotos_SelectedIndexChanged(object sender, EventArgs e)
        {
            NikonEnum imgSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ImageSize);
            imgSize.Index = comboBox_TaillePhotos.SelectedIndex;
            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_ImageSize, imgSize);
        }

        private void comboBox_TailleLiveView_SelectedIndexChanged(object sender, EventArgs e)
        {
            NikonEnum lvSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize);
            lvSize.Index = comboBox_TaillePhotos.SelectedIndex;
            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize, lvSize);
        }


        #endregion





       
    }
}