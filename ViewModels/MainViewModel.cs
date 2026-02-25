using Avalonia.Threading;
using NetSparkleUpdater;
using OB.Models;
using OB.Tools;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Material.Icons;

namespace OB.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private bool _isMenuExpanded = true;
        private LeftMenuItem _selectedMenuItem = null!;
        private LogUserInfo _logUser = null!;
        private RemoteDBTools _remoteDBTools = null!;
        private readonly IRegionManager _regionManager;
        private IRegionNavigationJournal? _journal;

        public RemoteDBTools RemoteDBTools { get => _remoteDBTools; set => SetProperty(ref _remoteDBTools, value); }
        public LogUserInfo LogUser { get => _logUser; set => SetProperty(ref _logUser, value); }
        public bool IsMenuExpanded { get => _isMenuExpanded; set => SetProperty(ref _isMenuExpanded, value); }

        public LeftMenuItem SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value) && value != null)
                {
                    _ = NavigateAsync(value);
                }
            }
        }

        public ObservableCollection<LeftMenuItem> MenuItems { get; }
        public DelegateCommand ToggleMenuCommand { get; }
        public DelegateCommand<LeftMenuItem> SelectMenuItemCommand { get; }

        public MainViewModel(IRegionManager regionManager)
        {
            this._regionManager = regionManager;
            _logUser = new LogUserInfo();

            // 1. 初始化導航選單
            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "Home" },
                new LeftMenuItem { Icon = MaterialIconKind.Database, Title = "檢測", ViewName = "Detect" },
                new LeftMenuItem { Icon = MaterialIconKind.ChatProcessing, Title = "流程", ViewName = "Process" },
                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "设置", ViewName = "Settings" }
            };

            // 2. 命令綁定
            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(menuItem => _ = NavigateAsync(menuItem));

            // 預設選中第一項
            _selectedMenuItem = MenuItems.FirstOrDefault()!;

            // 【核心修復】：在主界面構造時，啟動異步延遲更新檢查
            // 這樣可以避開登錄窗口（Login Dialog）關閉時的焦點衝突
            _ = CheckForUpdatesDelayedAsync();
        }

        /// <summary>
        /// 進入主界面後，延遲檢查更新。
        /// 這樣可以確保 Login 對話框已經完全關閉，且主窗口已經獲取焦點。
        /// </summary>
        private async Task CheckForUpdatesDelayedAsync()
        {
            // 延遲 3 秒，給用戶一點緩衝時間進入系統
            await Task.Delay(3000);

            if (App.SparkleInstance != null)
            {
                try
                {
                    // 1. 在後台線程檢查是否有更新（Quietly 不會彈出任何 UI）
                    var updateInfo = await App.SparkleInstance.CheckForUpdatesQuietly();

                    // 2. 判斷狀態是否為「有可用更新」
                    if (updateInfo.Status == NetSparkleUpdater.Enums.UpdateStatus.UpdateAvailable && updateInfo.Updates != null)
                    {
                        // 3. 關鍵：必須切換回 Avalonia 的 UI 線程來彈出更新窗口
                        Dispatcher.UIThread.Post(() =>
                        {
                            // 將更新列表傳給 NetSparkle 的原生 UI 進行展示
                            App.SparkleInstance.ShowUpdateNeededUI(new List<AppCastItem>(updateInfo.Updates));
                        });
                    }
                }
                catch (Exception ex)
                {
                    // 打印錯誤日誌，方便調試
                    System.Diagnostics.Debug.WriteLine($"[Update Check Error] {ex.Message}");
                }
            }
        }

        // --- 導航邏輯 ---

        public async Task NavigateAsync(LeftMenuItem menuItem)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.ViewName)) return;

            var parameters = new NavigationParameters
            {
                { "LogUser", LogUser },
                { "dbtools", RemoteDBTools }
            };

            _regionManager.Regions["MainRegion"].RequestNavigate(
                menuItem.ViewName,
                callback => _journal = callback.Context.NavigationService.Journal,
                parameters
            );
        }

        public async Task DefaultNavigateAsync()
        {
            var parameters = new NavigationParameters
            {
                { "LogUser", LogUser },
                { "dbtools", RemoteDBTools }
            };

            _regionManager.Regions["MainRegion"].RequestNavigate(
                "Home",
                callback => _journal = callback.Context.NavigationService.Journal,
                parameters
            );
        }
    }
}