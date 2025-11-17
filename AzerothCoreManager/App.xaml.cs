using NetSparkleUpdater;
using NetSparkleUpdater.Interfaces;
using NetSparkleUpdater.UI.WPF;
using System.Windows;

namespace AzerothCoreManager
{
    public partial class App : Application
    {
        private SparkleUpdater? _sparkle;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // URL zu deiner update_feed.xml (kann lokal oder auf Webserver)
            string update_feedUrl = "https://github.com/thatslifex/AzerothCoreManager/raw/refs/heads/master/AzerothCoreManager/update_feed.xml";

            // NoSignatureChecker überspringt Signaturprüfung
            var signatureChecker = new NoSignatureChecker();

            _sparkle = new SparkleUpdater(appcastUrl, signatureChecker)
            {
                UIFactory = new WpfUIFactory(),
                RelaunchAfterUpdate = true
            };

            // Nur prüfen, wenn App startet oder du manuell Start-Button einbaust
            _sparkle.CheckForUpdatesAtStartup = true;
            _sparkle.StartLoop(true, 3600); // optional: jede Stunde prüfen
        }
    }
}
