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
                // 1. 確保 TLS 協議支持 GitHub 下載
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

                string appcastUrl = "https://github.com/cypwlp/OB/releases/latest/download/appcast.xml";

                // 2. 獲取當前運行的 .exe 路徑 (這是在 .NET 8 中獲取當前路徑最可靠的方法)
                string assemblyPath = System.Environment.ProcessPath ?? Assembly.GetEntryAssembly()?.Location ?? "";

                System.Diagnostics.Debug.WriteLine($"[Update] 正在檢查路徑: {assemblyPath}");

                // 3. 初始化 SparkleUpdater
                // 【關鍵修復】：使用三個參數的構造函數，傳入 assemblyPath
                // SecurityMode.Unsafe 解決了沒有簽名的問題
                _sparkle = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe), assemblyPath)
                {
                    UIFactory = null,
                    RelaunchAfterUpdate = true
                };

                // 下載完成後的處理
                _sparkle.DownloadFinished += (sender, path) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Update] 更新包下載完成: {path}");
                    _isUpdateReady = true;
                    _updateInstallerPath = path;
                    // 觸發事件通知 UI (MainViewModel 會監聽此事件)
                    UpdateReadyToInstall?.Invoke(null, EventArgs.Empty);
                };

                // 4. 主動檢查更新
                var updateInfo = await _sparkle.CheckForUpdatesQuietly();

                // 判斷是否發現新版本
                if (updateInfo.Status == UpdateStatus.UpdateAvailable && updateInfo.Updates?.Count > 0)
                {
                    _updateItem = updateInfo.Updates[0];
                    System.Diagnostics.Debug.WriteLine($"[Update] 發現新版本: {_updateItem.Version}，正在後台下載...");

                    // 發現更新，立即開始下載
                    await _sparkle.InitAndBeginDownload(_updateItem);
                }
                else
                {
                    // 這裡可以查看識別到的當前版本，方便排查
                    var currentV = _sparkle.Configuration?.InstalledVersion;
                    System.Diagnostics.Debug.WriteLine($"[Update] 無需更新。識別版本: {currentV}, 狀態: {updateInfo.Status}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Update] 更新檢查失敗: {ex.Message}");
            }
        }

        public static void InstallUpdate()
        {
            if (_sparkle != null && _updateItem != null && _updateInstallerPath != null)
            {
                // 調用安裝程序
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
                ShowInTaskbar = false,
                IsHitTestVisible = false
            };
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
                    else { splashWindow.Close(); desktopLifetime.Shutdown(); }
                }
                else { splashWindow.Close(); desktopLifetime.Shutdown(); }
            });
        }
    }
}