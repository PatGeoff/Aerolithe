using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        private static string projectPath = null;
        private static string imagesFolderPath = null;
        private void SaveProject()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Project Files (*.aero)|*.aero|All Files (*.*)|*.*";
                saveFileDialog.Title = "Save Project As";
                if (projectPath != null)
                {
                    saveFileDialog.InitialDirectory = projectPath;
                }

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    projectPath = saveFileDialog.FileName;
                    btn_goToProjectFolder.Enabled = true;
                    string projectDirectory = Path.GetDirectoryName(projectPath);
                    string projectName = Path.GetFileName(projectPath);
                    btn_goToProjectFolder.Text = $"{Path.GetFileName(projectDirectory)}/{projectName}";

                    SaveProjectToFile(projectPath);
                    SetImageFolder();
                }
            }
        }

        private void SetImageFolder()
        {
            try
            {
                if (projectPath == null)
                {
                    SaveProject();
                }
                string projectDirectory = Path.GetDirectoryName(projectPath);
                imagesFolderPath = Path.Combine(projectDirectory, "images");
                btn_goToImageFolder.Enabled = true;
                if (!Directory.Exists(imagesFolderPath))
                {
                    Directory.CreateDirectory(imagesFolderPath);
                    btn_goToImageFolder.Text = imagesFolderPath;

                }

                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select the folder to save images";
                    folderDialog.SelectedPath = imagesFolderPath;

                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        imagesFolderPath = folderDialog.SelectedPath;

                    }
                }
                btn_goToImageFolder.Text = $"{Path.GetFileName(projectDirectory)}/images";
                btn_goToImageFolder.Enabled = true;
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Aero Erreur: " + ex.Message);
            }


        }

        private void SaveProjectToFile(string filePath)
        {
            // Your logic to save the project to the specified file path
            // For example:
            // File.WriteAllText(filePath, projectData);
            //MessageBox.Show($"Project saved to {filePath}");

        }


    }
}
