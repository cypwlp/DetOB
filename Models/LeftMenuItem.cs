using Material.Icons;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OB.Models
{
    public class LeftMenuItem:BindableBase
    {
        private MaterialIconKind _icon;
        private string? _title;
        private string? _viewName;

        public MaterialIconKind Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string ViewName
        {
            get => _viewName;
            set => SetProperty(ref _viewName, value);
        }
    }
}
