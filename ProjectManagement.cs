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

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private static string projectPath = null;
        private static string projectDirectory = null;
        private static string imagesFolderPath = null;
        private static string imageNameBase = null;
        private static string backgroundImage_1, backgroundImage_2, backgroundImage_3;
        private ColorConversion selectedConversion = ColorConversion.BayerBg2Bgr;

        private void CreateNewProject()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Project Files (*.aero)|*.aero|All Files (*.*)|*.*";
                saveFileDialog.Title = "Create New Project";
                if (projectPath != null)
                {
                    saveFileDialog.InitialDirectory = projectPath;
                }

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = saveFileDialog.FileName;
                    string projectName = Path.GetFileNameWithoutExtension(selectedPath);

                    // Custom validation to check if the project name contains dots
                    if (projectName.Contains("."))
                    {
                        MessageBox.Show("The project name should not contain dots. Please choose a different name.", "Invalid Project Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    projectPath = selectedPath;

                    // Create the file if it doesn't exist
                    if (!File.Exists(projectPath))
                    {
                        File.Create(projectPath).Dispose();
                    }

                    btn_goToProjectFolder.Enabled = true;
                    projectDirectory = Path.GetDirectoryName(projectPath);
                    btn_goToProjectFolder.Text = $"{Path.GetFileName(projectDirectory)}/{Path.GetFileName(projectPath)}";
                    WritePrefs("projectPath", projectPath);
                    WritePrefs("backgroundImage_1", "");
                    WritePrefs("backgroundImage_2", "");
                    WritePrefs("backgroundImage_3", "");
                    SetImageFolder();
                }
            }
        }


        private void SelectExistingProject()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Project Files (*.aero)|*.aero|All Files (*.*)|*.*";
                openFileDialog.Title = "Select Project File";
                if (projectPath != null)
                {
                    openFileDialog.InitialDirectory = projectPath;
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    projectPath = openFileDialog.FileName;

                    btn_goToProjectFolder.Enabled = true;
                    projectDirectory = Path.GetDirectoryName(projectPath);
                    string projectName = Path.GetFileName(projectPath);
                    btn_goToProjectFolder.Text = $"{Path.GetFileName(projectDirectory)}/{projectName}";
                    LoadPrefs();
                    Task.Run(() => loadBackgroundMasks());
                }
               
            }
        }

        private void SaveProject()
        {
            MessageBox.Show("Svp sauvegarder ou ouvrir un projet");
        }

        private void SetImageFolder()
        {
            JObject json = null;
            try
            {

                if (projectPath == null)
                {
                    CreateNewProject();
                }
                string projectDirectory = Path.GetDirectoryName(projectPath);
                imagesFolderPath = Path.Combine(projectDirectory, "Images");
                btn_goToImageFolder.Enabled = true;
                if (!Directory.Exists(imagesFolderPath))
                {
                    Directory.CreateDirectory(imagesFolderPath);

                }

                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select or create the folder to save images in";
                    folderDialog.SelectedPath = imagesFolderPath;

                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        imagesFolderPath = folderDialog.SelectedPath;
                        //MessageBox.Show(imagesFolderPath);

                    }
                }
                btn_goToImageFolder.Text = imagesFolderPath;
                btn_goToImageFolder.Enabled = true;
                //    // Write imagesFolderPath to projectPath file if it exists
                // Write imagesFolderPath to projectPath file if it exists
                WritePrefs("imageFolderPath", imagesFolderPath);
            }
            catch (Exception ex) { }
              
            #region temp
            //try
            //{

            //    if (projectPath == null)
            //    {
            //        SaveProject();
            //    }
            //    string projectDirectory = Path.GetDirectoryName(projectPath);
            //    imagesFolderPath = Path.Combine(projectDirectory, "images");
            //    btn_goToImageFolder.Enabled = true;
            //    if (!Directory.Exists(imagesFolderPath))
            //    {
            //        Directory.CreateDirectory(imagesFolderPath);
            //        btn_goToImageFolder.Text = imagesFolderPath;

            //    }

            //    using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            //    {
            //        folderDialog.Description = "Select the folder to save images";
            //        folderDialog.SelectedPath = imagesFolderPath;

            //        if (folderDialog.ShowDialog() == DialogResult.OK)
            //        {
            //            imagesFolderPath = folderDialog.SelectedPath;

            //        }
            //    }
            //    btn_goToImageFolder.Text = $"{Path.GetFileName(projectDirectory)}/images";
            //    btn_goToImageFolder.Enabled = true;

            //    // Write imagesFolderPath to projectPath file if it exists
            //    if (File.Exists(projectPath))
            //    {
            //        using (StreamReader reader = new StreamReader(projectPath))
            //        {
            //            string line;
            //            while ((line = reader.ReadLine()) != null)
            //            {
            //                if (line.StartsWith("Images Folder Path:"))
            //                {
            //                    imagesFolderPath = line.Substring("Images Folder Path:".Length).Trim();
            //                    btn_goToImageFolder.Text = imagesFolderPath;
            //                    break;
            //                }
            //                if (line.StartsWith("ImageName:"))
            //                {
            //                    string imagesName = line.Substring("ImageName:".Length).Trim();
            //                    txtBox_nomImages.Text = imagesName;
            //                    break;
            //                }
            //            }
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    AppendTextToConsoleNL("Aero Erreur: " + ex.Message);
            //}
            #endregion
        }

        private void LoadPrefs()
        {
            JObject prefs = ReadPrefFile();
            if (prefs != null) {
                imagesFolderPath = prefs["Preferences"]["imageFolderPath"]?.ToString();
                imageNameBase = prefs["Preferences"]["imageName"]?.ToString();
                btn_goToImageFolder.Text= imagesFolderPath;
                btn_goToImageFolder.Enabled = true;
                txtBox_nomImages.Text = imageNameBase;
                backgroundImage_1 = prefs["Preferences"]["backgroundImage_1"]?.ToString();
                backgroundImage_2 = prefs["Preferences"]["backgroundImage_2"]?.ToString();
                backgroundImage_3 = prefs["Preferences"]["backgroundImage_3"]?.ToString();
            }
        }

        private JObject ReadPrefFile()
        {
            // Ensure the file exists
            if (File.Exists(projectPath))
            {
                // Read the settings file
                var json = File.ReadAllText(projectPath);
                var settings = JObject.Parse(json);
                return settings;
            }
            else
            {
                // Handle the case where the file does not exist
                Console.WriteLine("Settings file not found.");
                return null;
            }
        }

        private void WritePrefs(string prefKey, string val)
        {
            if (File.Exists(projectPath))
            {
                // Read the existing settings file
                var json = File.ReadAllText(projectPath);

                JObject settings;

                // Check if the file is empty
                if (string.IsNullOrWhiteSpace(json))
                {
                    settings = new JObject();
                }
                else
                {
                    settings = JObject.Parse(json);
                }

                // Ensure the Preferences object exists
                if (settings["Preferences"] == null)
                {
                    settings["Preferences"] = new JObject();
                }

                // Update or add the key-value pair
                settings["Preferences"][prefKey] = val;

                // Write the updated settings back to the file
                File.WriteAllText(projectPath, settings.ToString());
            }
            else
            {
                CreateNewProject();
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
        private void PopulateColorColorDropdown()
        {
                                     
            comboBox_EmguColor.Items.Add("Rouge");
            comboBox_EmguColor.Items.Add("Vert");
            comboBox_EmguColor.Items.Add("Bleu");
            comboBox_EmguColor.Items.Add("Gris");
            
            comboBox_EmguColor.SelectedIndex = 3;
        }


        private async Task ManageRichTextBoxContent()
        {
            int maxLength = 2147483647; // Maximum length for RichTextBox
            int threshold = (int)(maxLength * 0.5); // 50% of the maximum length

            if (txtBox_Console.Text.Length > threshold)
            {
                // Find the position to start removing text
                int removeLength = txtBox_Console.Text.Length - threshold;

                // Remove the oldest lines
                txtBox_Console.Text = txtBox_Console.Text.Substring(removeLength);
            }
        }

        private async Task PrisePhotoBackground(int index)
        {

        }

        private void LoadAndResizeImage(string imagePath, PictureBox pictureBox)
        {
            using (Image originalImage = Image.FromFile(imagePath))
            {
                int newWidth = pictureBox.Width;
                int newHeight = pictureBox.Height;

                Bitmap resizedImage = new Bitmap(newWidth, newHeight);

                using (Graphics graphics = Graphics.FromImage(resizedImage))
                {
                    graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
                }

                // Safely update the PictureBox on the UI thread
                if (pictureBox.InvokeRequired)
                {
                    pictureBox.Invoke(new Action(() =>
                    {
                        pictureBox.Image = resizedImage;
                    }));
                }
                else
                {
                    pictureBox.Image = resizedImage;
                }
            }
        }




    }
}
