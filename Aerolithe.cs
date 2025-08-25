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
using System.Drawing.Printing;
using System.Runtime.CompilerServices;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        // THIS IP ADDRESS 192.168.2.4 //
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


        public bool stackedImageInBuffer = false;
        private bool isDragging = false;
        private Point startPoint;
        private int scrollStart;

        private bool isChangingCheckState = false;
        //private string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyResources\\Models", "u2net.onnx");




        private UdpClient udpClient;
        private UdpClient udpClientOSC;
        private TaskCompletionSource<int> _turntablePositionTcs;
        private TaskCompletionSource<double> _actuatorAngleTcs;
        private CancellationTokenSource tokenSource;



        private bool calibrationDone = true;


        //private CustomFlowLayoutPanel customFlowLayoutPanel1, customFlowLayoutPanel2, customFlowLayoutPanel3;



        public Aerolithe()
        {
            InitializeComponent();
            this.KeyDown += new KeyEventHandler(Form1_KeyDown);
            this.KeyPreview = true; // Ensure the form receives key events
            picBox_LiveView_Main.Image = Properties.Resources.camera_offline; // Mettre ça ici parce que Visual Studio fait chier 
            settings = settings.Load();
            if (!string.IsNullOrEmpty(settings.ProjectPath))
            {
                try
                {
                    prefs = prefs.Load(settings.ProjectPath);
                    OpenProject(settings.ProjectPath);
                    if (!string.IsNullOrWhiteSpace(prefs.ImageFolderPath))
                        imagesFolderPath = prefs.ImageFolderPath;

                    if (!string.IsNullOrWhiteSpace(prefs.ImageName))
                        imageNameBase = prefs.ImageName;

                }
                catch (Exception e)
                {
                    Debug.WriteLine("Erreur:" + e);
                }

            }
            CamSetup();
            ButtonSetup();
            InitializeUdpClient();
            Task.Run(() => listenUDP());
            Ping();
            PopulateColorConversionDropdown();
            PopulateColorColorDropdown();
            SetupPen();
            SetTooltips();
            getActuatorAngleFromEsp32();
            getTurntablePosFromWaveshare();
            // _session = new InferenceSession(modelPath);


        }


        private void SetTooltips()
        {
            System.Windows.Forms.ToolTip toolTipMask = new System.Windows.Forms.ToolTip();
            toolTipMask.SetToolTip(btn_applyMask, "Appliquer le masque");
            System.Windows.Forms.ToolTip toolTipCutoff = new System.Windows.Forms.ToolTip();
            toolTipCutoff.SetToolTip(btn_displayLineBlack, "Afficher le cutoff");

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
            udpClientOSC?.Close();
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
            nikonDoFocus();
        }


        private void btn_imageFond_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedIndex = 6;
            tabControl2.SelectedIndex = 4;
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
            AppendTextToConsoleNL("takePictureAsync a mis ImageReadyTcs à true");
            //takePictureAsync();
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
            //AppendTextToConsoleNL("Aero: Position demandée à la table tournante. Si plus rien ne répond c'est que la communication est perdue");
            Task.Run(async () => await getTurntablePosFromWaveshare());
        }

        private void trkBar_turntable_MouseUp(object sender, MouseEventArgs e)
        {
            TurnTableRotation(trkBar_turntable.Value);
            turntablePosition = trkBar_turntable.Value;
        }
        private void trkBar_turntable_ValueChanged(object sender, EventArgs e)
        {
            lbl_turntablePosition.Text = trkBar_turntable.Value.ToString() + " / 4096";
            lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";

        }
        private async Task getTurntablePosFromWaveshare()  // Demande la position et attend une réponse du waveshare avant de continuer. 
        {
            try
            {
                _turntablePositionTcs = new TaskCompletionSource<int>();
                await UdpSendTurnTableMessageAsync("Aerolithe_Asks_GetPosition");
                turntablePosition = await _turntablePositionTcs.Task;
                if (trkBar_turntable.InvokeRequired)
                {
                    trkBar_turntable.Invoke(new Action(() =>
                    {
                        trkBar_turntable.Value = turntablePosition;
                        lbl_turntablePosition.Text = turntablePosition.ToString() + "/ 4096";
                        lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
                    }));
                }
                else
                {
                    trkBar_turntable.Value = turntablePosition;
                    lbl_turntablePosition.Text = turntablePosition.ToString() + "/ 4096";
                    lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
                }
            }
            catch (Exception e)
            {

                AppendTextToConsoleNL(e.Message);
            }



        }

        private async Task getActuatorAngleFromEsp32()
        {

            try
            {
                _actuatorAngleTcs = new TaskCompletionSource<double>();
                await UdpSendActuatorMessageAsync("actuator angle");
                actuatorAngle = await _actuatorAngleTcs.Task;
                //AppendTextToConsoleNL("angle: " + actuatorAngle.ToString());

            }
            catch (Exception e)
            {
                AppendTextToConsoleNL(e.Message);
            }
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

        private void btn_gotCustomAngle_Click(object sender, EventArgs e)
        {
            if (int.TryParse(txtBox_customAngle.Text, out int angle))
            {
                if (angle >= 0 && angle <= 55)
                {
                    UdpSendActuatorMessageAsync($"actuator custom, {angle}");
                }
                else
                {
                    MessageBox.Show("L'angle ne doit pas dépasser 55 degrés", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Svp entrer une valeur valide pour l'angle", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                // Append the success status in green or red
                richTextBox_PicReport.SelectionColor = success ? Color.Green : Color.Red;
                richTextBox_PicReport.AppendText(success ? "Réussi\t" : "Échec\t");

                // Append the imageName and degrees in white
                richTextBox_PicReport.SelectionColor = Color.White;
                richTextBox_PicReport.AppendText($"{imageName}\t{degrees} degrés\n");

                richTextBox_PicReport.ScrollToCaret();


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
                //Debug.WriteLine("Invoke required");
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
                //Debug.WriteLine("Invoke required");
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
                Task.Run(() => ManageRichTextBoxContent());
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

                SavePrefsSettings();

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
            //imgSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ImageSize);
            //AppendTextToConsoleNL("la dimension des images prises est l'index : " + imgSize.Index.ToString());
        }

        private void comboBox_TailleLiveView_SelectedIndexChanged(object sender, EventArgs e)
        {
            NikonEnum lvSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize);
            lvSize.Index = comboBox_TailleLiveView.SelectedIndex;
            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize, lvSize);
            //uint WBM = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_SpotWBMode);
            //AppendTextToConsoleNL("le mode de SpotWMMode est de " + WBM.ToString());
            //uint MUS = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_MirrorUpStatus);
            //AppendTextToConsoleNL("le mode MirrorUpStatus est " + MUS.ToString());
            //lvSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_LiveViewImageSize);
            //AppendTextToConsoleNL("la dimension du Liveview de la cam est l'index : " + lvSize.Index.ToString());
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


        private void btn_PrisePhotoSeqTotale_Click(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                tokenSource = new CancellationTokenSource();
                await SequencePrisePhotoTotale(tokenSource.Token);
            });
        }




        private void btn_prisePhotoSeq1_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("Les images seront enregistrées en tant que : " + imageNameBase + Environment.NewLine + "dans " + imagesFolderPath);
            currentSequence = 0;
            Task.Run(async () =>
            {
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
                tokenSource = new CancellationTokenSource();
                await PrisePhotoSequenceAsync(tokenSource.Token, 1);
            });

        }

        private void btn_prisePhotoSeq3_Click(object sender, EventArgs e)
        {
            currentSequence = 2;
            Task.Run(async () =>
            {
                tokenSource = new CancellationTokenSource();

                await PrisePhotoSequenceAsync(tokenSource.Token, 2);
            });
        }



        private void QueryProject()
        {
            if (projectPath == null)
            {
                SavePrefsSettings();  // Demande à setter le projet
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
                //WritePrefs("imageName", imageNameBase);
                prefs.ImageName = imageNameBase;
                SavePrefsSettings();
            }
        }

        private void txtBox_nomImages_KeyDown(object sender, KeyEventArgs e)
        {
            if (txtBox_nomImages.Text != null && e.KeyCode == Keys.Enter)
            {
                imageNameBase = txtBox_nomImages.Text;
                //WritePrefs("imageName", imageNameBase);
                prefs.ImageName = imageNameBase;
                SavePrefsSettings();
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
            Task.Run(() =>
            {
                if (tokenSource != null)
                {
                    tokenSource.Cancel();
                }
            });
        }


        private void btn_focusMinus_Click(object sender, EventArgs e)
        {
            try
            {
                ManualFocus(1, (double)hScrollBar_driveStep.Value);
                focusStackStepVar -= 1;
                lbl_focusStepsVar.Text = focusStackStepVar.ToString();
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
                focusStackStepVar += 1;
                lbl_focusStepsVar.Text = focusStackStepVar.ToString();
            }
            catch (Exception)
            {


            }
        }

        private void btn_clearFocusStepVar_Click(object sender, EventArgs e)
        {
            focusStackStepVar = 0;
            lbl_focusStepsVar.Text = focusStackStepVar.ToString();
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
            AutomaticFocusRoutine();
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
            //uint mode = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AFModeAtLiveView);

        }

        private void comboBox_ImageType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var imageType = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_CompressionLevel);
            imageType.Index = comboBox_ImageType.SelectedIndex;
            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_CompressionLevel, imageType);
        }


        private void btn_clearPicLayout_Click(object sender, EventArgs e)
        {
            flowLayoutPanel1.Controls.Clear();
            var result = MessageBox.Show(
                                               "Voulez-vous aussi supprimer toutes les images sur le disque?",
                                               "Suppression de l'image",
                                               MessageBoxButtons.YesNo,
                                               MessageBoxIcon.Question
                                           );

            if (result == DialogResult.Yes)
            {
                try
                {
                    string[] imageFiles = Directory.GetFiles(imagesFolderPath, "*.jpg"); // ou *.png, *.jpeg, etc.
                    foreach (string file in imageFiles)
                    {
                        File.Delete(file);
                    }

                    MessageBox.Show("Toutes les images ont été supprimées avec succès.", "Suppression terminée", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de la suppression des fichiers : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

        }

        private void btn_clearPicReport_Click(object sender, EventArgs e)
        {
            richTextBox_PicReport.Clear();
        }



        private void pnl_LiveView_Main_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDrawing = true;
                startY = e.Y;
                currentY = e.Y;
                pnl_DrawingLiveView.Invalidate(); // Redraw the panel
            }
        }

        private void pnl_LiveView_Main_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                currentY = e.Y;
                pnl_DrawingLiveView.Invalidate(); // Redraw the panel
            }
        }

        private void pnl_LiveView_Main_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                isDrawing = false;
                pnl_DrawingLiveView.Invalidate(); // Redraw the panel
            }
        }


        private void pnl_LiveView_Paint(object sender, PaintEventArgs e)
        {
            if (isDrawing || currentY > 0)
            {
                if (customPen.IsVisible)
                {
                    using (Pen pen = customPen.GetPen())
                    {
                        e.Graphics.DrawLine(pen, 0, startY, pnl_DrawingLiveView.Width, startY);
                    }
                }
                if (customBrush.IsVisible)
                {
                    DrawBlackBelowLine(startY, e.Graphics);
                }
            }
        }


        private void btn_displayLineBlack_Click(object sender, EventArgs e)
        {

            customPen.IsVisible = !customPen.IsVisible; // Toggle pen visibility
            customBrush.IsVisible = !customBrush.IsVisible; // Toggle brush visibility
            pnl_DrawingLiveView.Invalidate(); // Redraw the panel

        }


        private void btn_plusSizePic_Click(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                panelSize = new Size(panelSize.Width + 20, panelSize.Height + 20);
                await ResizePanelsAsync(panelSize);
            });


        }

        private void btn_minusSizePic_Click(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                panelSize = new Size(panelSize.Width - 20, panelSize.Height - 20);
                await ResizePanelsAsync(panelSize);
            });

        }

        private async Task ResizePanelsAsync(Size newSize)
        {
            foreach (Panel panel in flowLayoutPanel1.Controls.OfType<Panel>())
            {

                await Task.Run(() =>
                {
                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                    {
                        panel.Size = newSize;
                    });
                });

            }
        }



        private void btn_applyMask_Click(object sender, EventArgs e)
        {
            applyMaskToLiveView = applyMaskToLiveView ? false : true;


            if (chkBox_background1.Checked)
            {
                try
                {
                    background = CvInvoke.Imread(backgroundImage_1, ImreadModes.Color);
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL(ex.Message);
                    return;
                }

            }
            else if (chkBox_background2.Checked)
            {
                try
                {
                    background = CvInvoke.Imread(backgroundImage_2, ImreadModes.Color);
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL(ex.Message);
                    return;
                }

            }
            else if (chkBox_background3.Checked)
            {
                try
                {
                    background = CvInvoke.Imread(backgroundImage_3, ImreadModes.Color);
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL(ex.Message);
                    return;
                }

            }
        }

        private void btn_toggleBW_Click(object sender, EventArgs e)
        {
            customBrush.Color = customBrush.Color == Color.White ? Color.Black : (customBrush.Color == Color.Black ? Color.White : customBrush.Color);


        }

        private void Aerolithe_SizeChanged(object sender, EventArgs e)
        {
            pnl_DrawingLiveView.Width = picBox_LiveView_Main.Width;
            pnl_DrawingLiveView.Height = picBox_LiveView_Main.Height;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            AppendTextToConsoleNL($"LiveView size = ");
        }

        private void btn_getActuatorAngle_Click(object sender, EventArgs e)
        {
            //AppendTextToConsoleNL("Angle demandé à l'actuateur: ");
            Task.Run(async () => await getActuatorAngleFromEsp32());


        }

        private void btn_threadCount_Click(object sender, EventArgs e)
        {
            AppendTextToConsoleNL("Thread count: " + Process.GetCurrentProcess().Threads.Count);
        }

        private void btn_stopActuatorMoving_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator stop");
        }



        private void chkBox_background_CheckedChanged(object sender, EventArgs e)
        {
            if (isChangingCheckState) return;

            var changedBox = sender as CheckBox;

            if (changedBox.Checked)
            {
                isChangingCheckState = true;

                // Uncheck all others
                foreach (var box in new[] { chkBox_background1, chkBox_background2, chkBox_background3 })
                {
                    if (box != changedBox)
                        box.Checked = false;
                }

                isChangingCheckState = false;
            }
            else
            {
                // Prevent unchecking the only selected checkbox
                if (!chkBox_background1.Checked && !chkBox_background2.Checked && !chkBox_background3.Checked)
                {
                    isChangingCheckState = true;
                    changedBox.Checked = true;
                    isChangingCheckState = false;
                }
            }
        }

        private void tableLayoutPanel41_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btn_loadSharpImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select an Image";
                openFileDialog.Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tif;*.tiff";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Load the selected image into the PictureBox
                    picBox_FocusStackedImage.Image = new Bitmap(openFileDialog.FileName);
                    picBox_FocusStackedImage.SizeMode = PictureBoxSizeMode.Zoom; // Optional: scales image to fit
                }
            }
        }

        private void btn_applySharpMask_Click(object sender, EventArgs e)
        {
            MasqueAvecPixels();
        }

        private void picBox_pictureTaken_DoubleClick(object sender, EventArgs e)
        {           
                if (picBox_pictureTaken.Image != null && imagesFolderPath != null && imageNameBase != null)
                {
                    string nomImage = imageNameBase + "_" + imageIncr + ".jpg";
                    string imagePath = Path.Combine(imagesFolderPath, nomImage);

                    if (File.Exists(imagePath))
                    {
                        ImageViewerForm viewer = new ImageViewerForm(imagePath);
                        viewer.Show();
                    }
                    else
                    {
                        MessageBox.Show("Image introuvable : " + imagePath + Environment.NewLine + "Il faut choisir un projet");
                    }
                }
                else
                {
                     MessageBox.Show($"le dossier de l'image ({imagesFolderPath}) et le nom de l'image ({imageNameBase}) ne sont pas bons");
                }
        }


        public partial class ImageViewerForm : Form
        {
            public ImageViewerForm(string imagePath)
            {


                this.Text = "Aperçu de l'image";
                this.WindowState = FormWindowState.Maximized;

                PictureBox pictureBox = new PictureBox
                {
                    Image = Image.FromFile(imagePath),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Dock = DockStyle.Fill
                };

                this.Controls.Add(pictureBox);
            }
        }

      

        private void hScrollBar_liveMaskThresh_Scroll(object sender, ScrollEventArgs e)
        {
            lbl_maskAmount.Text = hScrollBar_liveMaskThresh.Value.ToString();
        }

        private void btn_goToImgFolder_Click(object sender, EventArgs e)
        {
            if (btn_goToImageFolder.Enabled)
            {
                OpenExplorerAtProjectPath(imagesFolderPath);
            }
        }

        private async void button1_Click_1(object sender, EventArgs e)
        {
            for (int i = 0; i < int.Parse(txtBox_nbrImg5deg.Text); i++)
            {
                NikonAutofocus();

                await WaitUntilDeviceReady();

                takePictureAsync();

            }
        }

        private async Task WaitUntilDeviceReady()
        {


            while (true)                //wait until device is no longer busy 
            {
                try
                {
                    device.Start(eNkMAIDCapability.kNkMAIDCapability_DeviceReady);
                }
                catch (NikonException ex)
                {
                    if (ex.ErrorCode == eNkMAIDResult.kNkMAIDResult_DeviceBusy)
                    {
                        Thread.Sleep(150);
                        Debug.WriteLine(ex.Message);
                        continue;                   //continue waiting
                    }
                    else
                    {
                        //received something like 'Not Supported', 'OutOfFocus', 'DriveEnd',       //..'BulbReleaseBusy','CaptureFailure' or 'UnexpectedError'.
                        //.. break for anything you expect, otherwise throw
                        throw;
                    }
                }
                break;                              //stop waiting
            }

            AppendTextToConsoleNL("✅ Device prêt.");
        }

        private void btn_SelectFocusStackImg_Click(object sender, EventArgs e)
        {
            MakeFocusStack();
        }

        private void btn_goToFSFolder_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(focusStackOutputPath))
            {
                string folderPath = Path.GetDirectoryName(focusStackOutputPath);
                if (Directory.Exists(folderPath))
                {
                    Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    MessageBox.Show("Le dossier de destination n'existe pas.");
                }
            }
            else
            {
                MessageBox.Show("Aucun chemin de sortie défini.");
            }
        }

        private void picBox_FocusStackedImage_Click(object sender, EventArgs e)
        {
            AppendTextToConsoleNL("stackedImageInBuffer = " + stackedImageInBuffer);
            if (stackedImageInBuffer == false)
            {
                if (File.Exists(focusStackOutputPath))
                {
                   
                    ImageViewerForm viewer = new ImageViewerForm(focusStackOutputPath);

                    viewer.Show();
                    stackedImageInBuffer = false;
                }
                else
                {
                    MessageBox.Show("Image introuvable : " + focusStackOutputPath + Environment.NewLine + "Il faut choisir un projet");
                }
            }
            else
            {
                
                if (File.Exists(focusStackOutputPath))
                {
                    string directory = Path.GetDirectoryName(focusStackOutputPath);
                    string filenameWithoutExt = Path.GetFileNameWithoutExtension(focusStackOutputPath);
                    string extension = Path.GetExtension(focusStackOutputPath);
                    string newFilePath = Path.Combine(directory, $"{filenameWithoutExt}_Mask{extension}");
                    ImageViewerForm viewer = new ImageViewerForm(newFilePath);
                    viewer.Show();
                    stackedImageInBuffer = false;
                }
                else
                {
                    MessageBox.Show("Image introuvable : " + focusStackOutputPath + Environment.NewLine + "Il faut choisir un projet");
                }
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //AppendTextToConsoleNL("Selected index = " + tabControl1.SelectedIndex.ToString());
            if (tabControl1.SelectedIndex == 3) Task.Run(async () => await getTurntablePosFromWaveshare());
        }

        private void btn_reculeTTdeg_Click(object sender, EventArgs e)
        {
            int incr = (int)(4096 / turntableIncrement);
            if (trkBar_turntable.Value - incr < trkBar_turntable.Minimum)
                TurnTableRotation(trkBar_turntable.Minimum);
            else
                TurnTableRotation(trkBar_turntable.Value -= incr);

            lbl_turntablePosition.Text = trkBar_turntable.Value.ToString() + " / 4096";
            lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
            turntablePosition = trkBar_turntable.Value;
        }
        private void btn_avanveTTdeg_Click(object sender, EventArgs e)
        {
            int incr = (int)(4096 / turntableIncrement);
            if (trkBar_turntable.Value + incr > trkBar_turntable.Maximum)
                TurnTableRotation(trkBar_turntable.Maximum);
            else
                TurnTableRotation(trkBar_turntable.Value += incr);

            lbl_turntablePosition.Text = trkBar_turntable.Value.ToString() + " / 4096";
            lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
            turntablePosition = trkBar_turntable.Value;
        }

        private void trackBar_ttIncrements_ValueChanged(object sender, EventArgs e)
        {
            turntableIncrement = trackBar_ttIncrements.Value;
            lbl_ttIterations.Text = turntableIncrement.ToString() + " itérations";
            lbl_turntableIterationDeg.Text = ((int)360 / turntableIncrement).ToString() + " degrés";
        }

        private void btn_PostFocusStackMask_Click(object sender, EventArgs e)
        {
            if (File.Exists(focusStackOutputPath))
            {
                using (var originalBitmap = new Bitmap(focusStackOutputPath))
                {
                    Bitmap finalBitmap;

                    var sourceImage = originalBitmap.ToImage<Emgu.CV.Structure.Bgr, byte>();
                    var maskGray = maskBitmapLive.ToImage<Emgu.CV.Structure.Gray, byte>();

                    var resizedMask = maskGray.Resize(sourceImage.Width, sourceImage.Height, Emgu.CV.CvEnum.Inter.Linear);
                    var invertedMask = resizedMask.Not();
                    var maskBgr = invertedMask.Convert<Emgu.CV.Structure.Bgr, byte>();

                    sourceImage._And(maskBgr);
                    finalBitmap = sourceImage.ToBitmap();

                    // Libération
                    maskGray.Dispose();
                    resizedMask.Dispose();
                    invertedMask.Dispose();
                    maskBgr.Dispose();
                    sourceImage.Dispose();

                    // Sauvegarde de l'image finale
                    string directory = Path.GetDirectoryName(focusStackOutputPath);
                    string filenameWithoutExt = Path.GetFileNameWithoutExtension(focusStackOutputPath);
                    string extension = Path.GetExtension(focusStackOutputPath);
                    string newFilePath = Path.Combine(directory, $"{filenameWithoutExt}_Mask{extension}");

                    finalBitmap.Save(newFilePath);

                    // Affichage dans le PictureBox
                    picBox_FocusStackedImage.Image?.Dispose();
                    picBox_FocusStackedImage.Image = finalBitmap;
                    stackedImageInBuffer = true;
                    //AppendTextToConsoleNL("stackedImageInBuffer = " + stackedImageInBuffer);
                }
            }


        }


        public void btn_openStackedImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                openFileDialog.Title = "Sélectionner une image empilée";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    focusStackOutputPath = openFileDialog.FileName;

                    // Afficher l'image dans le PictureBox
                    picBox_FocusStackedImage.Image = Image.FromFile(focusStackOutputPath);
                    picBox_FocusStackedImage.SizeMode = PictureBoxSizeMode.Zoom; // Optionnel pour bien ajuster l'image
                }
            }
        }

    }
}