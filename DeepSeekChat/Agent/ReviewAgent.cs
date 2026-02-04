using DeepSeekChat.Models;
using DeepSeekChat.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace DeepSeekChat.Agent
{
    public class ReviewAgent : Agent
    {
        private readonly ToolApiService _toolApiService;
        private readonly ToolService _toolService;
        private readonly ChatService _chartService;
        private IDisposable _subscription;
        private string ROOT_PATH;

        // 添加对话历史记录
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();
        private readonly Dictionary<string, ReviewAnalysisSession> _sessions = new Dictionary<string, ReviewAnalysisSession>();

        public ReviewAgent(IMessageBus messageBus,
                          ChatService chatService,
                          ToolApiService toolApiService,
                          ToolService toolService,
                          IConfiguration configuration,
                          CancellationToken cancellationToken = default)
            : base("ReviewAgent", "代码审查员", messageBus, cancellationToken)
        {
            _toolApiService = toolApiService;
            _toolService = toolService;
            _chartService = chatService;

            ROOT_PATH = configuration["ProjectSettings:projectPath"];
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

                        //处理设计任务
                        var (response, isComplete, sessionId) = await ProcessReviewTask(message);

                        // 设置Metadata用于状态跟踪
                        var metadata = new Dictionary<string, object>
                        {
                            { "SessionId", sessionId },
                            { "RequirementStatus", isComplete ? "Complete" : "Collecting" },
                            { "Timestamp", DateTime.UtcNow }
                        };

                        if (!isComplete)
                        {
                            // 还在收集需求中，返回给用户继续提问
                            var userResponseMessage = new AgentMessage
                            {
                                Sender = Name,
                                Recipient = message.Sender,
                                Content = response.Content,
                                Type = AgentMessageType.TaskResponse,
                                Metadata = metadata
                            };

                            _messageHistory.Add(userResponseMessage);
                            RaiseMessageSent(userResponseMessage);
                        }
                        else // 需求收集完成，发送给设计代理
                        {
                            // 准备发送给设计代理的消息
                            var reviewAgentMetadata = new Dictionary<string, object>
                            {
                                { "SessionId", sessionId },
                                { "OriginalRequester", message.Sender },
                                { "RequirementType", "CompleteAnalysis" },
                                { "AnalysisTime", DateTime.UtcNow },
                                { "ProjectName", response.ProjectName },
                                { "ReviewFileNames", response.FileNames },
                                { "UserInput", response.UserInputs[0] }
                            };

                            var reviewAgentMessage = new AgentMessage
                            {
                                Sender = Name,
                                Recipient = "ReviewHandleAgent",
                                Content = response.Content,
                                Type = AgentMessageType.TaskRequest,
                                Metadata = reviewAgentMetadata
                            };

                            _messageHistory.Add(reviewAgentMessage);
                            RaiseMessageSent(reviewAgentMessage);

                            await _messageBus.PublishAsync(reviewAgentMessage);

                            // 给用户的确认消息
                            var userConfirmMetadata = new Dictionary<string, object>
                            {
                                { "SessionId", sessionId },
                                { "Status", "Complete" },
                                { "NextAgent", "DesignAgent" },
                                { "RequirementSummary", response.Summary }
                            };

                            var userConfirmMessage = new AgentMessage
                            {
                                Sender = Name,
                                Recipient = message.Sender,
                                Content = $"需求分析已完成！\n\n项目名称：{response.ProjectName}\nReview文件名：{string.Join(",",response.FileNames)}\n\n已发送给Review处理Agent进行处理。",
                                Type = AgentMessageType.TaskResponse,
                                Metadata = userConfirmMetadata
                            };

                            _messageHistory.Add(userConfirmMessage);
                            RaiseMessageSent(userConfirmMessage);

                        }
                        
                        
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

        private async Task<(ReviewAnalysisResult result, bool isComplete, string sessionId)> ProcessReviewTask(AgentMessage message)
        {
            // 从Metadata获取或生成sessionId
            string sessionId;
            if (message.Metadata.TryGetValue("SessionId", out object? existingSessionId) && existingSessionId is string)
            {
                sessionId = (string)existingSessionId;
            }
            else
            {
                sessionId = Guid.NewGuid().ToString();
            }

            // 获取或创建会话
            if (!_sessions.ContainsKey(sessionId))
            {
                _sessions[sessionId] = new ReviewAnalysisSession
                {
                    SessionId = sessionId,
                    UserId = message.Sender,
                    StartTime = DateTime.Now,
                    // 如果是新会话，保存用户的原始需求
                    OriginalRequirement = message.Content,  // 保存原始需求
                    HasOriginalRequirement = true,
                    // 从Metadata中获取初始信息（如果有）
                    ProjectName = message.Metadata.TryGetValue("ProjectName", out var projectName)
                        ? projectName.ToString()
                        : string.Empty
                };
            }

            var session = _sessions[sessionId];

            // 如果是新会话的第一个消息，记录为原始需求
            if (!session.HasOriginalRequirement && !session.UserInputs.Any())
            {
                session.OriginalRequirement = message.Content;
                session.HasOriginalRequirement = true;
            }

            // 记录用户输入（无论是原始需求还是后续回答）
            session.UserInputs.Add(new RequirementInput
            {
                Timestamp = DateTime.Now,
                Input = message.Content,
                IsClarifyingQuestion = false,
                IsOriginalRequirement = !session.UserInputs.Any(), // 第一条为原始需求
                SourceMetadata = message.Metadata
            });

            // 解析用户需求并更新会话状态
            await UpdateReviewRequirementSession(session, message.Content, isOriginalRequirement: !session.UserInputs.Any(x => !x.IsOriginalRequirement));

            // 检查是否还需要更多信息
            if (session.IsComplete())
            {
                // 生成最终需求分析结果（包含原始需求）
                var requirementResult = GenerateRequirementResult(session);

                // 清理会话
                _sessions.Remove(sessionId);

                return (requirementResult, true, sessionId);
            }
            else
            {
                // 生成下一个问题（基于原始需求和已收集信息）
                var nextQuestion = GenerateNextQuestion(session);

                session.AgentQuestions.Add(new RequirementInput
                {
                    Timestamp = DateTime.Now,
                    Input = nextQuestion,
                    IsClarifyingQuestion = true
                });

                var collectingResult = new ReviewAnalysisResult
                {
                    SessionId = sessionId,
                    Content = nextQuestion,
                    ProjectName = session.ProjectName,
                    FileNames = session.ReviewFileNames,
                    Status = "Collecting",
                    MissingInformation = GetMissingInformation(session),
                    // 添加原始需求引用
                    OriginalRequirement = session.OriginalRequirement,
                    ConversationHistory = GetConversationSummary(session)
                };

                var agentMessage = new AgentMessage
                {
                    Sender = "RequirementAnalysisAgent",
                    Recipient = "User",
                    Content = nextQuestion,
                    Type = AgentMessageType.TaskResponse
                };

                _ = _messageBus.PublishAsync(agentMessage);

                return (collectingResult, false, sessionId);
            }
        }


        private async Task UpdateReviewRequirementSession(ReviewAnalysisSession session, string userInput, bool isOriginalRequirement = false)
        {
            // 构建包含原始需求的完整上下文
            var analysisPrompt =
            $@"请分析用户的代码审查需求：

用户原始需求：""{session.OriginalRequirement}""

最新输入：""{userInput}""

重要规则：
1. 用户的需求描述（如""追加删除一行的鼠标动作的功能""）是功能描述，不是项目名称
2. 只有在用户明确提到""项目""、""工程""、""project""等关键词时，才提取项目名称
3. 如果用户没有指定具体项目，保持项目名称为空
4. 重点识别需要审查的具体文件名或文件类型

请根据以下JSON格式回复：
{{
  ""ProjectName"": ""（只有用户明确提及时才填写，否则留空）"",
  ""ReviewFileNames"": [""可能的文件名1"", ""可能的文件名2""]
}}

请确保：
- 不要把功能描述当作项目名称
- 文件名应该是具体的，如""UserInterface.cs""、""MouseActionHandler.py""
- 如果没有明确提到文件，ReviewFileNames可以是空数组";

            var conversation = new List<ChatMessage>
    {
        new ChatMessage
        {
            Role = "system",
            Content = "你是代码审查需求分析师，请始终以用户的修改意见为核心，后续对话只是澄清和补充。"
        },
        new ChatMessage
        {
            Role = "user",
            Content = analysisPrompt
        }
    };

            var analysisResult = await _chartService.SendChatRequestAsync(conversation);

            // 解析分析结果并更新会话
            UpdateSessionFromAnalysis(session, analysisResult.Content, userInput);
        }

        private string GetConversationAfterOriginal(ReviewAnalysisSession session, string latestInput)
        {
            var sb = new StringBuilder();

            // 获取原始需求之后的所有用户输入
            var originalInput = session.UserInputs.FirstOrDefault(x => x.IsOriginalRequirement);
            if (originalInput != null)
            {
                // 原始需求之后的对话
                var subsequentInputs = session.UserInputs
                    .Where(x => x.Timestamp > originalInput.Timestamp)
                    .OrderBy(x => x.Timestamp);

                foreach (var input in subsequentInputs)
                {
                    sb.AppendLine($"用户: {input.Input}");

                    // 对应的代理问题
                    var relatedQuestion = session.AgentQuestions
                        .FirstOrDefault(q => Math.Abs((q.Timestamp - input.Timestamp).TotalSeconds) < 10);
                    if (relatedQuestion != null)
                    {
                        sb.AppendLine($"分析师: {relatedQuestion.Input}");
                    }
                }
            }

            // 添加最新的输入
            sb.AppendLine($"用户（最新）: {latestInput}");

            return sb.ToString();
        }

        private string GetCurrentSessionInfo(ReviewAnalysisSession session)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"项目名称: {session.ProjectName}");
            sb.AppendLine($"review文件列表: ");
            foreach (var fileName in session.ReviewFileNames)
            {
                sb.AppendLine($"    {fileName} ");
            }

            return sb.ToString();
        }


        private void UpdateSessionFromAnalysis(ReviewAnalysisSession session, string analysisResult, string originalInput)
        {
            try
            {
                // 尝试解析JSON
                using JsonDocument doc = JsonDocument.Parse(analysisResult);
                var root = doc.RootElement;

                // 更新项目名称
                if (root.TryGetProperty("ProjectName", out var projectNameElement) &&
                    projectNameElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(projectNameElement.GetString()))
                {
                    session.ProjectName = projectNameElement.GetString().Trim();
                }

                // 更新review文件列表
                if (root.TryGetProperty("ReviewFileNames", out var filesElement) &&
                    filesElement.ValueKind == JsonValueKind.Array)
                {
                    session.ReviewFileNames.Clear();
                    foreach (var fileElement in filesElement.EnumerateArray())
                    {
                        if (fileElement.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(fileElement.GetString()))
                        {
                            session.ReviewFileNames.Add(fileElement.GetString().Trim());
                        }
                    }
                }

                // 如果没有解析到项目名称，尝试从原始输入中提取
                if (string.IsNullOrEmpty(session.ProjectName))
                {
                    // 简单的提取逻辑（根据实际情况调整）
                    var projectPatterns = new[] { "项目", "工程", "project", "工程名" };
                    foreach (var pattern in projectPatterns)
                    {
                        if (originalInput.Contains(pattern))
                        {
                            // 提取可能的项目名称
                            var match = Regex.Match(originalInput, @$"{pattern}[：:]\s*(\w+)");
                            if (match.Success)
                            {
                                session.ProjectName = match.Groups[1].Value;
                                break;
                            }
                        }
                    }
                }

                session.LastUpdated = DateTime.Now;
            }
            catch (JsonException)
            {
                // 如果JSON解析失败，尝试其他方式提取信息
                ExtractInfoFromText(session, analysisResult, originalInput);
            }
        }

        private void ExtractInfoFromText(ReviewAnalysisSession session, string text, string originalInput)
        {
            // 简单的文本提取逻辑
            var lines = text.Split('\n');

            foreach (var line in lines)
            {
                // 查找项目名称
                if (line.Contains("项目名称") || line.Contains("项目:"))
                {
                    var parts = line.Split(':', '：');
                    if (parts.Length > 1)
                    {
                        session.ProjectName = parts[1].Trim();
                    }
                }

                // 查找文件列表
                if (line.Contains("文件") || line.Contains("文件名"))
                {
                    // 提取可能的文件名
                    var pattern = @"[\w\.\-]+\.\w{2,4}"; // 简单文件名模式
                    var matches = Regex.Matches(line, pattern);
                    foreach (Match match in matches)
                    {
                        if (!session.ReviewFileNames.Contains(match.Value))
                        {
                            session.ReviewFileNames.Add(match.Value);
                        }
                    }
                }
            }

            session.LastUpdated = DateTime.Now;
        }
        private ReviewAnalysisResult GenerateRequirementResult(ReviewAnalysisSession session)
        {
            var requirementDocument = GenerateReivewDocument(session);
            var summary = GenerateReviewSummary(session);

            return new ReviewAnalysisResult
            {
                SessionId = session.SessionId,
                Content = requirementDocument,
                ProjectName = session.ProjectName,
                FileNames = session.ReviewFileNames,
                Status = "Complete",
                Summary = summary,
                UserInputs = session.UserInputs.Select(i => i.Input).ToList(),
                AgentQuestions = session.AgentQuestions.Select(i => i.Input).ToList()
            };
        }

        private string GenerateReivewDocument(ReviewAnalysisSession session)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== Review需求分析报告 ===");
            sb.AppendLine($": {session.SessionId}");
            sb.AppendLine($"分析时间: {DateTime.Now}");
            sb.AppendLine();

            sb.AppendLine("1. 用户要求");
            sb.AppendLine($"   用户要求: {session.UserInputs[0]?.Input}");

            sb.AppendLine();
            sb.AppendLine("2. 工程");
            sb.AppendLine($"   工程名称: {session.ProjectName}");
            sb.AppendLine($"   Review文件名: ");
            foreach (var fileName in session.ReviewFileNames)
            {
                sb.AppendLine($"        {fileName} ");
            }

            sb.AppendLine();
            sb.AppendLine("3. 用户输入历史");
            foreach (var input in session.UserInputs.TakeLast(5))
            {
                sb.AppendLine($"   [{input.Timestamp:HH:mm:ss}] {input.Input}");
            }

            return sb.ToString();
        }

        private string GenerateReviewSummary(ReviewAnalysisSession session)
        {
            return $"项目名称{session.ProjectName}，Review文件{string.Join(", ", session.ReviewFileNames)}";
        }

        private Dictionary<string, object> GetMissingInformation(ReviewAnalysisSession session)
        {
            var missing = new Dictionary<string, object>();

            if (string.IsNullOrEmpty(session.ProjectName))
                missing["ProjectName"] = "需要确定项目名称";

            if (session.ReviewFileNames.Count == 0)
                missing["ReviewFileNames"] = "需要确定review文件名称列表";


            return missing;
        }

        private string GetConversationSummary(ReviewAnalysisSession session)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== 需求分析对话摘要 ===");
            sb.AppendLine($"会话ID: {session.SessionId}");
            sb.AppendLine($"用户: {session.UserId}");
            sb.AppendLine($"创建时间: {session.StartTime:yyyy-MM-dd HH:mm:ss}");

            // 显示原始需求
            if (!string.IsNullOrEmpty(session.OriginalRequirement))
            {
                sb.AppendLine($"\n原始需求:");
                sb.AppendLine($"  \"{Utils.Utils.TruncateText(session.OriginalRequirement, 200)}\"");
            }

            // 对话历史
            sb.AppendLine($"\n对话历史 ({session.UserInputs.Count} 轮):");

            // 合并用户输入和代理提问，按时间排序
            var allInteractions = new List<RequirementInteraction>();

            // 添加用户输入
            foreach (var input in session.UserInputs)
            {
                allInteractions.Add(new RequirementInteraction
                {
                    Timestamp = input.Timestamp,
                    Role = "用户",
                    Content = input.Input,
                    IsOriginalRequirement = input.IsOriginalRequirement,
                    IsClarifyingQuestion = false
                });
            }

            // 添加代理提问
            foreach (var question in session.AgentQuestions)
            {
                allInteractions.Add(new RequirementInteraction
                {
                    Timestamp = question.Timestamp,
                    Role = "分析师",
                    Content = question.Input,
                    IsClarifyingQuestion = question.IsClarifyingQuestion,
                    IsOriginalRequirement = false
                });
            }

            // 按时间排序
            var sortedInteractions = allInteractions
                .OrderBy(x => x.Timestamp)
                .ToList();

            // 显示对话摘要（最多显示最近5轮）
            int maxDisplayCount = Math.Min(5, sortedInteractions.Count);
            for (int i = 0; i < sortedInteractions.Count; i++)
            {
                var interaction = sortedInteractions[i];

                // 标记原始需求
                string prefix = interaction.IsOriginalRequirement ? "[原始需求] " : "";
                string suffix = interaction.IsClarifyingQuestion ? " [澄清问题]" : "";

                sb.AppendLine($"\n[{interaction.Timestamp:HH:mm:ss}] {interaction.Role}:");
                sb.AppendLine($"  {prefix}{Utils.Utils.TruncateText(interaction.Content, 150)}{suffix}");

                // 如果是原始需求后的对话，显示序号
                if (!interaction.IsOriginalRequirement && i < maxDisplayCount)
                {
                    sb.AppendLine($"  (第{i}轮对话)");
                }
            }

            // 如果对话轮次太多，显示统计信息
            if (sortedInteractions.Count > maxDisplayCount)
            {
                sb.AppendLine($"\n... 还有 {sortedInteractions.Count - maxDisplayCount} 轮对话已省略");
            }

            // 当前确认的信息摘要
            sb.AppendLine($"\n当前已确认信息摘要:");
            sb.AppendLine($"  项目名称: {session.ProjectName}");
            sb.AppendLine($"  Review文件名: ");


            // 缺失信息
            var missingInfo = GetMissingInformation(session);
            if (missingInfo.Any())
            {
                sb.AppendLine($"\n仍需确认的信息:");
                foreach (var missing in missingInfo)
                {
                    sb.AppendLine($"  - {missing.Key}: {missing.Value}");
                }
            }
            else
            {
                sb.AppendLine($"\n所有必要信息已收集完成 ✓");
            }

            return sb.ToString();
        }

        private string GenerateNextQuestion(ReviewAnalysisSession session)
        {
            // 首先检查会话是否为空（新会话）
            if (string.IsNullOrEmpty(session.ProjectName) && session.ReviewFileNames.Count == 0)
            {
                if (session.UserInputs.Count == 1) // 只有原始需求
                {
                    return $"我理解了您的需求：{session.OriginalRequirement}\n\n" +
                           $"为了开始审查，我需要知道：\n" +
                           $"1. 项目名称是什么？\n" +
                           $"2. 您想审查哪些具体的文件？";
                }
                else if (!string.IsNullOrEmpty(session.OriginalRequirement))
                {
                    // 已经对话过，但还是没有信息
                    return $"让我们继续讨论您的需求：{session.OriginalRequirement}\n\n" +
                           $"请问项目名称是什么？";
                }
            }

            if (string.IsNullOrEmpty(session.ProjectName))
            {
                return $"请告诉我项目名称，这样我可以更好地定位需要审查的文件。";
            }

            if (session.ReviewFileNames.Count == 0)
            {
                return $"好的，项目是 {session.ProjectName}。\n" +
                       $"请告诉我需要审查哪些文件？\n" +
                       $"（可以列出具体文件名，或者说'所有文件'、'主要文件'等）";
            }

            return $"基于您的需求，我需要确认：\n" +
                   $"• 项目：{session.ProjectName}\n" +
                   $"• 审查文件：{string.Join(", ", session.ReviewFileNames)}\n\n" +
                   $"还需要审查其他文件吗？或者有特定的审查重点吗？";
        }


        public override async Task<AgentResponse> ProcessAsync(AgentMessage message)
        {
            // 保存接收到的消息
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            //处理设计任务
            var (response, isComplete, sessionId) = await ProcessReviewTask(message);

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

            // 准备响应消息
            var metadata = new Dictionary<string, object>
            {
                { "SessionId", sessionId },
                { "RequirementStatus", isComplete ? "Complete" : "Collecting" },
                { "Timestamp", DateTime.UtcNow }
            };

            return new AgentResponse
            {
                Success = true,
                SessionId = sessionId,
                Content = response.Content,
                NextAgent = isComplete ? "ReviewHandleAgent" : null,
                Metadata = metadata
            };
        }

        public string? GetFirstSessionId()
        {
            // 使用 LINQ 的 FirstOrDefault 方法
            return _sessions.Keys.FirstOrDefault();
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

    public class ReviewAnalysisResult
    {

        public string SessionId { get; set; }
        public string Content { get; set; }
        public string ProjectName { get; set; }
        public List<string> FileNames { get; set; }

        public string Status { get; set; }
        public string Summary { get; set; }
        public List<string> UserInputs { get; set; } = new List<string>();
        public List<string> AgentQuestions { get; set; } = new List<string>();
        public Dictionary<string, object> MissingInformation { get; set; } = new Dictionary<string, object>();
        public string OriginalRequirement { get; set; }
        public string RequirementSummary { get; set; }
        public string ConversationHistory { get; set; }
    }

    // Review分析会话类
    public class ReviewAnalysisSession
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string OriginalRequirement { get; set; }
        public bool HasOriginalRequirement { get; set; }
        // 需求信息
        public string ProjectName { get; set; } = string.Empty;
        public List<string> ReviewFileNames { get; set; } = new List<string>();

        // 对话历史
        public List<RequirementInput> UserInputs { get; set; } = new List<RequirementInput>();
        public List<RequirementInput> AgentQuestions { get; set; } = new List<RequirementInput>();

        public bool IsComplete()
        {
            return !string.IsNullOrEmpty(ProjectName) &&
                   ReviewFileNames.Count() > 0 ;
        }
    }
}
