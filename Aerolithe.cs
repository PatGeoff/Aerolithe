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
        private readonly int localPortOSC = 55545;
        public readonly int actuatorPort = 44499;


        private bool isDragging = false;
        private Point startPoint;
        private int scrollStart;




        private UdpClient udpClient;
        private UdpClient udpClientOSC;
        private TaskCompletionSource<int> _turntablePositionTcs;
        private CancellationTokenSource tokenSource;

        private bool calibrationDone = true;


        //private CustomFlowLayoutPanel customFlowLayoutPanel1, customFlowLayoutPanel2, customFlowLayoutPanel3;



        public Aerolithe()
        {
            InitializeComponent();
            //CreateCustomFlowLayouPanels();
            this.KeyDown += new KeyEventHandler(Form1_KeyDown);
            this.KeyPreview = true; // Ensure the form receives key events
            picBox_LiveView_Main.Image = Properties.Resources.camera_offline; // Mettre ça ici parce que Visual Studio fait chier 
            CamSetup();
            ButtonSetup();
            InitializeUdpClient();
            listenUDP();
            Ping();            
            PopulateColorConversionDropdown();
            PopulateColorColorDropdown();
            //Task.Run(async () => await InitializeAsync());


        }

        //private void CreateCustomFlowLayouPanels()
        //{

        //    customFlowLayoutPanel1 = new CustomFlowLayoutPanel
        //    {
        //        AutoScroll = false, // Disable AutoScroll
        //        Dock = DockStyle.Fill // Adjust as needed
        //    };
        //    tableLayoutPanel7.Controls.Add(customFlowLayoutPanel1);
        //    tableLayoutPanel7.SetRow(customFlowLayoutPanel1, 0);
        //    tableLayoutPanel7.SetColumn(customFlowLayoutPanel1, 0);

        //    customFlowLayoutPanel2 = new CustomFlowLayoutPanel
        //    {
        //        AutoScroll = false, // Disable AutoScroll
        //        Dock = DockStyle.Fill // Adjust as needed
        //    };
        //    tableLayoutPanel7.Controls.Add(customFlowLayoutPanel2);
        //    tableLayoutPanel7.SetRow(customFlowLayoutPanel2, 1);
        //    tableLayoutPanel7.SetColumn(customFlowLayoutPanel2, 0);

        //    customFlowLayoutPanel3 = new CustomFlowLayoutPanel
        //    {
        //        AutoScroll = false, // Disable AutoScroll
        //        Dock = DockStyle.Fill // Adjust as needed
        //    };
        //    tableLayoutPanel7.Controls.Add(customFlowLayoutPanel3);
        //    tableLayoutPanel7.SetRow(customFlowLayoutPanel3, 2);
        //    tableLayoutPanel7.SetColumn(customFlowLayoutPanel3, 0);

        //}


        private async Task InitializeAsync()
        {
            //await CheckCommunication(); // Wait for CheckCommunication to complete

            //if (waveshareAlive)
            //{
            //    await getTurntablePosFromWaveshare(); // Wait for getTurntablePosFromWaveshare to complete
            //}
            //else
            //{
            //    MessageBox.Show("Il y a un problème avec la table tournante.\n" +
            //                    "Assurez-vous que le controlleur Waveshare est bien branché\n" +
            //                    "Ensuite, redémarrez l'application.");
            //}
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
            AppendTextToConsoleNL("btnAutofocus_Click");
            NikonAutofocus();
        }


        private void btn_imageFond_Click(object sender, EventArgs e)
        {
            DialogResult result = AutoCloseMessageBox.ShowPressClose("Enlever la météorite, allumer la lumière de la table tournante et appuyer sur le bouton OK ci-bas", 650, 180);
            if (result == DialogResult.OK)
            {
                Task.Run(async () =>
                {
                    await getBackgroundImage();
                });

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

            //try
            //{

            //    await PrisePhotoSequenceAsync(cancellationTokenSource.Token);
            //}
            //catch (OperationCanceledException)
            //{
            //    AppendTextToConsoleNL("La prise de photos a été cancellée");
            //}
            //finally
            //{
            //    working = false;
            //    // Hide the cancel button and enable the start button
            //    btn_cancelSequence.Visible = false;
            //    btn_DemarrerPrisePhotos.Visible = true;
            //}

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



        private async void btn_takePicture_Click(object sender, EventArgs e)
        {

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
            //else { MessageBox.Show("Il y a un problème avec la table tournante.\nAssurez-vous que le controlleur Waveshare est bien branché"); }
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

        private void performActuatorCalibration()
        {
            UdpSendActuatorMessageAsync("actuator calibration");
        }
        private void btn_actoatorCalibration_Click(object sender, EventArgs e)
        {
            performActuatorCalibration();
        }

        #endregion

        #region MAIN FORM
        private void btn_clearConsole_Click(object sender, EventArgs e)
        {
            txtBox_Console.Clear();
        }

        public async Task PhotoSuccess(string imageName, int degrees, bool success)
        {

            Action updateRichTextBox = () =>
            {
                
                // Append the imageName and degrees in white
                richTextBox_PicReport.SelectionColor = Color.White;
                richTextBox_PicReport.AppendText($"{imageName}\t{degrees} degrés\t");

                // Append the success status in green or red
                richTextBox_PicReport.SelectionColor = success ? Color.Green : Color.Red;
                richTextBox_PicReport.AppendText(success ? "Réussi\n" : "Échec\n");
            };

            if (richTextBox_PicReport.InvokeRequired)
            {
                richTextBox_PicReport.Invoke(updateRichTextBox);
            }
            else
            {
                updateRichTextBox();
            }

        }
        public async Task AppendTextToConsoleNL(string message) // New Line
        {
            string timestamp = $"{DateTime.Now:HH:mm:ss:ff} - ";

            if (txtBox_Console.InvokeRequired)
            {
                Debug.WriteLine("Invoke required");
                txtBox_Console.Invoke(new Action(() =>
                {
                    AppendFormattedText(timestamp, Color.Gray);
                    AppendFormattedText(message + Environment.NewLine, txtBox_Console.ForeColor);
                }));
            }
            else
            {
                AppendFormattedText(timestamp, Color.Gray);
                AppendFormattedText(message + Environment.NewLine, txtBox_Console.ForeColor);
            }
        }

        private async Task AppendTextToConsoleSL(string message) // Single Line
        {

            if (txtBox_Console.InvokeRequired)
            {
                Debug.WriteLine("Invoke required");
                txtBox_Console.Invoke(new Action(() =>
                {
                    AppendFormattedText(message, txtBox_Console.ForeColor);
                }));
            }
            else
            {
                AppendFormattedText(message, txtBox_Console.ForeColor);
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


        private async Task AppendFormattedText(string text, Color color)
        {


            if (txtBox_Console.InvokeRequired)
            {
                txtBox_Console.Invoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    AppendFormattedTextInternal(text, color);
                });
            }
            else
            {
                AppendFormattedTextInternal(text, color);
            }

            ManageRichTextBoxContent();

        }


        private void AppendFormattedTextInternal(string text, Color color)
        {
            txtBox_Console.SelectionStart = txtBox_Console.TextLength;
            txtBox_Console.SelectionLength = 0;

            txtBox_Console.SelectionColor = color;
            txtBox_Console.AppendText(text);
            txtBox_Console.SelectionColor = txtBox_Console.ForeColor;
            ManageRichTextBoxContent();
        }




        private void AppendFormattedTextOL(string text, Color color)
        {
            txtBox_Console.SelectionStart = txtBox_Console.TextLength;
            txtBox_Console.SelectionLength = 0;
            txtBox_Console.SelectionColor = color;

            string[] lines = txtBox_Console.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            int lastLineIndex = txtBox_Console.Lines.Length - 1;

            if (lines.Length > 0)
            {
                // Remove the last line
                txtBox_Console.Text = string.Join(Environment.NewLine, lines, 0, lines.Length - 1);
                // Append the new text
                txtBox_Console.AppendText(Environment.NewLine + text);
                Task.Run ( () => ManageRichTextBoxContent());
            }
            else
            {
                txtBox_Console.Text = text;
            }


            txtBox_Console.SelectionColor = txtBox_Console.ForeColor;
        }


        //private void ScrollToBottom()
        //{

        //    if (txtBox_Console != null && !string.IsNullOrEmpty(txtBox_Console.Text))
        //    {
        //        //txtBox_Console.Focus(); // Ensure the TextBox has focus
        //        //int textLength = txtBox_Console.Text.Length;
        //        //if (textLength >= 0)
        //        //{
        //        //    txtBox_Console.SelectionStart = textLength;
        //        //    txtBox_Console.ScrollToCaret();
        //        //}
        //    }

        //}
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


        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {

            if ((e.Control || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) && e.KeyCode == Keys.S)
            {

                SaveProject();

            }
        }
        private void btn_setProject_Click(object sender, EventArgs e)
        {
            CreateNewProject();
        }

        private void btn_openProject_Click(object sender, EventArgs e)
        {
            SelectExistingProject();
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
        private void comboBox_ExpoMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            NikonEnum modeSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ExposureMode);
            modeSize.Index = comboBox_ExpoMode.SelectedIndex;
            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_ExposureMode, modeSize);
        }

        private void comboBox_shutterTime_SelectedIndexChanged(object sender, EventArgs e)
        {
            NikonEnum expoMode = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ExposureMode);
            if (expoMode.Index != 3)
            {
                MessageBox.Show("Il faut mettre le mode d'exposition à Manuel");
            }
            else
            {
                NikonEnum exposureTime = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed);
                exposureTime.Index = comboBox_shutterTime.SelectedIndex;
                device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed, exposureTime);
            }

        }



        #endregion







        private void btn_prisePhotoSeq1_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("Les images seront enregistrées en tant que : " + imageNameBase + Environment.NewLine + "dans " + imagesFolderPath);
            currentSequence = 0;
            Task.Run(async () =>
            {
                //await nikonDoFocus();

                //AppendTextToConsoleNL("on a fini le focus à ce qui paraît");
                //await UdpSendActuatorMessageAsync("actuator 5");
                // await Task.Delay(4000); // Non-blocking wait


                tokenSource = new CancellationTokenSource();

                await PrisePhotoSequenceAsync(tokenSource.Token, 0);

            });

        }

        private void btn_prisePhotoSeq2_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("Les images seront enregistrées en tant que : " + imageNameBase + Environment.NewLine + "dans " + imagesFolderPath);
            currentSequence = 1;

            Task.Run(async () =>
            {
                //await nikonDoFocus();
                //await UdpSendActuatorMessageAsync("actuator 25");
                //await Task.Delay(4000); // Non-blocking wait


                tokenSource = new CancellationTokenSource();

                await PrisePhotoSequenceAsync(tokenSource.Token, 1);
            });

        }

        private void btn_prisePhotoSeq3_Click(object sender, EventArgs e)
        {
            currentSequence = 2;
            //MessageBox.Show("Les images seront enregistrées en tant que : " + imageNameBase + Environment.NewLine + "dans " + imagesFolderPath);
            //Task.Run(async () => await nikonDoFocus());
            Task.Run(async () =>
            {
                //await UdpSendActuatorMessageAsync("actuator 45");
                // await Task.Delay(4000); // Non-blocking wait
                //await nikonDoFocus();

                tokenSource = new CancellationTokenSource();

                await PrisePhotoSequenceAsync(tokenSource.Token, 2);
            });
        }



        private void QueryProject()
        {
            if (projectPath == null)
            {
                SaveProject();  // Demande à setter le projet
            }
            if (projectPath == null)
            {
                return;  // Cancel la prise de photo si le projet n'est pas setté parce que Cancel a été choisi
            }
        }

        private void nouveauToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateNewProject();
        }

        private void ouvrirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectExistingProject();
        }

        private void txtBox_nomImages_Leave(object sender, EventArgs e)
        {
            if (txtBox_nomImages.Text != null)
            {
                imageNameBase = txtBox_nomImages.Text;
                WritePrefs("imageName", imageNameBase);
            }
        }

        private void txtBox_nomImages_KeyDown(object sender, KeyEventArgs e)
        {
            if (txtBox_nomImages.Text != null && e.KeyCode == Keys.Enter)
            {
                imageNameBase = txtBox_nomImages.Text;
                WritePrefs("imageName", imageNameBase);
            }
        }

        private void chkBox_liveView_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBox_liveView.Checked)
            {

                device.LiveViewEnabled = true;
                Task.Run(async () => Task.Delay(100));
                liveViewTimer.Start();

            }
            else
            {
                device.LiveViewEnabled = false;
                liveViewTimer.Stop();
            }


        }

        private void btn_cancelPhotoShoot_Click(object sender, EventArgs e)
        {
            if (tokenSource != null)
            {
                tokenSource.Cancel();
            }
        }


        private void btn_focusMinus_Click(object sender, EventArgs e)
        {
            try
            {
                ManualFocus(1, (double)hScrollBar_driveStep.Value);
            }
            catch (Exception)
            {


            }

        }

        private void btn_focusPlus_Click(object sender, EventArgs e)
        {
            try
            {
                ManualFocus(0, (double)hScrollBar_driveStep.Value);
            }
            catch (Exception)
            {


            }
        }



        private void comboBox_AfcPriority_SelectedIndexChanged(object sender, EventArgs e)
        {
            NikonEnum focusModes = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_AFcPriority);
            focusModes.Index = comboBox_AfcPriority.SelectedIndex;
            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_AFcPriority, focusModes);
        }

        private void comboBox_FocusAeraMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            //try
            //{
            //    NikonEnum focusAreaModes = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_FocusAreaMode);
            //    focusAreaModes.Index = comboBox_FocusAeraMode.SelectedIndex;
            //    device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_FocusAreaMode, focusAreaModes);
            //}
            //catch (Exception)
            //{
            //    MessageBox.Show("Fonction impossible");
            //    throw;
            //}

        }

        private void comboBox_AFMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            uint fm = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AFMode);
            if (fm != 0)
            {
                try
                {
                    uint afm = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AFMode);

                    fm = (uint)comboBox_AFMode.SelectedIndex;
                    device.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AFMode, fm);
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL(ex.Message);
                }

            }
            else
            {
                MessageBox.Show("La lentille et la caméra ne doivent pas être en mode manuel (MF)");
            }
            GetFocusMode();
        }

        private void hScrollBar_driveStep_ValueChanged(object sender, EventArgs e)
        {
            txtBox_DriveStep.Text = hScrollBar_driveStep.Value.ToString();

        }

        private void txtBox_DriveStep_TextChanged(object sender, EventArgs e)
        {
            try
            {
                hScrollBar_driveStep.Value = int.Parse(txtBox_DriveStep.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Valeur Invalide");
            }
        }



        private void btn_AutomaticMFocus_Click(object sender, EventArgs e)
        {
            Task.Run(async () => await AutomaticMFocus());
        }

        private void comboBox_EmguConversion_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedConversion = (ColorConversion)comboBox_EmguConversion.SelectedItem;
        }

        private void comboBox_EmguColor_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_EmguColor.SelectedIndex == 3) { comboBox_EmguConversion.SelectedIndex = 12; }
            else { comboBox_EmguConversion.SelectedIndex = 48; }
        }

        private void btn_liveViewStatus_Click(object sender, EventArgs e)
        {
            AppendTextToConsoleNL(liveViewStatus.ToString());
        }

        private void comboBox_LiveViewAFMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            uint mode = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AFModeAtLiveView);

        }

        private void btn_clearPicLayout_Click(object sender, EventArgs e)
        {

        }

        private void btn_clearPicReport_Click(object sender, EventArgs e)
        {
            richTextBox_PicReport.Clear();
        }
    }
}