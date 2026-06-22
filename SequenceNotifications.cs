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
                if (_manualSequenceCancellationRequested)
                {
                    AppendTextToConsoleNL("Notification courriel ignoree: sequence annulee manuellement.");
                    return;
                }

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
                Cote = projet.Cote == 0 ? "A" : "B",
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
                        Cote = failedTask.Cote == 0 ? "A" : "B",
                        FileName = Path.GetFileName(failedTask.OutputPath)
                    });
                }
            }

            lock (_sequencePhotoStatsLock)
            {
                foreach (var serie in _sequencePhotoStats.OrderBy(stat => stat.Serie))
                {
                    report.PhotoSeriesStats.Add(new SequencePhotoSeriesStats
                    {
                        Serie = serie.Serie,
                        Angle = serie.Angle,
                        Planned = serie.Planned,
                        Succeeded = serie.Succeeded,
                        Failed = serie.Failed,
                        Ignored = serie.Ignored
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

            if (flowPanelReports.InvokeRequired)
            {
                flowPanelReports.Invoke(new Action(ClearFocusStackReports));
            }
            else
            {
                ClearFocusStackReports();
            }
        }

        private void ResetSequencePhotoNotificationTracking()
        {
            lock (_sequencePhotoStatsLock)
            {
                _sequencePhotoStats.Clear();
            }
        }

        private void RegisterSequencePhotoSeries(int serieIndex, int angle, int planned, bool ignored)
        {
            lock (_sequencePhotoStatsLock)
            {
                var stats = GetOrCreateSequencePhotoSeriesStats(serieIndex, angle);
                stats.Planned = planned;
                stats.Ignored = ignored;
            }
        }

        private void MarkSequencePhotoSucceeded(int serieIndex, int angle)
        {
            lock (_sequencePhotoStatsLock)
            {
                var stats = GetOrCreateSequencePhotoSeriesStats(serieIndex, angle);
                stats.Ignored = false;
                stats.Succeeded++;
            }
        }

        private void MarkSequencePhotoFailed(int serieIndex, int angle)
        {
            lock (_sequencePhotoStatsLock)
            {
                var stats = GetOrCreateSequencePhotoSeriesStats(serieIndex, angle);
                stats.Ignored = false;
                stats.Failed++;
            }
        }

        private SequencePhotoSeriesStats GetOrCreateSequencePhotoSeriesStats(int serieIndex, int angle)
        {
            int serieNumber = serieIndex + 1;
            var stats = _sequencePhotoStats.FirstOrDefault(item => item.Serie == serieNumber);
            if (stats != null)
            {
                return stats;
            }

            stats = new SequencePhotoSeriesStats
            {
                Serie = serieNumber,
                Angle = angle
            };
            _sequencePhotoStats.Add(stats);
            return stats;
        }
    }
}
