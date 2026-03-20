using Prism.Mvvm;
using Prism.Dialogs;
using Prism.Commands;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OB.ViewModels.Dialogs
{
    public class UploadViewModel : BindableBase, IDialogAware
    {
        public DialogCloseListener RequestClose { get; private set; }

        private string _version;
        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        private string _updateLog;
        public string UpdateLog
        {
            get => _updateLog;
            set => SetProperty(ref _updateLog, value);
        }

        private string _changelog;
        public string Changelog
        {
            get => _changelog;
            set => SetProperty(ref _changelog, value);
        }

        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public UploadViewModel()
        {
            ConfirmCommand = new DelegateCommand(async () => await UploadAsync());
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

        private async Task UploadAsync()
        {
            if (string.IsNullOrWhiteSpace(Version) || string.IsNullOrWhiteSpace(UpdateLog))
            {
                Debug.WriteLine("版本号或更新日志不能为空");
                return;
            }

            try
            {
                string gitTag = $"v{Version.TrimStart('v')}";
                string message = $"{UpdateLog}\n\n{Changelog}".Replace("\"", "\\\"");

                // 创建带注释的标签
                var tagProcess = System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"tag -a {gitTag} -m \"{message}\"",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                await tagProcess.WaitForExitAsync();
                if (tagProcess.ExitCode != 0)
                {
                    string error = await tagProcess.StandardError.ReadToEndAsync();
                    Debug.WriteLine($"创建标签失败: {error}");
                    // 可在此显示错误对话框
                    return;
                }

                // 推送标签
                var pushProcess = System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"push origin {gitTag}",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                await pushProcess.WaitForExitAsync();
                if (pushProcess.ExitCode != 0)
                {
                    string error = await pushProcess.StandardError.ReadToEndAsync();
                    Debug.WriteLine($"推送标签失败: {error}");
                    return;
                }

                RequestClose.Invoke(new DialogResult(ButtonResult.OK));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters parameters) { }
        public string Title => "发布新版本";
    }
}