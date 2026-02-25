using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DryIoc.ImTools;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.Avalonia; // 確保有引用這個
using OB.Models;
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
using System.Collections.Generic; // 必須引用，為了使用 List<>
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace OB
{
    public partial class App : PrismApplication
    {
        // 將 Sparkle 暴露出來，方便在設置頁面手動觸發檢查
        public static SparkleUpdater? SparkleInstance { get; private set; }

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
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                string appcastUrl = @"https://github.com/cypwlp/OB/releases/latest/download/appcast.xml";

                var assembly = Assembly.GetEntryAssembly();
                string assemblyPath = Environment.ProcessPath ?? assembly?.Location ?? "";

                // 注意：這裡不需要設定 UIFactory 了，因為我們要自己寫 UI
                SparkleInstance = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe), assemblyPath)
                {
                    RelaunchAfterUpdate = true,
                    RestartExecutableName = "OB.exe",
                    UserInteractionMode = UserInteractionMode.DownloadAndInstall
                };

                // 1. 靜默檢查更新
                var updateInfo = await SparkleInstance.CheckForUpdatesQuietly();

                if (updateInfo.Status == UpdateStatus.UpdateAvailable && updateInfo.Updates?.Count > 0)
                {
                    var latestUpdate = updateInfo.Updates[0];

                    // 2. 切換到 UI 線程，使用 Prism DialogService 彈出你的自定義視窗
                    Dispatcher.UIThread.Post(() =>
                    {
                        var dialogService = Container.Resolve<IDialogService>();
                        var parameters = new DialogParameters { { "UpdateInfo", latestUpdate } };

                        dialogService.ShowDialog("UpdateDialog", parameters, result =>
                        {
                            if (result.Result == ButtonResult.OK)
                            {
                                SparkleInstance?.InstallUpdate(latestUpdate);
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Update Error] {ex.Message}");
            }
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // 創建一個透明的虛擬視窗作為初始主窗口
            var splashWindow = new Window { Width = 1, Height = 1, SystemDecorations = SystemDecorations.None, ShowInTaskbar = false, Opacity = 0 };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            var dialogService = Container.Resolve<IDialogService>();
            dialogService.ShowDialog("Login", null, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    if (result.Parameters.TryGetValue<RemoteDBTools>("dbtools", out var dbtools) &&
                        result.Parameters.TryGetValue<LogUserInfo>("LogUser", out var LogUser))
                    {
                        var mainWin = Container.Resolve<MainWin>();
                        var vm = Container.Resolve<MainViewModel>();
                        vm.LogUser = LogUser;
                        vm.RemoteDBTools = dbtools;
                        mainWin.DataContext = vm;

                        var regionManager = Container.Resolve<IRegionManager>();
                        RegionManager.SetRegionManager(mainWin, regionManager);

                        mainWin.Show();
                        desktopLifetime.MainWindow = mainWin;
                        await vm.DefaultNavigateAsync();
                        _ = CheckForUpdatesAsync();
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