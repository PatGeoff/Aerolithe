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
        public const string UiRevision = "REV-0069-email-photo-series-stats";
        public const string UiRevisionDate = "2026-05-22";
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
        private bool _networkConsoleMessagesEnabled;
        private bool _oscConsoleMessagesEnabled;
        private PrivateFontCollection? _bundledPhosphorFonts;
        private readonly object _sequencePauseLock = new();
        private bool _sequencePaused;
        private TaskCompletionSource<bool> _sequenceResumeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static readonly Color SequenceActionButtonBackColor = Color.FromArgb(35, 35, 35);
        private static readonly Color SequencePausedButtonBackColor = Color.FromArgb(110, 70, 20);
        private readonly Dictionary<System.Windows.Forms.Button, Color> _sequencePauseButtonBackColors = new();
        private string _lastSequenceErrorMessage = string.Empty;
        private volatile bool _manualSequenceCancellationRequested;
        private TableLayoutPanel? _volumeSequenceActionsPanel;
        private TableLayoutPanel? _totalSequenceActionsPanel;
        private System.Windows.Forms.Button? _volumePauseResumeButton;
        private System.Windows.Forms.Button? _totalPauseResumeButton;
        private bool _isInitializingMaskShrinkSettings;
        private bool _isInitializingActuatorSpeed;
        private readonly object _sequencePhotoStatsLock = new();
        private readonly List<SequencePhotoSeriesStats> _sequencePhotoStats = new();
        private int? _specificResumeLocalRotationOverride;
        private int? _specificResumeImageNumberOverride;


        public bool stackedImageInBuffer = false;

        public bool _DebugContinue = true;

        private bool isChangingCheckState = false;
        private bool _isInitializingMaskThresholds = false;
        private bool _testAutoCenterActuatorEnabled = false;
        private bool _automaticFocusRoutineRunning = false;
        private readonly Color _automaticFocusRoutineNormalBackColor = Color.FromArgb(30, 30, 30);
        private readonly Color _automaticFocusRoutineCancelBackColor = Color.FromArgb(100, 80, 30, 30);
        private CancellationTokenSource? _manualActuatorAutoCenterCts;
        private readonly object _calibrationAutoCentrageOverrideLock = new();
        private bool? _calibrationAutoCentrageSavedAuto;
        private bool? _calibrationAutoCentrageSavedActuator;
        private bool? _espLiftVerticalMaxSwitchPressed;
        private bool? _espLiftVerticalMinSwitchPressed;
        private bool? _espLiftHorizontalLeftSwitchPressed;
        private bool? _espLiftHorizontalRightSwitchPressed;
        private TaskCompletionSource<bool>? _liftVerticalSwitchStateTcs;
        private TaskCompletionSource<bool>? _liftHorizontalSwitchStateTcs;
        //private string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyResources\\Models", "u2net.onnx");

        private int[] serieId = [];


        private UdpClient udpClient;
        private UdpClient udpClientOSC;
        private TaskCompletionSource<int> _turntablePositionTcs;
        private TaskCompletionSource<int> _linearPositionTcs;
        private TaskCompletionSource<(bool NearPressed, bool FarPressed)>? _linearSwitchStateTcs;
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

            Program.StartupLog("Aerolithe constructor: InitializeComponent starting.");
            InitializeComponent();
            Program.StartupLog("Aerolithe constructor: InitializeComponent completed.");
            InitializeActuatorSpeedEvents();
            InitializeLiftXYPad();
            InitializeMessagingPanelLayout();
            SetMainWindowTitle();
            fichierToolStripMenuItem1.Click += quitterAerolitheToolStripMenuItem_Click;
            toolStripMenuItem1.Click += ReglagesServeurEnvoiToolStripMenuItem_Click;
            InitClasses();
            Instance = this;
            this.KeyDown += new KeyEventHandler(Form1_KeyDown);
            this.KeyPreview = true;
            picBox_LiveView_Main.Image = Properties.Resources.camera_offline;

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
            UpdateNetworkConsoleMessagesButton();
            UpdateOscConsoleMessagesButton();
            btn_maxImagesFS.Click += btn_maxImagesFS_Click;
            AttachDriveStepSettingsButton();
            AttachFocusStackDenoiseControls();
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

            Shown += Aerolithe_Shown;

        }

        private async void Aerolithe_Shown(object? sender, EventArgs e)
        {
            Shown -= Aerolithe_Shown;
            Program.StartupLog("Main form shown.");
            await InitializeAfterFirstRenderAsync();
        }

        private async Task InitializeAfterFirstRenderAsync()
        {
            await Task.Yield();

            AppendTextToConsoleNL("Initialisation d'Aérolithe...");
            Program.StartupLog("Post-render initialization starting.");

            Program.StartupLog("Applying UI fonts starting.");
            if (!ApplyBundledPhosphorFontToControls())
            {
                AppendTextToConsoleNL("Police Phosphor locale introuvable. Les icônes peuvent être mal affichées.");
            }
            Program.StartupLog("Applying UI fonts completed.");

            var nikonDir = Path.Combine(AppContext.BaseDirectory, "MyResources", "NikonLibs");
            var oldPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Environment.SetEnvironmentVariable("PATH", nikonDir + Path.PathSeparator + oldPath, EnvironmentVariableTarget.Process);

            try
            {
                appSettings = appSettings.Load();
                ApplyThumbnailSizeFromSettings();
                Debug.WriteLine(appSettings.ProjectPath);
                if (string.IsNullOrWhiteSpace(appSettings.ProjectPath) || !File.Exists(appSettings.ProjectPath))
                {
                    appSettings.ProjectPath = "";
                }
                appSettings.Save();
                LoadMessagingUsersInUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur durant appSettings.Load()\nerreur: " + ex.Message);
                return;
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
                        DisplayPathsInUI();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Erreur:" + ex);
                        AppendTextToConsoleNL("Erreur chargement projet: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur durant project.Load()\nerreur: " + ex.Message);
                return;
            }

            ToolTipsSetup();
            SetupPen();
            SetTooltips();
            SetVariables();
            ApplyProjectStateToUi();

            try
            {
                InitializeUdpClient();
                StartAutoPingLoop(TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur durant InitializeUdpClient(): " + ex.Message);
            }

            _ = getActuatorAngleFromEsp32();
            _ = getTurntablePosFromWaveshare();
            _ = UdpSendLiftVerticalMessageAsync("stepmotor readData");

            try
            {
                CamSetup();
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur durant CamSetup(): " + ex.Message);
                picBox_LiveView_Main.Image = Properties.Resources.camera_offline;
            }

            TestLoadNikonDlls();
            AppendTextToConsoleNL("Initialisation terminée.");
        }

        private void ApplyProjectStateToUi()
        {
            ToggleCote(projet.Cote);

            btn_focusStack.Text = projet.FocusStackEnabled ? "" : "";
            btn_applyMask.Text = projet.ApplyMask ? "" : "";
            InitializeMaskThresholdSettings();
            InitializeMaskShrinkSettings();
            InitializeMaskAlgorithmDropdown();
            btn_saveImageForMesurementSequence.Text = projet.SaveImageForMesurements ? "" : "";
            //btn_SaveImageToDisk.Text = projet.SaveImageToDisk ? "" : "";
            btn_LiveViewEnable.Text = projet.LiveViewEnabled ? "" : "";
            btn_AutoCentrageAuto.Text = projet.AutoCentrage ? "" : "";
            btn_AutoCentrageActuator.Text = projet.AutoCentrageActuator ? "" : "";
            btn_CalibrationAutoCentrage.Text = appSettings.CalibrationAutoCentrage ? "" : "";
            btn_ShowSharpnessOverlay.Text = projet.ViewSharpnessOverlay ? "" : "";
            UpdateMaxImagesFSButton();
            UpdateDriveStepSettingsButton();
            ApplyFocusStackDenoiseToUi();
            ApplyActuatorSpeedToUi();
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

        private void SetPhotoShootCancellationButtonVisible(bool visible)
        {
            void update()
            {
                btn_cancelPhotoShoot.Visible = visible;
                btn_cancelPhotoShoot.Enabled = visible;
                btn_cancelPhotoShoot.BackColor = visible
                    ? Color.FromArgb(100, 80, 30, 30)
                    : Color.FromArgb(30, 30, 30);
            }

            if (btn_cancelPhotoShoot.InvokeRequired)
            {
                btn_cancelPhotoShoot.Invoke((Action)update);
            }
            else
            {
                update();
            }
        }

        private T? FindControlByName<T>(string name) where T : Control
        {
            return Controls.Find(name, searchAllChildren: true).OfType<T>().FirstOrDefault();
        }

        private void AttachDriveStepSettingsButton()
        {
            var button = FindControlByName<System.Windows.Forms.Button>("btn_goToDriveStepSettings");
            if (button == null) return;

            button.Click -= btn_goToDriveStepSettings_Click;
            button.Click += btn_goToDriveStepSettings_Click;
            UpdateDriveStepSettingsButton();
        }

        private void UpdateDriveStepSettingsButton()
        {
            var button = FindControlByName<System.Windows.Forms.Button>("btn_goToDriveStepSettings");
            if (button == null) return;

            if (int.TryParse(txtBox_DriveStep.Text, out int value))
            {
                button.Text = value.ToString();
                return;
            }

            button.Text = projet.StepSize.ToString();
        }

        private void btn_goToDriveStepSettings_Click(object? sender, EventArgs e)
        {
            aerolitheTabControl2.SelectedTab = tabPage20;
            aerolitheTabControl3.SelectedTab = tabPage26;

            txtBox_DriveStep.Focus();
            txtBox_DriveStep.SelectAll();
        }

        private void AttachFocusStackDenoiseControls()
        {
            var trackBar = FindControlByName<System.Windows.Forms.TrackBar>("trackBar_FSDenoise");
            if (trackBar == null) return;

            trackBar.Scroll -= trackBar_FSDenoise_Scroll;
            trackBar.ValueChanged -= trackBar_FSDenoise_Scroll;
            trackBar.Scroll += trackBar_FSDenoise_Scroll;
            trackBar.ValueChanged += trackBar_FSDenoise_Scroll;
            ApplyFocusStackDenoiseToUi();
        }

        private void ApplyFocusStackDenoiseToUi()
        {
            var trackBar = FindControlByName<System.Windows.Forms.TrackBar>("trackBar_FSDenoise");
            if (trackBar != null)
            {
                int range = Math.Max(1, trackBar.Maximum - trackBar.Minimum);
                int value = trackBar.Minimum + (int)Math.Round(ClampFocusStackDenoise(projet.FocusStackDenoise) * range);
                trackBar.Value = Math.Max(trackBar.Minimum, Math.Min(trackBar.Maximum, value));
            }

            UpdateFocusStackDenoiseLabel();
        }

        private double GetFocusStackDenoiseFromUi()
        {
            var trackBar = FindControlByName<System.Windows.Forms.TrackBar>("trackBar_FSDenoise");
            if (trackBar == null)
            {
                return ClampFocusStackDenoise(projet.FocusStackDenoise);
            }

            int range = Math.Max(1, trackBar.Maximum - trackBar.Minimum);
            return ClampFocusStackDenoise((trackBar.Value - trackBar.Minimum) / (double)range);
        }

        private static double ClampFocusStackDenoise(double value)
        {
            if (double.IsNaN(value)) return 1.0;
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private void UpdateFocusStackDenoiseLabel()
        {
            var label = FindControlByName<Label>("lbl_FocusStackDenoise");
            if (label == null) return;

            label.Text = GetFocusStackDenoiseFromUi().ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void trackBar_FSDenoise_Scroll(object? sender, EventArgs e)
        {
            projet.FocusStackDenoise = GetFocusStackDenoiseFromUi();
            UpdateFocusStackDenoiseLabel();

            if (!string.IsNullOrWhiteSpace(appSettings?.ProjectPath))
            {
                projet.Save(appSettings.ProjectPath);
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

        private async Task<double?> RequestActuatorAngleAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var angleTcs = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            _actuatorAngleTcs = angleTcs;

            await SendActuatorAngleRequestAsync();

            Task completedTask = await Task.WhenAny(angleTcs.Task, Task.Delay(timeout, cancellationToken));
            if (completedTask == angleTcs.Task)
            {
                return await angleTcs.Task;
            }

            AppendTextToConsoleNL("Lecture de l'angle actuateur non reçue avant timeout.");
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }

        private static bool IsActuatorNearTarget(double angle, double target)
        {
            const double delta = 3;
            return Math.Abs(angle - target) <= delta;
        }

        private async Task<bool> ShowTotalSequenceReadyPromptAsync(CancellationToken cancellationToken)
        {
            var promptTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Form? promptForm = null;

            void showPrompt()
            {
                if (IsDisposed)
                {
                    promptTcs.TrySetResult(false);
                    return;
                }

                promptForm = new Form
                {
                    Text = "Routine totale en pause",
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.None,
                    MinimizeBox = false,
                    MaximizeBox = false,
                    ShowInTaskbar = false,
                    TopMost = true,
                    BackColor = Color.White,
                    Padding = new Padding(1),
                    ClientSize = new Size(460, 150)
                };

                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(40, 40, 40)
                };

                var label = new Label
                {
                    Text = "L'actuateur est rendu à 5°.\r\nAjuste le threshold, le masque ou le centrage au besoin, puis continue la routine.",
                    Dock = DockStyle.Top,
                    Height = 82,
                    Padding = new Padding(14, 14, 14, 6),
                    BackColor = Color.FromArgb(40, 40, 40),
                    ForeColor = Color.White,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                };

                var buttonsPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 52,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(10),
                    WrapContents = false,
                    BackColor = Color.FromArgb(40, 40, 40)
                };

                var continueButton = new System.Windows.Forms.Button
                {
                    Text = "Continuer",
                    Width = 120,
                    Height = 30,
                    BackColor = Color.FromArgb(55, 55, 55),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                continueButton.FlatAppearance.BorderColor = Color.White;

                var cancelButton = new System.Windows.Forms.Button
                {
                    Text = "Annuler",
                    Width = 120,
                    Height = 30,
                    BackColor = Color.FromArgb(55, 55, 55),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                cancelButton.FlatAppearance.BorderColor = Color.White;

                continueButton.Click += (_, __) =>
                {
                    promptTcs.TrySetResult(true);
                    promptForm.Close();
                };

                cancelButton.Click += (_, __) =>
                {
                    promptTcs.TrySetResult(false);
                    promptForm.Close();
                };

                promptForm.FormClosed += (_, __) =>
                {
                    promptTcs.TrySetResult(false);
                    promptForm.Dispose();
                };

                buttonsPanel.Controls.Add(continueButton);
                buttonsPanel.Controls.Add(cancelButton);
                contentPanel.Controls.Add(label);
                contentPanel.Controls.Add(buttonsPanel);
                promptForm.Controls.Add(contentPanel);
                promptForm.AcceptButton = continueButton;
                promptForm.CancelButton = cancelButton;

                promptForm.Show(this);
                promptForm.Activate();
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(showPrompt));
            }
            else
            {
                showPrompt();
            }

            using var registration = cancellationToken.Register(() =>
            {
                promptTcs.TrySetCanceled(cancellationToken);
                try
                {
                    BeginInvoke(new Action(() => promptForm?.Close()));
                }
                catch
                {
                }
            });

            return await promptTcs.Task;
        }

        private bool ApplyBundledPhosphorFontToControls()
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
                Program.StartupLog("Bundled Phosphor font not found: " + fontPath);
                return false;
            }

            try
            {
                _bundledPhosphorFonts = new PrivateFontCollection();
                _bundledPhosphorFonts.AddFontFile(fontPath);

                if (_bundledPhosphorFonts.Families.Length == 0)
                {
                    Program.StartupLog("Bundled Phosphor font file loaded with no font families: " + fontPath);
                    return false;
                }

                ApplyPhosphorFontToControlTree(this, _bundledPhosphorFonts.Families[0]);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Impossible de charger la fonte Phosphor locale: " + ex.Message);
                Program.StartupLog("Unable to load bundled Phosphor font: " + ex);
                return false;
            }
        }

        private static void ApplyPhosphorFontToControlTree(Control parent, FontFamily phosphorFamily)
        {
            foreach (Control control in parent.Controls)
            {
                ApplyWindowsTextFontToControl(control);

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
                        button.UseCompatibleTextRendering = true;
                    }
                    else if (control is Label label)
                    {
                        label.UseCompatibleTextRendering = true;
                    }
                }

                if (control.HasChildren)
                {
                    ApplyPhosphorFontToControlTree(control, phosphorFamily);
                }
            }
        }

        private static void ApplyWindowsTextFontToControl(Control control)
        {
            if (control.Font == null ||
                string.Equals(control.Font.FontFamily.Name, "Phosphor", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string familyName = control.Font.FontFamily.Name;
            bool shouldUseSegoe =
                familyName.StartsWith("Roboto", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(familyName, FontFamily.GenericSansSerif.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(familyName, "Microsoft Sans Serif", StringComparison.OrdinalIgnoreCase);

            if (!shouldUseSegoe)
            {
                return;
            }

            control.Font = new Font(
                "Segoe UI",
                control.Font.Size,
                control.Font.Style,
                control.Font.Unit,
                control.Font.GdiCharSet,
                control.Font.GdiVerticalFont);
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
            txtBox_DefaultMaskThresh3.Text = ClampMaskThreshold(appSettings.ThreshVal_3).ToString();

            txtBox_DefaultMaskThresh.ForeColor = Color.White;
            txtBox_DefaultMaskThresh2.ForeColor = Color.White;
            txtBox_DefaultMaskThresh3.ForeColor = Color.White;

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

        private int ClampActuatorSpeed(int value)
        {
            return Math.Clamp(value, 150, 1023);
        }

        private void InitializeActuatorSpeedEvents()
        {
            textBox_VitesseActuateur.TextChanged -= textBox_VitesseActuateur_TextChanged;
            textBox_VitesseActuateur.KeyDown -= textBox_VitesseActuateur_KeyDown;
            textBox_VitesseActuateur.Leave -= textBox_VitesseActuateur_Leave;

            textBox_VitesseActuateur.TextChanged += textBox_VitesseActuateur_TextChanged;
            textBox_VitesseActuateur.KeyDown += textBox_VitesseActuateur_KeyDown;
            textBox_VitesseActuateur.Leave += textBox_VitesseActuateur_Leave;
        }

        private void ApplyActuatorSpeedToUi()
        {
            _isInitializingActuatorSpeed = true;
            int speed = ClampActuatorSpeed(appSettings.ActuatorSpeed);
            appSettings.ActuatorSpeed = speed;
            textBox_VitesseActuateur.Text = speed.ToString(CultureInfo.InvariantCulture);
            textBox_VitesseActuateur.ForeColor = Color.White;
            _isInitializingActuatorSpeed = false;
        }

        private bool TryApplyActuatorSpeedFromUi(bool showMessage)
        {
            if (!int.TryParse(textBox_VitesseActuateur.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int speed))
            {
                if (showMessage)
                {
                    MessageBox.Show("SVP entrer une vitesse d'actuateur valide entre 150 et 1023.");
                }

                ApplyActuatorSpeedToUi();
                return false;
            }

            speed = ClampActuatorSpeed(speed);
            appSettings.ActuatorSpeed = speed;
            textBox_VitesseActuateur.Text = speed.ToString(CultureInfo.InvariantCulture);
            textBox_VitesseActuateur.ForeColor = Color.White;
            appSettings.Save();
            return true;
        }

        private void textBox_VitesseActuateur_TextChanged(object? sender, EventArgs e)
        {
            if (_isInitializingActuatorSpeed) return;
            textBox_VitesseActuateur.ForeColor = Color.Gray;
        }

        private void textBox_VitesseActuateur_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            TryApplyActuatorSpeedFromUi(showMessage: true);
            e.SuppressKeyPress = true;
        }

        private void textBox_VitesseActuateur_Leave(object? sender, EventArgs e)
        {
            TryApplyActuatorSpeedFromUi(showMessage: false);
        }

        private int GetMaskThresholdSetting(int algorithmIndex)
        {
            return algorithmIndex switch
            {
                2 => appSettings.ThreshVal_3,
                1 => appSettings.ThreshVal_2,
                _ => appSettings.ThreshVal_1,
            };
        }

        private int GetMaskShrinkSetting(int algorithmIndex)
        {
            return algorithmIndex switch
            {
                2 => projet.MaskShrink_3,
                1 => projet.MaskShrink_2,
                _ => projet.MaskShrink_1,
            };
        }

        private void SetMaskThresholdSetting(int algorithmIndex, int value)
        {
            value = ClampMaskThreshold(value);

            switch (algorithmIndex)
            {
                case 2:
                    appSettings.ThreshVal_3 = value;
                    txtBox_DefaultMaskThresh3.Text = value.ToString();
                    txtBox_DefaultMaskThresh3.ForeColor = Color.White;
                    break;
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
                case 2:
                    projet.MaskShrink_3 = value;
                    trackBar_maskShrink3.Value = value;
                    lbl_maskShrink3.Text = value.ToString();
                    break;
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
            SetMaskShrinkSetting(2, projet.MaskShrink_3);

            trackBar_maskShrink1.Scroll -= trackBar_maskShrink_Scroll;
            trackBar_maskShrink2.Scroll -= trackBar_maskShrink_Scroll;
            trackBar_maskShrink3.Scroll -= trackBar_maskShrink_Scroll;
            trackBar_maskShrink1.ValueChanged -= trackBar_maskShrink_Scroll;
            trackBar_maskShrink2.ValueChanged -= trackBar_maskShrink_Scroll;
            trackBar_maskShrink3.ValueChanged -= trackBar_maskShrink_Scroll;
            trackBar_maskShrink1.Scroll += trackBar_maskShrink_Scroll;
            trackBar_maskShrink2.Scroll += trackBar_maskShrink_Scroll;
            trackBar_maskShrink3.Scroll += trackBar_maskShrink_Scroll;
            trackBar_maskShrink1.ValueChanged += trackBar_maskShrink_Scroll;
            trackBar_maskShrink2.ValueChanged += trackBar_maskShrink_Scroll;
            trackBar_maskShrink3.ValueChanged += trackBar_maskShrink_Scroll;

            _isInitializingMaskShrinkSettings = false;
        }

        private int GetCurrentMaskShrink()
        {
            try
            {
                System.Windows.Forms.TrackBar trackBar = appSettings.MaskAlgorithmIndex switch
                {
                    2 => trackBar_maskShrink3,
                    1 => trackBar_maskShrink2,
                    _ => trackBar_maskShrink1,
                };

                if (trackBar.InvokeRequired)
                {
                    return (int)trackBar.Invoke(new Func<int>(() => ClampMaskShrink(trackBar.Value)));
                }

                return ClampMaskShrink(trackBar.Value);
            }
            catch
            {
                return ClampMaskShrink(GetMaskShrinkSetting(appSettings.MaskAlgorithmIndex));
            }
        }



        #region PROCÉDURE TAB

        private void btnAutofocus_Click(object sender, EventArgs e)
        {
            nikonDoFocus();
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
            AppendNetworkConsoleMessage("Aero: turnTablePosition = " + turntablePosition.ToString());
            string message = "turntable," + position.ToString() + "," + turntableSpeed;
            if (trkBar_turntable.InvokeRequired)
            {
                trkBar_turntable.Invoke(new Action(() => trkBar_turntable.Value = turntablePosition));
            }

            AppendNetworkConsoleMessage("Aero --> Table Tournate: " + message);
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
            AppendNetworkConsoleMessage("Demande la position de la table tournante au micro-controlleur");
            try
            {
                int? requestedPosition = await RequestTurntablePositionAsync(TimeSpan.FromSeconds(2));
                if (!requestedPosition.HasValue)
                {
                    AppendNetworkConsoleMessage("Table tournante: aucune réponse de position.");
                    return;
                }

                turntablePosition = requestedPosition.Value;
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

        private void InitializeLiftXYPad()
        {
            var liftPad = new LiftXYPadControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 1, 1, -15),
                HorizontalMinimum = trkBar_LiftHorizontal.Minimum,
                HorizontalMaximum = trkBar_LiftHorizontal.Maximum,
                VerticalMinimum = trkBar_LiftVertical.Minimum,
                VerticalMaximum = trkBar_LiftVertical.Maximum
            };

            liftPad.PadChanged += LiftXYPad_PadChanged;
            liftPad.PadReleased += LiftXYPad_PadReleased;

            tableLayoutPanel39.Controls.Add(liftPad, 2, 0);
        }

        private async void HorizontalLiftSwitchesButton_Click(object? sender, EventArgs e)
        {
            await RequestHorizontalLiftSwitchDiagnosticsAsync();
        }

        private async void VerticalLiftSwitchesButton_Click(object? sender, EventArgs e)
        {
            await RequestVerticalLiftSwitchDiagnosticsAsync();
        }

        private async Task RequestHorizontalLiftSwitchDiagnosticsAsync()
        {
            _liftHorizontalSwitchStateTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task horizontalStateTask = _liftHorizontalSwitchStateTcs.Task;

            try
            {
                await UdpSendLiftHorizontalMessageAsync("stepmotor switchState");
                await Task.WhenAny(horizontalStateTask, Task.Delay(800));
            }
            finally
            {
                AppendHorizontalLiftSwitchDiagnosticReport();

                if (ReferenceEquals(_liftHorizontalSwitchStateTcs?.Task, horizontalStateTask))
                {
                    _liftHorizontalSwitchStateTcs = null;
                }
            }
        }

        private async Task RequestVerticalLiftSwitchDiagnosticsAsync()
        {
            _liftVerticalSwitchStateTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task verticalStateTask = _liftVerticalSwitchStateTcs.Task;

            try
            {
                await UdpSendLiftVerticalMessageAsync("stepmotor switchState");
                await Task.WhenAny(verticalStateTask, Task.Delay(800));
            }
            finally
            {
                AppendVerticalLiftSwitchDiagnosticReport();

                if (ReferenceEquals(_liftVerticalSwitchStateTcs?.Task, verticalStateTask))
                {
                    _liftVerticalSwitchStateTcs = null;
                }
            }
        }

        private void AppendHorizontalLiftSwitchDiagnosticReport()
        {
            AppendTextToConsoleNL(
                $"(Esp32) Switch Horizontale Gauche = {FormatSwitchState(_espLiftHorizontalLeftSwitchPressed)}, " +
                $"Switch Horizontale Droite = {FormatSwitchState(_espLiftHorizontalRightSwitchPressed)}");
        }

        private void AppendVerticalLiftSwitchDiagnosticReport()
        {
            AppendTextToConsoleNL(
                $"(Esp32) Switch Verticale Max = {FormatSwitchState(_espLiftVerticalMaxSwitchPressed)}, " +
                $"Switch Verticale Min = {FormatSwitchState(_espLiftVerticalMinSwitchPressed)}");
        }

        private static string FormatSwitchState(bool? state)
        {
            return state.HasValue ? state.Value.ToString() : "Inconnu";
        }

        private void LiftXYPad_PadChanged(object? sender, LiftXYPadChangedEventArgs e)
        {
            trkBar_LiftHorizontal.Value = Math.Clamp(e.HorizontalValue, trkBar_LiftHorizontal.Minimum, trkBar_LiftHorizontal.Maximum);
            trkBar_LiftVertical.Value = Math.Clamp(e.VerticalValue, trkBar_LiftVertical.Minimum, trkBar_LiftVertical.Maximum);

            int horizontalSpeed = trkBar_LiftHorizontal.Value * -5;
            if (horizontalSpeed != lastHorizontalValue)
            {
                udpSendLiftHorizontalData(horizontalSpeed);
                lastHorizontalValue = horizontalSpeed;
            }

            int verticalSpeed = trkBar_LiftVertical.Value * 100;
            if (verticalSpeed != lastVerticalValue)
            {
                udpSendLiftVerticalMotorData(verticalSpeed);
                lastVerticalValue = verticalSpeed;
            }
        }

        private void LiftXYPad_PadReleased(object? sender, EventArgs e)
        {
            trkBar_LiftHorizontal.Value = 0;
            trkBar_LiftVertical.Value = 0;
            udpSendLiftHorizontalData(0);
            udpSendLiftVerticalMotorData(0);
            UdpSendLiftVerticalMessageAsync("stepmotor readData");
            lastHorizontalValue = 0;
            lastVerticalValue = 0;
        }


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
            _stopRequested = false;
            cancelAutoCentrage = false;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400); // délai avant la routine
                    await RoutineAutoCentrage();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL($"Erreur auto-centrage manuel: {ex.Message}", Color.Red);
                }
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
            _stopRequested = false;
            cancelAutoCentrage = false;
            UdpSendActuatorMessageAsync("actuator 5");
            StartManualActuatorAutoCenterTracking(5);
        }

        private void btn_actuator_25_Click(object sender, EventArgs e)
        {
            _stopRequested = false;
            cancelAutoCentrage = false;
            UdpSendActuatorMessageAsync("actuator 25");
            StartManualActuatorAutoCenterTracking(25);
        }

        private void btn_actuator_45_Click(object sender, EventArgs e)
        {
            _stopRequested = false;
            cancelAutoCentrage = false;
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
            _stopRequested = false;
            UdpSendActuatorMessageAsync("actuator down");
            StartManualActuatorAutoCenterTracking();
        }

        private void btn_Actuator_Up_Click(object sender, EventArgs e)
        {
            _stopRequested = false;
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
            double initialActuatorAngle = await RequestActuatorAngleAsync(TimeSpan.FromMilliseconds(800), cancellationToken) ?? actuatorAngle;
            bool actuatorAlreadyWithinTolerance = Math.Abs(initialActuatorAngle - target) <= delta;
            int timeoutMs = CalculateActuatorWaitTimeoutMs(target, initialActuatorAngle);
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

                    double currentActuatorAngle = await RequestActuatorAngleAsync(TimeSpan.FromMilliseconds(800), cancellationToken) ?? actuatorAngle;

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
                    if (Math.Abs(currentActuatorAngle - target) <= delta)
                    {
                        AppendTextToConsoleNL($"Actuateur dans la plage : {currentActuatorAngle} (cible {target})");
                        targetReached = true;
                        break;
                    }

                    await Task.Delay(500, cancellationToken);
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
                        if (!actuatorAlreadyWithinTolerance)
                        {
                            await Task.Delay(2000, cancellationToken);
                        }
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

        private int CalculateActuatorWaitTimeoutMs(double target, double currentAngle)
        {
            double angleDistance = Math.Abs(currentAngle - target);
            return Math.Clamp((int)Math.Round(5000 + angleDistance * 750), 10000, 40000);
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
            if (!ShouldAutoCenterDuringActuatorMove()) return;

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
            if (_isInitializingCameraSettings || device == null || comboBox_TaillePhotos.SelectedIndex < 0) return;

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
            if (_isInitializingCameraSettings || device == null || comboBox_TailleLiveView.SelectedIndex < 0) return;

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
            if (_isInitializingCameraSettings || device == null || comboBox_ExpoMode.SelectedIndex < 0) return;

            NikonEnum modeSize = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ExposureMode);
            modeSize.Index = comboBox_ExpoMode.SelectedIndex;
            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_ExposureMode, modeSize);
        }

        private void comboBox_shutterTime_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInitializingCameraSettings || _isSyncingShutterTimeComboBoxes || device == null) return;

            if (sender is not System.Windows.Forms.ComboBox sourceComboBox || sourceComboBox.SelectedIndex < 0)
            {
                return;
            }

            SyncShutterTimeComboBoxes(sourceComboBox);

            NikonEnum expoMode = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ExposureMode);
            if (expoMode.Index != 3)
            {
                MessageBox.Show("Il faut mettre le mode d'exposition à Manuel");
            }
            else
            {
                NikonEnum exposureTime = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed);
                exposureTime.Index = sourceComboBox.SelectedIndex;
                device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_ShutterSpeed, exposureTime);
            }

        }

        private void SyncShutterTimeComboBoxes(System.Windows.Forms.ComboBox sourceComboBox)
        {
            var targetComboBox = ReferenceEquals(sourceComboBox, comboBox_shutterTime)
                ? comboBox_shutterTime_2
                : comboBox_shutterTime;

            if (targetComboBox.SelectedIndex == sourceComboBox.SelectedIndex)
            {
                return;
            }

            _isSyncingShutterTimeComboBoxes = true;
            try
            {
                targetComboBox.SelectedIndex = sourceComboBox.SelectedIndex;
            }
            finally
            {
                _isSyncingShutterTimeComboBoxes = false;
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
            DateTime startedAt = DateTime.Now;
            bool focusStackWasEnabled = projet.FocusStackEnabled;
            _lastSequenceErrorMessage = string.Empty;
            ResetFocusStackNotificationTracking();
            ResetSequencePhotoNotificationTracking();

            Task.Run(async () =>
            {
                SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: true);
                string status = "Réussi";
                string errorMessage = string.Empty;
                try
                {
                    tokenSource = new CancellationTokenSource();
                    await SequencePrisePhotoTotale(tokenSource.Token, promptAfterInitialFiveDegreeMove: true);
                    if (_stopRequested)
                    {
                        status = "Échoué";
                        errorMessage = _lastSequenceErrorMessage;
                    }
                }
                catch (OperationCanceledException)
                {
                    status = "Annulé";
                    AppendTextToConsoleNL("Séquence totale de prise de photos annulée.");
                }
                catch (Exception ex)
                {
                    status = "Échoué";
                    errorMessage = ex.Message;
                    _stopRequested = true;
                    AppendTextToConsoleNL($"Erreur StartTotalPhotoSequenceWithControls: {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                }
                finally
                {
                    await SendSequenceNotificationAsync("Routine totale", startedAt, status, focusStackWasEnabled, errorMessage);
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
                    BeginCalibrationAutoCentrageOverride();
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
                    RestoreCalibrationAutoCentrageOverride();
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
            DateTime startedAt = DateTime.Now;
            bool focusStackWasEnabled = projet.FocusStackEnabled;
            ResetFocusStackNotificationTracking();
            ResetSequencePhotoNotificationTracking();

            Task.Run(async () =>
            {
                SetPhotoShootCancellationButtonVisible(true);
                string status = "Réussi";
                string errorMessage = string.Empty;
                try
                {
                    if (GetPhotoCountForCurrentSerie() == 0)
                    {
                        status = "Ignoré";
                        RegisterSequencePhotoSeries(projet.Serie, 5, 0, ignored: true);
                        AppendTextToConsoleNL("Série 5° ignorée: nombre de photos à 0. Actuateur et auto-centrage non exécutés.");
                        UpdateSequenceStatusLabels(5, 0, 0);
                        return;
                    }

                    await UdpSendActuatorMessageAsync("actuator 5");
                    if (_stopRequested)
                    {
                        status = "Échoué";
                        return;
                    }
                    await WaitForActuator(5, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    projet.Serie = 0;
                    await PrisePhotoSequenceAsync(cancellationToken);
                    if (_stopRequested)
                    {
                        status = "Échoué";
                    }
                }
                catch (OperationCanceledException)
                {
                    status = "Annulé";
                    AppendTextToConsoleNL("Séquence photo 5° annulée.");
                }
                catch (Exception ex)
                {
                    status = "Échoué";
                    errorMessage = ex.Message;
                    _stopRequested = true;
                    AppendTextToConsoleNL($"Erreur btn_prisePhotoSeq1_Click: {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                }
                finally
                {
                    await SendSequenceNotificationAsync("Série 5°", startedAt, status, focusStackWasEnabled, errorMessage);
                    SetPhotoShootCancellationButtonVisible(false);
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
            DateTime startedAt = DateTime.Now;
            bool focusStackWasEnabled = projet.FocusStackEnabled;
            ResetFocusStackNotificationTracking();
            ResetSequencePhotoNotificationTracking();

            Task.Run(async () =>
            {
                SetPhotoShootCancellationButtonVisible(true);
                string status = "Réussi";
                string errorMessage = string.Empty;
                try
                {
                    if (GetPhotoCountForCurrentSerie() == 0)
                    {
                        status = "Ignoré";
                        RegisterSequencePhotoSeries(projet.Serie, 25, 0, ignored: true);
                        AppendTextToConsoleNL("Série 25° ignorée: nombre de photos à 0. Actuateur et auto-centrage non exécutés.");
                        UpdateSequenceStatusLabels(25, 0, 0);
                        return;
                    }

                    await UdpSendActuatorMessageAsync("actuator 25");
                    if (_stopRequested)
                    {
                        status = "Échoué";
                        return;
                    }
                    await WaitForActuator(25, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    await PrisePhotoSequenceAsync(cancellationToken);
                    if (_stopRequested)
                    {
                        status = "Échoué";
                    }
                }
                catch (OperationCanceledException)
                {
                    status = "Annulé";
                    AppendTextToConsoleNL("Séquence photo 25° annulée.");
                }
                catch (Exception ex)
                {
                    status = "Échoué";
                    errorMessage = ex.Message;
                    _stopRequested = true;
                    AppendTextToConsoleNL($"Erreur btn_prisePhotoSeq2_Click: {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                }
                finally
                {
                    await SendSequenceNotificationAsync("Série 25°", startedAt, status, focusStackWasEnabled, errorMessage);
                    SetPhotoShootCancellationButtonVisible(false);
                }
            });

        }

        private async Task<int?> RequestTurntablePositionAsync(TimeSpan timeout)
        {
            var requestTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _turntablePositionTcs = requestTcs;

            try
            {
                await UdpSendTurnTableMessageAsync("Aerolithe_Asks_GetPosition");

                Task completedTask = await Task.WhenAny(requestTcs.Task, Task.Delay(timeout));
                if (completedTask == requestTcs.Task)
                {
                    return await requestTcs.Task;
                }

                return null;
            }
            finally
            {
                if (ReferenceEquals(_turntablePositionTcs, requestTcs))
                {
                    _turntablePositionTcs = null;
                }
            }
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
            DateTime startedAt = DateTime.Now;
            bool focusStackWasEnabled = projet.FocusStackEnabled;
            ResetFocusStackNotificationTracking();
            ResetSequencePhotoNotificationTracking();

            Task.Run(async () =>
            {
                SetPhotoShootCancellationButtonVisible(true);
                string status = "Réussi";
                string errorMessage = string.Empty;
                try
                {
                    if (GetPhotoCountForCurrentSerie() == 0)
                    {
                        status = "Ignoré";
                        RegisterSequencePhotoSeries(projet.Serie, 45, 0, ignored: true);
                        AppendTextToConsoleNL("Série 45° ignorée: nombre de photos à 0. Actuateur et auto-centrage non exécutés.");
                        UpdateSequenceStatusLabels(45, 0, 0);
                        return;
                    }

                    await UdpSendActuatorMessageAsync("actuator 45");
                    if (_stopRequested)
                    {
                        status = "Échoué";
                        return;
                    }
                    await WaitForActuator(45, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    await PrisePhotoSequenceAsync(cancellationToken);
                    if (_stopRequested)
                    {
                        status = "Échoué";
                    }
                }
                catch (OperationCanceledException)
                {
                    status = "Annulé";
                    AppendTextToConsoleNL("Séquence photo 45° annulée.");
                }
                catch (Exception ex)
                {
                    status = "Échoué";
                    errorMessage = ex.Message;
                    _stopRequested = true;
                    AppendTextToConsoleNL($"Erreur btn_prisePhotoSeq3_Click: {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                }
                finally
                {
                    await SendSequenceNotificationAsync("Série 45°", startedAt, status, focusStackWasEnabled, errorMessage);
                    SetPhotoShootCancellationButtonVisible(false);
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

        private void ReglagesServeurEnvoiToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using var form = new SmtpSettingsForm(appSettings);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                AppendTextToConsoleNL("Réglages SMTP sauvegardés.");
            }
        }


        private void btn_stopAutomaticFocusCapture_Click(object sender, EventArgs e)
        {
            if (_automaticFocusRoutineRunning)
            {
                RequestAutomaticFocusRoutineCancel();
                return;
            }

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

        private void StopSequences()
        {
            _manualSequenceCancellationRequested = true;
            maskFreeze = false;
            btn_freezeMask.Text = "";

            AppendTextToConsoleNL($"{GetActiveSequenceName()} cancellée par l'utilisateur.", Color.Red);

            tokenSource?.Cancel();
            _cts?.Cancel();
            _manualActuatorAutoCenterCts?.Cancel();
            cancelAutoCentrage = true;
            _stopRequested = true;
            RestoreCalibrationAutoCentrageOverride();
            SetSequenceActionControlsVisible(_volumeSequenceActionsPanel, visible: false);
            SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: false);
            SetPhotoShootCancellationButtonVisible(false);
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

        }

        private void ResetSequenceCancellationButton()
        {
            _stopRequested = false;
            _manualSequenceCancellationRequested = false;
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
            if (_isInitializingCameraSettings || device == null || comboBox_AfcPriority.SelectedIndex < 0) return;

            NikonEnum focusModes = device.GetEnum(eNkMAIDCapability.kNkMAIDCapability_AFcPriority);
            focusModes.Index = comboBox_AfcPriority.SelectedIndex;
            device.SetEnum(eNkMAIDCapability.kNkMAIDCapability_AFcPriority, focusModes);
        }

        private void comboBox_FocusAeraMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInitializingCameraSettings || device == null || comboBox_FocusAeraMode.SelectedIndex < 0) return;

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
            if (_isInitializingCameraSettings || device == null || comboBox_AFMode.SelectedIndex < 0) return;

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




        private async void btn_AutomaticMFocus_Click(object sender, EventArgs e)
        {
            if (_automaticFocusRoutineRunning)
            {
                RequestAutomaticFocusRoutineCancel();
                return;
            }

            _automaticFocusRoutineRunning = true;
            _stopRequested = false;
            SetAutomaticFocusRoutineButtonCancelState(true);

            try
            {
                await AutomaticFocusRoutine();
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur Focus de routine: {ex.Message}", Color.Red);
            }
            finally
            {
                _automaticFocusRoutineRunning = false;
                maskFreeze = false;
                btn_freezeMask.Text = "";
                SetAutomaticFocusRoutineButtonCancelState(false);
                _stopRequested = false;
            }
        }

        private void RequestAutomaticFocusRoutineCancel()
        {
            _stopRequested = true;
            maskFreeze = false;
            btn_freezeMask.Text = "";
            AppendTextToConsoleNL("Focus de routine cancellé par l'utilisateur.", Color.Red);
        }

        private void SetAutomaticFocusRoutineButtonCancelState(bool isCancel)
        {
            void Apply()
            {
                btn_AutomaticMFocus.Text = isCancel ? "Cancel" : "Focus de routine";
                btn_AutomaticMFocus.BackColor = isCancel
                    ? _automaticFocusRoutineCancelBackColor
                    : _automaticFocusRoutineNormalBackColor;
                btn_AutomaticMFocus.ForeColor = Color.White;
            }

            if (btn_AutomaticMFocus.InvokeRequired)
            {
                btn_AutomaticMFocus.Invoke((Action)Apply);
            }
            else
            {
                Apply();
            }
        }





        private void btn_liveViewStatus_Click(object sender, EventArgs e)
        {
            AppendTextToConsoleNL(liveViewStatus.ToString());
        }

        private void comboBox_LiveViewAFMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInitializingCameraSettings || device == null || comboBox_LiveViewAFMode.SelectedIndex < 0) return;

            //uint mode = device.GetUnsigned(eNkMAIDCapability.kNkMAIDCapability_AFModeAtLiveView);

        }

        private void comboBox_ImageType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInitializingCameraSettings || device == null || comboBox_ImageType.SelectedIndex < 0) return;

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
                SaveThumbnailSizeToSettings();
                await ResizePanelsAsync(panelSize);
            });


        }

        private void btn_minusSizePic_Click(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                panelSize = new Size(Math.Max(90, panelSize.Width - 20), Math.Max(70, panelSize.Height - 20));
                SaveThumbnailSizeToSettings();
                await ResizePanelsAsync(panelSize);
            });

        }

        private void ApplyThumbnailSizeFromSettings()
        {
            int width = appSettings.ThumbnailWidth > 0 ? appSettings.ThumbnailWidth : panelSize.Width;
            int height = appSettings.ThumbnailHeight > 0 ? appSettings.ThumbnailHeight : panelSize.Height;
            panelSize = new Size(Math.Max(90, width), Math.Max(70, height));
        }

        private void SaveThumbnailSizeToSettings()
        {
            appSettings.ThumbnailWidth = panelSize.Width;
            appSettings.ThumbnailHeight = panelSize.Height;
            appSettings.Save();
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
            cancelAutoCentrage = true;
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

        private void tableLayoutPanel55_Paint(object sender, PaintEventArgs e)
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
            else if (sender == trackBar_maskShrink3)
            {
                SetMaskShrinkSetting(2, trackBar_maskShrink3.Value);
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
            SetMaskShrinkSetting(2, projet.MaskShrink_3);
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
            //tabControl4.SelectedTab = tabPage16;
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
                if (TryApplySequenceImageCount(txtBox_nbrImg5deg, lbl_Serie5Angle, value => appSettings.NbrImg5Deg = value))
                {
                    UpdateSequencePadding(true);
                }
                else
                {
                    MessageBox.Show("SVP entrer un nombre valide égal ou plus grand que zéro");
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
                if (TryApplySequenceImageCount(txtBox_nbrImg25deg, lbl_Serie25Angle, value => appSettings.NbrImg25Deg = value))
                {
                    UpdateSequencePadding(true);
                }
                else
                {
                    MessageBox.Show("SVP entrer un nombre valide égal ou plus grand que zéro");
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
                if (TryApplySequenceImageCount(txtBox_nbrImg45deg, lbl_Serie45Angle, value => appSettings.NbrImg45Deg = value))
                {
                    UpdateSequencePadding(true);
                }
                else
                {
                    MessageBox.Show("SVP entrer un nombre valide égal ou plus grand que zéro");
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;

            }
        }

        private bool TryApplySequenceImageCount(System.Windows.Forms.TextBox textBox, Label angleLabel, Action<int> applyValue)
        {
            if (!int.TryParse(textBox.Text, out int valeur) || valeur < 0)
            {
                return false;
            }

            textBox.ForeColor = Color.White;
            angleLabel.Text = valeur == 0
                ? "Série ignorée"
                : (4096 / valeur).ToString() + " / " + (360 / valeur).ToString();
            applyValue(valeur);
            return true;
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
            UpdateDriveStepSettingsButton();
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
                    UpdateDriveStepSettingsButton();
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
                    UpdateMaxImagesFSButton();
                }
                // Empêche le son 'ding'
                e.SuppressKeyPress = true;
            }
        }

        private void UpdateMaxImagesFSButton()
        {
            if (btn_maxImagesFS == null) return;

            btn_maxImagesFS.Text = maxNbrPicturesAllowed.ToString();
        }

        private void btn_maxImagesFS_Click(object? sender, EventArgs e)
        {
            aerolitheTabControl2.SelectTab("tabPage25");
            aerolitheTabControl4.SelectTab("tabPage32");
            //tabControl1.SelectedTab = tabPage7;
            //tabControl2.SelectedTab = tabPage10;

            textBox_nbrPhotosFS.Focus();
            textBox_nbrPhotosFS.SelectAll();
        }

        private void btn_consoleScrollToCaret_Click(object sender, EventArgs e)
        {
            MainConsoleScrollToCarret = !MainConsoleScrollToCarret;
            btn_consoleScrollToCaret.BackColor = MainConsoleScrollToCarret ? Color.FromArgb(25, 25, 25) : Color.FromArgb(100, 100, 100);

        }

        private int lastHorizontalValue = -1;
        private int lastVerticalValue = -1;

        private int? _previousStepperCameraMotorValue = null;

        private void stepperCameraMotor_trkbar_Scroll(object sender, EventArgs e)
        {
            int currentValue = stepperCameraMotor_trkbar.Value;

            if (currentValue != _previousStepperCameraMotorValue)
            {
                _previousStepperCameraMotorValue = currentValue;
                udpSendCameraLinearMotorData(currentValue * 1000);
            }
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
            //tabControl1.SelectedTab = tabPage7;
            //tabControl2.SelectedTab = tabPage12;
            aerolitheTabControl2.SelectTab("tabPage25");
            aerolitheTabControl4.SelectTab("tabPage30");
            await PingAll();
        }



        private void Aerolithe_FormClosing(object sender, FormClosingEventArgs e)
        {
            ShutdownApplication();
        }

        private void quitterAerolitheToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Close();
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
            try { RestoreCalibrationAutoCentrageOverride(); } catch { }
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
            _specificResumeLocalRotationOverride = null;
            _specificResumeImageNumberOverride = null;
            if (projet.Serie < 0) projet.Serie = 0;
            if (projet.Serie >= angleIndexes.Length) projet.Serie = angleIndexes.Length - 1;

            string cote = lbl_CoteSerie.Text = projet.Cote == 0 ? "A" : "B";

            if (ConfirmResumeLastSuccessfulSequence(cote))
            {
                if (!await ConfirmNetworkBeforeSequenceAsync()) return;

                lbl_CoteSerie.Text = projet.Cote == 0 ? "A" : "B";
                lbl_ElevSerie.Text = angleIndexes[projet.Serie].ToString();
                ResetSequenceCancellationButton();

                Task.Run(async () =>
                {
                    SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: true);
                    try
                    {
                        tokenSource = new CancellationTokenSource();
                        await SequencePrisePhotoTotale(tokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        AppendTextToConsoleNL("Reprise à partir de la dernière séquence réussie annulée.");
                    }
                    catch (Exception ex)
                    {
                        _stopRequested = true;
                        _lastSequenceErrorMessage = ex.Message;
                        AppendTextToConsoleNL($"Erreur reprise dernière séquence: {ex.Message}");
                        ShowSequenceErrorMessage(ex);
                    }
                    finally
                    {
                        SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: false);
                    }
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



        private async void RepriseSpecifiqueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            aerolitheTabControl2.SelectTab("tabPage20");
            aerolitheTabControl3.SelectTab("tabPage28");

            _autoPingCts?.Cancel();

            if (!TryPromptSpecificResumeValues(out int serie, out int cote, out int rotation, out int imageNumber))
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                $"Reprendre la séquence à partir de ces paramètres?\nSérie {serie} ({angleIndexes[serie]}°)\nCôté {(cote == 0 ? "A" : "B")}\nNo rotation {rotation}\nImage focus stack {GetFocusStackPreviewImageName(imageNumber)}",
                "Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;
            if (!await ConfirmNetworkBeforeSequenceAsync()) return;

            projet.Serie = serie;
            projet.Cote = cote;
            projet.RotationSerieIncrement = imageNumber;
            projet.FocusSerieIncrement = 0;
            _specificResumeLocalRotationOverride = rotation;
            _specificResumeImageNumberOverride = imageNumber;

            ToggleCote(projet.Cote);
            lbl_CoteSerie.Text = projet.Cote == 0 ? "A" : "B";
            lbl_ElevSerie.Text = angleIndexes[projet.Serie].ToString(CultureInfo.InvariantCulture);
            DisplayPathsInUI();
            SavePrefsSettings();
            ResetSequenceCancellationButton();

            Task.Run(async () =>
            {
                SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: true);
                try
                {
                    tokenSource = new CancellationTokenSource();
                    await SequencePrisePhotoTotale(tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    AppendTextToConsoleNL("Reprise à partir d'un endroit spécifique annulée.");
                }
                catch (Exception ex)
                {
                    _stopRequested = true;
                    _lastSequenceErrorMessage = ex.Message;
                    AppendTextToConsoleNL($"Erreur reprise spécifique: {ex.Message}");
                    ShowSequenceErrorMessage(ex);
                }
                finally
                {
                    _specificResumeLocalRotationOverride = null;
                    _specificResumeImageNumberOverride = null;
                    SetSequenceActionControlsVisible(_totalSequenceActionsPanel, visible: false);
                }
            });
        }

        private bool TryPromptSpecificResumeValues(out int serie, out int cote, out int rotation, out int imageNumber)
        {
            serie = projet.Serie;
            cote = projet.Cote;
            rotation = projet.RotationSerieIncrement;
            imageNumber = projet.RotationSerieIncrement;

            using Form prompt = new Form
            {
                Text = "Reprise à partir d'un endroit spécifique",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                ClientSize = new Size(440, 365)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                ColumnCount = 2,
                RowCount = 8
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            int GetPhotoCountForSerie(int serieIndex)
            {
                return serieIndex switch
                {
                    0 => appSettings.NbrImg5Deg,
                    1 => appSettings.NbrImg25Deg,
                    2 => appSettings.NbrImg45Deg,
                    _ => 0
                };
            }

            int GetDefaultRotationForSerie(int serieIndex)
            {
                int photoCount = GetPhotoCountForSerie(serieIndex);
                if (photoCount <= 0) return 0;

                int localRotation = projet.RotationSerieIncrement - GetPaddingForSerie(serieIndex);
                return localRotation >= 0 && localRotation < photoCount
                    ? localRotation
                    : photoCount / 2;
            }

            int GetPaddingForSerie(int serieIndex)
            {
                return serieIndex switch
                {
                    0 => appSettings.Padding5Deg,
                    1 => appSettings.Padding25Deg,
                    2 => appSettings.Padding45Deg,
                    _ => 0
                };
            }

            var cmbSerie = new System.Windows.Forms.ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
            cmbSerie.Items.AddRange(new object[] { "0", "1", "2" });
            int initialSerie = projet.Serie >= 0 && projet.Serie < angleIndexes.Length ? projet.Serie : 0;
            cmbSerie.SelectedIndex = initialSerie;
            var cmbCote = new System.Windows.Forms.ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
            cmbCote.Items.AddRange(new object[] { "A", "B" });
            cmbCote.SelectedIndex = projet.Cote == 0 ? 0 : 1;
            var txtRotation = new System.Windows.Forms.TextBox { Dock = DockStyle.Fill, Text = GetDefaultRotationForSerie(initialSerie).ToString(CultureInfo.InvariantCulture), BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
            var txtImageNumber = new System.Windows.Forms.TextBox { Dock = DockStyle.Fill, Text = Math.Max(0, projet.RotationSerieIncrement).ToString(CultureInfo.InvariantCulture), BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
            var lblSerieInfo = new Label { Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White };
            var lblImageName = new Label { Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White };

            var btnOk = new System.Windows.Forms.Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Fill, BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var btnCancel = new System.Windows.Forms.Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill, BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 40, 40), ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 8, 0, 0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            buttons.Controls.Add(btnCancel, 1, 0);
            buttons.Controls.Add(btnOk, 2, 0);

            layout.Controls.Add(new Label { Text = "Série (0, 1, 2)", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 0);
            layout.Controls.Add(cmbSerie, 1, 0);
            layout.Controls.Add(new Label { Text = "Côté", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 1);
            layout.Controls.Add(cmbCote, 1, 1);
            layout.Controls.Add(lblSerieInfo, 0, 2);
            layout.SetColumnSpan(lblSerieInfo, 2);
            layout.Controls.Add(new Label { Text = "No rotation", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 3);
            layout.Controls.Add(txtRotation, 1, 3);
            layout.Controls.Add(new Label { Text = "Entrer le numéro de rotation, pas l'angle.", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 4);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 4), 2);
            layout.Controls.Add(new Label { Text = "No image FS", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 5);
            layout.Controls.Add(txtImageNumber, 1, 5);
            layout.Controls.Add(lblImageName, 0, 6);
            layout.SetColumnSpan(lblImageName, 2);
            layout.Controls.Add(buttons, 0, 7);
            layout.SetColumnSpan(buttons, 2);

            prompt.Controls.Add(layout);
            prompt.AcceptButton = btnOk;
            prompt.CancelButton = btnCancel;

            void updateSerieInfo()
            {
                int serieIndex = cmbSerie.SelectedIndex;
                if (serieIndex < 0 || serieIndex >= angleIndexes.Length)
                {
                    lblSerieInfo.Text = "Série invalide.";
                    return;
                }

                int photoCount = GetPhotoCountForSerie(serieIndex);
                string stepText = photoCount > 0
                    ? (360.0 / photoCount).ToString("0.#", CultureInfo.InvariantCulture) + "°"
                    : "n/a";

                string rotationText = string.Empty;
                if (photoCount > 0
                    && int.TryParse(txtRotation.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rotationIndex)
                    && rotationIndex >= 0)
                {
                    if (rotationIndex < photoCount)
                    {
                        string rotationDegrees = (rotationIndex * 360.0 / photoCount).ToString("0.#", CultureInfo.InvariantCulture);
                        rotationText = $" No rotation {rotationIndex} = {rotationDegrees}°.";
                    }
                    else
                    {
                        rotationText = $" No rotation max: {photoCount - 1}.";
                    }
                }

                lblSerieInfo.Text = $"{photoCount} photos pour cette série. {stepText} entre rotations.{rotationText}";
                lblImageName.Text = int.TryParse(txtImageNumber.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int imageNumberIndex)
                    && imageNumberIndex >= 0
                    ? $"Image focus stack: {GetFocusStackPreviewImageName(imageNumberIndex)}"
                    : "Image focus stack: n/a";
            }

            cmbSerie.SelectedIndexChanged += (_, __) =>
            {
                int serieIndex = cmbSerie.SelectedIndex;
                int photoCount = GetPhotoCountForSerie(serieIndex);
                if (photoCount > 0
                    && int.TryParse(txtRotation.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rotationIndex)
                    && rotationIndex >= photoCount)
                {
                    txtRotation.Text = (photoCount / 2).ToString(CultureInfo.InvariantCulture);
                }

                updateSerieInfo();
            };
            txtRotation.TextChanged += (_, __) => updateSerieInfo();
            txtImageNumber.TextChanged += (_, __) => updateSerieInfo();
            updateSerieInfo();

            while (prompt.ShowDialog(this) == DialogResult.OK)
            {
                serie = cmbSerie.SelectedIndex;
                if (serie < 0 || serie >= angleIndexes.Length)
                {
                    MessageBox.Show(this, "Série invalide. Choisir 0, 1 ou 2.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                cote = cmbCote.SelectedIndex == 0 ? 0 : 1;

                if (!int.TryParse(txtRotation.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out rotation) || rotation < 0)
                {
                    MessageBox.Show(this, "Numéro de rotation invalide. Entrer un nombre entier positif.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                int photoCount = GetPhotoCountForSerie(serie);
                if (photoCount <= 0)
                {
                    MessageBox.Show(this, "Nombre de photos invalide pour cette série.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                if (rotation >= photoCount)
                {
                    MessageBox.Show(this, $"Numéro de rotation invalide. Pour cette série, entrer une valeur entre 0 et {photoCount - 1}.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                if (!int.TryParse(txtImageNumber.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out imageNumber) || imageNumber < 0)
                {
                    MessageBox.Show(this, "Numéro d'image focus stack invalide. Entrer un nombre entier positif.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                return true;
            }

            return false;
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
                    int algorithmIndex = textBox == txtBox_DefaultMaskThresh3
                        ? 2
                        : textBox == txtBox_DefaultMaskThresh2 ? 1 : 0;

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

        private bool ConfirmResumeLastSuccessfulSequence(string cote)
        {
            using Form prompt = new Form
            {
                Text = "Confirmation",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                ClientSize = new Size(390, 235)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                ColumnCount = 2,
                RowCount = 6
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

            var title = new Label
            {
                Text = "Reprendre à partir de la dernière séquence réussie?",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            var btnOk = new System.Windows.Forms.Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90, Height = 30, BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var btnCancel = new System.Windows.Forms.Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 30, BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.FromArgb(40, 40, 40), Padding = new Padding(0, 6, 0, 0) };
            buttons.Controls.Add(btnOk);
            buttons.Controls.Add(btnCancel);

            layout.Controls.Add(title, 0, 0);
            layout.SetColumnSpan(title, 2);
            layout.Controls.Add(new Label { Text = "Côté", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 1);
            layout.Controls.Add(new Label { Text = cote, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 1, 1);
            layout.Controls.Add(new Label { Text = "Série", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 2);
            layout.Controls.Add(new Label { Text = $"{projet.Serie} ({angleIndexes[projet.Serie]}°)", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 1, 2);
            layout.Controls.Add(new Label { Text = "Rotation", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 3);
            layout.Controls.Add(new Label { Text = projet.RotationSerieIncrement.ToString(CultureInfo.InvariantCulture), Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 1, 3);
            layout.Controls.Add(new Label { Text = "Image focus stack", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 0, 4);
            layout.Controls.Add(new Label { Text = GetFocusStackPreviewImageName(projet.RotationSerieIncrement), Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = Color.White }, 1, 4);
            layout.Controls.Add(buttons, 0, 5);
            layout.SetColumnSpan(buttons, 2);

            prompt.Controls.Add(layout);
            prompt.AcceptButton = btnOk;
            prompt.CancelButton = btnCancel;

            return prompt.ShowDialog(this) == DialogResult.OK;
        }

        private string GetFocusStackPreviewImageName(int rotation)
        {
            string imageNameBase = string.IsNullOrWhiteSpace(projet.ImageNameBase)
                ? "Image"
                : projet.ImageNameBase;

            return $"{imageNameBase}_{rotation:D2}";
        }

        private void SetMainWindowTitle(string? baseTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(baseTitle))
            {
                _windowTitleBase = baseTitle;
            }

            Text = _windowTitleBase;
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

            if (projet.AutoCentrageActuator)
            {
                _stopRequested = false;
                cancelAutoCentrage = false;
                StartManualActuatorAutoCenterTracking();
            }
            else
            {
                _manualActuatorAutoCenterCts?.Cancel();
                cancelAutoCentrage = true;
                udpSendLiftVerticalMotorData(0);
                udpSendLiftHorizontalData(0);
                udpSendCameraLinearMotorData(0);
            }
        }

        private void btn_CalibrationAutoCentrage_Click(object sender, EventArgs e)
        {
            appSettings.CalibrationAutoCentrage = !appSettings.CalibrationAutoCentrage;
            btn_CalibrationAutoCentrage.Text = appSettings.CalibrationAutoCentrage ? "" : "";
            appSettings.Save();
        }

        private void BeginCalibrationAutoCentrageOverride()
        {
            if (appSettings.CalibrationAutoCentrage)
            {
                return;
            }

            lock (_calibrationAutoCentrageOverrideLock)
            {
                if (_calibrationAutoCentrageSavedAuto.HasValue || _calibrationAutoCentrageSavedActuator.HasValue)
                {
                    return;
                }

                _calibrationAutoCentrageSavedAuto = projet.AutoCentrage;
                _calibrationAutoCentrageSavedActuator = projet.AutoCentrageActuator;
            }

            SetAutoCentrageState(autoCentrage: false, autoCentrageActuator: false);
            AppendTextToConsoleNL("Auto-centrage de calibration désactivé temporairement.");
        }

        private void RestoreCalibrationAutoCentrageOverride()
        {
            bool? autoCentrage;
            bool? autoCentrageActuator;

            lock (_calibrationAutoCentrageOverrideLock)
            {
                autoCentrage = _calibrationAutoCentrageSavedAuto;
                autoCentrageActuator = _calibrationAutoCentrageSavedActuator;
                _calibrationAutoCentrageSavedAuto = null;
                _calibrationAutoCentrageSavedActuator = null;
            }

            if (!autoCentrage.HasValue || !autoCentrageActuator.HasValue)
            {
                return;
            }

            SetAutoCentrageState(autoCentrage.Value, autoCentrageActuator.Value);
            AppendTextToConsoleNL("Auto-centrage restauré après la séquence de photos de calibration.");
        }

        private void SetAutoCentrageState(bool autoCentrage, bool autoCentrageActuator)
        {
            void applyState()
            {
                projet.AutoCentrage = autoCentrage;
                projet.AutoCentrageActuator = autoCentrageActuator;
                btn_AutoCentrageAuto.Text = projet.AutoCentrage ? "" : "";
                btn_AutoCentrageActuator.Text = projet.AutoCentrageActuator ? "" : "";
            }

            if (InvokeRequired)
            {
                Invoke(new Action(applyState));
            }
            else
            {
                applyState();
            }
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

        private void btn_enableNetworkConsoleMess_Click(object sender, EventArgs e)
        {
            _networkConsoleMessagesEnabled = !_networkConsoleMessagesEnabled;
            UpdateNetworkConsoleMessagesButton();
        }

        private void UpdateNetworkConsoleMessagesButton()
        {
            btn_enableNetworkConsoleMess.ForeColor = _networkConsoleMessagesEnabled
                ? Color.White
                : Color.FromArgb(100, 100, 100);
        }

        private void btn_enableOscConsoleMessage_Click(object sender, EventArgs e)
        {
            _oscConsoleMessagesEnabled = !_oscConsoleMessagesEnabled;
            UpdateOscConsoleMessagesButton();
        }

        private void UpdateOscConsoleMessagesButton()
        {
            btn_enableOscConsoleMessage.ForeColor = _oscConsoleMessagesEnabled
                ? Color.White
                : Color.FromArgb(100, 100, 100);
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            //tabControl1.SelectTab("TabPage7");
            //tabControl2.SelectTab("TabPage8");
        }

        private void btn_GoAutomationPage_1_Click(object sender, EventArgs e)
        {
            //tabControl1.SelectTab("TabPage3");
            //tabControl4.SelectTab("TabPage18");
            aerolitheTabControl2.SelectTab("tabPage20");
            aerolitheTabControl3.SelectTab("tabPage28");

        }

        private void btn_GoAutomationPage_2_Click(object sender, EventArgs e)
        {
            //tabControl1.SelectTab("TabPage3");
            //tabControl4.SelectTab("TabPage18");
            aerolitheTabControl2.SelectTab("tabPage20");
            aerolitheTabControl3.SelectTab("tabPage28");
        }
        public void GoToFSFolder()
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

        private void btn_goToFSFolder_Click(object sender, EventArgs e)
        {
            GoToFSFolder();
        }

        private void btn_GoToFSFolder2_Click(object sender, EventArgs e)
        {
            GoToFSFolder();
        }
    }
}
