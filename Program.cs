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
            // Config WinForms (.NET 8)
            ApplicationConfiguration.Initialize();

            // Empêcher plusieurs instances simultanées d’Aerolithe
            if (!AppLifecycle.EnsureSingleInstance("Aerolithe_SingleInstance"))
            {
                // Facultatif : fermer les autres instances déjà ouvertes (si tu relances par erreur)
                AppLifecycle.TryCloseOtherInstances("Aerolithe", waitMs: 1500);
                return; // on ne lance pas une nouvelle instance
            }

            // Hooks globaux de fermeture / erreurs non gérées :
            Application.ApplicationExit += (_, __) =>
                AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1500, killIfStuck: true);

            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1500, killIfStuck: true);

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1000, killIfStuck: true);

            // Lance le Form principal
            Application.Run(new Aerolithe());

            // Ceinture & bretelles : si on revient de Run(), on force l'arrêt si quelque chose traîne
            AppLifecycle.HardExitAfter(AppLifecycle.StopAllGraceful, graceMs: 1000, killIfStuck: true);
        }
    }
}
