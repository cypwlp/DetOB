using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// 初始化並檢查更新 (使用 NetSparkle 原生 UI)
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // 確保安全傳輸協議
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

                string appcastUrl = @"https://github.com/cypwlp/OB/releases/latest/download/appcast.xml";

                var assembly = Assembly.GetEntryAssembly();
                string assemblyPath = System.Environment.ProcessPath ?? assembly?.Location ?? "";

                // 【修正 1】：類名是 UIFactory 而不是 AvaloniaUIFactory
                var guiFactory = new UIFactory();

                // 2. 初始化 SparkleUpdater
                SparkleInstance = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe), assemblyPath)
                {
                    UIFactory = guiFactory,
                    RelaunchAfterUpdate = true,
                    RestartExecutableName = "OB.exe"
                };

                // 3. 背景檢查更新
                var updateInfo = await SparkleInstance.CheckForUpdatesQuietly();

                if (updateInfo.Status == UpdateStatus.UpdateAvailable && updateInfo.Updates != null)
                {
                    // 【修正 2】：傳入 List<AppCastItem> 而不是單個項
                    var updatesList = new List<AppCastItem> { updateInfo.Updates[0] };

                    // 4. 發現更新！切換到 UI 線程彈出 NetSparkle 的原生更新窗口
                    Dispatcher.UIThread.Post(() =>
                    {
                        SparkleInstance.ShowUpdateNeededUI(updatesList);
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

            // 啟動後台更新檢查
            _ = CheckForUpdatesAsync();

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