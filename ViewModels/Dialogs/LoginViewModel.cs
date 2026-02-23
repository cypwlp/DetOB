using OB.Models;
using OB.Tools;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using RemoteService;
using System;
using System.Threading.Tasks;

namespace OB.ViewModels.Dialogs
{
    public class LoginViewModel : BindableBase, IDialogAware
    {
        #region 字段
        private readonly IDialogService _dialogService;
        private string _userName;
        private string _password;
        private string _server;
        private int _selectedIndex;
        private LogUserInfo logUser;
        private RemoteService.LoginInfo logInfo;
        #endregion


        #region 屬性

        public LogUserInfo LogUser
        {  get => logUser; 
           set => logUser = value;    
        }

        public RemoteService.LoginInfo LogInfo
        {
            get => logInfo;
            set => logInfo = value;
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                SetProperty(ref _selectedIndex, value);
                RaisePropertyChanged(nameof(CurrentView));  // Raise to update content
            }
        }

        public string CurrentView => SelectedIndex == 0 ? "Login" : "Settings";  // Used for template selection

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

        public string Server
        {
            get => _server;
            set => SetProperty(ref _server, value);
        }
        #endregion

        public LoginViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            logUser = new LogUserInfo();
            // 从设置中加载默认用户名和服务器

            var settings = OB.Default;
            UserName = settings.mUsername;
            Server = settings.mServer;
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
            var dbtools = new RemoteDBTools("http://www.Topmix.net/dataservice/GetData.asmx", "210.5.181.130", "TopmixData", UserName, Password);
            bool success = await dbtools.InitializeAsync(UserName, Password, "TopmixData");
            if (success)
            {
                var parameters = new DialogParameters();
                //OB.Default.mUsername = UserName;
                //OB.Default.mPassword = Password;
                LogUser.UserName = UserName;
                LogUser.Password = Password;
                //LogUser.FullName = LogInfo.FullName;
                //LogInfo = await dbtools.GetLoginInfoAsync();
                LogInfo = await dbtools.GetLoginInfoAsync();
                LogUser.FullName = LogInfo?.FullName ?? string.Empty;
                parameters.Add("dbtools", dbtools);
                parameters.Add("LogUser", LogUser);
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
            SelectedIndex = 1;
        }

        //public DelegateCommand SaveSettingsCommand => new DelegateCommand(SaveSettings);

        //private void SaveSettings()
        //{
        //    var settings = OB.Default;
        //    settings.mServer = Server;
        //    settings.mUsername = UserName;
        //    // 假设有Save方法来持久化设置
        //    settings.Save(); // 如果OB.Default有Save方法，否则实现保存逻辑
        //    SelectedIndex = 0; // 返回登录界面
        //}
    }
}