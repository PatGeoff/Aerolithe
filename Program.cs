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
using System.Diagnostics;
using System.Drawing.Text;
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

                if (!EnsurePhosphorFontIsInstalled())
                {
                    StartupLog("Required font is missing. Startup aborted.");
                    return;
                }

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

        private static bool EnsurePhosphorFontIsInstalled()
        {
            const string fontFamilyName = "Phosphor";

            if (IsFontInstalled(fontFamilyName))
            {
                StartupLog("Required font is installed: " + fontFamilyName);
                return true;
            }

            var fontPath = Path.Combine(
                AppContext.BaseDirectory,
                "MyResources",
                "Fonts",
                "Phosphor",
                "regular",
                "Phosphor.ttf");

            if (!File.Exists(fontPath))
            {
                MessageBox.Show(
                    "La police Phosphor n'est pas installee sur Windows.\n\n" +
                    "Aerolithe utilise cette police pour afficher les icones de l'interface.\n\n" +
                    "Le fichier d'installation est introuvable :\n" +
                    fontPath + "\n\n" +
                    "Reinstallez Aerolithe ou copiez Phosphor.ttf, puis redemarrez l'application.",
                    "Aerolithe - police manquante",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                StartupLog("Phosphor font file not found: " + fontPath);
                return false;
            }

            var result = MessageBox.Show(
                "La police Phosphor n'est pas installee sur Windows.\n\n" +
                "Aerolithe utilise cette police pour afficher les icones de l'interface.\n\n" +
                "Voulez-vous ouvrir le fichier Phosphor.ttf maintenant pour l'installer?\n\n" +
                "Apres l'installation, redemarrez Aerolithe.",
                "Aerolithe - police manquante",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(fontPath)
                    {
                        UseShellExecute = true
                    });

                    StartupLog("Opened Phosphor font installer: " + fontPath);
                }
                catch (Exception ex)
                {
                    StartupLog("Unable to open Phosphor font installer: " + ex);
                    MessageBox.Show(
                        "Impossible d'ouvrir le fichier de police automatiquement.\n\n" +
                        "Installez manuellement ce fichier, puis redemarrez Aerolithe :\n" +
                        fontPath + "\n\n" +
                        ex.Message,
                        "Aerolithe - police manquante",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            return false;
        }

        private static bool IsFontInstalled(string fontFamilyName)
        {
            using var fonts = new InstalledFontCollection();

            foreach (var family in fonts.Families)
            {
                if (string.Equals(family.Name, fontFamilyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void StartupLog(string message)
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
