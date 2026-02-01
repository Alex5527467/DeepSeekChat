using DeepSeekChat.Agent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Agent
{
    public interface IMessageBus
    {
        Task PublishAsync(AgentMessage message);
        IObservable<AgentMessage> Subscribe(string agentName);
        IObservable<AgentMessage> SubscribeAll();
    }

    public class InMemoryMessageBus : IMessageBus
    {
        private readonly Subject<AgentMessage> _subject = new();
        private readonly ConcurrentDictionary<string, List<AgentMessage>> _agentInbox = new();

        public async Task PublishAsync(AgentMessage message)
        {
            if (!string.IsNullOrEmpty(message.Recipient))
            {
                _agentInbox.AddOrUpdate(message.Recipient,
                    new List<AgentMessage> { message },
                    (key, existing) =>
                    {
                        existing.Add(message);
                        return existing;
                    });
            }

            _subject.OnNext(message);
            await Task.CompletedTask;
        }

        public IObservable<AgentMessage> Subscribe(string agentName)
        {
            return _subject
                .Where(msg => msg.Recipient == agentName ||
                             msg.Recipient == "broadcast")
                .AsObservable();
        }

        public IObservable<AgentMessage> SubscribeAll()
        {
            return _subject.AsObservable();
        }
    }
}
