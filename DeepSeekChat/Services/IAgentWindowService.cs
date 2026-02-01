using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public interface IAgentWindowService
    {
        void ShowAgentWindow(string agentName, object agentInstance, string displayName);
        void CloseAgentWindow(string agentName);
        bool IsAgentWindowOpen(string agentName);
    }
}