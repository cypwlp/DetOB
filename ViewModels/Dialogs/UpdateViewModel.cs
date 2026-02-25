using Prism.Dialogs;
using Prism.Mvvm;
using Prism.Commands;
using NetSparkleUpdater;

namespace OB.ViewModels.Dialogs
{
    public class UpdateViewModel : BindableBase, IDialogAware
    {
        private AppCastItem? _updateInfo;
        public AppCastItem? UpdateInfo
        {
            get => _updateInfo;
            set
            {
                if (SetProperty(ref _updateInfo, value))
                    RaisePropertyChanged(nameof(NewVersion));
            }
        }

        public string NewVersion => UpdateInfo?.Version ?? "未知版本";

        public DialogCloseListener RequestClose { get; private set; }

        public DelegateCommand UpdateCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public UpdateViewModel()
        {
            UpdateCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.OK));
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.TryGetValue<AppCastItem>("UpdateInfo", out var info))
            {
                UpdateInfo = info;
            }
        }

        public string Title => "發現新版本";
    }
}