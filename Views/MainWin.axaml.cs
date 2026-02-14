using Avalonia.Controls;
using Prism.Dialogs;           // 关键修改：将 using Prism.Services.Dialogs; 改为 Prism.Dialogs
using System;

namespace OB.Views
{
    public partial class MainWin : Window
    {
        private readonly IDialogService _dialogService;

        public MainWin(IDialogService dialogService)
        {
            InitializeComponent();
            _dialogService = dialogService;
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            // 使用非泛型方法显示对话框，需要传递注册的名称（例如 "Login"）
            // 假设在 App 中注册对话框时使用了 "Login" 作为名称
            _dialogService.ShowDialog("Login", new DialogParameters(), result =>
            {
                if (result.Result != ButtonResult.OK)
                {
                    // 登录取消或失败，关闭主窗口
                    this.Close();
                }
                else
                {
                    // 登录成功，如果需要可恢复窗口显示
                    // this.WindowState = Avalonia.Controls.WindowState.Normal;
                }
            });
        }
    }
}