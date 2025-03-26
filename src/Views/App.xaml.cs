using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;

using Skatech.Components;
using Skatech.Components.Settings;

namespace Skatech.Euphoria;

public partial class App : Application {
    public static readonly string AppdataDirectory =
        Environment.GetEnvironmentVariable("EUP_DATA_DIR") ?? Path.GetFullPath("./APPDATA");
    public static string LegacyDataDirectory { get; private set; } = String.Empty;

    public App() {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--nogpu"))) {
            System.Windows.Media.RenderOptions.ProcessRenderMode =
                System.Windows.Interop.RenderMode.SoftwareOnly;
        }
        Startup += OnStartup;
        Exit += OnExit;
    }
    
    private void OnStartup(object sender, StartupEventArgs e) {
        Directory.CreateDirectory(AppdataDirectory);
        ServiceLocator.Register<ISettings>(SettingsService.Create(AppdataDirectory));

        var root = ServiceLocator.Resolve<ISettings>().GetString(
            "ImageServiceRoot", Path.Combine(AppdataDirectory, "Data"), true);
        ServiceLocator.Register<IImageDataService>(new ImageDataService(root));
        LegacyDataDirectory = Path.Combine(root, "@exh");
    }
    
    private void OnExit(object sender, ExitEventArgs e) {
        if (ServiceLocator.Resolve<ISettings>() is IDisposable settings)
            settings.Dispose();
    }
}
