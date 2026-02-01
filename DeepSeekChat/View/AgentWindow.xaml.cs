using System.Windows;
using DeepSeekChat.ViewModels;

namespace DeepSeekChat.Views
{
    public partial class AgentWindow : Window
    {
        public AgentWindow()
        {
            InitializeComponent();
            Loaded += AgentWindow_Loaded;
        }

        private void AgentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AgentViewModel viewModel)
            {
                // 窗口加载完成后初始化ViewModel
                viewModel.InitializeAsync().ConfigureAwait(false);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is AgentViewModel viewModel)
            {
                // 询问用户是否确认关闭
                var result = MessageBox.Show(
                    "确认关闭Agent窗口吗？这会停止Agent的运行。",
                    "确认关闭",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // 停止Agent
                viewModel.StopAsync().ConfigureAwait(false);
            }
        }
    }
}