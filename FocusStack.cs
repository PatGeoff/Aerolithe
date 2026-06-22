using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
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
        private const int MaxFocusStackReportControls = 200;

        // File d'attente pour les focus stacks
        private List<FocusStackTask> focusStackQueue = new List<FocusStackTask>();


        private Dictionary<FocusStackTask, FocusStackReportControl> taskControls = new();

        private bool isProcessingQueue = false;

        public class FocusStackTask
        {
            public Guid Id { get; } = Guid.NewGuid();
            public int Serie { get; set; }
            public int Elevation { get; set; }
            public int Rotation { get; set; }
            public int RotationSerieIncrement { get; set; }
            public int Cote { get; set; }
            public string[] ImagePaths { get; set; } = Array.Empty<string>();
            public string OutputPath { get; set; } = string.Empty;
            public string MaskPath { get; set; } = string.Empty;
            public bool ApplyMask { get; set; }
            public string Status { get; set; } = string.Empty; // "En attente", "En cours", "Terminé", "Erreur"
            public bool IsRetry { get; set; }
        }

        private void EnqueueFocusStackTask(string[] imagePaths, string outputPath, string maskPath, bool applyMask, int elevation, int rotation, int serie, string status = "En attente")
        {
            AppendTextToConsoleNL("EnqueueFocusStackTask");
            var task = new FocusStackTask
            {
                Serie = serie,
                Elevation = elevation,
                Rotation = rotation,
                RotationSerieIncrement = projet.RotationSerieIncrement,
                Cote = projet.Cote,
                ImagePaths = imagePaths,
                OutputPath = outputPath,
                MaskPath = maskPath,
                ApplyMask = applyMask,
                Status = status
            };

            focusStackQueue.Add(task);

            var info = CreateFocusStackTaskInfo(task);

            var control = new FocusStackReportControl();
            control.SetTaskInfo(info);

            flowPanelReports.Controls.Add(control);
            taskControls[task] = control;
            flowPanelReports.ScrollControlIntoView(control);
            UpdateQueueDisplay();
            TrimFocusStackReports();

            if (!isProcessingQueue)
            {
                _ = ProcessFocusStackQueue();
            }
        }

        // Quand on appuie sur le bouton pour faire un FocusStack. 
        // Pas la version automatique
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
                    string cote = projet.Cote == 0 ? "A" : "B";
                    string suggestedFileName = Microsoft.VisualBasic.Interaction.InputBox(
                        "Nom du fichier de sortie :",
                        "Nom du fichier",
                        baseName + $"_{cote}" + "_stacked.jpg");

                    string outputPath = Path.Combine(selectedFolder, suggestedFileName);
                    if (lbl_focusStackOutputDest.InvokeRequired)
                    {
                        lbl_focusStackOutputDest.Invoke(new Action(() =>
                        {
                            lbl_focusStackOutputDest.Text = outputPath;
                        }));
                    }

                    EnqueueFocusStackTask(imagePaths, outputPath, projet.GetMaskFullImagePath(), projet.ApplyMask, (int)actuatorAngle, turntablePosition, projet.Serie + 1);
                }
            }
        }

        public Task MakeFocusStackSerie()
        {
            this.BeginInvoke((Action)(() => AppendTextToConsoleNL($"_stopRequested = {_stopRequested}")));
            if (_stopRequested) return Task.CompletedTask;

            //this.BeginInvoke((Action)(() => AppendTextToConsoleNL("on se rend ici?????")));

            if (!Directory.Exists(projet.GetFocusStackPath()))
            {
                Directory.CreateDirectory(projet.GetFocusStackPath());
            }
            this.BeginInvoke((Action)(() => AppendTextToConsoleNL($"projet.GetFocusStackPath:  {projet.GetFocusStackPath()}")));

            if (!Directory.Exists(projet.GetMaskFolderPath()))
            {
                Directory.CreateDirectory(projet.GetMaskFolderPath());
                this.BeginInvoke((Action)(() => MessageBox.Show("Dossier " + projet.GetMaskFolderPath() + " créé")));
            }
            this.BeginInvoke((Action)(() => AppendTextToConsoleNL($"projet.GetMaskFolderPath: {projet.GetMaskFolderPath()}")));


            string folderPath = projet.GetTempImageFolderPath();
            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                this.BeginInvoke((Action)(() => AppendTextToConsoleNL("FocusStack: dossier temporaire invalide.")));
                return Task.CompletedTask;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                this.BeginInvoke((Action)(() => AppendTextToConsoleNL("FocusStack: dossier temporaire créé car il était absent: " + folderPath)));
            }

            var imageFiles = Directory.GetFiles(folderPath)
                                      .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                                      .ToArray();

            //string baseName = imageFiles.Length > 0
            //    ? Path.GetFileNameWithoutExtension(imageFiles[0])
            //    : "Erreur";

            //baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "_\\d+$", "");
            //string outputPath = Path.Combine(projet.GetFocusStackPath(), baseName + "_stacked.jpg");

            string outputPath = projet.GetFocusStackImageFullPath();
            string maskPath = projet.GetMaskFullImagePath();

            this.BeginInvoke((Action)(() => AppendTextToConsoleNL($"projet.GetFocusStackImageFullPath: {projet.GetFocusStackImageFullPath()}")));
            this.BeginInvoke((Action)(() => AppendTextToConsoleNL($"projet.GetMaskFullImagePath: {projet.GetMaskFullImagePath()}")));

            this.BeginInvoke((Action)(() =>
            {
                if (imageFiles.Length > 0)
                {
                    this.BeginInvoke((Action)(() => AppendTextToConsoleNL("FocusStack première image : " + imageFiles[0])));
                    EnqueueFocusStackTask(imageFiles, outputPath, maskPath, projet.ApplyMask, (int)actuatorAngle, turntablePosition, projet.Serie + 1);
                }
                else
                {
                    EnqueueFocusStackTask(Array.Empty<string>(), outputPath, maskPath, projet.ApplyMask, (int)actuatorAngle, turntablePosition, projet.Serie + 1, "Erreur");
                    this.BeginInvoke((Action)(() => AppendTextToConsoleNL("Aucune image trouvée dans le dossier.")));
                }
            }));

            return Task.CompletedTask;
        }



        private async Task ProcessFocusStackQueue()
        {
            AppendTextToConsoleNL("- ProcessFocusStackQueue");
            if (isProcessingQueue) return;
            isProcessingQueue = true;

            try
            {
                while (true)
                {
                    var nextTask = focusStackQueue.FirstOrDefault(t => t.Status == "En attente");
                    if (nextTask == null || _stopRequested) break;

                    nextTask.Status = "En cours";
                    UpdateQueueDisplay();

                    try
                    {
                        await RunExistingFocusStackTaskAsync(nextTask);
                    }
                    catch (Exception ex)
                    {
                        nextTask.Status = "Erreur";
                        AppendTextToConsoleNL($"Erreur focus stack, passage au suivant: {ex.Message}", Color.Red);
                    }

                    this.BeginInvoke((Action)(() =>
                    {
                        UpdateQueueDisplay();
                        TrimFocusStackReports();
                    }));
                }
            }
            finally
            {
                isProcessingQueue = false;
            }
        }


      


        private void UpdateQueueDisplay()
        {
            AppendTextToConsoleNL("- UpdateQueueDisplay");
            foreach (var task in focusStackQueue)
            {
                if (taskControls.TryGetValue(task, out var control))
                {
                    var info = CreateFocusStackTaskInfo(task);

                    control.SetTaskInfo(info); // ✅ nouvelle méthode
                }
            }
        }

        private void TrimFocusStackReports()
        {
            while (taskControls.Count > MaxFocusStackReportControls)
            {
                FocusStackTask? removableTask = focusStackQueue.FirstOrDefault(t => t.Status == "Terminé" || t.Status == "Erreur");
                if (removableTask == null)
                {
                    break;
                }

                RemoveFocusStackReportTask(removableTask);
            }
        }

        private void ClearFocusStackReports()
        {
            foreach (Control control in flowPanelReports.Controls.Cast<Control>().ToArray())
            {
                flowPanelReports.Controls.Remove(control);
                control.Dispose();
            }

            focusStackQueue.Clear();
            taskControls.Clear();
        }

        private void RemoveFocusStackReportTask(FocusStackTask task)
        {
            focusStackQueue.Remove(task);

            if (taskControls.TryGetValue(task, out FocusStackReportControl? control))
            {
                flowPanelReports.Controls.Remove(control);
                control.Dispose();
                taskControls.Remove(task);
            }
        }

        private FocusStackTaskInfo CreateFocusStackTaskInfo(FocusStackTask task)
        {
            return new FocusStackTaskInfo
            {
                TaskId = task.Id,
                Serie = task.Serie.ToString(CultureInfo.InvariantCulture),
                Elevation = task.Elevation,
                Rotation = task.Rotation,
                RotationSerieIncrement = task.RotationSerieIncrement,
                ImageNumber = GetFocusStackImageNumber(task),
                CoteIndex = task.Cote,
                Cote = task.Cote == 0 ? "A" : "B",
                ImagePaths = task.ImagePaths,
                OutputPath = task.OutputPath,
                MaskPath = task.MaskPath,
                ApplyMask = task.ApplyMask,
                Filename = Path.GetFileName(task.OutputPath),
                Status = task.Status,
                IsRetry = task.IsRetry
            };
        }

        private static int GetFocusStackImageNumber(FocusStackTask task)
        {
            string name = Path.GetFileNameWithoutExtension(task.OutputPath);
            Match match = Regex.Match(name, @"_(\d+)$");

            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int imageNumber))
            {
                return imageNumber;
            }

            return task.RotationSerieIncrement;
        }

        public async Task ReprendreFocusStackSeulementAsync(FocusStackTaskInfo info)
        {
            AppendTextToConsoleNL($"Reprise focus stack seulement: {info.Filename}");
            if (!TryGetFocusStackTask(info, out FocusStackTask? foundTask))
            {
                AppendTextToConsoleNL("Reprise focus stack impossible: tâche introuvable.", Color.Red);
                return;
            }

            FocusStackTask task = foundTask!;
            task.IsRetry = true;
            await RetryFocusStackOnExistingTaskAsync(task);
        }

        public async Task ReprendrePrisePhotoFocusStackAsync(FocusStackTaskInfo info)
        {
            AppendTextToConsoleNL($"Reprise prise photo + focus stack: côté {info.Cote}, rotation {info.RotationSerieIncrement:D2}");
            if (!TryGetFocusStackTask(info, out FocusStackTask? foundTask))
            {
                AppendTextToConsoleNL("Reprise prise photo + focus stack impossible: tâche introuvable.", Color.Red);
                return;
            }
            FocusStackTask task = foundTask!;
            task.IsRetry = true;

            using var cts = new CancellationTokenSource();
            ResetSequenceCancellationButton();

            int previousSerie = projet.Serie;
            int previousCote = projet.Cote;
            int previousRotation = projet.RotationSerieIncrement;
            int previousFocus = projet.FocusSerieIncrement;
            bool previousFocusStackEnabled = projet.FocusStackEnabled;

            try
            {
                task.Status = "En cours";
                UpdateQueueDisplay();

                projet.Serie = Math.Max(0, ParseSerieNumber(info.Serie) - 1);
                projet.Cote = info.CoteIndex;
                projet.RotationSerieIncrement = info.RotationSerieIncrement;
                projet.FocusSerieIncrement = 0;
                projet.FocusStackEnabled = true;
                ToggleCote(projet.Cote);
                SavePrefsSettings();

                AppendTextToConsoleNL("Reprise: suppression des anciennes images temporaires.");
                DeleteFilesIfPresent(ResolveFocusStackInputImages(info));
                if (!string.IsNullOrWhiteSpace(info.OutputPath) && File.Exists(info.OutputPath))
                {
                    File.Delete(info.OutputPath);
                }

                AppendTextToConsoleNL($"Reprise: déplacement actuateur vers {info.Elevation:0}°.");
                await UdpSendActuatorMessageAsync($"actuator {info.Elevation.ToString("0", CultureInfo.InvariantCulture)}");
                await WaitForActuator(info.Elevation, cts.Token);
                AppendTextToConsoleNL($"Reprise: déplacement table tournante vers {info.Rotation:0} steps.");
                await UdpSendTurnTableMessageAsync($"turntable,{info.Rotation},{turntableSpeed}");
                await WaitForTurntablePositionAsync((int)info.Rotation, cancellationToken: cts.Token);

                if (projet.AutoCentrage)
                {
                    AppendTextToConsoleNL("Reprise: auto-centrage.");
                    await RoutineAutoCentrage(cancellationToken: cts.Token);
                }

                bool captured = false;
                for (int focusAttempt = 1; focusAttempt <= 2; focusAttempt++)
                {
                    if (focusAttempt == 2)
                    {
                        ApplyTemporaryBlurThresholdForRetry();
                    }

                    AppendTextToConsoleNL($"Reprise: autofocus essai {focusAttempt}/2.");
                    AutomaticFocusResult focusResult = await AutomaticFocusRoutine(cts.Token);
                    if (focusResult != AutomaticFocusResult.Success)
                    {
                        task.Status = "Erreur";
                        UpdateQueueDisplay();
                        AppendTextToConsoleNL($"Reprise prise photo + focus stack arrêtée: {focusResult}.");
                        return;
                    }

                    AppendTextToConsoleNL($"Reprise: capture focus stack essai {focusAttempt}/2.");
                    captured = await AutomaticFocusThenCapture(delta, cts.Token);
                    if (captured)
                    {
                        break;
                    }
                }

                if (!captured)
                {
                    task.Status = "Erreur";
                    UpdateQueueDisplay();
                    AppendTextToConsoleNL("Reprise arrêtée: capture focus stack impossible après récupération de netteté.", Color.Red);
                    return;
                }

                if (_stopRequested)
                {
                    task.Status = "Erreur";
                    UpdateQueueDisplay();
                    AppendTextToConsoleNL("Reprise arrêtée avant focus-stack.exe: _stopRequested est actif.", Color.Red);
                    return;
                }

                task.ImagePaths = ResolveFocusStackInputImages(info);
                AppendTextToConsoleNL($"Reprise: {task.ImagePaths.Length} images trouvées pour focus-stack.exe.");
                await RunExistingFocusStackTaskAsync(task);
            }
            catch (Exception ex)
            {
                task.Status = "Erreur";
                UpdateQueueDisplay();
                AppendTextToConsoleNL("Erreur reprise prise photo + focus stack: " + ex.Message, Color.Red);
            }
            finally
            {
                projet.Serie = previousSerie;
                projet.Cote = previousCote;
                projet.RotationSerieIncrement = previousRotation;
                projet.FocusSerieIncrement = previousFocus;
                projet.FocusStackEnabled = previousFocusStackEnabled;
                ClearTemporaryBlurThresholdOverride();
                ToggleCote(projet.Cote);
                SavePrefsSettings();
            }
        }

        private bool TryGetFocusStackTask(FocusStackTaskInfo info, out FocusStackTask? task)
        {
            task = focusStackQueue.FirstOrDefault(t => t.Id == info.TaskId);
            if (task != null)
            {
                return true;
            }

            task = focusStackQueue.FirstOrDefault(t =>
                string.Equals(t.OutputPath, info.OutputPath, StringComparison.OrdinalIgnoreCase) &&
                t.RotationSerieIncrement == info.RotationSerieIncrement &&
                t.Cote == info.CoteIndex);

            return task != null;
        }

        private async Task RetryFocusStackOnExistingTaskAsync(FocusStackTask task)
        {
            task.Status = "En cours";
            UpdateQueueDisplay();

            try
            {
                FocusStackTaskInfo info = CreateFocusStackTaskInfo(task);
                task.ImagePaths = ResolveFocusStackInputImages(info);
                AppendTextToConsoleNL($"Reprise focus stack seulement: {task.ImagePaths.Length} images trouvées.");
                await RunExistingFocusStackTaskAsync(task);
            }
            catch (Exception ex)
            {
                task.Status = "Erreur";
                UpdateQueueDisplay();
                AppendTextToConsoleNL("Erreur reprise focus stack seulement: " + ex.Message, Color.Red);
            }
        }

        private async Task RunExistingFocusStackTaskAsync(FocusStackTask task)
        {
            if (task.ImagePaths.Length == 0)
            {
                AppendTextToConsoleNL("Focus stack ignoré: aucune image source trouvée.", Color.Red);
                task.Status = "Erreur";
                UpdateQueueDisplay();
                return;
            }

            AppendTextToConsoleNL($"Focus stack: lancement de focus-stack.exe pour {Path.GetFileName(task.OutputPath)} avec {task.ImagePaths.Length} images.");
            if (!string.IsNullOrWhiteSpace(task.OutputPath) && File.Exists(task.OutputPath))
            {
                File.Delete(task.OutputPath);
            }

            string staleMaskedOutputPath = Path.ChangeExtension(task.OutputPath, ".png");
            if (!string.IsNullOrWhiteSpace(staleMaskedOutputPath) && File.Exists(staleMaskedOutputPath))
            {
                File.Delete(staleMaskedOutputPath);
            }

            bool success = await RunFocusStack(task.ImagePaths, task.OutputPath);
            task.Status = success ? "Terminé" : "Erreur";
            UpdateQueueDisplay();
        }

        private string[] ResolveFocusStackInputImages(FocusStackTaskInfo info)
        {
            if (info.ImagePaths.Length > 0 && info.ImagePaths.Any(File.Exists))
            {
                return info.ImagePaths.Where(File.Exists).ToArray();
            }

            int previousCote = projet.Cote;
            int previousRotation = projet.RotationSerieIncrement;
            try
            {
                projet.Cote = info.CoteIndex;
                projet.RotationSerieIncrement = info.RotationSerieIncrement;
                string folderPath = projet.GetTempImageFolderPath();
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    return Array.Empty<string>();
                }

                string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };
                return Directory.GetFiles(folderPath)
                    .Where(file => extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToArray();
            }
            finally
            {
                projet.Cote = previousCote;
                projet.RotationSerieIncrement = previousRotation;
            }
        }

        private static int ParseSerieNumber(string serie)
        {
            return int.TryParse(serie, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : 1;
        }

        private void DeleteFilesIfPresent(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex)
                {
                    AppendTextToConsoleNL($"Impossible de supprimer {path}: {ex.Message}");
                }
            }
        }

        //public async Task<bool> RunFocusStack(string[] imagePaths, string outputImage, string maskPath, bool ApplyMask)
        //{

        //    AppendTextToConsoleNL("- RunFocusStack");
        //    string exePath = Path.Combine(Application.StartupPath, "MyResources", "Focus-stack", "focus-stack.exe");


        //    if (!File.Exists(exePath))
        //    {
        //        MessageBox.Show("focus-stack.exe introuvable !");
        //        return false;
        //    }

        //    string args = $" --output=\"{outputImage}\" " + string.Join(" ", imagePaths.Select(p => $"\"{p}\""));

        //    ProcessStartInfo psi = new ProcessStartInfo
        //    {
        //        FileName = exePath,
        //        Arguments = args,
        //        UseShellExecute = false,
        //        RedirectStandardOutput = true,
        //        RedirectStandardError = true,
        //        CreateNoWindow = true,
        //        StandardOutputEncoding = Encoding.UTF8
        //    };

        //    using (Process process = new Process())
        //    {
        //        process.StartInfo = psi;
        //        process.OutputDataReceived += async (sender, e) =>
        //        {

        //            if (!string.IsNullOrEmpty(e.Data))
        //            {
        //                if (StackConsoleView)
        //                {
        //                    await AppendTextToFFMPEGConsoleNL(e.Data);
        //                }
        //                var match = Regex.Match(e.Data, @"\[(\d+)/(\d+)\]");
        //                if (match.Success)
        //                {
        //                    int current = int.Parse(match.Groups[1].Value);
        //                    int total = int.Parse(match.Groups[2].Value);

        //                    progressBar_ImageSave.Invoke(() =>
        //                    {
        //                        progressBar_ImageSave.Maximum = total;
        //                        progressBar_ImageSave.Value = Math.Min(current, total);
        //                    });

        //                }
        //            }
        //        };

        //        process.ErrorDataReceived += async (sender, e) =>
        //        {
        //            if (!string.IsNullOrEmpty(e.Data))
        //                await AppendTextToFFMPEGConsoleNL(e.Data);
        //        };

        //        process.Start();
        //        process.BeginOutputReadLine();
        //        process.BeginErrorReadLine();

        //        await Task.Run(() => process.WaitForExit());


        //       //if (process.ExitCode == 0 && File.Exists(outputImage) && File.Exists(maskPath))
        //        if (process.ExitCode == 0 && File.Exists(outputImage) )
        //            {
        //           // //on applique le masque
        //           //if (ApplyMask)
        //           // {
        //           //     using Bitmap _bmp = new Bitmap(outputImage);
        //           //     using Bitmap _msk = new Bitmap(maskPath);
        //           //     using Bitmap _png = await ApplyAlphaMaskFromJpg(_bmp, _msk);
        //           //     await SavePngAndDeleteJpg(_png, outputImage);
        //           // }

                    


        //            focusStackOutputPath = outputImage;

        //            Action updateImage = () =>
        //            {
        //                using (var original = new Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte>(outputImage))
        //                {
        //                    int boxWidth = picBox_FocusStackedImage.Width;
        //                    int boxHeight = picBox_FocusStackedImage.Height;

        //                    // Calcul du ratio
        //                    double ratioX = (double)boxWidth / original.Width;
        //                    double ratioY = (double)boxHeight / original.Height;
        //                    double ratio = Math.Min(ratioX, ratioY); // garde le ratio sans dépasser

        //                    int newWidth = (int)(original.Width * ratio);
        //                    int newHeight = (int)(original.Height * ratio);

        //                    var resized = original.Resize(newWidth, newHeight, Emgu.CV.CvEnum.Inter.Linear);

        //                    // Assigner l'image redimensionnée
        //                    picBox_FocusStackedImage.Image = resized.ToBitmap();
        //                }
        //            };

        //            if (picBox_FocusStackedImage.InvokeRequired)
        //                picBox_FocusStackedImage.Invoke(updateImage);
        //            else
        //                updateImage();

        //            AppendTextToConsoleNL("- Focus Stack Terminé");
        //            return true;
        //        }


        //        else
        //        {
        //            MessageBox.Show("Erreur lors du traitement.");
        //            return false;
        //        }
        //    }
        //}


        public static async Task<Bitmap> ApplyAlphaMaskFromJpg(Bitmap sourceJpg, Bitmap maskJpg)
        {
            if (sourceJpg == null) throw new ArgumentNullException(nameof(sourceJpg));
            if (maskJpg == null) throw new ArgumentNullException(nameof(maskJpg));

            // Crée une surface 32bppArgb (avec alpha) et y dessine la source JPG
            Bitmap argb = new Bitmap(sourceJpg.Width, sourceJpg.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(argb))
                g.DrawImage(sourceJpg, new Rectangle(0, 0, argb.Width, argb.Height));

            // Adapter le masque à la même taille si besoin
            Bitmap maskSized = maskJpg;
            if (maskJpg.Width != argb.Width || maskJpg.Height != argb.Height)
            {
                maskSized = new Bitmap(argb.Width, argb.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(maskSized))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(maskJpg, new Rectangle(0, 0, argb.Width, argb.Height));
                }
            }

            try
            {
                // Verrouillage mémoire pour perf
                var dataImg = argb.LockBits(new Rectangle(0, 0, argb.Width, argb.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                // On normalise la lecture masque en 32bpp pour simplifier l’accès
                using var mask32 = new Bitmap(maskSized.Width, maskSized.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(mask32))
                    g.DrawImage(maskSized, new Rectangle(0, 0, mask32.Width, mask32.Height));
                var dataMask = mask32.LockBits(new Rectangle(0, 0, mask32.Width, mask32.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                unsafe
                {
                    for (int y = 0; y < argb.Height; y++)
                    {
                        byte* rowImg = (byte*)dataImg.Scan0 + y * dataImg.Stride;
                        byte* rowMask = (byte*)dataMask.Scan0 + y * dataMask.Stride;

                        for (int x = 0; x < argb.Width; x++)
                        {
                            // Masque JPG → pas d’alpha intrinsèque. On calcule l’intensité (luminance) du pixel masque
                            byte mb = rowMask[x * 4 + 0];
                            byte mg = rowMask[x * 4 + 1];
                            byte mr = rowMask[x * 4 + 2];

                            // Luminance perceptuelle (BT.601)
                            int gray = (int)(0.299 * mr + 0.587 * mg + 0.114 * mb);

                            // Blanc = opaque, noir = transparent
                            byte alpha = (byte)gray;
                            // Si tu veux inverser: byte alpha = (byte)(255 - gray);

                            rowImg[x * 4 + 3] = alpha; // on écrase l’alpha
                        }
                    }
                }

                argb.UnlockBits(dataImg);
                mask32.UnlockBits(dataMask);

                return argb; // (à disposer par l’appelant)
            }
            finally
            {
                if (!ReferenceEquals(maskSized, maskJpg))
                    maskSized.Dispose();
            }
        }


        public static async Task SavePngAndDeleteJpg(Bitmap argbImage, string outputImageJpgPath)
        {
            if (argbImage == null) throw new ArgumentNullException(nameof(argbImage));
            if (string.IsNullOrWhiteSpace(outputImageJpgPath)) throw new ArgumentNullException(nameof(outputImageJpgPath));

            string pngPath = Path.ChangeExtension(outputImageJpgPath, ".png");
            string dir = Path.GetDirectoryName(pngPath)!;
            string tmpPath = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp.png");

            // Sauvegarde PNG
            argbImage.Save(tmpPath, ImageFormat.Png);

            // Remplacement atomique
            if (File.Exists(pngPath))
            {
                string backup = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".bak.png");
                File.Replace(tmpPath, pngPath, backup, ignoreMetadataErrors: true);
                try { if (File.Exists(backup)) File.Delete(backup); } catch { /* ignore */ }
            }
            else
            {
                File.Move(tmpPath, pngPath);
            }

            // Supprimer l’ancien JPG
            try
            {
                if (File.Exists(outputImageJpgPath)) {
                    Debug.WriteLine($"Tentative d'effacement de l'image {outputImageJpgPath}");
                    File.Delete(outputImageJpgPath);
                }
            }
            catch
            {
                // log éventuel
            }
        }


        public async Task<bool> RunFocusStack(string[] imagePaths, string outputImage)
        {

            AppendTextToConsoleNL("- RunFocusStack");
            string exePath = Path.Combine(Application.StartupPath, "MyResources", "Focus-stack", "focus-stack.exe");


            if (!File.Exists(exePath))
            {
                AppendTextToConsoleNL("focus-stack.exe introuvable: " + exePath, Color.Red);
                return false;
            }

            string denoise = ClampFocusStackDenoise(projet.FocusStackDenoise).ToString("0.###", CultureInfo.InvariantCulture);
            string args = $" --denoise={denoise} --output=\"{outputImage}\" " + string.Join(" ", imagePaths.Select(p => $"\"{p}\""));
            AppendTextToConsoleNL($"focus-stack.exe --denoise={denoise}");

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
                        if (StackConsoleView)
                        {
                            await AppendTextToFFMPEGConsoleNL(e.Data);
                        }
                        var match = Regex.Match(e.Data, @"\[(\d+)/(\d+)\]");
                        if (match.Success)
                        {
                            int current = int.Parse(match.Groups[1].Value);
                            int total = int.Parse(match.Groups[2].Value);

                            //progressBar_ImageSave.Invoke(() =>
                            //{
                            //    progressBar_ImageSave.Maximum = total;
                            //    progressBar_ImageSave.Value = Math.Min(current, total);
                            //});

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

                    Action updateImage = () =>
                    {
                        using (var original = new Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte>(outputImage))
                        {
                            int boxWidth = picBox_FocusStackedImage.Width;
                            int boxHeight = picBox_FocusStackedImage.Height;

                            // Calcul du ratio
                            double ratioX = (double)boxWidth / original.Width;
                            double ratioY = (double)boxHeight / original.Height;
                            double ratio = Math.Min(ratioX, ratioY); // garde le ratio sans dépasser

                            int newWidth = (int)(original.Width * ratio);
                            int newHeight = (int)(original.Height * ratio);

                            var resized = original.Resize(newWidth, newHeight, Emgu.CV.CvEnum.Inter.Linear);

                            // Assigner l'image redimensionnée
                            picBox_FocusStackedImage.Image = resized.ToBitmap();
                        }
                    };

                    if (picBox_FocusStackedImage.InvokeRequired)
                        picBox_FocusStackedImage.Invoke(updateImage);
                    else
                        updateImage();

                    AppendTextToConsoleNL("- Focus Stack Terminé");
                    return true;
                }


                else
                {
                    AppendTextToConsoleNL("Erreur lors du traitement focus stack.", Color.Red);
                    return false;
                }
            }
        }


    }
}

