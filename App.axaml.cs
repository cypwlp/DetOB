using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using OB.Tools;
using OB.ViewModels;
using OB.ViewModels.Dialogs;
using OB.Views;
using OB.Views.Dialogs;
using Prism.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
using System;

namespace OB
{
    public partial class App : PrismApplication
    {
        protected override AvaloniaObject CreateShell() => null;

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册登录对话框
            containerRegistry.RegisterDialog<Login, LoginViewModel>();
            // 注册主窗口导航
            containerRegistry.RegisterForNavigation<MainWin, MainViewModel>();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // 创建临时隐藏窗口作为登录对话框的所有者
            var splashWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SystemDecorations = SystemDecorations.None,
                Background = new SolidColorBrush(Colors.White),
                ShowInTaskbar = false,
                ShowActivated = false,
                IsHitTestVisible = false
            };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            var dialogService = Container.Resolve<IDialogService>();

            dialogService.ShowDialog("Login", null, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    // 登录成功，获取传递的 DBTools 实例
                    if (result.Parameters.TryGetValue<DBTools>("dbtools", out var dbtools))
                    {
                        var mainWin = Container.Resolve<MainWin>();
                        if (mainWin.DataContext is MainViewModel vm)
                        {
                           // vm.DBTools = dbtools; // 需要在 MainViewModel 中定义该属性
                        }
                        mainWin.Show();
                        desktopLifetime.MainWindow = mainWin;
                        splashWindow.Close();
                    }
                    else
                    {
                        // 未获取到 dbtools，异常情况，直接退出
                        splashWindow.Close();
                        desktopLifetime.Shutdown();
                    }
                }
                else
                {
                    // 用户取消或关闭登录对话框，退出应用
                    splashWindow.Close();
                    desktopLifetime.Shutdown();
                }
            });
        }
    }
}