using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DeepSeekChat.Models
{
    public class ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolCallId { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
        public List<ToolCall> ToolCalls { get; set; }
    }

    public class ApiResponse
    {
        [JsonProperty("choices")]
        public List<Choice> Choices { get; set; }
    }

    public class Choice
    {
        [JsonProperty("message")]
        public Message Message { get; set; }
    }

    public class ChatRequestResponse
    {
        public string Content { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
        public bool HasToolCalls { get; set; }
        public bool Success { get; set; }
    }


    public class Message
    {
        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("tool_calls")]
        public List<ToolCall> ToolCalls { get; set; }
    }

    public class ToolCall
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("function")]
        public FunctionCall Function { get; set; }
    }

    public class FunctionCall
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }
    }

    public class ChatRequest
    {
        public string Model { get; set; } = "deepseek-chat";
        public List<ChatMessage> Messages { get; set; }
        public int MaxTokens { get; set; } = 2000;
        public double Temperature { get; set; } = 0.7;
        public bool Stream { get; set; } = false;
        public List<object> Tools { get; set; }
        public string ToolChoice { get; set; } = "auto";
    }

    public class WeatherResult
    {
        public string Temperature { get; set; }
        public string Condition { get; set; }
        public string Humidity { get; set; }
        public string Wind { get; set; }
        public string Note { get; set; }
    }

    public class RequirementSessionInfo
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? LastInteraction { get; set; }
        public string Status { get; set; } // Collecting, Complete, Timeout
        public string CurrentQuestion { get; set; }
        public List<string> UserAnswers { get; set; } = new();
        public List<string> AgentQuestions { get; set; } = new();
    }

    public class RequirementInteraction
    {
        public DateTime Timestamp { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public bool IsOriginalRequirement { get; set; }
        public bool IsClarifyingQuestion { get; set; }
    }

}