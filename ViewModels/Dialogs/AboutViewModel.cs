using Prism.Dialogs;
using Prism.Mvvm;
using Prism.Commands;
using System;
using Velopack;
using System.Threading.Tasks;
using Avalonia.Threading;
using Prism.Ioc;
using Velopack.Sources;
using Material.Icons; // 確保引用此命名空間以使用 MaterialIconKind

namespace OB.ViewModels.Dialogs
{
    public class AboutViewModel : BindableBase, IDialogAware
    {
        private readonly IDialogService _dialogService;

        public string Title => "關於 OB";
        public DialogCloseListener RequestClose { get; private set; }

        private string _currentVersion = "1.0.0";
        public string CurrentVersion
        {
            get => _currentVersion;
            set => SetProperty(ref _currentVersion, value);
        }

        private bool _isChecking;
        public bool IsChecking
        {
            get => _isChecking;
            set
            {
                if (SetProperty(ref _isChecking, value))
                {
                    // 當狀態改變時，通知按鈕文字和圖標更新
                    RaisePropertyChanged(nameof(UpdateButtonText));
                    RaisePropertyChanged(nameof(UpdateIcon));
                }
            }
        }

        // 動態文字與圖標
        public string UpdateButtonText => IsChecking ? "正在檢查..." : "檢查更新";
        public MaterialIconKind UpdateIcon => IsChecking ? MaterialIconKind.Refresh : MaterialIconKind.Update;

        public DelegateCommand CheckUpdateCommand { get; }

        public AboutViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            // 【修正點】：將 CurrentlyInstalledVersion 改為 CurrentVersion
            var mgr = new UpdateManager(new GithubSource("https://github.com/cypwlp/OB", "", false));
            CurrentVersion = mgr.CurrentVersion?.ToString() ?? "開發版本";

            CheckUpdateCommand = new DelegateCommand(async () => await CheckForUpdatesInternalAsync());
        }

        private async Task CheckForUpdatesInternalAsync()
        {
            if (IsChecking) return;
            IsChecking = true;

            try
            {
                var source = new GithubSource("https://github.com/cypwlp/OB", "", false);
                var mgr = new UpdateManager(source);
                var updateInfo = await mgr.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    // 如果沒有更新，可以彈出提示或在介面顯示
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var parameters = new DialogParameters { { "UpdateInfo", updateInfo } };
                    var result = await _dialogService.ShowDialogAsync("UpdateDialog", parameters);

                    if (result?.Result == ButtonResult.OK)
                    {
                        await mgr.DownloadUpdatesAsync(updateInfo);
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新檢查失敗: {ex.Message}");
            }
            finally
            {
                IsChecking = false;
            }
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
    }
}