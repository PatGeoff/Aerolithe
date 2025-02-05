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
                    btn_projectSetup.Enabled = true;
                    btn_projectSetup.Text = Path.GetFileName(projectPath);
                    SaveProjectToFile(projectPath);
                }
                string projectDirectory = Path.GetDirectoryName(projectPath);
                string imagesFolderPath = Path.Combine(projectDirectory, "images");
                if (!Directory.Exists(imagesFolderPath))
                {
                    Directory.CreateDirectory(imagesFolderPath);
                }
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
