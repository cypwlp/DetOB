using Avalonia.Controls;
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
using Material.Icons;

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
            _selectedMenuItem = MenuItems.FirstOrDefault();

            // 注意：這裡不再需要手動訂閱更新事件，NetSparkle 會自動彈窗。
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
    }
}