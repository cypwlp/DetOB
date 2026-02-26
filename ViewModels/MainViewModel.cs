using Avalonia.Controls;
using Material.Icons;
using OB.Models;
using OB.Tools;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Ioc;
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
        private LeftMenuItem? _selectedMenuItem;
        private LogUserInfo _logUser;
        private RemoteDBTools? _remoteDBTools;
        private readonly IRegionManager _regionManager;
        private IRegionNavigationJournal? _journal;

        // --- 公開屬性 ---
        public RemoteDBTools RemoteDBTools
        {
            get => _remoteDBTools;
            set => SetProperty(ref _remoteDBTools, value);
        }

        public LogUserInfo LogUser
        {
            get => _logUser;
            set => SetProperty(ref _logUser, value);
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
                    if (!string.IsNullOrEmpty(value.ViewName))
                    {
                        _ = NavigateAsync(value);
                    }
                }
            }
        }

        public ObservableCollection<LeftMenuItem> MenuItems { get; set; }
        public DelegateCommand ToggleMenuCommand { get; }
        public DelegateCommand<LeftMenuItem> SelectMenuItemCommand { get; }


        public DelegateCommand ShowAboutCommand { get; set; }
        public MainViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;
            _logUser = new LogUserInfo();
            LoadMenu();
            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(async menuItem =>
            {
                if (menuItem != null && !string.IsNullOrEmpty(menuItem.ViewName))
                {
                    await NavigateAsync(menuItem);
                }
            });
            ShowAboutCommand = new DelegateCommand(async () =>
            {
                var parameters = new DialogParameters();
                var dialogService = Prism.Ioc.ContainerLocator.Container.Resolve<Prism.Dialogs.IDialogService>();
                await dialogService.ShowDialogAsync("About", parameters);
            });
            _selectedMenuItem = MenuItems.FirstOrDefault();
        }

        private void LoadMenu()
        {
            MenuItems =
            [
                new() { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "Home" },
                new() {
                    Icon = MaterialIconKind.Database,
                    Title = "檢測系統",
                    SubItems =
                    [
                        new() { Icon = MaterialIconKind.Magnify, Title = "實時檢測", ViewName = "Detect" },
                        new() { Icon = MaterialIconKind.History, Title = "歷史數據", ViewName = "History" }
                    ]
                },

                new LeftMenuItem
                {
                    Icon = MaterialIconKind.ChatProcessing,
                    Title = "流程管理",
                    SubItems =
                    [
                        new() { Icon = MaterialIconKind.FileTree, Title = "當前流程", ViewName = "Process" },
                        new() { Icon = MaterialIconKind.FormatListBulleted, Title = "工單列表", ViewName = "WorkOrder" }
                    ]
                },
                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "設置",
                SubItems = 
                    [
                        new() { Icon = MaterialIconKind.Cog, Title = "檢測設置", ViewName = "Settings" },
                        new() { Icon = MaterialIconKind.Account, Title = "個人中心", ViewName = "Personal" }
                    ]

                }
            ];
        }

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
                callback =>
                {
                    if (callback.Success == true)
                    {
                        _journal = callback.Context.NavigationService.Journal;
                    }
                    else
                    {

                        System.Diagnostics.Debug.WriteLine($"導航至 {menuItem.ViewName} 失敗: {callback.Exception?.Message}");
                    }
                },
                parameters
            );
        }

        public async Task OnNavigatedToAsync(NavigationContext navigationContext)
        {
    
        }
        public async Task DefaultNavigateAsync()
        {
            var homeItem = MenuItems.FirstOrDefault(x => x.ViewName == "Home");
            if (homeItem != null)
            {
                await NavigateAsync(homeItem);
            }
        }
    }
}