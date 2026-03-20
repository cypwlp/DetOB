using Avalonia;
using OB.Views;
using ReactiveUI.Avalonia;
using System;
using Velopack;

namespace OB
{
    internal sealed class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Velopack 必须放在第一行
            VelopackApp.Build().Run();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();
    }
}