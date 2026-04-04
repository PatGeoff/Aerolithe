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
using System.Diagnostics.Eventing.Reader;
using System.Xml.Linq;
using static Emgu.CV.DISOpticalFlow;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using System.Collections.Concurrent;
using System.DirectoryServices.ActiveDirectory;

namespace Aerolithe
{

    public partial class Aerolithe : Form
    {
        public AppSettings appSettings;
        public ProjectPreferences projet;
        public tempData tmpData;

        private void InitClasses()
        {
            appSettings = new AppSettings();
            projet = new ProjectPreferences();
            tmpData = new tempData();

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
                    CreateAllFolders(Path.GetDirectoryName(appSettings.ProjectPath));

                    lbl_focusStackOutputDest.Text = projet.GetFocusStackPath();
                    string nom = Path.GetFileName(appSettings.ProjectPath).Split('.')[0];
                    projet.ImageNameBase = nom;
                    string name = appSettings.ProjectPath;
                    this.Text = name;

                    SavePrefsSettings();
                    AssembleImageName();

                }
            }
        }

        private void CreateAllFolders(string projectDirectory)
        {

            projet.ImageFolderPath = Path.Combine(projectDirectory, "images");
            if (!Directory.Exists(projet.ImageFolderPath))
            {
                Directory.CreateDirectory(projet.ImageFolderPath);
            }
            var imagesFolderNameSides = Path.Combine(projet.ImageFolderPath, "serie_A");
            if (!Directory.Exists(imagesFolderNameSides))
            {
                Directory.CreateDirectory(imagesFolderNameSides);
            }           
            imagesFolderNameSides = Path.Combine(projet.ImageFolderPath, "serie_B");
            if (!Directory.Exists(imagesFolderNameSides))
            {
                Directory.CreateDirectory(imagesFolderNameSides);
            }
            imagesFolderNameSides = Path.Combine(projet.ImageFolderPath, "noFS", "serie_A");
            if (!Directory.Exists(imagesFolderNameSides))
            {
                Directory.CreateDirectory(imagesFolderNameSides);
            }
            imagesFolderNameSides = Path.Combine(projet.ImageFolderPath, "noFS", "serie_B");
            if (!Directory.Exists(imagesFolderNameSides))
            {
                Directory.CreateDirectory(imagesFolderNameSides);
            }
            imagesFolderNameSides = Path.Combine(projet.ImageFolderPath, "mesures", "serie_A");
            if (!Directory.Exists(imagesFolderNameSides))
            {
                Directory.CreateDirectory(imagesFolderNameSides);
            }
            imagesFolderNameSides = Path.Combine(projet.ImageFolderPath, "mesures", "serie_B");
            if (!Directory.Exists(imagesFolderNameSides))
            {
                Directory.CreateDirectory(imagesFolderNameSides);
            }   
            
            var focusStackFolderName = Path.Combine(projet.ImageFolderPath, "focusStack");
            projet.FocusStackFolderName = focusStackFolderName;

            if (!Directory.Exists(focusStackFolderName))
            {
                Directory.CreateDirectory(focusStackFolderName);
            }
            var focusStackFolderNameA = Path.Combine(focusStackFolderName, "focusStack_A");
            if (!Directory.Exists(focusStackFolderNameA))
            {
                Directory.CreateDirectory(focusStackFolderNameA);
            }
            var focusStackFolderNameB = Path.Combine(focusStackFolderName, "focusStack_B");
            if (!Directory.Exists(focusStackFolderNameB))
            {
                Directory.CreateDirectory(focusStackFolderNameB);
            }

            var maskFolderName = Path.Combine(focusStackFolderName, "masques_A");
            if (!Directory.Exists(maskFolderName))
            {
                Directory.CreateDirectory(maskFolderName);
            }

            maskFolderName = Path.Combine(focusStackFolderName, "masques_B");
            if (!Directory.Exists(maskFolderName))
            {
                Directory.CreateDirectory(maskFolderName);
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
            //string projectDirectory = Path.GetDirectoryName(appSettings.ProjectPath);
            //lbl_projectPath.Text = $"{Path.GetFileName(projectDirectory)}/{Path.GetFileName(appSettings.ProjectPath)}";
            this.Text = appSettings.ProjectPath;


            try
            {
                projet.Load(appSettings.ProjectPath);
                stepSize = projet.StepSize;
                hScrollBar_driveStep.Value = stepSize;
                txtBox_DriveStep.Text = stepSize.ToString();
                maxNbrPicturesAllowed = projet.MaxPicturesAllowed;
                if (maxNbrPicturesAllowed == 0) maxNbrPicturesAllowed = 15;
                textBox_nbrPhotosFS.Text = maxNbrPicturesAllowed.ToString();
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur d'ouverture: " + ex.Message);
            }

        }

        public void SavePrefsSettings()
        {
            if (appSettings.ProjectPath == null)
            {
                MessageBox.Show("Svp sauvegarder ou ouvrir un projet");
                return;
            }
            // Pas besoin ici???
            appSettings.Save();
            projet.Save(appSettings.ProjectPath);
        }

        private void SetImageFolder()
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
                    projet.ImageFolderPath = folderDialog.SelectedPath;
                    SavePrefsSettings();
                    CreateAllFolders(projet.ImageFolderPath);
                    AssembleImageName();
                }
            }
        }

        public void SetFocusStackFolder()
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
                    projet.FocusStackFolderName = folderDialog.SelectedPath;
                    lbl_StackedPath.Text = projet.GetFocusStackPath();
                    SavePrefsSettings();
                }
            }
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

        private void PreparationDossierDestTemp()
        {
            AppendTextToConsoleNL("PreparationDossierDestTemp");
            //projet.TempImageFolderPath = Path.Combine(projet.ImageFolderPath, projet.ImageNameBase.Split("-")[0], projet.RotationSerieIncrement.ToString("D2"));
            if (!Directory.Exists(projet.GetTempImageFolderPath()))
            {
                Directory.CreateDirectory(projet.GetTempImageFolderPath());
            }

        }

        // projet.RotationSerieIncrement "-**"  -> pour chaque rotation de la table tournante
        // projet.FocusSerieIncrement "_**"  -> pour chaque photo du focusStack


        private void PreparationNomImage()
        {
            AppendTextToConsoleNL("PreparationNomImage");
            //MessageBox.Show(projet.RotationSerieIncrement.ToString());
            oldImgIncr = projet.FocusSerieIncrement;


            //projet.ImageNameBase = txtBox_nomImages.Text + "-" + projet.RotationSerieIncrement.ToString("D2");
            //projet.ImageNameFull = projet.ImageNameBase + "_" + projet.FocusSerieIncrement.ToString("D2") + ".jpg";
            //projet.ImageFullPath = Path.Combine(projet.TempImageFolderPath, projet.ImageNameFull);


            projet.Save(appSettings.ProjectPath);
            projet.FocusSerieIncrement++;
           
        }

        private void AssembleImageName()
        {
            AppendTextToConsoleNL(" - AssembleImageName()");
            try
            {
                if (lbl_ImgFullPath.InvokeRequired)
                {
                    lbl_ImgFullPath.Invoke(new Action(() =>
                    {
                        lbl_ImgFullPath.Text = projet.GetImageFullPath();
                        lbl_StackedPath.Text = projet.GetFocusStackPath();
                    }));
                }
                else
                {
                    lbl_ImgFullPath.Text = projet.GetImageFullPath();
                    lbl_StackedPath.Text = projet.GetFocusStackPath();
                }
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL(ex.Message);

            }

        }


        private void UpdateFocusStackImageFolder()
        {

        }

        private async Task ResetIncrementation()
        {            
            AppendTextToConsoleNL(" * ResetIncrementation()");

            try
            {
                oldImgIncr = 0;
                projet.FocusSerieIncrement = 0;
                AssembleImageName();
                projet.Save(appSettings.ProjectPath);
            }
            catch (Exception e)
            {
                AppendTextToConsoleNL(e.Message);
            }

        }

        private void ResetSerieIncrement()
        {
            AppendTextToConsoleNL(" * ResetIncrementation()");
            try
            {
                projet.RotationSerieIncrement = 0;
                projet.FocusSerieIncrement = 0;
                AssembleImageName();
                projet.Save(appSettings.ProjectPath);
            }
            catch (Exception e)
            {
                AppendTextToConsoleNL(e.Message);
            }
        }

        public async Task IncrementImgSeq()
        {
            projet.RotationSerieIncrement += 1;
            AssembleImageName();
            await Task.Delay(50);
            projet.Save(appSettings.ProjectPath);
            await Task.Delay(100);

        }

        public async Task DecrementImgSeq()
        {
            if (projet.RotationSerieIncrement > 0)
            {
                projet.RotationSerieIncrement -= 1;
                AssembleImageName();
                await Task.Delay(50);
                projet.Save(appSettings.ProjectPath);
                await Task.Delay(100);
            }
        }

        public void DeleteAllPicturesInFolderWithPrompt()
        {
            
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
                    flowLayoutPanel1.Controls.Clear();
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
            toolTip.InitialDelay = 50;
            toolTip.ReshowDelay = 50;
            toolTip.ShowAlways = true;

            // ToolTips spécifiques
            toolTip.SetToolTip(textBox_FocusIterations, "Nombre de steps effectués d'un côté ou l'autre à partir de la position après l'autofocus");
            toolTip.SetToolTip(textBox_FocusFreqSpeed, "Délai en millisecondes entre chaque focus.");
            toolTip.SetToolTip(lbl_ResBlurDetect, "Résolution de la netteté, généralement autour de 100.");
            toolTip.SetToolTip(lbl_BlockAmountBlurDetet, "Grosseur des carrés de détection, les valeurs étant 16,32,64 ou 128");
            toolTip.SetToolTip(textBox_minDetect, "Seuil minimum de détections pour considérer une image d'avoir une partie nette.");
            toolTip.SetToolTip(textBox_nbrPhotosFS, "Seuil maximal de photos prises lors d'un focus stack");
            toolTip.SetToolTip(lbl_FreezeMask, "Freeze le masque ci-haut");
            toolTip.SetToolTip(btn_freezeMask, "Freeze le masque ci-haut");
            toolTip.SetToolTip(lbl_FocusStackEnable, "Active le Focus Stacking");
            toolTip.SetToolTip(btn_focusStack, "Active le Focus Stacking");
            toolTip.SetToolTip(lbl_applyMaskFS, "Applique le masque à chaque image et la sauvegarde ainsi dans ../images/focusstack/focusstack_A ou focusstack_B");
            toolTip.SetToolTip(btn_applyMask, "Applique le masque à chaque image et la sauvegarde ainsi dans ../images/focusstack/focusstack_A ou focusstack_B");
            toolTip.SetToolTip(btn_SaveImageToDisk, "Sauvegarde des image sur disque. Essentiel");
            toolTip.SetToolTip(lbl_saveImageTodisk, "Sauvegarde des image sur disque. Essentiel");
            toolTip.SetToolTip(btn_saveImageForMesurements, $"Permet la sauvegarde D'UNE image de pour la mesure mais il faut appuyer sur Prendre une photo.\n ATTENTION: Différent du bouton dans l'onglet Caméra\nL'image se retrouvera dans {projet.GetMesurementsFolderpath()}\" ");
            toolTip.SetToolTip(lbl_saveImageForMesurements, $"Permet la sauvegarde D'UNE image de pour la mesure mais il faut appuyer sur Prendre une photo.\n ATTENTION: Différent du bouton dans l'onglet Caméra\nL'image se retrouvera dans {projet.GetMesurementsFolderpath()}\" ");
            toolTip.SetToolTip(lbl_LiveViewEnable, "Active/Désactive le Live View");
            toolTip.SetToolTip(btn_LiveViewEnable, "Active/Désactive le Live View");
            toolTip.SetToolTip(lbl_saveImageForMesurementSequence, $"Sauvegarde automatique DES images pour mesure.\n ATTENTION: Différent du bouton sous le masque\nLes images se retrouveront dans {projet.GetMesurementsFolderpath()}");
            toolTip.SetToolTip(btn_saveImageForMesurementSequence, $"Sauvegarde automatique DES images pour mesure.\n ATTENTION: Différent du bouton sous le masque\nLes images se retrouveront dans {projet.GetMesurementsFolderpath()}");

        }



        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string lpFileName);

        void TestLoadNikonDlls()
        {
            string baseDir = AppContext.BaseDirectory;
            string[] files = { "NkdPTP.dll", "dnssd.dll", "NkRoyalmile.dll" };
            foreach (var f in files)
            {
                string p = Path.Combine(baseDir, "MyResources", "NikonLibs", f); // adapte si nécessaire
                IntPtr h = LoadLibrary(p);
                if (h == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    AppendTextToConsoleNL($"{f} LoadLibrary FAILED (Win32 {err})\nPath: {p}");
                }
                else
                {
                    AppendTextToConsoleNL($"{f} LoadLibrary OK\nPath: {p}");
                }
            }
        }
        private string PromptCreateUser()
        {

            string username = Interaction.InputBox(
                    "Veuillez entrer le courriel de l'utilisateur :", // message
                    "Ajout d'un utilisateur",                         // titre
                    ""                                                // valeur par défaut
                );

            return username.Trim();

        }

        private void CreateUser(string userName)
        {
            string _userName = userName ?? string.Empty;
            Panel panel = new Panel
            {
                Size = new Size(432, 26),
                BorderStyle = BorderStyle.FixedSingle
            };

            TableLayoutPanel tbl = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Fill
            };


            // Largeur des colonnes : 26 | (reste) | 26
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));   // Col 0 : 26 px
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // Col 1 : 100% du reste
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));   // Col 2 : 26 px

            // Hauteur de la (seule) ligne : 26 px
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            panel.Controls.Add(tbl);

            CheckBox ckb = new CheckBox
            {
                Checked = true,
                Text = "",
                Padding = new Padding(5)
            };

            Label lbl = new Label
            {
                Text = _userName,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericSansSerif, 12)
            };

            Button dB = new Button
            {

                BackgroundImage = Properties.Resources.echec,
                BackgroundImageLayout = ImageLayout.Zoom,
                Size = new Size(12, 12),
                AutoSize = false,
                Dock = DockStyle.None,                 // ✅ ne pas remplir la cellule
                Anchor = AnchorStyles.None,            // ✅ centré automatiquement dans la cellule
                Margin = new Padding(0),               // évite un décalage
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false

            };

            dB.FlatAppearance.BorderSize = 0;
            dB.FlatAppearance.BorderColor = Color.Black;

            dB.Click += (s, e) =>
            {
                var result = MessageBox.Show(
                    "Voulez-vous supprimer l'utilisateur de la liste d'envoi?",
                    "Supression du courrielà envoyer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        flowlayoutPanel_Messagerie.Controls.Remove(panel);
                        panel.Dispose();

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de la suppression du fichier : {ex.Message}");
                    }
                }
            };

            tbl.Controls.Add(ckb, 0, 0);
            tbl.Controls.Add(lbl, 1, 0);
            tbl.Controls.Add(dB, 2, 0);

            flowlayoutPanel_Messagerie.Controls.Add(panel);
        }
    }

    // Les appSettings du projet 

    public class ProjectPreferences
    {
        // --- Propriétés essentielles ---
        public string ImageNameBase { get; set; }           // ex: "GrosseRoche_2"
        public int RotationSerieIncrement { get; set; }     // ex: 49
        public int FocusSerieIncrement { get; set; }        // ex: 62

        public string ImageFolderPath { get; set; }         // ex: "C:\\Projet\\images"
        public string FocusStackFolderName { get; set; }    // ex: "focusStack"
                                                            // 
        public string MeasurementsFolderPath { get; set; }     
        public int MaxPicturesAllowed { get; set; } = 30;        // Paramètre global
        public int StepSize { get; set; } = 30;                // Paramètre global
        public int Cote { get; set; } = 0;                      // 0 = B, 1 = A

        public int Serie { get; set; } = 0;                     // Série 0 = 5° , etc

        public bool ApplyMask { get; set; } = true;

        public bool MaskSave { get; set; } = true;

        public bool SaveImageToDisk { get; set; } = true;

        public bool ViewSharpnessOverlay { get; set; } = true;

        public int PictureWidth { get; set; }
        public int PictureHeight { get; set; }

        public bool FocusStackEnabled   { get; set; } = true;

        public bool SaveImageForMesurements { get; set; } = false;

        public bool LiveViewEnabled { get; set; } = true;

        // --- Méthodes utilitaires ---
        public string GetImageFullPath()
        {
            return Path.Combine(GetTempImageFolderPath(), GetImageNameFull());
        }
        public string GetImageFullPathNoFS()
        {
            return Path.Combine(GetImageFolderPathNoFS(), GetImageNameFull());
        }

       
       
        public string GetImageNameFull()
        {
            // Format: Base_Rotation_Focus.jpg
            string cote = (Cote == 0) ? "A" : "B";
            return $"{ImageNameBase}_{cote}_{RotationSerieIncrement:D2}_{FocusSerieIncrement:D2}.jpg";
        }

        public string GetTempImageFolderPath()
        {
            // ex: C:\Projet\images\GrosseRoche_2\49
            string coteFolder = (Cote == 0) ? "serie_A" : "serie_B";
            return Path.Combine(ImageFolderPath, coteFolder, RotationSerieIncrement.ToString("D2"));
        }

        public string GetImageFolderPathNoFS()
        {
            string coteFolder = (Cote == 0) ? "serie_A" : "serie_B";
            return Path.Combine(ImageFolderPath, "noFS", coteFolder);
        }

        public string GetMesurementsFullImagePath()
        {
            return Path.Combine(GetMesurementsFolderpath(), GetImageNameFull());
        }
        public string GetMesurementsFolderpath()
        {
            string coteFolder = (Cote == 0) ? "serie_A" : "serie_B";
            return Path.Combine(ImageFolderPath, "mesures", coteFolder);
        }

        public string GetFocusStackPath()
        {
            // ex: C:\Projet\images\focusStack_A
            string coteFolder = (Cote == 0) ? "focusStack_A" : "focusStack_B";
            return Path.Combine(ImageFolderPath, $"{FocusStackFolderName}", coteFolder);
        }

        public string GetFocusStackImageFullPath()
        {
            /// JPG
            string nom = $"{ImageNameBase}_{RotationSerieIncrement:D2}.jpg";
            return Path.Combine(GetFocusStackPath(), nom);
        }

        ///

        public string GetMaskFolderPath()
        {
            // ex: C:\Projet\images\masques_focusStack_A
            string coteFolder = (Cote == 0) ? "masques_A" : "masques_B";
            return Path.Combine(FocusStackFolderName, coteFolder);
            //return ImageFolderPath;
        }

        public string GetMaskImageName()
        {
            // PNG
            string cote = (Cote == 0) ? "A" : "B";
            return $"{ImageNameBase}_{cote}_{RotationSerieIncrement:D2}_mask.png";
        }

        public string GetMaskFullImagePath()
        {
            return Path.Combine(GetMaskFolderPath(), GetMaskImageName());
        }
         
        ///
    

        // --- Sauvegarde / Chargement ---
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

        public int VerticalLiftCurrentPos { get; set; }

        public int VerticalLiftMaxPos { get; set; }

        public int VerticalLiftDefaultPos { get; set; }

        public bool MaskAuto { get; set; }

        public bool MaskSave { get; set; }
       

        public int ThreshVal { get; set; } = 20;



        public List<MessagingUserSetting> MessagingUsers { get; set; } = new();

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
                settings.MessagingUsers ??= new List<MessagingUserSetting>();
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


    public class MessagingUserSetting
    {
        public string Email { get; set; } = string.Empty;
        public bool Send { get; set; } = true;
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





    public static class AppLifecycle
    {
        private static readonly ConcurrentBag<CancellationTokenSource> _ctsBag = new();
        private static readonly ConcurrentBag<IDisposable> _disposablesBag = new();
        private static readonly ConcurrentBag<Task> _tasksBag = new();

        private static CancellationTokenSource _globalCts = new();
        public static CancellationToken GlobalToken => _globalCts.Token;

        public static CancellationTokenSource RegisterCts(CancellationTokenSource cts)
        { if (cts != null) _ctsBag.Add(cts); return cts; }

        public static T RegisterDisposable<T>(T disposable) where T : IDisposable
        { if (disposable != null) _disposablesBag.Add(disposable); return disposable; }

        public static void RegisterTask(Task task)
        { if (task != null) _tasksBag.Add(task); }

        public static CancellationTokenSource CreateLinkedCts(params CancellationToken[] tokens)
        {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(
                tokens?.Length > 0 ? new[] { _globalCts.Token }.Concat(tokens).ToArray()
                                   : new[] { _globalCts.Token });
            _ctsBag.Add(linked);
            return linked;
        }

        public static void StopAllGraceful(int waitMsPerTask = 500)
        {
            try { _globalCts.Cancel(); } catch { }
            foreach (var cts in _ctsBag) { try { cts.Cancel(); } catch { } }
            foreach (var d in _disposablesBag) { try { d.Dispose(); } catch { } }
            foreach (var t in _tasksBag) { try { t.Wait(waitMsPerTask); } catch { } }
        }


        public static void StopAllGraceful()
            => StopAllGraceful(waitMsPerTask: 500);


        public static void HardExitAfter(Action gracefulStop, int graceMs = 1500, bool killIfStuck = true)
        {
            try
            {
                gracefulStop?.Invoke();
                Thread.Sleep(graceMs);
                if (killIfStuck)
                {
                    try { System.Windows.Forms.Application.Exit(); } catch { }
                    Environment.Exit(0);
                }
            }
            catch { Environment.Exit(0); }
        }

        public static void Clear()
        {
            while (_ctsBag.TryTake(out _)) { }
            while (_disposablesBag.TryTake(out _)) { }
            while (_tasksBag.TryTake(out _)) { }
            try { _globalCts.Dispose(); } catch { }
            _globalCts = new CancellationTokenSource();
        }

        private static Mutex _singleInstanceMutex;

        public static bool EnsureSingleInstance(string mutexName = "Aerolithe_SingleInstance")
        {
            bool createdNew = false;
            _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);
            return createdNew;
        }

        public static void TryCloseOtherInstances(string processName = "Aerolithe", int waitMs = 1500)
        {
            try
            {
                var current = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(processName))
                {
                    if (p.Id == current.Id) continue;
                    try
                    {
                        if (p.CloseMainWindow()) p.WaitForExit(waitMs);
                        if (!p.HasExited) p.Kill(); // dernier recours
                    }
                    catch { }
                }
            }
            catch { }
        }
    }


}
