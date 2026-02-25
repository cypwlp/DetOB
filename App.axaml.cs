using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OB
{
    public partial class App : PrismApplication
    {
        private static SparkleUpdater? _sparkle;
        private static bool _isUpdateReady = false;
        private static AppCastItem? _updateItem;
        private static string? _updateInstallerPath;
        private static StringBuilder _debugLogs = new StringBuilder();

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
            _debugLogs.Clear();
            _debugLogs.AppendLine($"--- 更新檢查診斷報告 ({DateTime.Now}) ---");

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                string appcastUrl = "https://github.com/cypwlp/OB/releases/latest/download/appcast.xml";

                var assembly = Assembly.GetEntryAssembly();
                var currentVersion = assembly?.GetName().Version ?? new Version(0, 0, 0, 0);
                string assemblyPath = System.Environment.ProcessPath ?? assembly?.Location ?? "";

                _debugLogs.AppendLine($"[本地版本] {currentVersion}");
                _debugLogs.AppendLine($"[檔案路徑] {assemblyPath}");

                _sparkle = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe), assemblyPath)
                {
                    UIFactory = null,
                    RelaunchAfterUpdate = true
                };

                // 修正：NetSparkle 3.0.3 的 DownloadStarted 只有 2 個參數 (item, path)
                _sparkle.DownloadStarted += (item, path) => _debugLogs.AppendLine($"[下載] 開始下載: {item.Version}");

                // 修正：NetSparkle 3.0.3 的 DownloadFinished 只有 2 個參數 (item, path)
                _sparkle.DownloadFinished += (item, path) =>
                {
                    _debugLogs.AppendLine($"[下載] 成功！存儲於: {path}");
                    _updateInstallerPath = path;
                    _isUpdateReady = true;
                    UpdateReadyToInstall?.Invoke(null, EventArgs.Empty);
                };

                // 修正：事件名稱為 DownloadHadError，參數為 (item, path, exception)
                _sparkle.DownloadHadError += (item, path, exception) =>
                {
                    string errMsg = $"[錯誤] 下載失敗！原因: {exception.Message}";
                    _debugLogs.AppendLine(errMsg);
                    ShowDebugError("下載更新包失敗", errMsg);
                };

                var updateInfo = await _sparkle.CheckForUpdatesQuietly();
                _debugLogs.AppendLine($"[NetSparkle狀態] {updateInfo.Status}");

                if (updateInfo.Status == UpdateStatus.UpdateAvailable && updateInfo.Updates?.Count > 0)
                {
                    _updateItem = updateInfo.Updates[0];
                    _debugLogs.AppendLine($"[偵測到新版本] {_updateItem.Version}");

                    if (new Version(_updateItem.Version) > currentVersion)
                    {
                        _debugLogs.AppendLine("[動作] 啟動背景下載...");
                        await _sparkle.InitAndBeginDownload(_updateItem);
                    }
                    else
                    {
                        _debugLogs.AppendLine("[忽略] 雲端版本不比本地高。");
                    }
                }
                else
                {
                    // 使用 else 處理所有「不需要更新」的情況 (UpdateNotAvailable, CouldNotDetermine 等)
                    _debugLogs.AppendLine($"[結果] 目前不執行更新。狀態碼: {updateInfo.Status}");
                }
            }
            catch (Exception ex)
            {
                _debugLogs.AppendLine($"[崩潰] {ex.Message}");
                ShowDebugError("更新檢查過程出錯", _debugLogs.ToString());
            }
        }

        private void ShowDebugError(string title, string content)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var tipText = new TextBlock
                {
                    Text = "?? 請截圖此視窗回報給開發者",
                    Margin = new Thickness(10),
                    Foreground = Brushes.Red,
                    FontWeight = FontWeight.Bold
                };
                DockPanel.SetDock(tipText, Dock.Top);

                var logBox = new TextBox
                {
                    Text = content,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10)
                };

                var rootPanel = new DockPanel();
                rootPanel.Children.Add(tipText);
                rootPanel.Children.Add(logBox);

                var win = new Window
                {
                    Title = "?? 更新診斷助手 - " + title,
                    Width = 600,
                    Height = 450,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = rootPanel
                };

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    await win.ShowDialog(desktop.MainWindow ?? win);
            });
        }

        public static void InstallUpdate()
        {
            if (_sparkle != null && _updateItem != null && _updateInstallerPath != null)
                _sparkle.InstallUpdate(_updateItem, _updateInstallerPath);
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            var splashWindow = new Window { Width = 1, Height = 1, SystemDecorations = SystemDecorations.None, ShowInTaskbar = false };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

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