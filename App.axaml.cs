using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Events;
using NetSparkleUpdater.SignatureVerifiers;
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
using System.Threading.Tasks;

namespace OB
{
    public partial class App : PrismApplication
    {
        private static SparkleUpdater? _sparkle;
        private static bool _isUpdateReady = false;
        private static AppCastItem? _updateItem;
        private static string? _updateInstallerPath;
        public static event EventHandler? UpdateReadyToInstall;
        public static bool IsUpdateReady => _isUpdateReady;

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

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                string appcastUrl = "https://github.com/cypwlp/OB/releases/latest/download/appcast.xml";
                _sparkle = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe))
                {
                    SecurityProtocolType = ServicePointManager.SecurityProtocol, // 使用系統默認
                    UIFactory = null, // 無內建 UI
                    RelaunchAfterUpdate = true // 更新後自動重啟
                };

                // 更新檢測 → 自動開始下載
                _sparkle.UpdateDetected += async (sender, e) =>
                {
                    if (e.LatestVersion != null)
                    {
                        _updateItem = e.LatestVersion;
                        await _sparkle.InitAndBeginDownload(_updateItem);
                    }
                };

                // 下載完成 → 標記並觸發事件
                _sparkle.DownloadFinished += (sender, path) =>
                {
                    _isUpdateReady = true;
                    _updateInstallerPath = path;
                    UpdateReadyToInstall?.Invoke(null, EventArgs.Empty);
                };

                // 靜默檢查更新（异步調用）
                var updateInfo = await _sparkle.CheckForUpdatesQuietly();
                if (updateInfo.Status == UpdateStatus.UpdateAvailable)
                {
                    // 事件會自動處理
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新檢查失敗: {ex}");
            }
        }

        public static void InstallUpdate()
        {
            if (_sparkle != null && _updateItem != null && _updateInstallerPath != null)
            {
                _sparkle.InstallUpdate(_updateItem, _updateInstallerPath);
            }
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // 臨時隱藏窗口（作為登入對話框的所有者）
            var splashWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SystemDecorations = SystemDecorations.None,
                Background = new SolidColorBrush(Colors.White),
                ShowInTaskbar = false,
                ShowActivated = false,
                IsHitTestVisible = false
            };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            // 後台開始更新檢查（不阻塞 UI）
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