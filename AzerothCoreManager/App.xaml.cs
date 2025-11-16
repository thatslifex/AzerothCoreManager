using System.Configuration;
using System.Data;
using System.Windows;
using NetSparkleUpdater;
using NetSparkleUpdater.UI.WPF;

namespace AzerothCoreManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private SparkleUpdater? _updater;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _updater = new SparkleUpdater("https://raw.githubusercontent.com/deinrepo/main/update_feed.xml");
            _updater.UIFactory = new WPFUIFactory();
            _updater.CheckForUpdatesAtUserRequest();
        }
    }

}
