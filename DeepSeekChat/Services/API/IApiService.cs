using DeepSeekChat.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public interface IApiService
    {
        Task<ChatRequestResponse> SendChatRequestAsync(List<ChatMessage> conversationHistory);
        Task<string> SendSecondRequestAsync(List<ChatMessage> conversationHistory);
    }
}