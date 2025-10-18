using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Aerolithe
{
    public partial class Aerolithe : Form
    {
        public string focusStackOutputPath = "";

        // File d'attente pour les focus stacks
        private List<FocusStackTask> focusStackQueue = new List<FocusStackTask>();
        private bool isProcessingQueue = false;

        public class FocusStackTask
        {
            public int Serie {  get; set; }
            public int Elevation {  get; set; }
            public int Rotation { get; set; }
            public string[] ImagePaths { get; set; }
            public string OutputPath { get; set; }
            public string Status { get; set; } // "En attente", "En cours", "Terminé", "Erreur"
        }

        private void EnqueueFocusStackTask(string[] imagePaths, string outputPath, int elevation, int rotation, int serie)
        {
            focusStackQueue.Add(new FocusStackTask
            {
                Serie = serie,
                Elevation = elevation,
                Rotation = rotation,
                ImagePaths = imagePaths,
                OutputPath = outputPath,
                Status = "En attente"
            });

            UpdateQueueDisplay();

            if (!isProcessingQueue)
            {
                _ = ProcessFocusStackQueue();
            }
        }

        public async Task MakeFocusStack()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Images|*.jpg;*.jpeg;*.png;*.tif;*.tiff"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string[] imagePaths = openFileDialog.FileNames;

                string baseName = Path.GetFileNameWithoutExtension(imagePaths[0]);
                baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "_\\d+$", "");

                FolderBrowserDialog folderDialog = new FolderBrowserDialog();
                folderDialog.Description = "Choisis le dossier de destination pour l'image fusionnée";

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFolder = folderDialog.SelectedPath;

                    string suggestedFileName = Microsoft.VisualBasic.Interaction.InputBox(
                        "Nom du fichier de sortie :",
                        "Nom du fichier",
                        baseName + "_stacked.jpg");

                    string outputPath = Path.Combine(selectedFolder, suggestedFileName);
                    if (lbl_focusStackOutputDest.InvokeRequired)
                    {
                        lbl_focusStackOutputDest.Invoke(new Action(() =>
                        {
                            lbl_focusStackOutputDest.Text = outputPath;
                        }));
                    }

                    EnqueueFocusStackTask(imagePaths, outputPath, (int)actuatorAngle, turntablePosition, _Serie);
                }
            }
        }

        public async Task MakeFocusStackSerie()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(async () => await MakeFocusStackSerie()));
                return;
            }

            string folderPath = projet.TempImageFolderPath;
            string[] extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };

            var imageFiles = Directory.GetFiles(folderPath)
                                      .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                                      .ToArray();

            if (imageFiles.Length > 0)
            {
                if (!Directory.Exists(projet.FocusStackPath))
                {
                    Directory.CreateDirectory(projet.FocusStackPath);
                    MessageBox.Show("Dossier " + projet.FocusStackPath + " créé");
                }

                string baseName = Path.GetFileNameWithoutExtension(imageFiles[0]);
                baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "_\\d+$", "");
                string suggestedFileName = baseName + "_stacked.jpg";
                string outputPath = Path.Combine(projet.FocusStackPath, suggestedFileName);

                EnqueueFocusStackTask(imageFiles, outputPath, (int)actuatorAngle, turntablePosition, _Serie);
            }
            else
            {
                AppendTextToConsoleNL("Aucune image trouvée dans le dossier.");

            }
        }

        private async Task ProcessFocusStackQueue()
        {
            if (isProcessingQueue) return;
            isProcessingQueue = true;

            while (true)
            {
                var nextTask = focusStackQueue.FirstOrDefault(t => t.Status == "En attente");
                if (nextTask == null) break;

                nextTask.Status = "En cours";
                UpdateQueueDisplay();

                try
                {
                    bool success = await RunFocusStack(nextTask.ImagePaths, nextTask.OutputPath);
                    nextTask.Status = success ? "Terminé" : "Erreur";
                }
                catch
                {
                    nextTask.Status = "Erreur";
                }

                UpdateQueueDisplay();
            }

            isProcessingQueue = false;
        }

        private void UpdateQueueDisplay()
        {
            if (richTextBox_PicReport.InvokeRequired)
            {
                richTextBox_PicReport.Invoke(new Action(UpdateQueueDisplay));
                return;
            }

            richTextBox_PicReport.Clear();

            if (focusStackQueue.Count == 0)
            {
                richTextBox_PicReport.SelectionColor = Color.LightGray;
                richTextBox_PicReport.AppendText(" La file d'attente est vide.");
                return;
            }
            

            foreach (var task in focusStackQueue)
            {


                string fileName = Path.GetFileName(task.OutputPath);
                string prefix = $"  {task.Serie}\t{task.Elevation}°\t{task.Rotation}°\t📷 {fileName}\t—    Statut : ";

                richTextBox_PicReport.SelectionColor = Color.White;
                richTextBox_PicReport.AppendText(prefix);

                switch (task.Status)
                {
                    case "Terminé":
                        richTextBox_PicReport.SelectionColor = Color.LimeGreen;
                        break;
                    case "En cours":
                        richTextBox_PicReport.SelectionColor = Color.Orange;
                        break;
                    case "Erreur":
                        richTextBox_PicReport.SelectionColor = Color.Red;
                        break;
                    case "En attente":
                        richTextBox_PicReport.SelectionColor = Color.DeepSkyBlue;
                        break;
                    default:
                        richTextBox_PicReport.SelectionColor = Color.White;
                        break;
                }

                richTextBox_PicReport.AppendText(task.Status + "\n");
            }

            richTextBox_PicReport.SelectionColor = Color.White;
            richTextBox_PicReport.Refresh();
            richTextBox_PicReport.ScrollToCaret();
        }

       
        public async Task<bool> RunFocusStack(string[] imagePaths, string outputImage)
        {
            string exePath = Path.Combine(Application.StartupPath, "MyResources", "Focus-stack", "focus-stack.exe");

            if (!File.Exists(exePath))
            {
                MessageBox.Show("focus-stack.exe introuvable !");
                return false;
            }

            string args = $"--output=\"{outputImage}\" " + string.Join(" ", imagePaths.Select(p => $"\"{p}\""));

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += async (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        await AppendTextToFFMPEGConsoleNL(e.Data);

                        var match = Regex.Match(e.Data, @"\[(\d+)/(\d+)\]");
                        if (match.Success)
                        {
                            int current = int.Parse(match.Groups[1].Value);
                            int total = int.Parse(match.Groups[2].Value);

                            progressBar_ImageSave.Invoke(() =>
                            {
                                progressBar_ImageSave.Maximum = total;
                                progressBar_ImageSave.Value = Math.Min(current, total);
                            });
                        }
                    }
                };

                process.ErrorDataReceived += async (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        await AppendTextToFFMPEGConsoleNL(e.Data);
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

                    return true;
                }
                else
                {
                    MessageBox.Show("Erreur lors du traitement.");
                    return false;
                }
            }
        }
    }
}

