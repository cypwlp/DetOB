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
            set { if (SetProperty(ref _selectedMenuItem, value) && value != null) _ = NavigateAsync(value); }
        }
        public ObservableCollection<LeftMenuItem> MenuItems { get; }
        public DelegateCommand ToggleMenuCommand { get; }
        public DelegateCommand<LeftMenuItem> SelectMenuItemCommand { get; }

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
            _selectedMenuItem = MenuItems.FirstOrDefault();

            // ==========================================
            // 【核心修復邏輯】
            // ==========================================
            // 1. 監聽未來的更新完成事件
            App.UpdateReadyToInstall += OnUpdateReadyToInstall;

            // 2. 檢查當前是否已經下載完成（處理登錄期間就下好的情況）
            if (App.IsUpdateReady)
            {
                System.Diagnostics.Debug.WriteLine("[Update] 檢測到更新已在登錄前就緒，準備彈窗...");
                Dispatcher.UIThread.Post(() => OnUpdateReadyToInstall(null, EventArgs.Empty), DispatcherPriority.Background);
            }
        }

        private async void OnUpdateReadyToInstall(object? sender, EventArgs e)
        {
            // 避免重複訂閱或多次彈窗
            App.UpdateReadyToInstall -= OnUpdateReadyToInstall;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // 等待 UI 完全穩定
                await Task.Delay(1000);

                bool shouldInstall = await ShowUpdateConfirmationDialog();
                if (shouldInstall)
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                    {
                        lifetime.MainWindow?.Hide();
                    }
                    App.InstallUpdate();
                }
            });
        }

        private async Task<bool> ShowUpdateConfirmationDialog()
        {
            var content = new StackPanel
            {
                Spacing = 20,
                Margin = new Thickness(24),
                Width = 320,
                Children =
                {
                    new TextBlock { Text = "🚀 發現新版本", FontSize = 20, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.Indigo },
                    new TextBlock { Text = "OB 系統更新已下載完成。\n現在安裝並自動重啟應用嗎？", HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 16,
                        Children =
                        {
                            new Button { Content = "立即更新", Width = 100, Classes = { "Primary" }, Command = new DelegateCommand(() => DialogHost.Close("MainDialogHost", true)) },
                            new Button { Content = "稍後", Width = 100, Theme = Application.Current.FindResource("MaterialFlatButton") as Avalonia.Styling.ControlTheme, Command = new DelegateCommand(() => DialogHost.Close("MainDialogHost", false)) }
                        }
                    }
                }
            };

            try
            {
                var result = await DialogHost.Show(content, "MainDialogHost");
                return result is bool b && b;
            }
            catch { return false; }
        }

        #region 導航邏輯
        public async Task NavigateAsync(LeftMenuItem menuItem)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.ViewName)) return;
            var parameters = new NavigationParameters { { "LogUser", LogUser }, { "dbtools", RemoteDBTools } };
            regionManager.Regions["MainRegion"].RequestNavigate(menuItem.ViewName, callback => journal = callback.Context.NavigationService.Journal, parameters);
        }

        public async Task DefaultNavigateAsync()
        {
            var parameters = new NavigationParameters { { "LogUser", LogUser }, { "dbtools", RemoteDBTools } };
            regionManager.Regions["MainRegion"].RequestNavigate("Home", callback => journal = callback.Context.NavigationService.Journal, parameters);
        }
        #endregion
    }
}