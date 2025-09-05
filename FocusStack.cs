using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public string focusStackOutputPath = "";
        public async Task MakeFocusStack()
        {
            listBox_focusStackImg.Items.Clear();
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Images|*.jpg;*.jpeg;*.png;*.tif;*.tiff"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string[] imagePaths = openFileDialog.FileNames;

                foreach (var img in imagePaths)
                {
                    listBox_focusStackImg.Items.Add(img);
                }

                // Suggestion de nom basé sur le premier fichier
                string baseName = Path.GetFileNameWithoutExtension(imagePaths[0]);
                baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "_\\d+$", ""); // Supprime le suffixe _01, _02, etc.

                // Choix du dossier de destination
                FolderBrowserDialog folderDialog = new FolderBrowserDialog();
                folderDialog.Description = "Choisis le dossier de destination pour l'image fusionnée";

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFolder = folderDialog.SelectedPath;

                    // Nom suggéré avec possibilité de modification
                    string suggestedFileName = Microsoft.VisualBasic.Interaction.InputBox(
                        "Nom du fichier de sortie :",
                        "Nom du fichier",
                        baseName + "_stacked.jpg");

                    focusStackOutputPath = Path.Combine(selectedFolder, suggestedFileName);

                    lbl_focusStackOutputDest.Text = focusStackOutputPath;

                    await RunFocusStack(imagePaths);
                }
            }


        }


        public async Task RunFocusStack(string[] imagePaths)
        {
            string exePath = Path.Combine(Application.StartupPath, "MyResources", "Focus-stack", "focus-stack.exe");

            if (!File.Exists(exePath))
            {
                MessageBox.Show("focus-stack.exe introuvable !");
                return;
            }

            string outputImage = focusStackOutputPath;

            string args = $"--output=\"{outputImage}\" " + string.Join(" ", imagePaths.Select(p => $"\"{p}\""));

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = psi;

                // Événements pour lire la sortie en temps réel
                process.OutputDataReceived += async (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        await AppendTextToConsoleNL(e.Data);
                };

                process.ErrorDataReceived += async (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        await AppendTextToConsoleNL(e.Data);
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0 && File.Exists(outputImage))
                {
                    if (picBox_FocusStackedImage.InvokeRequired)
                    {
                        picBox_FocusStackedImage.Invoke(() =>
                        {
                            picBox_FocusStackedImage.Image = System.Drawing.Image.FromFile(outputImage);
                        });
                    }
                    else
                    {
                        picBox_FocusStackedImage.Image = System.Drawing.Image.FromFile(outputImage);
                    }
                }
                else
                {
                    MessageBox.Show("Erreur lors du traitement.");
                }
            }
        }


    }
}
