using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Net.Http.Json;
using Emgu.CV.CvEnum;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing;
using System.Timers;

namespace Aerolithe
{

    public partial class Aerolithe : Form
    {
        public AppSettings appSettings;
        public ProjectPreferences projet;

        private void InitClasses()
        {
            appSettings = new AppSettings();
            projet = new ProjectPreferences();
           
        }

        private void CreateNewProject()
        {

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Project Files (*.aero)|*.aero|All Files (*.*)|*.*";
                saveFileDialog.Title = "Create New Project";
                if (appSettings.ProjectPath != null)
                {
                    saveFileDialog.InitialDirectory = appSettings.ProjectPath;
                }

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    appSettings.ProjectPath = saveFileDialog.FileName;
                    string projectName = Path.GetFileNameWithoutExtension(appSettings.ProjectPath).Replace(" ", "_");


                    if (projectName.Contains("."))
                    {
                        MessageBox.Show("The project name should not contain dots. Please choose a different name.", "Invalid Project Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!File.Exists(appSettings.ProjectPath))
                    {
                        File.Create(appSettings.ProjectPath).Dispose();
                    }

                    string projectDirectory = Path.GetDirectoryName(appSettings.ProjectPath);
                    btn_goToProjectFolder.Enabled = true;
                    lbl_projectPath.Text = $"{Path.GetFileName(projectDirectory)}/{Path.GetFileName(appSettings.ProjectPath)}";


                    SetImageFolder();
                    SavePrefsSettings();
                }
            }
        }

        private void SetVariables()
        {
            serieId = new int[] { int.Parse(txtBox_nbrImg5deg.Text), int.Parse(txtBox_nbrImg25deg.Text), int.Parse(txtBox_nbrImg45deg.Text) };
        }

        private void SelectExistingProject()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Project Files (*.aero)|*.aero|All Files (*.*)|*.*";
                openFileDialog.Title = "Select Project File";
                if (appSettings.ProjectPath != null)
                {
                    openFileDialog.InitialDirectory = appSettings.ProjectPath;
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    appSettings.ProjectPath = openFileDialog.FileName;
                    //Debug.WriteLine(appSettings.ProjectPath);
                    OpenProject(appSettings.ProjectPath);
                    SavePrefsSettings();
                }
            }
        }

        public void OpenProject(string path)
        {
            string projectDirectory = Path.GetDirectoryName(appSettings.ProjectPath);
            btn_goToProjectFolder.Enabled = true;
            lbl_projectPath.Text = $"{Path.GetFileName(projectDirectory)}/{Path.GetFileName(appSettings.ProjectPath)}" ;
            txtBox_nomImages.Text = projet.ImageNameBase.Split("-")[0];
            btn_goToImageFolder.Enabled = true;            
            
            try
            {
                stepSize = projet.StepSize;
                hScrollBar_driveStep.Value = stepSize;
                txtBox_DriveStep.Text = stepSize.ToString();
                maxNbrPicturesAllowed = projet.MaxPicturesAllowed;
                if (maxNbrPicturesAllowed == 0) maxNbrPicturesAllowed = 15;
                textBox_nbrPhotosFS.Text = maxNbrPicturesAllowed.ToString();
            }
            catch { }

        }

        public void SavePrefsSettings()
        {
            if (appSettings.ProjectPath == null)
            {
                MessageBox.Show("Svp sauvegarder ou ouvrir un projet");
                return;
            }
            // Pas besoin ici???
            // projet.ImageNameBase = txtBox_nomImages.Text + "-" + lbl_SerieIncrement.Text;
            appSettings.Save();
            projet.Save(appSettings.ProjectPath);
        }

        private void SetImageFolder()
        {
            if (appSettings.ProjectPath == null)
            {
                CreateNewProject();
            }


            if (!Directory.Exists(projet.ImageFolderPath))
            {
                Directory.CreateDirectory(projet.ImageFolderPath);
            }

            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select or create the folder to save images in";
                folderDialog.SelectedPath = projet.ImageFolderPath;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    projet.ImageFolderPath = folderDialog.SelectedPath;
                    btn_goToImageFolder.Enabled = true;
                    lbl_ImgFolderPath.Text = projet.ImageFolderPath + "\\";
                    SavePrefsSettings();
                }
            }

        }

        public void SetStackedImageFolderPath()
        {
            if (appSettings.ProjectPath == null)
            {
                CreateNewProject();
            }
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select or create the folder to save images in";
                folderDialog.SelectedPath = projet.ImageFolderPath;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    projet.FocusStackPath = folderDialog.SelectedPath;
                    lbl_StackedPath.Text = projet.FocusStackPath;
                    SavePrefsSettings();
                }


            }
        }

        private void PopulateColorConversionDropdown()
        {
            comboBox_EmguConversion.Items.Clear();
            foreach (ColorConversion conversion in Enum.GetValues(typeof(ColorConversion)))
            {
                comboBox_EmguConversion.Items.Add(conversion);
            }
            comboBox_EmguConversion.SelectedIndex = 12;
        }

        private void UpdateSequencePadding()
        {
            int pad1, pad2, pad3, qte1, qte2, qte3;
            qte1 = int.Parse(txtBox_nbrImg5deg.Text);
            qte2 = int.Parse(txtBox_nbrImg25deg.Text);
            qte3 = int.Parse(txtBox_nbrImg45deg.Text);
            pad1 = int.Parse(txtBox_seqPad1.Text);
            pad2 = qte1 + pad1;
            pad3 = qte2 + pad2;
            txtBox_seqPad2.Text = pad2.ToString();
            txtBox_seqPad3.Text = pad3.ToString();
            txtBox_seqPad1.ForeColor = Color.White;
            txtBox_seqPad2.ForeColor = Color.White;
            txtBox_seqPad3.ForeColor = Color.White;

        }

        private void PopulateColorColorDropdown()
        {

            comboBox_EmguColor.Items.Add("Rouge");
            comboBox_EmguColor.Items.Add("Vert");
            comboBox_EmguColor.Items.Add("Bleu");
            comboBox_EmguColor.Items.Add("Gris");

            comboBox_EmguColor.SelectedIndex = 3;
        }

       

        private void PreparationDossierDestTemp()
        {
            projet.TempImageFolderPath = Path.Combine(projet.ImageFolderPath, projet.ImageNameBase.Split("-")[0], projet.SerieIncrement.ToString("D2"));
            if (!Directory.Exists(projet.TempImageFolderPath))
            {
                Directory.CreateDirectory(projet.TempImageFolderPath);
            }            
            
        }

        // projet.SerieIncrement "-**"  -> pour chaque rotation de la table tournante
        // projet.ImageIncrement "_**"  -> pour chaque photo du focusStack


        private void PreparationNomImage()
        {
            //MessageBox.Show(projet.SerieIncrement.ToString());
            oldImgIncr = projet.ImageIncrement;
           

            projet.ImageNameBase = txtBox_nomImages.Text + "-" + projet.SerieIncrement.ToString("D2");
            projet.ImageNameFull = projet.ImageNameBase + "_" + projet.ImageIncrement.ToString("D2") + ".jpg";
            projet.ImageFullPath = Path.Combine(projet.TempImageFolderPath, projet.ImageNameFull);

            if (lbl_FullImageName.InvokeRequired)
            {
                lbl_FullImageName.Invoke(new Action(() =>
                {
                    lbl_FullImageName.Text = projet.ImageNameFull;
                }));
            }
            else
            {
                lbl_FullImageName.Text = projet.ImageNameFull;
            }          
            
            projet.Save(appSettings.ProjectPath);
            projet.ImageIncrement++;
        }

        private async Task AssembleImageName()
        {
            if (lbl_FullImageName.InvokeRequired)
            {
                lbl_FullImageName.Invoke(new Action(() =>
                {
                    projet.ImageNameBase = txtBox_nomImages.Text + "-" + projet.SerieIncrement.ToString();
                    lbl_FullImageName.Text = projet.ImageNameBase + "_**.jpg";
                }));
            }
            else
            {
                projet.ImageNameBase = txtBox_nomImages.Text + "-" + projet.SerieIncrement.ToString();
                lbl_FullImageName.Text = projet.ImageNameBase + "_**.jpg";
            }
            projet.Save(appSettings.ProjectPath);
        }



        private async Task ResetIncrementation()
        {
            oldImgIncr = 0;
            projet.ImageIncrement = 0;
            AssembleImageName();
            projet.Save(appSettings.ProjectPath);
                     
        }

        private void ResetSerieIncrement()
        {
            lbl_SerieIncrement.Text = "00";
            projet.SerieIncrement = 0;
            AssembleImageName();
            projet.Save(appSettings.ProjectPath);
        }

        public async Task IncrementImgSeq()
        {
            int inc;
            if (int.TryParse(lbl_SerieIncrement.Text, out inc))
            {                
                projet.SerieIncrement = inc + 1;
                if (lbl_SerieIncrement.InvokeRequired)
                {
                    lbl_SerieIncrement.Invoke(new Action(() =>{
                        lbl_SerieIncrement.Text = projet.SerieIncrement.ToString("D2");
                    }));
                }
                AssembleImageName();
                await Task.Delay(50);
                projet.Save(appSettings.ProjectPath);
                await Task.Delay(100);
            }
        }

        public async Task DecrementImgSeq()
        {
            int inc;
            if (int.TryParse(lbl_SerieIncrement.Text, out inc))
            {
                if (inc > 0)
                {                   
                    projet.SerieIncrement = inc - 1;
                    if (lbl_SerieIncrement.InvokeRequired){
                        lbl_SerieIncrement.Invoke(new Action(() =>
                        {
                            lbl_SerieIncrement.Text = projet.SerieIncrement.ToString("D2");
                        }));
                    }
                    AssembleImageName();
                    projet.Save(appSettings.ProjectPath);
                }
            }
        }
        
        public void DeleteAllPicturesInFolderWithPrompt()
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
                    string[] imageFiles = Directory.GetFiles(projet.ImageFolderPath, "*.jpg"); // ou *.png, *.jpeg, etc.
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

        public void DeleteAllPicturesInFolderWith()
        {
            flowLayoutPanel1.Controls.Clear();

            try
            {
                string[] imageFiles = Directory.GetFiles(projet.ImageFolderPath, "*.jpg"); // ou *.png, *.jpeg, etc.
                foreach (string file in imageFiles)
                {
                    File.Delete(file);
                }

                //MessageBox.Show("Toutes les images ont été supprimées avec succès.", "Suppression terminée", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la suppression des fichiers : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void ToolTipsSetup()
        {
            ToolTip toolTip = new ToolTip();

            // Configuration générale (facultatif)
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 200;
            toolTip.ReshowDelay = 200;
            toolTip.ShowAlways = true;

            // ToolTips spécifiques
            toolTip.SetToolTip(textBox_FocusIterations, "Nombre de steps effectués d'un côté ou l'autre à partir de la position après l'autofocus");
            toolTip.SetToolTip(textBox_FocusFreqSpeed, "Délai en millisecondes entre chaque focus.");
            toolTip.SetToolTip(lbl_ResBlurDetect, "Résolution de la netteté, généralement autour de 100.");
            toolTip.SetToolTip(lbl_BlockAmountBlurDetet, "Grosseur des carrés de détection, les valeurs étant 16,32,64 ou 128");
            toolTip.SetToolTip(textBox_minDetect, "Seuil minimum de détections pour considérer une image d'avoir une partie nette.");
            toolTip.SetToolTip(textBox_nbrPhotosFS, "Seuil maximal de photos prises lors d'un focus stack");

        }

    }

    // Les appSettings du projet 
    public class ProjectPreferences
    {


        private string _imageNameBase;
        private string _imageNameFull;
        private string _imageFolderPath;
        private string _tempImageFolderPath;
        private string _imageFullPath;
        private string _focusStackPath;
        private int _maxPicturesAllowed;

        // image increment => nomImage-00_**.jpg  (**)
        private int _imageIncrement;
        // serie increment => nomImage-**_00.jpg  (**)
        private int _serieIncrement;

        private int _stepSize;

        public int  MaxPicturesAllowed
        {
            get {  return _maxPicturesAllowed; }
            set { _maxPicturesAllowed = value; }
        }

        public int StepSize
        {
            get { return _stepSize; }
            set
            {
                _stepSize = value;
            }
        }

        public string ImageNameBase
        {
            get => _imageNameBase;
            set
            {
                _imageNameBase = value;
                //Debug.WriteLine(_imageNameBase);
                
            }
        }
        public string ImageNameFull
        {
            get => _imageNameFull;
            set
            {
                _imageNameFull = value;
                //Debug.WriteLine(_imageNameFull);
            }
        }
        public string ImageFolderPath
        {
            get => _imageFolderPath;
            set
            {
                _imageFolderPath = value;
                //Debug.WriteLine(_imageFolderPath);
            }
        }
        public string TempImageFolderPath
        {
            get => _tempImageFolderPath;
            set
            {
                _tempImageFolderPath = value;
                //Debug.WriteLine(_tempImageFolderPath);
            }
        }
        public string ImageFullPath
        {
            get => _imageFullPath;
            set
            {
                _imageFullPath = value;
                //Debug.WriteLine(_imageFullPath);
            }
        }
        public string FocusStackPath
        {
            get =>_focusStackPath;
            set
            {
                _focusStackPath = value;
            }
        }
        public int ImageIncrement
        {
            get => _imageIncrement;
            set 
            {
                _imageIncrement = value;
            }
        }        
        public int SerieIncrement
        {
            get => _serieIncrement;
            set
            {
                _serieIncrement = value;
            }

        }

        public ProjectPreferences Load(string filePath)
        {

            if (!File.Exists(filePath))
            {
                var defaultPrefs = new ProjectPreferences();
                defaultPrefs.Save(filePath);
                return defaultPrefs;
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ProjectPreferences>(json) ?? new ProjectPreferences();

        }

        public void Save(string filePath)
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
    }

    // Les appSettings de l'application Aerolithe
    public class AppSettings 
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Aerolithe"
        );

        private static readonly string SettingsFilePath = Path.Combine(FolderPath, "appSettings.json");

        public string ProjectPath { get; set; }

        public int MaskIntensity { get; set; }

        public int NbrImg5Deg { get; set; }

        public int NbrImg25Deg { get; set; }

        public int NbrImg45Deg { get; set; }

        public int VerticalLiftCurrentPos {  get; set; }

        public int VerticalLiftMaxPos { get; set; } 

        public int VerticalLiftDefaultPos { get; set; }

        public AppSettings Load()
        {
            // Crée le dossier Aerolithe s'il n'existe pas
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            // Si le fichier n'existe pas, créer des paramètres par défaut
            if (!File.Exists(SettingsFilePath))
            {
                var defaultSettings = new AppSettings();
                defaultSettings.Save();
                return defaultSettings;
            }

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                return settings;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors du chargement des paramètres : " + ex.Message);
                return new AppSettings(); // Retourne des paramètres par défaut en cas d'erreur
            }
        }

        public void Save()
        {
            // Assure que le dossier existe avant d'écrire
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
        }
    }


public class TimerController
    {
        private System.Timers.Timer _timer;
        private TimeSpan _elapsedTime;
        private Label _lblTimeDisplay;
        private readonly int _interval = 1000; // 1 seconde

        public TimerController(Label lblTimeDisplay)
        {
            _lblTimeDisplay = lblTimeDisplay;
            _timer = new System.Timers.Timer(_interval);
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = true;
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            _elapsedTime = _elapsedTime.Add(TimeSpan.FromSeconds(1));
            string timeStr = $"{_elapsedTime.Hours:D2}:{_elapsedTime.Minutes:D2}:{_elapsedTime.Seconds:D2}";

            // Mise à jour du label via Invoke
            if (_lblTimeDisplay.InvokeRequired)
            {
                _lblTimeDisplay.Invoke(new Action(() => _lblTimeDisplay.Text = timeStr));
            }
            else
            {
                _lblTimeDisplay.Text = timeStr;
            }
        }

        //public async Task ControlTimerAsync(string action)
        //{
        //    await Task.Run(() =>
        //    {
        //        switch (action.ToLower())
        //        {
        //            case "lancer":
        //                if (!_timer.Enabled)
        //                {
        //                    _timer.Start();
        //                }
        //                break;

        //            case "stopper":
        //                if (_timer.Enabled)
        //                {
        //                    _timer.Stop();
        //                }
        //                break;

        //            case "resetter":
        //                _timer.Stop();
        //                _elapsedTime = TimeSpan.Zero;
        //                if (_lblTimeDisplay.InvokeRequired)
        //                {
        //                    _lblTimeDisplay.Invoke(new Action(() => _lblTimeDisplay.Text = ""));
        //                }
        //                else
        //                {
        //                    _lblTimeDisplay.Text = "";
        //                }
        //                break;

        //            default:
        //                throw new ArgumentException("Action non reconnue. Utilisez 'lancer', 'stopper' ou 'resetter'.");
        //        }
        //    });
        //}
        public async Task ControlTimerAsync(string action)
        {
            switch (action.ToLower())
            {
                case "lancer":
                    if (!_timer.Enabled)
                    {
                        _timer.Start();
                    }
                    break;

                case "stopper":
                    if (_timer.Enabled)
                    {
                        _timer.Stop();
                    }
                    break;

                case "resetter":
                    _timer.Stop();
                    _elapsedTime = TimeSpan.Zero;
                    if (_lblTimeDisplay.InvokeRequired)
                    {
                        _lblTimeDisplay.Invoke(new Action(() => _lblTimeDisplay.Text = ""));
                    }
                    else
                    {
                        _lblTimeDisplay.Text = "";
                    }
                    break;

                default:
                    throw new ArgumentException("Action non reconnue. Utilisez 'lancer', 'stopper' ou 'resetter'.");
            }

            await Task.CompletedTask; // Pour respecter la signature async
        }
    }


}
