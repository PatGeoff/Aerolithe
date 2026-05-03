//Aerolithe.cs

using Aerolithe.Properties;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.XPhoto;
using Microsoft.VisualBasic;
using Nikon;
using ScottPlot.Statistics;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using static Emgu.CV.DISOpticalFlow;
using static Emgu.Util.Platform;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public const string UiRevision = "REV-0027-nonblocking-focus-mask";
        private string _windowTitleBase = "Aucun projet";

        // THIS IP ADDRESS 192.168.2.4 //

        // Champs d’instance (readonly) : on les initialise dans le constructeur
        public readonly IPAddress stepperCameraIpAddress;
        public readonly int stepperCameraPort = 44455;

        public readonly IPAddress turntableIpAddress;
        public readonly int turntablePort = 44466;

        public readonly IPAddress scissorLiftIpAddress;
        public readonly int scissorLiftPort = 44477;

        public readonly IPAddress actuatorIpAddress;
        public readonly int actuatorPort = 44499;

        public readonly IPAddress liftVerticalIpAddress;
        public readonly int liftVerticalPort = 44433;

        public readonly int localPort = 55544;
        private readonly int localPortOSC = 55545;

        private readonly (string Name, IPAddress Address)[] devices;

        private Dictionary<string, Label> _labelMap;
        private CancellationTokenSource _autoPingCts;
        private volatile bool _shutdownStarted;
        private PrivateFontCollection? _bundledPhosphorFonts;
        private readonly object _sequencePauseLock = new();
        private bool _sequencePaused;
        private TaskCompletionSource<bool> _sequenceResumeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static readonly Color SequenceActionButtonBackColor = Color.FromArgb(35, 35, 35);
        private static readonly Color SequencePausedButtonBackColor = Color.FromArgb(110, 70, 20);
        private readonly Dictionary<System.Windows.Forms.Button, Color> _sequencePauseButtonBackColors = new();
        private TableLayoutPanel? _volumeSequenceActionsPanel;
        private TableLayoutPanel? _totalSequenceActionsPanel;
        private System.Windows.Forms.Button? _volumePauseResumeButton;
        private System.Windows.Forms.Button? _totalPauseResumeButton;
        private bool _isInitializingMaskShrinkSettings;


        public bool stackedImageInBuffer = false;

        public bool _DebugContinue = true;

        private bool isChangingCheckState = false;
        private bool _isInitializingMaskThresholds = false;
        private bool _testAutoCenterActuatorEnabled = false;
        private CancellationTokenSource? _manualActuatorAutoCenterCts;
        //private string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyResources\\Models", "u2net.onnx");

        private int[] serieId = [];


        private UdpClient udpClient;
        private UdpClient udpClientOSC;
        private TaskCompletionSource<int> _turntablePositionTcs;
        private TaskCompletionSource<int> _linearPositionTcs;
        private TaskCompletionSource<double> _actuatorAngleTcs;
        private CancellationTokenSource tokenSource;


        private bool calibrationDone = true;

        public static Aerolithe Instance { get; private set; }
        //private CustomFlowLayoutPanel customFlowLayoutPanel1, customFlowLayoutPanel2, customFlowLayoutPanel3;

        private bool StackConsoleView = false;
        private bool MainConsoleScrollToCarret = true;
        private bool StackConsoleScrollToCarret = true;

        public Timing timing = new Timing();


        // Pour auto scroll off et on 
        [DllImport("user32.dll")]
        private static extern int GetScrollPos(IntPtr hWnd, int nBar);

        [DllImport("user32.dll")]
        private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int SB_VERT = 1;
        private const int WM_VSCROLL = 0x0115;
        private const int SB_THUMBPOSITION = 4;


        public Aerolithe()
        {

            InitializeComponent();
            ApplyBundledPhosphorFontToControls();
            SetMainWindowTitle();


            stepperCameraIpAddress = IPAddress.Parse("192.168.2.11");
            turntableIpAddress = IPAddress.Parse("192.168.2.12");
            scissorLiftIpAddress = IPAddress.Parse("192.168.2.13");
            actuatorIpAddress = IPAddress.Parse("192.168.2.15");
            liftVerticalIpAddress = IPAddress.Parse("192.168.2.16");

            devices = new[]
                   {
            ("Stepper Camera",        stepperCameraIpAddress),
            ("Turntable",             turntableIpAddress),
            ("Actuator",              actuatorIpAddress),
            ("Lift Vertical",   liftVerticalIpAddress),
            ("Scissor Lift",          scissorLiftIpAddress)
            };


            _labelMap = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase)
        {
            { "Stepper Camera",       lbl_IP_stepperCamera },
            { "Turntable",            lbl_IP_turnTable },
            { "Scissor Lift",         lbl_IP_scissorLift },
            { "Actuator",             lbl_IP_Actuator },
            { "Lift Vertical",  lbl_IP_liftVertical },
        };

            if (IsRunningInDesigner())
            {
                return;
            }

            InitializeSequenceActionControls();
            StartAutoPingLoop(TimeSpan.FromSeconds(60));

            // Vérifie si on est sur le réseau WIFI Aérolithe et popup un message d'erreur sinon. À remettre à la version finale
            //this.Shown += Aerolithe_ShownAsync;


            var nikonDir = Path.Combine(AppContext.BaseDirectory, "MyResources", "NikonLibs");
            var oldPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", nikonDir + Path.PathSeparator + oldPath,
                EnvironmentVariableTarget.Process);



            InitClasses();

            this.KeyDown += new KeyEventHandler(Form1_KeyDown);
            this.KeyPreview = true; // Ensure the form receives key events
            picBox_LiveView_Main.Image = Properties.Resources.camera_offline; // Mettre ça ici parce que Visual Studio fait chier 

            try
            {
                appSettings = appSettings.Load();
                Debug.WriteLine(appSettings.ProjectPath);
                if (!File.Exists(appSettings.ProjectPath)) appSettings.ProjectPath = "";
                appSettings.Save();
            }
            catch (Exception e)
            {
                MessageBox.Show("Erreur durant appSettings.Load()\nerreur: " + e.Message);
                throw;
            }
            try
            {
                if (!string.IsNullOrEmpty(appSettings.ProjectPath))
                {
                    try
                    {
                        projet = projet.Load(appSettings.ProjectPath);
                        txtBox_nbrImg5deg.Text = appSettings.NbrImg5Deg.ToString();
                        txtBox_nbrImg25deg.Text = appSettings.NbrImg25Deg.ToString();
                        txtBox_nbrImg45deg.Text = appSettings.NbrImg45Deg.ToString();
                        txtBox_mesurements5deg.Text = projet.Mesurements5deg.ToString();
                        txtBox_mesurements25deg.Text = projet.Mesurements25deg.ToString();
                        txtBox_mesurements45deg.Text = projet.Mesurements45deg.ToString();
                        InitializeMaskShrinkSettings();
                        txtBox_seqPad1.Text = appSettings.Padding5Deg.ToString();
                        txtBox_seqPad2.Text = appSettings.Padding25Deg.ToString();
                        txtBox_seqPad3.Text = appSettings.Padding45Deg.ToString();

                        UpdateSequencePadding();

                        OpenProject(appSettings.ProjectPath);
                        if (!string.IsNullOrWhiteSpace(projet.ImageFolderPath))
                        {
                            lbl_ImgFullPath.Text = projet.ImageFolderPath + "\\";
                        }


                        if (!string.IsNullOrWhiteSpace(projet.ImageNameBase))
                        {
                            DisplayPathsInUI();
                        }


                        //if (!string.IsNullOrWhiteSpace(projet.GetFocusStackPath()))
                        //{
                        //    lbl_StackedPath.Text = projet.GetFocusStackPath();
                        //}


                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Erreur:" + e);
                    }

                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Erreur durant project.Load()\nerreur: " + e.Message);
                throw;
            }

            try
            {
                CamSetup();
            }
            catch (Exception e)
            {
                MessageBox.Show("Erreur durant CamSetup()\nerreur: " + e.Message);
                throw;
            }
            ToolTipsSetup();
            // ButtonSetup();
            try
            {
                InitializeUdpClient();
            }
            catch (Exception e)
            {
                MessageBox.Show("Erreur durant InitializeUdpClient()\nerreur: " + e.Message);
                throw;
            }

            SetupPen();
            SetTooltips();
            try
            {
                getActuatorAngleFromEsp32();
            }
            catch (Exception e)
            {
                MessageBox.Show("Erreur durant getActuatorAngleFromEsp32()\nerreur: " + e.Message);
                throw;
            }
            try
            {
                getTurntablePosFromWaveshare();
            }
            catch (Exception e)
            {
                MessageBox.Show("Erreur durant getTurntablePosFromWaveshare()\nerreur: " + e.Message);
                throw;
            }
            SetVariables();

            //tabControl1.SelectedTab = tabPage3; tabControl4.SelectedTab = tabControl4.TabPages[2];
            try
            {
                UdpSendLiftVerticalMessageAsync("stepmotor readData");
            }
            catch (Exception e)
            {
                MessageBox.Show("Erreur durant UdpSendLiftStepperNema23MessageAsync(\"stepmotor readData\")\nerreur: " + e.Message);
                throw;
            }
            Instance = this; // Définit l'instance globale pour la classe FocusStackReportControl
            TestLoadNikonDlls();

            ToggleCote(projet.Cote);

            btn_focusStack.Text = projet.FocusStackEnabled ? "" : "";
            btn_applyMask.Text = projet.ApplyMask ? "" : "";
            InitializeMaskThresholdSettings();
            InitializeMaskShrinkSettings();
            InitializeMaskAlgorithmDropdown();
            btn_saveImageForMesurementSequence.Text = projet.SaveImageForMesurements ? "" : "";
            btn_SaveImageToDisk.Text = projet.SaveImageToDisk ? "" : "";
            btn_LiveViewEnable.Text = projet.LiveViewEnabled ? "" : "";
            btn_AutoCentrageAuto.Text = projet.AutoCentrage ? "" : "";
            btn_AutoCentrageActuator.Text = projet.AutoCentrageActuator ? "" : "";
            btn_ShowSharpnessOverlay.Text = projet.ViewSharpnessOverlay ? "" : "";

            // Timer servant à calculer le temps entre takePictureAsync et device_ImageReady
            timing = new Timing();

            // Positionnement de la fenêtre au départ
            this.StartPosition = FormStartPosition.Manual;
            var screen = Screen.FromControl(this).WorkingArea;
            this.Location = new Point(
                screen.Left, // Centré en X
                screen.Top                       // Tout en haut en Y
                );
            this.MinimumSize = new Size(
                Math.Min(1800, screen.Width),
                Math.Min(1000, screen.Height));
            this.Size = new Size(
                Math.Min(4374, screen.Width),
                Math.Min(2529, screen.Height));
            this.WindowState = FormWindowState.Maximized;


        }

        private static bool IsRunningInDesigner()
        {
            var processName = Process.GetCurrentProcess().ProcessName;

            return LicenseManager.UsageMode == LicenseUsageMode.Designtime ||
                processName.Contains("devenv", StringComparison.OrdinalIgnoreCase) ||
                processName.Contains("DesignToolsServer", StringComparison.OrdinalIgnoreCase);
        }

        private void InitializeSequenceActionControls()
        {

            _volumeSequenceActionsPanel = tableLayoutPanel62;
            _totalSequenceActionsPanel = tableLayoutPanel63;

            ConfigureSequenceActionPanel(
                _volumeSequenceActionsPanel,
                "tl_volumeSequenceActions",
                out _volumePauseResumeButton,
                out var volumeCancelButton);
            ConfigureSequenceActionPanel(
                _totalSequenceActionsPanel,
                "tl_totalSequenceActions",
                out _totalPauseResumeButton,
                out var totalCancelButton);

            _volumePauseResumeButton.Click += (_, __) => ToggleSequencePause(_volumePauseResumeButton, _totalPauseResumeButton);
            _totalPauseResumeButton.Click += (_, __) => ToggleSequencePause(_volumePauseResumeButton, _totalPauseResumeButton);
            volumeCancelButton.Click += (_, __) => StopSequences();
            totalCancelButton.Click += (_, __) => StopSequences();

            SetSequenceActionControlsVisible(_volumeSequenceActionsPanel, visible: false);
            SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: false);
        }

        private void ConfigureSequenceActionPanel(TableLayoutPanel panel, string name, out System.Windows.Forms.Button pauseResumeButton, out System.Windows.Forms.Button cancelButton)
        {
            panel.Dock = DockStyle.Top;

            var buttons = panel.Controls
                .OfType<System.Windows.Forms.Button>()
                .OrderBy(button => panel.GetRow(button))
                .ThenBy(button => panel.GetColumn(button))
                .ToList();

            if (buttons.Count >= 2)
            {
                pauseResumeButton = buttons[0];
                cancelButton = buttons[1];
            }
            else
            {
                pauseResumeButton = buttons.Count > 0 ? buttons[0] : CreateSequenceActionButton("Pause");
                cancelButton = CreateSequenceActionButton("Cancellation");
                cancelButton.BackColor = Color.FromArgb(80, 30, 30);

                if (!panel.Controls.Contains(pauseResumeButton))
                {
                    panel.Controls.Add(pauseResumeButton, 0, 0);
                }

                if (!panel.Controls.Contains(cancelButton))
                {
                    panel.Controls.Add(cancelButton, 1, 0);
                }
            }

            pauseResumeButton.Text = "Pause";
            pauseResumeButton.Dock = DockStyle.Fill;
            cancelButton.Text = "Cancellation";
            cancelButton.Dock = DockStyle.Fill;
            _sequencePauseButtonBackColors[pauseResumeButton] = pauseResumeButton.BackColor;
        }

        private System.Windows.Forms.Button CreateSequenceActionButton(string text)
        {
            return new System.Windows.Forms.Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = SequenceActionButtonBackColor,
                ForeColor = Color.White,
                Font = new Font("Roboto Medium", 9F, FontStyle.Bold, GraphicsUnit.Point, 0),
                UseVisualStyleBackColor = false
            };
        }

        private void ToggleSequencePause(params System.Windows.Forms.Button?[] pauseButtons)
        {
            bool paused;
            lock (_sequencePauseLock)
            {
                if (_sequencePaused)
                {
                    _sequencePaused = false;
                    _sequenceResumeTcs.TrySetResult(true);
                }
                else
                {
                    _sequencePaused = true;
                    _sequenceResumeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                paused = _sequencePaused;
            }

            AppendTextToConsoleNL(
                paused
                    ? $"{GetActiveSequenceName()} mise en pause."
                    : $"{GetActiveSequenceName()} reprise.",
                paused ? Color.Orange : Color.LightGreen);

            foreach (var button in pauseButtons)
            {
                SetPauseButtonState(button, paused);
            }
        }

        private string GetActiveSequenceName()
        {
            if (_volumeSequenceActionsPanel?.Visible == true) return "Séquence volume";
            if (_totalSequenceActionsPanel?.Visible == true) return "Routine totale";
            return "Séquence";
        }

        private void ResumeSequenceIfPaused()
        {
            lock (_sequencePauseLock)
            {
                _sequencePaused = false;
                _sequenceResumeTcs.TrySetResult(true);
            }

            SetPauseButtonState(_volumePauseResumeButton, paused: false);
            SetPauseButtonState(_totalPauseResumeButton, paused: false);
        }

        private void SetPauseButtonState(System.Windows.Forms.Button? button, bool paused)
        {
            if (button == null) return;

            button.Text = paused ? "Reprise" : "Pause";
            button.BackColor = paused
                ? SequencePausedButtonBackColor
                : (_sequencePauseButtonBackColors.TryGetValue(button, out var backColor) ? backColor : SequenceActionButtonBackColor);
        }

        private void SetSequenceActionControlsVisible(TableLayoutPanel? panel, bool visible)
        {
            void update()
            {
                if (panel == null) return;
                if (!visible) ResumeSequenceIfPaused();
                panel.Visible = visible;
                panel.Enabled = visible;
            }

            if (InvokeRequired)
            {
                Invoke(new Action(update));
            }
            else
            {
                update();
            }
        }

        private async Task WaitIfSequencePausedAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                TaskCompletionSource<bool> resumeTcs;
                lock (_sequencePauseLock)
                {
                    if (!_sequencePaused) return;
                    resumeTcs = _sequenceResumeTcs;
                }

                using var registration = cancellationToken.Register(() => resumeTcs.TrySetCanceled(cancellationToken));
                await resumeTcs.Task;
            }
        }

        private void ApplyBundledPhosphorFontToControls()
        {
            var fontPath = Path.Combine(
                AppContext.BaseDirectory,
                "MyResources",
                "Fonts",
                "Phosphor",
                "regular",
                "Phosphor.ttf");

            if (!File.Exists(fontPath))
            {
                return;
            }

            try
            {
                _bundledPhosphorFonts = new PrivateFontCollection();
                _bundledPhosphorFonts.AddFontFile(fontPath);

                if (_bundledPhosphorFonts.Families.Length == 0)
                {
                    return;
                }

                ApplyPhosphorFontToControlTree(this, _bundledPhosphorFonts.Families[0]);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Impossible de charger la fonte Phosphor locale: " + ex.Message);
            }
        }

        private static void ApplyPhosphorFontToControlTree(Control parent, FontFamily phosphorFamily)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Font != null &&
                    string.Equals(control.Font.FontFamily.Name, "Phosphor", StringComparison.OrdinalIgnoreCase))
                {
                    control.Font = new Font(
                        phosphorFamily,
                        control.Font.Size,
                        control.Font.Style,
                        control.Font.Unit,
                        control.Font.GdiCharSet,
                        control.Font.GdiVerticalFont);

                    if (control is ButtonBase button)
                    {
                        button.UseCompatibleTextRendering = false;
                    }
                    else if (control is Label label)
                    {
                        label.UseCompatibleTextRendering = false;
                    }
                }

                if (control.HasChildren)
                {
                    ApplyPhosphorFontToControlTree(control, phosphorFamily);
                }
            }
        }



        private void SetTooltips()
        {
            System.Windows.Forms.ToolTip toolTipMask = new System.Windows.Forms.ToolTip();
            System.Windows.Forms.ToolTip toolTipCutoff = new System.Windows.Forms.ToolTip();
        }

        private void InitializeMaskAlgorithmDropdown()
        {
            int selectedIndex = appSettings.MaskAlgorithmIndex;
            if (selectedIndex == 2 && comboBox_MaskAlgorithm.Items.Count == 2)
            {
                selectedIndex = 1;
            }

            if (selectedIndex < 0 || selectedIndex >= comboBox_MaskAlgorithm.Items.Count)
            {
                selectedIndex = 0;
            }

            comboBox_MaskAlgorithm.SelectedIndex = selectedIndex;
            ApplyMaskThresholdForSelectedAlgorithm();
        }

        private void InitializeMaskThresholdSettings()
        {
            _isInitializingMaskThresholds = true;

            if (appSettings.ThreshVal_1 == 20 && appSettings.ThreshVal != 20)
            {
                appSettings.ThreshVal_1 = appSettings.ThreshVal;
            }

            txtBox_DefaultMaskThresh.Text = ClampMaskThreshold(appSettings.ThreshVal_1).ToString();
            txtBox_DefaultMaskThresh2.Text = ClampMaskThreshold(appSettings.ThreshVal_2).ToString();

            txtBox_DefaultMaskThresh.ForeColor = Color.White;
            txtBox_DefaultMaskThresh2.ForeColor = Color.White;

            _isInitializingMaskThresholds = false;
        }

        private int ClampMaskThreshold(int value)
        {
            return Math.Max(hScrollBar_liveMaskThresh.Minimum, Math.Min(255, value));
        }

        private int ClampMaskShrink(int value)
        {
            return Math.Max(trackBar_maskShrink1.Minimum, Math.Min(trackBar_maskShrink1.Maximum, value));
        }

        private int GetMaskThresholdSetting(int algorithmIndex)
        {
            return algorithmIndex switch
            {
                1 => appSettings.ThreshVal_2,
                _ => appSettings.ThreshVal_1,
            };
        }

        private int GetMaskShrinkSetting(int algorithmIndex)
        {
            return algorithmIndex switch
            {
                1 => projet.MaskShrink_2,
                _ => projet.MaskShrink_1,
            };
        }

        private void SetMaskThresholdSetting(int algorithmIndex, int value)
        {
            value = ClampMaskThreshold(value);

            switch (algorithmIndex)
            {
                case 1:
                    appSettings.ThreshVal_2 = value;
                    txtBox_DefaultMaskThresh2.Text = value.ToString();
                    txtBox_DefaultMaskThresh2.ForeColor = Color.White;
                    break;
                default:
                    appSettings.ThreshVal_1 = value;
                    appSettings.ThreshVal = value;
                    txtBox_DefaultMaskThresh.Text = value.ToString();
                    txtBox_DefaultMaskThresh.ForeColor = Color.White;
                    break;
            }
        }

        private void SetMaskShrinkSetting(int algorithmIndex, int value)
        {
            value = ClampMaskShrink(value);

            switch (algorithmIndex)
            {
                case 1:
                    projet.MaskShrink_2 = value;
                    trackBar_maskShrink2.Value = value;
                    lbl_maskShrink2.Text = value.ToString();
                    break;
                default:
                    projet.MaskShrink_1 = value;
                    trackBar_maskShrink1.Value = value;
                    lbl_maskShrink1.Text = value.ToString();
                    break;
            }
        }

        private void ApplyMaskThresholdForSelectedAlgorithm()
        {
            int value = ClampMaskThreshold(GetMaskThresholdSetting(comboBox_MaskAlgorithm.SelectedIndex));
            hScrollBar_liveMaskThresh.Value = value;
            lbl_maskAmount.Text = value.ToString();
        }

        private void InitializeMaskShrinkSettings()
        {
            _isInitializingMaskShrinkSettings = true;

            SetMaskShrinkSetting(0, projet.MaskShrink_1);
            SetMaskShrinkSetting(1, projet.MaskShrink_2);

            trackBar_maskShrink1.Scroll -= trackBar_maskShrink_Scroll;
            trackBar_maskShrink2.Scroll -= trackBar_maskShrink_Scroll;
            trackBar_maskShrink1.Scroll += trackBar_maskShrink_Scroll;
            trackBar_maskShrink2.Scroll += trackBar_maskShrink_Scroll;

            _isInitializingMaskShrinkSettings = false;
        }

        private int GetCurrentMaskShrink()
        {
            return ClampMaskShrink(GetMaskShrinkSetting(appSettings.MaskAlgorithmIndex));
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
            takePictureAsyncSimple();
            //await PhotoSuccess(projet.ImageNameFull, turntablePosition, true, tempsMs);
            // EssayerPrendrePhotoAsync(5);
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


        private void btn_stepperGetPosition_Click(object sender, EventArgs e)
        {
            UdpSendCameraLinearMessageAsync("stepmotor getstepperposition");
        }
        private void btn_StopLinearMotor_Click(object sender, EventArgs e)
        {
            udpSendCameraLinearMotorData(0);
        }



        #endregion

        #region TABLE TOURNANTE TAB




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
            AppendTextToConsoleNL("getTurntablePosFromWaveshare");
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


        private void btn_printLiftPositionConsole_Click(object sender, EventArgs e)
        {
            UdpSendLiftVerticalMessageAsync("stepmotor readData");
        }

        private void trkBar_LiftHorizontal_Scroll(object sender, EventArgs e)
        {
            int currentValue = trkBar_LiftHorizontal.Value * -5;
            if (currentValue != lastHorizontalValue)
            {
                udpSendLiftHorizontalData(currentValue);
                lastHorizontalValue = currentValue;
            }
        }

        private void trkBar_LiftVertical_Scroll(object sender, EventArgs e)
        {
            int currentValue = trkBar_LiftVertical.Value * 100;
            if (currentValue != lastVerticalValue)
            {
                udpSendLiftVerticalMotorData(currentValue);
                lastVerticalValue = currentValue;
            }
        }

        private void trkBar_LiftHorizontal_MouseUp(object sender, MouseEventArgs e)
        {
            trkBar_LiftHorizontal.Value = 0;
            udpSendLiftHorizontalData(0);
        }

        private void trkBar_LiftVertical_MouseUp(object sender, MouseEventArgs e)
        {
            trkBar_LiftVertical.Value = 0;
            udpSendLiftVerticalMotorData(0);
            UdpSendLiftVerticalMessageAsync("stepmotor readData");
        }

        private void btn_VerticalLiftStep_Calibration_Click(object sender, EventArgs e)
        {
            UdpSendLiftVerticalMessageAsync("stepmotor calibration");
        }

        private void btn_LiftVerticalDefault_Click(object sender, EventArgs e)
        {
            UdpSendLiftVerticalMessageAsync("stepmotor setDefault");
        }

        private void btn_VerticalLiftGoToDefault_Click(object sender, EventArgs e)
        {
            UdpSendLiftVerticalMessageAsync("stepmotor moveto " + appSettings.VerticalLiftDefaultPos.ToString());
        }



        private void btn_LiftAutoCenterRoutine_Click(object sender, EventArgs e)
        {
            calculerCentre = true;
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

            await udpSendLiftHorizontalData(newSpeed);

        }

        #endregion

        #region ACTUATEUR


        private void btn_actuator_5_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator 5");
            StartManualActuatorAutoCenterTracking(5);
        }

        private void btn_actuator_25_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator 25");
            StartManualActuatorAutoCenterTracking(25);
        }

        private void btn_actuator_45_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator 45");
            StartManualActuatorAutoCenterTracking(45);
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
            StartManualActuatorAutoCenterTracking();
        }

        private void btn_Actuator_Up_Click(object sender, EventArgs e)
        {
            UdpSendActuatorMessageAsync("actuator up");
            StartManualActuatorAutoCenterTracking();
        }

        private void performActuatorCalibration()
        {
            UdpSendActuatorMessageAsync("actuator calibration");
        }
        private void btn_actoatorCalibration_Click(object sender, EventArgs e)
        {
            performActuatorCalibration();
        }

        public async Task<bool> WaitForActuator(double target, CancellationToken cancellationToken = default)
        {
            AppendTextToConsoleNL("WaitForActuator");
            double delta = 3;
            int timeoutMs = 10000;
            DateTime startTime = DateTime.Now;
            CancellationTokenSource? actuatorAutoCenterCts = null;
            Task? actuatorAutoCenterTask = null;
            bool targetReached = false;
            bool blobSeenDuringMove = offsets.hasForeground;
            DateTime? blobLostSince = offsets.hasForeground ? null : DateTime.Now;
            int autofocusRecoveryCount = 0;

            if (ShouldAutoCenterDuringActuatorMove())
            {
                actuatorAutoCenterCts = new CancellationTokenSource();
                actuatorAutoCenterTask = RunAutoCentrageContinuPendantActuateurAsync(actuatorAutoCenterCts.Token);
            }

            try
            {
                while (!_stopRequested && !cancellationToken.IsCancellationRequested && (DateTime.Now - startTime).TotalMilliseconds <= timeoutMs)
                {
                    await WaitIfSequencePausedAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    await SendActuatorAngleRequestAsync();

                    if (ShouldAutoCenterDuringActuatorMove())
                    {
                        if (offsets.hasForeground)
                        {
                            blobSeenDuringMove = true;
                            blobLostSince = null;
                        }
                        else if (blobSeenDuringMove)
                        {
                            blobLostSince ??= DateTime.Now;

                            if (autofocusRecoveryCount < 2 && (DateTime.Now - blobLostSince.Value).TotalMilliseconds >= 1000)
                            {
                                autofocusRecoveryCount++;
                                AppendTextToConsoleNL("Blob perdu pendant mouvement d'actuateur: pause + autofocus");
                                await UdpSendActuatorMessageAsync("actuator stop");
                                await TryAutofocusPendantActuateurAsync(cancellationToken);
                                await SendActuatorTargetAsync(target);
                                blobLostSince = DateTime.Now;
                            }
                        }
                    }

                    // Vérifie si on est dans la plage cible
                    if (Math.Abs(actuatorAngle - target) <= delta)
                    {
                        AppendTextToConsoleNL($"Actuateur dans la plage : {actuatorAngle} (cible {target})");
                        targetReached = true;
                        break;
                    }

                    await Task.Delay(500, cancellationToken); // Aligné avec la fréquence d'update
                }
            }
            finally
            {
                actuatorAutoCenterCts?.Cancel();
                cancelAutoCentrage = true;

                if (actuatorAutoCenterTask != null)
                {
                    try
                    {
                        await actuatorAutoCenterTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                actuatorAutoCenterCts?.Dispose();

                if (targetReached && ShouldAutoCenterDuringActuatorMove() && !_stopRequested && !cancellationToken.IsCancellationRequested)
                {
                    if (blobSeenDuringMove)
                    {
                        AppendTextToConsoleNL("Autofocus final à la position d'actuateur atteinte");
                        await TryAutofocusPendantActuateurAsync(cancellationToken);
                    }

                    if (offsets.hasForeground)
                    {
                        AppendTextToConsoleNL("Auto-centrage final à la position d'actuateur atteinte");
                        await RoutineAutoCentrage(3000);
                    }
                    else
                    {
                        AppendTextToConsoleNL("Auto-centrage final ignoré: aucun blob détecté");
                    }
                }

                udpSendLiftVerticalMotorData(0);
                udpSendLiftHorizontalData(0);
                udpSendCameraLinearMotorData(0);
            }

            if (targetReached)
            {
                return true;
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
            else if (cancellationToken.IsCancellationRequested)
            {
                AppendTextToConsoleNL("Arrêt demandé par l'utilisateur.");
            }

            return false;
        }

        private bool ShouldAutoCenterDuringActuatorMove()
        {
            return projet.AutoCentrageActuator || _testAutoCenterActuatorEnabled;
        }

        private async Task SendActuatorTargetAsync(double target)
        {
            if (Math.Abs(target - 5) < 0.1)
            {
                await UdpSendActuatorMessageAsync("actuator 5");
            }
            else if (Math.Abs(target - 25) < 0.1)
            {
                await UdpSendActuatorMessageAsync("actuator 25");
            }
            else if (Math.Abs(target - 45) < 0.1)
            {
                await UdpSendActuatorMessageAsync("actuator 45");
            }
            else
            {
                await UdpSendActuatorMessageAsync($"actuator custom, {target.ToString("0", CultureInfo.InvariantCulture)}");
            }
        }

        private async Task TryAutofocusPendantActuateurAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || _stopRequested) return;

            try
            {
                await nikonDoFocus();
                await Task.Delay(300, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Autofocus pendant actuateur ignoré: {ex.Message}");
            }
        }

        private void StartManualActuatorAutoCenterTracking(double? target = null)
        {
            if (!_testAutoCenterActuatorEnabled) return;

            _manualActuatorAutoCenterCts?.Cancel();
            _manualActuatorAutoCenterCts = new CancellationTokenSource();
            var token = _manualActuatorAutoCenterCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (target.HasValue)
                    {
                        await WaitForActuator(target.Value, token);
                        return;
                    }

                    Task autoCenterTask = RunAutoCentrageContinuPendantActuateurAsync(token);

                    try
                    {
                        DateTime startTime = DateTime.Now;
                        while (!token.IsCancellationRequested && !_stopRequested && (DateTime.Now - startTime).TotalSeconds < 20)
                        {
                            await SendActuatorAngleRequestAsync();
                            await Task.Delay(300, token);
                        }
                    }
                    finally
                    {
                        _manualActuatorAutoCenterCts?.Cancel();
                        cancelAutoCentrage = true;
                        await autoCenterTask;
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
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

        public void AppendTextToConsoleNL(string message) // New Line
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

        public void AppendTextToConsoleNL(string message, Color messageColor) // New Line
        {

            System.Windows.Forms.RichTextBox textbox = txtBox_Console;

            string timestamp = $"{DateTime.Now:HH:mm:ss:ff} - ";

            if (textbox.InvokeRequired)
            {
                textbox.Invoke(new Action(() =>
                {
                    AppendFormattedText(timestamp, Color.Gray, textbox);
                    AppendFormattedText(message + Environment.NewLine, messageColor, textbox);
                }));
            }
            else
            {
                AppendFormattedText(timestamp, Color.Gray, textbox);
                AppendFormattedText(message + Environment.NewLine, messageColor, textbox);
            }
        }


        private void AppendFormattedText(string text, Color color, System.Windows.Forms.RichTextBox textbox)
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

            ManageRichTextBoxContent(textbox);

        }


        //private void AppendFormattedTextInternal(string text, Color color, System.Windows.Forms.RichTextBox textbox)
        //{
        //    textbox.SelectionStart = textbox.Text.Length;
        //    if (MainConsoleScrollToCarret)
        //    {
        //        textbox.ScrollToCaret();
        //        textbox.Select(); // Active le caret sans voler le focus
        //        textbox.SelectionLength = 0;
        //    }



        //    textbox.SelectionColor = color;
        //    textbox.AppendText(text);
        //    textbox.SelectionColor = txtBox_Console.ForeColor;
        //    ManageRichTextBoxContent(textbox);
        //    textbox.Refresh();
        //}



        private void AppendFormattedTextInternal(string text, Color color, RichTextBox textbox)
        {
            int scrollPos = 0;

            if (!MainConsoleScrollToCarret)
            {
                scrollPos = GetScrollPos(textbox.Handle, SB_VERT);
            }

            textbox.SuspendLayout();

            textbox.SelectionStart = textbox.TextLength;
            textbox.SelectionLength = 0;
            textbox.SelectionColor = color;
            textbox.AppendText(text);
            textbox.SelectionColor = textbox.ForeColor;

            ManageRichTextBoxContent(textbox);

            if (MainConsoleScrollToCarret)
            {
                textbox.ScrollToCaret();
            }
            else
            {
                SetScrollPos(textbox.Handle, SB_VERT, scrollPos, true);
                SendMessage(textbox.Handle, WM_VSCROLL, SB_THUMBPOSITION, scrollPos);
            }

            textbox.ResumeLayout();
        }


        private void ManageRichTextBoxContent(System.Windows.Forms.RichTextBox textbox)
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
            projet.PictureWidth = int.Parse((imgSize[imgSize.Index].ToString().Split("*")[0].Substring(2)));
            projet.PictureHeight = int.Parse(imgSize[imgSize.Index].ToString().Split("*")[1][..^1]);
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

        private async void btn_PrisePhotoSeqTotaleMain_Click(object sender, EventArgs e)
        {
            if (!await ConfirmNetworkBeforeSequenceAsync()) return;

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
                    ResetSerieIncrementAndName();
                    ResetRotationIncrementAndName();
                    ResetFocusIncrementationAndName();
                    try
                    {
                        DeleteAllPicturesInFolderWith();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur  btn_PrisePhotoSeqTotaleMain_Click :: DeleteAllPicturesInFolderWith  {ex.Message}");
                    }

                    ResetSequenceCancellationButton();
                    StartTotalPhotoSequenceWithControls();
                }
                if (result == DialogResult.No)
                {
                    DialogResult dr = MessageBox.Show(
                   "Voulez-vous démarrer la série?",
                   "Confirmation",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Question
                  );
                    if (dr == DialogResult.Yes)
                    {

                        try
                        {
                            DeleteAllPicturesInFolderWith();
                        }
                        catch (Exception ex)
                        {
                            AppendTextToConsoleNL($"Erreur  btn_PrisePhotoSeqTotaleMain_Click :: DeleteAllPicturesInFolderWith  {ex.Message}");
                        }

                        ResetSequenceCancellationButton();
                        StartTotalPhotoSequenceWithControls();
                    }
                    else
                    {
                        return;
                    }

                }
            }
            else
            {
                DialogResult dr = MessageBox.Show(
                   "Voulez-vous démarrer la série?",
                   "Confirmation",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Question
                  );
                if (dr == DialogResult.Yes)
                {

                    try
                    {
                        DeleteAllPicturesInFolderWith();
                    }
                    catch (Exception ex)
                    {
                        AppendTextToConsoleNL($"Erreur  btn_PrisePhotoSeqTotaleMain_Click :: DeleteAllPicturesInFolderWith  {ex.Message}");
                    }

                    ResetSerieIncrementAndName();
                    ResetRotationIncrementAndName();
                    ResetFocusIncrementationAndName();
                    ResetSequenceCancellationButton();
                    StartTotalPhotoSequenceWithControls();
                }
                else
                {
                    return;
                }
            }

        }

        private void btn_cancelPhotoShootMain_Click(object sender, EventArgs e)
        {
            StopSequences();
        }

        private void StartTotalPhotoSequenceWithControls()
        {
            Task.Run(async () =>
            {
                SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: true);
                try
                {
                    tokenSource = new CancellationTokenSource();
                    await SequencePrisePhotoTotale(tokenSource.Token);
                }
                finally
                {
                    SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: false);
                }
            });
        }


        private async void btn_PrisePhotoSeqTotale_Click(object sender, EventArgs e)
        {
            if (!await ConfirmNetworkBeforeSequenceAsync()) return;

            bool startFromBeginning = projet.FocusSerieIncrement == 0 && projet.RotationSerieIncrement == 0;

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
                    startFromBeginning = true;
                    ResetSerieIncrementAndName();
                    ResetFocusIncrementationAndName();
                    ResetRotationIncrementAndName();
                    DeleteAllPicturesInFolderWith();
                }
            }
            if (startFromBeginning)
            {
                ResetSerieIncrementAndName();
                ResetFocusIncrementationAndName();
                ResetRotationIncrementAndName();
            }

            ResetSequenceCancellationButton();


            StartTotalPhotoSequenceWithControls();
        }

        private async void btn_PriseImagesMesuresTotale_Click(object sender, EventArgs e)
        {
            if (!await ConfirmNetworkBeforeSequenceAsync()) return;

            QueryProject();
            if (appSettings.ProjectPath == null) return;

            if (!TryUpdateMesurementCountsFromTextBoxes()) return;
            SavePrefsSettings();

            DisplayPathsInUI();
            ResetSequenceCancellationButton();

            Task.Run(async () =>
            {
                SetSequenceActionControlsVisible(_volumeSequenceActionsPanel, visible: true);
                try
                {
                    tokenSource = new CancellationTokenSource();
                    await SequenceTotaleImageMesuresAsync(tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    AppendTextToConsoleNL("Séquence totale d'images de mesure annulée.");
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL($"Erreur btn_PriseImagesMesuresTotale_Click: {ex.Message}");
                    _stopRequested = true;
                    ShowMeasurementSequenceErrorMessage(ex);
                }
                finally
                {
                    SetSequenceActionControlsVisible(_volumeSequenceActionsPanel, visible: false);
                }
            });
        }


        private async void btn_prisePhotoSeq1_Click(object sender, EventArgs e)
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

            if (!await ConfirmNetworkBeforeSequenceAsync()) return;

            // Si l'utilisateur accepte, on continue
            //projet.RotationSerieIncrement = int.Parse(txtBox_seqPad1.Text);

            DisplayPathsInUI();

            ResetSequenceCancellationButton();
            projet.Serie = 0;
            projet.RotationSerieIncrement = 0;
            projet.FocusSerieIncrement = 0;
            SavePrefsSettings();

            tokenSource = new CancellationTokenSource();
            var cancellationToken = tokenSource.Token;

            Task.Run(async () =>
            {
                try
                {
                    await UdpSendActuatorMessageAsync("actuator 5");
                    if (_stopRequested) return;
                    await WaitForActuator(5);
                    cancellationToken.ThrowIfCancellationRequested();
                    projet.Serie = 0;
                    await PrisePhotoSequenceAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    AppendTextToConsoleNL("Séquence photo 5° annulée.");
                }
                catch (Exception ex)
                {
                    _stopRequested = true;
                    AppendTextToConsoleNL($"Erreur btn_prisePhotoSeq1_Click: {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                }
            });
        }

        private async void btn_prisePhotoSeq2_Click(object sender, EventArgs e)
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

            if (!await ConfirmNetworkBeforeSequenceAsync()) return;

            //projet.RotationSerieIncrement = int.Parse(txtBox_seqPad2.Text);

            DisplayPathsInUI();

            ResetSequenceCancellationButton();
            projet.Serie = 1;
            projet.RotationSerieIncrement = 0;
            projet.FocusSerieIncrement = 0;
            SavePrefsSettings();

            tokenSource = new CancellationTokenSource();
            var cancellationToken = tokenSource.Token;

            Task.Run(async () =>
            {
                try
                {
                    await UdpSendActuatorMessageAsync("actuator 25");
                    if (_stopRequested) return;
                    await WaitForActuator(25);
                    cancellationToken.ThrowIfCancellationRequested();
                    await PrisePhotoSequenceAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    AppendTextToConsoleNL("Séquence photo 25° annulée.");
                }
                catch (Exception ex)
                {
                    _stopRequested = true;
                    AppendTextToConsoleNL($"Erreur btn_prisePhotoSeq2_Click: {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                }
            });

        }

        private async void btn_prisePhotoSeq3_Click(object sender, EventArgs e)
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

            if (!await ConfirmNetworkBeforeSequenceAsync()) return;

            //projet.RotationSerieIncrement = int.Parse(txtBox_seqPad3.Text);

            DisplayPathsInUI();


            ResetSequenceCancellationButton();
            projet.Serie = 2;
            projet.RotationSerieIncrement = 0;
            projet.FocusSerieIncrement = 0;
            SavePrefsSettings();

            tokenSource = new CancellationTokenSource();
            var cancellationToken = tokenSource.Token;

            Task.Run(async () =>
            {
                try
                {
                    await UdpSendActuatorMessageAsync("actuator 45");
                    if (_stopRequested) return;
                    await WaitForActuator(45);
                    cancellationToken.ThrowIfCancellationRequested();
                    await PrisePhotoSequenceAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    AppendTextToConsoleNL("Séquence photo 45° annulée.");
                }
                catch (Exception ex)
                {
                    _stopRequested = true;
                    AppendTextToConsoleNL($"Erreur btn_prisePhotoSeq3_Click: {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                }
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


        private void btn_stopAutomaticFocusCapture_Click(object sender, EventArgs e)
        {
            StopSequences();
            maskFreeze = false;
            btn_freezeMask.Text = "";

        }
        private void btn_cancelPhotoShoot_Click(object sender, EventArgs e)
        {
            StopSequences();
            maskFreeze = false;
            btn_freezeMask.Text = "";
        }

        private async void StopSequences()
        {
            maskFreeze = false;
            btn_freezeMask.Text = "";

            AppendTextToConsoleNL($"{GetActiveSequenceName()} cancellée par l'utilisateur.", Color.Red);

            tokenSource?.Cancel();
            _cts?.Cancel();
            _stopRequested = true;
            SetSequenceActionControlsVisible(_volumeSequenceActionsPanel, visible: false);
            SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: false);
            if (btn_cancelPhotoShoot.InvokeRequired)
            {
                btn_cancelPhotoShoot.Invoke(new Action(() =>
                {
                    btn_stopAutomaticFocusCapture.BackColor = Color.FromArgb(30, 30, 30);
                    btn_cancelPhotoShoot.BackColor = Color.FromArgb(30, 30, 30);
                    // lbl_CancelStatus.Text = "Cancel? OUI";
                }));
            }
            else
            {
                btn_stopAutomaticFocusCapture.BackColor = Color.FromArgb(30, 30, 30);
                btn_cancelPhotoShoot.BackColor = Color.FromArgb(30, 30, 30);
                // lbl_CancelStatus.Text = "Cancel? OUI";
            }

            await Task.Delay(5000);

            ResetSequenceCancellationButton();

        }

        private void ResetSequenceCancellationButton()
        {
            _stopRequested = false;
            if (btn_stopAutomaticFocusCapture.InvokeRequired)
            {
                btn_stopAutomaticFocusCapture.Invoke(new Action(() =>
                {
                    btn_cancelPhotoShoot.BackColor = System.Drawing.Color.FromArgb(100, 80, 30, 30);
                    btn_stopAutomaticFocusCapture.BackColor = System.Drawing.Color.FromArgb(100, 80, 30, 30);
                }));
            }
            else
            {
                btn_cancelPhotoShoot.BackColor = System.Drawing.Color.FromArgb(100, 80, 30, 30);
                btn_stopAutomaticFocusCapture.BackColor = System.Drawing.Color.FromArgb(100, 30, 80, 30);
            }
        }

        private async void btn_focusMinus_Click(object sender, EventArgs e)
        {
            try
            {
                await ManualFocusAsync(1, stepSize);
                focusStackStepVar -= 1;
                lbl_focusStepsVar.Text = focusStackStepVar.ToString();
            }
            catch (Exception)
            {
            }

        }

        private async void btn_focusPlus_Click(object sender, EventArgs e)
        {
            try
            {
                await ManualFocusAsync(0, stepSize);
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
                panelSize = new Size(Math.Max(90, panelSize.Width - 20), Math.Max(70, panelSize.Height - 20));
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
                        ApplyThumbnailLayout(panel, newSize);
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
            _manualActuatorAutoCenterCts?.Cancel();
            UdpSendActuatorMessageAsync("actuator stop");
        }

        private void btn_TestAutoCenterActuator_Click(object sender, EventArgs e)
        {
            _testAutoCenterActuatorEnabled = !_testAutoCenterActuatorEnabled;
            btn_TestAutoCenterActuator.Text = _testAutoCenterActuatorEnabled ? "" : "";

            if (!_testAutoCenterActuatorEnabled)
            {
                _manualActuatorAutoCenterCts?.Cancel();
                udpSendLiftVerticalMotorData(0);
                udpSendLiftHorizontalData(0);
                udpSendCameraLinearMotorData(0);
            }
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

        private void trackBar_maskShrink_Scroll(object? sender, EventArgs e)
        {
            if (_isInitializingMaskShrinkSettings) return;

            if (sender == trackBar_maskShrink2)
            {
                SetMaskShrinkSetting(1, trackBar_maskShrink2.Value);
            }
            else
            {
                SetMaskShrinkSetting(0, trackBar_maskShrink1.Value);
            }

            SavePrefsSettings();
        }

        private void comboBox_MaskAlgorithm_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_MaskAlgorithm.SelectedIndex < 0) return;

            appSettings.MaskAlgorithmIndex = comboBox_MaskAlgorithm.SelectedIndex;
            ApplyMaskThresholdForSelectedAlgorithm();
            SetMaskShrinkSetting(0, projet.MaskShrink_1);
            SetMaskShrinkSetting(1, projet.MaskShrink_2);
            appSettings.Save();

            if (maskFreeze)
            {
                maskFreeze = false;
                btn_freezeMask.Text = "";
            }
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
            ResetSerieIncrementAndName();

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
                if (int.TryParse(txtBox_nbrImg5deg.Text, out int valeur) && valeur > 0)
                {
                    txtBox_nbrImg5deg.ForeColor = Color.White;
                    lbl_Serie5Angle.Text = (4096 / valeur).ToString() + " / " + (360 / valeur).ToString();
                    appSettings.NbrImg5Deg = valeur;
                    UpdateSequencePadding(true);
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;

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
                if (int.TryParse(txtBox_nbrImg25deg.Text, out int valeur) && valeur > 0)
                {
                    txtBox_nbrImg25deg.ForeColor = Color.White;
                    lbl_Serie25Angle.Text = (4096 / valeur).ToString() + " / " + (360 / valeur).ToString();
                    appSettings.NbrImg25Deg = valeur;
                    UpdateSequencePadding(true);
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;

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
                if (int.TryParse(txtBox_nbrImg45deg.Text, out int valeur) && valeur > 0)
                {
                    txtBox_nbrImg45deg.ForeColor = Color.White;
                    lbl_Serie45Angle.Text = (4096 / valeur).ToString() + " / " + (360 / valeur).ToString();
                    appSettings.NbrImg45Deg = valeur;
                    UpdateSequencePadding(true);
                }
                else
                {
                    MessageBox.Show("SVP enter un nombre valide");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;

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
                    UpdateSequencePadding();
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
                    UpdateSequencePadding();
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
                    UpdateSequencePadding();
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
            MainConsoleScrollToCarret = !MainConsoleScrollToCarret;
            btn_consoleScrollToCaret.BackColor = MainConsoleScrollToCarret ? Color.FromArgb(25, 25, 25) : Color.FromArgb(100, 100, 100);

        }

        private int lastHorizontalValue = -1;
        private int lastVerticalValue = -1;



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
                DisplayPathsInUI();
            }
        }

        private void btn_coteA_Click(object sender, EventArgs e)
        {
            ToggleCote(0);
            projet.Cote = 0;
            string coteFolder = (projet.Cote == 0) ? "A" : "B";
            lbl_CoteSerie.Text = coteFolder;
            projet.Save(appSettings.ProjectPath);
            DisplayPathsInUI();
        }

        private void btn_coteB_Click(object sender, EventArgs e)
        {
            ToggleCote(1);
            projet.Cote = 1;
            string coteFolder = (projet.Cote == 0) ? "A" : "B";
            lbl_CoteSerie.Text = coteFolder;
            projet.Save(appSettings.ProjectPath);
            DisplayPathsInUI();
        }


        private void ToggleCote(int newCote)
        {
            btn_coteA.BackColor = (newCote == 0) ? Color.FromArgb(0, 120, 0) : Color.FromArgb(30, 30, 30);
            btn_coteB.BackColor = (newCote == 1) ? Color.FromArgb(0, 120, 0) : Color.FromArgb(30, 30, 30);
            string coteFolder = (projet.Cote == 0) ? "A" : "B";
            lbl_CoteSerie.Text = coteFolder;
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



        private bool isCalibrating = false;


        private async void btn_LinearCalibration_Click(object sender, EventArgs e)
        {

            await RoutineCalibration();
        }


        private async void btn_GetSwtichesState_Click(object sender, EventArgs e)
        {
            //AppendTextToConsoleNL("État des switch demandé");
            await GetLinearSwitchesStateFromLinear();
        }

        private void btn_AjoutUser_Click(object sender, EventArgs e)
        {

            string usr = PromptCreateUser();
            if (usr != null)
            {
                CreateUser(usr);
            }
        }

        private void Aerolithe_FormClosed(object sender, FormClosedEventArgs e)
        {
            ShutdownApplication();
            AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 250, killIfStuck: true);
        }

        private async void btn_PingAll_Click(object sender, EventArgs e)
        {
            await PingAll();

        }

        private async void btn_WarningPing_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage7;
            tabControl2.SelectedTab = tabPage12;
            await PingAll();
        }



        private void Aerolithe_FormClosing(object sender, FormClosingEventArgs e)
        {
            ShutdownApplication();
        }

        private void ShutdownApplication()
        {
            if (_shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
            _stopRequested = true;

            try { _autoPingCts?.Cancel(); } catch { }
            try { _actuatorAnglePollingCts?.Cancel(); } catch { }
            try { _manualActuatorAutoCenterCts?.Cancel(); } catch { }
            try { tokenSource?.Cancel(); } catch { }
            try { _cts?.Cancel(); } catch { }
            try { SetSequenceActionControlsVisible(_volumeSequenceActionsPanel, visible: false); } catch { }
            try { SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: false); } catch { }

            try { imageReadyTcs?.TrySetCanceled(); } catch { }
            try { captureCompleteTcs?.TrySetCanceled(); } catch { }
            try { miniaturesTcs?.TrySetCanceled(); } catch { }
            try { _pendingMiniatureTcs?.TrySetCanceled(); } catch { }

            try
            {
                liveViewTimer?.Stop();
                liveViewTimer?.Dispose();
            }
            catch { }

            try
            {
                _oscTimer?.Stop();
                _oscTimer?.Dispose();
            }
            catch { }

            try { udpClient?.Close(); } catch { }
            try { udpClient?.Dispose(); } catch { }
            try { udpClientOSC?.Close(); } catch { }
            try { udpClientOSC?.Dispose(); } catch { }

            try
            {
                if (device != null)
                {
                    try { device.ImageReady -= new ImageReadyDelegate(device_ImageReady); } catch { }
                    try { device.CaptureComplete -= new CaptureCompleteDelegate(device_CaptureComplete); } catch { }
                    try { device.Progress -= new ProgressDelegate(OnNikonProgress); } catch { }
                    try { device.LiveViewEnabled = false; } catch { }
                }

                if (manager != null)
                {
                    try { manager.DeviceAdded -= new DeviceAddedDelegate(manager_DeviceAdded); } catch { }
                    try { manager.DeviceRemoved -= new DeviceRemovedDelegate(manager_DeviceRemoved); } catch { }

                    var shutdownTask = Task.Run(() => manager.Shutdown());
                    shutdownTask.Wait(TimeSpan.FromMilliseconds(1000));
                }
            }
            catch { }

            AppLifecycle.StopAllGraceful(waitMsPerTask: 100);
        }

        private async void repriseDerniereSequence_Click(object sender, EventArgs e)
        {
            _autoPingCts?.Cancel();
            if (projet.Serie < 0) projet.Serie = 0;

            string cote = lbl_CoteSerie.Text = projet.Cote == 0 ? "A" : "B";

            DialogResult result = MessageBox.Show(
                $"Reprendre à partir de la dernière séquence réussie?\nSérie {projet.Serie}\nAngle {angleIndexes[projet.Serie]}\nCôté {projet.Cote}\nRotation {projet.RotationSerieIncrement}",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                if (!await ConfirmNetworkBeforeSequenceAsync()) return;

                lbl_CoteSerie.Text = projet.Cote == 0 ? "A" : "B";
                lbl_ElevSerie.Text = angleIndexes[projet.Serie].ToString();

                Task.Run(async () =>
                {
                    tokenSource = new CancellationTokenSource();
                    // projet.Serie = 0, 1 ou 2
                    //await SequencePrisePhotoTotale(tokenSource.Token, projet.Serie, projet.RotationSerieIncrement);
                    await SequencePrisePhotoTotale(tokenSource.Token);
                });
            }

        }


        private void btn_StackConsoleView_Click(object sender, EventArgs e)
        {
            StackConsoleView = !StackConsoleView;
            btn_StackConsoleView.BackColor = StackConsoleView ? Color.FromArgb(60, 60, 60) : Color.FromArgb(25, 25, 25);
        }

        private void btn_ffmpegJobClear_Click(object sender, EventArgs e)
        {
            flowPanelReports.Controls.Clear();
        }



        private void RepriseSpecifiqueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage3;
            tabControl4.SelectedTab = tabPage18;

            //    _autoPingCts?.Cancel();

            //    // Valeurs par défaut proposées = valeurs actuelles de 'projet'
            //    string serieStr = Interaction.InputBox(
            //        "Entrer la Série (0 (= 5°)\n1 (= 25°)\n2 (= 45°)) :",
            //        "Paramètre - Série",
            //        projet.Serie.ToString());

            //    if (string.IsNullOrWhiteSpace(serieStr)) return; // Annulé
            //    if (!int.TryParse(serieStr, out int serie) || serie < 0 || serie > 2)
            //    {
            //        MessageBox.Show("Série invalide.\nEntrer 0, 1 ou 2", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        return;
            //    }


            //    string coteStr = Interaction.InputBox(
            //        "Entrer le Côté (A ou B) :",
            //        "Paramètre - Côté",
            //        (projet.Cote == 0 ? "A" : "B"));

            //    if (string.IsNullOrWhiteSpace(coteStr)) return; // Annulé
            //    coteStr = coteStr.Trim().ToUpperInvariant();
            //    int cote = coteStr == "A" ? 0 : (coteStr == "B" ? 1 : -1);
            //    if (cote == -1)
            //    {
            //        MessageBox.Show("Côté invalide. Valeurs permises : A ou B.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        return;
            //    }


            //    string rotationStr = Interaction.InputBox(
            //        "Entrer le numéro du dossier à partir duquel débuter\nSe référer possiblement au dernier ou à l'avant dernier dossier de photos réussies\nEx: 49",
            //        "Paramètre - Rotation",
            //        projet.RotationSerieIncrement.ToString());

            //    if (string.IsNullOrWhiteSpace(rotationStr)) return; // Annulé
            //    if (!int.TryParse(rotationStr, out int rotation))
            //    {
            //        MessageBox.Show("Rotation invalide.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //        return;
            //    }

            //    // Mise à jour du modèle et de l'UI
            //    projet.Serie = serie;
            //    projet.Cote = cote;
            //    projet.RotationSerieIncrement = rotation;

            //    lbl_CoteSerie.Text = projet.Cote == 0 ? "A" : "B";
            //    lbl_ElevSerie.Text = angleIndexes[projet.Serie].ToString();

            //    Task.Run(async () =>
            //    {
            //        tokenSource = new CancellationTokenSource();
            //        await SequencePrisePhotoTotale(tokenSource.Token, projet.Serie, projet.RotationSerieIncrement);
            //    });
        }



        private void btn_ShowSharpnessOverlay_Click(object sender, EventArgs e)
        {
            projet.ViewSharpnessOverlay = !projet.ViewSharpnessOverlay;
            btn_ShowSharpnessOverlay.Text = projet.ViewSharpnessOverlay ? "" : "";
            projet.Save(appSettings.ProjectPath);

        }

        private void btn_maskAuto_Click(object sender, EventArgs e)
        {
            // Sauvegarder ou Appliquer le masque
            projet.ApplyMask = !projet.ApplyMask;
            btn_applyMask.Text = projet.ApplyMask ? "" : "";


            // disable la sauvegarde de l'image pour la mesure du volume dans Mestashape
            photoPourMesure = false;
            btn_saveImageForMesurements.Text = "";

            projet.Save(appSettings.ProjectPath);

        }

        private void btn_focusStackEnable_Click(object sender, EventArgs e)
        {
            projet.FocusStackEnabled = !projet.FocusStackEnabled;
            btn_focusStack.Text = projet.FocusStackEnabled ? "" : "";


            // disable la sauvegarde de l'image pour la mesure du volume dans Mestashape
            photoPourMesure = false;
            btn_saveImageForMesurements.Text = "";

            projet.Save(appSettings.ProjectPath);
        }

        private void btn_freezeMask_Click(object sender, EventArgs e)
        {
            maskFreeze = !maskFreeze;
            btn_freezeMask.Text = maskFreeze ? "" : "";

            // disable la sauvegarde de l'image pour la mesure du volume dans Mestashape
            photoPourMesure = false;
            btn_saveImageForMesurements.Text = "";

        }

        // Bouton sous le masque
        private void btn_saveImageForMesurements_Click(object sender, EventArgs e)
        {

            photoPourMesure = !photoPourMesure;
            btn_saveImageForMesurementSequence.Text = photoPourMesure ? "" : "";
            if (photoPourMesure) saveImageForMesurementEnable();
            else saveImageForMesurementRemettre();
        }


        //Bouton dans l'onlet Camera
        private void btn_saveImageForMesurementSequence_Click(object sender, EventArgs e)
        {
            projet.SaveImageForMesurements = !projet.SaveImageForMesurements;
            btn_saveImageForMesurementSequence.Text = projet.SaveImageForMesurements ? "" : "";
            SavePrefsSettings();
        }

        private async Task saveImageForMesurementEnable()
        {
            // sauvegarder les états actuels
            photoPourMesure = true;
            tmpData.mask = projet.ApplyMask;
            tmpData.focusStack = projet.FocusStackEnabled;
            tmpData.freeze = maskFreeze;

            // mettre tout à false

            Invoke(new Action(() =>
            {
                btn_applyMask.Text = "";                                   // false
                btn_focusStack.Text = "";                                  // false
                btn_freezeMask.Text = "";                                  // false
                btn_saveImageForMesurements.Text = "";                   // true
            }));
            await Task.Delay(200);
            projet.ApplyMask = false;
            projet.FocusStackEnabled = false;
            maskFreeze = false;

            SavePrefsSettings();

        }

        private async Task saveImageForMesurementRemettre()
        {
            // remettre comme avant
            photoPourMesure = false;
            projet.ApplyMask = tmpData.mask;
            projet.FocusStackEnabled = tmpData.focusStack;
            maskFreeze = tmpData.freeze;

            SavePrefsSettings();
            await Task.Delay(200);

            Invoke(new Action(() =>
            {
                if (projet.ApplyMask) btn_applyMask.Text = "";             // true?
                if (projet.FocusStackEnabled) btn_focusStack.Text = "";    // true?
                if (maskFreeze) btn_freezeMask.Text = "";                  // true?
                btn_saveImageForMesurements.Text = "";                   // false
            }));
        }

        private void txtBox_DefaultMaskThresh_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (sender is System.Windows.Forms.TextBox textBox && int.TryParse(textBox.Text, out int value))
                {
                    int algorithmIndex = textBox == txtBox_DefaultMaskThresh2 ? 1 : 0;

                    SetMaskThresholdSetting(algorithmIndex, value);
                    if (comboBox_MaskAlgorithm.SelectedIndex == algorithmIndex)
                    {
                        ApplyMaskThresholdForSelectedAlgorithm();
                    }
                    appSettings.Save();
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }
        private void txtBox_DefaultMaskThresh_TextChanged(object sender, EventArgs e)
        {
            if (_isInitializingMaskThresholds) return;

            if (sender is System.Windows.Forms.TextBox textBox)
            {
                textBox.ForeColor = Color.Gray;
            }
        }

        private void btn_SaveThresh_Click(object sender, EventArgs e)
        {
            int selectedIndex = comboBox_MaskAlgorithm.SelectedIndex < 0 ? 0 : comboBox_MaskAlgorithm.SelectedIndex;
            SetMaskThresholdSetting(selectedIndex, hScrollBar_liveMaskThresh.Value);
            appSettings.Save();
        }

        private void btn_SaveImageToDisk_Click(object sender, EventArgs e)
        {
            projet.SaveImageToDisk = !projet.SaveImageToDisk;
            btn_SaveImageToDisk.Text = projet.SaveImageToDisk ? "" : "";
            SavePrefsSettings();
        }

        private async void btn_LiveViewEnable_Click(object sender, EventArgs e)
        {
            projet.LiveViewEnabled = !projet.LiveViewEnabled;
            btn_LiveViewEnable.Text = projet.LiveViewEnabled ? "" : "";
            SavePrefsSettings();
            await RunExclusiveNikonOperationAsync(() =>
            {
                if (device == null)
                {
                    return Task.CompletedTask;
                }

                if (projet.LiveViewEnabled)
                {
                    device.LiveViewEnabled = true;
                    liveViewTimer.Start();
                }
                else
                {
                    device.LiveViewEnabled = false;
                    liveViewTimer.Stop();
                }

                return Task.CompletedTask;
            });

        }

        private void SetMainWindowTitle(string? baseTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(baseTitle))
            {
                _windowTitleBase = baseTitle;
            }

            Text = $"{_windowTitleBase} | {UiRevision}";
        }

        private void txtBox_mesurements5deg_TextChanged(object sender, EventArgs e)
        {
            txtBox_mesurements5deg.ForeColor = Color.Gray;
        }

        private void txtBox_mesurements5deg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SaveMesurementCountFromTextBox(txtBox_mesurements5deg, 5);
                e.SuppressKeyPress = true;
            }
        }

        private void txtBox_mesurements25deg_TextChanged(object sender, EventArgs e)
        {
            txtBox_mesurements25deg.ForeColor = Color.Gray;
        }

        private void txtBox_mesurements25deg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SaveMesurementCountFromTextBox(txtBox_mesurements25deg, 25);
                e.SuppressKeyPress = true;
            }
        }
        private void txtBox_mesurements45deg_TextChanged(object sender, EventArgs e)
        {
            txtBox_mesurements45deg.ForeColor = Color.Gray;
        }

        private void txtBox_mesurements45deg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SaveMesurementCountFromTextBox(txtBox_mesurements45deg, 45);
                e.SuppressKeyPress = true;
            }
        }


        private bool TryUpdateMesurementCountsFromTextBoxes()
        {
            return TryUpdateMesurementCountFromTextBox(txtBox_mesurements5deg, 5)
                && TryUpdateMesurementCountFromTextBox(txtBox_mesurements25deg, 25)
                && TryUpdateMesurementCountFromTextBox(txtBox_mesurements45deg, 45);
        }

        private bool TryUpdateMesurementCountFromTextBox(System.Windows.Forms.TextBox textBox, int angle)
        {
            if (!int.TryParse(textBox.Text, out int valeur) || valeur < 0)
            {
                MessageBox.Show("SVP entrer un nombre valide égal ou plus grand que zéro");
                return false;
            }

            textBox.ForeColor = Color.White;
            if (angle == 5) projet.Mesurements5deg = valeur;
            if (angle == 25) projet.Mesurements25deg = valeur;
            if (angle == 45) projet.Mesurements45deg = valeur;
            return true;
        }

        private void SaveMesurementCountFromTextBox(System.Windows.Forms.TextBox textBox, int angle)
        {
            if (!TryUpdateMesurementCountFromTextBox(textBox, angle))
            {
                return;
            }

            SavePrefsSettings();
        }

        private void btn_AutoCentrageAuto_Click(object sender, EventArgs e)
        {
            projet.AutoCentrage = !projet.AutoCentrage;
            btn_AutoCentrageAuto.Text = projet.AutoCentrage ? "" : "";
            SavePrefsSettings();
        }

        private void btn_AutoCentrageActuator_Click(object sender, EventArgs e)
        {
            projet.AutoCentrageActuator = !projet.AutoCentrageActuator;
            btn_AutoCentrageActuator.Text = projet.AutoCentrageActuator ? "" : "";
            SavePrefsSettings();
        }

        private void effacerToutesLesImagesEtFocusToolStripMenuItem_Click(object sender, EventArgs e)
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
