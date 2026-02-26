using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Material.Icons;
using Material.Icons.Avalonia;

namespace OB.Views
{
    public partial class MainWin : Window
    {
        public MainWin()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 實現頂部欄拖拽窗口的功能
        /// </summary>
        private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 按下左鍵時拖動
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                // 如果是雙擊，則切換最大化狀態
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                }
                else
                {
                    this.BeginMoveDrag(e);
                }
            }
        }

        /// <summary>
        /// 最小化按鈕
        /// </summary>
        private void btnMin_Click(object? sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化/還原按鈕
        /// </summary>
        private void btnMax_Click(object? sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        /// <summary>
        /// 關閉按鈕
        /// </summary>
        private void BtnClose_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 切換窗口最大化狀態並更新圖標
        /// </summary>
        private void ToggleMaximize()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                if (this.FindControl<MaterialIcon>("MaxIcon") is MaterialIcon icon)
                    icon.Kind = MaterialIconKind.WindowMaximize;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                if (this.FindControl<MaterialIcon>("MaxIcon") is MaterialIcon icon)
                    icon.Kind = MaterialIconKind.WindowRestore;
            }
        }
    }
}