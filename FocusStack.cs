using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
            public int Serie { get; set; }
            public int Elevation { get; set; }
            public int Rotation { get; set; }
            public string[] ImagePaths { get; set; }
            public string OutputPath { get; set; }
            public string MaskPath { get; set; }
            public bool ApplyMask { get; set; }
            public string Status { get; set; } // "En attente", "En cours", "Terminé", "Erreur"
        }

        private void EnqueueFocusStackTask(string[] imagePaths, string outputPath, string maskPath, bool applyMask, int elevation, int rotation, int serie, string status = "En attente")
        {
            AppendTextToConsoleNL("- EnqueueFocusStackTask");
            var task = new FocusStackTask
            {
                Serie = serie - 1,
                Elevation = elevation,
                Rotation = rotation,
                ImagePaths = imagePaths,
                OutputPath = outputPath,
                MaskPath = maskPath,
                ApplyMask = applyMask,
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
                    string cote = projet.Cote == 1 ? "A" : "B";
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

                    EnqueueFocusStackTask(imagePaths, outputPath, projet.GetMaskFullImagePath(), projet.ApplyMask, (int)actuatorAngle, turntablePosition, _Serie);
                }
            }
        }

        public Task MakeFocusStackSerie()
        {
            if (_stopRequested) return Task.CompletedTask;

            this.BeginInvoke((Action)(() => AppendTextToConsoleNL("on se rend ici?????")));

            if (!Directory.Exists(projet.GetFocusStackPath()))
            {
                Directory.CreateDirectory(projet.GetFocusStackPath());
                this.BeginInvoke((Action)(() => MessageBox.Show("Dossier " + projet.GetFocusStackPath() + " créé")));
            }

            if (projet.MaskSave)
            {
                if (!Directory.Exists(projet.GetMaskFolderPath()))
                {
                    Directory.CreateDirectory(projet.GetMaskFolderPath());
                    this.BeginInvoke((Action)(() => MessageBox.Show("Dossier " + projet.GetMaskFolderPath() + " créé")));
                }
            }

            string folderPath = projet.GetTempImageFolderPath();
            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };

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

            this.BeginInvoke((Action)(() =>
            {
                if (imageFiles.Length > 0)
                {
                    AppendTextToConsoleNL("FocusStack première image : " + imageFiles[0]);
                    EnqueueFocusStackTask(imageFiles, outputPath, maskPath, projet.ApplyMask, (int)actuatorAngle, turntablePosition, _Serie);
                }
                else
                {
                    EnqueueFocusStackTask(Array.Empty<string>(), outputPath, maskPath, projet.ApplyMask, (int)actuatorAngle, turntablePosition, _Serie, "Erreur");
                    AppendTextToConsoleNL("Aucune image trouvée dans le dossier.");
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

                    bool success = await RunFocusStack(nextTask.ImagePaths, nextTask.OutputPath);
                    nextTask.Status = success ? "Terminé" : "Erreur";
                    this.BeginInvoke((Action)(UpdateQueueDisplay));
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
                MessageBox.Show("focus-stack.exe introuvable !");
                return false;
            }

            string args = $" --output=\"{outputImage}\" " + string.Join(" ", imagePaths.Select(p => $"\"{p}\""));

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
                    MessageBox.Show("Erreur lors du traitement.");
                    return false;
                }
            }
        }


    }
}

