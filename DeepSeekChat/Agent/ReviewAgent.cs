using DeepSeekChat.Models;
using DeepSeekChat.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DeepSeekChat.Agent
{
    public class ReviewAgent : Agent
    {
        private readonly ToolApiService _toolApiService;
        private readonly ToolService _toolService;
        private IDisposable _subscription;
        private string ROOT_PATH;

        // 添加对话历史记录
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();

        public ReviewAgent(IMessageBus messageBus,
                          ToolApiService toolApiService,
                          ToolService toolService,
                          IConfiguration configuration,
                          CancellationToken cancellationToken = default)
            : base("ReviewAgent", "代码审查员", messageBus, cancellationToken)
        {
            _toolApiService = toolApiService;
            _toolService = toolService;

            ROOT_PATH = configuration["ProjectSettings:projectPath"];
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

                        // 处理设计任务
                        var response = await ProcessReviewTask(message);

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

        private async Task<AgentResponse> ProcessReviewTask(AgentMessage message)
        {
            // todo
            string projectName = ""; 
            var folderStructure = _toolService.ExecuteTool("get_folder_structure_description", (ROOT_PATH + $"\\{projectName}"));

            return new AgentResponse
            {
                Success = true,
                Content = $"设计方案已生成，并发送给任务管理者",
            };
        }


        public override async Task<AgentResponse> ProcessAsync(AgentMessage message)
        {
            // 保存接收到的消息
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            var response = await ProcessReviewTask(message);

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
