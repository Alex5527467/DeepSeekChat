// ChatService.cs - 专门处理对话
using DeepSeekChat.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public class ChatService : IChatService
    {
        private readonly HttpClient _httpClient;
        private const string ApiUrl = "https://api.deepseek.com/chat/completions";

        public ChatService(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<ChatRequestResponse> SendChatRequestAsync(List<ChatMessage> conversationHistory)
        {
            var requestData = new ChatRequest
            {
                Messages = conversationHistory,
                Tools = null, // 对话服务不使用工具
                MaxTokens = 2000,
                Temperature = 0.7,
                Stream = false
            };

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                },
                NullValueHandling = NullValueHandling.Ignore
            };

            var jsonContent = JsonConvert.SerializeObject(requestData, settings);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(ApiUrl, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseJson);

                var result = new ChatRequestResponse();

                if (apiResponse?.Choices?.Count > 0)
                {
                    var message = apiResponse.Choices[0].Message;
                    result.Content = message.Content ?? string.Empty;
                    result.Success = true;
                }
                else
                {
                    result.Content = "抱歉，我无法生成回复。";
                    result.Success = false;
                }
                return result;
            }
            catch (HttpRequestException ex)
            {
                return new ChatRequestResponse
                {
                    Content = $"请求失败: {ex.Message}",
                    Success = false
                };
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public interface IChatService : IDisposable
    {
        Task<ChatRequestResponse> SendChatRequestAsync(List<ChatMessage> conversationHistory);
    }
}