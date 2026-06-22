using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Aerolithe
{
    public partial class Aerolithe
    {
        private const int MetashapeProgressDisplayLineLimit = 8;
        private const int MetashapeStreamingErrorLineLimit = 120;

        private enum MetashapeMeteoriteType
        {
            Normale,
            Lisse,
            Unie,
            Reflechissante
        }

        private sealed record MetashapeAutomationPlan(
            string ProjectName,
            string ProjectFolder,
            string ProjectFile,
            string ModelsFolder,
            string MeasuresSerieA,
            string FocusStackA,
            string FocusStackB,
            string ScriptPath,
            MetashapeMeteoriteType MeteoriteType,
            bool SaveProject,
            bool ImportMeasures,
            bool DetectMarkers,
            bool ImportFocusStacks,
            bool AlignPhotos,
            bool BuildModels)
        {
            public bool GuidedMatching => MeteoriteType != MetashapeMeteoriteType.Normale;
            public string HighModelPath => Path.Combine(ModelsFolder, ProjectName + "_HR.glb");
            public string LowModelPath => Path.Combine(ModelsFolder, ProjectName + "_LR.glb");
        }

        private sealed record MetashapeLaunchTarget(string Path, bool IsMacAppBundle);

        private sealed record MacMetashapeHelperInstallResult(
            bool Success,
            bool InstalledOrUpdated,
            string Message,
            string QueueFolder,
            string LogFolder);

        private void ConfigureMetashapeMenu()
        {
            metashapeToolStripMenuItem.Click -= metashapeToolStripMenuItem_Click;
            metashapeToolStripMenuItem.DropDownItems.Clear();

            ToolStripMenuItem launch = new()
            {
                Text = "Lancer"
            };
            launch.Click += metashapeToolStripMenuItem_Click;

            ToolStripMenuItem settings = new()
            {
                Text = "Settings"
            };
            settings.Click += metashapeSettingsToolStripMenuItem_Click;

            ToolStripMenuItem terminal = new()
            {
                Text = "Terminal SSH/Python"
            };
            terminal.Click += metashapeTerminalToolStripMenuItem_Click;

            metashapeToolStripMenuItem.DropDownItems.Add(launch);
            metashapeToolStripMenuItem.DropDownItems.Add(settings);
            metashapeToolStripMenuItem.DropDownItems.Add(terminal);
        }

        private void metashapeToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                StartMetashapeAutomation();
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur Metashape: " + ex.Message, Color.Red);
                MessageBox.Show(
                    "Impossible de préparer Metashape.\n\n" + ex.Message,
                    "Metashape",
                    MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
        }

        private void metashapeSettingsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                MetashapeAutomationPlan plan = appSettings?.ProjectPath != null && projet != null
                    ? CreateMetashapeAutomationPlan(MetashapeMeteoriteType.Normale)
                    : CreateDefaultMetashapeAutomationPlan();
                TryConfigureMetashapeLaunch(plan, out _, settingsOnly: true);
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur settings Metashape: " + ex.Message, Color.Red);
                MessageBox.Show(
                    "Impossible d'ouvrir les settings Metashape.\n\n" + ex.Message,
                    "Metashape",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void metashapeTerminalToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                ShowMetashapeSshTerminal();
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur terminal SSH Metashape: " + ex.Message, Color.Red);
                MessageBox.Show(
                    "Impossible d'ouvrir le terminal SSH Metashape.\n\n" + ex.Message,
                    "Metashape",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ouvrirVisualisateurGLBToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                GlbViewerForm viewer = new();
                viewer.Show(this);
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL("Erreur visualisateur GLB: " + ex.Message, Color.Red);
                MessageBox.Show(
                    "Impossible d'ouvrir le visualisateur GLB.\n\n" + ex.Message,
                    "Visualisateur GLB",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void StartMetashapeAutomation()
        {
            if (string.IsNullOrWhiteSpace(appSettings?.ProjectPath) || projet == null)
            {
                MessageBox.Show("Svp ouvrir ou créer un projet avant de lancer Metashape.", "Metashape", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TrySelectMetashapeMeteoriteType(out MetashapeMeteoriteType meteoriteType))
            {
                return;
            }

            MetashapeAutomationPlan plan = CreateMetashapeAutomationPlan(meteoriteType);
            ValidateMetashapeAutomationPlan(plan);

            Directory.CreateDirectory(plan.ProjectFolder);
            Directory.CreateDirectory(plan.ModelsFolder);

            MetashapeLaunchTarget? metashapeTarget = FindMetashapeLaunchTarget();
            if (metashapeTarget == null)
            {
                AppendTextToConsoleNL("Metashape Pro introuvable. Script prêt à lancer manuellement: " + plan.ScriptPath, Color.Orange);
                MessageBox.Show(
                    "Le script Metashape a été généré, mais Aerolithe n'a pas trouvé metashape.exe ni MetashapePro.app.\n\nScript:\n" + plan.ScriptPath,
                    "Metashape",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                OpenExplorerAtProjectPath(plan.ProjectFolder);
                return;
            }

            string pythonCode = BuildMetashapePipelinePythonScript(
                plan,
                new HashSet<string>(GetDefaultMetashapePipelineStepIds(), StringComparer.OrdinalIgnoreCase),
                metashapeTarget.IsMacAppBundle ? ToMacPath : null);
            File.WriteAllText(plan.ScriptPath, pythonCode, Encoding.UTF8);
            AppendTextToConsoleNL("Script Metashape généré: " + plan.ScriptPath);

            ProcessStartInfo startInfo = new()
            {
                WorkingDirectory = plan.ProjectFolder
            };

            if (metashapeTarget.IsMacAppBundle)
            {
                StartMacMetashapeBatchWithProgress(plan, metashapeTarget.Path);
                return;
            }
            else
            {
                startInfo.FileName = metashapeTarget.Path;
                startInfo.UseShellExecute = false;
                startInfo.ArgumentList.Add("-r");
                startInfo.ArgumentList.Add(plan.ScriptPath);
            }

            Process.Start(startInfo);
            AppendTextToConsoleNL("Metashape lancé: " + metashapeTarget.Path);
        }

        private string WriteMacMetashapeHelperRequest(MetashapeAutomationPlan plan, string commandPath)
        {
            string queueFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Aerolithe",
                "metashape-queue");
            Directory.CreateDirectory(queueFolder);

            string requestId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string requestPath = Path.Combine(queueFolder, "metashape-current.json");
            string macCommandPath = ToMacPath(commandPath);

            if (Directory.EnumerateFiles(queueFolder, "*.json.running").Any())
            {
                throw new InvalidOperationException("Metashape semble déjà en cours via le helper. La nouvelle commande n'a pas été envoyée pour éviter une file d'attente implicite.");
            }

            foreach (string pendingRequest in Directory.EnumerateFiles(queueFolder, "*.json"))
            {
                File.Delete(pendingRequest);
            }

            StringBuilder json = new();
            json.AppendLine("{");
            json.AppendLine("  \"request_id\": " + JsonString(requestId) + ",");
            json.AppendLine("  \"created_at\": " + JsonString(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")) + ",");
            json.AppendLine("  \"project_name\": " + JsonString(plan.ProjectName) + ",");
            json.AppendLine("  \"project_folder\": " + JsonString(ToMacPath(plan.ProjectFolder)) + ",");
            json.AppendLine("  \"script_path\": " + JsonString(ToMacPath(plan.ScriptPath)) + ",");
            json.AppendLine("  \"command_path\": " + JsonString(macCommandPath));
            json.AppendLine("}");

            string tempPath = requestPath + ".tmp";
            File.WriteAllText(tempPath, json.ToString(), Encoding.UTF8);
            File.Move(tempPath, requestPath, overwrite: true);
            return requestPath;
        }

        private string LaunchMacMetashapeViaSsh(MetashapeAutomationPlan plan, string appBundlePath)
        {
            string scriptPath = ToMacPath(plan.ScriptPath);
            string commandPath = ToMacPath(WriteMacMetashapeCommand(plan, appBundlePath));
            string logPath = ToMacPath(Path.Combine(plan.ProjectFolder, plan.ProjectName + "_metashape_ssh.log"));
            string remoteShellCommand = "COMMAND_PATH="
                + BashString(commandPath)
                + "; LOG_PATH="
                + BashString(logPath)
                + "; SCRIPT_PATH="
                + BashString(scriptPath)
                + "; rm -f \"$LOG_PATH\"; "
                + "{ echo \"$(date)\"; echo \"COMMAND=$COMMAND_PATH\"; echo \"SCRIPT=$SCRIPT_PATH\"; "
                + "echo \"Méthode: ouvrir .command dans Terminal\"; } > \"$LOG_PATH\" 2>&1; "
                + "/bin/chmod +x \"$COMMAND_PATH\" >> \"$LOG_PATH\" 2>&1; "
                + "/usr/bin/open -a Terminal \"$COMMAND_PATH\" >> \"$LOG_PATH\" 2>&1; "
                + "OPEN_EXIT=$?; "
                + "sleep 3; "
                + "echo \"terminal open exit=$OPEN_EXIT\"; "
                + "echo \"COMMAND=$COMMAND_PATH\"; "
                + "echo \"SCRIPT=$SCRIPT_PATH\"; "
                + "echo \"LOG=$LOG_PATH\"; "
                + "echo \"Terminal ouvert; Metashape démarre depuis la fenêtre Terminal Mac.\"; "
                + "echo \"--- process Metashape ---\"; "
                + "pgrep -fl \"Metashape|MetaShape\" || echo \"Aucun process Metashape trouvé après 3 s; vérifier la fenêtre Terminal Mac.\"; "
                + "echo \"--- log début ---\"; "
                + "if [ -s \"$LOG_PATH\" ]; then tail -n 80 \"$LOG_PATH\"; else echo \"Log vide\"; fi; "
                + "echo \"--- log fin ---\"";

            MacSshCommandResult result = RunMacSshCommand("/bin/bash -lc " + BashString(remoteShellCommand), waitMilliseconds: 25000);
            return "Commande Metashape envoyée par SSH à "
                + GetConfiguredMetashapeSshUser()
                + "@"
                + result.Host
                + ".\n\nTest sans script à coller dans la console SSH:\n"
                + "/usr/bin/open -n \"/Applications/MetashapePro.app\"; echo \"open metashape exit=$?\"; sleep 8; pgrep -fl \"Metashape|MetaShape\" || echo \"Aucun Metashape trouvé\""
                + ".\n\nScript:\n"
                + scriptPath
                + "\n\nCommande Mac:\n"
                + commandPath
                + "\n\nLog Mac:\n"
                + logPath
                + (string.IsNullOrWhiteSpace(result.Output) ? string.Empty : "\n\nSSH:\n" + result.Output);
        }

        private string SendMacMetashapePythonViaSsh(string pythonCode)
        {
            string command = BuildSendPythonCodeToOpenMetashapeCommand(pythonCode);
            MacSshCommandResult result = RunMacSshCommand("/bin/bash -lc " + BashString(command), waitMilliseconds: 120000);
            return "Pipeline Metashape envoyé à "
                + GetConfiguredMetashapeSshUser()
                + "@"
                + result.Host
                + " via SSH/Python."
                + (string.IsNullOrWhiteSpace(result.Output) ? string.Empty : "\n\nSSH:\n" + result.Output);
        }

        private string SendMacMetashapePythonFileViaSsh(string macPythonScriptPath)
        {
            string command = BuildSendPythonFileToOpenMetashapeCommand(macPythonScriptPath);
            MacSshCommandResult result = RunMacSshCommand("/bin/bash -lc " + BashString(command), waitMilliseconds: 120000);
            return "Pipeline Metashape envoyé à "
                + GetConfiguredMetashapeSshUser()
                + "@"
                + result.Host
                + " via SSH/Python."
                + "\n\nScript Mac:\n"
                + macPythonScriptPath
                + (string.IsNullOrWhiteSpace(result.Output) ? string.Empty : "\n\nSSH:\n" + result.Output);
        }

        private void StartMacMetashapeBatchWithProgress(MetashapeAutomationPlan plan, string appBundlePath)
        {
            if (!ConfirmCloseRunningMacMetashapeBeforeBatch())
            {
                AppendTextToConsoleNL("Lancement Metashape annulé par l'utilisateur avant fermeture de la GUI.", Color.Orange);
                return;
            }

            string scriptPath = ToMacPath(plan.ScriptPath);
            string projectPath = ToMacPath(plan.ProjectFile);
            string pipelineLogPath = ToMacPath(Path.Combine(plan.ProjectFolder, plan.ProjectName + "_metashape.log"));
            string runnerLogPath = ToMacPath(Path.Combine(plan.ProjectFolder, plan.ProjectName + "_metashape_runner.log"));
            string remoteCommand = BuildMacMetashapeBatchCommand(appBundlePath, scriptPath, projectPath, pipelineLogPath, runnerLogPath);

            Form progressForm = CreateMetashapeProgressForm(
                plan,
                pipelineLogPath,
                runnerLogPath,
                out TextBox outputTextBox,
                out Label statusLabel,
                out ProgressBar progressBar,
                out Button cancelButton);
            progressForm.Show(this);
            int totalProgressSteps = GetDefaultMetashapePipelineStepIds().Count();
            Dictionary<string, int> progressStepIndexes = GetDefaultMetashapePipelineStepLabels()
                .Select((label, index) => new { label, index })
                .ToDictionary(item => item.label, item => item.index + 1, StringComparer.OrdinalIgnoreCase);
            HashSet<string> completedProgressSteps = new(StringComparer.OrdinalIgnoreCase);
            Queue<string> recentProgressLines = new();
            bool cancelRequested = false;

            void AppendProgress(string line)
            {
                if (progressForm.IsDisposed) return;

                try
                {
                    progressForm.BeginInvoke(new Action(() =>
                    {
                        if (outputTextBox.IsDisposed) return;
                        foreach (string physicalLine in SplitProgressLines(line))
                        {
                            recentProgressLines.Enqueue(physicalLine);
                            if (IsMetashapeErrorLine(physicalLine))
                            {
                                AppendTextToConsoleNL("Metashape: " + physicalLine, Color.Red);
                            }
                        }

                        while (recentProgressLines.Count > MetashapeProgressDisplayLineLimit)
                        {
                            recentProgressLines.Dequeue();
                        }

                        outputTextBox.Text = string.Join(Environment.NewLine, recentProgressLines);
                        outputTextBox.SelectionStart = outputTextBox.TextLength;
                        outputTextBox.ScrollToCaret();
                        outputTextBox.ClearUndo();
                        UpdateMetashapeProgressDisplay(line, statusLabel, progressBar, completedProgressSteps, progressStepIndexes, totalProgressSteps);
                    }));
                }
                catch
                {
                    // La fenêtre peut avoir été fermée pendant l'arrivée d'une ligne SSH.
                }
            }

            cancelButton.Click += (_, __) =>
            {
                if (cancelRequested) return;

                cancelRequested = true;
                cancelButton.Enabled = false;
                cancelButton.Text = "Annulation...";
                AppendProgress("Annulation demandée: arrêt du process Metashape batch.");
                _ = Task.Run(() => CancelMacMetashapeBatch(scriptPath, AppendProgress));
            };

            AppendTextToConsoleNL("Metashape lancé en traitement suivi: " + plan.ProjectName, Color.LightGreen);
            _ = Task.Run(() =>
            {
                try
                {
                    RunMacSshCommandStreaming(
                        "/bin/bash -lc " + BashString(remoteCommand),
                        AppendProgress,
                        TimeSpan.FromHours(12));
                    AppendProgress("");
                    AppendProgress("Traitement Metashape terminé.");
                    AppendTextToConsoleNL("Traitement Metashape terminé: " + plan.ProjectName, Color.LightGreen);
                    TryOpenLowModelViewer(plan, AppendProgress);
                }
                catch (Exception ex)
                {
                    AppendProgress("");
                    if (cancelRequested)
                    {
                        AppendProgress("Traitement Metashape annulé.");
                        AppendTextToConsoleNL("Traitement Metashape annulé: " + plan.ProjectName, Color.Orange);
                    }
                    else
                    {
                        AppendProgress("ERREUR: " + ex.Message);
                        AppendTextToConsoleNL("Erreur Metashape pendant le traitement suivi: voir la fenêtre Progression Metashape et les fichiers logs.", Color.Red);
                    }
                }
            });
        }

        private static bool IsMetashapeErrorLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;

            return line.Contains("ERREUR:", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Error:", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("RuntimeError:", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Traceback", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Lost exception", StringComparison.OrdinalIgnoreCase) ||
                   (line.Contains("Metashape exit=", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("Metashape exit=0", StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> SplitProgressLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield return string.Empty;
                yield break;
            }

            using StringReader reader = new(text);
            string? line;
            bool emitted = false;
            while ((line = reader.ReadLine()) != null)
            {
                emitted = true;
                yield return line;
            }

            if (!emitted)
            {
                yield return string.Empty;
            }
        }

        private void CancelMacMetashapeBatch(string scriptPath, Action<string> appendProgress)
        {
            try
            {
                string command =
                    "SCRIPT_PATH=" + BashString(scriptPath) + "; "
                    + "echo \"Arrêt Metashape pour script: $SCRIPT_PATH\"; "
                    + "pkill -TERM -f \"$SCRIPT_PATH\" >/dev/null 2>&1 || true; "
                    + "sleep 2; "
                    + "pkill -KILL -f \"$SCRIPT_PATH\" >/dev/null 2>&1 || true; "
                    + "echo \"Commande d'arrêt envoyée\"";
                MacSshCommandResult result = RunMacSshCommand("/bin/bash -lc " + BashString(command), waitMilliseconds: 10000);
                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    foreach (string line in result.Output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            appendProgress(line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                appendProgress("Annulation Metashape échouée: " + ex.Message);
                AppendTextToConsoleNL("Annulation Metashape échouée: " + ex.Message, Color.Orange);
            }
        }

        private void TryOpenLowModelViewer(MetashapeAutomationPlan plan, Action<string> appendProgress)
        {
            try
            {
                string lowModelPath = plan.LowModelPath;
                for (int i = 0; i < 20 && !File.Exists(lowModelPath); i++)
                {
                    Thread.Sleep(500);
                }

                if (!File.Exists(lowModelPath))
                {
                    appendProgress("GLB LR introuvable: " + lowModelPath);
                    AppendTextToConsoleNL("Viewer GLB LR non ouvert, fichier introuvable: " + lowModelPath, Color.Orange);
                    return;
                }

                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        GlbViewerForm viewer = new(lowModelPath);
                        viewer.Show(this);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Le modèle LR a été exporté, mais le viewer GLB n'a pas pu l'ouvrir.\n\n" +
                            lowModelPath + "\n\n" + ex.Message,
                            "Viewer GLB LR",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        AppendTextToConsoleNL("Viewer GLB LR échoué: " + ex.Message, Color.Orange);
                    }
                }));
            }
            catch (Exception ex)
            {
                appendProgress("Viewer GLB LR échoué: " + ex.Message);
                AppendTextToConsoleNL("Viewer GLB LR échoué: " + ex.Message, Color.Orange);
            }
        }

        private bool ConfirmCloseRunningMacMetashapeBeforeBatch()
        {
            try
            {
                MacSshCommandResult result = RunMacSshCommand(
                    "if pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1; then pgrep -fl \"Metashape|MetaShape\"; fi",
                    waitMilliseconds: 10000);

                if (string.IsNullOrWhiteSpace(result.Output))
                {
                    return true;
                }

                DialogResult answer = MessageBox.Show(
                    "Metashape est déjà ouvert sur le Mac.\n\n" +
                    "Pour lancer le traitement automatisé, Aérolithe doit fermer la fenêtre Metashape en cours afin d'éviter que le projet soit ouvert en lecture seule.\n\n" +
                    "Sauvegarde ton travail dans Metashape avant de continuer.\n\n" +
                    "Processus détecté:\n" + result.Output + "\n\n" +
                    "Fermer Metashape et lancer le traitement?",
                    "Fermer Metashape?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                return answer == DialogResult.Yes;
            }
            catch (Exception ex)
            {
                DialogResult answer = MessageBox.Show(
                    "Aérolithe n'a pas pu vérifier si Metashape est déjà ouvert sur le Mac.\n\n" +
                    "Détail: " + ex.Message + "\n\n" +
                    "Continuer quand même peut fermer Metashape si une instance est active et le batch doit prendre le contrôle du projet.\n\n" +
                    "Continuer?",
                    "Vérification Metashape impossible",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                return answer == DialogResult.Yes;
            }
        }

        private Form CreateMetashapeProgressForm(
            MetashapeAutomationPlan plan,
            string pipelineLogPath,
            string runnerLogPath,
            out TextBox outputTextBox,
            out Label statusLabel,
            out ProgressBar progressBar,
            out Button cancelButton)
        {
            Form dialog = new()
            {
                Text = "Progression Metashape - " + plan.ProjectName,
                Icon = this.Icon,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = true,
                MaximizeBox = true,
                ClientSize = new Size(900, 250),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            Label title = new()
            {
                Text = "Metashape travaille en arrière-plan. Aérolithe reste utilisable.",
                Dock = DockStyle.Top,
                Height = 34,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0),
                ForeColor = Color.White
            };

            statusLabel = new Label
            {
                Text = "Préparation du traitement Metashape...",
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0),
                ForeColor = Color.Gainsboro
            };

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 18,
                Minimum = 0,
                Maximum = Math.Max(1, GetDefaultMetashapePipelineStepIds().Count()),
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            outputTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.None,
                WordWrap = false,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F)
            };

            outputTextBox.Text =
                "Log pipeline Mac: " + pipelineLogPath + Environment.NewLine +
                "Log runner Mac: " + runnerLogPath + Environment.NewLine +
                "GLB LR: " + plan.LowModelPath + Environment.NewLine +
                Environment.NewLine;

            Button close = new()
            {
                Text = "Fermer",
                Dock = DockStyle.Right,
                Width = 110
            };
            close.Click += (_, __) => dialog.Close();

            cancelButton = new Button
            {
                Text = "Annuler",
                Dock = DockStyle.Right,
                Width = 110
            };

            Panel bottom = new()
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            bottom.Controls.Add(close);
            bottom.Controls.Add(cancelButton);

            dialog.Controls.Add(outputTextBox);
            dialog.Controls.Add(bottom);
            dialog.Controls.Add(progressBar);
            dialog.Controls.Add(statusLabel);
            dialog.Controls.Add(title);
            return dialog;
        }

        private static void UpdateMetashapeProgressDisplay(
            string line,
            Label statusLabel,
            ProgressBar progressBar,
            HashSet<string> completedSteps,
            Dictionary<string, int> stepIndexes,
            int totalSteps)
        {
            if (statusLabel.IsDisposed || progressBar.IsDisposed) return;

            string message = ExtractMetashapePipelineMessage(line);
            if (string.IsNullOrWhiteSpace(message))
            {
                if (line.Contains("Lancement Metashape", StringComparison.OrdinalIgnoreCase))
                {
                    statusLabel.Text = "Démarrage de Metashape...";
                }
                else if (line.Contains("Fermeture de Metashape GUI", StringComparison.OrdinalIgnoreCase))
                {
                    statusLabel.Text = "Fermeture de Metashape avant traitement...";
                }
                else if (line.Contains("Metashape exit=0", StringComparison.OrdinalIgnoreCase))
                {
                    progressBar.Value = progressBar.Maximum;
                    statusLabel.Text = "Traitement Metashape terminé.";
                }
                else if (line.Contains("ERREUR:", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("Metashape exit=", StringComparison.OrdinalIgnoreCase))
                {
                    statusLabel.Text = "Erreur pendant le traitement Metashape.";
                }

                return;
            }

            if (message.StartsWith("START ", StringComparison.OrdinalIgnoreCase))
            {
                string step = message["START ".Length..].Trim();
                int stepIndex = stepIndexes.TryGetValue(step, out int index)
                    ? index
                    : Math.Min(completedSteps.Count + 1, totalSteps);
                statusLabel.Text = $"Étape {stepIndex}/{totalSteps}: {step}";
                return;
            }

            if (message.StartsWith("DONE ", StringComparison.OrdinalIgnoreCase))
            {
                string step = message["DONE ".Length..].Trim();
                completedSteps.Add(step);
                int stepIndex = stepIndexes.TryGetValue(step, out int doneIndex)
                    ? doneIndex
                    : completedSteps.Count;
                progressBar.Value = Math.Min(progressBar.Maximum, Math.Max(completedSteps.Count, stepIndex));
                statusLabel.Text = $"Étape complétée {stepIndex}/{totalSteps}: {step}";
                return;
            }

            if (message.Contains("PIPELINE TERMINE", StringComparison.OrdinalIgnoreCase))
            {
                progressBar.Value = progressBar.Maximum;
                statusLabel.Text = "Pipeline Metashape terminé.";
            }
        }

        private static string ExtractMetashapePipelineMessage(string line)
        {
            const string prefix = "[Aerolithe Pipeline] ";
            int index = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            return index < 0 ? string.Empty : line[(index + prefix.Length)..].Trim();
        }

        private string BuildMacMetashapeBatchCommand(
            string appBundlePath,
            string scriptPath,
            string projectPath,
            string pipelineLogPath,
            string runnerLogPath)
        {
            string appPath = GetMacAppBundlePathForMac(appBundlePath);
            string executableSelection = BuildMacMetashapeExecutableSelectionCommand(appBundlePath);
            return executableSelection
                + "METASHAPE_APP=" + BashString(appPath) + "; "
                + "SCRIPT_PATH=" + BashString(scriptPath) + "; "
                + "PROJECT_PATH=" + BashString(projectPath) + "; "
                + "PIPELINE_LOG=" + BashString(pipelineLogPath) + "; "
                + "RUNNER_LOG=" + BashString(runnerLogPath) + "; "
                + "mkdir -p \"$(dirname \"$PIPELINE_LOG\")\"; "
                + "[ -s \"$PIPELINE_LOG\" ] && cp \"$PIPELINE_LOG\" \"$PIPELINE_LOG.previous\" || true; "
                + "[ -s \"$RUNNER_LOG\" ] && cp \"$RUNNER_LOG\" \"$RUNNER_LOG.previous\" || true; "
                + ": > \"$PIPELINE_LOG\"; "
                + ": > \"$RUNNER_LOG\"; "
                + "echo \"$(date '+%Y-%m-%d %H:%M:%S') Lancement Metashape\"; "
                + "echo \"Script: $SCRIPT_PATH\"; "
                + "echo \"Projet: $PROJECT_PATH\"; "
                + "if pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1; then "
                + "echo \"Fermeture de Metashape GUI pour éviter un projet read-only\"; "
                + "/usr/bin/osascript -e 'tell application \"MetashapePro\" to quit' >/dev/null 2>&1 || true; "
                + "/usr/bin/osascript -e 'tell application \"Metashape\" to quit' >/dev/null 2>&1 || true; "
                + "for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20; do "
                + "pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1 || break; sleep 1; "
                + "done; "
                + "if pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1; then "
                + "echo \"ERREUR: Metashape est encore ouvert apres la demande de fermeture. Traitement annule pour eviter un projet read-only.\"; "
                + "pgrep -fl \"Metashape|MetaShape\"; "
                + "exit 87; "
                + "fi; "
                + "fi; "
                + "echo \"Exécution: $METASHAPE_EXE -r $SCRIPT_PATH\"; "
                + "\"$METASHAPE_EXE\" -r \"$SCRIPT_PATH\" > \"$RUNNER_LOG\" 2>&1 & "
                + "MS_PID=$!; "
                + "tail -n " + MetashapeProgressDisplayLineLimit.ToString(CultureInfo.InvariantCulture) + " -f \"$PIPELINE_LOG\" & PIPE_TAIL_PID=$!; "
                + "tail -n " + MetashapeProgressDisplayLineLimit.ToString(CultureInfo.InvariantCulture) + " -f \"$RUNNER_LOG\" & RUN_TAIL_PID=$!; "
                + "wait \"$MS_PID\"; STATUS=$?; "
                + "kill \"$PIPE_TAIL_PID\" \"$RUN_TAIL_PID\" >/dev/null 2>&1 || true; "
                + "wait \"$PIPE_TAIL_PID\" \"$RUN_TAIL_PID\" >/dev/null 2>&1 || true; "
                + "echo \"Metashape exit=$STATUS\"; "
                + "if [ \"$STATUS\" -eq 0 ]; then "
                + "echo \"Ouverture du projet dans Metashape GUI\"; "
                + "/usr/bin/open -a \"$METASHAPE_APP\" \"$PROJECT_PATH\" || true; "
                + "fi; "
                + "exit \"$STATUS\"";
        }

        private string TestMacMetashapeSsh()
        {
            MacSshCommandResult result = RunMacSshCommand("echo Aerolithe SSH OK && hostname && whoami", waitMilliseconds: 10000);
            return "SSH OK via " + result.Host + Environment.NewLine + result.Output;
        }

        private void ShowMetashapeSshTerminal()
        {
            MetashapeAutomationPlan terminalPlan = appSettings?.ProjectPath != null && projet != null
                ? CreateMetashapeAutomationPlan(MetashapeMeteoriteType.Normale)
                : CreateDefaultMetashapeAutomationPlan();
            Directory.CreateDirectory(terminalPlan.ProjectFolder);
            using Form dialog = new()
            {
                Text = "Terminal SSH/Python Metashape",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimizeBox = false,
                MaximizeBox = true,
                ClientSize = new Size(1040, 900),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            TextBox startMetashapeTextBox = CreateMetashapeSshCommandTextBox(BuildDefaultMetashapeTerminalCommand());
            TextBox pythonCodeTextBox = new()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                AcceptsTab = true,
                ReadOnly = false,
                Enabled = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            TextBox outputTextBox = new()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.FixedSingle
            };

            CheckBox guidedMatchingCheckBox = CreateMetashapeCheckBox("Guided Image Matching", false);

            string[] pipelineStepIds = GetDefaultMetashapePipelineStepIds().ToArray();
            string[] pipelineStepLabels =
            {
                "Importer mesures",
                "Détecter marqueurs",
                "Importer focus stacks",
                "Aligner photos",
                "Scale bar 25 mm",
                "Désactiver mesures",
                "Construire modèle HR",
                "Texture modèle HR",
                "Construire modèle LR",
                "Texture modèle LR",
                "Zoom modèle 3D",
                "Exporter modèles"
            };
            CheckedListBox pipelineStepsListBox = new()
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            for (int i = 0; i < pipelineStepLabels.Length; i++)
            {
                pipelineStepsListBox.Items.Add(pipelineStepLabels[i], true);
            }

            void RunTerminalCommand(TextBox sourceTextBox)
            {
                try
                {
                    string command = sourceTextBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(command))
                    {
                        return;
                    }

                    outputTextBox.Text = "Commande envoyée..." + Environment.NewLine;
                    MacSshCommandResult result = RunMacSshCommand("/bin/bash -lc " + BashString(command), waitMilliseconds: 30000);
                    outputTextBox.Text = "Hôte: " + result.Host + Environment.NewLine + Environment.NewLine + result.Output;
                    AppendTextToConsoleNL("Terminal SSH Metashape OK via " + result.Host, Color.LightGreen);
                }
                catch (Exception ex)
                {
                    outputTextBox.Text = ex.Message;
                    AppendTextToConsoleNL("Terminal SSH Metashape échoué: " + ex.Message, Color.Orange);
                }
            }

            void SendPythonCode()
            {
                try
                {
                    string pythonCode = pythonCodeTextBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(pythonCode))
                    {
                        return;
                    }

                    outputTextBox.Text = "Code Python en cours d'envoi..." + Environment.NewLine
                        + "Taille: " + pythonCode.Length.ToString(CultureInfo.InvariantCulture) + " caractère(s)" + Environment.NewLine;
                    string command = BuildSendPythonCodeToOpenMetashapeCommand(pythonCode);
                    MacSshCommandResult result = RunMacSshCommand("/bin/bash -lc " + BashString(command), waitMilliseconds: 120000);
                    outputTextBox.Text = "Hôte: " + result.Host + Environment.NewLine + Environment.NewLine + result.Output;
                    AppendTextToConsoleNL("Code Python envoyé à Metashape via " + result.Host, Color.LightGreen);
                }
                catch (Exception ex)
                {
                    outputTextBox.Text = ex.Message;
                    AppendTextToConsoleNL("Envoi Python Metashape échoué: " + ex.Message, Color.Orange);
                }
            }

            void GeneratePipelineCode()
            {
                HashSet<string> selectedSteps = new(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < pipelineStepsListBox.Items.Count; i++)
                {
                    if (pipelineStepsListBox.GetItemChecked(i))
                    {
                        selectedSteps.Add(pipelineStepIds[i]);
                    }
                }

                if (selectedSteps.Count == 0)
                {
                    outputTextBox.Text = "Aucune étape cochée.";
                    return;
                }

                MetashapeAutomationPlan pipelinePlan = guidedMatchingCheckBox.Checked
                    ? terminalPlan with { MeteoriteType = MetashapeMeteoriteType.Lisse }
                    : terminalPlan with { MeteoriteType = MetashapeMeteoriteType.Normale };
                pythonCodeTextBox.Text = BuildMetashapePipelinePythonScript(pipelinePlan, selectedSteps, ToMacPath);
                pythonCodeTextBox.SelectionStart = 0;
                pythonCodeTextBox.SelectionLength = 0;
                pythonCodeTextBox.ScrollToCaret();
                outputTextBox.Text = "Pipeline généré: " + selectedSteps.Count + " étape(s)." + Environment.NewLine
                    + "Le champ Code Python a été remplacé au complet. Cliquez Envoyer pour l'exécuter dans Metashape.";
            }

            TableLayoutPanel startRow = CreateMetashapeSshCommandRow("Démarrer Metashape", startMetashapeTextBox, () => RunTerminalCommand(startMetashapeTextBox));
            TableLayoutPanel pythonCodeRow = CreateMetashapePythonCodeRow(
                pythonCodeTextBox,
                SendPythonCode,
                () =>
                {
                    pythonCodeTextBox.Clear();
                    outputTextBox.Text = "Code Python effacé.";
                    pythonCodeTextBox.Focus();
                });
            TableLayoutPanel pipelineRow = CreateMetashapePipelineRow(pipelineStepsListBox, guidedMatchingCheckBox, GeneratePipelineCode);

            Button close = new()
            {
                Text = "Fermer",
                DialogResult = DialogResult.Cancel,
                Dock = DockStyle.Fill
            };

            TableLayoutPanel buttons = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3
            };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            buttons.Controls.Add(close, 1, 0);

            TableLayoutPanel body = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 10,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 240F));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 12F));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            body.Controls.Add(CreateMetashapeLabel("Commande Mac"), 0, 0);
            body.Controls.Add(startRow, 0, 1);
            body.Controls.Add(CreateMetashapeLabel("Pipeline"), 0, 2);
            body.Controls.Add(pipelineRow, 0, 3);
            body.Controls.Add(CreateMetashapeLabel("Code Python"), 0, 4);
            body.Controls.Add(pythonCodeRow, 0, 5);
            body.Controls.Add(CreateMetashapeLabel("Sortie"), 0, 6);
            body.Controls.Add(outputTextBox, 0, 7);
            body.Controls.Add(buttons, 0, 9);

            dialog.Controls.Add(body);
            dialog.CancelButton = close;
            dialog.ShowDialog(this);
        }

        private string BuildDefaultMetashapeTerminalCommand()
        {
            return "if pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1; then echo \"Metashape déjà ouvert\"; pgrep -fl \"Metashape|MetaShape\"; else /usr/bin/open -n \"/Applications/MetashapePro.app\"; echo \"open metashape exit=$?\"; sleep 8; pgrep -fl \"Metashape|MetaShape\" || echo \"Aucun Metashape trouvé\"; fi";
        }

        private static TextBox CreateMetashapeSshCommandTextBox(string command)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                AcceptsTab = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = command
            };
        }

        private TableLayoutPanel CreateMetashapeSshCommandRow(string buttonText, TextBox commandTextBox, Action runCommand)
        {
            TableLayoutPanel row = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));

            Button run = new()
            {
                Text = buttonText,
                Dock = DockStyle.Fill
            };
            run.Click += (_, __) => runCommand();

            row.Controls.Add(commandTextBox, 0, 0);
            row.Controls.Add(run, 1, 0);
            return row;
        }

        private TableLayoutPanel CreateMetashapePythonCodeRow(TextBox codeTextBox, Action sendPythonCode, Action clearPythonCode)
        {
            TableLayoutPanel row = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));

            Button send = new()
            {
                Text = "Envoyer",
                Dock = DockStyle.Fill
            };
            send.Click += (_, __) => sendPythonCode();

            Button clear = new()
            {
                Text = "Clear",
                Dock = DockStyle.Fill
            };
            clear.Click += (_, __) => clearPythonCode();

            row.Controls.Add(codeTextBox, 0, 0);
            row.Controls.Add(send, 1, 0);
            row.Controls.Add(clear, 2, 0);
            return row;
        }

        private TableLayoutPanel CreateMetashapePipelineRow(CheckedListBox stepsListBox, CheckBox guidedMatchingCheckBox, Action generatePipeline)
        {
            TableLayoutPanel row = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            row.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Button generate = new()
            {
                Text = "Générer pipeline",
                Dock = DockStyle.Fill
            };
            generate.Click += (_, __) => generatePipeline();

            row.Controls.Add(guidedMatchingCheckBox, 0, 0);
            row.Controls.Add(stepsListBox, 0, 1);
            row.Controls.Add(generate, 1, 0);
            row.SetRowSpan(generate, 2);
            return row;
        }

        private static IEnumerable<string> GetDefaultMetashapePipelineStepIds()
        {
            return new[]
            {
                "import_measures",
                "detect_markers",
                "import_focus_stacks",
                "align_photos",
                "scale_bar_25mm",
                "disable_measures",
                "build_hr_model",
                "texture_hr_model",
                "build_lr_model",
                "texture_lr_model",
                "zoom_model",
                "export_models"
            };
        }

        private static IEnumerable<string> GetDefaultMetashapePipelineStepLabels()
        {
            return new[]
            {
                "Import mesures",
                "Detect markers",
                "Import focus stacks",
                "Align photos",
                "Scale bar 25mm",
                "Disable measures",
                "Build HR model",
                "Texture HR model",
                "Build LR model",
                "Texture LR model",
                "Zoom model",
                "Export models"
            };
        }

        private string BuildMetashapePipelinePythonScript(MetashapeAutomationPlan plan, ISet<string> selectedSteps, Func<string, string>? pathMapper = null)
        {
            bool importMeasures = selectedSteps.Contains("import_measures");
            bool detectMarkers = selectedSteps.Contains("detect_markers");
            bool importFocusStacks = selectedSteps.Contains("import_focus_stacks");
            bool alignPhotos = selectedSteps.Contains("align_photos");
            bool disableMeasures = selectedSteps.Contains("disable_measures");
            bool buildHrModel = selectedSteps.Contains("build_hr_model");
            bool textureHrModel = selectedSteps.Contains("texture_hr_model");
            bool buildLrModel = selectedSteps.Contains("build_lr_model");
            bool textureLrModel = selectedSteps.Contains("texture_lr_model");
            bool scaleBar25mm = selectedSteps.Contains("scale_bar_25mm");
            bool zoomModel = selectedSteps.Contains("zoom_model");
            bool exportModels = selectedSteps.Contains("export_models");

            string Map(string value) => pathMapper == null ? value : pathMapper(value);

            StringBuilder script = new();
            script.AppendLine("import math");
            script.AppendLine("import os");
            script.AppendLine("import Metashape");
            script.AppendLine();
            script.AppendLine("PROJECT_PATH = " + PythonString(Map(plan.ProjectFile)));
            script.AppendLine("MEASURES_SERIE_A = " + PythonString(Map(plan.MeasuresSerieA)));
            script.AppendLine("FOCUS_STACK_A = " + PythonString(Map(plan.FocusStackA)));
            script.AppendLine("FOCUS_STACK_B = " + PythonString(Map(plan.FocusStackB)));
            script.AppendLine("HR_MODEL_PATH = " + PythonString(Map(plan.HighModelPath)));
            script.AppendLine("LR_MODEL_PATH = " + PythonString(Map(plan.LowModelPath)));
            script.AppendLine("HR_MODEL_LABEL = 'Aerolithe HR'");
            script.AppendLine("LR_MODEL_LABEL = 'Aerolithe LR'");
            script.AppendLine("GUIDED_MATCHING = " + (plan.GuidedMatching ? "True" : "False"));
            script.AppendLine("IMAGE_EXTENSIONS = ('.jpg', '.jpeg', '.tif', '.tiff', '.png')");
            script.AppendLine("LOG_PATH = os.path.splitext(PROJECT_PATH)[0] + '_metashape.log'");
            script.AppendLine();
            script.AppendLine("def log(message):");
            script.AppendLine("    line = '[Aerolithe Pipeline] ' + str(message)");
            script.AppendLine("    try:");
            script.AppendLine("        os.makedirs(os.path.dirname(LOG_PATH), exist_ok=True)");
            script.AppendLine("        with open(LOG_PATH, 'a', encoding='utf-8') as log_file:");
            script.AppendLine("            log_file.write(line + '\\n')");
            script.AppendLine("    except Exception:");
            script.AppendLine("        pass");
            script.AppendLine();
            script.AppendLine("def describe_selection():");
            script.AppendLine("    selected = []");
            if (importMeasures) script.AppendLine("    selected.append('Importer mesures')");
            if (detectMarkers) script.AppendLine("    selected.append('Detecter marqueurs')");
            if (importFocusStacks) script.AppendLine("    selected.append('Importer focus stacks')");
            if (alignPhotos) script.AppendLine("    selected.append('Aligner photos')");
            if (scaleBar25mm) script.AppendLine("    selected.append('Scale bar 25 mm')");
            if (disableMeasures) script.AppendLine("    selected.append('Desactiver mesures')");
            if (buildHrModel) script.AppendLine("    selected.append('Construire modele HR')");
            if (textureHrModel) script.AppendLine("    selected.append('Texture modele HR')");
            if (buildLrModel) script.AppendLine("    selected.append('Construire modele LR')");
            if (textureLrModel) script.AppendLine("    selected.append('Texture modele LR')");
            if (zoomModel) script.AppendLine("    selected.append('Zoom modele 3D')");
            if (exportModels) script.AppendLine("    selected.append('Exporter modeles')");
            script.AppendLine("    return ', '.join(selected)");
            script.AppendLine();
            script.AppendLine("def existing_folder(path):");
            script.AppendLine("    candidates = [path]");
            script.AppendLine("    candidates.append(path.replace('/images/', '/Images/'))");
            script.AppendLine("    candidates.append(path.replace('/Images/', '/images/'))");
            script.AppendLine("    for candidate in candidates:");
            script.AppendLine("        if os.path.isdir(candidate):");
            script.AppendLine("            return candidate");
            script.AppendLine("    raise RuntimeError('Dossier introuvable: ' + path)");
            script.AppendLine();
            script.AppendLine("def image_files(folder):");
            script.AppendLine("    folder = existing_folder(folder)");
            script.AppendLine("    files = []");
            script.AppendLine("    for name in sorted(os.listdir(folder)):");
            script.AppendLine("        path = os.path.join(folder, name)");
            script.AppendLine("        if os.path.isfile(path) and name.lower().endswith(IMAGE_EXTENSIONS):");
            script.AppendLine("            files.append(path)");
            script.AppendLine("    if not files:");
            script.AppendLine("        raise RuntimeError('Aucune image trouvee dans ' + folder)");
            script.AppendLine("    return files");
            script.AppendLine();
            script.AppendLine("def save_step(doc, label):");
            script.AppendLine("    os.makedirs(os.path.dirname(PROJECT_PATH), exist_ok=True)");
            script.AppendLine("    current_path = str(getattr(doc, 'path', '') or '')");
            script.AppendLine("    try:");
            script.AppendLine("        if current_path and os.path.abspath(current_path) == os.path.abspath(PROJECT_PATH):");
            script.AppendLine("            doc.save()");
            script.AppendLine("        else:");
            script.AppendLine("            doc.save(PROJECT_PATH)");
            script.AppendLine("    except OSError as exc:");
            script.AppendLine("        message = str(exc)");
            script.AppendLine("        if 'read-only' in message.lower() or 'editing is disabled' in message.lower():");
            script.AppendLine("            raise RuntimeError('Projet Metashape ouvert en lecture seule pendant ' + label + '. Fermer toute fenetre Metashape qui utilise ' + PROJECT_PATH + ', puis relancer.')");
            script.AppendLine("        raise");
            script.AppendLine("    log(label + ' - projet sauvegarde')");
            script.AppendLine();
            script.AppendLine("def current_chunk(doc):");
            script.AppendLine("    chunk = doc.chunk");
            script.AppendLine("    if chunk is None and len(doc.chunks):");
            script.AppendLine("        chunk = doc.chunks[0]");
            script.AppendLine("    if chunk is None:");
            script.AppendLine("        raise RuntimeError('Aucun chunk actif')");
            script.AppendLine("    return chunk");
            script.AppendLine();
            script.AppendLine("def ensure_project_loaded(doc):");
            script.AppendLine("    current_path = str(getattr(doc, 'path', '') or '')");
            script.AppendLine("    if current_path and os.path.abspath(current_path) != os.path.abspath(PROJECT_PATH):");
            script.AppendLine("        log('Projet Metashape ouvert different: ' + current_path)");
            script.AppendLine("        try:");
            script.AppendLine("            doc.save()");
            script.AppendLine("            log('Projet ouvert sauvegarde avant changement')");
            script.AppendLine("        except Exception as exc:");
            script.AppendLine("            log('Sauvegarde du projet ouvert impossible: ' + str(exc))");
            script.AppendLine("        log('Ouverture du bon projet: ' + PROJECT_PATH)");
            script.AppendLine("        doc.open(PROJECT_PATH)");
            script.AppendLine("        return doc");
            script.AppendLine("    if len(doc.chunks):");
            script.AppendLine("        return doc");
            script.AppendLine("    if not os.path.isfile(PROJECT_PATH):");
            script.AppendLine("        raise RuntimeError('Projet Metashape introuvable: ' + PROJECT_PATH)");
            script.AppendLine("    log('Chargement du projet: ' + PROJECT_PATH)");
            script.AppendLine("    doc.open(PROJECT_PATH)");
            script.AppendLine("    return doc");
            script.AppendLine();
            script.AppendLine("def prepare_project_for_import(doc):");
            script.AppendLine("    os.makedirs(os.path.dirname(PROJECT_PATH), exist_ok=True)");
            script.AppendLine("    current_path = str(getattr(doc, 'path', '') or '')");
            script.AppendLine("    if current_path and os.path.abspath(current_path) != os.path.abspath(PROJECT_PATH):");
            script.AppendLine("        log('Projet Metashape ouvert different: ' + current_path)");
            script.AppendLine("        try:");
            script.AppendLine("            doc.save()");
            script.AppendLine("            log('Projet ouvert sauvegarde avant changement')");
            script.AppendLine("        except Exception as exc:");
            script.AppendLine("            log('Sauvegarde du projet ouvert impossible: ' + str(exc))");
            script.AppendLine("        if os.path.isfile(PROJECT_PATH):");
            script.AppendLine("            log('Ouverture du projet cible: ' + PROJECT_PATH)");
            script.AppendLine("            doc.open(PROJECT_PATH)");
            script.AppendLine("            return doc");
            script.AppendLine("    if current_path and os.path.abspath(current_path) == os.path.abspath(PROJECT_PATH):");
            script.AppendLine("        return doc");
            script.AppendLine("    log('Creation/attachement du projet Metashape: ' + PROJECT_PATH)");
            script.AppendLine("    doc.save(PROJECT_PATH)");
            script.AppendLine("    return doc");
            script.AppendLine();
            script.AppendLine("def step_import_measures(doc):");
            script.AppendLine("    doc = prepare_project_for_import(doc)");
            script.AppendLine("    while len(doc.chunks):");
            script.AppendLine("        doc.remove(doc.chunks[-1])");
            script.AppendLine("    chunk = doc.addChunk()");
            script.AppendLine("    chunk.label = 'Aerolithe - mesures serie A'");
            script.AppendLine("    images = image_files(MEASURES_SERIE_A)");
            script.AppendLine("    log('Import mesures: ' + str(len(images)) + ' images')");
            script.AppendLine("    chunk.addPhotos(images)");
            script.AppendLine("    save_step(doc, 'Import mesures')");
            script.AppendLine();
            script.AppendLine("def step_detect_markers(doc):");
            script.AppendLine("    chunk = current_chunk(doc)");
            script.AppendLine("    for camera in chunk.cameras:");
            script.AppendLine("        camera.selected = True");
            script.AppendLine("    log('Images selectionnees: ' + str(len(chunk.cameras)))");
            script.AppendLine("    chunk.detectMarkers(target_type=Metashape.CircularTarget12bit, tolerance=50, inverted=False, noparity=True, merge_markers=True)");
            script.AppendLine("    log('Marqueurs detectes: ' + str(len(chunk.markers)))");
            script.AppendLine("    save_step(doc, 'Marqueurs')");
            script.AppendLine();
            script.AppendLine("def step_import_focus_stacks(doc):");
            script.AppendLine("    chunk = current_chunk(doc)");
            script.AppendLine("    focus_a = image_files(FOCUS_STACK_A)");
            script.AppendLine("    focus_b = image_files(FOCUS_STACK_B)");
            script.AppendLine("    log('Import focus stacks: ' + str(len(focus_a) + len(focus_b)) + ' images')");
            script.AppendLine("    chunk.addPhotos(focus_a)");
            script.AppendLine("    chunk.addPhotos(focus_b)");
            script.AppendLine("    save_step(doc, 'Import focus stacks')");
            script.AppendLine();
            script.AppendLine("def step_align_photos(doc):");
            script.AppendLine("    chunk = current_chunk(doc)");
            script.AppendLine("    log('Match photos')");
            script.AppendLine("    chunk.matchPhotos(downscale=1, generic_preselection=True, reference_preselection=False, keypoint_limit=60000, guided_matching=GUIDED_MATCHING, filter_stationary_points=True)");
            script.AppendLine("    log('Align cameras')");
            script.AppendLine("    chunk.alignCameras(adaptive_fitting=True)");
            script.AppendLine("    save_step(doc, 'Alignement')");
            script.AppendLine();
            script.AppendLine("def disable_cameras_from_folder(chunk, folder):");
            script.AppendLine("    try:");
            script.AppendLine("        folder = os.path.normcase(os.path.abspath(existing_folder(folder)))");
            script.AppendLine("    except Exception:");
            script.AppendLine("        return");
            script.AppendLine("    disabled = 0");
            script.AppendLine("    for camera in chunk.cameras:");
            script.AppendLine("        photo = getattr(camera, 'photo', None)");
            script.AppendLine("        path = getattr(photo, 'path', '') if photo else ''");
            script.AppendLine("        if path and os.path.normcase(os.path.abspath(path)).startswith(folder):");
            script.AppendLine("            camera.enabled = False");
            script.AppendLine("            disabled += 1");
            script.AppendLine("    log('Images de mesures desactivees: ' + str(disabled))");
            script.AppendLine("    return disabled");
            script.AppendLine();
            script.AppendLine("def step_disable_measures(doc):");
            script.AppendLine("    chunk = current_chunk(doc)");
            script.AppendLine("    disable_cameras_from_folder(chunk, MEASURES_SERIE_A)");
            script.AppendLine("    save_step(doc, 'Desactivation mesures')");
            script.AppendLine();
            script.AppendLine("def chunk_models(chunk):");
            script.AppendLine("    return list(getattr(chunk, 'models', []) or [])");
            script.AppendLine();
            script.AppendLine("def model_label(model):");
            script.AppendLine("    return str(getattr(model, 'label', '') or '')");
            script.AppendLine();
            script.AppendLine("def find_model(chunk, label):");
            script.AppendLine("    for model in chunk_models(chunk):");
            script.AppendLine("        if model_label(model) == label:");
            script.AppendLine("            return model");
            script.AppendLine("    current = getattr(chunk, 'model', None)");
            script.AppendLine("    if current is not None and model_label(current) == label:");
            script.AppendLine("        return current");
            script.AppendLine("    return None");
            script.AppendLine();
            script.AppendLine("def set_current_model(chunk, model):");
            script.AppendLine("    if model is None:");
            script.AppendLine("        raise RuntimeError('Modele introuvable')");
            script.AppendLine("    try:");
            script.AppendLine("        chunk.model = model");
            script.AppendLine("    except Exception as exc:");
            script.AppendLine("        log('Selection modele non supportee par API: ' + str(exc))");
            script.AppendLine("    return getattr(chunk, 'model', model)");
            script.AppendLine();
            script.AppendLine("def build_model(doc, face_count, label):");
            script.AppendLine("    chunk = current_chunk(doc)");
            script.AppendLine("    disable_cameras_from_folder(chunk, MEASURES_SERIE_A)");
            script.AppendLine("    keep_uv_mapping = getattr(Metashape, 'KeepUVMapping', getattr(Metashape, 'KeepUvMapping', Metashape.GenericMapping))");
            script.AppendLine("    log('Build depth maps ' + label)");
            script.AppendLine("    chunk.buildDepthMaps(downscale=2, filter_mode=Metashape.MildFiltering)");
            script.AppendLine("    save_step(doc, 'Depth maps ' + label)");
            script.AppendLine("    log('Build model ' + label)");
            script.AppendLine("    try:");
            script.AppendLine("        chunk.buildModel(source_data=Metashape.DepthMapsData, surface_type=Metashape.Arbitrary, interpolation=Metashape.EnabledInterpolation, face_count=face_count, replace_asset=False, build_texture=False)");
            script.AppendLine("    except TypeError:");
            script.AppendLine("        chunk.buildModel(source_data=Metashape.DepthMapsData, surface_type=Metashape.Arbitrary, interpolation=Metashape.EnabledInterpolation, face_count=face_count, build_texture=False)");
            script.AppendLine("    if getattr(chunk, 'model', None) is None:");
            script.AppendLine("        raise RuntimeError('Aucun modele actif apres build ' + label)");
            script.AppendLine("    chunk.model.label = label");
            script.AppendLine("    save_step(doc, 'Modele ' + label)");
            script.AppendLine("    return chunk.model");
            script.AppendLine();
            script.AppendLine("def texture_model(doc, label):");
            script.AppendLine("    chunk = current_chunk(doc)");
            script.AppendLine("    model = find_model(chunk, label)");
            script.AppendLine("    if model is not None:");
            script.AppendLine("        set_current_model(chunk, model)");
            script.AppendLine("    elif getattr(chunk, 'model', None) is None:");
            script.AppendLine("        raise RuntimeError('Aucun modele a texturer: ' + label)");
            script.AppendLine("    keep_uv_mapping = getattr(Metashape, 'KeepUVMapping', getattr(Metashape, 'KeepUvMapping', Metashape.GenericMapping))");
            script.AppendLine("    log('Build texture ' + label)");
            script.AppendLine("    chunk.buildUV(mapping_mode=keep_uv_mapping, page_count=2, texture_size=4096)");
            script.AppendLine("    model_class = getattr(Metashape, 'Model', None)");
            script.AppendLine("    diffuse_map = getattr(model_class, 'DiffuseMap', getattr(Metashape, 'DiffuseMap', None))");
            script.AppendLine("    texture_args = dict(source_data=Metashape.ImagesData, blending_mode=Metashape.MosaicBlending, texture_size=4096, fill_holes=True, ghosting_filter=True)");
            script.AppendLine("    if diffuse_map is not None:");
            script.AppendLine("        texture_args['texture_type'] = diffuse_map");
            script.AppendLine("    try:");
            script.AppendLine("        chunk.buildTexture(**texture_args)");
            script.AppendLine("    except TypeError:");
            script.AppendLine("        texture_args.pop('ghosting_filter', None)");
            script.AppendLine("        chunk.buildTexture(**texture_args)");
            script.AppendLine("    save_step(doc, 'Texture ' + label)");
            script.AppendLine();
            script.AppendLine("def step_build_hr_model(doc):");
            script.AppendLine("    return build_model(doc, Metashape.HighFaceCount, HR_MODEL_LABEL)");
            script.AppendLine();
            script.AppendLine("def step_texture_hr_model(doc):");
            script.AppendLine("    texture_model(doc, HR_MODEL_LABEL)");
            script.AppendLine();
            script.AppendLine("def step_build_lr_model(doc):");
            script.AppendLine("    return build_model(doc, Metashape.LowFaceCount, LR_MODEL_LABEL)");
            script.AppendLine();
            script.AppendLine("def step_texture_lr_model(doc):");
            script.AppendLine("    texture_model(doc, LR_MODEL_LABEL)");
            script.AppendLine();
            script.AppendLine("def marker_position(marker):");
            script.AppendLine("    position = getattr(marker, 'position', None)");
            script.AppendLine("    return position");
            script.AppendLine();
            script.AppendLine("def vector_average(points):");
            script.AppendLine("    if not points:");
            script.AppendLine("        return None");
            script.AppendLine("    total = points[0]");
            script.AppendLine("    for point in points[1:]:");
            script.AppendLine("        total = total + point");
            script.AppendLine("    return total * (1.0 / len(points))");
            script.AppendLine();
            script.AppendLine("def projection_coord(projection):");
            script.AppendLine("    coord = getattr(projection, 'coord', None)");
            script.AppendLine("    return coord");
            script.AppendLine();
            script.AppendLine("def estimate_marker_position_from_projections(chunk, marker):");
            script.AppendLine("    if marker_position(marker) is not None:");
            script.AppendLine("        return True");
            script.AppendLine("    projections = getattr(marker, 'projections', {}) or {}");
            script.AppendLine("    point_cloud = getattr(chunk, 'point_cloud', None)");
            script.AppendLine("    if point_cloud is None:");
            script.AppendLine("        point_cloud = getattr(chunk, 'tie_points', None)");
            script.AppendLine("    if point_cloud is None or not hasattr(point_cloud, 'pickPoint'):");
            script.AppendLine("        return False");
            script.AppendLine("    picked = []");
            script.AppendLine("    for camera, projection in projections.items():");
            script.AppendLine("        try:");
            script.AppendLine("            if getattr(camera, 'transform', None) is None:");
            script.AppendLine("                continue");
            script.AppendLine("            coord = projection_coord(projection)");
            script.AppendLine("            if coord is None:");
            script.AppendLine("                continue");
            script.AppendLine("            origin = camera.center");
            script.AppendLine("            target = camera.unproject(coord)");
            script.AppendLine("            point = point_cloud.pickPoint(origin, target)");
            script.AppendLine("            if point is not None:");
            script.AppendLine("                picked.append(point)");
            script.AppendLine("        except Exception:");
            script.AppendLine("            pass");
            script.AppendLine("    position = vector_average(picked)");
            script.AppendLine("    if position is None:");
            script.AppendLine("        return False");
            script.AppendLine("    marker.position = position");
            script.AppendLine("    return True");
            script.AppendLine();
            script.AppendLine("def ensure_marker_positions(chunk):");
            script.AppendLine("    estimated = 0");
            script.AppendLine("    projection_markers = 0");
            script.AppendLine("    for marker in chunk.markers:");
            script.AppendLine("        projections = getattr(marker, 'projections', {}) or {}");
            script.AppendLine("        aligned_projection_count = 0");
            script.AppendLine("        for camera in projections.keys():");
            script.AppendLine("            if getattr(camera, 'transform', None) is not None:");
            script.AppendLine("                aligned_projection_count += 1");
            script.AppendLine("        if aligned_projection_count >= 2:");
            script.AppendLine("            projection_markers += 1");
            script.AppendLine("        if estimate_marker_position_from_projections(chunk, marker):");
            script.AppendLine("            if aligned_projection_count >= 1:");
            script.AppendLine("                estimated += 1");
            script.AppendLine("    if estimated:");
            script.AppendLine("        log('Positions 3D estimees pour marqueurs: ' + str(estimated))");
            script.AppendLine("    return projection_markers");
            script.AppendLine();
            script.AppendLine("def marker_distance(marker_a, marker_b):");
            script.AppendLine("    position_a = marker_position(marker_a)");
            script.AppendLine("    position_b = marker_position(marker_b)");
            script.AppendLine("    if position_a is None or position_b is None:");
            script.AppendLine("        return None");
            script.AppendLine("    delta = position_a - position_b");
            script.AppendLine("    try:");
            script.AppendLine("        return float(delta.norm())");
            script.AppendLine("    except Exception:");
            script.AppendLine("        x = float(delta.x)");
            script.AppendLine("        y = float(delta.y)");
            script.AppendLine("        z = float(delta.z)");
            script.AppendLine("        return math.sqrt(x * x + y * y + z * z)");
            script.AppendLine();
            script.AppendLine("def marker_sort_key(marker):");
            script.AppendLine("    label = str(getattr(marker, 'label', '') or '')");
            script.AppendLine("    digits = ''.join([ch for ch in label if ch.isdigit()])");
            script.AppendLine("    if digits:");
            script.AppendLine("        try:");
            script.AppendLine("            return (0, int(digits), label)");
            script.AppendLine("        except Exception:");
            script.AppendLine("            pass");
            script.AppendLine("    return (1, label)");
            script.AppendLine();
            script.AppendLine("def marker_number(marker):");
            script.AppendLine("    label = str(getattr(marker, 'label', '') or '')");
            script.AppendLine("    digits = ''.join([ch for ch in label if ch.isdigit()])");
            script.AppendLine("    if not digits:");
            script.AppendLine("        return None");
            script.AppendLine("    try:");
            script.AppendLine("        return int(digits)");
            script.AppendLine("    except Exception:");
            script.AppendLine("        return None");
            script.AppendLine();
            script.AppendLine("def preferred_detected_marker_pair(markers):");
            script.AppendLine("    by_number = {}");
            script.AppendLine("    for marker in markers:");
            script.AppendLine("        number = marker_number(marker)");
            script.AppendLine("        if number is not None and number not in by_number:");
            script.AppendLine("            by_number[number] = marker");
            script.AppendLine("    preferred_pairs = [(19, 20), (11, 12), (5, 6), (21, 22), (14, 15), (23, 24), (1, 7), (14, 15), (20, 21)]");
            script.AppendLine("    for number_a, number_b in preferred_pairs:");
            script.AppendLine("        if number_a in by_number and number_b in by_number:");
            script.AppendLine("            return (by_number[number_a], by_number[number_b])");
            script.AppendLine("    sorted_markers = sorted(markers, key=marker_sort_key)");
            script.AppendLine("    for i in range(len(sorted_markers) - 1):");
            script.AppendLine("        number_a = marker_number(sorted_markers[i])");
            script.AppendLine("        number_b = marker_number(sorted_markers[i + 1])");
            script.AppendLine("        if number_a is not None and number_b is not None and abs(number_a - number_b) == 1:");
            script.AppendLine("            return (sorted_markers[i], sorted_markers[i + 1])");
            script.AppendLine("    if len(sorted_markers) >= 2:");
            script.AppendLine("        return (sorted_markers[0], sorted_markers[1])");
            script.AppendLine("    return None");
            script.AppendLine();
            script.AppendLine("def evaluate_marker_pair(best, marker_a, marker_b):");
            script.AppendLine("    distance = marker_distance(marker_a, marker_b)");
            script.AppendLine("    if distance is None or distance <= 0:");
            script.AppendLine("        return best");
            script.AppendLine("    if best is None or distance < best[0]:");
            script.AppendLine("        return (distance, marker_a, marker_b)");
            script.AppendLine("    return best");
            script.AppendLine();
            script.AppendLine("def closest_marker_pair(chunk):");
            script.AppendLine("    total_markers = len(chunk.markers)");
            script.AppendLine("    aligned_cameras = len([camera for camera in chunk.cameras if getattr(camera, 'transform', None) is not None])");
            script.AppendLine("    projection_markers = ensure_marker_positions(chunk)");
            script.AppendLine("    markers = sorted([marker for marker in chunk.markers if marker_position(marker) is not None], key=marker_sort_key)");
            script.AppendLine("    log('Scale bar diagnostic: marqueurs=' + str(total_markers) + ', marqueurs 3D=' + str(len(markers)) + ', marqueurs avec projections alignees=' + str(projection_markers) + ', cameras alignees=' + str(aligned_cameras) + '/' + str(len(chunk.cameras)))");
            script.AppendLine("    best = None");
            script.AppendLine("    if len(markers) >= 3:");
            script.AppendLine("        for i in range(len(markers) - 2):");
            script.AppendLine("            marker_1 = markers[i]");
            script.AppendLine("            marker_2 = markers[i + 1]");
            script.AppendLine("            marker_3 = markers[i + 2]");
            script.AppendLine("            best = evaluate_marker_pair(best, marker_1, marker_2)");
            script.AppendLine("            best = evaluate_marker_pair(best, marker_2, marker_3)");
            script.AppendLine("    if best is not None:");
            script.AppendLine("        return best");
            script.AppendLine("    all_markers = sorted(list(chunk.markers), key=marker_sort_key)");
            script.AppendLine("    if len(all_markers) < 2:");
            script.AppendLine("        raise RuntimeError('Scale bar impossible: moins de deux marqueurs detectes.')");
            script.AppendLine("    fallback_pair = preferred_detected_marker_pair(all_markers)");
            script.AppendLine("    if fallback_pair is None:");
            script.AppendLine("        raise RuntimeError('Scale bar impossible: aucune paire de marqueurs detectes.')");
            script.AppendLine("    log('Scale bar sans calcul 3D: utilisation de la paire detectee ' + fallback_pair[0].label + ' / ' + fallback_pair[1].label)");
            script.AppendLine("    return (None, fallback_pair[0], fallback_pair[1])");
            script.AppendLine();
            script.AppendLine("def step_scale_bar_25mm(doc):");
            script.AppendLine("    chunk = current_chunk(doc)");
            script.AppendLine("    distance_actuelle, marker_a, marker_b = closest_marker_pair(chunk)");
            script.AppendLine("    for marker in chunk.markers:");
            script.AppendLine("        try:");
            script.AppendLine("            marker.selected = False");
            script.AppendLine("        except Exception:");
            script.AppendLine("            pass");
            script.AppendLine("    marker_a.selected = True");
            script.AppendLine("    marker_b.selected = True");
            script.AppendLine("    scale_bar = chunk.addScalebar(marker_a, marker_b)");
            script.AppendLine("    scale_bar.label = 'Aerolithe scale bar 25 mm'");
            script.AppendLine("    scale_bar.reference.distance = 0.025");
            script.AppendLine("    if distance_actuelle is None:");
            script.AppendLine("        log('Scale bar 25 mm creee entre ' + marker_a.label + ' et ' + marker_b.label + ' sans distance 3D calculee')");
            script.AppendLine("        save_step(doc, 'Scale bar 25mm')");
            script.AppendLine("        return");
            script.AppendLine("    distance_cible = 0.025");
            script.AppendLine("    facteur = distance_cible / distance_actuelle");
            script.AppendLine("    matrix = chunk.transform.matrix");
            script.AppendLine("    if not matrix:");
            script.AppendLine("        raise RuntimeError('Scale bar impossible: matrice du chunk absente.')");
            script.AppendLine("    chunk.transform.matrix = Metashape.Matrix.Diag([facteur, facteur, facteur, 1]) * matrix");
            script.AppendLine("    log('Scale bar 25 mm creee entre ' + marker_a.label + ' et ' + marker_b.label)");
            script.AppendLine("    log('Distance brute: ' + str(distance_actuelle) + ' m; facteur: ' + str(facteur))");
            script.AppendLine("    log('Distance forcee: 0.025 m')");
            script.AppendLine("    save_step(doc, 'Scale bar 25mm')");
            script.AppendLine();
            script.AppendLine("def step_zoom_model(doc):");
            script.AppendLine("    log('Zoom modele 3D')");
            script.AppendLine("    try:");
            script.AppendLine("        Metashape.app.update()");
            script.AppendLine("    except Exception as exc:");
            script.AppendLine("        log('Update vue impossible: ' + str(exc))");
            script.AppendLine("    try:");
            script.AppendLine("        viewer = getattr(Metashape.app, 'model_view', None)");
            script.AppendLine("        if viewer is not None and hasattr(viewer, 'resetView'):");
            script.AppendLine("            viewer.resetView()");
            script.AppendLine("    except Exception as exc:");
            script.AppendLine("        log('Zoom automatique non supporte: ' + str(exc))");
            script.AppendLine();
            script.AppendLine("def export_model(doc, label, output_path):");
            script.AppendLine("    chunk = current_chunk(doc)");
            script.AppendLine("    os.makedirs(os.path.dirname(output_path), exist_ok=True)");
            script.AppendLine("    model = find_model(chunk, label)");
            script.AppendLine("    if model is None:");
            script.AppendLine("        raise RuntimeError('Modele a exporter introuvable: ' + label)");
            script.AppendLine("    set_current_model(chunk, model)");
            script.AppendLine("    log('Export GLB ' + label + ': ' + output_path)");
            script.AppendLine("    chunk.exportModel(output_path, format=Metashape.ModelFormatGLB, binary=True, save_texture=True, save_uv=True, save_normals=True, save_colors=True)");
            script.AppendLine("    save_step(doc, 'Export ' + label)");
            script.AppendLine();
            script.AppendLine("def step_export_models(doc):");
            script.AppendLine("    export_model(doc, HR_MODEL_LABEL, HR_MODEL_PATH)");
            script.AppendLine("    export_model(doc, LR_MODEL_LABEL, LR_MODEL_PATH)");
            script.AppendLine();
            script.AppendLine("doc = Metashape.app.document");
            script.AppendLine("log('SCRIPT RECU PAR METASHAPE')");
            script.AppendLine("log('Etapes selectionnees: ' + describe_selection())");
            script.AppendLine("log('Projet: ' + PROJECT_PATH)");
            if (!importMeasures)
            {
                script.AppendLine("doc = ensure_project_loaded(doc)");
            }
            script.AppendLine("steps = []");
            if (importMeasures) script.AppendLine("steps.append(('Import mesures', lambda: step_import_measures(doc)))");
            if (detectMarkers) script.AppendLine("steps.append(('Detect markers', lambda: step_detect_markers(doc)))");
            if (importFocusStacks) script.AppendLine("steps.append(('Import focus stacks', lambda: step_import_focus_stacks(doc)))");
            if (alignPhotos) script.AppendLine("steps.append(('Align photos', lambda: step_align_photos(doc)))");
            if (scaleBar25mm) script.AppendLine("steps.append(('Scale bar 25mm', lambda: step_scale_bar_25mm(doc)))");
            if (disableMeasures) script.AppendLine("steps.append(('Disable measures', lambda: step_disable_measures(doc)))");
            if (buildHrModel) script.AppendLine("steps.append(('Build HR model', lambda: step_build_hr_model(doc)))");
            if (textureHrModel) script.AppendLine("steps.append(('Texture HR model', lambda: step_texture_hr_model(doc)))");
            if (buildLrModel) script.AppendLine("steps.append(('Build LR model', lambda: step_build_lr_model(doc)))");
            if (textureLrModel) script.AppendLine("steps.append(('Texture LR model', lambda: step_texture_lr_model(doc)))");
            if (zoomModel) script.AppendLine("steps.append(('Zoom model', lambda: step_zoom_model(doc)))");
            if (exportModels) script.AppendLine("steps.append(('Export models', lambda: step_export_models(doc)))");
            script.AppendLine();
            script.AppendLine("for label, action in steps:");
            script.AppendLine("    log('START ' + label)");
            script.AppendLine("    action()");
            script.AppendLine("    log('DONE ' + label)");
            script.AppendLine("log('PIPELINE TERMINE')");
            return script.ToString();
        }

        private string BuildSendPythonCodeToOpenMetashapeCommand(string pythonCode)
        {
            pythonCode = NormalizePythonCodeForMetashapeConsole(pythonCode);
            string consoleCommand = "exec(" + JsonString(pythonCode) + ")";
            return BuildSendPythonConsoleCommandToOpenMetashapeCommand(consoleCommand);
        }

        private string BuildSendPythonFileToOpenMetashapeCommand(string macPythonScriptPath)
        {
            return "if [ ! -f " + BashString(macPythonScriptPath) + " ]; then "
                + "echo \"Script Python introuvable sur le Mac: " + macPythonScriptPath.Replace("\"", "\\\"") + "\"; exit 3; "
                + "fi; "
                + "if ! pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1; then "
                + "echo \"Metashape non ouvert; démarrage via SSH\"; "
                + "/usr/bin/open -n \"/Applications/MetashapePro.app\"; "
                + "echo \"open metashape exit=$?\"; "
                + "for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30; do "
                + "pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1 && break; "
                + "sleep 1; "
                + "done; "
                + "else "
                + "echo \"Metashape déjà ouvert\"; "
                + "fi; "
                + "if ! pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1; then "
                + "echo \"Metashape n'a pas démarré\"; exit 2; "
                + "fi; "
                + "echo \"Script Python prêt: " + macPythonScriptPath.Replace("\"", "\\\"") + "\"; "
                + "OSASCRIPT_OUTPUT=$(/usr/bin/osascript "
                + "-e 'set scriptPath to " + JsonString(macPythonScriptPath) + "' "
                + "-e 'tell application \"System Events\"' "
                + "-e 'set metashapeProcess to missing value' "
                + "-e 'repeat with candidateProcess in processes' "
                + "-e 'if (name of candidateProcess contains \"Metashape\") and ((count of windows of candidateProcess) > 0) then' "
                + "-e 'set metashapeProcess to candidateProcess' "
                + "-e 'exit repeat' "
                + "-e 'end if' "
                + "-e 'end repeat' "
                + "-e 'if metashapeProcess is missing value then error \"Aucune fenetre Metashape visible\"' "
                + "-e 'set frontmost of metashapeProcess to true' "
                + "-e 'delay 1.0' "
                + "-e 'set runScriptOpened to false' "
                + "-e 'try' "
                + "-e 'click menu item \"Run Script...\" of menu \"Tools\" of menu bar item \"Tools\" of menu bar 1 of metashapeProcess' "
                + "-e 'set runScriptOpened to true' "
                + "-e 'end try' "
                + "-e 'if runScriptOpened is false then' "
                + "-e 'try' "
                + "-e 'click menu item \"Run Script…\" of menu \"Tools\" of menu bar item \"Tools\" of menu bar 1 of metashapeProcess' "
                + "-e 'set runScriptOpened to true' "
                + "-e 'end try' "
                + "-e 'end if' "
                + "-e 'if runScriptOpened is false then error \"Menu Tools > Run Script introuvable\"' "
                + "-e 'delay 1.0' "
                + "-e 'keystroke \"g\" using {command down, shift down}' "
                + "-e 'delay 0.4' "
                + "-e 'set the clipboard to scriptPath' "
                + "-e 'keystroke \"a\" using command down' "
                + "-e 'delay 0.1' "
                + "-e 'keystroke \"v\" using command down' "
                + "-e 'delay 0.4' "
                + "-e 'key code 36' "
                + "-e 'delay 1.2' "
                + "-e 'try' "
                + "-e 'click button \"Open\" of window 1 of metashapeProcess' "
                + "-e 'on error' "
                + "-e 'try' "
                + "-e 'click button \"Ouvrir\" of window 1 of metashapeProcess' "
                + "-e 'on error' "
                + "-e 'key code 36' "
                + "-e 'end try' "
                + "-e 'end try' "
                + "-e 'end tell' 2>&1); "
                + "OSASCRIPT_EXIT=$?; "
                + "if [ -n \"$OSASCRIPT_OUTPUT\" ]; then echo \"$OSASCRIPT_OUTPUT\"; fi; "
                + "if [ \"$OSASCRIPT_EXIT\" -ne 0 ]; then "
                + "echo \"osascript a échoué avec le code $OSASCRIPT_EXIT\"; "
                + "echo \"Si l'erreur est 1002, autoriser /usr/bin/osascript, sshd-keygen-wrapper et Parallels Desktop dans Réglages système > Confidentialité et sécurité > Accessibilité\"; "
                + "exit \"$OSASCRIPT_EXIT\"; "
                + "fi; "
                + "echo \"Script Python demandé via Tools > Run Script\"";
        }

        private string BuildSendPythonConsoleCommandToOpenMetashapeCommand(string consoleCommand)
        {
            return "if ! pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1; then "
                + "echo \"Metashape non ouvert; démarrage via SSH\"; "
                + "/usr/bin/open -n \"/Applications/MetashapePro.app\"; "
                + "echo \"open metashape exit=$?\"; "
                + "for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30; do "
                + "pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1 && break; "
                + "sleep 1; "
                + "done; "
                + "else "
                + "echo \"Metashape déjà ouvert\"; "
                + "fi; "
                + "if ! pgrep -fl \"Metashape|MetaShape\" >/dev/null 2>&1; then "
                + "echo \"Metashape n'a pas démarré\"; exit 2; "
                + "fi; "
                + "printf %s "
                + BashString(consoleCommand)
                + " | /usr/bin/pbcopy; "
                + "echo \"Code Python copié dans le presse-papiers du Mac\"; "
                + "echo \"Taille presse-papiers: $(/usr/bin/pbpaste | /usr/bin/wc -c | /usr/bin/tr -d ' ') octets\"; "
                + "OSASCRIPT_OUTPUT=$(/usr/bin/osascript "
                + "-e 'tell application \"System Events\"' "
                + "-e 'set metashapeProcess to missing value' "
                + "-e 'repeat with candidateProcess in processes' "
                + "-e 'if (name of candidateProcess contains \"Metashape\") and ((count of windows of candidateProcess) > 0) then' "
                + "-e 'set metashapeProcess to candidateProcess' "
                + "-e 'exit repeat' "
                + "-e 'end if' "
                + "-e 'end repeat' "
                + "-e 'if metashapeProcess is missing value then error \"Aucune fenetre Metashape visible\"' "
                + "-e 'set frontmost of metashapeProcess to true' "
                + "-e 'delay 1.5' "
                + "-e 'repeat 30 times' "
                + "-e 'if (count of windows of metashapeProcess) > 0 then exit repeat' "
                + "-e 'delay 0.5' "
                + "-e 'end repeat' "
                + "-e 'delay 0.5' "
                + "-e 'set consoleHandled to false' "
                + "-e 'try' "
                + "-e 'set consoleItem to menu item \"Console\" of menu \"View\" of menu bar item \"View\" of menu bar 1 of metashapeProcess' "
                + "-e 'set consoleMark to \"\"' "
                + "-e 'try' "
                + "-e 'set consoleMark to value of attribute \"AXMenuItemMarkChar\" of consoleItem' "
                + "-e 'end try' "
                + "-e 'if consoleMark is \"\" or consoleMark is missing value then click consoleItem' "
                + "-e 'set consoleHandled to true' "
                + "-e 'delay 0.5' "
                + "-e 'end try' "
                + "-e 'if consoleHandled is false then' "
                + "-e 'try' "
                + "-e 'set consoleItem to menu item \"Console\" of menu \"Panes\" of menu item \"Panes\" of menu \"View\" of menu bar item \"View\" of menu bar 1 of metashapeProcess' "
                + "-e 'set consoleMark to \"\"' "
                + "-e 'try' "
                + "-e 'set consoleMark to value of attribute \"AXMenuItemMarkChar\" of consoleItem' "
                + "-e 'end try' "
                + "-e 'if consoleMark is \"\" or consoleMark is missing value then click consoleItem' "
                + "-e 'delay 0.5' "
                + "-e 'end try' "
                + "-e 'end if' "
                + "-e 'try' "
                + "-e 'set {winX, winY} to position of window 1 of metashapeProcess' "
                + "-e 'set {winW, winH} to size of window 1 of metashapeProcess' "
                + "-e 'click at {winX + 90, winY + winH - 24}' "
                + "-e 'delay 0.2' "
                + "-e 'click at {winX + 160, winY + winH - 24}' "
                + "-e 'delay 0.2' "
                + "-e 'end try' "
                + "-e 'delay 0.5' "
                + "-e 'keystroke \"v\" using command down' "
                + "-e 'delay 0.3' "
                + "-e 'key code 36' "
                + "-e 'end tell' 2>&1); "
                + "OSASCRIPT_EXIT=$?; "
                + "if [ -n \"$OSASCRIPT_OUTPUT\" ]; then echo \"$OSASCRIPT_OUTPUT\"; fi; "
                + "if [ \"$OSASCRIPT_EXIT\" -ne 0 ]; then "
                + "echo \"osascript a échoué avec le code $OSASCRIPT_EXIT\"; "
                + "echo \"Si l'erreur est 1002, autoriser /usr/bin/osascript, sshd-keygen-wrapper et Parallels Desktop dans Réglages système > Confidentialité et sécurité > Accessibilité\"; "
                + "exit \"$OSASCRIPT_EXIT\"; "
                + "fi; "
                + "echo \"Code Python envoyé à Metashape via presse-papiers\"";
        }

        private static string NormalizePythonCodeForMetashapeConsole(string pythonCode)
        {
            string normalized = (pythonCode ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();
            return RemoveAccidentalPythonIndentAfterFirstTopLevelLine(normalized);
        }

        private static string RemoveAccidentalPythonIndentAfterFirstTopLevelLine(string pythonCode)
        {
            string[] lines = pythonCode.Split('\n');
            int firstCodeLine = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
            if (firstCodeLine < 0)
            {
                return pythonCode;
            }

            if (LeadingSpaces(lines[firstCodeLine]) != 0)
            {
                return pythonCode;
            }

            int? commonIndent = null;
            for (int i = firstCodeLine + 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                int indent = LeadingSpaces(lines[i]);
                if (indent == 0)
                {
                    return pythonCode;
                }

                commonIndent = commonIndent.HasValue ? Math.Min(commonIndent.Value, indent) : indent;
            }

            if (!commonIndent.HasValue || commonIndent.Value <= 0 || commonIndent.Value > 4)
            {
                return pythonCode;
            }

            for (int i = firstCodeLine + 1; i < lines.Length; i++)
            {
                if (lines[i].Length >= commonIndent.Value)
                {
                    lines[i] = lines[i][commonIndent.Value..];
                }
            }

            return string.Join("\n", lines);
        }

        private static int LeadingSpaces(string value)
        {
            int count = 0;
            while (count < value.Length && value[count] == ' ')
            {
                count++;
            }

            return count;
        }

        private string BuildMacMetashapeAppBundleSelectionCommand(string appBundlePath)
        {
            StringBuilder command = new();
            command.Append("METASHAPE_APP=");
            command.Append(BashString(GetMacAppBundlePathForMac(appBundlePath)));
            command.Append("; if [ ! -d \"$METASHAPE_APP\" ]; then ");
            command.Append("for candidate in ");
            foreach (string candidate in GetMacMetashapeAppBundleCandidates(appBundlePath))
            {
                command.Append(BashString(candidate));
                command.Append(' ');
            }

            command.Append("; do if [ -d \"$candidate\" ]; then METASHAPE_APP=\"$candidate\"; break; fi; done; fi; ");
            command.Append("if [ ! -d \"$METASHAPE_APP\" ]; then echo \"Metashape.app introuvable\"; ");
            command.Append("echo \"Chemin essayé: $METASHAPE_APP\"; ");
            command.Append("ls -la /Applications | grep -i metashape || true; exit 127; fi; ");
            return command.ToString();
        }

        private string BuildMacMetashapeExecutableSelectionCommand(string appBundlePath)
        {
            StringBuilder command = new();
            command.Append("METASHAPE_EXE=");
            command.Append(BashString(GetMacAppExecutablePathForMac(appBundlePath)));
            command.Append("; if [ ! -x \"$METASHAPE_EXE\" ]; then ");
            command.Append("for candidate in ");
            foreach (string candidate in GetMacMetashapeExecutableCandidates(appBundlePath))
            {
                command.Append(BashString(candidate));
                command.Append(' ');
            }

            command.Append("; do if [ -x \"$candidate\" ]; then METASHAPE_EXE=\"$candidate\"; break; fi; done; fi; ");
            command.Append("if [ ! -x \"$METASHAPE_EXE\" ]; then echo \"Metashape executable introuvable\"; ");
            command.Append("echo \"Chemin essayé: $METASHAPE_EXE\"; ");
            command.Append("ls -la /Applications | grep -i metashape || true; exit 127; fi; ");
            return command.ToString();
        }

        private IEnumerable<string> GetMacMetashapeAppBundleCandidates(string appBundlePath)
        {
            string configured = GetMacAppBundlePathForMac(appBundlePath);
            string[] candidates =
            {
                configured,
                "/Applications/MetashapePro.app",
                "/Applications/MetaShapePro.app",
                "/Applications/Metashape.app"
            };

            return candidates
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal);
        }

        private IEnumerable<string> GetMacMetashapeExecutableCandidates(string appBundlePath)
        {
            string configured = GetMacAppExecutablePathForMac(appBundlePath);
            string[] candidates =
            {
                configured,
                "/Applications/MetashapePro.app/Contents/MacOS/MetashapePro",
                "/Applications/MetaShapePro.app/Contents/MacOS/MetashapePro",
                "/Applications/Metashape.app/Contents/MacOS/Metashape",
                "/Applications/MetashapePro.app/Contents/MacOS/Metashape",
                "/Applications/MetaShapePro.app/Contents/MacOS/Metashape"
            };

            return candidates
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal);
        }

        private sealed record MacSshCommandResult(string Host, string Output);

        private MacSshCommandResult RunMacSshCommand(string remoteCommand, int waitMilliseconds)
        {
            string user = GetConfiguredMetashapeSshUser();
            if (string.IsNullOrWhiteSpace(user))
            {
                throw new InvalidOperationException("Configurer l'utilisateur SSH Mac dans Metashape > Settings.");
            }

            List<string> errors = new();
            foreach (string host in GetMetashapeSshHostCandidates())
            {
                try
                {
                    string output = RunMacSshCommandForHost(user, host, GetConfiguredMetashapeSshKeyPath(), remoteCommand, waitMilliseconds);
                    if (!string.Equals(appSettings.MetashapeSshHost, host, StringComparison.OrdinalIgnoreCase))
                    {
                        appSettings.MetashapeSshHost = host;
                        appSettings.Save();
                    }

                    return new MacSshCommandResult(host, output);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is TimeoutException || ex is System.ComponentModel.Win32Exception)
                {
                    if (IsRemoteCommandExecutionFailure(ex))
                    {
                        throw;
                    }

                    errors.Add(host + ": " + ex.Message);
                }
            }

            throw new InvalidOperationException("SSH Metashape a échoué pour tous les hôtes essayés." + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        private void RunMacSshCommandStreaming(string remoteCommand, Action<string> onLine, TimeSpan timeout)
        {
            string user = GetConfiguredMetashapeSshUser();
            if (string.IsNullOrWhiteSpace(user))
            {
                throw new InvalidOperationException("Configurer l'utilisateur SSH Mac dans Metashape > Settings.");
            }

            List<string> errors = new();
            foreach (string host in GetMetashapeSshHostCandidates())
            {
                try
                {
                    RunMacSshCommandForHostStreaming(user, host, GetConfiguredMetashapeSshKeyPath(), remoteCommand, onLine, timeout);
                    if (!string.Equals(appSettings.MetashapeSshHost, host, StringComparison.OrdinalIgnoreCase))
                    {
                        appSettings.MetashapeSshHost = host;
                        appSettings.Save();
                    }

                    return;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is TimeoutException || ex is System.ComponentModel.Win32Exception)
                {
                    if (IsRemoteCommandExecutionFailure(ex))
                    {
                        throw;
                    }

                    errors.Add(host + ": " + ex.Message);
                }
            }

            throw new InvalidOperationException("SSH Metashape a échoué pour tous les hôtes essayés." + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        private static bool IsRemoteCommandExecutionFailure(Exception ex)
        {
            return ex is InvalidOperationException &&
                   ex.Message.StartsWith("SSH a retourné le code ", StringComparison.OrdinalIgnoreCase);
        }

        private static void RunMacSshCommandForHostStreaming(
            string user,
            string host,
            string keyPath,
            string remoteCommand,
            Action<string> onLine,
            TimeSpan timeout)
        {
            ProcessStartInfo startInfo = CreateMacSshStartInfo(user, host, keyPath, remoteCommand);

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Impossible de démarrer ssh.exe.");

            using CancellationTokenSource timeoutCts = new(timeout);
            Task<string> stdoutTask = ReadStreamLinesAsync(process.StandardOutput, onLine, timeoutCts.Token);
            Task<string> stderrTask = ReadStreamLinesAsync(process.StandardError, onLine, timeoutCts.Token);

            try
            {
                while (!process.WaitForExit(500))
                {
                    if (!timeoutCts.IsCancellationRequested) continue;

                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Best effort cleanup.
                    }

                    throw new TimeoutException("Metashape n'a pas terminé avant le délai de suivi.");
                }
            }
            finally
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
            }

            string output = stdoutTask.GetAwaiter().GetResult();
            string error = stderrTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                string details = string.Join(
                    Environment.NewLine,
                    new[] { output, error }.Where(value => !string.IsNullOrWhiteSpace(value)));
                throw new InvalidOperationException("SSH a retourné le code " + process.ExitCode + ".\n" + details);
            }
        }

        private static async Task<string> ReadStreamLinesAsync(StreamReader reader, Action<string> onLine, CancellationToken cancellationToken)
        {
            Queue<string> recentLines = new();
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) break;

                recentLines.Enqueue(line);
                while (recentLines.Count > MetashapeStreamingErrorLineLimit)
                {
                    recentLines.Dequeue();
                }

                onLine(line);
            }

            return string.Join(Environment.NewLine, recentLines).Trim();
        }

        private static string RunMacSshCommandForHost(string user, string host, string keyPath, string remoteCommand, int waitMilliseconds)
        {
            ProcessStartInfo startInfo = CreateMacSshStartInfo(user, host, keyPath, remoteCommand);

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Impossible de démarrer ssh.exe.");

            if (!process.WaitForExit(waitMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort cleanup for a stuck SSH process.
                }

                throw new TimeoutException("SSH n'a pas répondu avant le délai. Vérifie que Connexion à distance est active sur le Mac et que l'authentification par clé fonctionne.");
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            if (process.ExitCode != 0)
            {
                string details = string.Join(
                    Environment.NewLine,
                    new[] { output, error }.Where(value => !string.IsNullOrWhiteSpace(value)));
                throw new InvalidOperationException("SSH a retourné le code " + process.ExitCode + ".\n" + details);
            }

            return string.Join(
                Environment.NewLine,
                new[] { output, error }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static ProcessStartInfo CreateMacSshStartInfo(string user, string host, string keyPath, string remoteCommand)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "ssh",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add("BatchMode=yes");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add("StrictHostKeyChecking=accept-new");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add("ConnectTimeout=8");
            if (!string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
            {
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(keyPath);
            }
            startInfo.ArgumentList.Add(user + "@" + host);
            startInfo.ArgumentList.Add("export LANG=fr_CA.UTF-8 LC_ALL=fr_CA.UTF-8; " + remoteCommand);
            return startInfo;
        }

        private IEnumerable<string> GetMetashapeSshHostCandidates()
        {
            string configured = GetConfiguredMetashapeSshHost();
            string[] candidates =
            {
                configured,
                "10.211.55.2",
                "macstuderolithe.local",
                "MacStuderolithe.local",
                "macstuderolithe.montrealnet.vdm.qc.ca"
            };

            return candidates
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private MacMetashapeHelperInstallResult EnsureMacMetashapeHelperInstalled()
        {
            try
            {
                string? windowsMacHome = GetParallelsMacHomeWindowsPath();
                if (windowsMacHome == null)
                {
                    return new MacMetashapeHelperInstallResult(
                        Success: false,
                        InstalledOrUpdated: false,
                        Message: "Helper Metashape non installé: C:\\Mac\\Home ou \\\\Mac\\Home est introuvable depuis Parallels.",
                        QueueFolder: string.Empty,
                        LogFolder: string.Empty);
                }

                string macHome = GetConfiguredMacHomePath();
                string launchAgents = Path.Combine(windowsMacHome, "Library", "LaunchAgents");
                string documentsAerolithe = Path.Combine(windowsMacHome, "Documents", "Aerolithe");
                string queueFolder = Path.Combine(documentsAerolithe, "metashape-queue");
                string logFolder = Path.Combine(documentsAerolithe, "metashape-helper-logs");
                string helperPath = Path.Combine(documentsAerolithe, "aerolithe-metashape-helper.sh");
                string plistPath = Path.Combine(launchAgents, "com.aerolithe.metashape-helper.plist");

                Directory.CreateDirectory(documentsAerolithe);
                Directory.CreateDirectory(queueFolder);
                Directory.CreateDirectory(Path.Combine(queueFolder, "done"));
                Directory.CreateDirectory(Path.Combine(queueFolder, "failed"));
                Directory.CreateDirectory(logFolder);
                bool helperChanged = WriteTextIfChanged(helperPath, BuildMacMetashapeHelperScript(), Encoding.UTF8);

                try
                {
                    Directory.CreateDirectory(launchAgents);
                    bool plistChanged = WriteTextIfChanged(
                        plistPath,
                        BuildMacMetashapeHelperPlist(macHome),
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                    string message = helperChanged || plistChanged
                        ? "Helper Metashape installé/mis à jour. S'il n'était pas déjà actif, il sera chargé au prochain login macOS."
                        : "Helper Metashape déjà installé. Commande unique active: " + queueFolder;

                    return new MacMetashapeHelperInstallResult(
                        Success: true,
                        InstalledOrUpdated: helperChanged || plistChanged,
                        Message: message,
                        QueueFolder: queueFolder,
                        LogFolder: logFolder);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return new MacMetashapeHelperInstallResult(
                        Success: false,
                        InstalledOrUpdated: helperChanged,
                        Message: "Helper script installé dans Documents/Aerolithe, mais Aerolithe n'a pas accès à Library/LaunchAgents pour activer l'autoload macOS. Donne l'accès complet au disque à Parallels, puis réessaie. Détail: " + ex.Message,
                        QueueFolder: queueFolder,
                        LogFolder: logFolder);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                return new MacMetashapeHelperInstallResult(
                    Success: false,
                    InstalledOrUpdated: false,
                    Message: "Helper Metashape non installé: " + ex.Message,
                    QueueFolder: string.Empty,
                    LogFolder: string.Empty);
            }
        }

        private string GetMacMetashapeHelperStatus()
        {
            string? windowsMacHome = GetParallelsMacHomeWindowsPath();
            if (windowsMacHome == null)
            {
                return "C:\\Mac\\Home ou \\\\Mac\\Home est introuvable depuis Parallels.";
            }

            string helperPath = Path.Combine(windowsMacHome, "Documents", "Aerolithe", "aerolithe-metashape-helper.sh");
            string plistPath = Path.Combine(windowsMacHome, "Library", "LaunchAgents", "com.aerolithe.metashape-helper.plist");
            string queueFolder = Path.Combine(windowsMacHome, "Documents", "Aerolithe", "metashape-queue");
            string logFolder = Path.Combine(windowsMacHome, "Documents", "Aerolithe", "metashape-helper-logs");
            string helperLog = Path.Combine(logFolder, "helper.log");

            StringBuilder status = new();
            status.AppendLine("Helper script: " + (File.Exists(helperPath) ? "OK" : "manquant"));
            status.AppendLine("LaunchAgent: " + (File.Exists(plistPath) ? "OK" : "manquant"));
            status.AppendLine("Polling 5 s: " + (HasStartInterval(plistPath) ? "OK" : "manquant"));
            status.AppendLine("Dossier commande: " + queueFolder);
            status.AppendLine("Commande en attente: " + (File.Exists(Path.Combine(queueFolder, "metashape-current.json")) ? "oui" : "non"));
            status.AppendLine("Commande en cours: " + (File.Exists(Path.Combine(queueFolder, "metashape-current.json.running")) ? "oui" : "non"));
            status.AppendLine("Requêtes en erreur: " + CountFiles(Path.Combine(queueFolder, "failed"), "*.json"));
            status.AppendLine("Logs: " + logFolder);

            if (File.Exists(helperLog))
            {
                status.AppendLine();
                status.AppendLine("Dernières lignes:");
                foreach (string line in File.ReadLines(helperLog).TakeLast(8))
                {
                    status.AppendLine(line);
                }
            }
            else
            {
                status.AppendLine();
                status.AppendLine("Aucun helper.log pour l'instant. Si le LaunchAgent vient d'être installé pour la première fois, il peut nécessiter un logout/login macOS.");
            }

            return status.ToString();
        }

        private static int CountFiles(string folder, string searchPattern)
        {
            return Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, searchPattern).Count()
                : 0;
        }

        private static bool HasStartInterval(string plistPath)
        {
            try
            {
                return File.Exists(plistPath)
                    && File.ReadAllText(plistPath).Contains("<key>StartInterval</key>", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static bool WriteTextIfChanged(string path, string content, Encoding encoding)
        {
            if (File.Exists(path) && File.ReadAllText(path, encoding) == content)
            {
                return false;
            }

            File.WriteAllText(path, content, encoding);
            return true;
        }

        private static string? GetParallelsMacHomeWindowsPath()
        {
            string[] candidates =
            {
                @"C:\Mac\Home",
                @"\\Mac\Home"
            };

            return candidates.FirstOrDefault(Directory.Exists);
        }

        private string BuildMacMetashapeHelperPlist(string macHome)
        {
            string documentsAerolithe = macHome.TrimEnd('/') + "/Documents/Aerolithe";
            string queueFolder = documentsAerolithe + "/metashape-queue";
            string logFolder = documentsAerolithe + "/metashape-helper-logs";
            string helperPath = documentsAerolithe + "/aerolithe-metashape-helper.sh";

            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
                + "<plist version=\"1.0\">\n"
                + "<dict>\n"
                + "    <key>Label</key>\n"
                + "    <string>com.aerolithe.metashape-helper</string>\n"
                + "    <key>ProgramArguments</key>\n"
                + "    <array>\n"
                + "        <string>/bin/bash</string>\n"
                + "        <string>" + PlistString(helperPath) + "</string>\n"
                + "    </array>\n"
                + "    <key>WatchPaths</key>\n"
                + "    <array>\n"
                + "        <string>" + PlistString(queueFolder) + "</string>\n"
                + "    </array>\n"
                + "    <key>StartInterval</key>\n"
                + "    <integer>5</integer>\n"
                + "    <key>RunAtLoad</key>\n"
                + "    <true/>\n"
                + "    <key>StandardOutPath</key>\n"
                + "    <string>" + PlistString(logFolder + "/launchd.out.log") + "</string>\n"
                + "    <key>StandardErrorPath</key>\n"
                + "    <string>" + PlistString(logFolder + "/launchd.err.log") + "</string>\n"
                + "</dict>\n"
                + "</plist>\n";
        }

        private static string BuildMacMetashapeHelperScript()
        {
            return "#!/bin/bash\n"
                + "set -u\n"
                + "\n"
                + "QUEUE_DIR=\"$HOME/Documents/Aerolithe/metashape-queue\"\n"
                + "LOG_DIR=\"$HOME/Documents/Aerolithe/metashape-helper-logs\"\n"
                + "DONE_DIR=\"$QUEUE_DIR/done\"\n"
                + "FAILED_DIR=\"$QUEUE_DIR/failed\"\n"
                + "LOCK_DIR=\"$QUEUE_DIR/.helper.lock\"\n"
                + "\n"
                + "mkdir -p \"$QUEUE_DIR\" \"$LOG_DIR\" \"$DONE_DIR\" \"$FAILED_DIR\"\n"
                + "\n"
                + "if ! mkdir \"$LOCK_DIR\" 2>/dev/null; then\n"
                + "  exit 0\n"
                + "fi\n"
                + "trap 'rmdir \"$LOCK_DIR\" 2>/dev/null || true' EXIT\n"
                + "\n"
                + "log_helper() {\n"
                + "  printf '%s %s\\n' \"$(date '+%Y-%m-%d %H:%M:%S')\" \"$*\" >> \"$LOG_DIR/helper.log\"\n"
                + "}\n"
                + "\n"
                + "read_json_value() {\n"
                + "  /usr/bin/python3 - \"$1\" \"$2\" <<'PY'\n"
                + "import json\n"
                + "import sys\n"
                + "\n"
                + "path = sys.argv[1]\n"
                + "key = sys.argv[2]\n"
                + "with open(path, \"r\", encoding=\"utf-8\") as f:\n"
                + "    data = json.load(f)\n"
                + "print(data.get(key, \"\"))\n"
                + "PY\n"
                + "}\n"
                + "\n"
                + "shopt -s nullglob\n"
                + "for stale_request in \"$QUEUE_DIR\"/metashape-*.json; do\n"
                + "  if [ \"$(basename \"$stale_request\")\" != \"metashape-current.json\" ]; then\n"
                + "    log_helper \"Removing stale pending request $(basename \"$stale_request\")\"\n"
                + "    rm -f \"$stale_request\"\n"
                + "  fi\n"
                + "done\n"
                + "\n"
                + "request_path=\"$QUEUE_DIR/metashape-current.json\"\n"
                + "if [ -f \"$request_path\" ]; then\n"
                + "  request_name=\"$(basename \"$request_path\")\"\n"
                + "  running_path=\"$QUEUE_DIR/metashape-current.json.running\"\n"
                + "\n"
                + "  if ! mv \"$request_path\" \"$running_path\" 2>/dev/null; then\n"
                + "    exit 0\n"
                + "  fi\n"
                + "\n"
                + "  request_id=\"$(read_json_value \"$running_path\" \"request_id\" 2>> \"$LOG_DIR/helper.log\" || true)\"\n"
                + "  if [ -z \"$request_id\" ]; then\n"
                + "    request_id=\"${request_name%.json}\"\n"
                + "  fi\n"
                + "  command_path=\"$(read_json_value \"$running_path\" \"command_path\" 2>> \"$LOG_DIR/helper.log\" || true)\"\n"
                + "  project_name=\"$(read_json_value \"$running_path\" \"project_name\" 2>> \"$LOG_DIR/helper.log\" || true)\"\n"
                + "  log_path=\"$LOG_DIR/${request_id}.log\"\n"
                + "\n"
                + "  log_helper \"Request $request_id received for $project_name\"\n"
                + "\n"
                + "  if [ -z \"$command_path\" ] || [ ! -f \"$command_path\" ]; then\n"
                + "    log_helper \"Request $request_id failed: command_path missing or not found: $command_path\"\n"
                + "    mv \"$running_path\" \"$FAILED_DIR/$request_name\"\n"
                + "    continue\n"
                + "  fi\n"
                + "\n"
                + "  {\n"
                + "    echo \"Aerolithe Metashape helper\"\n"
                + "    echo \"Request: $request_id\"\n"
                + "    echo \"Project: $project_name\"\n"
                + "    echo \"Command: $command_path\"\n"
                + "    echo \"Started: $(date '+%Y-%m-%d %H:%M:%S')\"\n"
                + "    echo\n"
                + "    /bin/bash \"$command_path\"\n"
                + "    status=$?\n"
                + "    echo\n"
                + "    echo \"Finished: $(date '+%Y-%m-%d %H:%M:%S')\"\n"
                + "    echo \"Exit code: $status\"\n"
                + "    exit \"$status\"\n"
                + "  } > \"$log_path\" 2>&1\n"
                + "\n"
                + "  status=$?\n"
                + "  if [ \"$status\" -eq 0 ]; then\n"
                + "    log_helper \"Request $request_id completed\"\n"
                + "    mv \"$running_path\" \"$DONE_DIR/metashape-$request_id.json\"\n"
                + "  else\n"
                + "    log_helper \"Request $request_id failed with exit code $status\"\n"
                + "    mv \"$running_path\" \"$FAILED_DIR/metashape-$request_id.json\"\n"
                + "  fi\n"
                + "fi\n";
        }

        private static string PlistString(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private MetashapeAutomationPlan CreateMetashapeAutomationPlan(MetashapeMeteoriteType meteoriteType)
        {
            string projectName = !string.IsNullOrWhiteSpace(projet.ImageNameBase)
                ? SanitizeMetashapeProjectName(projet.ImageNameBase)
                : SanitizeMetashapeProjectName(Path.GetFileNameWithoutExtension(appSettings.ProjectPath));

            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string metashapeRoot = Path.Combine(documentsFolder, "Metashape");
            string projectFolder = Path.Combine(metashapeRoot, projectName);
            string projectFile = Path.Combine(projectFolder, projectName + ".psx");
            string modelsFolder = Path.Combine(projectFolder, "Modèles");
            string focusStackRoot = projet.GetFocusStackRootPath();

            return new MetashapeAutomationPlan(
                projectName,
                projectFolder,
                projectFile,
                modelsFolder,
                Path.Combine(projet.ImageFolderPath, "mesures", "serie_A"),
                Path.Combine(focusStackRoot, "focusStack_A"),
                Path.Combine(focusStackRoot, "focusStack_B"),
                Path.Combine(projectFolder, projectName + "_metashape.py"),
                meteoriteType,
                SaveProject: true,
                ImportMeasures: true,
                DetectMarkers: true,
                ImportFocusStacks: true,
                AlignPhotos: true,
                BuildModels: true);
        }

        private MetashapeAutomationPlan CreateDefaultMetashapeAutomationPlan()
        {
            string projectName = "Projet_Aerolithe";
            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string metashapeRoot = Path.Combine(documentsFolder, "Metashape");
            string projectFolder = Path.Combine(metashapeRoot, projectName);
            string projectFile = Path.Combine(projectFolder, projectName + ".psx");
            string modelsFolder = Path.Combine(projectFolder, "Modèles");
            string imagesFolder = Path.Combine(documentsFolder, "Projets", projectName, "images");
            string focusStackRoot = Path.Combine(imagesFolder, "focusStack");

            return new MetashapeAutomationPlan(
                projectName,
                projectFolder,
                projectFile,
                modelsFolder,
                Path.Combine(imagesFolder, "mesures", "serie_A"),
                Path.Combine(focusStackRoot, "focusStack_A"),
                Path.Combine(focusStackRoot, "focusStack_B"),
                Path.Combine(projectFolder, projectName + "_metashape.py"),
                MetashapeMeteoriteType.Normale,
                SaveProject: true,
                ImportMeasures: true,
                DetectMarkers: true,
                ImportFocusStacks: true,
                AlignPhotos: true,
                BuildModels: true);
        }

        private void ValidateMetashapeAutomationPlan(MetashapeAutomationPlan plan)
        {
            List<string> errors = new();
            if (plan.ImportMeasures)
            {
                ValidateImageFolder(plan.MeasuresSerieA, "images/mesures/serie_A", errors);
            }

            if (plan.ImportFocusStacks)
            {
                ValidateImageFolder(plan.FocusStackA, "images/focusStack/focusStack_A", errors);
                ValidateImageFolder(plan.FocusStackB, "images/focusStack/focusStack_B", errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }
        }

        private bool TryConfigureMetashapeLaunch(MetashapeAutomationPlan defaultPlan, out MetashapeAutomationPlan plan, bool settingsOnly)
        {
            plan = defaultPlan;

            using Form dialog = new()
            {
                Text = settingsOnly ? "Settings Metashape" : "Lancer Metashape",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(900, settingsOnly ? 360 : 420),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            Label title = new()
            {
                Text = settingsOnly ? "Choisir le chemin de MetashapePro" : "Préparer le lancement Metashape",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0),
                ForeColor = Color.White
            };

            TableLayoutPanel body = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = settingsOnly ? 2 : 2,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            if (settingsOnly)
            {
                body.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
                body.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            }
            else
            {
                body.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
                body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            }

            TextBox metashapePathTextBox = new()
            {
                Text = appSettings.MetashapePath ?? string.Empty,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Button browseMetashape = new()
            {
                Text = "Parcourir...",
                Dock = DockStyle.Fill
            };
            browseMetashape.Click += (_, __) =>
            {
                using FolderBrowserDialog folderDialog = new()
                {
                    Description = "Choisir MetashapePro.app, Metashape.app ou le dossier qui contient l'application.",
                    SelectedPath = GetExistingMetashapeBrowseStartPath(metashapePathTextBox.Text)
                };

                if (folderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    string selectedPath = folderDialog.SelectedPath;
                    string? appPath = TryFindMetashapeAppInFolder(selectedPath);
                    metashapePathTextBox.Text = appPath ?? selectedPath;
                }
            };

            TableLayoutPanel metashapePanel = CreateTwoColumnPathPanel();
            Label metashapeLabel = CreateMetashapeLabel("Chemin de MetashapePro.app ou metashape.exe");
            metashapePanel.Controls.Add(metashapeLabel, 0, 0);
            metashapePanel.SetColumnSpan(metashapeLabel, 2);
            metashapePanel.Controls.Add(metashapePathTextBox, 0, 1);
            metashapePanel.Controls.Add(browseMetashape, 1, 1);

            TextBox sshHostTextBox = new()
            {
                Text = GetConfiguredMetashapeSshHost(),
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            TextBox sshUserTextBox = new()
            {
                Text = GetConfiguredMetashapeSshUser(),
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            TextBox sshKeyPathTextBox = new()
            {
                Text = GetConfiguredMetashapeSshKeyPath(),
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            TableLayoutPanel sshPanel = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
                Padding = new Padding(0, 4, 0, 4),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            sshPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            sshPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            sshPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            sshPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            sshPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            sshPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            sshPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            sshPanel.Controls.Add(CreateMetashapeLabel("Hôte SSH Mac"), 0, 0);
            sshPanel.Controls.Add(CreateMetashapeLabel("Utilisateur SSH Mac"), 1, 0);
            sshPanel.Controls.Add(sshHostTextBox, 0, 1);
            sshPanel.Controls.Add(sshUserTextBox, 1, 1);
            Label sshKeyLabel = CreateMetashapeLabel("Clé SSH Windows");
            sshPanel.Controls.Add(sshKeyLabel, 0, 2);
            sshPanel.SetColumnSpan(sshKeyLabel, 2);
            sshPanel.Controls.Add(sshKeyPathTextBox, 0, 3);
            sshPanel.SetColumnSpan(sshKeyPathTextBox, 2);

            Button testSsh = new()
            {
                Text = "Tester SSH",
                Dock = DockStyle.Fill
            };
            testSsh.Click += (_, __) =>
            {
                try
                {
                    appSettings.MetashapeSshHost = sshHostTextBox.Text.Trim();
                    appSettings.MetashapeSshUser = sshUserTextBox.Text.Trim();
                    appSettings.MetashapeSshKeyPath = sshKeyPathTextBox.Text.Trim();
                    appSettings.Save();
                    string result = TestMacMetashapeSsh();
                    AppendTextToConsoleNL(result, Color.LightGreen);
                    MessageBox.Show(result, "SSH Metashape", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    string message = "Test SSH échoué: " + ex.Message;
                    AppendTextToConsoleNL(message, Color.Orange);
                    MessageBox.Show(message, "SSH Metashape", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            sshPanel.Controls.Add(testSsh, 2, 1);
            sshPanel.SetRowSpan(testSsh, 3);

            CheckBox chooseProjectPath = CreateMetashapeCheckBox("Choisir le chemin et le nom du projet", false);
            TextBox projectPathTextBox = new()
            {
                Text = defaultPlan.ProjectFile,
                Dock = DockStyle.Fill,
                Enabled = false,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Button browseProject = new()
            {
                Text = "Parcourir...",
                Dock = DockStyle.Fill,
                Enabled = false
            };

            chooseProjectPath.CheckedChanged += (_, __) =>
            {
                projectPathTextBox.Enabled = chooseProjectPath.Checked;
                browseProject.Enabled = chooseProjectPath.Checked;
            };

            browseProject.Click += (_, __) =>
            {
                using SaveFileDialog saveFileDialog = new()
                {
                    Filter = "Metashape project (*.psx)|*.psx|All files (*.*)|*.*",
                    Title = "Choisir le projet Metashape",
                    FileName = Path.GetFileName(projectPathTextBox.Text),
                    InitialDirectory = Directory.Exists(Path.GetDirectoryName(projectPathTextBox.Text))
                        ? Path.GetDirectoryName(projectPathTextBox.Text)
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    projectPathTextBox.Text = saveFileDialog.FileName;
                }
            };

            TableLayoutPanel projectPanel = CreateTwoColumnPathPanel();
            projectPanel.Controls.Add(chooseProjectPath, 0, 0);
            projectPanel.SetColumnSpan(chooseProjectPath, 2);
            projectPanel.Controls.Add(projectPathTextBox, 0, 1);
            projectPanel.Controls.Add(browseProject, 1, 1);

            CheckBox saveProject = CreateMetashapeCheckBox("Créer/enregistrer le projet", defaultPlan.SaveProject);
            CheckBox importMeasures = CreateMetashapeCheckBox("Importer les images de mesure série A", defaultPlan.ImportMeasures);
            CheckBox detectMarkers = CreateMetashapeCheckBox("Détecter les marqueurs circular 12 bit", defaultPlan.DetectMarkers);
            CheckBox importFocusStacks = CreateMetashapeCheckBox("Importer les focus stack A et B", defaultPlan.ImportFocusStacks);
            CheckBox alignPhotos = CreateMetashapeCheckBox("Aligner les photos", defaultPlan.AlignPhotos);
            CheckBox buildModels = CreateMetashapeCheckBox("Construire/texturer/exporter HR et LR", defaultPlan.BuildModels);

            FlowLayoutPanel stepsPanel = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            stepsPanel.Controls.Add(saveProject);
            stepsPanel.Controls.Add(importMeasures);
            stepsPanel.Controls.Add(detectMarkers);
            stepsPanel.Controls.Add(importFocusStacks);
            stepsPanel.Controls.Add(alignPhotos);
            stepsPanel.Controls.Add(buildModels);

            TextBox paths = new()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.Gainsboro,
                Text = BuildMetashapePlanSummary(defaultPlan)
            };

            TableLayoutPanel buttons = new()
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                ColumnCount = 3,
                Padding = new Padding(8)
            };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));

            Button cancel = new() { Text = "Annuler", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill };
            Button ok = new() { Text = settingsOnly ? "OK" : "Lancer", DialogResult = DialogResult.OK, Dock = DockStyle.Fill };
            buttons.Controls.Add(cancel, 1, 0);
            buttons.Controls.Add(ok, 2, 0);

            if (settingsOnly)
            {
                body.Controls.Add(metashapePanel, 0, 0);
                body.Controls.Add(sshPanel, 0, 1);
            }
            else
            {
                body.Controls.Add(projectPanel, 0, 0);
                body.Controls.Add(paths, 0, 1);
            }
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.Controls.Add(title, 0, 0);
            root.Controls.Add(body, 0, 1);
            root.Controls.Add(buttons, 0, 2);

            dialog.Controls.Add(root);
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            appSettings.MetashapePath = metashapePathTextBox.Text.Trim();
            appSettings.MetashapeSshHost = sshHostTextBox.Text.Trim();
            appSettings.MetashapeSshUser = sshUserTextBox.Text.Trim();
            appSettings.MetashapeSshKeyPath = sshKeyPathTextBox.Text.Trim();
            appSettings.Save();

            if (settingsOnly)
            {
                AppendTextToConsoleNL("Chemin Metashape sauvegardé: " + appSettings.MetashapePath);
                return true;
            }

            string projectFile = chooseProjectPath.Checked ? projectPathTextBox.Text.Trim() : defaultPlan.ProjectFile;
            if (string.IsNullOrWhiteSpace(projectFile))
            {
                MessageBox.Show("Le chemin du projet Metashape est vide.", "Metashape", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!projectFile.EndsWith(".psx", StringComparison.OrdinalIgnoreCase))
            {
                projectFile += ".psx";
            }

            string? projectFolder = Path.GetDirectoryName(projectFile);
            if (string.IsNullOrWhiteSpace(projectFolder))
            {
                MessageBox.Show("Le dossier du projet Metashape est invalide.", "Metashape", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            string projectName = SanitizeMetashapeProjectName(Path.GetFileNameWithoutExtension(projectFile));
            plan = defaultPlan with
            {
                ProjectName = projectName,
                ProjectFolder = projectFolder,
                ProjectFile = projectFile,
                ModelsFolder = Path.Combine(projectFolder, "Modèles"),
                ScriptPath = Path.Combine(projectFolder, projectName + "_metashape.py"),
                SaveProject = defaultPlan.SaveProject,
                ImportMeasures = defaultPlan.ImportMeasures,
                DetectMarkers = defaultPlan.DetectMarkers,
                ImportFocusStacks = defaultPlan.ImportFocusStacks,
                AlignPhotos = defaultPlan.AlignPhotos,
                BuildModels = defaultPlan.BuildModels
            };

            return true;
        }

        private static TableLayoutPanel CreateTwoColumnPathPanel()
        {
            TableLayoutPanel panel = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            return panel;
        }

        private static Label CreateMetashapeLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White
            };
        }

        private static string GetExistingMetashapeBrowseStartPath(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                if (Directory.Exists(configuredPath))
                {
                    return configuredPath;
                }

                string? parent = Path.GetDirectoryName(configuredPath);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    return parent;
                }
            }

            string[] candidates =
            {
                @"C:\Mac\Applications",
                @"\\Mac\Applications",
                "/Applications",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            return candidates.FirstOrDefault(Directory.Exists)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private static string? TryFindMetashapeAppInFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            if (IsMacAppBundlePath(folder) || IsMetashapeExecutablePath(folder))
            {
                return folder;
            }

            string[] names =
            {
                "MetashapePro.app",
                "MetaShapePro.app",
                "Metashape.app",
                "metashape.exe"
            };

            return names
                .Select(name => Path.Combine(folder, name))
                .FirstOrDefault(path => Directory.Exists(path) || File.Exists(path));
        }

        private static CheckBox CreateMetashapeCheckBox(string text, bool isChecked)
        {
            return new CheckBox
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                ForeColor = Color.White,
                Margin = new Padding(0, 5, 0, 5)
            };
        }

        private static void ValidateImageFolder(string folder, string label, List<string> errors)
        {
            if (!Directory.Exists(folder))
            {
                errors.Add(label + " introuvable: " + folder);
                return;
            }

            if (!Directory.EnumerateFiles(folder).Any(IsMetashapeImageFile))
            {
                errors.Add(label + " ne contient pas d'images: " + folder);
            }
        }

        private static bool IsMetashapeImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeMetashapeProjectName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "Projet_Aerolithe" : value.Trim().Replace(' ', '_');
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }

        private bool TrySelectMetashapeMeteoriteType(out MetashapeMeteoriteType meteoriteType)
        {
            meteoriteType = MetashapeMeteoriteType.Normale;

            using Form dialog = new()
            {
                Text = "Type de météorite",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 180),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };

            Label title = new()
            {
                Text = "Choisir le type pour l'alignement Metashape",
                Dock = DockStyle.Top,
                Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0),
                ForeColor = Color.White
            };

            FlowLayoutPanel options = new()
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Height = 76,
                Padding = new Padding(18, 4, 0, 0),
                BackColor = Color.FromArgb(30, 30, 30)
            };

            RadioButton rbNormale = CreateMetashapeTypeRadio("Normale", true);
            RadioButton rbGuided = CreateMetashapeTypeRadio("Lisse / métallique / peu de détails", false);

            options.Controls.Add(rbNormale);
            options.Controls.Add(rbGuided);

            TableLayoutPanel buttons = new()
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                ColumnCount = 3,
                Padding = new Padding(8)
            };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));

            Button cancel = new() { Text = "Annuler", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill };
            Button ok = new() { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Fill };
            buttons.Controls.Add(cancel, 1, 0);
            buttons.Controls.Add(ok, 2, 0);

            dialog.Controls.Add(buttons);
            dialog.Controls.Add(options);
            dialog.Controls.Add(title);
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            if (rbGuided.Checked) meteoriteType = MetashapeMeteoriteType.Lisse;
            else meteoriteType = MetashapeMeteoriteType.Normale;

            return true;
        }

        private static RadioButton CreateMetashapeTypeRadio(string text, bool isChecked)
        {
            return new RadioButton
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                ForeColor = Color.White,
                Margin = new Padding(0, 3, 0, 3)
            };
        }

        private static string BuildMetashapePlanSummary(MetashapeAutomationPlan plan)
        {
            return "Projet: " + plan.ProjectFile + Environment.NewLine
                + "Mesures: " + plan.MeasuresSerieA + Environment.NewLine
                + "Focus stack A: " + plan.FocusStackA + Environment.NewLine
                + "Focus stack B: " + plan.FocusStackB + Environment.NewLine
                + "Guided image matching: " + (plan.GuidedMatching ? "Checked" : "Unchecked") + Environment.NewLine
                + "Exports: " + plan.HighModelPath + " | " + plan.LowModelPath;
        }

        private MetashapeLaunchTarget? FindMetashapeLaunchTarget()
        {
            MetashapeLaunchTarget? configuredTarget = CreateMetashapeLaunchTarget(appSettings.MetashapePath);
            if (configuredTarget != null)
            {
                return configuredTarget;
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string[] executableCandidates =
            {
                Path.Combine(programFiles, "Agisoft", "Metashape Pro", "metashape.exe"),
                Path.Combine(programFiles, "Agisoft", "Metashape Professional", "metashape.exe"),
                Path.Combine(programFiles, "Agisoft", "Metashape", "metashape.exe"),
                Path.Combine(programFilesX86, "Agisoft", "Metashape Pro", "metashape.exe"),
                Path.Combine(programFilesX86, "Agisoft", "Metashape Professional", "metashape.exe"),
                Path.Combine(localAppData, "Agisoft", "Metashape Pro", "metashape.exe"),
                Path.Combine(localAppData, "Agisoft", "Metashape Professional", "metashape.exe")
            };

            string? executable = executableCandidates.FirstOrDefault(File.Exists);
            if (executable != null)
            {
                return new MetashapeLaunchTarget(executable, IsMacAppBundle: false);
            }

            string[] macAppCandidates =
            {
                @"C:\Mac\Applications\MetashapePro.app",
                @"C:\Mac\Applications\MetaShapePro.app",
                @"C:\Mac\Applications\Metashape.app",
                @"\\Mac\Applications\MetashapePro.app",
                @"\\Mac\Applications\MetaShapePro.app",
                @"\\Mac\Applications\Metashape.app",
                "/Applications/MetashapePro.app",
                "/Applications/MetaShapePro.app",
                "/Applications/Metashape.app"
            };

            string? macApp = macAppCandidates.FirstOrDefault(Directory.Exists);
            return macApp == null
                ? null
                : new MetashapeLaunchTarget(macApp, IsMacAppBundle: true);
        }

        private static MetashapeLaunchTarget? CreateMetashapeLaunchTarget(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string trimmedPath = path.Trim();
            if (IsMacAppBundlePath(trimmedPath))
            {
                return new MetashapeLaunchTarget(trimmedPath, IsMacAppBundle: true);
            }

            if (IsMetashapeExecutablePath(trimmedPath) && File.Exists(trimmedPath))
            {
                return new MetashapeLaunchTarget(trimmedPath, IsMacAppBundle: false);
            }

            string? appInFolder = TryFindMetashapeAppInFolder(trimmedPath);
            if (appInFolder != null)
            {
                return CreateMetashapeLaunchTarget(appInFolder);
            }

            return null;
        }

        private static bool IsMacAppBundlePath(string path)
        {
            return path.EndsWith(".app", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMetashapeExecutablePath(string path)
        {
            string fileName = Path.GetFileName(path);
            return fileName.Equals("metashape.exe", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("MetashapePro.exe", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("Metashape.exe", StringComparison.OrdinalIgnoreCase);
        }

        private string WriteMacMetashapeCommand(MetashapeAutomationPlan plan, string appBundlePath)
        {
            string commandPath = Path.Combine(plan.ProjectFolder, plan.ProjectName + "_run_metashape.command");
            string appPath = GetMacAppBundlePathForMac(appBundlePath);
            string executablePath = GetMacAppExecutablePathForMac(appBundlePath);
            string scriptPath = ToMacPath(plan.ScriptPath);

            StringBuilder command = new();
            command.Append("#!/bin/bash\n");
            command.Append("set -e\n");
            command.Append("cd " + BashString(Path.GetDirectoryName(scriptPath) ?? "/") + "\n");
            command.Append("echo 'Vérification de Metashape...'\n");
            command.Append("if /usr/bin/pgrep -fl 'Metashape|MetaShape' >/dev/null 2>&1; then\n");
            command.Append("  echo 'Metashape déjà ouvert'\n");
            command.Append("  /usr/bin/pgrep -fl 'Metashape|MetaShape'\n");
            command.Append("else\n");
            command.Append("  echo 'Ouverture de Metashape...'\n");
            command.Append("/usr/bin/open -n " + BashString(appPath) + "\n");
            command.Append("echo \"open metashape exit=$?\"\n");
            command.Append("sleep 8\n");
            command.Append("/usr/bin/pgrep -fl 'Metashape|MetaShape' || echo 'Aucun Metashape trouvé'\n");
            command.Append("fi\n");
            command.Append("echo 'Lancement du script Metashape...'\n");
            command.Append(BashString(executablePath) + " -r " + BashString(scriptPath) + "\n");
            command.Append("echo\n");
            command.Append("echo 'Metashape terminé. Vous pouvez fermer cette fenêtre.'\n");

            File.WriteAllText(commandPath, command.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            TryMakeExecutable(commandPath);
            return commandPath;
        }

        private static void TryMakeExecutable(string path)
        {
            try
            {
                File.SetUnixFileMode(
                    path,
                    UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute);
            }
            catch
            {
                // Sur Windows/Parallels, le fichier peut quand même être lançable via association .command.
            }
        }

        private static string GetMacAppExecutablePathForWindows(string appBundlePath)
        {
            string appPath = appBundlePath.TrimEnd('\\', '/');
            string appName = Path.GetFileNameWithoutExtension(appPath);
            string[] candidates =
            {
                Path.Combine(appPath, "Contents", "MacOS", appName),
                Path.Combine(appPath, "Contents", "MacOS", "MetashapePro"),
                Path.Combine(appPath, "Contents", "MacOS", "Metashape")
            };

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        private string GetMacAppExecutablePathForMac(string appBundlePath)
        {
            string macAppPath = GetMacAppBundlePathForMac(appBundlePath);
            string appName = Path.GetFileNameWithoutExtension(macAppPath);
            string[] candidates =
            {
                macAppPath.TrimEnd('/') + "/Contents/MacOS/" + appName,
                macAppPath.TrimEnd('/') + "/Contents/MacOS/MetashapePro",
                macAppPath.TrimEnd('/') + "/Contents/MacOS/Metashape"
            };

            return appName.Equals("MetashapePro", StringComparison.OrdinalIgnoreCase)
                ? candidates[1]
                : candidates[0];
        }

        private string GetMacAppBundlePathForMac(string appBundlePath)
        {
            string macAppPath = ToMacPath(appBundlePath).TrimEnd('/', '\\');
            if (LooksLikeWindowsDrivePath(macAppPath) && IsMacAppBundlePath(macAppPath))
            {
                return "/Applications/" + Path.GetFileName(macAppPath);
            }

            return macAppPath;
        }

        private static bool LooksLikeWindowsDrivePath(string path)
        {
            return path.Length >= 3
                && char.IsLetter(path[0])
                && path[1] == ':'
                && (path[2] == '/' || path[2] == '\\');
        }

        private string ToMacPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            string normalized = path.Replace('\\', '/');
            string macHomePath = GetConfiguredMacHomePath();

            if (normalized.StartsWith("C:/Mac/Home/", StringComparison.OrdinalIgnoreCase))
            {
                return macHomePath + "/" + normalized["C:/Mac/Home/".Length..];
            }

            if (normalized.Equals("C:/Mac/Home", StringComparison.OrdinalIgnoreCase))
            {
                return macHomePath;
            }

            if (normalized.StartsWith("//Mac/Home/", StringComparison.OrdinalIgnoreCase))
            {
                return macHomePath + "/" + normalized["//Mac/Home/".Length..];
            }

            if (normalized.Equals("//Mac/Home", StringComparison.OrdinalIgnoreCase))
            {
                return macHomePath;
            }

            if (normalized.StartsWith("C:/Mac/Applications", StringComparison.OrdinalIgnoreCase))
            {
                return "/Applications" + normalized["C:/Mac/Applications".Length..];
            }

            if (normalized.StartsWith("//Mac/Applications", StringComparison.OrdinalIgnoreCase))
            {
                return "/Applications" + normalized["//Mac/Applications".Length..];
            }

            if (normalized.StartsWith("C:/Mac/Volumes/", StringComparison.OrdinalIgnoreCase))
            {
                return "/Volumes/" + normalized["C:/Mac/Volumes/".Length..];
            }

            if (normalized.StartsWith("//Mac/Volumes/", StringComparison.OrdinalIgnoreCase))
            {
                return "/Volumes/" + normalized["//Mac/Volumes/".Length..];
            }

            return normalized;
        }

        private string GetConfiguredMacHomePath()
        {
            string configured = appSettings?.MetashapeMacHomePath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.TrimEnd('/', '\\').Replace('\\', '/');
            }

            string userName = Environment.UserName;
            return string.IsNullOrWhiteSpace(userName)
                ? "/Users/tech"
                : "/Users/" + userName;
        }

        private string GetConfiguredMetashapeSshHost()
        {
            string configured = appSettings?.MetashapeSshHost?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(configured)
                ? "10.211.55.2"
                : configured;
        }

        private string GetConfiguredMetashapeSshUser()
        {
            string configured = appSettings?.MetashapeSshUser?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(configured)
                ? "tech"
                : configured;
        }

        private string GetConfiguredMetashapeSshKeyPath()
        {
            string configured = appSettings?.MetashapeSshKeyPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh",
                "aerolithe_metashape");
        }

        private static string BashString(string value)
        {
            return "'" + value.Replace("'", "'\\''") + "'";
        }

        private static string JsonString(string value)
        {
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                + "\"";
        }

        private static string BuildMetashapePythonScript(MetashapeAutomationPlan plan, Func<string, string>? pathMapper = null)
        {
            string MapPath(string value) => pathMapper == null ? value : pathMapper(value);

            StringBuilder script = new();
            script.AppendLine("# Generated by Aerolithe.");
            script.AppendLine("import os");
            script.AppendLine("import shutil");
            script.AppendLine("import Metashape");
            script.AppendLine();
            script.AppendLine("PROJECT_PATH = " + PythonString(MapPath(plan.ProjectFile)));
            script.AppendLine("MEASURES_SERIE_A = " + PythonString(MapPath(plan.MeasuresSerieA)));
            script.AppendLine("FOCUS_STACK_A = " + PythonString(MapPath(plan.FocusStackA)));
            script.AppendLine("FOCUS_STACK_B = " + PythonString(MapPath(plan.FocusStackB)));
            script.AppendLine("HR_MODEL_PATH = " + PythonString(MapPath(plan.HighModelPath)));
            script.AppendLine("LR_MODEL_PATH = " + PythonString(MapPath(plan.LowModelPath)));
            script.AppendLine("GUIDED_MATCHING = " + (plan.GuidedMatching ? "True" : "False"));
            script.AppendLine("SAVE_PROJECT = " + (plan.SaveProject ? "True" : "False"));
            script.AppendLine("IMPORT_MEASURES = " + (plan.ImportMeasures ? "True" : "False"));
            script.AppendLine("DETECT_MARKERS = " + (plan.DetectMarkers ? "True" : "False"));
            script.AppendLine("IMPORT_FOCUS_STACKS = " + (plan.ImportFocusStacks ? "True" : "False"));
            script.AppendLine("ALIGN_PHOTOS = " + (plan.AlignPhotos ? "True" : "False"));
            script.AppendLine("BUILD_MODELS = " + (plan.BuildModels ? "True" : "False"));
            script.AppendLine();
            script.AppendLine("IMAGE_EXTENSIONS = ('.jpg', '.jpeg', '.tif', '.tiff', '.png')");
            script.AppendLine("LOG_PATH = os.path.splitext(PROJECT_PATH)[0] + '_metashape.log'");
            script.AppendLine();
            script.AppendLine("def log(message):");
            script.AppendLine("    line = '[Aerolithe Metashape] ' + str(message)");
            script.AppendLine("    try:");
            script.AppendLine("        os.makedirs(os.path.dirname(LOG_PATH), exist_ok=True)");
            script.AppendLine("        with open(LOG_PATH, 'a', encoding='utf-8') as log_file:");
            script.AppendLine("            log_file.write(line + '\\n')");
            script.AppendLine("    except Exception:");
            script.AppendLine("        pass");
            script.AppendLine();
            script.AppendLine("def image_files(folder):");
            script.AppendLine("    files = []");
            script.AppendLine("    for name in sorted(os.listdir(folder)):");
            script.AppendLine("        path = os.path.join(folder, name)");
            script.AppendLine("        if os.path.isfile(path) and name.lower().endswith(IMAGE_EXTENSIONS):");
            script.AppendLine("            files.append(path)");
            script.AppendLine("    if not files:");
            script.AppendLine("        raise RuntimeError('Aucune image dans ' + folder)");
            script.AppendLine("    return files");
            script.AppendLine();
            script.AppendLine("def save_step(doc, label):");
            script.AppendLine("    if not SAVE_PROJECT:");
            script.AppendLine("        log(label + ' - sauvegarde ignorée')");
            script.AppendLine("        return");
            script.AppendLine("    try:");
            script.AppendLine("        doc.save(PROJECT_PATH)");
            script.AppendLine("    except OSError as exc:");
            script.AppendLine("        message = str(exc)");
            script.AppendLine("        if 'read-only' in message.lower() or 'editing is disabled' in message.lower():");
            script.AppendLine("            raise RuntimeError('Projet Metashape ouvert en lecture seule pendant ' + label + '. Fermer toute fenetre Metashape qui utilise ' + PROJECT_PATH + ', puis relancer.')");
            script.AppendLine("        raise");
            script.AppendLine("    log(label + ' - projet sauvegardé')");
            script.AppendLine();
            script.AppendLine("def disable_cameras_from_folder(chunk, folder):");
            script.AppendLine("    folder = os.path.normcase(os.path.abspath(folder))");
            script.AppendLine("    for camera in chunk.cameras:");
            script.AppendLine("        photo = getattr(camera, 'photo', None)");
            script.AppendLine("        path = getattr(photo, 'path', '') if photo else ''");
            script.AppendLine("        if path and os.path.normcase(os.path.abspath(path)).startswith(folder):");
            script.AppendLine("            camera.enabled = False");
            script.AppendLine();
            script.AppendLine("def force_scale_to_25mm(chunk):");
            script.AppendLine("    if not chunk.scalebars:");
            script.AppendLine("        log('Aucune barre d\\'échelle trouvée; échelle non appliquée.')");
            script.AppendLine("        return");
            script.AppendLine("    sb = chunk.scalebars[0]");
            script.AppendLine("    m0 = sb.point0");
            script.AppendLine("    m1 = sb.point1");
            script.AppendLine("    if not m0 or not m1 or not m0.position or not m1.position:");
            script.AppendLine("        log('Barre d\\'échelle ou marqueurs invalides; échelle non appliquée.')");
            script.AppendLine("        return");
            script.AppendLine("    current_distance = (m0.position - m1.position).norm()");
            script.AppendLine("    if current_distance == 0:");
            script.AppendLine("        log('Distance de barre d\\'échelle à 0; échelle non appliquée.')");
            script.AppendLine("        return");
            script.AppendLine("    factor = 0.025 / current_distance");
            script.AppendLine("    matrix = chunk.transform.matrix");
            script.AppendLine("    if not matrix:");
            script.AppendLine("        log('Transformation absente; échelle non appliquée.')");
            script.AppendLine("        return");
            script.AppendLine("    chunk.transform.matrix = Metashape.Matrix.Diag([factor, factor, factor, 1]) * matrix");
            script.AppendLine("    log('Échelle forcée à 25 mm')");
            script.AppendLine();
            script.AppendLine("def build_and_export_model(doc, chunk, face_count, output_path):");
            script.AppendLine("    label = os.path.splitext(os.path.basename(output_path))[0]");
            script.AppendLine("    keep_uv_mapping = getattr(Metashape, 'KeepUVMapping', getattr(Metashape, 'KeepUvMapping', Metashape.GenericMapping))");
            script.AppendLine("    log('Construction depth maps high pour ' + label)");
            script.AppendLine("    chunk.buildDepthMaps(downscale=2, filter_mode=Metashape.MildFiltering)");
            script.AppendLine("    save_step(doc, 'Depth maps ' + label)");
            script.AppendLine("    log('Construction modèle ' + label)");
            script.AppendLine("    chunk.buildModel(source_data=Metashape.DepthMapsData, surface_type=Metashape.Arbitrary, interpolation=Metashape.EnabledInterpolation, face_count=face_count, replace_asset=True, build_texture=False)");
            script.AppendLine("    save_step(doc, 'Modèle ' + label)");
            script.AppendLine("    log('UV + texture ' + label)");
            script.AppendLine("    chunk.buildUV(mapping_mode=keep_uv_mapping, page_count=2, texture_size=4096)");
            script.AppendLine("    model_class = getattr(Metashape, 'Model', None)");
            script.AppendLine("    diffuse_map = getattr(model_class, 'DiffuseMap', getattr(Metashape, 'DiffuseMap', None))");
            script.AppendLine("    texture_args = dict(source_data=Metashape.ImagesData, blending_mode=Metashape.MosaicBlending, texture_size=4096, fill_holes=True, ghosting_filter=True)");
            script.AppendLine("    if diffuse_map is not None:");
            script.AppendLine("        texture_args['texture_type'] = diffuse_map");
            script.AppendLine("    try:");
            script.AppendLine("        chunk.buildTexture(**texture_args)");
            script.AppendLine("    except TypeError:");
            script.AppendLine("        texture_args.pop('ghosting_filter', None)");
            script.AppendLine("        chunk.buildTexture(**texture_args)");
            script.AppendLine("    save_step(doc, 'Texture ' + label)");
            script.AppendLine("    log('Export GLB ' + output_path)");
            script.AppendLine("    chunk.exportModel(output_path, format=Metashape.ModelFormatGLB, binary=True, save_texture=True, save_uv=True, save_normals=True, save_colors=True)");
            script.AppendLine("    save_step(doc, 'Export ' + label)");
            script.AppendLine();
            script.AppendLine("def main():");
            script.AppendLine("    os.makedirs(os.path.dirname(PROJECT_PATH), exist_ok=True)");
            script.AppendLine("    os.makedirs(os.path.dirname(HR_MODEL_PATH), exist_ok=True)");
            script.AppendLine("    doc = Metashape.app.document");
            script.AppendLine("    current_path = str(getattr(doc, 'path', '') or '')");
            script.AppendLine("    if current_path and os.path.abspath(current_path) != os.path.abspath(PROJECT_PATH):");
            script.AppendLine("        log('Projet Metashape ouvert différent: ' + current_path)");
            script.AppendLine("        try:");
            script.AppendLine("            doc.save()");
            script.AppendLine("            log('Projet ouvert sauvegardé avant changement')");
            script.AppendLine("        except Exception as exc:");
            script.AppendLine("            log('Sauvegarde du projet ouvert impossible: ' + str(exc))");
            script.AppendLine("    if SAVE_PROJECT:");
            script.AppendLine("        project_files = os.path.splitext(PROJECT_PATH)[0] + '.files'");
            script.AppendLine("        if os.path.exists(PROJECT_PATH):");
            script.AppendLine("            os.remove(PROJECT_PATH)");
            script.AppendLine("        if os.path.isdir(project_files):");
            script.AppendLine("            shutil.rmtree(project_files)");
            script.AppendLine("    if len(doc.chunks):");
            script.AppendLine("        while len(doc.chunks):");
            script.AppendLine("            doc.remove(doc.chunks[-1])");
            script.AppendLine("    chunk = doc.addChunk()");
            script.AppendLine("    chunk.label = 'Aerolithe'");
            script.AppendLine("    save_step(doc, 'Projet initial')");
            script.AppendLine("    if IMPORT_MEASURES:");
            script.AppendLine("        measures = image_files(MEASURES_SERIE_A)");
            script.AppendLine("        log('Test simple: import images de calibration série A (' + str(len(measures)) + ' images)')");
            script.AppendLine("        chunk.addPhotos(measures)");
            script.AppendLine("        save_step(doc, 'Test import images de calibration')");
            script.AppendLine("    log('Test simple terminé')");
            script.AppendLine("    return");
            script.AppendLine("    if DETECT_MARKERS:");
            script.AppendLine("        log('Détection marqueurs circular 12 bit')");
            script.AppendLine("        chunk.detectMarkers(target_type=Metashape.CircularTarget12bit, tolerance=50, inverted=False, noparity=True, merge_markers=True)");
            script.AppendLine("        save_step(doc, 'Marqueurs')");
            script.AppendLine("    if IMPORT_FOCUS_STACKS:");
            script.AppendLine("        focus_a = image_files(FOCUS_STACK_A)");
            script.AppendLine("        focus_b = image_files(FOCUS_STACK_B)");
            script.AppendLine("        log('Import focus stack A et B')");
            script.AppendLine("        chunk.addPhotos(focus_a)");
            script.AppendLine("        chunk.addPhotos(focus_b)");
            script.AppendLine("        save_step(doc, 'Import focus stack')");
            script.AppendLine("    if ALIGN_PHOTOS:");
            script.AppendLine("        log('Alignement photos')");
            script.AppendLine("        chunk.matchPhotos(downscale=1, generic_preselection=True, reference_preselection=False, keypoint_limit=60000, guided_matching=GUIDED_MATCHING, filter_stationary_points=True)");
            script.AppendLine("        chunk.alignCameras(adaptive_fitting=True)");
            script.AppendLine("        save_step(doc, 'Alignement')");
            script.AppendLine("    if BUILD_MODELS:");
            script.AppendLine("        disable_cameras_from_folder(chunk, MEASURES_SERIE_A)");
            script.AppendLine("        save_step(doc, 'Images de mesures désactivées')");
            script.AppendLine("        force_scale_to_25mm(chunk)");
            script.AppendLine("        save_step(doc, 'Distance 25 mm')");
            script.AppendLine("        build_and_export_model(doc, chunk, Metashape.HighFaceCount, HR_MODEL_PATH)");
            script.AppendLine("        build_and_export_model(doc, chunk, Metashape.LowFaceCount, LR_MODEL_PATH)");
            script.AppendLine("    log('Terminé')");
            script.AppendLine();
            script.AppendLine("main()");
            return script.ToString();
        }

        private static string PythonString(string value)
        {
            return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        }
    }
}
