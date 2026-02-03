using DeepSeekChat.Agent;
using DeepSeekChat.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public class HandleUserNeedsService
    {
        private readonly InMemoryMessageBus _messageBus;
        public HandleUserNeedsService(InMemoryMessageBus msgBus)
        {
            _messageBus = msgBus;
        }

        public async Task<bool> HandleUserNeedsAsync(string arguments)
        {
            var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(arguments);
            if (args == null || !args.ContainsKey("agent_name") || !args.ContainsKey("to_agent_content"))
            {
               return false;
            }

            // 发送给编码 Agent
            var codingMessage = new AgentMessage
            {
                Sender = "RequirementAnalysisAgent",
                Recipient = args["agent_name"].ToString(),
                Content = args["to_agent_content"].ToString(),
                Type = AgentMessageType.TaskRequest,
            };

            // 发布到消息总线
            await _messageBus.PublishAsync(codingMessage);

            return true;
        }
    }
}
