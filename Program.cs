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

            try
            {
                // Config WinForms (.NET 8)
                ApplicationConfiguration.Initialize();
                Application.SetCompatibleTextRenderingDefault(false);
                StartupLog("ApplicationConfiguration initialized.");

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
                    AppLifecycle.StopAllGraceful(waitMsPerTask: 100);
                };

                AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                {
                    StartupLog("ProcessExit.");
                    AppLifecycle.StopAllGraceful(waitMsPerTask: 100);
                };

                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    StartupLog("UnhandledException: " + e.ExceptionObject);
                    AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1000, killIfStuck: true);
                };

                StartupLog("Creating main form.");
                using var mainForm = new Aerolithe();

                StartupLog("Starting message loop.");
                Application.Run(mainForm);

                // Ceinture & bretelles : si on revient de Run(), on force l’arrêt si quelque chose traîne
                StartupLog("Application.Run returned.");
                AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1000, killIfStuck: true);
            }
            catch (Exception ex)
            {
                StartupLog("Fatal startup exception: " + ex);
                MessageBox.Show(
                    "Aerolithe n'a pas pu demarrer.\n\n" + ex,
                    "Aerolithe - erreur de demarrage",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        internal static void StartupLog(string message)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Aerolithe-startup.log");
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // Le diagnostic ne doit jamais empecher le demarrage.
            }
        }
    }
}
