using DeepSeekChat.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DeepSeekChat.Agent
{
    public class ReviewHandleAgent : Agent
    {
        private readonly ChatService _chatService;
        private IDisposable _subscription;

        // 添加对话历史记录
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();

        public ReviewHandleAgent(IMessageBus messageBus,
                          ChatService chatService,
                          CancellationToken cancellationToken = default)
            : base("ReviewHandleAgent", "代码审查对应者", messageBus, cancellationToken)
        {
            _chatService = chatService;
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
                        var response = await ProcessReviewHandleTask(message);

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

        private async Task<AgentResponse> ProcessReviewHandleTask(AgentMessage message)
        {
         
            //todo
            return new AgentResponse
            {
                Success = true,
                Content = $"用户要求修改内容已经修改完成",
            };
        }

        public override async Task<AgentResponse> ProcessAsync(AgentMessage message)
        {
            // 保存接收到的消息
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            var response = await ProcessReviewHandleTask(message);

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