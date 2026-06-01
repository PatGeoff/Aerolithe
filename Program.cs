//namespace Aerolithe
//{
//    internal static class Program
//    {
//        /// <summary>
//        ///  The main entry point for the application.
//        /// </summary>
//        [STAThread]
//        static void Main()
//        {
//            // To customize application configuration such as set high DPI appSettings or default font,
//            // see https://aka.ms/applicationconfiguration.
//            ApplicationConfiguration.Initialize();
//            Application.Run(new Aerolithe());
//        }
//    }
//}


using Aerolithe;              // <-- utilitaire AppLifecycle.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aerolithe
{
    internal static class Program
    {
        /// <summary>
        /// Point d'entrée de l'application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            StartupLog("Main started.");
            CrashLog.WriteEvent("Main started.");

            try
            {
                // Config WinForms (.NET 8)
                ApplicationConfiguration.Initialize();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                StartupLog("ApplicationConfiguration initialized.");
                CrashLog.WriteEvent("ApplicationConfiguration initialized.");

                // Empêcher plusieurs instances simultanées d’Aerolithe
                if (!AppLifecycle.EnsureSingleInstance("Aerolithe_SingleInstance"))
                {
                    StartupLog("Another Aerolithe instance was detected. Closing this process.");
                    // Facultatif : fermer les autres instances déjà ouvertes (si tu relances par erreur)
                    AppLifecycle.TryCloseOtherInstances("Aerolithe", waitMs: 1500);
                    return; // on ne lance pas une nouvelle instance
                }

                // Hooks globaux de fermeture / erreurs non gérées :
                Application.ApplicationExit += (_, __) =>
                {
                    StartupLog("ApplicationExit.");
                    CrashLog.WriteEvent("ApplicationExit.");
                    AppLifecycle.StopAllGraceful(waitMsPerTask: 100);
                };

                AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                {
                    StartupLog("ProcessExit.");
                    CrashLog.WriteEvent("ProcessExit.");
                    AppLifecycle.StopAllGraceful(waitMsPerTask: 100);
                };

                Application.ThreadException += (_, e) =>
                {
                    string logPath = CrashLog.WriteCrash("Application.ThreadException", e.Exception);
                    StartupLog("ThreadException logged to: " + logPath);
                    AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1000, killIfStuck: true);
                };

                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    string logPath = CrashLog.WriteCrash(
                        "AppDomain.CurrentDomain.UnhandledException",
                        e.ExceptionObject as Exception,
                        e.ExceptionObject);
                    StartupLog("UnhandledException logged to: " + logPath);
                    StartupLog("UnhandledException: " + e.ExceptionObject);
                    AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1000, killIfStuck: true);
                };

                TaskScheduler.UnobservedTaskException += (_, e) =>
                {
                    string logPath = CrashLog.WriteCrash("TaskScheduler.UnobservedTaskException", e.Exception);
                    StartupLog("UnobservedTaskException logged to: " + logPath);
                    e.SetObserved();
                };

                StartupLog("Creating main form.");
                using var mainForm = new Aerolithe();
                CrashLog.ProjectPathProvider = () => mainForm.appSettings?.ProjectPath;

                StartupLog("Starting message loop.");
                Application.Run(mainForm);

                // Ceinture & bretelles : si on revient de Run(), on force l’arrêt si quelque chose traîne
                StartupLog("Application.Run returned.");
                AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1000, killIfStuck: true);
            }
            catch (Exception ex)
            {
                string logPath = CrashLog.WriteCrash("Fatal startup exception", ex);
                StartupLog("Fatal startup exception: " + ex);
                MessageBox.Show(
                    "Aerolithe n'a pas pu demarrer.\n\n" + ex + "\n\nLog: " + logPath,
                    "Aerolithe - erreur de demarrage",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        internal static void StartupLog(string message)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
                string path = Path.Combine(AppContext.BaseDirectory, "Aerolithe-startup.log");
                try
                {
                    File.AppendAllText(path, line);
                    return;
                }
                catch
                {
                    string fallbackDirectory = Path.Combine(Path.GetTempPath(), "Aerolithe", "logs");
                    Directory.CreateDirectory(fallbackDirectory);
                    File.AppendAllText(Path.Combine(fallbackDirectory, "Aerolithe-startup.log"), line);
                }
            }
            catch
            {
                // Le diagnostic ne doit jamais empecher le demarrage.
            }
        }
    }
}
