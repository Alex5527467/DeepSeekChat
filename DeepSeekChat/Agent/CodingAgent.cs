using DeepSeekChat.Services;
using DeepSeekChat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static DeepSeekChat.Services.ToolApiService;
using System.Windows.Interop;
using AgentApp1.Models;
using Newtonsoft.Json.Serialization;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace DeepSeekChat.Agent
{
    public class CodingAgent : Agent
    {
        private readonly ToolService _toolService;
        private readonly IChatService _chatService;
        private readonly IToolApiService _toolApiService;

        // 定义根路径常量，方便重复使用和修改
        private string ROOT_PATH;

        private IDisposable _subscription;
        private readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1); // 添加信号量控制并发
        // 添加对话历史记录
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();

        public CodingAgent(IMessageBus messageBus,
                          ToolService toolService,
                          IChatService chatService,
                          IToolApiService toolApiService,
                          IConfiguration configuration,
                          CancellationToken cancellationToken = default)
            : base("CodingAgent", "专业程序员", messageBus, cancellationToken)
        {
            _toolService = toolService;
            _chatService = chatService;
            _toolApiService = toolApiService;

            ROOT_PATH = configuration["ProjectSettings:projectPath"];
        }

        public override async Task StartAsync()
        {
            _subscription = _messageBus.Subscribe(Name)
                .Subscribe(async message =>
                {
                    // 等待当前处理完成
                    await _processingLock.WaitAsync();

                    try
                    {
                        // 保存接收到的消息
                        _messageHistory.Add(message);

                        // 触发消息接收事件
                        RaiseMessageReceived(message);

                        if (message.Type == AgentMessageType.CodingRequest)
                        {
                            var response = await ProcessCodingTask(message);

                            // 保存发送的消息
                            var responseMessage = new AgentMessage
                            {
                                Sender = Name,
                                Recipient = message.Recipient,
                                Content = response.Content,
                                Type = AgentMessageType.TaskResponse
                            };
                            _messageHistory.Add(responseMessage);

                            // 触发消息发送事件
                            RaiseMessageSent(responseMessage);
                        }
                        else
                        {
                            _ = ReportError($"不支持的消息类型");
                        }
                    }
                    finally
                    {
                        // 释放锁，允许处理下一个消息
                        _processingLock.Release();
                    }

                });

            await Task.CompletedTask;
        }

        private async Task<AgentResponse> ProcessCodingTask(AgentMessage message)
        {
            try
            {
                List<TaskDefinition> tasks = RestoreProjectBatchFromJson(message.Content);

                string projectName = GetProjectName(tasks);

                string readContent = CreateDependenciesFileReadContent(tasks);
                string readDependFileData = string.Empty;
                if (!string.IsNullOrEmpty(readContent))
                {
                    // 是否需要读取
                    readDependFileData = await NeededReadOtherDependFileAsync(readContent, projectName);
                }

                string taskContent = CreateProjectStructureFromBatch(tasks);
                // 编写代码
                var codingResult = await CodeFileAsync(taskContent, readDependFileData, projectName);

                if (codingResult.Success)
                {
                    await ReportSuccess($"代码生成成功！");

                    return new AgentResponse
                    {
                        Success = true,
                        Content = $"代码已经创建成功",
                    };
                }
                return new AgentResponse
                {
                    Success = true,
                    Content = $"代码创建失败",
                };
            }
            catch (Exception ex)
            {
                await ReportError($"处理编码任务时发生错误: {ex.Message}");
                return new AgentResponse
                {
                    Success = true,
                    Content = $"代码创建失败",
                };
            }
        }

        public List<TaskDefinition> RestoreProjectBatchFromJson(string json)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    // 属性名不区分大小写
                    ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new CamelCaseNamingStrategy
                        {
                            ProcessDictionaryKeys = true,
                            OverrideSpecifiedNames = true
                        }
                    },
                    // 或者直接使用 CamelCasePropertyNamesContractResolver
                    // ContractResolver = new CamelCasePropertyNamesContractResolver(),

                    // 忽略大小写匹配
                    MissingMemberHandling = MissingMemberHandling.Ignore,

                    // 空值处理
                    NullValueHandling = NullValueHandling.Ignore,

                    // 日期时间处理
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,

                    // 允许注释
                    // CommentHandling = CommentHandling.Ignore
                };

                var projectBatch = JsonConvert.DeserializeObject<List<TaskDefinition>>(json, settings);
                return projectBatch; // 注意：需要根据实际 ProjectBatch 类的结构调整
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                throw new InvalidOperationException("JSON解析失败，请检查JSON格式是否正确。", ex);
            }
        }

        private string CreateProjectStructureFromBatch(List<TaskDefinition> batch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【文件创建任务】");
            sb.AppendLine();
            sb.AppendLine($"批次ID：{Guid.NewGuid().ToString()}");
            sb.AppendLine($"总任务数：{batch.Count}");
            sb.AppendLine();
            sb.AppendLine("任务清单：");

            int taskNumber = 1;
            foreach (var task in batch)
            {
                sb.AppendLine($"{taskNumber}. 文件创建任务：");
                sb.AppendLine($"   - 文件名：{task.FileName}");
                sb.AppendLine($"   - 文件路径：{task.FilePath}");
                sb.AppendLine($"   - 功能说明：{task.Function}");
                sb.AppendLine($"   - 前置依赖：{(task.Dependencies != null && task.Dependencies.Any() ? $"需要先读取 {string.Join("、", task.Dependencies)} 文件的内容" : "无")}");
                sb.AppendLine($"   - 技术要求：{(task.TechnologyRequirements != null && task.TechnologyRequirements.Any() ? $"{string.Join("、", task.TechnologyRequirements)}（技术栈）" : "无")}");
                sb.AppendLine($"   - 复杂度评估：{task.EstimatedComplexity}");
                sb.AppendLine($"   - 特殊需求：{task.Requirements}");
                sb.AppendLine();
                taskNumber++;
            }

            sb.AppendLine("任务指令：");
            sb.AppendLine("请根据每个任务的具体描述编写代码并创建相应文件");
            sb.AppendLine("严格按照文件路径和文件名要求生成");
            sb.AppendLine("确保满足所有功能需求和技术要求");
            sb.AppendLine("考虑依赖文件的读取和处理");

            return sb.ToString();
        }

        private string CreateDependenciesFileReadContent(List<TaskDefinition> batch)
        {
            // 检查batch是否为null或空
            if (batch == null || !batch.Any())
            {
                return string.Empty;
            }

            // 使用LINQ检查是否有任何任务包含依赖
            bool hasAnyDependency = batch.Any(task =>
                task.Dependencies != null && task.Dependencies.Any());

            // 如果所有任务都没有依赖，返回空字符串
            if (!hasAnyDependency)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("【文件读取任务】");
            sb.AppendLine("任务清单：");

            int taskNumber = 1;
            foreach (var task in batch)
            {
                sb.AppendLine($"{taskNumber}. 文件读取任务：");
                sb.AppendLine($"   - 读取文件：{(task.Dependencies != null && task.Dependencies.Any() ? $"需要先读取 {string.Join("、", task.Dependencies)} 文件的内容" : "无")}");
                taskNumber++;
            }
            return sb.ToString();
        }

        private string GetProjectName(List<TaskDefinition> batch)
        {
            // 检查batch是否为null或空
            if (batch == null || !batch.Any())
            {
                return string.Empty;
            }

            var firstTask = batch.FirstOrDefault();

            if (firstTask == null)
            {
                return string.Empty;
            }

            return firstTask.ProjectName;
        }
        private async Task<ToolCallResponse> CodeFileAsync(string content, string readDependFileData, string projectName)
        {

            var folderStructure = _toolService.ExecuteTool("get_folder_structure_description", (ROOT_PATH+ $"\\{projectName}"));
            var conversation = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = $"你是一名资深C#开发工程师，专注于项目代码编写。\n\n" +
            
                             $"【项目根目录】\n{ROOT_PATH}\\{projectName}\n\n" +
            
                             $"【目录结构】\n{folderStructure}\n\n" +
            
                             "【核心规则】\n" +
                             "1. 所有文件路径必须基于上述根目录转换为绝对路径\n" +
                             "2. 调用 create_file 时，file_name 参数必须是完整绝对路径\n" +
                             "3. 路径示例：\"./utils/helper.cs\" → \"" + ROOT_PATH.Replace(@"\", @"\\") + "\\utils\\helper.cs\"\n" +
                             "4. 你可以直接覆盖现有文件\n" +
            
                             "5. **代码注释规则（重要）**：\n" +
                             "   - 不要写任何注释\n" +
            
                             "【工作流程】\n" +
                             "1. 分析需求，确定要创建/修改的文件\n" +
                             "2. 将相对路径转换为绝对路径\n" +
                             "3. **生成简洁代码，最小化注释**\n" +
                             "4. 用绝对路径调用 create_file\n\n" +
            
                             "【重要提醒】\n" +
                             "- 所有文件操作必须使用绝对路径\n" +
                             "- 代码简洁：优先清晰的命名，注释仅用于必要的解释\n" +
                             "- 不要使用相对路径调用 create_file\n" +
                             "- **生成的代码应保持专业但简洁，避免冗余注释**"
                },
                new ChatMessage
                {
                      Role = "user",
                      Content = content + (string.IsNullOrEmpty(readDependFileData) ? "" : $"\n\n【已读取的依赖文件内容】\n{readDependFileData}")
                }
            };
            await ReportSuccess($"CodingAgent =======> DeepSeekAPI：{JsonConvert.SerializeObject(conversation)}");

            var response = await _toolApiService.SendToolRequestAsync(conversation, ToolApiType.FileCreate);

            // 检查是否需要工具调用
            if (response.HasToolCalls && response.ToolCalls != null && response.ToolCalls.Count > 0)
            {
                // 处理工具调用
                ProcessCreateFileToolCalls(response.ToolCalls);
            }

            return new ToolCallResponse()
            {
                Content = $"工具调用请求成功",
                Success = true
            }; 
        }

        private async Task<string> NeededReadOtherDependFileAsync(string msg, string projectName)
        {
            var folderStructure = _toolService.ExecuteTool("get_folder_structure_description", (ROOT_PATH + $"\\{projectName}"));
            var conversation = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = "# 文件读取策略 - 强制多文件模式 #\n" +
                              $"项目根目录\n{ROOT_PATH}\\{projectName}\n\n" +
                             "目录结构已知（已提供）。\n\n" +
                             "## 核心规则 ##\n" +
                             "1. 当你需要读取多个文件时，必须在单个响应中提交所有文件的读取请求\n" +
                             "2. 不允许使用多个对话轮次来读取文件\n" +
                             "3. 不允许说\"先读取X，再读取Y\" - 直接同时请求\n" +
                             "4. 响应必须包含tool_calls数组，其中每个元素是一个read_file调用\n\n" +
                             "## 目录结构参考 ##\n" +
                             folderStructure + "\n\n" +
                             "## 路径规则 ##\n" +
                             "- 使用绝对路径\n" +
                             "- 根目录：" + ROOT_PATH.Replace(@"\", @"\\") + "\n" +
                             "- 示例：'./src/XmlValidatorTool/Interfaces/IXmlValidator.cs' -> '" + ROOT_PATH.Replace(@"\", @"\\") + @"\src\XmlValidatorTool\Interfaces\IXmlValidator.cs" + "'\n\n" +
                             "## 违反规则的后果 ##\n" +
                             "如果你分批请求文件，我将无法正确处理，会要求你重新发送包含所有文件请求的响应。"
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = "任务：" + msg + "\n\n" +
                             "要求：\n" +
                             "1. 分析需要读取哪些文件\n" +
                             "2. 在一个响应中同时请求所有文件\n" +
                             "3. 不要添加任何解释性文本\n" +
                             "4. 直接返回包含多个tool_calls的响应"
                }
            };
            await ReportSuccess($"CodingAgent =======> DeepSeekAPI：{JsonConvert.SerializeObject(conversation)}");

            var response = await _toolApiService.SendToolRequestAsync(conversation, ToolApiType.FileRead);

            string readFileData = "";
            // 检查是否需要工具调用
            if (response.HasToolCalls && response.ToolCalls != null && response.ToolCalls.Count > 0)
            {
                // 处理工具调用
                readFileData += ProcessReadFileToolCalls(response.ToolCalls);

            }
            return readFileData;
        }



        private string ProcessReadFileToolCalls(List<ToolCall> toolCalls)
        {
            StringBuilder result = new StringBuilder();
            foreach (var toolCall in toolCalls)
            {
                try
                {
                    if (toolCall.Function.Name == "read_file")
                    {
                        var toolResult = _toolService.ExecuteTool(toolCall.Function.Name, toolCall.Function.Arguments);

                        if (toolResult is FileReadResult fileReadResult)
                        {
                            result.AppendLine($"文件名: {fileReadResult.FilePath}");
                            result.AppendLine("文件内容:");
                            result.AppendLine("----------------------------------------");
                            result.AppendLine(fileReadResult.Content);
                            result.AppendLine("----------------------------------------");
                            result.AppendLine(); // 添加空行分隔
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = ReportError(ex.Message);
                }
            }

            return result.ToString();
        }


        private void ProcessCreateFileToolCalls(List<ToolCall> toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
                try
                {
                    if (toolCall.Function.Name == "create_file")
                    {
                        var toolResult = _toolService.ExecuteTool(toolCall.Function.Name, toolCall.Function.Arguments);
                        var toolJson = JsonConvert.SerializeObject(toolResult);
                    }
                }
                catch (Exception ex)
                {
                    _ = ReportError(ex.Message);
                }
            }

        }

        private async Task ReportSuccess(string message)
        {
            await _messageBus.PublishAsync(new AgentMessage
            {
                Sender = Name,
                Recipient = "User",
                Content = message,
                Type = AgentMessageType.FolderRefresh,
            });
        }

        private async Task ReportError(string error)
        {
            await _messageBus.PublishAsync(new AgentMessage
            {
                Sender = Name,
                Recipient = "User",
                Content = error,
                Type = AgentMessageType.HelpRequest,
            });
        }

        public override async Task<AgentResponse> ProcessAsync(AgentMessage message)
        {
            try
            {
                // 保存接收到的消息
                _messageHistory.Add(message);
                RaiseMessageReceived(message);

                if (message.Type == AgentMessageType.TaskRequest)
                {
                    var response = await ProcessCodingTask(message);

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

                    return response;
                }

                return new AgentResponse { Success = false, Content = "不支持的消息类型" };
            }
            catch (Exception ex)
            {
                return new AgentResponse { Success = false, Content = ex.Message };
            }
        }

        public override Task StopAsync()
        {
            _subscription?.Dispose();
            return Task.CompletedTask;
        }
    }

}