using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DialogHostAvalonia;
using Material.Icons;
using OB.Models;
using OB.Tools;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OB.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private bool _isMenuExpanded = true;
        private LeftMenuItem _selectedMenuItem;
        private LogUserInfo logUser;
        private RemoteDBTools remoteDBTools;
        private readonly IRegionManager regionManager;
        private IRegionNavigationJournal? journal;

        public RemoteDBTools RemoteDBTools { get => remoteDBTools; set => SetProperty(ref remoteDBTools, value); }
        public LogUserInfo LogUser { get => logUser; set => SetProperty(ref logUser, value); }
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
            this.regionManager = regionManager;
            logUser = new LogUserInfo();

            // 1. 初始化選單項目
            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "Home" },
                new LeftMenuItem { Icon = MaterialIconKind.Database, Title = "檢測", ViewName = "Detect" },
                new LeftMenuItem { Icon = MaterialIconKind.ChatProcessing, Title = "流程", ViewName = "Process" },
                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "设置", ViewName = "Settings" }
            };

            // 2. 初始化命令
            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(menuItem => _ = NavigateAsync(menuItem));

            // 預設選中第一項
            _selectedMenuItem = MenuItems.FirstOrDefault();

            // 3. 【核心更新邏輯】
            // 訂閱來自 App 的更新準備就緒事件
            App.UpdateReadyToInstall += OnUpdateReadyToInstall;

            // 檢查是否在進入此頁面時，更新就已經在背景下載好了
            if (App.IsUpdateReady)
            {
                System.Diagnostics.Debug.WriteLine("[Update] 檢測到更新已就緒，準備通知用戶...");
                // 延遲執行，確保 MainWin UI 已穩定載入
                Dispatcher.UIThread.Post(() => OnUpdateReadyToInstall(null, EventArgs.Empty), DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// 當更新包下載完成後觸發
        /// </summary>
        private async void OnUpdateReadyToInstall(object? sender, EventArgs e)
        {
            // 移除訂閱，避免重複彈出對話框
            App.UpdateReadyToInstall -= OnUpdateReadyToInstall;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // 等待 1.5 秒，確保用戶已經看清楚主介面，避免干擾啟動體驗
                await Task.Delay(1500);

                bool shouldInstall = await ShowUpdateConfirmationDialog();
                if (shouldInstall)
                {
                    System.Diagnostics.Debug.WriteLine("[Update] 用戶同意更新，正在執行安裝程序...");
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                    {
                        // 隱藏主視窗，讓安裝程序看起來更順暢
                        lifetime.MainWindow?.Hide();
                    }
                    App.InstallUpdate();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Update] 用戶選擇稍後更新。");
                }
            });
        }

        /// <summary>
        /// 彈出 Material Design 風格的更新確認對話框
        /// </summary>
        private async Task<bool> ShowUpdateConfirmationDialog()
        {
            var content = new StackPanel
            {
                Spacing = 20,
                Margin = new Thickness(24),
                Width = 320,
                Children =
                {
                    new TextBlock {
                        Text = "🚀 發現系統新版本",
                        FontSize = 20,
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = Brushes.Indigo
                    },
                    new TextBlock {
                        Text = "最新的 OB 檢測系統更新已下載完成。\n為了確保功能正常，建議立即安裝。\n現在安裝並自動重啟應用嗎？",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 16,
                        Children =
                        {
                            new Button {
                                Content = "立即更新",
                                Width = 100,
                                Classes = { "Primary" },
                                Command = new DelegateCommand(() => DialogHost.Close("MainDialogHost", true))
                            },
                            new Button {
                                Content = "稍後再說",
                                Width = 100,
                                Theme = Application.Current.FindResource("MaterialFlatButton") as Avalonia.Styling.ControlTheme,
                                Command = new DelegateCommand(() => DialogHost.Close("MainDialogHost", false))
                            }
                        }
                    }
                }
            };

            try
            {
                // 注意：這裡的 "MainDialogHost" 必須與 MainWin.axaml 中的 Identifier 一致
                var result = await DialogHost.Show(content, "MainDialogHost");
                return result is bool b && b;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Update Dialog Error] {ex.Message}");
                return false;
            }
        }

        #region 導航與業務邏輯
        public async Task NavigateAsync(LeftMenuItem menuItem)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.ViewName)) return;

            var parameters = new NavigationParameters
            {
                { "LogUser", LogUser },
                { "dbtools", RemoteDBTools }
            };

            regionManager.Regions["MainRegion"].RequestNavigate(
                menuItem.ViewName,
                callback => journal = callback.Context.NavigationService.Journal,
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

            regionManager.Regions["MainRegion"].RequestNavigate(
                "Home",
                callback => journal = callback.Context.NavigationService.Journal,
                parameters
            );
        }
        #endregion
    }
}