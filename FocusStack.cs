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


        private Dictionary<FocusStackTask, FocusStackReportControl> taskControls = new();

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

        private void EnqueueFocusStackTask(string[] imagePaths, string outputPath, int elevation, int rotation, int serie, string status = "En attente")
        {
            AppendTextToConsoleNL("- EnqueueFocusStackTask");
            var task = new FocusStackTask
            {
                Serie = serie -1,
                Elevation = elevation,
                Rotation = rotation,
                ImagePaths = imagePaths,
                OutputPath = outputPath,
                Status = status
            };

            focusStackQueue.Add(task);

            var info = new FocusStackTaskInfo
            {
                Serie = serie.ToString(),
                Elevation = elevation,
                Rotation = rotation,
                Filename = Path.GetFileName(outputPath),
                Status = status
            };

            var control = new FocusStackReportControl();
            control.SetTaskInfo(info); 

            flowPanelReports.Controls.Add(control);
            taskControls[task] = control;
            flowPanelReports.ScrollControlIntoView(control);
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

        //public async Task MakeFocusStackSerie()
        //{
        //    AppendTextToConsoleNL("- MakeFocusStackSerie");
        //    if (!_stopRequested)
        //    {

        //        if (InvokeRequired)
        //        {
        //            Invoke(new Action(async () => MakeFocusStackSerie()));
        //            return;
        //        }
        //        //this.BeginInvoke((Action)(() => AppendTextToConsoleNL("on se rend ici?????")));
        //        if (!Directory.Exists(projet.FocusStackPath))
        //        {
        //            Directory.CreateDirectory(projet.FocusStackPath);
        //            MessageBox.Show("Dossier " + projet.FocusStackPath + " créé");
        //            //await Task.Delay(500);
        //        }
        //        string folderPath = projet.TempImageFolderPath;

        //        string[] extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };

        //        var imageFiles = Directory.GetFiles(folderPath)
        //                                  .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
        //                                  .ToArray();

        //        string baseName = imageFiles.Length > 0
        //            ? Path.GetFileNameWithoutExtension(imageFiles[0])
        //            : "Erreur";


        //        baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "_\\d+$", "");
        //        string suggestedFileName = baseName + "_stacked.jpg";
        //        string outputPath = Path.Combine(projet.FocusStackPath, suggestedFileName);


        //        //await Task.Delay(300);

        //        if (imageFiles.Length > 0)
        //        {
        //            AppendTextToConsoleNL("FocusStack première image : " + imageFiles[0]);
        //            AppendTextToConsoleNL("Nom du fichier de destination : " + baseName);
        //            EnqueueFocusStackTask(imageFiles, outputPath, (int)actuatorAngle, turntablePosition, _Serie);
        //        }
        //        else
        //        {
        //            // Crée une tâche avec un statut "Erreur" et sans images
        //            EnqueueFocusStackTask(new string[0], outputPath, (int)actuatorAngle, turntablePosition, _Serie, "Erreur");
        //            AppendTextToConsoleNL("Aucune image trouvée dans le dossier.");
        //        }
        //        return Task.CompletedTask;
        //    }

        //}
        public Task MakeFocusStackSerie()
        {
            if (_stopRequested) return Task.CompletedTask;

            this.BeginInvoke((Action)(() => AppendTextToConsoleNL("on se rend ici?????")));

            if (!Directory.Exists(projet.GetFocusStackPath()))
            {
                Directory.CreateDirectory(projet.GetFocusStackPath());
                this.BeginInvoke((Action)(() => MessageBox.Show("Dossier " + projet.GetFocusStackPath() + " créé")));
            }

            string folderPath = projet.GetTempImageFolderPath();
            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };

            var imageFiles = Directory.GetFiles(folderPath)
                                      .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                                      .ToArray();

            string baseName = imageFiles.Length > 0
                ? Path.GetFileNameWithoutExtension(imageFiles[0])
                : "Erreur";

            baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "_\\d+$", "");
            string outputPath = Path.Combine(projet.GetFocusStackPath(), baseName + "_stacked.jpg");

            this.BeginInvoke((Action)(() =>
            {
                if (imageFiles.Length > 0)
                {
                    AppendTextToConsoleNL("FocusStack première image : " + imageFiles[0]);
                    AppendTextToConsoleNL("Nom du fichier de destination : " + baseName);
                    EnqueueFocusStackTask(imageFiles, outputPath, (int)actuatorAngle, turntablePosition, _Serie);
                }
                else
                {
                    EnqueueFocusStackTask(Array.Empty<string>(), outputPath, (int)actuatorAngle, turntablePosition, _Serie, "Erreur");
                    AppendTextToConsoleNL("Aucune image trouvée dans le dossier.");
                }
            }));

            return Task.CompletedTask;
        }

        private Task ProcessFocusStackQueue()
        {
            AppendTextToConsoleNL("- ProcessFocusStackQueue");
            if (isProcessingQueue) return Task.CompletedTask;
            isProcessingQueue = true;

            var pendingTasks = focusStackQueue.Where(t => t.Status == "En attente").ToList();

            foreach (var nextTask in pendingTasks)
            {
                nextTask.Status = "En cours";
                UpdateQueueDisplay();

                _ = Task.Run(async () =>
                {
                   
                    bool success = await RunFocusStack(nextTask.ImagePaths, nextTask.OutputPath);
                    nextTask.Status = success ? "Terminé" : "Erreur";
                    this.BeginInvoke((Action)(UpdateQueueDisplay));
                });
            }

            isProcessingQueue = false;
            return Task.CompletedTask;
        }
        private void UpdateQueueDisplay()
        {
            AppendTextToConsoleNL("- UpdateQueueDisplay");
            foreach (var task in focusStackQueue)
            {
                if (taskControls.TryGetValue(task, out var control))
                {
                    var info = new FocusStackTaskInfo
                    {
                        Serie = task.Serie.ToString(),
                        Elevation = task.Elevation,
                        Rotation = task.Rotation,
                        Filename = Path.GetFileName(task.OutputPath),
                        Status = task.Status
                    };

                    control.SetTaskInfo(info); // ✅ nouvelle méthode
                }
            }
        }

        public async Task<bool> RunFocusStack(string[] imagePaths, string outputImage)
        {
            AppendTextToConsoleNL("- RunFocusStack");
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
                    focusStackOutputPath = outputImage;
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
                    AppendTextToConsoleNL("- Focus Stack Terminé");
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

