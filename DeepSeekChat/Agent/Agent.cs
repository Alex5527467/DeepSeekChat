using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeekChat.Agent
{
    // 支持类型定义
    public enum AgentMessageType
    {
        HelpRequest,
        TaskRequest,
        TaskResponse,
        TaskCompleted,
        CodingRequest,
        CoordinationRequest,
        CoordinationResponse,
        FolderRefresh
    }

    public abstract class Agent
    {
        public string Name { get; protected set; }
        public string Role { get; protected set; }
        protected readonly IMessageBus _messageBus;
        protected readonly CancellationToken _cancellationToken;

        public Agent(string name, string role, IMessageBus messageBus,
                    CancellationToken cancellationToken = default)
        {
            Name = name;
            Role = role;
            _messageBus = messageBus;
            _cancellationToken = cancellationToken;
        }

        public abstract Task<AgentResponse> ProcessAsync(AgentMessage message);
        public abstract Task StartAsync();
        public abstract Task StopAsync();

        // 添加事件
        public event EventHandler<AgentMessage> OnMessageReceived;
        public event EventHandler<AgentMessage> OnMessageSent;

        // 添加受保护的方法来触发事件
        protected virtual void RaiseMessageReceived(AgentMessage message)
        {
            OnMessageReceived?.Invoke(this, message);
        }

        protected virtual void RaiseMessageSent(AgentMessage message)
        {
            OnMessageSent?.Invoke(this, message);
        }

        public IMessageBus GetMessageBus()
        {
            return _messageBus;
        }
    }

    public class AgentMessage
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public required string Sender { get; set; }
        public required string Recipient { get; set; }
        public required string Content { get; set; }
        public AgentMessageType Type { get; set; }
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class AgentResponse
    {
        public string SessionId { get; set; }
        public bool Success { get; set; }
        public string Content { get; set; }
        public string NextAgent { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public bool IsComplete;
    }
}
