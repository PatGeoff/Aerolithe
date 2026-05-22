using System.Text;

namespace Aerolithe
{
    public sealed class SequenceNotificationReport
    {
        public string ProjectName { get; set; } = string.Empty;
        public string SequenceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public TimeSpan Duration => FinishedAt - StartedAt;
        public bool FocusStackEnabled { get; set; }
        public int FocusStackSucceeded { get; set; }
        public int FocusStackFailed { get; set; }
        public int FocusStackPendingOrRunning { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<SequencePhotoSeriesStats> PhotoSeriesStats { get; } = new();
        public List<FocusStackNotificationItem> FocusStackFailures { get; } = new();

        public string Subject
        {
            get
            {
                string project = string.IsNullOrWhiteSpace(ProjectName) ? "Projet sans nom" : ProjectName;
                return $"Aerolithe - {SequenceName} - {project} - {Status}";
            }
        }

        public string BuildBody()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Rapport de sequence Aerolithe");
            sb.AppendLine();
            sb.AppendLine($"Projet: {ProjectName}");
            sb.AppendLine($"Sequence: {SequenceName}");
            sb.AppendLine($"Etat: {Status}");
            sb.AppendLine($"Debut: {StartedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Fin: {FinishedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Duree: {Duration:hh\\:mm\\:ss}");
            sb.AppendLine();
            sb.AppendLine($"Focus stack active: {(FocusStackEnabled ? "Oui" : "Non")}");

            if (PhotoSeriesStats.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Photos par serie:");
                foreach (var serie in PhotoSeriesStats.OrderBy(s => s.Serie))
                {
                    if (serie.Ignored)
                    {
                        sb.AppendLine($"- Serie {serie.Serie} ({serie.Angle} deg): serie ignoree");
                        continue;
                    }

                    string failedText = serie.Failed == 0
                        ? "0 echec"
                        : serie.Failed == 1
                            ? "1 echec"
                            : $"{serie.Failed} echecs";

                    sb.AppendLine($"- Serie {serie.Serie} ({serie.Angle} deg): {serie.Succeeded} photos reussies, {failedText}");
                }
            }

            if (FocusStackEnabled)
            {
                sb.AppendLine($"Focus stacks reussis: {FocusStackSucceeded}");
                sb.AppendLine($"Focus stacks echoues: {FocusStackFailed}");
                sb.AppendLine($"Focus stacks encore en attente/en cours: {FocusStackPendingOrRunning}");
            }

            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                sb.AppendLine();
                sb.AppendLine("Erreur:");
                sb.AppendLine(ErrorMessage);
            }

            if (FocusStackFailures.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Focus stacks echoues:");
                foreach (var failure in FocusStackFailures)
                {
                    sb.AppendLine($"- Serie {failure.Serie}, elevation {failure.Elevation} deg, rotation {failure.Rotation}, fichier {failure.FileName}");
                }
            }

            return sb.ToString();
        }
    }

    public sealed class FocusStackNotificationItem
    {
        public int Serie { get; set; }
        public int Elevation { get; set; }
        public int Rotation { get; set; }
        public string FileName { get; set; } = string.Empty;
    }

    public sealed class SequencePhotoSeriesStats
    {
        public int Serie { get; set; }
        public int Angle { get; set; }
        public int Planned { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public bool Ignored { get; set; }
    }
}
