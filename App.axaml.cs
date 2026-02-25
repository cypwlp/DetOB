using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using NetSparkleUpdater;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.Avalonia;
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
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace OB
{
    public partial class App : PrismApplication
    {
        // 公開靜態實例，方便在 MainViewModel 中調用
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
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                // 1. 初始化更新器配置（僅配置，不執行檢查）
                InitUpdateConfig();

                // 2. 啟動登錄流程
                StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        private void InitUpdateConfig()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                string appcastUrl = @"https://github.com/cypwlp/OB/releases/latest/download/appcast.xml";

                var assembly = Assembly.GetEntryAssembly();
                string assemblyPath = System.Environment.ProcessPath ?? assembly?.Location ?? "";

                // 初始化 NetSparkle
                SparkleInstance = new SparkleUpdater(appcastUrl, new Ed25519Checker(NetSparkleUpdater.Enums.SecurityMode.Unsafe), assemblyPath)
                {
                    UIFactory = new UIFactory(),
                    RelaunchAfterUpdate = true,
                    RestartExecutableName = "OB.exe"
                };

                // 測試建議：如果你曾經點過「跳過此版本」，取消下面這行註釋可以重置狀態
                // SparkleInstance.ClearSkippedVersion(); 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Update Init Error] {ex.Message}");
            }
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
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