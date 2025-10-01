//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace Aerolithe
//{
//    public partial class Aerolithe : Form
//    {
//        public string focusStackOutputPath = "";
//        public async Task MakeFocusStack()
//        {
//            //listBox_focusStackImg.Items.Clear();
//            OpenFileDialog openFileDialog = new OpenFileDialog
//            {
//                Multiselect = true,
//                Filter = "Images|*.jpg;*.jpeg;*.png;*.tif;*.tiff"
//            };

//            if (openFileDialog.ShowDialog() == DialogResult.OK)
//            {
//                string[] imagePaths = openFileDialog.FileNames;

//                //foreach (var img in imagePaths)
//                //{
//                //    listBox_focusStackImg.Items.Add(img);
//                //}

//                // Suggestion de nom basé sur le premier fichier
//                string baseName = Path.GetFileNameWithoutExtension(imagePaths[0]);
//                baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "_\\d+$", ""); // Supprime le suffixe _01, _02, etc.

//                // Choix du dossier de destination
//                FolderBrowserDialog folderDialog = new FolderBrowserDialog();
//                folderDialog.Description = "Choisis le dossier de destination pour l'image fusionnée";

//                if (folderDialog.ShowDialog() == DialogResult.OK)
//                {
//                    string selectedFolder = folderDialog.SelectedPath;

//                    // Nom suggéré avec possibilité de modification
//                    string suggestedFileName = Microsoft.VisualBasic.Interaction.InputBox(
//                        "Nom du fichier de sortie :",
//                        "Nom du fichier",
//                        baseName + "_stacked.jpg");

//                    focusStackOutputPath = Path.Combine(selectedFolder, suggestedFileName);

//                    lbl_focusStackOutputDest.Text = focusStackOutputPath;

//                    await RunFocusStack(imagePaths);
//                }
//            }

//        }

//        public async Task MakeFocusStackSerie()
//        {
//            if (InvokeRequired)
//            {
//                Invoke(new Action(async () => await MakeFocusStackSerie()));
//                return;
//            }

//            //listBox_focusStackImg.Items.Clear();

//            string folderPath = projet.TempImageFolderPath;
//            string[] extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };

//            var imageFiles = Directory.GetFiles(folderPath)
//                                      .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
//                                      .ToArray();

//            if (imageFiles.Length > 0)
//            {
//                if (!Directory.Exists(projet.FocusStackPath))
//                {
//                    Directory.CreateDirectory(projet.FocusStackPath);
//                    MessageBox.Show("Dossier " + projet.FocusStackPath + " créé");
//                }

//                string baseName = Path.GetFileNameWithoutExtension(imageFiles[0]);
//                baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "_\\d+$", "");
//                string suggestedFileName = baseName + "_stacked.jpg";
//                focusStackOutputPath = Path.Combine(projet.FocusStackPath, suggestedFileName);
//            }
//            else
//            {
//                AppendTextToConsoleNL("Aucune image trouvée dans le dossier.");
//                return;
//            }

//            await RunFocusStack(imageFiles);

//        }

//        public async Task RunFocusStack(string[] imagePaths)
//        {
//            string exePath = Path.Combine(Application.StartupPath, "MyResources", "Focus-stack", "focus-stack.exe");

//            if (!File.Exists(exePath))
//            {
//                MessageBox.Show("focus-stack.exe introuvable !");
//                return;
//            }

//            string outputImage = focusStackOutputPath;

//            string args = $"--output=\"{outputImage}\" " + string.Join(" ", imagePaths.Select(p => $"\"{p}\""));

//            ProcessStartInfo psi = new ProcessStartInfo
//            {
//                FileName = exePath,
//                Arguments = args,
//                UseShellExecute = false,
//                RedirectStandardOutput = true,
//                RedirectStandardError = true,
//                CreateNoWindow = true,
//                StandardOutputEncoding = Encoding.UTF8
//            };

//            using (Process process = new Process())
//            {
//                process.StartInfo = psi;
//                psi.StandardOutputEncoding = Encoding.UTF8;
//                process.OutputDataReceived += async (sender, e) =>
//                {
//                    if (!string.IsNullOrEmpty(e.Data))
//                    {
//                        await AppendTextToFFMPEGConsoleNL(e.Data);

//                        // Extraction du format [x/y]
//                        var match = Regex.Match(e.Data, @"\[(\d+)/(\d+)\]");
//                        if (match.Success)
//                        {
//                            int current = int.Parse(match.Groups[1].Value);
//                            int total = int.Parse(match.Groups[2].Value);

//                            progressBar_ImageSave.Invoke(() =>
//                            {
//                                progressBar_ImageSave.Maximum = total;
//                                progressBar_ImageSave.Value = Math.Min(current, total);
//                            });
//                        }
//                    }
//                };

//                process.ErrorDataReceived += async (sender, e) =>
//                {
//                    if (!string.IsNullOrEmpty(e.Data))
//                        await AppendTextToFFMPEGConsoleNL(e.Data);
//                };

//                process.Start();

//                process.BeginOutputReadLine();
//                process.BeginErrorReadLine();

//                await Task.Run(() => process.WaitForExit());

//                if (process.ExitCode == 0 && File.Exists(outputImage))
//                {
//                    if (picBox_FocusStackedImage.InvokeRequired)
//                    {
//                        picBox_FocusStackedImage.Invoke(() =>
//                        {
//                            picBox_FocusStackedImage.Image = System.Drawing.Image.FromFile(outputImage);
//                        });
//                    }
//                    else
//                    {
//                        picBox_FocusStackedImage.Image = System.Drawing.Image.FromFile(outputImage);
//                    }
//                }
//                else
//                {
//                    MessageBox.Show("Erreur lors du traitement.");
//                }
//            }


//        }

//    }
//}

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            public string[] ImagePaths { get; set; }
            public string OutputPath { get; set; }
            public string Status { get; set; } // "En attente", "En cours", "Terminé", "Erreur"
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
                baseName = Regex.Replace(baseName, "_\\d+$", "");

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
                    if (lbl_focusStackOutputDest.InvokeRequired) {
                        lbl_focusStackOutputDest.Invoke(new Action(() =>
                        {
                            lbl_focusStackOutputDest.Text = outputPath;
                        }));
                    }                    

                    focusStackQueue.Add(new FocusStackTask
                    {
                        ImagePaths = imagePaths,
                        OutputPath = outputPath,
                        Status = "En attente"
                    });

                    UpdateQueueDisplay();
                    await ProcessFocusStackQueue();
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
                baseName = Regex.Replace(baseName, "_\\d+$", "");
                string suggestedFileName = baseName + "_stacked.jpg";
                string outputPath = Path.Combine(projet.FocusStackPath, suggestedFileName);

                focusStackQueue.Add(new FocusStackTask
                {
                    ImagePaths = imageFiles,
                    OutputPath = outputPath,
                    Status = "En attente"
                });

                UpdateQueueDisplay();
                await ProcessFocusStackQueue();
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

            foreach (var task in focusStackQueue.Where(t => t.Status == "En attente").ToList())
            {
                task.Status = "En cours";
                UpdateQueueDisplay();

                try
                {
                    await RunFocusStack(task.ImagePaths, task.OutputPath);
                    task.Status = "Terminé";
                }
                catch
                {
                    task.Status = "Erreur";
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
                richTextBox_PicReport.SelectionColor = System.Drawing.Color.LightGray;
                richTextBox_PicReport.AppendText("📭 La file d'attente est vide.\n");
                return;
            }

            foreach (var task in focusStackQueue)
            {
                string fileName = Path.GetFileName(task.OutputPath);
                string prefix = $"📷 {fileName} — Statut : ";

                // Affiche le préfixe en blanc
                richTextBox_PicReport.SelectionColor = System.Drawing.Color.White;
                richTextBox_PicReport.AppendText(prefix);

                // Affiche le statut en couleur
                switch (task.Status)
                {
                    case "Terminé":
                        richTextBox_PicReport.SelectionColor = System.Drawing.Color.LimeGreen;
                        break;
                    case "En cours":
                        richTextBox_PicReport.SelectionColor = System.Drawing.Color.Orange;
                        break;
                    case "Erreur":
                        richTextBox_PicReport.SelectionColor = System.Drawing.Color.Red;
                        break;
                    case "En attente":
                        richTextBox_PicReport.SelectionColor = System.Drawing.Color.DeepSkyBlue;
                        break;
                    default:
                        richTextBox_PicReport.SelectionColor = System.Drawing.Color.White;
                        break;
                }

                richTextBox_PicReport.AppendText(task.Status + "\n");
            }

            // Remettre la couleur par défaut
            richTextBox_PicReport.SelectionColor = System.Drawing.Color.White;
        }


        public async Task RunFocusStack(string[] imagePaths, string outputImage)
        {
            string exePath = Path.Combine(Application.StartupPath, "MyResources", "Focus-stack", "focus-stack.exe");

            if (!File.Exists(exePath))
            {
                MessageBox.Show("focus-stack.exe introuvable !");
                return;
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