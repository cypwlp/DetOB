using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using OB.Models;
using Prism.Dialogs;
using Velopack;
using Velopack.Sources;

namespace OB.Services.impls
{
    public class UpdateServiceImpl : IUpdateService
    {
        private readonly IIpLocationService _ipService;
        private readonly string _internalSharePath = @"http://129.204.149.106:8080/";
        private readonly string _githubRepoUrl = "https://github.com/cypwlp/OB";

        public UpdateServiceImpl(IIpLocationService ipService)
        {
            _ipService = ipService;
        }

        public async Task<(bool IsChina, UpdateChannel Channel)> DetectUpdateChannelAsync()
        {
            string ip = await _ipService.GetPublicIpAsync();
            if (string.IsNullOrEmpty(ip))
                return (false, UpdateChannel.GitHub);

            string country = _ipService.GetCountryByIp(ip)?.Trim() ?? "";
            bool isChina = country switch
            {
                "中国" or "中國" or "China" or "CN" or "CHN" => true,
                _ => country.Contains("中国") || country.Contains("中國") || country.Contains("内地")
            };

            return (isChina, isChina ? UpdateChannel.Internal : UpdateChannel.GitHub);
        }

        public async Task CheckAndUpdateAsync(IDialogService dialogService)
        {
            var (isChina, channel) = await DetectUpdateChannelAsync();
            if (channel == UpdateChannel.Internal)
            {
                await CheckInternalUpdateAsync(dialogService);
            }
            else
            {
                await CheckGitHubVelopackUpdateAsync(dialogService);
            }
        }

        public async Task CheckGitHubVelopackUpdateAsync(IDialogService dialogService)
        {
            try
            {
                var source = new GithubSource(_githubRepoUrl, "", false);
                var mgr = new UpdateManager(source);
                var update = await mgr.CheckForUpdatesAsync();
                if (update == null)
                    return;

                var parameters = new DialogParameters
                {
                    { "UpdateInfo", update },
                    { "IsInternal", false }
                };

                var result = await dialogService.ShowDialogAsync("UpdateDialog", parameters);
                if (result?.Result == ButtonResult.OK)
                {
                    await mgr.DownloadUpdatesAsync(update);
                    mgr.ApplyUpdatesAndRestart(update);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public async Task CheckInternalUpdateAsync(IDialogService dialogService)
        {
            try
            {
                // 获取 version.json
                string versionJsonUrl = $"{_internalSharePath}version.json";
                RemoteVersionInfo remoteInfo = null;
                try
                {
                    using var httpClient = new HttpClient();
                    string json = await httpClient.GetStringAsync(versionJsonUrl);
                    remoteInfo = JsonSerializer.Deserialize<RemoteVersionInfo>(json);
                }
                catch
                {
                    // 若无法获取 version.json，仍尝试检查更新（仅基于 releases.json）
                }

                // 创建 HTTP 更新源
                var source = new HttpUpdateSource(_internalSharePath);
                var mgr = new UpdateManager(source);

                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null)
                    return;

                var parameters = new DialogParameters
                {
                    { "IsInternal", true },
                    { "RemoteVersion", $"v{remoteInfo?.Version ?? updateInfo.TargetFullRelease.Version.ToString()}" },
                    { "UpdateLog", remoteInfo?.UpdateLog ?? "" },
                    { "Changelog", remoteInfo?.Changelog ?? "" },
                    { "RemoteInfo", remoteInfo }
                };

                var result = await dialogService.ShowDialogAsync("UpdateDialog", parameters);
                if (result?.Result == ButtonResult.OK)
                {
                    await mgr.DownloadUpdatesAsync(updateInfo);
                    mgr.ApplyUpdatesAndRestart(updateInfo);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}