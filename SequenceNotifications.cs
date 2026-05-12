namespace Aerolithe
{
    public partial class Aerolithe
    {
        private async Task SendSequenceNotificationAsync(
            string sequenceName,
            DateTime startedAt,
            string status,
            bool focusStackWasEnabled,
            string errorMessage = "")
        {
            try
            {
                if (focusStackWasEnabled)
                {
                    await WaitForFocusStackQueueIdleAsync(TimeSpan.FromMinutes(20));
                }

                var recipients = GetSelectedMessagingRecipients();
                if (recipients.Count == 0)
                {
                    AppendTextToConsoleNL("Notification courriel ignoree: aucun destinataire coche.");
                    return;
                }

                var report = BuildSequenceNotificationReport(sequenceName, startedAt, DateTime.Now, status, focusStackWasEnabled, errorMessage);
                var service = new EmailNotificationService(appSettings);

                if (!service.IsConfigured)
                {
                    AppendTextToConsoleNL("Notification courriel ignoree: SMTP non configure.");
                    return;
                }

                await service.SendAsync(recipients, report.Subject, report.BuildBody());
                AppendTextToConsoleNL($"Notification courriel envoyee a {recipients.Count} destinataire(s).");
            }
            catch (Exception ex)
            {
                AppendTextToConsoleNL($"Erreur notification courriel: {ex.Message}");
            }
        }

        private List<string> GetSelectedMessagingRecipients()
        {
            return appSettings.MessagingUsers
                .Where(user => user.Send && !string.IsNullOrWhiteSpace(user.Email))
                .Select(user => user.Email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private SequenceNotificationReport BuildSequenceNotificationReport(
            string sequenceName,
            DateTime startedAt,
            DateTime finishedAt,
            string status,
            bool focusStackWasEnabled,
            string errorMessage)
        {
            var projectName = !string.IsNullOrWhiteSpace(appSettings.ProjectPath)
                ? Path.GetFileNameWithoutExtension(appSettings.ProjectPath)
                : "Projet sans nom";

            var report = new SequenceNotificationReport
            {
                ProjectName = projectName,
                SequenceName = sequenceName,
                Status = status,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                FocusStackEnabled = focusStackWasEnabled,
                ErrorMessage = errorMessage
            };

            if (focusStackWasEnabled)
            {
                var tasks = focusStackQueue.ToList();
                report.FocusStackSucceeded = tasks.Count(task => task.Status == "Termine" || task.Status == "Terminé");
                report.FocusStackFailed = tasks.Count(task => task.Status == "Erreur");
                report.FocusStackPendingOrRunning = tasks.Count(task => task.Status == "En attente" || task.Status == "En cours");

                foreach (var failedTask in tasks.Where(task => task.Status == "Erreur"))
                {
                    report.FocusStackFailures.Add(new FocusStackNotificationItem
                    {
                        Serie = failedTask.Serie,
                        Elevation = failedTask.Elevation,
                        Rotation = failedTask.Rotation,
                        FileName = Path.GetFileName(failedTask.OutputPath)
                    });
                }
            }

            return report;
        }

        private async Task WaitForFocusStackQueueIdleAsync(TimeSpan timeout)
        {
            DateTime start = DateTime.UtcNow;
            await Task.Delay(500);

            while (DateTime.UtcNow - start < timeout)
            {
                bool hasRunningWork = focusStackQueue.Any(task => task.Status == "En attente" || task.Status == "En cours");
                if (!hasRunningWork && !isProcessingQueue)
                {
                    return;
                }

                await Task.Delay(500);
            }

            AppendTextToConsoleNL("Timeout en attente de la file de focus stack avant notification.");
        }

        private void ResetFocusStackNotificationTracking()
        {
            focusStackQueue.Clear();
            taskControls.Clear();

            void clearReports() => flowPanelReports.Controls.Clear();

            if (flowPanelReports.InvokeRequired)
            {
                flowPanelReports.Invoke(clearReports);
            }
            else
            {
                clearReports();
            }
        }
    }
}
