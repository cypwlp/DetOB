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
        #region 字段
        private bool _isMenuExpanded = true;
        private LeftMenuItem _selectedMenuItem;
        private LogUserInfo logUser;
        private RemoteDBTools remoteDBTools;
        private readonly IRegionManager regionManager;
        private IRegionNavigationJournal? journal;
        #endregion

        #region 属性
        public RemoteDBTools RemoteDBTools
        {
            get => remoteDBTools;
            set => SetProperty(ref remoteDBTools, value);
        }

        public LogUserInfo LogUser
        {
            get => logUser;
            set => SetProperty(ref logUser, value);
        }

        public bool IsMenuExpanded
        {
            get => _isMenuExpanded;
            set => SetProperty(ref _isMenuExpanded, value);
        }

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
        #endregion

        public MainViewModel(IRegionManager regionManager)
        {
            this.regionManager = regionManager;
            logUser = new LogUserInfo();

            // 初始化菜單
            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "Home" },
                new LeftMenuItem { Icon = MaterialIconKind.Database, Title = "檢測", ViewName = "Detect" },
                new LeftMenuItem { Icon = MaterialIconKind.ChatProcessing, Title = "流程", ViewName = "Process" },
                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "设置", ViewName = "Settings" }
            };

            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(menuItem => _ = NavigateAsync(menuItem));

            // 設置默認選中項
            _selectedMenuItem = MenuItems.FirstOrDefault();

            // ==========================================
            // 【核心修復】：註冊更新事件
            // ==========================================
            App.UpdateReadyToInstall += OnUpdateReadyToInstall;

            // 如果在登錄過程中更新已經下載好了，直接彈出
            if (App.IsUpdateReady)
            {
                // 稍微延遲，確保主窗口和 DialogHost 已經渲染完成
                Dispatcher.UIThread.Post(() => OnUpdateReadyToInstall(null, EventArgs.Empty), DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// 當更新包下載完成時觸發
        /// </summary>
        private async void OnUpdateReadyToInstall(object? sender, EventArgs e)
        {
            // 防止重複彈窗
            App.UpdateReadyToInstall -= OnUpdateReadyToInstall;

            // 確保在 UI 線程執行
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // 再給 UI 一點緩衝時間（1.5秒），確保 DialogHost 已加載
                await Task.Delay(1500);

                bool shouldInstall = await ShowUpdateConfirmationDialog();

                if (shouldInstall)
                {
                    // 執行安裝並重啟
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                    {
                        // 隱藏主窗口，避免安裝時文件佔用或干擾
                        lifetime.MainWindow?.Hide();
                    }

                    // 調用 App.cs 裡的靜態安裝方法
                    App.InstallUpdate();
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
                Width = 300,
                Children =
                {
                    new TextBlock
                    {
                        Text = "🚀 發現新版本",
                        FontSize = 20,
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = Brushes.Indigo
                    },
                    new TextBlock
                    {
                        Text = "OB 系統更新包已下載完成。\n現在安裝並自動重啟應用嗎？",
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
                            new Button
                            {
                                Content = "立即更新",
                                Width = 100,
                                Classes = { "Primary" },
                                Command = new DelegateCommand(() => DialogHost.Close("MainDialogHost", true))
                            },
                            new Button
                            {
                                Content = "稍後",
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
                // Identifier 必須對應 MainWin.axaml 中的 <DialogHost Identifier="MainDialogHost">
                var result = await DialogHost.Show(content, "MainDialogHost");
                return result is bool b && b;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Update UI Error] 對話框顯示失敗: {ex.Message}");
                return false;
            }
        }

        #region 导航逻辑
        public async Task NavigateAsync(LeftMenuItem menuItem)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.ViewName))
                return;

            var parameters = new NavigationParameters();
            if (LogUser != null)
            {
                parameters.Add("LogUser", LogUser);
                parameters.Add("dbtools", RemoteDBTools);
            }

            regionManager.Regions["MainRegion"].RequestNavigate(
                menuItem.ViewName,
                callback => journal = callback.Context.NavigationService.Journal,
                parameters);
        }

        public async Task DefaultNavigateAsync()
        {
            var parameters = new NavigationParameters();
            if (LogUser != null)
            {
                parameters.Add("LogUser", LogUser);
                parameters.Add("dbtools", RemoteDBTools);
            }

            regionManager.Regions["MainRegion"].RequestNavigate(
                "Home",
                callback => journal = callback.Context.NavigationService.Journal,
                parameters);
        }
        #endregion
    }
}