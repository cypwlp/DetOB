using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Example;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using OB.Models;
using OB.Services;
using OB.Services.impls;
using OB.Tools;
using OB.ViewModels;
using OB.ViewModels.Dialogs;
using OB.Views;
using OB.Views.Dialogs;
using Prism.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace OB
{
    public partial class App : PrismApplication
    {
        protected override AvaloniaObject CreateShell() => null!;

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<Login, LoginViewModel>();
            containerRegistry.RegisterForNavigation<MainWin, MainViewModel>();
            containerRegistry.RegisterForNavigation<Home, HomeViewModel>();
            containerRegistry.RegisterForNavigation<Settings, SettingsViewModel>();
            containerRegistry.RegisterForNavigation<Detect, DetectViewModel>();
            containerRegistry.RegisterForNavigation<Process, ProcessViewModel>();
            containerRegistry.RegisterDialog<UpdateDialog, UpdateViewModel>();
            containerRegistry.RegisterDialog<About, AboutViewModel>();
            containerRegistry.RegisterDialog<UploadDialog, UploadViewModel>();

            // 注册 IP 定位服务
            containerRegistry.RegisterSingleton<ISearcher>(() =>
            {
                // IP2Region 数据库文件路径（确保该文件存在于运行目录）
                string dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "ip2region_v6.xdb");
                return new Searcher(CachePolicy.Content, dbPath);
            });
            containerRegistry.RegisterSingleton<IIpLocationService, IpLocationServiceImpl>();

            // 注册更新服务
            containerRegistry.RegisterSingleton<IUpdateService, UpdateServiceImpl>();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                _ = StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        private async Task StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            var splashWindow = new Window
            {
                Width = 1,
                Height = 1,
                SystemDecorations = SystemDecorations.None,
                ShowInTaskbar = false,
                Opacity = 0
            };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            var dialogService = Container.Resolve<IDialogService>();

            dialogService.ShowDialog("Login", null, async result =>
            {
                if (result?.Result == ButtonResult.OK)
                {
                    if (result.Parameters.TryGetValue<RemoteDBTools>("dbtools", out var dbtools) &&
                        result.Parameters.TryGetValue<LogUserInfo>("LogUser", out var logUser))
                    {
                        var mainWin = Container.Resolve<MainWin>();
                        var vm = Container.Resolve<MainViewModel>();
                        vm.LogUser = logUser;
                        vm.RemoteDBTools = dbtools;
                        mainWin.DataContext = vm;

                        var regionManager = Container.Resolve<IRegionManager>();
                        RegionManager.SetRegionManager(mainWin, regionManager);

                        mainWin.Show();
                        desktopLifetime.MainWindow = mainWin;

                        await vm.DefaultNavigateAsync();

                        // 检查更新（自动判断通道）
                        var updateService = Container.Resolve<IUpdateService>();
                        _ = updateService.CheckAndUpdateAsync(dialogService);

                        splashWindow.Close();
                    }
                    else
                    {
                        splashWindow.Close();
                        desktopLifetime.Shutdown();
                    }
                }
                else
                {
                    splashWindow.Close();
                    desktopLifetime.Shutdown();
                }
            });
        }
    }
}