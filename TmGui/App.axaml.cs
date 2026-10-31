using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TmGui.ViewModels;
using TmGui.Views;
using Grpc.Net.Client;

namespace TmGui;

public partial class App : Application
{
    private GrpcChannel channel;
    private Models.TargetManagerProxy managerProxy;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        /* Setup grpc channel. */
        channel = GrpcChannel.ForAddress("http://localhost:5138");
        
        /* Setup static vars for proxy objects. */
        Models.TargetManagerProxy.Initialize(channel);
        Models.TargetProxy.Initialize(channel);
        
        /* Setup manager proxy. */
        managerProxy = new();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(managerProxy),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}