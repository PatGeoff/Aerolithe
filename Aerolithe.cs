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
using System.Windows.Forms.VisualStyles;
using Aerolithe.Properties;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        // THIS IP ADDRESS 192.168.2.4 //
        public readonly IPAddress stepperCameraIpAddress = IPAddress.Parse("192.168.2.11");
        public readonly int stepperCameraPort = 44455;    // Port sur lequel on envoie les messages UDP au ESP32 du stepper motor et de l'actuateur
        public readonly IPAddress turntableIpAddress = IPAddress.Parse("192.168.2.12");
        public readonly int turntablePort = 44466;  // Port sur lequel on reçoit les messages UDP au ESP32 de la table tournante
        public readonly IPAddress scissorLiftIpAddress = IPAddress.Parse("192.168.2.13");
        public readonly int scissorLiftPort = 44477;  // Lift, moteur horizontal 
        public readonly IPAddress actuatorIpAddress = IPAddress.Parse("192.168.2.15");
        public readonly int actuatorPort = 44499;
        public readonly IPAddress stepperLiftNema23IpAddress = IPAddress.Parse("192.168.2.16");
        public readonly int stepperLiftNema23Port = 44433; // Lift, moteur vertical 


        public readonly int localPort = 55544;      // Port sur lequel on reçoit les messages UDP
        private readonly int localPortOSC = 55545;


        public bool stackedImageInBuffer = false;
        private bool isDragging = false;
        private Point startPoint;
        private bool mainConsoleScrollToCaret = true;
        private bool focusStackConsoleScrollToCaret = true;
        private bool stackConsoleScrollToCaret = true;

        public bool _DebugContinue = true;

        private bool isChangingCheckState = false;
        //private string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyResources\\Models", "u2net.onnx");

        private int[] serieId = null;
        private int serieProgress = 0;


        private UdpClient udpClient;
        private UdpClient udpClientOSC;
        private TaskCompletionSource<int> _turntablePositionTcs;
        private TaskCompletionSource<double> _actuatorAngleTcs;
        private CancellationTokenSource tokenSource;


        private bool calibrationDone = true;

        public static Aerolithe Instance { get; private set; }
        //private CustomFlowLayoutPanel customFlowLayoutPanel1, customFlowLayoutPanel2, customFlowLayoutPanel3;



        public Aerolithe()
        {
            InitializeComponent();
            InitClasses();
            this.KeyDown += new KeyEventHandler(Form1_KeyDown);
            this.KeyPreview = true; // Ensure the form receives key events
            picBox_LiveView_Main.Image = Properties.Resources.camera_offline; // Mettre ça ici parce que Visual Studio fait chier 

            appSettings = appSettings.Load();
            Debug.WriteLine(appSettings.ProjectPath);
            if (!File.Exists(appSettings.ProjectPath)) appSettings.ProjectPath = "";
            appSettings.Save();
            if (!string.IsNullOrEmpty(appSettings.ProjectPath))
            {
                Debug.WriteLine("here");
                try
                {
                    projet = projet.Load(appSettings.ProjectPath);
                    txtBox_nbrImg5deg.Text = appSettings.NbrImg5Deg.ToString();
                    txtBox_nbrImg25deg.Text = appSettings.NbrImg25Deg.ToString();
                    txtBox_nbrImg45deg.Text = appSettings.NbrImg45Deg.ToString();




                    OpenProject(appSettings.ProjectPath);
                    if (!string.IsNullOrWhiteSpace(projet.ImageFolderPath))
                    {
                        lbl_ImgFullPath.Text = projet.ImageFolderPath + "\\";
                    }


                    if (!string.IsNullOrWhiteSpace(projet.ImageNameBase))
                    {
                        AssembleImageName();
                    }


                    if (!string.IsNullOrWhiteSpace(projet.GetFocusStackPath()))
                    {
                        lbl_StackedPath.Text = projet.GetFocusStackPath();
                    }


                }
                catch (Exception e)
                {
                    Debug.WriteLine("Erreur:" + e);
                }

            }
            CamSetup();
            ToolTipsSetup();
            // ButtonSetup();
            InitializeUdpClient();
            Task.Run(() => listenUDP());
            SetupPen();
            SetTooltips();
            getActuatorAngleFromEsp32();
            getTurntablePosFromWaveshare();
            SetVariables();

            //tabControl1.SelectedTab = tabPage3; tabControl4.SelectedTab = tabControl4.TabPages[2];
            UdpSendLiftStepperNema23MessageAsync("stepmotor readData");
            Instance = this; // Définit l'instance globale pour la classe FocusStackReportControl
        }


        private void SetTooltips()
        {
            System.Windows.Forms.ToolTip toolTipMask = new System.Windows.Forms.ToolTip();
            System.Windows.Forms.ToolTip toolTipCutoff = new System.Windows.Forms.ToolTip();
            toolTipCutoff.SetToolTip(btn_displayLineBlack, "Afficher le cutoff");

        }



        #region PROCÉDURE TAB

        private void btnAutofocus_Click(object sender, EventArgs e)
        {
            nikonDoFocus();
        }


        private void btn_imageFond_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedIndex = 6;
            tabControl2.SelectedIndex = 4;
        }



        #endregion

        #region CAMÉRA TAB



        private async void btn_takePicture_Click(object sender, EventArgs e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            await takePictureAsync();  // attend que imageReadyTcs soit résolu   
            sw.Stop();
            string tempsMs = sw.Elapsed.TotalSeconds.ToString("F2");
            //await PhotoSuccess(projet.ImageNameFull, turntablePosition, true, tempsMs);
        }



        #endregion

        #region LINÉAIRE TAB
        private void btn_setStepperZeroPosition(object sender, EventArgs e)
        {
            UdpSendCameraLinearMessageAsync("stepmotor setZero");
        }

        private void btn_setStepperMaxPosition(object sender, EventArgs e)
        {
            UdpSendCameraLinearMessageAsync("stepmotor setMaxPos");
        }

        private void stepperMotor_trkbar_MouseUp(object sender, MouseEventArgs e)
        {
            if (calibrationDone)
            {
                int position = stepperCameraMotor_trkbar.Value;
                int speed = 4000;
                lbl_position.Text = position.ToString();
                udpSendCameraLinearMotorData(speed, position);  // Speed, acceleration, position
            }

        }
        private void stepperCalibration_btn_Click(object sender, EventArgs e)
        {
            AppendTextToConsoleNL("Calibrating" + Environment.NewLine);
            UdpSendCameraLinearMessageAsync("stepmotor calibration");
            stepperCameraMotor_trkbar.Enabled = true;
            stepperCameraMotor_trkbar.Value = 30000;
            UdpSendCameraLinearMessageAsync("stepmotor calibration");
            calibrationDone = true;

        }
        private void btn_stepperGetPosition_Click(object sender, EventArgs e)
        {
            UdpSendCameraLinearMessageAsync("stepmotor getstepperposition");
        }
        private void btn_StopLinearMotor_Click(object sender, EventArgs e)
        {
            udpSendCameraLinearMotorData(0);
        }

        public void encoderRotationStepper(int speed)
        {
            if (calibrationDone)
            {

                int position = 0;
                int newSpeed = speed * 400;


                // Use Invoke to safely access the stepperMotor_trkbar.Value
                stepperCameraMotor_trkbar.Invoke(new Action(() =>
                {
                    position = stepperCameraMotor_trkbar.Value;
                }));

                //AppendTextToConsoleNL(position.ToString());
                //AppendTextToConsoleNL($"sending {speed} (newSpeed: {newSpeed}) to stepper trackbar, Current position: {position}");

                udpSendCameraLinearMotorData(newSpeed);
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
            ttTargetPosition = turntablePosition;
        }
        private void trkBar_turntable_ValueChanged(object sender, EventArgs e)
        {
            lbl_turntablePosition.Text = trkBar_turntable.Value.ToString() + " / 4096";
            lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
            lbl_ttCurrentPos.Text = "Table Tournante: " + turntablePosition.ToString() + " / " + ttTargetPosition.ToString();
        }
        private async Task getTurntablePosFromWaveshare()  // Demande la position et attend une réponse du waveshare avant de continuer. 
        {
            AppendTextToConsoleNL("- getTurntablePosFromWaveshare");
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
                        lbl_ttCurrentPos.Text = "Table Tournante: " + turntablePosition.ToString() + " / " + ttTargetPosition.ToString();
                    }));
                }
                else
                {
                    trkBar_turntable.Value = turntablePosition;
                    lbl_turntablePosition.Text = turntablePosition.ToString() + "/ 4096";
                    lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)).ToString() + " degrés";
                    lbl_ttCurrentPos.Text = "Table Tournante: " + turntablePosition.ToString() + " / " + ttTargetPosition.ToString();
                }
            }
            catch (Exception e)
            {

                AppendTextToConsoleNL(e.Message);
            }

        }

        //private async Task getTurntablePosFromWaveshare()
        //{
        //    int timeoutMs = 2000;
        //    AppendTextToConsoleNL("- getTurntablePosFromWaveshare");
        //    try
        //    {
        //        _turntablePositionTcs = new TaskCompletionSource<int>();

        //        // Envoie la requête pour obtenir la position
        //        await UdpSendTurnTableMessageAsync("Aerolithe_Asks_GetPosition");

        //        await Task.Delay(500);
        //        // Crée une tâche de timeout
        //        var timeoutTask = Task.Delay(timeoutMs);
        //        var completedTask = await Task.WhenAny(_turntablePositionTcs.Task, timeoutTask);

        //        if (completedTask == timeoutTask)
        //        {
        //            AppendTextToConsoleNL("⚠ Timeout : pas de réponse Waveshare après " + timeoutMs + " ms");
        //            return; // Sort sans bloquer
        //        }

        //        // Si la réponse est reçue avant le timeout
        //        turntablePosition = await _turntablePositionTcs.Task;

        //        // Mise à jour UI thread-safe
        //        if (trkBar_turntable.InvokeRequired)
        //        {
        //            trkBar_turntable.Invoke(new Action(() =>
        //            {
        //                trkBar_turntable.Value = turntablePosition;
        //                lbl_turntablePosition.Text = turntablePosition + "/ 4096";
        //                lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)) + " degrés";
        //                lbl_ttCurrentPos.Text = $"Table Tournante: {turntablePosition} / {ttTargetPosition}";
        //            }));
        //        }
        //        else
        //        {
        //            trkBar_turntable.Value = turntablePosition;
        //            lbl_turntablePosition.Text = turntablePosition + "/ 4096";
        //            lbl_turntablePositionDeg.Text = ((int)(trkBar_turntable.Value / 4096.0 * 360)) + " degrés";
        //            lbl_ttCurrentPos.Text = $"Table Tournante: {turntablePosition} / {ttTargetPosition}";
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        AppendTextToConsoleNL("Erreur getTurntablePosFromWaveshare : " + e.Message);
        //    }
        //}




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
            UdpSendCameraLinearMessageAsync("lift setZero");
            trkBar_LiftVertical.Value = 500;
        }
        private void btn_liftMaxUp_Click(object sender, EventArgs e)
        {

        }

        private void trkBar_Lift_MouseUp(object sender, MouseEventArgs e)
        {
            int val = trkBar_LiftVertical.Value;
            UdpSendScissorLiftMessageAsync("lift position " + val.ToString());

        }

        private void btn_printLiftPositionConsole_Click(object sender, EventArgs e)
        {
            UdpSendLiftStepperNema23MessageAsync("stepmotor readData");
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

        public async Task<bool> WaitForActuator(double target)
        {
            double delta = 3;
            int timeoutMs = 10000;
            DateTime startTime = DateTime.Now;

            while (!_stopRequested && (DateTime.Now - startTime).TotalMilliseconds <= timeoutMs)
            {
                // Vérifie si on est dans la plage cible
                if (Math.Abs(actuatorAngle - target) <= delta)
                {
                    AppendTextToConsoleNL($"Actuateur dans la plage : {actuatorAngle} (cible {target})");
                    return true;
                }

                await Task.Delay(500); // Aligné avec la fréquence d'update
            }

            // Si on sort de la boucle, soit timeout, soit stop demandé
            if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
            {
                AppendTextToConsoleNL("Timeout atteint, actuateur non stabilisé.");
            }
            else if (_stopRequested)
            {
                AppendTextToConsoleNL("Arrêt demandé par l'utilisateur.");
            }

            return false;
        }




        #endregion

        #region MAIN FORM
        private void btn_clearConsole_Click(object sender, EventArgs e)
        {
            txtBox_Console.Clear();
        }




        public async Task AppendTextToFFMPEGConsoleNL(string message) // New Line
        {

            System.Windows.Forms.RichTextBox textbox = txtBox_FFMPEGConsole;

            string timestamp = $"{DateTime.Now:HH:mm:ss:ff} - ";

            if (textbox.InvokeRequired)
            {
                //Debug.WriteLine("Invoke required");
                textbox.Invoke(new Action(() =>
                {
                    AppendFormattedText(timestamp, Color.Gray, textbox);
                    AppendFormattedText(message + Environment.NewLine, txtBox_Console.ForeColor, textbox);
                }));
            }
            else
            {
                AppendFormattedText(timestamp, Color.Gray, textbox);
                AppendFormattedText(message + Environment.NewLine, txtBox_Console.ForeColor, textbox);
            }
        }

        public async Task AppendTextToConsoleNL(string message) // New Line
        {

            System.Windows.Forms.RichTextBox textbox = txtBox_Console;

            string timestamp = $"{DateTime.Now:HH:mm:ss:ff} - ";

            if (textbox.InvokeRequired)
            {
                //Debug.WriteLine("Invoke required");
                textbox.Invoke(new Action(() =>
                {
                    AppendFormattedText(timestamp, Color.Gray, textbox);
                    AppendFormattedText(message + Environment.NewLine, txtBox_Console.ForeColor, textbox);
                }));
            }
            else
            {
                AppendFormattedText(timestamp, Color.Gray, textbox);
                AppendFormattedText(message + Environment.NewLine, txtBox_Console.ForeColor, textbox);
            }
        }


        private async Task AppendFormattedText(string text, Color color, System.Windows.Forms.RichTextBox textbox)
        {


            if (textbox.InvokeRequired)
            {
                textbox.Invoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    AppendFormattedTextInternal(text, color, textbox);
                });
            }
            else
            {
                AppendFormattedTextInternal(text, color, textbox);
            }

            await ManageRichTextBoxContent(textbox);

        }



        private void AppendFormattedTextInternal(string text, Color color, System.Windows.Forms.RichTextBox textbox)
        {
            textbox.SelectionStart = textbox.Text.Length;
            if (mainConsoleScrollToCaret) textbox.ScrollToCaret();
            textbox.Select(); // Active le caret sans voler le focus
            textbox.SelectionLength = 0;

            textbox.SelectionColor = color;
            textbox.AppendText(text);
            textbox.SelectionColor = txtBox_Console.ForeColor;
            ManageRichTextBoxContent(textbox);
            textbox.Refresh();
        }

        private async Task ManageRichTextBoxContent(System.Windows.Forms.RichTextBox textbox)
        {
            int maxLength = 2147483647; // Maximum length for RichTextBox
            int threshold = (int)(maxLength * 0.5); // 50% of the maximum length

            if (textbox.Text.Length > threshold)
            {
                // Find the position to start removing text
                int removeLength = textbox.Text.Length - threshold;

                // Remove the oldest lines
                textbox.Text = textbox.Text.Substring(removeLength);
            }
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


        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {

            if ((e.Control || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) && e.KeyCode == Keys.S)
            {

                SavePrefsSettings();

            }
        }



        private void OpenExplorerAtProjectPath(string dest)
        {
            if (!string.IsNullOrEmpty(dest))
            {
                // Si dest est un fichier, on prend son dossier
                if (File.Exists(dest))
                {
                    dest = Path.GetDirectoryName(dest);
                }

                // Ouvrir directement le dossier
                Process.Start("explorer.exe", $"\"{dest}\"");
            }
            else
            {
                MessageBox.Show("Project path is not set.");
            }
        }

        #endregion

        #region PREFERENCES


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

        private void btn_PrisePhotoSeqTotaleMain_Click(object sender, EventArgs e)
        {
            if (projet.FocusSerieIncrement != 0 || projet.RotationSerieIncrement != 0)
            {
                DialogResult result = MessageBox.Show(
                    "Voulez-vous commencer à partir de zéro ?",
                    "Confirmation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    ResetSerieIncrement();
                    DeleteAllPicturesInFolderWith();
                }
            }
            ResetSequenceCancellation();
            Task.Run(async () =>
            {
                tokenSource = new CancellationTokenSource();
                await SequencePrisePhotoTotale(tokenSource.Token);
            });
        }

        private void btn_cancelPhotoShootMain_Click(object sender, EventArgs e)
        {
            StopSequences();
        }


        private void btn_PrisePhotoSeqTotale_Click(object sender, EventArgs e)
        {
            if (projet.FocusSerieIncrement != 0 || projet.RotationSerieIncrement != 0)
            {
                DialogResult result = MessageBox.Show(
                    "Overwriter toutes les images existantes?",
                    "Confirmation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    ResetSerieIncrement();
                    DeleteAllPicturesInFolderWith();
                }
            }
            ResetSequenceCancellation();
            Task.Run(async () =>
            {
                tokenSource = new CancellationTokenSource();
                await SequencePrisePhotoTotale(tokenSource.Token);
            });
        }


        private void btn_prisePhotoSeq1_Click(object sender, EventArgs e)
        {
            // Afficher une boîte de dialogue pour confirmer
            var result = MessageBox.Show(
                "Voulez-vous vraiment reprendre la série à 5° commençant l'incrémentation à " + txtBox_seqPad1.Text + "?",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                return; // Si l'utilisateur refuse, on sort de la méthode
            }

            // Si l'utilisateur accepte, on continue
            projet.RotationSerieIncrement = int.Parse(txtBox_seqPad1.Text);
            AssembleImageName();

            ResetSequenceCancellation();
            currentSequence = 0;


            Task.Run(async () =>
            {
                await UdpSendActuatorMessageAsync("actuator 5");
                if (_stopRequested) return;
                await WaitForActuator(5);
                tokenSource = new CancellationTokenSource();
                await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            });
        }

        private void btn_prisePhotoSeq2_Click(object sender, EventArgs e)
        {
            // Afficher une boîte de dialogue pour confirmer
            var result = MessageBox.Show(
                "Voulez-vous vraiment reprendre la série à 25° commençant l'incrémentation à " + txtBox_seqPad2.Text + "?",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                return; // Si l'utilisateur refuse, on sort de la méthode
            }
            projet.RotationSerieIncrement = int.Parse(txtBox_seqPad2.Text);
            AssembleImageName();

            ResetSequenceCancellation();
            currentSequence = 1;

            Task.Run(async () =>
            {
                await UdpSendActuatorMessageAsync("actuator 25");
                if (_stopRequested) return;
                await WaitForActuator(25);
                tokenSource = new CancellationTokenSource();
                await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            });

        }

        private void btn_prisePhotoSeq3_Click(object sender, EventArgs e)
        {
            // Afficher une boîte de dialogue pour confirmer
            var result = MessageBox.Show(
                "Voulez-vous vraiment reprendre la série à 45° commençant l'incrémentation à " + txtBox_seqPad3.Text + "?",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                return; // Si l'utilisateur refuse, on sort de la méthode
            }

            projet.RotationSerieIncrement = int.Parse(txtBox_seqPad3.Text);
            AssembleImageName();


            ResetSequenceCancellation();
            currentSequence = 2;
            Task.Run(async () =>
            {
                await UdpSendActuatorMessageAsync("actuator 45");
                if (_stopRequested) return;
                await WaitForActuator(45);
                tokenSource = new CancellationTokenSource();
                await PrisePhotoSequenceAsync(tokenSource.Token, currentSequence);
            });
        }



        private void QueryProject()
        {
            if (appSettings.ProjectPath == null)
            {
                SavePrefsSettings();  // Demande à setter le projet
            }
            if (appSettings.ProjectPath == null)
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

        private void btn_stopAutomaticFocusCapture_Click(object sender, EventArgs e)
        {
            StopSequences();

        }
        private void btn_cancelPhotoShoot_Click(object sender, EventArgs e)
        {
            StopSequences();
        }

        private async void StopSequences()
        {
            _stopRequested = true;
            if (btn_cancelPhotoShoot.InvokeRequired)
            {
                btn_cancelPhotoShoot.Invoke(new Action(() =>
                {
                    btn_stopAutomaticFocusCapture.BackColor = Color.FromArgb(30, 30, 30);
                    btn_cancelPhotoShoot.BackColor = Color.FromArgb(30, 30, 30);
                    btn_cancelPhotoShootMain.BackColor = Color.FromArgb(30, 30, 30);
                    // lbl_CancelStatus.Text = "Cancel? OUI";
                }));
            }
            else
            {
                btn_stopAutomaticFocusCapture.BackColor = Color.FromArgb(30, 30, 30);
                btn_cancelPhotoShoot.BackColor = Color.FromArgb(30, 30, 30);
                btn_cancelPhotoShootMain.BackColor = Color.FromArgb(30, 30, 30);
                // lbl_CancelStatus.Text = "Cancel? OUI";
            }

            await Task.Delay(5000);

            ResetSequenceCancellation();

        }

        private void ResetSequenceCancellation()
        {
            _stopRequested = false;
            if (btn_stopAutomaticFocusCapture.InvokeRequired)
            {
                btn_stopAutomaticFocusCapture.Invoke(new Action(() =>
                {
                    btn_cancelPhotoShoot.BackColor = System.Drawing.Color.FromArgb(100, 80, 30, 30);
                    btn_stopAutomaticFocusCapture.BackColor = System.Drawing.Color.FromArgb(100, 80, 30, 30);
                    btn_cancelPhotoShootMain.BackColor = Color.FromArgb(30, 30, 30);
                }));
            }
            else
            {
                btn_cancelPhotoShoot.BackColor = System.Drawing.Color.FromArgb(100, 80, 30, 30);
                btn_stopAutomaticFocusCapture.BackColor = System.Drawing.Color.FromArgb(100, 30, 80, 30);
                btn_cancelPhotoShootMain.BackColor = Color.FromArgb(30, 30, 30);
            }
        }

        private void btn_focusMinus_Click(object sender, EventArgs e)
        {
            try
            {
                ManualFocus(1, stepSize);
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
                ManualFocus(0, stepSize);
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




        private void btn_AutomaticMFocus_Click(object sender, EventArgs e)
        {
            AutomaticFocusRoutine();
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
            DeleteAllPicturesInFolderWithPrompt();
        }

        private void btn_clearPicReport_Click(object sender, EventArgs e)
        {
            //richTextBox_PicReport.Clear();
            flowPanelReports.Controls.Clear();
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

                        foreach (Control inner in panel.Controls.OfType<TableLayoutPanel>())
                        {
                            foreach (Control item in inner.Controls)
                            {
                                if (item is Label label)
                                {
                                    float newFontSize = Math.Max(6, newSize.Height / 20f); // ajustable
                                    label.Font = new Font(label.Font.FontFamily, newFontSize);
                                }

                            }
                        }
                    });
                });
            }

            await Task.CompletedTask;
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




        private void tableLayoutPanel41_Paint(object sender, PaintEventArgs e)
        {

        }

        private void picBox_pictureTaken_DoubleClick(object sender, EventArgs e)
        {
            if (picBox_pictureTaken.Image != null && projet.ImageFolderPath != null && projet.ImageNameBase != null)
            {

                string imagePath = Path.Combine(projet.ImageFolderPath, projet.GetImageNameFull());

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
                MessageBox.Show($"le dossier de l'image ({projet.ImageFolderPath}) et le nom de l'image ({projet.GetImageNameFull()}) ne sont pas bons");
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
                    Image = System.Drawing.Image.FromFile(imagePath),
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

            OpenExplorerAtProjectPath(projet.ImageFolderPath);

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
            //AppendTextToConsoleNL("stackedImageInBuffer = " + stackedImageInBuffer);
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
            ReculeTableTournanteDeg();
        }
        private void btn_avanveTTdeg_Click(object sender, EventArgs e)
        {
            AvanceTableTournateDeg();
        }

        private void trackBar_ttIncrements_ValueChanged(object sender, EventArgs e)
        {
            turntableIncrement = trackBar_ttIncrements.Value;
            lbl_ttIterations.Text = turntableIncrement.ToString() + " itérations";
            lbl_turntableIterationDeg.Text = ((int)360 / turntableIncrement).ToString() + " degrés";
        }

        private void btn_PostFocusStackMask_Click(object sender, EventArgs e)
        {
            PostFocusStackMask();
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
                    picBox_FocusStackedImage.Image = System.Drawing.Image.FromFile(focusStackOutputPath);
                    picBox_FocusStackedImage.SizeMode = PictureBoxSizeMode.Zoom; // Optionnel pour bien ajuster l'image
                }
            }
        }

        private void btn_priseAutoPourFS_Click(object sender, EventArgs e)
        {
            focusStackStepVar = 0;
            lbl_focusStepsVar.Text = focusStackStepVar.ToString();
            if (int.TryParse(textBox_nbrFocusSteps.Text, out int LocalIterations))
            {

                AutomaticFocusThenCapture(LocalIterations);
            }
        }

        private void btn_incrImgSeq_Click(object sender, EventArgs e)
        {
            Task.Run(() => IncrementImgSeq());
        }

        private void btn_decrImgSeq_Click(object sender, EventArgs e)
        {
            Task.Run(() => DecrementImgSeq());
        }

        private void btn_ResetIncr_Click(object sender, EventArgs e)
        {
            ResetSerieIncrement();

        }

        private void btn_setStackedFolderPath_Click(object sender, EventArgs e)
        {
            SetFocusStackFolder();
        }


        private void btn_nextAutoFocustackCapture_Click(object sender, EventArgs e)
        {
            tabControl4.SelectedTab = tabPage16;
            IncrementImgSeq();
            AvanceTableTournateDeg();
        }

        private void btn_GotoStackedFolder_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(projet.GetFocusStackPath()))
            {
                System.Diagnostics.Process.Start("explorer.exe", projet.GetFocusStackPath());
            }
        }




        private void txtBox_nbrImg5deg_TextChanged(object sender, EventArgs e)
        {
            txtBox_nbrImg5deg.ForeColor = Color.Gray;
        }
        private void txtBox_nbrImg5deg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(txtBox_nbrImg5deg.Text, out int valeur))
                {
                    txtBox_nbrImg5deg.ForeColor = Color.White;
                    lbl_Serie5Angle.Text = (4096 / valeur).ToString() + " / " + (360 / valeur).ToString();
                    appSettings.NbrImg5Deg = valeur;
                    appSettings.Save();
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
                UpdateSequencePadding();
            }
        }

        private void txtBox_nbrImg25deg_TextChanged(object sender, EventArgs e)
        {
            txtBox_nbrImg25deg.ForeColor = Color.Gray;
        }


        private void txtBox_nbrImg25deg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(txtBox_nbrImg25deg.Text, out int valeur))
                {
                    txtBox_nbrImg25deg.ForeColor = Color.White;
                    lbl_Serie25Angle.Text = (4096 / valeur).ToString() + " / " + (360 / valeur).ToString();
                    appSettings.NbrImg25Deg = valeur;
                    appSettings.Save();
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
                UpdateSequencePadding();
            }
        }
        private void txtBox_nbrImg45deg_TextChanged(object sender, EventArgs e)
        {
            txtBox_nbrImg45deg.ForeColor = Color.Gray;
        }
        private void txtBox_nbrImg45deg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(txtBox_nbrImg45deg.Text, out int valeur))
                {
                    txtBox_nbrImg45deg.ForeColor = Color.White;
                    lbl_Serie45Angle.Text = (4096 / valeur).ToString() + " / " + (360 / valeur).ToString();
                    appSettings.NbrImg45Deg = valeur;
                    appSettings.Save();
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
                UpdateSequencePadding();
            }
        }

        private void txtBox_nomImages_KeyDown(object sender, KeyEventArgs e)
        {
            // Empêche le son 'ding'
            e.SuppressKeyPress = true;
        }

        private void txtBox_seqPad1_TextChanged(object sender, EventArgs e)
        {
            txtBox_seqPad1.ForeColor = Color.Gray;
        }

        private void txtBox_seqPad1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(txtBox_seqPad1.Text, out int valeur))
                {
                    txtBox_seqPad1.ForeColor = Color.White;
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }

        private void txtBox_seqPad2_TextChanged(object sender, EventArgs e)
        {
            txtBox_seqPad2.ForeColor = Color.Gray;
        }

        private void txtBox_seqPad2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(txtBox_seqPad2.Text, out int valeur))
                {
                    txtBox_seqPad2.ForeColor = Color.White;
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }

        private void txtBox_seqPad3_TextChanged(object sender, EventArgs e)
        {
            txtBox_seqPad3.ForeColor = Color.Gray;
        }

        private void txtBox_seqPad3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(txtBox_seqPad3.Text, out int valeur))
                {
                    txtBox_seqPad3.ForeColor = Color.White;
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }



        private void txtBox_DriveStep_TextChanged(object sender, EventArgs e)
        {
            txtBox_DriveStep.ForeColor = Color.Gray;
        }

        private void txtBox_DriveStep_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(txtBox_DriveStep.Text, out int value))
                {
                    hScrollBar_driveStep.Value = value;
                    txtBox_DriveStep.ForeColor = Color.White;
                    stepSize = value;
                    projet.StepSize = stepSize;
                    projet.Save(appSettings.ProjectPath);
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }

        private void trackBar_blobCount_Scroll(object sender, EventArgs e)
        {
            lbl_BlockAmountBlurDetet.Text = (trackBar_blobCount.Value * 16).ToString();
        }

        private void trackBar_blurThreshold_Scroll(object sender, EventArgs e)
        {
            lbl_ResBlurDetect.Text = trackBar_blurThreshold.Value.ToString();
        }

        private void textBox_minDetect_TextChanged(object sender, EventArgs e)
        {
            textBox_minDetect.ForeColor = Color.Gray;
        }

        private void textBox_minDetect_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(textBox_minDetect.Text, out int value))
                {
                    textBox_minDetect.ForeColor = Color.White;
                    minDetect = value;
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }

        private void textBox_FocusFreqSpeed_TextChanged(object sender, EventArgs e)
        {
            textBox_FocusFreqSpeed.ForeColor = Color.Gray;
        }

        private void textBox_FocusFreqSpeed_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(textBox_FocusFreqSpeed.Text, out int value))
                {
                    textBox_FocusFreqSpeed.ForeColor = Color.White;
                    delayTime = value;
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }

        private void textBox_FocusIterations_TextChanged(object sender, EventArgs e)
        {
            textBox_FocusIterations.ForeColor = Color.Gray;
        }

        private void textBox_FocusIterations_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(textBox_FocusIterations.Text, out int value))
                {
                    textBox_FocusIterations.ForeColor = Color.White;
                    iterations = value;
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }

        private void btn_tempButtonTest_Click(object sender, EventArgs e)
        {
            PreparationDossierDestTemp();
        }

        private void checkBox_StackAuto_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_StackAuto.Checked)
            {
                checkBox_SeqFocusStack.Checked = true;
            }
            else checkBox_SeqFocusStack.Checked = false;
        }

        private void checkBox_SeqFocusStack_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_SeqFocusStack.Checked)
            {
                checkBox_StackAuto.Checked = true;
            }
            else checkBox_StackAuto.Checked = false;
        }

        private void btn_clearFFMPEGConsole_Click(object sender, EventArgs e)
        {
            txtBox_FFMPEGConsole.Clear();
        }

        private void btn_DebugContinue_Click(object sender, EventArgs e)
        {
            _DebugContinue = true;
        }

        private async Task WaitForDebugContinue()
        {
            AppendTextToConsoleNL("APPLICATION MISE EN PAUSE. APPUYER SUR \"Debug - Continue\" pour continuer");
            while (!_DebugContinue)
            {
                await Task.Delay(100); // Attend 100 ms avant de revérifier
                Application.DoEvents(); // Permet au GUI de rester réactif
            }
        }

        private void textBox_nbrPhotosFS_TextChanged(object sender, EventArgs e)
        {
            textBox_nbrPhotosFS.ForeColor = Color.Gray;
        }

        private void textBox_nbrPhotosFS_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (int.TryParse(textBox_nbrPhotosFS.Text, out int value))
                {
                    textBox_nbrPhotosFS.ForeColor = Color.White;
                    maxNbrPicturesAllowed = value;
                    projet.MaxPicturesAllowed = maxNbrPicturesAllowed;
                    projet.Save(appSettings.ProjectPath);
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }

        private void btn_consoleScrollToCaret_Click(object sender, EventArgs e)
        {
            if (mainConsoleScrollToCaret)
            {
                mainConsoleScrollToCaret = false;
                btn_consoleScrollToCaret.FlatAppearance.BorderSize = 1;
            }

            else
            {
                mainConsoleScrollToCaret = true;
                btn_consoleScrollToCaret.FlatAppearance.BorderSize = 0;
            }
        }

        private int lastHorizontalValue = -1;
        private int lastVerticalValue = -1;

        private void trkBar_LiftHorizontal_Scroll(object sender, EventArgs e)
        {
            int currentValue = trkBar_LiftHorizontal.Value * -5;
            if (currentValue != lastHorizontalValue)
            {
                udpSendScissorData(currentValue);
                lastHorizontalValue = currentValue;
            }
        }

        private void trkBar_LiftVertical_Scroll(object sender, EventArgs e)
        {
            int currentValue = trkBar_LiftVertical.Value * 100;
            if (currentValue != lastVerticalValue)
            {
                udpSendStepperLiftNema23MotorData(currentValue);
                lastVerticalValue = currentValue;
            }
        }

        private void trkBar_LiftHorizontal_MouseUp(object sender, MouseEventArgs e)
        {
            trkBar_LiftHorizontal.Value = 0;
            udpSendScissorData(0);
        }

        private void trkBar_LiftVertical_MouseUp(object sender, MouseEventArgs e)
        {
            trkBar_LiftVertical.Value = 0;
            udpSendStepperLiftNema23MotorData(0);
            UdpSendLiftStepperNema23MessageAsync("stepmotor readData");
        }

        private void btn_VerticalLiftStep_Calibration_Click(object sender, EventArgs e)
        {
            UdpSendLiftStepperNema23MessageAsync("stepmotor calibration");
        }

        private void btn_LiftVerticalDefault_Click(object sender, EventArgs e)
        {
            UdpSendLiftStepperNema23MessageAsync("stepmotor setDefault");
        }

        private void btn_VerticalLiftGoToDefault_Click(object sender, EventArgs e)
        {
            UdpSendLiftStepperNema23MessageAsync("stepmotor moveto " + appSettings.VerticalLiftDefaultPos.ToString());
        }

        private void chkBox_CalculerCentrage_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBox_CalculerCentrage.Checked)
            {
                AppendTextToConsoleNL($"{offsets.offsetX} {offsets.offsetY}");
                calculerCentre = true;
            }
            else
            {
                chkBox_CalculerCentrage.Text = "";
                calculerCentre = false;
            }
        }

        private void btn_LiftAutoCenterRoutine_Click(object sender, EventArgs e)
        {
            calculerCentre = true;
            chkBox_CalculerCentrage.Checked = calculerCentre;

            Task.Run(async () =>
            {
                await Task.Delay(400); // délai avant la routine
                await RoutineAutoCentrage();
            });
        }

        private void btn_CancelAutoCentrage_Click(object sender, EventArgs e)
        {
            cancelAutoCentrage = true;

        }

        private void stepperCameraMotor_trkbar_Scroll(object sender, EventArgs e)
        {
            udpSendCameraLinearMotorData(stepperCameraMotor_trkbar.Value * 100);
        }

        private void stepperCameraMotor_trkbar_MouseUp(object sender, MouseEventArgs e)
        {
            stepperCameraMotor_trkbar.Value = 0;
            udpSendCameraLinearMotorData(0);
        }

        private void allerAuDossierToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenExplorerAtProjectPath(appSettings.ProjectPath);

        }

        private void choisirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetImageFolder();
        }

        private void ouvrirToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            OpenExplorerAtProjectPath(projet.ImageFolderPath);
        }

        private void choisirUnDossierToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetFocusStackFolder();
        }

        private void ouvrirLeDossierToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenExplorerAtProjectPath(projet.FocusStackFolderName);
        }


        private void modifierLeNomDesImagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Entrez le nouveau nom de l'image :",
                "Modifier le nom",
                projet.ImageNameBase // valeur par défaut
            );

            if (!string.IsNullOrWhiteSpace(input))
            {
                projet.ImageNameBase = input;
                projet.Save(appSettings.ProjectPath);
                AssembleImageName();
            }
        }

        private void btn_coteA_Click(object sender, EventArgs e)
        {
            ToggleCote(0);
            projet.Cote = 0;
            projet.Save(appSettings.ProjectPath);
            AssembleImageName();
        }

        private void btn_coteB_Click(object sender, EventArgs e)
        {
            ToggleCote(1);
            projet.Cote = 1;
            projet.Save(appSettings.ProjectPath);
            AssembleImageName();
        }


        private void ToggleCote(int newCote)
        {
            btn_coteA.BackColor = (newCote == 0) ? Color.FromArgb(0, 120, 0) : Color.FromArgb(30, 30, 30);
            btn_coteB.BackColor = (newCote == 1) ? Color.FromArgb(0, 120, 0) : Color.FromArgb(30, 30, 30);
        }



        private void effacerToutesLesImagesEtFocusStackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                string folderPath = projet.ImageFolderPath;

                if (Directory.Exists(folderPath))
                {
                    // Afficher une boîte de confirmation
                    DialogResult result = MessageBox.Show(
                        "Voulez-vous vraiment effacer toutes les images et sous-dossiers ?",
                        "Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);

                    if (result == DialogResult.Yes)
                    {
                        // Supprimer le dossier et son contenu
                        Directory.Delete(folderPath, true);

                        // Recréer le dossier vide
                        CreateAllFolders(Path.GetDirectoryName(appSettings.ProjectPath));

                        Debug.WriteLine("Toutes les images ont été effacées.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        Debug.WriteLine("Opération annulée.", "Annulation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    Debug.WriteLine("Le dossier spécifié n'existe pas.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erreur lors de la suppression : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

      
    }
}