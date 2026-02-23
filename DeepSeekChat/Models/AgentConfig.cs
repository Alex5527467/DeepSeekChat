// Configurations/AgentConfig.cs
using DeepSeekChat.Agent;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DeepSeekChat.Models
{
    public class AgentConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("is_first")]
        public bool IsFirst { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("system_prompt")]
        public List<string> SystemPrompt { get; set; }

        [JsonProperty("prompt_template")]
        public string PromptTemplate { get; set; }

        [JsonProperty("allowed_senders")]
        public List<string> AllowedSenders { get; set; }

        [JsonProperty("response_handlers")]
        public Dictionary<string, List<ResponseHandlerTarget>> ResponseHandlers { get; set; }

        [JsonProperty("tool_api_service")]
        public List<string> ToolApiServices { get; set; }

        [JsonProperty("tools")]
        public List<string> Tools { get; set; }
    }

    public class ResponseHandlerTarget
    {
        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("session")]
        public string Session { get; set; }
    }

    // 工具执行结果类
    public class ToolResult
    {
        public string ToolCallId { get; set; }
        public string ToolName { get; set; }
        public string Content { get; set; }
        public bool IsSuccess { get; set; }
    }

    #region Session相关类定义

    /// <summary>
    /// Session统计信息
    /// </summary>
    public class SessionStats
    {
        public int TotalSessions { get; set; }
        public int ActiveSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int SessionsAwaitingConfirmation { get; set; }
        public DateTime OldestSessionTime { get; set; }
        public DateTime NewestSessionTime { get; set; }
    }

    /// <summary>
    /// Session状态
    /// </summary>
    public class SessionState : Dictionary<string, object>
    {
    }

    /// <summary>
    /// Session状态枚举
    /// </summary>
    public enum SessionStatus
    {
        Active,
        Completed
    }

    /// <summary>
    /// Agent会话
    /// </summary>
    public class AgentSession
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public SessionStatus Status { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
    #endregion

    public class AgentMessage
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public required string Sender { get; set; }
        public required string Recipient { get; set; }
        public required string Content { get; set; }
        public AgentMessageType Type { get; set; }
        public DateTime? Timestamp { get; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    // 工具调用响应模型
    public class ToolCallResponse
    {
        public string Content { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
        public bool HasToolCalls { get; set; }
        public bool Success { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string SessionId {  get; set; }

        public string NextAgent { get; set; }

        public bool IsComplete;
    }

    public class ToolCallInfo
    {
        public string Id { get; set; }
        public string FunctionName { get; set; }
        public Dictionary<string,object> Arguments { get; set; }
    }
}