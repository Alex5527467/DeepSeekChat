using DeepSeekChat.Models;
using DeepSeekChat.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DeepSeekChat.Services.ToolApiService;

namespace DeepSeekChat.Agent
{
    public class TaskCreateAgent : Agent
    {
        private readonly ChatService _chatService;
        private readonly ToolApiService _toolApiService;
        private readonly ToolService _toolService;
        private IDisposable _subscription;

        // 添加对话历史记录
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();

        public TaskCreateAgent(IMessageBus messageBus,
                          ChatService chatService,
                          ToolApiService toolApiService,
                          ToolService toolService,
                          CancellationToken cancellationToken = default)
            : base("TaskCreateAgent", "任务拆分者", messageBus, cancellationToken)
        {
            _chatService = chatService;
            _toolApiService = toolApiService;
            _toolService = toolService;
        }

        public override async Task StartAsync()
        {
            _subscription = _messageBus.Subscribe(Name)
                .Subscribe(async message =>
                {
                    if (message.Type == AgentMessageType.TaskRequest)
                    {
                        // 保存接收到的消息
                        _messageHistory.Add(message);

                        // 触发消息接收事件
                        RaiseMessageReceived(message);

                        // 处理任务拆分
                        var response = await ProcessProjectTaskTask(message);

                        // 保存发送的消息
                        var responseMessage = new AgentMessage
                        {
                            Sender = Name,
                            Recipient = message.Sender,
                            Content = response.Content,
                            Type = AgentMessageType.TaskResponse
                        };

                        _messageHistory.Add(responseMessage);

                        // 触发消息发送事件
                        RaiseMessageSent(responseMessage);

                    }
                });

            await Task.CompletedTask;
        }

        private async Task<AgentResponse> ProcessProjectTaskTask(AgentMessage message)
        {
            // 使用 ChatService 拆分任务
            var projectTask = await CreateProjectTaskAsync(message.Content);

            return new AgentResponse
            {
                Success = true,
                Content = $"任务已生成，并已分配给编码Agent",
                NextAgent = "CodingAgent"
            };
        }

        private async Task<string> CreateProjectTaskAsync(string msg)
        {
            var conversation = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = "你是任务拆分者，请按照文件列表拆分成多个任务，技术栈是C#，并分配给CodingAgent。" +
                    "重要规则：" +
                    "1. 每个任务的dependencies字段必须只包含 依赖：后面列出的文件名，不能包含其他内容" +
                    "2. 如果\"依赖：\"后面为空或没有依赖，dependencies字段应该为空数组 []" +
                    "3. 如果\"依赖：\"后面有多个文件名，按照逗号分隔提取每个文件名" +
                    "4. 文件名提取后需要去除前后空格" +
                    "5. 如果是控制台程序，在技术需求中追加使用Main方法来接收参数" +
                    "请严格按照上述规则处理dependencies字段。"
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = msg
                }
            };

            var response = await _toolApiService.SendToolRequestAsync(conversation, ToolApiType.TaskManager);

            // 检查是否需要工具调用
            if (response.HasToolCalls && response.ToolCalls != null && response.ToolCalls.Count > 0)
            {
                // 处理工具调用
                await ProcessToolCalls(response.ToolCalls);

            }
            return response.Success ? response.Content : "拆分任务失败";
        }

        private Task ProcessToolCalls(List<ToolCall> toolCalls)
        {

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    var toolResult = _toolService.ExecuteTool(toolCall.Function.Name, toolCall.Function.Arguments);
                    var toolJson = JsonConvert.SerializeObject(toolResult);

                    var toolResponse = new ChatMessage
                    {
                        Role = "tool",
                        Content = toolJson,
                        ToolCallId = toolCall.Id,
                        Name = toolCall.Function.Name
                    };
              
                }
                catch (Exception ex)
                {
                }
            }

            return Task.CompletedTask;
        }

        public override async Task<AgentResponse> ProcessAsync(AgentMessage message)
        {
            // 保存接收到的消息
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            var response = await ProcessProjectTaskTask(message);

            // 保存响应消息
            var responseMessage = new AgentMessage
            {
                Sender = Name,
                Recipient = message.Sender,
                Content = response.Content,
                Type = AgentMessageType.TaskResponse
            };

            _messageHistory.Add(responseMessage);
            RaiseMessageSent(responseMessage);

            return response;
        }

        // 添加获取消息历史的方法
        public IReadOnlyList<AgentMessage> GetMessageHistory()
        {
            return _messageHistory.AsReadOnly();
        }

        public override Task StopAsync()
        {
            _subscription?.Dispose();
            _messageHistory.Clear();
            return Task.CompletedTask;
        }
    }
}