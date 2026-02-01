using DeepSeekChat.Agent;
using DeepSeekChat.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace DeepSeekChat.ViewModels
{
    public class AgentViewModel : ViewModelBase
    {
        private readonly Agent.Agent _agent;
        private readonly string _agentType;
        private string _status = "初始化中...";
        private Brush _statusColor = Brushes.Gray;
        private bool _isConnected = false;
        private string _connectionStatus = "未连接";
        private string _inputText = string.Empty;
        private string _busyMessage = "处理中...";

        public string WindowTitle => $"{AgentName} - Agent控制台";
        public string AgentName { get; }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        public ObservableCollection<AgentMessageItem> Messages { get; }
            = new ObservableCollection<AgentMessageItem>();

        public ICommand SendMessageCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RestartCommand { get; }
        public ICommand ClearLogCommand { get; }

        public AgentViewModel(string agentType, string displayName, Agent.Agent agent)
        {
            _agentType = agentType;
            AgentName = displayName;
            _agent = agent;

            // 初始化命令
            SendMessageCommand = new RelayCommandEx(
                execute: _ => SendMessageAsync(),
                canExecute: _ => !IsBusy && IsConnected && !string.IsNullOrWhiteSpace(InputText)
            );

            StopCommand = new RelayCommandEx(
                execute: _ => StopAsync(),
                canExecute: _ => IsConnected && !IsBusy
            );

            RestartCommand = new RelayCommandEx(
                execute: _ => RestartAsync(),
                canExecute: _ => !IsConnected && !IsBusy
            );

            ClearLogCommand = new RelayCommandEx(
                execute: _ => ClearMessages(),
                canExecute: _ => Messages.Any()
            );

            // 注册命令以便刷新
            // SendMessageCommand
            var sendMessageCommand = new RelayCommand(
                () => SendMessageAsync(),
                () => !IsBusy && IsConnected && !string.IsNullOrWhiteSpace(InputText));
            RegisterCommand("SendMessageCommand", sendMessageCommand);

            // StopCommand
            var stopCommand = new RelayCommand(
                () => StopAsync(),
                () => IsConnected && !IsBusy);
            RegisterCommand("StopCommand", stopCommand);

            // RestartCommand
            var restartCommand = new RelayCommand(
                () => RestartAsync(),
                () => !IsConnected && !IsBusy);
            RegisterCommand("RestartCommand", restartCommand);

            // ClearLogCommand
            var clearLogCommand = new RelayCommand(
                () => ClearMessages(),
                () => Messages.Any());
            RegisterCommand("ClearLogCommand", clearLogCommand);

            // 订阅Agent的消息事件
            //if (_agent is DesignAgent designAgent)
            //{
                _agent.OnMessageReceived += OnAgentMessageReceived;
                _agent.OnMessageSent += OnAgentMessageSent;
            //}
        }

        public override async Task InitializeAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                IsBusy = true;
                BusyMessage = "正在启动Agent...";

                if (_agent != null)
                {
                    //await _agent.StartAsync();
                    IsConnected = true;
                    ConnectionStatus = "已连接";
                    Status = "运行中";
                    StatusColor = Brushes.Green;

                    AddMessage("系统", "Agent已成功启动", Colors.LightGreen);

                    // 这里可以订阅Agent的消息总线
                    // SubscribeToAgentMessages();
                }
                else
                {
                    Status = "未连接";
                    StatusColor = Brushes.Red;
                    AddMessage("系统", "Agent实例为空，无法启动", Colors.LightPink);
                }

                RefreshAllCommands();
            }, "初始化Agent失败");
        }

        // Agent消息接收事件处理
        private void OnAgentMessageReceived(object sender, AgentMessage message)
        {
            // 只显示发送给当前Agent的消息（排除自己发送的消息）
            if (message.Recipient == _agentType && message.Sender != _agentType)
            {
                var senderName = message.Sender == "User" ? "用户" : message.Sender;
                AddMessage(senderName, $"[接收] {message.Content}", Colors.LightYellow, Colors.DarkOrange);
            }
        }

        // Agent消息发送事件处理
        private void OnAgentMessageSent(object sender, AgentMessage message)
        {
            var recipientName = message.Recipient == "User" ? "用户" : message.Recipient;
            AddMessage(AgentName, $"[发送到 {recipientName}] {message.Content}", Colors.LightCyan, Colors.DarkCyan);
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText))
                return;

            var message = InputText.Trim();
            InputText = string.Empty;

            AddMessage("用户", message, Colors.White, Colors.Blue);

            await ExecuteSafeAsync(async () =>
            {
                if (_agent != null)
                {
                    // 发送消息给Agent
                    var response = await _agent.ProcessAsync(new AgentMessage
                    {
                        Sender = "User",
                        Recipient = _agentType,
                        Content = message,
                        Type = AgentMessageType.TaskRequest
                    });

                    if (response.Success)
                    {
                        // 响应消息已经通过OnMessageSent事件添加，这里不需要重复添加
                    }
                    else
                    {
                        AddMessage("系统", $"处理失败: {response.Content}", Colors.LightPink, Colors.Red);
                    }
                }
                else
                {
                    AddMessage("系统", "Agent未连接，无法发送消息", Colors.LightPink, Colors.Red);
                }
            }, "发送消息失败");
        }

        public async Task StopAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                IsBusy = true;
                BusyMessage = "正在停止Agent...";

                if (_agent != null)
                {
                    // 取消订阅事件
                    if (_agent is DesignAgent designAgent)
                    {
                        designAgent.OnMessageReceived -= OnAgentMessageReceived;
                        designAgent.OnMessageSent -= OnAgentMessageSent;
                    }

                    await _agent.StopAsync();
                    IsConnected = false;
                    ConnectionStatus = "已断开";
                    Status = "已停止";
                    StatusColor = Brushes.Red;

                    AddMessage("系统", "Agent已停止", Colors.LightYellow);
                }

                RefreshAllCommands();
            }, "停止Agent失败");
        }

        public async Task RestartAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                IsBusy = true;
                BusyMessage = "正在重启Agent...";

                if (_agent != null)
                {
                    // 重新订阅事件
                    if (_agent is DesignAgent designAgent)
                    {
                        designAgent.OnMessageReceived -= OnAgentMessageReceived;
                        designAgent.OnMessageSent -= OnAgentMessageSent;
                        designAgent.OnMessageReceived += OnAgentMessageReceived;
                        designAgent.OnMessageSent += OnAgentMessageSent;
                    }

                    await _agent.StartAsync();
                    IsConnected = true;
                    ConnectionStatus = "已连接";
                    Status = "运行中";
                    StatusColor = Brushes.Green;

                    AddMessage("系统", "Agent已重启", Colors.LightGreen);
                }

                RefreshAllCommands();
            }, "重启Agent失败");
        }

        private void AddMessage(string sender, string content, Color bgColor, Color? senderColor = null)
        {
            var message = new AgentMessageItem
            {
                Sender = sender,
                Content = content,
                Background = new SolidColorBrush(bgColor),
                SenderColor = new SolidColorBrush(senderColor ?? Colors.Black),
                Timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            Messages.Add(message);

            // 如果消息太多，自动清理
            if (Messages.Count > 100)
            {
                Messages.RemoveAt(0);
            }
        }

        private void ClearMessages()
        {
            Messages.Clear();
            AddMessage("系统", "日志已清除", Colors.LightGray);
            RefreshAllCommands();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _agent != null)
            {
                // 取消订阅事件
                if (_agent is DesignAgent designAgent)
                {
                    designAgent.OnMessageReceived -= OnAgentMessageReceived;
                    designAgent.OnMessageSent -= OnAgentMessageSent;
                }

                _agent.StopAsync().ConfigureAwait(false);
            }

            base.Dispose(disposing);
        }
    }
}