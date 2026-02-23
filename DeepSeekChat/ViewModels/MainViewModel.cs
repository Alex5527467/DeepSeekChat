using DeepSeekChat.Agent;
using DeepSeekChat.Models;
using DeepSeekChat.Services;
using DeepSeekChat.Views;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeepSeekChat.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly ToolService _toolService;
        private readonly ToolApiService _toolApiService;
        private readonly InMemoryMessageBus _messageBus;
        private readonly List<ChatMessage> _conversationHistory = new();

        private string _inputText;
        private bool _isLoading;
        private ObservableCollection<MessageItem> _messages;
        private Dictionary<string, BaseAgent> _activeAgents = new();

        private readonly IConfiguration _configuration;
        private SettingsViewModel _settingsViewModel;
        private Window _settingsWindow;


        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged();

                // 重要：通知命令重新评估状态
                ((RelayCommand)SendCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();

                // 重要：通知命令重新评估状态
                ((RelayCommand)SendCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ObservableCollection<MessageItem> Messages
        {
            get => _messages;
            set
            {
                _messages = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<FileSystemItem> _projectFiles;
        private string _projectPath;

        public ObservableCollection<FileSystemItem> ProjectFiles
        {
            get => _projectFiles;
            set => SetProperty(ref _projectFiles, value);
        }

        public string ProjectPath
        {
            get => _projectPath;
            set => SetProperty(ref _projectPath, value);
        }

        private readonly Dictionary<string, RequirementSessionInfo> _requirementSessions = new();

        public ICommand SendCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand StartDesignAgentCommand { get; }
        public ICommand StartCodingAgentCommand { get; }
        public ICommand StartTaskCreateAgentCommand { get; }

        public ICommand ToggleMessageCommand { get; }
        public ICommand CopyMessageCommand { get; }

        public ICommand OpenSettingsCommand { get; } // 打开设置命令

        public ICommand FileClickedCommand { get; }

        public ICommand TreeViewItemExpandedCommand { get; }

        public ICommand OpenFileCommand { get; }

        public ICommand CopyFilePathCommand { get; }

        public ICommand ShowInExplorerCommand { get; }

        public MainViewModel(
            ToolService toolService,
            InMemoryMessageBus msgBus,
            ToolApiService toolApiService,
            IConfiguration configuration)
        {
            _toolService = toolService;
            _toolApiService = toolApiService;
            _messageBus = msgBus;
            _configuration = configuration;

            _settingsViewModel = new SettingsViewModel(_configuration);

            ProjectPath = _configuration["ProjectSettings:projectPath"];


            ProjectFiles = new ObservableCollection<FileSystemItem>();

            SubscribeToMessageBus();

            Messages = new ObservableCollection<MessageItem>();
            SendCommand = new RelayCommand(
                                async () => await SendMessageAsync(),
                                () => !IsLoading && !string.IsNullOrWhiteSpace(InputText)
                            );
            ClearCommand = new RelayCommand<string>(_ => ClearConversation(), _ => true);

            ToggleMessageCommand = new RelayCommand<MessageItem>(message =>
            {
                if (message != null)
                {
                    message.IsExpanded = !message.IsExpanded;
                }
            });

            CopyMessageCommand = new RelayCommand<MessageItem>(message =>
            {
                if (message != null)
                {
                    Clipboard.SetText(message.Message);
                }
            });

            FileClickedCommand = new RelayCommand<FileSystemItem>(OnFileClicked);
            TreeViewItemExpandedCommand = new RelayCommand<FileSystemItem>(OnTreeViewItemExpanded);

            OpenSettingsCommand = new RelayCommand(OpenSettings);

            OpenFileCommand = new RelayCommand<FileSystemItem>(OpenFile);

            CopyFilePathCommand = new RelayCommand<FileSystemItem>(CopyFilePath);

            ShowInExplorerCommand = new RelayCommand<FileSystemItem>(ShowInExplorer);

            InitializeAutoStartAgents();

            RefreshProject();
        }

        private void RefreshProject()
        {
            LoadProjectFiles();
        }

        private void LoadProjectFiles()
        {
            ProjectFiles.Clear();

            if (Directory.Exists(ProjectPath))
            {
                var rootItem = new FileSystemItem
                {
                    Name = Path.GetFileName(ProjectPath),
                    FullPath = ProjectPath,
                    Level = 0,
                    IsExpanded = true,
                    Type = FileSystemItemType.Directory,
                };

                ProjectFiles.Add(rootItem);
            }
        }

        private void LoadDirectoryContents(FileSystemItem parentItem)
        {
            try
            {
                // 要忽略的文件夹名称（不区分大小写）
                var ignoredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".vs",
                    "bin",
                    "obj",
                    ".git", // 可选：如果你也想忽略git文件夹
                };

                // 加载子目录
                foreach (var dir in Directory.GetDirectories(parentItem.FullPath))
                {
                    var dirName = Path.GetFileName(dir);

                    // 跳过需要忽略的文件夹
                    if (ignoredFolders.Contains(dirName))
                        continue;

                    var dirItem = new FileSystemItem
                    {
                        Name = dirName,
                        FullPath = dir,
                        Type = FileSystemItemType.Directory
                    };
                    parentItem.Children.Add(dirItem);
                }

                // 加载文件
                foreach (var file in Directory.GetFiles(parentItem.FullPath))
                {
                    var extension = Path.GetExtension(file).ToLower();
                    var fileItem = new FileSystemItem
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        Type = GetFileType(extension)
                    };
                    parentItem.Children.Add(fileItem);
                }
            }
            catch (Exception ex)
            {
                // 处理权限等异常
                // 建议至少记录异常，方便调试
                // Console.WriteLine($"加载目录内容时出错: {ex.Message}");
            }
        }
        private FileSystemItemType GetFileType(string extension)
        {
            return extension switch
            {
                ".cs" => FileSystemItemType.CSharpFile,
                ".xaml" => FileSystemItemType.XamlFile,
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => FileSystemItemType.ImageFile,
                ".txt" or ".json" or ".xml" or ".config" => FileSystemItemType.TextFile,
                _ => FileSystemItemType.OtherFile
            };
        }

        private void OnFileClicked(FileSystemItem fileItem)
        {
            if (fileItem == null) return;

            if (fileItem.Type == FileSystemItemType.Directory)
            {
                // 切换目录展开状态
                fileItem.IsExpanded = !fileItem.IsExpanded;

                // 如果展开且子项为空，则加载子项
                if (fileItem.IsExpanded && fileItem.Children.Count == 0)
                {
                    LoadDirectoryContents(fileItem);
                }
            }
            else
            {
                // 处理文件点击事件，例如读取文件内容
                try
                {
                    var content = File.ReadAllText(fileItem.FullPath);
                    AddMessageToUI("系统", $"已选择文件: {fileItem.Name}\n路径: {fileItem.FullPath}\n\n文件内容:\n{content}",
                        Color.FromRgb(0, 50, 0), // 暗绿色背景
                        HorizontalAlignment.Left,
                        Color.FromRgb(0, 255, 128)); // 青色文字
                }
                catch (Exception ex)
                {
                    AddMessageToUI("系统", $"无法读取文件 {fileItem.Name}: {ex.Message}",
                        Color.FromRgb(0, 50, 0), // 暗绿色背景
                        HorizontalAlignment.Left,
                        Color.FromRgb(0, 255, 128)); // 青色文字
                }
            }
        }

        public void OnTreeViewItemExpanded(FileSystemItem item)
        {
            if (item == null || item.Type != FileSystemItemType.Directory) return;

            // 如果展开且子项未加载，则加载子项
            if (item.IsExpanded && !item.HasLoadedChildren)
            {
                LoadDirectoryContents(item);
                item.HasLoadedChildren = true;
            }
        }

        public void OpenFile(FileSystemItem item)
        {
            if (item == null || item.Type == FileSystemItemType.Directory) return;

            try
            {
                var fullPath = item.FullPath;
                if (File.Exists(fullPath))
                {
                    // 使用系统默认程序打开文件
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fullPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                // 处理异常，可以显示提示信息
                Debug.WriteLine($"打开文件失败: {ex.Message}");
            }
        }

        public void CopyFilePath(FileSystemItem item)
        {
            if (item == null) return;

            try
            {
                string textToCopy;

                if (item.IsDirectory)
                {
                    // 如果是文件夹/路径，拷贝最后一个节点名
                    textToCopy = Path.GetFileName(item.FullPath.TrimEnd(Path.DirectorySeparatorChar));
                }
                else
                {
                    // 如果是文件，拷贝文件名（包含扩展名）
                    textToCopy = Path.GetFileName(item.FullPath);
                }

                // 如果获取到的名称为空（比如根目录的情况），则使用完整路径
                if (string.IsNullOrEmpty(textToCopy))
                {
                    textToCopy = item.FullPath;
                }

                Clipboard.SetText(textToCopy);

                // 可以添加一个短暂的提示消息
                // 例如：ShowToastMessage($"已复制: {textToCopy}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"复制路径失败: {ex.Message}");
            }
        }

        public void ShowInExplorer(FileSystemItem item)
        {
            if (item == null) return;

            try
            {
                var fullPath = item.FullPath;
                var argument = "/select, \"" + fullPath + "\"";
                Process.Start("explorer.exe", argument);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"在资源管理器中显示失败: {ex.Message}");
            }
        }
        private void OpenSettings()
        {
            try
            {
                // 如果设置窗口已经打开，将其激活并显示在最前面
                if (_settingsWindow != null && _settingsWindow.IsLoaded)
                {
                    _settingsWindow.Activate();
                    _settingsWindow.WindowState = WindowState.Normal;
                    _settingsWindow.Show();
                    return;
                }

                // 创建新的设置窗口
                _settingsWindow = new SettingsWindow
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    DataContext = _settingsViewModel,
                    Title = "设置"
                };

                // 监听窗口关闭事件，以便清理资源
                _settingsWindow.Closed += (s, e) =>
                {
                    _settingsWindow = null;

                    //AddMessageToUI("系统", "设置已保存",
                    //    Color.FromRgb(0, 50, 0), // 暗绿色背景
                    //    HorizontalAlignment.Left,
                    //    Color.FromRgb(0, 255, 128)); // 青色文字
                };

                _settingsWindow.Show();
            }
            catch (Exception ex)
            {
                AddMessageToUI("系统", $"打开设置失败: {ex.Message}",
                    Color.FromRgb(50, 0, 0), // 暗红色背景
                    HorizontalAlignment.Left,
                    Color.FromRgb(255, 102, 102)); // 亮红色文字
            }
        }

        private async void InitializeAutoStartAgents()
        {
            // 等待UI初始化完成
            await Task.Delay(1000);

            // 或者动态加载
            var configFiles = Directory.EnumerateFiles("Configs\\Agents", "*.json", SearchOption.AllDirectories);
            foreach (var configFile in configFiles)
            {
                var agent = AgentFactory.CreateAgent(
                    configFile,
                    _messageBus,
                    _toolApiService,
                    _toolService
                );
                // 保存Agent引用
                _activeAgents[agent.Name] = agent;
            }

            StartConfigAgents();

        }

        private async void StartConfigAgents()
        {
            foreach (var configAgent in _activeAgents)
            {
                await configAgent.Value.StartAsync();

                AddMessageToUI("系统", $"{configAgent.Value.GetAgentConfig().Description}已启动",
                      Color.FromRgb(0, 50, 0), // 暗绿色背景
                      HorizontalAlignment.Left,
                      Color.FromRgb(0, 255, 128)); // 青色文字
            }
        }

  

        private void SubscribeToMessageBus()
        {
            // 订阅所有的消息
            var subscription = _messageBus.SubscribeAll().
                Subscribe(message =>
                {
                    if(message.Sender == "CodingAgent" && message.Type == AgentMessageType.FolderRefresh)
                    {
                        RefreshProject();
                    }

                    var backgroundColor = GetAgentBackgroundColor(message.Sender);
                    var senderColor = GetAgentSenderColor(message.Sender);

                    AddMessageToUI($"{message.Sender} ====> {message.Recipient}", message.Content,
                                    backgroundColor,
                                    HorizontalAlignment.Left,
                                    senderColor);
                });
        }

        private async Task SendMessageAsync()
        {
            var userMessage = InputText.Trim();
            if (string.IsNullOrWhiteSpace(userMessage))
                return;

            // 添加用户消息到UI - 用户消息使用深蓝色系
            AddMessageToUI("用户", userMessage,
                Color.FromRgb(30, 30, 60), // 深蓝灰背景
                HorizontalAlignment.Right,
                Color.FromRgb(187, 134, 252)); // 紫色文字

            // 清空输入框
            InputText = string.Empty;

            // 显示加载状态
            IsLoading = true;

            try
            {
                // 检查是否有活动的Agent
                if (_activeAgents.Any())
                {
                    // 如果有Agent，将消息发送给所有Agent
                    await SendToActiveAgents(userMessage);
                }
            }
            catch (Exception ex)
            {
                AddMessageToUI("系统", $"请求失败: {ex.Message}",
                    Color.FromRgb(50, 0, 0), // 暗红色背景
                    HorizontalAlignment.Left,
                    Color.FromRgb(255, 102, 102)); // 亮红色文字
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SendToActiveAgents(string userMessage)
        {
            bool isHaveAcitveSession = false;
            AgentMessage? agentMessage = null;

            foreach (var activeAgent in _activeAgents)
            {
                if(activeAgent.Value.HasActiveSessions())
                {
                    isHaveAcitveSession = true;

                    agentMessage = new AgentMessage
                    {
                        Sender = "User",
                        Recipient = activeAgent.Key,
                        Content = userMessage,
                        Type = AgentMessageType.TaskRequest
                    };
                    string sessionId = activeAgent.Value.GetActiveSessionIds().FirstOrDefault();
                    agentMessage.Metadata = new Dictionary<string, object>
                    {
                        { "SessionId", sessionId },
                    };

                    break;
                }
            }

            if(isHaveAcitveSession == false)
            {
                var firstAgent =_activeAgents.FirstOrDefault(x => x.Value.GetAgentConfig().IsFirst == true);
                agentMessage = new AgentMessage
                {
                    Sender = "User",
                    Recipient = firstAgent.Key,
                    Content = userMessage,
                    Type = AgentMessageType.TaskRequest
                };
            }

            if (agentMessage != null)
            {
                try
                {
                    await _messageBus.PublishAsync(agentMessage);
                }
                catch (Exception ex)
                {
                    AddMessageToUI("UserIntentAnalysisAgent", $"处理失败: {ex.Message}",
                        Color.FromRgb(50, 0, 0),
                        HorizontalAlignment.Left,
                        Color.FromRgb(255, 102, 102));
                }
            }
        }


        private Color GetAgentBackgroundColor(string agentType)
        {
            return agentType switch
            {
                "User" => Color.FromRgb(30, 30, 60),          // 用户消息背景
                "系统" => Color.FromRgb(30, 30, 30),           // 系统消息背景
                _ => Color.FromRgb(40, 40, 40)                // 默认深灰色背景
            };
        }

        private Color GetAgentSenderColor(string agentType)
        {
            return agentType switch
            {
                "User" => Color.FromRgb(187, 134, 252),        // 紫色（与DesignAgent一致）
                "系统" => Color.FromRgb(255, 255, 255),         // 白色
                _ => Color.FromRgb(255, 255, 255)              // 白色（默认）
            };
        }

        private void AddMessageToUI(string sender, string message, Color bgColor,
                               HorizontalAlignment alignment, Color senderColor)
        {
            var messageItem = new MessageItem
            {
                Sender = sender,
                Message = message,
                BackgroundColor = new SolidColorBrush(bgColor),
                HorizontalAlignment = alignment,
                SenderColor = new SolidColorBrush(senderColor),
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(messageItem);
            });
        }

        private void ClearConversation()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Clear();
            });

            _conversationHistory.Clear();
        }

    }
}