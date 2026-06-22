using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Aerolithe
{
    internal static class CrashLog
    {
        private static readonly object SyncRoot = new();

        public static Func<string?>? ProjectPathProvider { get; set; }

        public static string LogDirectory
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrWhiteSpace(documents))
                {
                    return Path.Combine(AppContext.BaseDirectory, "logs");
                }

                return Path.Combine(documents, "Aerolithe", "logs");
            }
        }

        public static string WriteCrash(string source, Exception? exception = null, object? exceptionObject = null)
        {
            string content = BuildCrashLog(source, exception, exceptionObject);

            lock (SyncRoot)
            {
                foreach (string directory in GetCandidateLogDirectories())
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                        string path = GetCrashLogPath(directory);
                        File.AppendAllText(path, content, Encoding.UTF8);
                        return path;
                    }
                    catch
                    {
                        // Continue vers le prochain emplacement de secours.
                    }
                }
            }

            return "Crash log unavailable: unable to write to any configured log directory.";
        }

        private static IEnumerable<string> GetCandidateLogDirectories()
        {
            yield return LogDirectory;
            yield return Path.Combine(AppContext.BaseDirectory, "logs");

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                yield return Path.Combine(localAppData, "Aerolithe", "logs");
            }

            string temp = Path.GetTempPath();
            if (!string.IsNullOrWhiteSpace(temp))
            {
                yield return Path.Combine(temp, "Aerolithe", "logs");
            }
        }

        private static string GetCrashLogPath(string directory)
        {
            return Path.Combine(directory, $"aerolithe-crash-{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.log");
        }

        private static string BuildCrashLog(string source, Exception? exception, object? exceptionObject)
        {
            var process = Process.GetCurrentProcess();
            var assembly = Assembly.GetExecutingAssembly();
            var sb = new StringBuilder();

            sb.AppendLine("Aerolithe crash log");
            sb.AppendLine($"Date locale: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Date UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Source: {source}");
            sb.AppendLine($"Version: {assembly.GetName().Version}");
            sb.AppendLine($"Process: {process.ProcessName} ({process.Id})");
            sb.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($".NET: {Environment.Version}");
            sb.AppendLine($"Machine: {Environment.MachineName}");
            sb.AppendLine($"User: {Environment.UserName}");
            sb.AppendLine($"Thread: {Environment.CurrentManagedThreadId}");
            sb.AppendLine($"WorkingSet: {process.WorkingSet64:N0} bytes");
            sb.AppendLine($"PrivateMemory: {process.PrivateMemorySize64:N0} bytes");

            try
            {
                string? projectPath = ProjectPathProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(projectPath))
                {
                    sb.AppendLine($"ProjectPath: {projectPath}");
                }
            }
            catch
            {
                sb.AppendLine("ProjectPath: unavailable");
            }

            sb.AppendLine();
            sb.AppendLine("Exception:");
            if (exception != null)
            {
                sb.AppendLine(exception.ToString());
            }
            else if (exceptionObject != null)
            {
                sb.AppendLine(exceptionObject.ToString());
            }
            else
            {
                sb.AppendLine("Aucune exception fournie.");
            }

            sb.AppendLine();
            sb.AppendLine(new string('-', 80));
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
