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
using System.Linq;
using System.Net;
using System.Reflection;
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
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                string appcastUrl = "https://github.com/cypwlp/OB/releases/latest/download/appcast.xml";
                string assemblyPath = System.Environment.ProcessPath ?? Assembly.GetEntryAssembly()?.Location ?? "";

                System.Diagnostics.Debug.WriteLine($"[Update] 啟動更新檢查，路徑: {assemblyPath}");

                // 使用 Unsafe 模式處理無簽名情況
                _sparkle = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe), assemblyPath)
                {
                    UIFactory = null,
                    RelaunchAfterUpdate = true
                };

                _sparkle.DownloadFinished += (sender, path) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Update] 更新包已下載至: {path}");
                    _updateInstallerPath = path;
                    _isUpdateReady = true; // 設置狀態
                    UpdateReadyToInstall?.Invoke(null, EventArgs.Empty);
                };

                var updateInfo = await _sparkle.CheckForUpdatesQuietly();

                if (updateInfo.Status == UpdateStatus.UpdateAvailable && updateInfo.Updates?.Count > 0)
                {
                    _updateItem = updateInfo.Updates[0];
                    System.Diagnostics.Debug.WriteLine($"[Update] 檢測到新版本: {_updateItem.Version}，開始背景下載...");
                    await _sparkle.InitAndBeginDownload(_updateItem);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Update] 無需更新。當前識別版本: {_sparkle.Configuration?.InstalledVersion}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Update] 錯誤: {ex.Message}");
            }
        }

        public static void InstallUpdate()
        {
            if (_sparkle != null && _updateItem != null && _updateInstallerPath != null)
            {
                System.Diagnostics.Debug.WriteLine("[Update] 執行安裝並重啟...");
                _sparkle.InstallUpdate(_updateItem, _updateInstallerPath);
            }
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            var splashWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SystemDecorations = SystemDecorations.None,
                Background = new SolidColorBrush(Colors.White),
                ShowInTaskbar = false
            };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            // 異步檢查更新
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
                    else { splashWindow.Close(); desktopLifetime.Shutdown(); }
                }
                else { splashWindow.Close(); desktopLifetime.Shutdown(); }
            });
        }
    }
}