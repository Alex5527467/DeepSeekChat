using DeepSeekChat.Models;
using DeepSeekChat.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.NetworkOperators;

namespace DeepSeekChat.Agent
{
    public class RequirementAnalysisAgent : Agent
    {
        private readonly ChatService _chatService;
        private IDisposable _subscription;

        // 添加对话历史记录和会话状态
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();
        private readonly Dictionary<string, RequirementAnalysisSession> _sessions = new Dictionary<string, RequirementAnalysisSession>();

        public RequirementAnalysisAgent(IMessageBus messageBus,
                          ChatService chatService,
                          CancellationToken cancellationToken = default)
            : base("RequirementAnalysisAgent", "需求分析师", messageBus, cancellationToken)
        {
            _chatService = chatService;
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

                        // 处理需求分析任务
                        var (response, isComplete, sessionId) = await ProcessRequirementAnalysisTask(message);

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
                            var designAgentMetadata = new Dictionary<string, object>
                            {
                                { "SessionId", sessionId },
                                { "OriginalRequester", message.Sender },
                                { "RequirementType", "CompleteAnalysis" },
                                { "AnalysisTime", DateTime.UtcNow },
                                { "ProjectType", response.ProjectType },
                                { "TechnologyStack", response.TechnologyStack },
                                { "Complexity", response.Complexity },
                                { "HasUserInterface", response.HasUserInterface },
                                { "ArchitecturePattern", response.ArchitecturePattern },
                                { "UserInput", response.UserInputs[0] }
                            };

                            var designAgentMessage = new AgentMessage
                            {
                                Sender = Name,
                                Recipient = "DesignAgent",
                                Content = response.Content,
                                Type = AgentMessageType.TaskRequest,
                                Metadata = designAgentMetadata
                            };

                            _messageHistory.Add(designAgentMessage);
                            RaiseMessageSent(designAgentMessage);

                            await _messageBus.PublishAsync(designAgentMessage);

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
                                Content = $"需求分析已完成！\n\n项目类型：{response.ProjectType}\n技术栈：{response.TechnologyStack}\n复杂度：{response.Complexity}\n\n已发送给系统架构师进行设计。",
                                Type = AgentMessageType.TaskResponse,
                                Metadata = userConfirmMetadata
                            };

                            _messageHistory.Add(userConfirmMessage);
                            RaiseMessageSent(userConfirmMessage);
                        }
                    }
                });

            await Task.CompletedTask;
        }

        private async Task<(RequirementAnalysisResult result, bool isComplete, string sessionId)> ProcessRequirementAnalysisTask(AgentMessage message)
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
                _sessions[sessionId] = new RequirementAnalysisSession
                {
                    SessionId = sessionId,
                    UserId = message.Sender,
                    StartTime = DateTime.Now,
                    // 如果是新会话，保存用户的原始需求
                    OriginalRequirement = message.Content,  // 保存原始需求
                    HasOriginalRequirement = true,
                    // 从Metadata中获取初始信息（如果有）
                    ProjectType = message.Metadata.TryGetValue("ProjectTypeHint", out var projectType)
                        ? ParseProjectType(projectType.ToString())
                        : ProjectType.Unknown
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
            await UpdateRequirementSession(session, message.Content, isOriginalRequirement: !session.UserInputs.Any(x => !x.IsOriginalRequirement));

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

                var collectingResult = new RequirementAnalysisResult
                {
                    SessionId = sessionId,
                    Content = nextQuestion,
                    ProjectType = session.ProjectType.ToString(),
                    TechnologyStack = session.TechnologyStack.ToString(),
                    Complexity = session.Complexity.ToString(),
                    HasUserInterface = session.HasUserInterface,
                    ArchitecturePattern = session.ArchitecturePattern,
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

        private string GetConversationSummary(RequirementAnalysisSession session)
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
                sb.AppendLine($"  \"{TruncateText(session.OriginalRequirement, 200)}\"");
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
                sb.AppendLine($"  {prefix}{TruncateText(interaction.Content, 150)}{suffix}");

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
            sb.AppendLine($"  项目类型: {session.ProjectType}");
            sb.AppendLine($"  技术栈: {session.TechnologyStack}");
            sb.AppendLine($"  复杂度: {session.Complexity}");

            if (session.HasUserInterface)
            {
                sb.AppendLine($"  界面类型: {session.ProjectType}");
                sb.AppendLine($"  架构模式: {session.ArchitecturePattern}");
            }

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

        // 辅助方法：截断文本
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }
        private async Task UpdateRequirementSession(RequirementAnalysisSession session, string userInput, bool isOriginalRequirement = false)
        {
            // 构建包含原始需求的完整上下文
            var analysisPrompt = $"""
        请分析以下用户需求对话：
        
        ===== 用户原始需求（请始终参考这个） =====
        {session.OriginalRequirement}
        
        ===== 后续对话（澄清和补充） =====
        {GetConversationAfterOriginal(session, userInput)}
        
        ===== 当前已确认信息 =====
        {GetCurrentSessionInfo(session)}
        
        请基于用户的【原始需求】，结合最新的回答，提取或更新以下信息：
        （特别关注原始需求中提到的核心要求）
        1. 项目类型（控制台程序/界面程序）
        2. 程序复杂度（简单/带模型和接口/复杂分层）
        3. 技术栈（.NET/.NET Framework/其他）
        4. 界面类型（控制台/WPF/WinForms/其他）
        5. 架构模式（是否使用MVVM/MVC等）
        6. 是否需要数据访问层
        7. 是否需要接口抽象
        
        注意：原始需求优先级最高，后续对话只是澄清和补充。
        返回JSON格式。
        """;

            var conversation = new List<ChatMessage>
    {
        new ChatMessage
        {
            Role = "system",
            Content = "你是需求分析师，请始终以用户的最初需求为核心，后续对话只是澄清和补充。"
        },
        new ChatMessage
        {
            Role = "user",
            Content = analysisPrompt
        }
    };

            var analysisResult = await _chatService.SendChatRequestAsync(conversation);

            // 解析分析结果并更新会话
            UpdateSessionFromAnalysis(session, analysisResult.Content, userInput);
        }

        private string GetCurrentSessionInfo(RequirementAnalysisSession session)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"项目类型: {session.ProjectType}");
            sb.AppendLine($"技术栈: {session.TechnologyStack}");
            sb.AppendLine($"复杂度: {session.Complexity}");
            sb.AppendLine($"是否有界面: {session.HasUserInterface}");
            sb.AppendLine($"架构模式: {session.ArchitecturePattern}");
            sb.AppendLine($"需要数据访问层: {session.NeedsDataAccessLayer}");
            sb.AppendLine($"需要接口抽象: {session.NeedsInterfaceAbstraction}");
            sb.AppendLine($"需要ViewModel层: {session.NeedsViewModelLayer}");

            // 添加对话进度
            sb.AppendLine($"\n对话进度:");
            sb.AppendLine($"  用户输入次数: {session.UserInputs.Count}");
            sb.AppendLine($"  代理提问次数: {session.AgentQuestions.Count}");
            sb.AppendLine($"  会话开始时间: {session.StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  最后更新时间: {session.LastUpdated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");

            return sb.ToString();
        }

        private string GetConversationAfterOriginal(RequirementAnalysisSession session, string latestInput)
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
        private void UpdateSessionFromAnalysis(RequirementAnalysisSession session, string analysisResult, string originalInput)
        {
            // 简化处理，实际应该解析JSON
            var inputLower = originalInput.ToLower();

            // 检测项目类型
            if (inputLower.Contains("控制台") || inputLower.Contains("console"))
            {
                session.ProjectType = ProjectType.Console;
                session.HasUserInterface = false;
            }
            else if (inputLower.Contains("wpf") || inputLower.Contains("界面") ||
                     inputLower.Contains("图形界面") || inputLower.Contains("窗口"))
            {
                session.ProjectType = ProjectType.WPF;
                session.HasUserInterface = true;
            }
            else if (inputLower.Contains("winform") || inputLower.Contains("winforms"))
            {
                session.ProjectType = ProjectType.WinForms;
                session.HasUserInterface = true;
            }

            // 检测技术栈
            if (inputLower.Contains(".net framework") || inputLower.Contains("framework"))
            {
                session.TechnologyStack = TechnologyStack.DotNetFramework;
            }
            else if (inputLower.Contains(".net") || inputLower.Contains("core") || inputLower.Contains("5") || inputLower.Contains("6") || inputLower.Contains("7") || inputLower.Contains("8"))
            {
                session.TechnologyStack = TechnologyStack.DotNet;
            }

            // 检测复杂度
            if (inputLower.Contains("简单") || inputLower.Contains("基础") || inputLower.Contains("基本"))
            {
                session.Complexity = ComplexityLevel.Simple;
            }
            else if (inputLower.Contains("模型") || inputLower.Contains("接口") || inputLower.Contains("interface"))
            {
                session.Complexity = ComplexityLevel.WithModelsAndInterfaces;
                session.NeedsDataAccessLayer = true;
                session.NeedsInterfaceAbstraction = true;
            }
            else if (inputLower.Contains("mvvm") || inputLower.Contains("viewmodel") ||
                     inputLower.Contains("复杂") || inputLower.Contains("分层"))
            {
                session.Complexity = ComplexityLevel.ComplexLayered;
                session.ArchitecturePattern = "MVVM";
                session.NeedsDataAccessLayer = true;
                session.NeedsInterfaceAbstraction = true;
                session.NeedsViewModelLayer = true;
            }

            // 检测架构模式
            if (inputLower.Contains("mvvm"))
            {
                session.ArchitecturePattern = "MVVM";
                session.NeedsViewModelLayer = true;
            }
            else if (inputLower.Contains("mvc"))
            {
                session.ArchitecturePattern = "MVC";
            }
        }

        private string GenerateNextQuestion(RequirementAnalysisSession session)
        {
            var missingInfo = new List<string>();

            if (session.ProjectType == ProjectType.Unknown)
            {
                missingInfo.Add("您想要创建什么类型的程序？\n" +
                              "1. 控制台程序\n" +
                              "2. WPF界面程序\n" +
                              "3. WinForms程序");
            }
            else if (session.TechnologyStack == TechnologyStack.Unknown)
            {
                missingInfo.Add($"对于{session.ProjectType}项目，您希望使用哪个技术栈？\n" +
                              "1. .NET Framework\n" +
                              "2. .NET（推荐）");
            }
            else if (session.Complexity == ComplexityLevel.Unknown)
            {
                if (session.ProjectType == ProjectType.Console)
                {
                    missingInfo.Add("控制台程序的复杂度如何？\n" +
                                  "1. 简单的单文件程序\n" +
                                  "2. 包含模型和接口的程序\n" +
                                  "3. 完整的分层架构程序");
                }
                else
                {
                    missingInfo.Add("界面程序的复杂度如何？\n" +
                                  "1. 简单的界面程序\n" +
                                  "2. 包含模型和接口的程序\n" +
                                  "3. 完整的MVVM架构程序");
                }
            }

            if (missingInfo.Count > 0)
            {
                return "为了更准确地分析您的需求，请告诉我：\n\n" + string.Join("\n\n", missingInfo);
            }

            // 如果基本信息都有了，询问更多细节
            return $"基于您的需求（{session.ProjectType}项目，使用{session.TechnologyStack}，复杂度：{session.Complexity}），\n" +
                   "还有其他特定的要求吗？\n" +
                   "例如：是否需要数据库访问、特定的设计模式、第三方库等。";
        }

        private RequirementAnalysisResult GenerateRequirementResult(RequirementAnalysisSession session)
        {
            var requirementDocument = GenerateRequirementDocument(session);
            var summary = GenerateRequirementSummary(session);

            return new RequirementAnalysisResult
            {
                SessionId = session.SessionId,
                Content = requirementDocument,
                ProjectType = session.ProjectType.ToString(),
                TechnologyStack = session.TechnologyStack.ToString(),
                Complexity = session.Complexity.ToString(),
                HasUserInterface = session.HasUserInterface,
                ArchitecturePattern = session.ArchitecturePattern,
                NeedsDataAccessLayer = session.NeedsDataAccessLayer,
                NeedsInterfaceAbstraction = session.NeedsInterfaceAbstraction,
                NeedsViewModelLayer = session.NeedsViewModelLayer,
                TargetFramework = GetTargetFramework(session),
                SuggestedStructure = GetSuggestedProjectStructure(session),
                Status = "Complete",
                Summary = summary,
                UserInputs = session.UserInputs.Select(i => i.Input).ToList(),
                AgentQuestions = session.AgentQuestions.Select(i => i.Input).ToList()
            };
        }

        private string GenerateRequirementDocument(RequirementAnalysisSession session)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== 需求分析报告 ===");
            sb.AppendLine($"会话ID: {session.SessionId}");
            sb.AppendLine($"分析时间: {DateTime.Now}");
            sb.AppendLine();

            sb.AppendLine("1. 项目概述");
            sb.AppendLine($"   用户要求: {session.UserInputs[0]?.Input}");
            sb.AppendLine($"   项目类型: {session.ProjectType}");
            sb.AppendLine($"   技术栈: {session.TechnologyStack}");
            sb.AppendLine($"   复杂度: {session.Complexity}");
            sb.AppendLine($"   是否有界面: {session.HasUserInterface}");

            sb.AppendLine();
            sb.AppendLine("2. 架构需求");
            sb.AppendLine($"   架构模式: {session.ArchitecturePattern}");
            sb.AppendLine($"   需要数据访问层: {session.NeedsDataAccessLayer}");
            sb.AppendLine($"   需要接口抽象: {session.NeedsInterfaceAbstraction}");
            sb.AppendLine($"   需要ViewModel层: {session.NeedsViewModelLayer}");

            sb.AppendLine();
            sb.AppendLine("3. 技术细节");
            sb.AppendLine($"   目标框架: {GetTargetFramework(session)}");
            sb.AppendLine($"   建议的项目结构: {GetSuggestedProjectStructure(session)}");

            sb.AppendLine();
            sb.AppendLine("4. 用户输入历史");
            foreach (var input in session.UserInputs.TakeLast(5))
            {
                sb.AppendLine($"   [{input.Timestamp:HH:mm:ss}] {input.Input}");
            }

            return sb.ToString();
        }

        private string GenerateRequirementSummary(RequirementAnalysisSession session)
        {
            return $"{session.ProjectType}项目，使用{session.TechnologyStack}，{session.Complexity}级别，" +
                   $"{(session.HasUserInterface ? "带界面" : "无界面")}，架构模式：{session.ArchitecturePattern}";
        }

        private Dictionary<string, object> GetMissingInformation(RequirementAnalysisSession session)
        {
            var missing = new Dictionary<string, object>();

            if (session.ProjectType == ProjectType.Unknown)
                missing["ProjectType"] = "需要确定项目类型";

            if (session.TechnologyStack == TechnologyStack.Unknown)
                missing["TechnologyStack"] = "需要确定技术栈";

            if (session.Complexity == ComplexityLevel.Unknown)
                missing["Complexity"] = "需要确定复杂度";

            return missing;
        }

        private string GetTargetFramework(RequirementAnalysisSession session)
        {
            return session.TechnologyStack switch
            {
                TechnologyStack.DotNet => "net8.0",
                TechnologyStack.DotNetFramework => "net48",
                _ => "net8.0"
            };
        }

        private string GetSuggestedProjectStructure(RequirementAnalysisSession session)
        {
            if (session.ProjectType == ProjectType.Console)
            {
                return session.Complexity switch
                {
                    ComplexityLevel.Simple => "单项目，简单分层",
                    ComplexityLevel.WithModelsAndInterfaces => "项目分Models、Interfaces、Services层",
                    ComplexityLevel.ComplexLayered => "完整分层：Core、Application、Infrastructure",
                    _ => "标准控制台项目结构"
                };
            }
            else
            {
                return session.Complexity switch
                {
                    ComplexityLevel.Simple => "单项目，代码后置模式",
                    ComplexityLevel.WithModelsAndInterfaces => "项目分Models、Views、ViewModels层",
                    ComplexityLevel.ComplexLayered => "完整MVVM架构：Models、Views、ViewModels、Services",
                    _ => "标准WPF/WinForms项目结构"
                };
            }
        }

        private ProjectType ParseProjectType(string typeString)
        {
            return typeString.ToLower() switch
            {
                "console" => ProjectType.Console,
                "wpf" => ProjectType.WPF,
                "winforms" => ProjectType.WinForms,
                _ => ProjectType.Unknown
            };
        }

        public override async Task<AgentResponse> ProcessAsync(AgentMessage message)
        {
            // 保存接收到的消息
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            var (result, isComplete, sessionId) = await ProcessRequirementAnalysisTask(message);

            // 准备响应消息
            var metadata = new Dictionary<string, object>
            {
                { "SessionId", sessionId },
                { "RequirementStatus", isComplete ? "Complete" : "Collecting" },
                { "Timestamp", DateTime.UtcNow }
            };

            if (isComplete)
            {
                metadata["NextAgent"] = "DesignAgent";
                metadata["ProjectType"] = result.ProjectType;
                metadata["TechnologyStack"] = result.TechnologyStack;
                metadata["Complexity"] = result.Complexity;
            }

            var responseMessage = new AgentMessage
            {
                Sender = Name,
                Recipient = message.Sender,
                Content = result.Content,
                Type = AgentMessageType.TaskResponse,
                Metadata = metadata
            };

            _messageHistory.Add(responseMessage);
            RaiseMessageSent(responseMessage);

            return new AgentResponse
            {
                Success = true,
                SessionId = sessionId,
                Content = result.Content,
                NextAgent = isComplete ? "DesignAgent" : null,
                Metadata = metadata
            };
        }

        // 添加获取消息历史的方法
        public IReadOnlyList<AgentMessage> GetMessageHistory()
        {
            return _messageHistory.AsReadOnly();
        }

        // 获取会话状态
        public Dictionary<string, object> GetSessionStatus(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                return new Dictionary<string, object>
                {
                    { "SessionId", session.SessionId },
                    { "UserId", session.UserId },
                    { "ProjectType", session.ProjectType.ToString() },
                    { "TechnologyStack", session.TechnologyStack.ToString() },
                    { "Complexity", session.Complexity.ToString() },
                    { "IsComplete", session.IsComplete() },
                    { "UserInputCount", session.UserInputs.Count },
                    { "AgentQuestionCount", session.AgentQuestions.Count }
                };
            }

            return new Dictionary<string, object> { { "Status", "SessionNotFound" } };
        }

        public string? GetFirstSessionId()
        {
            // 使用 LINQ 的 FirstOrDefault 方法
            return _sessions.Keys.FirstOrDefault();
        }

        public override Task StopAsync()
        {
            _subscription?.Dispose();
            _messageHistory.Clear();
            _sessions.Clear();
            return Task.CompletedTask;
        }
    }

    // 需求分析会话类
    public class RequirementAnalysisSession
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string OriginalRequirement { get; set; }
        public bool HasOriginalRequirement { get; set; }
        // 需求信息
        public ProjectType ProjectType { get; set; } = ProjectType.Unknown;
        public TechnologyStack TechnologyStack { get; set; } = TechnologyStack.Unknown;
        public ComplexityLevel Complexity { get; set; } = ComplexityLevel.Unknown;
        public string ArchitecturePattern { get; set; } = "标准结构";

        // 功能需求
        public bool HasUserInterface { get; set; }
        public bool NeedsDataAccessLayer { get; set; }
        public bool NeedsInterfaceAbstraction { get; set; }
        public bool NeedsViewModelLayer { get; set; }

        // 对话历史
        public List<RequirementInput> UserInputs { get; set; } = new List<RequirementInput>();
        public List<RequirementInput> AgentQuestions { get; set; } = new List<RequirementInput>();

        public bool IsComplete()
        {
            return ProjectType != ProjectType.Unknown &&
                   TechnologyStack != TechnologyStack.Unknown &&
                   Complexity != ComplexityLevel.Unknown &&
                   (ProjectType != ProjectType.WPF || !string.IsNullOrEmpty(ArchitecturePattern));
        }
    }

    // 输入记录
    public class RequirementInput
    {
        public DateTime Timestamp { get; set; }
        public string Input { get; set; }
        public bool IsClarifyingQuestion { get; set; }
        public bool IsOriginalRequirement { get; set; }
        public Dictionary<string, object> SourceMetadata { get; set; } = new Dictionary<string, object>();
    }

    // 需求分析结果类
    public class RequirementAnalysisResult
    {
        public string SessionId { get; set; }
        public string Content { get; set; }
        public string ProjectType { get; set; }
        public string TechnologyStack { get; set; }
        public string Complexity { get; set; }
        public bool HasUserInterface { get; set; }
        public string ArchitecturePattern { get; set; }
        public bool NeedsDataAccessLayer { get; set; }
        public bool NeedsInterfaceAbstraction { get; set; }
        public bool NeedsViewModelLayer { get; set; }
        public string TargetFramework { get; set; }
        public string SuggestedStructure { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public List<string> UserInputs { get; set; } = new List<string>();
        public List<string> AgentQuestions { get; set; } = new List<string>();
        public Dictionary<string, object> MissingInformation { get; set; } = new Dictionary<string, object>();
        public string OriginalRequirement { get; set; }
        public string RequirementSummary { get; set; }
        public string ConversationHistory { get; set; }

    }

    // 枚举定义
    public enum ProjectType
    {
        Unknown,
        Console,
        WPF,
        WinForms,
        Web,
        Other
    }

    public enum TechnologyStack
    {
        Unknown,
        DotNet,
        DotNetFramework,
        Other
    }

    public enum ComplexityLevel
    {
        Unknown,
        Simple,
        WithModelsAndInterfaces,
        ComplexLayered
    }
}