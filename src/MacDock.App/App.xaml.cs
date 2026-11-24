using System.Windows;
using MacDock.App.Views;
using MacDock.Core.Config;
using MacDock.Core.Dock;
using MacDock.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MacDock.App;

/// <summary>
/// Composition root: the only place that knows concrete types. Everything else receives its
/// dependencies through constructors, which is what keeps Core testable without Windows.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\MacDock.SingleInstance";

    private Mutex? _singleInstance;
    private IHost? _host;

    /// <summary>Where unhandled exceptions are written, since a WinExe has no console to show them.</summary>
    public static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacDock", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) => { ReportCrash(args.Exception); args.Handled = true; Shutdown(1); };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => ReportCrash(args.ExceptionObject as Exception);

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        var builder = Host.CreateApplicationBuilder(e.Args);
        builder.Services.AddSingleton<IConfigStore>(_ => new JsonConfigStore(JsonConfigStore.DefaultPath));
        builder.Services.AddSingleton<IAppLauncher, ShellAppLauncher>();
        builder.Services.AddSingleton<IIconProvider, ShellIconProvider>();
        builder.Services.AddSingleton<DockViewModel>();
        builder.Services.AddSingleton<DockWindow>();
        _host = builder.Build();

        EnsureConfigFileExists(_host.Services.GetRequiredService<IConfigStore>());

        _host.Services.GetRequiredService<DockWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void ReportCrash(Exception? ex)
    {
        string text = $"[{DateTime.Now:O}] {ex}{Environment.NewLine}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, text);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Console.Error.WriteLine(text); // visible when started with `dotnet run`
        MessageBox.Show($"MacDock hit an error and will close.\n\n{ex?.Message}\n\nDetails: {CrashLogPath}", "MacDock", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>Writes the default config on first run so the user has a file to edit.</summary>
    private static void EnsureConfigFileExists(IConfigStore store)
    {
        if (!File.Exists(JsonConfigStore.DefaultPath))
            store.Save(store.Load());
    }
}
