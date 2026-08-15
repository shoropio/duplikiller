using System.Windows;
using DupliKiller.Core.Logging;

namespace DupliKiller.App;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            // Registrar el error y evitar mostrar múltiples MessageBox que bloqueen la UI.
            Logger.Error($"UI exception: {args.Exception}");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            // Registrar y no mostrar diálogos modales para excepciones masivas durante operaciones en lote.
            Logger.Error($"Fatal: {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error($"Task exception: {args.Exception}");
            args.SetObserved();
        };
    }
}
