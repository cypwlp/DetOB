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
            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "Home" },
                new LeftMenuItem { Icon = MaterialIconKind.Database, Title = "檢測", ViewName = "Detect" },
                new LeftMenuItem { Icon = MaterialIconKind.ChatProcessing, Title = "流程", ViewName = "Process" },
                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "设置", ViewName = "Settings" }
            };
            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(menuItem => _ = NavigateAsync(menuItem));
            SelectedMenuItem = MenuItems.FirstOrDefault();

            // 訂閱更新事件
            App.UpdateReadyToInstall += OnUpdateReadyToInstall;

            // 【核心修改】：如果進入主界面時已經下載好更新，延遲彈出
            if (App.IsUpdateReady)
            {
                // 延遲 2 秒，確保 MainWin 的 DialogHost 已就緒
                Task.Delay(2000).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => OnUpdateReadyToInstall(null, EventArgs.Empty));
                });
            }
        }

        private async void OnUpdateReadyToInstall(object? sender, EventArgs e)
        {
            // 防止多次觸發
            App.UpdateReadyToInstall -= OnUpdateReadyToInstall;

            // 確保在 UI 線程執行
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var result = await ShowUpdateConfirmationDialog();
                if (result)
                {
                    // 關閉窗口以釋放文件鎖
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                    {
                        lifetime.MainWindow?.Close();
                    }
                    await Task.Delay(500);
                    App.InstallUpdate();
                }
            });
        }

        private async Task<bool> ShowUpdateConfirmationDialog()
        {
            var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null) return false;

            var stackPanel = new StackPanel
            {
                Spacing = 20,
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock
                    {
                        Text = "✨ 新版本已準備就緒",
                        FontSize = 18,
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "更新包已下載完成，是否立即安裝並重啟？",
                        FontSize = 14,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 15,
                        Children =
                        {
                            new Button
                            {
                                Content = "立即安裝",
                                Classes = { "Primary" },
                                Width = 100,
                                Command = new DelegateCommand(() => DialogHost.Close("MainDialogHost", true))
                            },
                            new Button
                            {
                                Content = "稍後",
                                Width = 100,
                                Command = new DelegateCommand(() => DialogHost.Close("MainDialogHost", false))
                            }
                        }
                    }
                }
            };

            try
            {
                var result = await DialogHost.Show(stackPanel, "MainDialogHost");
                return result is bool boolResult && boolResult;
            }
            catch
            {
                // 如果 DialogHost 報錯，回退到普通視窗提示（備案）
                return false;
            }
        }

        #region 导航实现
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