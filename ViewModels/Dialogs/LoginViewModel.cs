using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using System;

namespace OB.ViewModels.Dialogs
{
    public class LoginViewModel : BindableBase, IDialogAware
    {
        private string _userName;
        private string _password;

        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public DialogCloseListener RequestClose { get; private set; }

        public string Title => "用户登录";

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters) { }

        private bool CanLogin() => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

        public DelegateCommand LoginCommand => new DelegateCommand(Login, CanLogin)
            .ObservesProperty(() => UserName)
            .ObservesProperty(() => Password);

        private void Login()
        {
            // 这里替换为实际的验证逻辑（如调用数据库）
            if (UserName == "admin" && Password == "admin")
            {
                var parameters = new DialogParameters();
                // 可以返回用户信息等数据
                RequestClose.Invoke(parameters, ButtonResult.OK);
            }
            else
            {
                // 可在此显示错误提示，例如通过对话框服务或弹窗
                // 简单起见，这里不处理，仅演示
            }
        }

        public DelegateCommand CancelCommand => new DelegateCommand(Cancel);

        private void Cancel()
        {
            RequestClose.Invoke(null, ButtonResult.Cancel);
        }
    }
}