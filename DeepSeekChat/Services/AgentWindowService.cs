using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using DeepSeekChat.Agent;
using DeepSeekChat.ViewModels;
using DeepSeekChat.Views;

namespace DeepSeekChat.Services
{
    /// <summary>
    /// Agent窗口管理服务
    /// </summary>
    public class AgentWindowService : IAgentWindowService
    {
        private readonly Dictionary<string, AgentWindow> _openWindows = new Dictionary<string, AgentWindow>();
        private readonly Dictionary<string, AgentViewModel> _viewModels = new Dictionary<string, AgentViewModel>();
        private readonly IServiceProvider _serviceProvider;

        public AgentWindowService(IServiceProvider serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 显示Agent窗口
        /// </summary>
        public void ShowAgentWindow(string agentName, object agentInstance, string displayName)
        {
            if (string.IsNullOrEmpty(agentName))
                throw new ArgumentException("agentName不能为空");

            if (IsAgentWindowOpen(agentName))
            {
                // 如果窗口已经打开，则激活它
                ActivateWindow(agentName);
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 创建ViewModel
                    var viewModel = CreateViewModel(agentName, displayName, agentInstance);
                    _viewModels[agentName] = viewModel;

                    // 创建窗口
                    var window = new AgentWindow
                    {
                        DataContext = viewModel,
                        Owner = Application.Current.MainWindow
                    };

                    // 保存窗口引用
                    _openWindows[agentName] = window;

                    // 订阅窗口关闭事件
                    window.Closed += (sender, e) =>
                    {
                        OnWindowClosed(agentName);
                    };

                    // 显示窗口
                    window.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建Agent窗口失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        /// <summary>
        /// 关闭Agent窗口
        /// </summary>
        public void CloseAgentWindow(string agentName)
        {
            if (!_openWindows.TryGetValue(agentName, out var window))
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                window.Close();
            });
        }

        /// <summary>
        /// 检查Agent窗口是否已打开
        /// </summary>
        public bool IsAgentWindowOpen(string agentName)
        {
            return _openWindows.ContainsKey(agentName);
        }

        /// <summary>
        /// 激活已存在的窗口
        /// </summary>
        private void ActivateWindow(string agentName)
        {
            if (!_openWindows.TryGetValue(agentName, out var window))
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;

                window.Activate();
                window.Focus();
            });
        }

        /// <summary>
        /// 创建ViewModel
        /// </summary>
        private AgentViewModel CreateViewModel(string agentName, string displayName, object agentInstance)
        {
            if (agentInstance is Agent.Agent agent)
            {
                return new AgentViewModel(agentName, displayName, agent);
            }

            return new AgentViewModel(agentName, displayName, null);
        }

        /// <summary>
        /// 窗口关闭时的清理工作
        /// </summary>
        private void OnWindowClosed(string agentName)
        {
            // 清理ViewModel
            if (_viewModels.TryGetValue(agentName, out var viewModel))
            {
                _viewModels.Remove(agentName);
            }

            // 移除窗口引用
            _openWindows.Remove(agentName);
        }

        /// <summary>
        /// 获取Agent的ViewModel
        /// </summary>
        public AgentViewModel GetViewModel(string agentName)
        {
            return _viewModels.TryGetValue(agentName, out var viewModel) ? viewModel : null;
        }

        /// <summary>
        /// 关闭所有Agent窗口
        /// </summary>
        public void CloseAllWindows()
        {
            var agentNames = new List<string>(_openWindows.Keys);

            foreach (var agentName in agentNames)
            {
                CloseAgentWindow(agentName);
            }

            _openWindows.Clear();
            _viewModels.Clear();
        }
    }
}