using OB.Tools;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;

namespace OB.ViewModels.Dialogs
{
    public class LoginViewModel : BindableBase, IDialogAware
    {
        private readonly IDialogService _dialogService;
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

        public LoginViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            // 从设置中加载默认用户名
            var settings = OB.Default;
            UserName = settings.mUsername;
        }

        public DialogCloseListener RequestClose { get; private set; }

        public string Title => "用户登录";

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters) { }

        private bool CanLogin() => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

        public DelegateCommand LoginCommand => new DelegateCommand(async () => await LoginAsync(), CanLogin)
            .ObservesProperty(() => UserName)
            .ObservesProperty(() => Password);

        private async Task LoginAsync()
        {
            var settings = OB.Default;
            string server = settings.mServer;
            string database = settings.mDatabase;

            // 根据服务器地址判断本地/远程（参照 VB.NET 逻辑）
            bool isLocal = server.StartsWith("192.168.") || server.StartsWith("10.");

            var dbtools = new DBTools(server, isLocal);
            bool success = await dbtools.InitializeAsync(UserName, Password, database);

            if (success)
            {
                var parameters = new DialogParameters();
                parameters.Add("dbtools", dbtools);
                RequestClose.Invoke(parameters, ButtonResult.OK);
            }
            else
            {
                // 登录失败，可在此弹出错误提示（需要消息对话框服务）
                // 这里简单处理：不清空密码，让用户重试
            }
        }

        public DelegateCommand CancelCommand => new DelegateCommand(Cancel);
        private void Cancel() => RequestClose.Invoke(null, ButtonResult.Cancel);

        public DelegateCommand ShowSetCommand => new DelegateCommand(ShowSet);
        private void ShowSet()
        {
            _dialogService.ShowDialog("SetUrl", null, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    // 更新用户名输入框为设置中保存的默认用户名
                    UserName = OB.Default.mUsername;
                }
            });
        }
    }
}