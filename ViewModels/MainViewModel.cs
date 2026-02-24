using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;
using DialogHostAvalonia;  // 確保 using 已存在
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

                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "设置", ViewName = "Settings" }
                
            };
            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(menuItem => _ = NavigateAsync(menuItem));
            SelectedMenuItem = MenuItems.FirstOrDefault();

            // 订阅更新事件
            App.UpdateReadyToInstall += OnUpdateReadyToInstall;
            if (App.IsUpdateReady)
            {
                // 如果更新已经在后台下载完成，立即提示（使用 UI 线程）
                Dispatcher.UIThread.Post(() => OnUpdateReadyToInstall(null, EventArgs.Empty));
            }
        }

        private async void OnUpdateReadyToInstall(object? sender, EventArgs e)
        {
            // 防止多次触发
            App.UpdateReadyToInstall -= OnUpdateReadyToInstall;
            var result = await ShowUpdateConfirmationDialog();
            if (result)
            {
                App.InstallUpdate();
            }
        }

        private async Task<bool> ShowUpdateConfirmationDialog()
        {
            var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null) return false;

            // 构建确认对话框内容
            var stackPanel = new StackPanel
            {
                Spacing = 20,
                Children =
                {
                    new TextBlock
                    {
                        Text = "新版本已下载，是否立即安装？",
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 10,
                        Children =
                        {
                            new Button
                            {
                                Content = "立即安装",
                                Classes = { "Primary" }, // 若 Material 主题中有 Primary 样式则生效
                                Command = new DelegateCommand(() => DialogHostAvalonia.DialogHost.Close("MainDialogHost", true)),  // 使用完全限定名称避免冲突
                                CommandParameter = true
                            },
                            new Button
                            {
                                Content = "稍后",
                                Command = new DelegateCommand(() => DialogHostAvalonia.DialogHost.Close("MainDialogHost", false)),  // 使用完全限定名称避免冲突
                                CommandParameter = false
                            }
                        }
                    }
                }
            };

            // 使用主窗口中的 DialogHost（Identifier="MainDialogHost"）
            var result = await DialogHost.Show(stackPanel, "MainDialogHost");
            return result is bool boolResult && boolResult;
        }

        #region 导航实现
        public async Task NavigateAsync(LeftMenuItem menuItem)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.ViewName))
                return;

            var parameters = new NavigationParameters();
            if (menuItem.ViewName == "Home" && LogUser != null)
            {
                parameters.Add("LogUser", LogUser);
                parameters.Add("dbtools", RemoteDBTools);
            }
            if (menuItem.ViewName == "Settings" && LogUser != null)
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