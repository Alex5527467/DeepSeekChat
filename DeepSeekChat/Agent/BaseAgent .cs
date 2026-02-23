using DeepSeekChat.Models;
using DeepSeekChat.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static DeepSeekChat.Services.ToolApiService;

namespace DeepSeekChat.Agent
{
    public abstract class BaseAgent : Agent
    {
        protected readonly ToolApiService _toolApiService;
        protected readonly ToolService _toolService;
        protected readonly AgentConfig _config;
        protected IDisposable _subscription;

        protected readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();
        private readonly string _configFilePath;

        // Session管理 - 只保存在内存中
        protected readonly Dictionary<string, AgentSession> _sessions = new Dictionary<string, AgentSession>();
        protected readonly Dictionary<string, SessionState> _sessionStates = new Dictionary<string, SessionState>();
        protected readonly int _sessionTimeoutMinutes = 10; // Session超时时间调整为10分钟

        // 添加一个字典来跟踪需要用户确认的Session
        protected readonly HashSet<string> _sessionsAwaitingConfirmation = new HashSet<string>();

        // 添加定时器用于清理过期Session
        private Timer _sessionCleanupTimer;

        public BaseAgent(string configFilePath,
                        IMessageBus messageBus,
                        ToolApiService toolApiService,
                        ToolService toolService,
                        CancellationToken cancellationToken = default)
            : base(Path.GetFileNameWithoutExtension(configFilePath),
                  "动态加载的Agent",
                  messageBus,
                  cancellationToken)
        {
            _configFilePath = configFilePath;
            _toolApiService = toolApiService;
            _toolService = toolService;
            _config = LoadConfig();
        }

        protected AgentConfig LoadConfig()
        {
            try
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonConvert.DeserializeObject<AgentConfig>(json);

                if (config == null)
                    throw new InvalidOperationException($"Failed to load agent config from {_configFilePath}");

                return config;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading agent configuration: {ex.Message}", ex);
            }
        }

        protected virtual string BuildPrompt(AgentMessage message)
        {
            if (string.IsNullOrEmpty(_config.PromptTemplate))
                return message.Content;

            // 获取Session信息
            var sessionContext = GetSessionContext(message);

            // 使用增强的模板替换
            var prompt = _config.PromptTemplate
                .Replace("{user_input}", message.Content)
                .Replace("{message_history}", FormatMessageHistory())
                .Replace("{session_context}", sessionContext)
                .Replace("{session_id}", GetSessionId(message) ?? "new_session");

            return prompt;
        }

        #region Session管理功能

        /// <summary>
        /// 获取或创建Session ID
        /// </summary>
        protected virtual string GetOrCreateSessionId(AgentMessage message)
        {
            if (message.Metadata != null && message.Metadata.TryGetValue("SessionId", out var sessionIdObj))
            {
                return sessionIdObj?.ToString();
            }

            // 从消息中提取用户ID或生成新的Session ID
            var sender = message.Sender;
            var sessionId = $"{sender}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 4)}";

            if (message.Metadata == null)
                message.Metadata = new Dictionary<string, object>();

            message.Metadata["SessionId"] = sessionId;
            return sessionId;
        }

        /// <summary>
        /// 获取Session ID
        /// </summary>
        protected virtual string GetSessionId(AgentMessage message)
        {
            if (message.Metadata != null && message.Metadata.TryGetValue("SessionId", out var sessionIdObj))
            {
                return sessionIdObj?.ToString();
            }
            return null;
        }

        /// <summary>
        /// 获取或创建Session（开始对话时开始Session）
        /// </summary>
        protected virtual AgentSession GetOrCreateSession(AgentMessage message)
        {
            var sessionId = GetOrCreateSessionId(message);

            if (!_sessions.ContainsKey(sessionId))
            {
                var session = new AgentSession
                {
                    SessionId = sessionId,
                    UserId = message.Sender,
                    CreatedTime = DateTime.Now,
                    LastActivityTime = DateTime.Now,
                    Status = SessionStatus.Active,
                    Metadata = new Dictionary<string, object>
                    {
                        { "OriginalSender", message.Sender },
                        { "StartTime", DateTime.Now },
                        { "AgentName", Name }
                    }
                };

                _sessions[sessionId] = session;
                _sessionStates[sessionId] = new SessionState();

                Console.WriteLine($"Session已开始: {sessionId}");
            }
            else
            {
                // 更新最后活动时间
                _sessions[sessionId].LastActivityTime = DateTime.Now;

                // 如果Session处于等待确认状态，保持该状态
                if (_sessionsAwaitingConfirmation.Contains(sessionId))
                {
                    Console.WriteLine($"Session等待用户确认中: {sessionId}");
                }
            }

            return _sessions[sessionId];
        }

        /// <summary>
        /// 获取Session状态
        /// </summary>
        protected virtual SessionState GetSessionState(string sessionId)
        {
            if (_sessionStates.ContainsKey(sessionId))
            {
                return _sessionStates[sessionId];
            }

            var state = new SessionState();
            _sessionStates[sessionId] = state;
            return state;
        }

        /// <summary>
        /// 更新Session状态
        /// </summary>
        protected virtual void UpdateSessionState(string sessionId, Dictionary<string, object> stateUpdates)
        {
            var state = GetSessionState(sessionId);

            foreach (var kvp in stateUpdates)
            {
                state[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// 获取Session上下文
        /// </summary>
        protected virtual string GetSessionContext(AgentMessage message)
        {
            var sessionId = GetSessionId(message);
            if (string.IsNullOrEmpty(sessionId) || !_sessions.ContainsKey(sessionId))
            {
                return "新会话，无历史记录。";
            }

            var session = _sessions[sessionId];
            var state = GetSessionState(sessionId);

            var sb = new StringBuilder();
            sb.AppendLine($"=== 会话信息 ===");
            sb.AppendLine($"会话ID: {session.SessionId}");
            sb.AppendLine($"用户: {session.UserId}");
            sb.AppendLine($"创建时间: {session.CreatedTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"最后活动: {session.LastActivityTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"状态: {session.Status}");

            if (_sessionsAwaitingConfirmation.Contains(sessionId))
            {
                sb.AppendLine($"等待用户确认: 是");
            }

            if (state.Any())
            {
                sb.AppendLine($"\n=== 会话状态 ===");
                foreach (var kvp in state)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            // 获取会话相关的历史消息
            var sessionMessages = _messageHistory
                .Where(m => m.Metadata != null &&
                           m.Metadata.TryGetValue("SessionId", out var msgSessionId) &&
                           msgSessionId?.ToString() == sessionId)
                .OrderBy(m => m.Timestamp)
                .TakeLast(30) 
                .ToList();

            if (sessionMessages.Any())
            {
                sb.AppendLine($"\n=== 最近对话 ===");
                foreach (var msg in sessionMessages)
                {
                    var time = msg.Timestamp?.ToString("HH:mm:ss") ?? "未知时间";
                    //sb.AppendLine($"[{time}] {msg.Sender}: {Utils.Utils.TruncateText(msg.Content,100)}");
                    sb.AppendLine($"[{time}] {msg.Sender}: {msg.Content}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 清理过期的Session（超时10分钟）
        /// </summary>
        protected virtual void CleanupExpiredSessions()
        {
            var now = DateTime.Now;
            var expiredSessions = _sessions
                .Where(kvp =>
                    (now - kvp.Value.LastActivityTime).TotalMinutes > _sessionTimeoutMinutes ||
                    kvp.Value.Status == SessionStatus.Completed)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _sessions.Remove(sessionId);
                _sessionStates.Remove(sessionId);
                _sessionsAwaitingConfirmation.Remove(sessionId);
                Console.WriteLine($"清理过期或已完成Session: {sessionId}");
            }
        }

        /// <summary>
        /// 完成Session（全部处理结束时结束Session）
        /// </summary>
        protected virtual void CompleteSession(string sessionId, Dictionary<string, object> finalData = null)
        {
            if (_sessions.ContainsKey(sessionId))
            {
                // 检查是否在等待用户确认，如果是则不能结束Session
                if (_sessionsAwaitingConfirmation.Contains(sessionId))
                {
                    Console.WriteLine($"Session {sessionId} 正在等待用户确认，不能结束");
                    return;
                }

                _sessions[sessionId].Status = SessionStatus.Completed;
                _sessions[sessionId].CompletedTime = DateTime.Now;

                if (finalData != null)
                {
                    foreach (var kvp in finalData)
                    {
                        _sessions[sessionId].Metadata[kvp.Key] = kvp.Value;
                    }
                }

                Console.WriteLine($"Session已完成: {sessionId}");
            }
        }

        /// <summary>
        /// 标记Session需要用户确认（有需要和用户确认时不能结束Session）
        /// </summary>
        protected virtual void MarkSessionAsAwaitingConfirmation(string sessionId)
        {
            if (_sessions.ContainsKey(sessionId))
            {
                _sessionsAwaitingConfirmation.Add(sessionId);
                Console.WriteLine($"Session标记为需要用户确认: {sessionId}");
            }
        }

        /// <summary>
        /// 标记Session不需要用户确认
        /// </summary>
        protected virtual void ClearSessionAwaitingConfirmation(string sessionId)
        {
            _sessionsAwaitingConfirmation.Remove(sessionId);
            Console.WriteLine($"Session清除等待用户确认标记: {sessionId}");
        }

        /// <summary>
        /// 检查Session是否在等待用户确认
        /// </summary>
        protected virtual bool IsSessionAwaitingConfirmation(string sessionId)
        {
            return _sessionsAwaitingConfirmation.Contains(sessionId);
        }

        /// <summary>
        /// 检查是否有激活的Session
        /// </summary>
        public virtual bool HasActiveSessions()
        {
            CleanupExpiredSessions(); // 先清理过期Session
            return _sessions.Any(s => s.Value.Status == SessionStatus.Active);
        }

        /// <summary>
        /// 获取所有激活的Session ID
        /// </summary>
        public virtual List<string> GetActiveSessionIds()
        {
            CleanupExpiredSessions(); // 先清理过期Session
            return _sessions
                .Where(s => s.Value.Status == SessionStatus.Active)
                .Select(s => s.Key)
                .ToList();
        }

        /// <summary>
        /// 获取特定用户的所有激活Session ID
        /// </summary>
        public virtual List<string> GetActiveSessionIdsForUser(string userId)
        {
            CleanupExpiredSessions(); // 先清理过期Session
            return _sessions
                .Where(s => s.Value.Status == SessionStatus.Active && s.Value.UserId == userId)
                .Select(s => s.Key)
                .ToList();
        }

        /// <summary>
        /// 获取所有Session的统计信息
        /// </summary>
        public virtual SessionStats GetSessionStats()
        {
            CleanupExpiredSessions(); // 先清理过期Session

            return new SessionStats
            {
                TotalSessions = _sessions.Count,
                ActiveSessions = _sessions.Count(s => s.Value.Status == SessionStatus.Active),
                CompletedSessions = _sessions.Count(s => s.Value.Status == SessionStatus.Completed),
                SessionsAwaitingConfirmation = _sessionsAwaitingConfirmation.Count,
                OldestSessionTime = _sessions.Any() ? _sessions.Values.Min(s => s.CreatedTime) : DateTime.MinValue,
                NewestSessionTime = _sessions.Any() ? _sessions.Values.Max(s => s.LastActivityTime) : DateTime.MinValue
            };
        }

        /// <summary>
        /// 强制结束Session（用于异常情况）
        /// </summary>
        public virtual void ForceCompleteSession(string sessionId)
        {
            if (_sessions.ContainsKey(sessionId))
            {
                _sessions[sessionId].Status = SessionStatus.Completed;
                _sessions[sessionId].CompletedTime = DateTime.Now;
                _sessionsAwaitingConfirmation.Remove(sessionId);
                Console.WriteLine($"强制结束Session: {sessionId}");
            }
        }

        #endregion

        protected virtual string FormatMessageHistory()
        {
            var history = new StringBuilder();
            foreach (var msg in _messageHistory.TakeLast(30))
            {
                var time = msg.Timestamp?.ToString("HH:mm:ss") ?? "未知时间";
                history.AppendLine($"[{time} {msg.Sender}]: {Utils.Utils.TruncateText(msg.Content, 150)}");
            }
            return history.ToString();
        }

        protected virtual async Task<ToolCallResponse> ProcessWithTools(AgentMessage message)
        {
            // 获取或创建Session
            var session = GetOrCreateSession(message);
            var sessionId = session.SessionId;

            // 清理过期Session
            CleanupExpiredSessions();

            var conversation = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = string.Join("\n", _config.SystemPrompt) ?? "你是一个AI助手。"
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = BuildPrompt(message)
                }
            };

            ToolCallResponse response = null;
            bool hasToolCalls = true;
            int maxIterations =   10; // 防止无限循环
            int iterationCount = 0;

            // 循环处理工具调用，直到任务完成
            while (hasToolCalls && iterationCount < maxIterations)
            {
                iterationCount++;

                // 调用工具API
                response = await _toolApiService.SendToolRequestAsync(conversation, _config.ToolApiServices);

                // 如果有工具调用，处理它们
                if (response.HasToolCalls && response.ToolCalls != null)
                {
                    // 处理工具调用并将结果添加到会话历史中
                    var toolResults = await ProcessToolCalls(response.ToolCalls, message, sessionId);

                    // 将工具调用和工具结果添加到对话历史中
                    // 添加工具调用消息
                    foreach (var toolCall in response.ToolCalls)
                    {
                        conversation.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = null,
                            ToolCalls = new List<ToolCall> { toolCall }
                        });
                    }

                    // 添加工具结果消息
                    foreach (var result in toolResults)
                    {
                        conversation.Add(new ChatMessage
                        {
                            Role = "tool",
                            Content = result.Content ?? "工具执行完成",
                            ToolCallId = result.ToolCallId,
                            Name = result.ToolName
                        });
                    }

                    // 继续下一轮工具调用
                    continue;
                }
                else
                {
                    // 没有工具调用，任务完成，跳出循环
                    hasToolCalls = false;
                }
            }

            // 检查是否达到最大迭代次数
            if (iterationCount >= maxIterations)
            {
                response = new ToolCallResponse
                {
                    Content = "工具调用达到最大迭代次数，可能存在问题，请检查工具配置或重试。",
                    Success = false
                };
            }

            // 处理响应处理器
            if (_config.ResponseHandlers != null && _config.ResponseHandlers.Any())
            {
                response = await ProcessResponseHandlers(response, message, sessionId, _config.ResponseHandlers);
            }

            // 添加Session信息到响应
            if (response.Metadata == null)
                response.Metadata = new Dictionary<string, object>();

            response.Metadata["SessionId"] = sessionId;
            response.Metadata["SessionStatus"] = session.Status.ToString();

            // 检查响应是否表示需要用户确认
            if (response.Content?.Contains("请确认") == true ||
                response.Content?.Contains("请问") == true ||
                response.Content?.Contains("是否") == true)
            {
                MarkSessionAsAwaitingConfirmation(sessionId);
            }

            return response;
        }

        protected virtual async Task<ToolCallResponse> ProcessResponseHandlers(ToolCallResponse response,
                                                                            AgentMessage message,
                                                                            string sessionId,
                                                                            Dictionary<string, List<ResponseHandlerTarget>> _responseHandlers)
        {
            var responseContent = response.Content?.Trim() ?? string.Empty;

            try
            {
                bool routeHandled = false;

                // 检查是否有配置的响应处理器
                if (_responseHandlers != null && _responseHandlers.Count > 0)
                {
                    foreach (var handler in _responseHandlers)
                    {
                        // 修改：不检查是否以handler.Key开头，而是检查是否包含
                        if (ContainsRouteInstruction(responseContent, handler.Key))
                        {
                            routeHandled = await HandleConfiguredRoute(handler, response, message, sessionId, responseContent);
                            break;
                        }
                    }
                }

                // 如果没有处理任何路由，记录日志
                if (!routeHandled)
                {
                    Console.WriteLine("未匹配到路由标识符，保持原始响应");

                    // 如果没有路由，检查是否可以完成Session
                    if (!IsSessionAwaitingConfirmation(sessionId))
                    {
                        CompleteSession(sessionId, new Dictionary<string, object>
                        {
                            { "CompletedTime", DateTime.Now },
                            { "FinalResponse", response.Content },
                            { "OriginalRequest", message.Content }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                // 保持向后兼容：记录错误但继续处理
                response.Metadata["Error"] = ex.Message;
                response.Metadata["NextAgent"] = "None";

                // 出错时强制结束Session
                ForceCompleteSession(sessionId);
            }

            return response;
        }

        /// <summary>
        /// 检查响应内容中是否包含指定的路由指令
        /// 修改：不检查是否开头，而是检查是否包含独立一行
        /// </summary>
        private bool ContainsRouteInstruction(string responseContent, string instruction)
        {
            if (string.IsNullOrEmpty(responseContent) || string.IsNullOrEmpty(instruction))
                return false;

            // 按行分割响应内容
            var lines = responseContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // 检查是否有完全匹配的行（忽略前后空格）
                if (line.Trim().StartsWith(instruction, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 处理配置的路由
        /// </summary>
        private async Task<bool> HandleConfiguredRoute(KeyValuePair<string, List<ResponseHandlerTarget>> handler,
                                                   ToolCallResponse response,
                                                   AgentMessage message,
                                                   string sessionId,
                                                   string responseContent)
        {
            var routeTargets = handler.Value; // 现在是 List<ResponseHandlerTarget>

            if (routeTargets != null && routeTargets.Any())
            {
                var instruction = handler.Key;

                // 提取路由指令之后的内容作为消息
                var contentAfterInstruction = ExtractContentAfterInstruction(responseContent, instruction);

                // 检查是否包含User目标
                var userTarget = routeTargets.FirstOrDefault(t => t.Target.Equals("User", StringComparison.OrdinalIgnoreCase));
                if (userTarget != null)
                {
                    // 路由给用户 - 根据session配置处理
                    if (userTarget.Session.Equals("continue", StringComparison.OrdinalIgnoreCase))
                    {
                        // 标记Session为等待确认状态（继续会话）
                        MarkSessionAsAwaitingConfirmation(sessionId);
                    }
                    else if (userTarget.Session.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        // 清除会话（结束会话）
                        CompleteSession(sessionId);
                    }

                    // 提取指令行之后的所有内容
                    var userMessage = contentAfterInstruction;

                    // 如果提取的消息为空，使用默认消息
                    if (string.IsNullOrEmpty(userMessage))
                    {
                        userMessage = "请提供更多详细信息。";
                    }

                    await SendToUser(userMessage, message, sessionId);

                    response.Content = userMessage;
                    response.Metadata["NextAgent"] = "User";
                    response.Metadata["RouteType"] = "Configured";
                    response.Metadata["AwaitingConfirmation"] = userTarget.Session.Equals("continue", StringComparison.OrdinalIgnoreCase);
                    response.Metadata["SessionAction"] = userTarget.Session;

                    return true;
                }
                else
                {
                    // 路由给其他Agent - 可能有多个目标Agent
                    foreach (var target in routeTargets)
                    {
                        // 检查目标Agent是否存在于响应内容中
                        if (responseContent.Contains($"ROUTE_TO_AGENT:{target.Target}", StringComparison.OrdinalIgnoreCase))
                        {
                            // 根据session配置处理
                            if (target.Session.Equals("clear", StringComparison.OrdinalIgnoreCase))
                            {
                                // 清除等待确认标记并完成当前Session
                                ClearSessionAwaitingConfirmation(sessionId);
                                CompleteSession(sessionId, new Dictionary<string, object>
                        {
                            { "NextAgent", target.Target },
                            { "CompletedTime", DateTime.Now },
                            { "HandoverReason", "路由到其他Agent" },
                            { "SessionAction", "clear" }
                        });
                            }
                            else if (target.Session.Equals("continue", StringComparison.OrdinalIgnoreCase))
                            {
                                // 继续会话，不清除Session
                                // 可以选择保留一些上下文信息
                            }

                            // 提取原始请求
                            var originalRequest = contentAfterInstruction;

                            await SendToAgent(target.Target, originalRequest, message, sessionId);

                            response.Content = $"需求已分析，正在转发给 {target.Target}...";
                            response.Metadata["NextAgent"] = target.Target;
                            response.Metadata["RouteType"] = "Configured";
                            response.Metadata["TargetAgent"] = target.Target;
                            response.Metadata["SessionAction"] = target.Session;

                            return true;
                        }
                    }
                }
            }

            return false;
        }


        /// <summary>
        /// 从响应内容中提取指令之后的内容
        /// 修改：指令可能在任意行，需要找到指令行然后提取之后的内容
        /// </summary>
        private string ExtractContentAfterInstruction(string responseContent, string instruction)
        {
            if (string.IsNullOrEmpty(responseContent))
                return string.Empty;

            // 按行分割响应内容
            var lines = responseContent.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            // 查找指令行
            int instructionLineIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith(instruction, StringComparison.OrdinalIgnoreCase))
                {
                    instructionLineIndex = i;
                    break;
                }
            }

            // 如果没有找到指令行，返回空
            if (instructionLineIndex == -1)
                return string.Empty;

            // 提取指令行之后的所有行
            var linesAfterInstruction = new List<string>();
            for (int i = instructionLineIndex + 1; i < lines.Length; i++)
            {
                linesAfterInstruction.Add(lines[i]);
            }

            return string.Join(Environment.NewLine, linesAfterInstruction).Trim();
        }

        /// <summary>
        /// 从指令之后的内容中提取原始请求
        /// 修改：从指令之后的内容中提取，而不是从整个响应内容
        /// </summary>
        private string ExtractOriginalRequestFromContentAfterInstruction(string contentAfterInstruction, string fallback)
        {
            if (string.IsNullOrEmpty(contentAfterInstruction))
                return fallback;

            var lines = contentAfterInstruction.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // 查找第一个非空行
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine) &&
                    !trimmedLine.StartsWith("[") &&
                    !trimmedLine.StartsWith("//") &&
                    !trimmedLine.StartsWith("#"))
                {
                    return trimmedLine;
                }
            }

            return fallback;
        }


        /// <summary>
        /// 提取目标Agent（增强版）
        /// 处理指令可能在任意行的情况
        /// </summary>
        private string ExtractTargetAgent(string responseContent, List<string> configuredTargets)
        {
            foreach (var target in configuredTargets)
            {
                // 使用正则表达式提取Agent名称
                var pattern = $@"ROUTE_TO_AGENT:\s*{Regex.Escape(target)}\s*(\n|$)";
                var match = Regex.Match(responseContent, pattern, RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    return target;
                }
            }

            // 兼容旧格式：尝试提取任意Agent名称
            var anyMatch = Regex.Match(responseContent, @"ROUTE_TO_AGENT:\s*(\w+)\s*\n", RegexOptions.IgnoreCase);
            if (anyMatch.Success)
            {
                string extractedAgent = anyMatch.Groups[1].Value.Trim();
                if (configuredTargets.Contains(extractedAgent, StringComparer.OrdinalIgnoreCase))
                {
                    return extractedAgent;
                }
            }

            return string.Empty;
        }

        private async Task SendToAgent(string targetAgent, string content, AgentMessage originalMessage, string sessionId)
        {
            var nextMessage = new AgentMessage
            {
                Sender = Name,
                Recipient = targetAgent,
                Content = content,
                Type = AgentMessageType.TaskRequest,
                Metadata = new Dictionary<string, object>
                {
                    { "SessionId", sessionId },
                    { "OriginalSender", originalMessage.Sender },
                    { "SourceAgent", Name },
                    { "PreviousResponse", content }
                }
            };

            await _messageBus.PublishAsync(nextMessage);
        }

        private async Task SendToUser(string content, AgentMessage originalMessage, string sessionId)
        {
            var userMessage = new AgentMessage
            {
                Sender = Name,
                Recipient = "User", // 返回给原始发送者
                Content = content,
                Type = AgentMessageType.Response,
                Metadata = new Dictionary<string, object>
                {
                    { "SessionId", sessionId },
                    { "SourceAgent", Name },
                    { "IsRouteToUser", true },
                    { "AwaitingConfirmation", true }
                }
            };

            await _messageBus.PublishAsync(userMessage);
        }

        protected virtual async Task<List<ToolResult>> ProcessToolCalls(List<ToolCall> toolCalls, AgentMessage message, string sessionId)
        {
            var toolResults = new List<ToolResult>();

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    bool toolExecuted = false;

                    foreach (var configTool in _config.Tools)
                    {
                        List<string> configToolCallNames = _toolApiService.GetToolsNameByService(configTool);

                        // 检查工具是否在配置的允许列表中
                        if (_config.Tools != null && configToolCallNames.Contains(toolCall.Function.Name))
                        {
                            // 添加Session信息到参数中
                            var arguments = toolCall.Function.ArgumentsObject ?? new Dictionary<string, object>();
                            arguments["SessionId"] = sessionId;
                            arguments["Sender"] = message.Sender;

                            // 执行工具
                            var toolResult = _toolService.ExecuteTool(
                                toolCall.Function.Name,
                                arguments);

                            // 创建ToolResult对象
                            var result = new ToolResult
                            {
                                ToolCallId = toolCall.Id,
                                ToolName = toolCall.Function.Name,
                                Content = toolResult?.ToString() ?? "工具执行完成",
                                IsSuccess = true
                            };

                            toolResults.Add(result);

                            // 保存工具执行结果到Session状态
                            UpdateSessionState(sessionId, new Dictionary<string, object>
                    {
                        { $"Tool_{toolCall.Function.Name}_Result", toolResult },
                        { $"Tool_{toolCall.Function.Name}_Time", DateTime.Now }
                    });

                            toolExecuted = true;
                            break; // 找到匹配的工具后跳出内层循环
                        }
                    }

                    // 如果没有找到匹配的工具
                    if (!toolExecuted)
                    {
                        var errorResult = new ToolResult
                        {
                            ToolCallId = toolCall.Id,
                            ToolName = toolCall.Function.Name,
                            Content = $"工具 '{toolCall.Function.Name}' 未在配置中找到或未启用",
                            IsSuccess = false
                        };

                        toolResults.Add(errorResult);

                        UpdateSessionState(sessionId, new Dictionary<string, object>
                {
                    { $"Tool_{toolCall.Function.Name}_Error", "工具未配置" }
                });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"工具执行错误: {ex.Message}");

                    var errorResult = new ToolResult
                    {
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Function.Name,
                        Content = $"工具执行错误: {ex.Message}",
                        IsSuccess = false
                    };

                    toolResults.Add(errorResult);

                    UpdateSessionState(sessionId, new Dictionary<string, object>
            {
                { $"Tool_{toolCall.Function.Name}_Error", ex.Message }
            });
                }
            }

            return toolResults;
        }
        public override async Task<ToolCallResponse> ProcessAsync(AgentMessage message)
        {
            // 检查发送者是否被允许
            if (_config.AllowedSenders != null && _config.AllowedSenders.Count > 0)
            {
                if (!_config.AllowedSenders.Contains(message.Sender))
                {
                    return new ToolCallResponse
                    {
                        Success = false,
                        Content = $"Agent {Name} 不接受来自 {message.Sender} 的消息",
                        Metadata = new Dictionary<string, object>
                        {
                            { "Error", "SenderNotAllowed" },
                            { "AllowedSenders", _config.AllowedSenders }
                        }
                    };
                }
            }

            // 记录消息历史
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            // 处理消息
            var response = await ProcessWithTools(message);

            // 创建响应消息
            var responseMessage = new AgentMessage
            {
                Sender = Name,
                Recipient = message.Sender,
                Content = response.Content,
                Type = AgentMessageType.TaskResponse,
                Metadata = response.Metadata ?? new Dictionary<string, object>()
            };

            // 确保SessionId在元数据中
            if (!responseMessage.Metadata.ContainsKey("SessionId"))
            {
                responseMessage.Metadata["SessionId"] = GetSessionId(message);
            }

            _messageHistory.Add(responseMessage);
            RaiseMessageSent(responseMessage);

            return response;
        }

        public override async Task StartAsync()
        {
            _subscription = _messageBus.Subscribe(Name)
                .Subscribe(async message =>
                {
                    if (message.Type == AgentMessageType.TaskRequest)
                    {
                        await ProcessAsync(message);
                    }
                });

            // 启动Session清理定时器（每5分钟清理一次）
            _sessionCleanupTimer = new Timer(_ =>
            {
                CleanupExpiredSessions();
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            await Task.CompletedTask;
        }

        public AgentConfig GetAgentConfig()
        {
            return _config;
        }

        public override Task StopAsync()
        {
            _subscription?.Dispose();

            // 停止Session清理定时器
            _sessionCleanupTimer?.Dispose();
            _sessionCleanupTimer = null;

            // 清理所有Session（不保存到文件）
            _messageHistory.Clear();
            _sessions.Clear();
            _sessionStates.Clear();
            _sessionsAwaitingConfirmation.Clear();

            return Task.CompletedTask;
        }

    }

    #region 辅助类和枚举



    #endregion
}