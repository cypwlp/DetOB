using Avalonia;
using Material.Icons;
using OB.Models;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Linq;

namespace OB.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private bool _isMenuExpanded = true;
        private LeftMenuItem _selectedMenuItem;
        public static MainViewModel DesignInstance => new MainViewModel();

        public bool IsMenuExpanded
        {
            get => _isMenuExpanded;
            set => SetProperty(ref _isMenuExpanded, value);
        }

        public LeftMenuItem SelectedMenuItem
        {
            get => _selectedMenuItem;
            set => SetProperty(ref _selectedMenuItem, value);
        }

        public ObservableCollection<LeftMenuItem> MenuItems { get; }
        public DelegateCommand ToggleMenuCommand { get; }
        public DelegateCommand<LeftMenuItem> SelectMenuItemCommand { get; }

        public MainViewModel()
        {
            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem { Icon = MaterialIconKind.Home, Title = "首頁", ViewName = "Home" },
                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "設置", ViewName = "Set" }
            };

            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(SelectMenuItem);

            SelectedMenuItem = MenuItems.FirstOrDefault();
        }

        private void SelectMenuItem(LeftMenuItem menuItem)
        {
            if (menuItem != null)
            {
                SelectedMenuItem = menuItem;
                // 這裡可以添加導航邏輯
            }
        }
    }
  
    
}