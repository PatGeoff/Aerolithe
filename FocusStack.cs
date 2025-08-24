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

                foreach (var img in imagePaths) {
                    listBox_focusStackImg.Items.Add(img);
                }

                // Ensuite, tu peux lancer le traitement

                await RunFocusStack(imagePaths);

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

            string outputImage = Path.Combine(Application.StartupPath, "stacked_output.jpg");
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
                            picBox_FocusStackedImage.Image = Image.FromFile(outputImage);
                        });
                    }
                    else
                    {
                        picBox_FocusStackedImage.Image = Image.FromFile(outputImage);
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
