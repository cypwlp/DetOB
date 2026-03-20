using OB.Models;
using OB.Services;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.IO;
using Velopack;
using Velopack.Sources;

namespace OB.ViewModels.Dialogs
{
    public class UpdateViewModel : BindableBase, IDialogAware
    {
        private readonly IUpdateService _updateService;

        public DialogCloseListener RequestClose { get; private set; }

        private string _newVersion = "未知版本";
        public string NewVersion
        {
            get => _newVersion;
            set => SetProperty(ref _newVersion, value);
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

        private bool _isInternal;
        private UpdateInfo _updateInfo;       // 用于 GitHub 更新
        private RemoteVersionInfo _remoteInfo; // 用于内部更新

        public DelegateCommand UpdateCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public UpdateViewModel(IUpdateService updateService)
        {
            _updateService = updateService;

            UpdateCommand = new DelegateCommand(async () =>
            {
                if (_isInternal && _remoteInfo != null)
                {
                    string internalSharePath = "http://129.204.149.106:8080/";
                    string releasesPath = Path.Combine(internalSharePath, "releases");  // 建議明確指向 releases 目錄

                    // 正確寫法
                    var source = new SimpleFileSource(new DirectoryInfo(releasesPath));

                    var mgr = new UpdateManager(source);
                    var updateInfo = await mgr.CheckForUpdatesAsync();

                    if (updateInfo != null)
                    {
                        await mgr.DownloadUpdatesAsync(updateInfo);
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                }
                else if (!_isInternal && _updateInfo != null)
                {
                    // GitHub 部分保持不變
                    var mgr = new UpdateManager(new GithubSource("https://github.com/cypwlp/OB", "", false));
                    await mgr.DownloadUpdatesAsync(_updateInfo);
                    mgr.ApplyUpdatesAndRestart(_updateInfo);
                }

                RequestClose.Invoke(new DialogResult(ButtonResult.OK));
            });

            CancelCommand = new DelegateCommand(() =>
            {
                RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
            });
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _isInternal = parameters.GetValue<bool>("IsInternal");

            if (_isInternal)
            {
                _remoteInfo = parameters.GetValue<RemoteVersionInfo>("RemoteInfo");
                NewVersion = parameters.GetValue<string>("RemoteVersion") ?? "未知版本";
                UpdateLog = parameters.GetValue<string>("UpdateLog") ?? "";
                Changelog = parameters.GetValue<string>("Changelog") ?? "";
            }
            else
            {
                if (parameters.TryGetValue<UpdateInfo>("UpdateInfo", out var info))
                {
                    _updateInfo = info;
                    NewVersion = info?.TargetFullRelease?.Version?.ToString() ?? "未知版本";
                    // 如果有 release notes，可进一步解析
                }
            }
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }
        public string Title => "發現新版本";
    }
}