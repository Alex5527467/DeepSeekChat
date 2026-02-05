using DeepSeekChat.Models;
using DeepSeekChat.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static DeepSeekChat.Services.ToolApiService;

namespace DeepSeekChat.Agent
{
    public class ReviewHandleAgent : Agent
    {
        private readonly IToolApiService _toolApiService;
        private readonly IToolService _toolService;
        private readonly IChatService _chatService;
        private IDisposable _subscription;
        private string ROOT_PATH;

        // 添加对话历史记录
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();

        public ReviewHandleAgent(IMessageBus messageBus,
                          IChatService chatService,
                          IToolApiService toolApiService,
                          IToolService toolService,
                          IConfiguration configuration,
                          CancellationToken cancellationToken = default)
            : base("ReviewHandleAgent", "代码审查对应者", messageBus, cancellationToken)
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
                    if (message.Type == AgentMessageType.TaskRequest)
                    {
                        // 保存接收到的消息
                        _messageHistory.Add(message);

                        // 触发消息接收事件
                        RaiseMessageReceived(message);

                        // 处理设计任务
                        var response = await ProcessReviewHandleTask(message);

                        // 保存发送的消息
                        var responseMessage = new AgentMessage
                        {
                            Sender = Name,
                            Recipient = "User",
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

        private async Task<AgentResponse> ProcessReviewHandleTask(AgentMessage message)
        {
            string projectName = message.Metadata["ProjectName"].ToString();
            string projectPath = Path.Combine(ROOT_PATH, projectName);

            if (string.IsNullOrEmpty(projectName))
            {
                return new AgentResponse
                {
                    Success = true,
                    Content = "需要用户提供修改工程的名称",
                };
            }

            List<string> fileNames = (List<string>)(message.Metadata["ReviewFileNames"]);
            if (fileNames.Count == 0)
            {
                return new AgentResponse
                {
                    Success = true,
                    Content = "需要用户提供修改文件的列表",
                };
            }

            // 收集所有文件内容和路径
            var fileContents = new Dictionary<string, (string content, string path)>();

            foreach (var fileName in fileNames)
            {
                var foundFiles = Directory.GetFiles(projectPath, fileName, SearchOption.AllDirectories);

                if (foundFiles.Length == 0)
                {
                    await ReportError($"未找到文件: {fileName}");
                }
                else
                {
                    string filePath = foundFiles.First();
                    try
                    {
                        var content = await File.ReadAllTextAsync(filePath);
                        fileContents[fileName] = (content, filePath);
                        ReportSuccess($"已读取文件: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        await ReportError($"读取文件失败 {fileName}: {ex.Message}");
                    }
                }
            }

            if (fileContents.Count > 0)
            {
                var result = await ReviewHandleFilesAsync(message.Content, fileContents);
                return new AgentResponse
                {
                    Success = true,
                    Content = result
                };
            }
            else
            {
                return new AgentResponse
                {
                    Success = false,
                    Content = "没有找到任何可修改的文件"
                };
            }
        }
        private async Task<string> ReviewHandleFilesAsync(
            string userRequirements,
            Dictionary<string, (string content, string path)> fileContents)
        {
            var filesInfo = new StringBuilder();
            var isMultipleFiles = fileContents.Count > 1;

            if (isMultipleFiles)
            {
                filesInfo.AppendLine("【需要同时处理的关联文件列表】");
                filesInfo.AppendLine("注意：以下文件是相互关联的，请确保修改的一致性");
                filesInfo.AppendLine();

                int fileIndex = 1;
                foreach (var kvp in fileContents)
                {
                    filesInfo.AppendLine($"=== 文件{fileIndex}: {Path.GetFileName(kvp.Value.path)} ===");
                    filesInfo.AppendLine($"完整路径: {kvp.Value.path}");
                    filesInfo.AppendLine($"内容:");
                    filesInfo.AppendLine(kvp.Value.content);
                    filesInfo.AppendLine();
                    fileIndex++;
                }
            }
            else
            {
                var (content, path) = fileContents.Values.First();
                filesInfo.AppendLine($"【需要处理的文件】");
                filesInfo.AppendLine($"文件路径: {path}");
                filesInfo.AppendLine($"文件内容:");
                filesInfo.AppendLine(content);
            }

            var conversation = new List<ChatMessage>
    {
        new ChatMessage
        {
            Role = "system",
            Content = @$"你是一名代码审查对应者，负责根据用户要求修改代码。

                        {filesInfo}
                        
                        【核心规则】
                        1. 调用 write_file 时，file_name 参数必须是完整绝对路径
                        2. 如果处理多个文件，这些文件都是相互关联的，需要确保修改的一致性
                        
                        【工作流程】
                        1. 分析所有文件内容和相互关系
                        2. 根据用户要求制定完整的修改方案
                        3. 逐一调用 write_file 工具修改每个文件
                        4. 修改完成后输出 '所有文件修改完成'
                        
                        【重要提示】
                        1. 你最多可以调用 {fileContents.Count} 次 write_file 工具
                        2. 每次调用只能修改一个文件
                        3. 请确保修改所有相关文件后再结束
                        
                        【输出要求】
                        完成所有修改后，请回复：'所有文件修改完成'"
        },
        new ChatMessage
        {
            Role = "user",
            Content = $@"【用户修改要求】
                        {userRequirements}

                        请根据上述要求修改所有相关文件，确保代码质量和一致性。"
        }
    };

            // 多轮对话处理
            int maxIterations = fileContents.Count + 3; // 最多尝试次数
            int processedFiles = 0;
            bool allFilesProcessed = false;

            for (int i = 0; i < maxIterations && !allFilesProcessed; i++)
            {
                var response = await _toolApiService.SendToolRequestAsync(conversation, ToolApiType.FileWrite);

                if (response.HasToolCalls && response.ToolCalls != null && response.ToolCalls.Count > 0)
                {
                    await ProcessFileToolCallsAsync(response.ToolCalls);
                    processedFiles += response.ToolCalls.Count;

                    // 添加AI的响应到对话历史
                    var assistantMessage = new ChatMessage
                    {
                        Role = "assistant",
                        Content = response.Content,
                        ToolCalls = response.ToolCalls.Select(tc => new ToolCall
                        {
                            Id = tc.Id,
                            Function = new FunctionCall
                            {
                                Name = tc.Function.Name,
                                Arguments = tc.Function.Arguments
                            }
                        }).ToList()
                    };
                    conversation.Add(assistantMessage);

                    // 添加工具执行结果到对话历史
                    var toolResultMessage = new ChatMessage
                    {
                        Role = "tool",
                        Content = $"{response.ToolCalls.Count} 个文件已成功修改",
                        ToolCallId = response.ToolCalls.First().Id
                    };
                    conversation.Add(toolResultMessage);

                    // 检查是否还有未处理的文件
                    if (processedFiles >= fileContents.Count)
                    {
                        allFilesProcessed = true;
                        // 最后一次请求，让AI确认完成
                        conversation.Add(new ChatMessage
                        {
                            Role = "user",
                            Content = "请确认是否已完成所有文件的修改。如果已完成，请回复'所有文件修改完成'"
                        });
                    }
                    else
                    {
                        // 继续请求修改剩余文件
                        conversation.Add(new ChatMessage
                        {
                            Role = "user",
                            Content = $"已修改 {processedFiles} 个文件，还有 {fileContents.Count - processedFiles} 个文件需要修改。请继续处理剩余文件。"
                        });
                    }
                }
                else
                {

                    allFilesProcessed = true;
                    break;

                    //// AI没有调用工具，检查是否表示已完成
                    //if (response.Content?.Contains("所有文件修改完成") == true)
                    //{
                    //    allFilesProcessed = true;
                    //    break;
                    //}

                    //// 否则继续对话
                    //conversation.Add(new ChatMessage
                    //{
                    //    Role = "assistant",
                    //    Content = response.Content
                    //});

                    //conversation.Add(new ChatMessage
                    //{
                    //    Role = "user",
                    //    Content = $"请继续处理剩余 {fileContents.Count - processedFiles} 个文件。"
                    //});
                }
            }
            string result = allFilesProcessed ?
                $"{processedFiles} 个文件已全部修改完成" :
                $"已修改 {processedFiles} 个文件，可能还有文件未处理";
            ReportSuccess(result);
            return result;
        }

        private async Task ProcessFileToolCallsAsync(List<ToolCall> toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
                try
                {
                    if (toolCall.Function.Name == "write_file")
                    {
                        // 如果是异步工具服务
                        if (_toolService is IToolService asyncToolService)
                        {
                            var toolResult = await asyncToolService.ExecuteToolAsync(toolCall.Function.Name, toolCall.Function.Arguments);
                            var toolJson = JsonConvert.SerializeObject(toolResult);

                            // 记录成功
                            var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(toolCall.Function.Arguments);
                            if (args != null && args.ContainsKey("file_path"))
                            {
                                var fileName = Path.GetFileName(args["file_path"]);
                                await ReportSuccess($"已修改文件: {fileName}");
                            }
                        }
                        else
                        {
                            // 同步工具服务的异步包装
                            var toolResult = await Task.Run(() =>
                                _toolService.ExecuteTool(toolCall.Function.Name, toolCall.Function.Arguments));

                            var toolJson = JsonConvert.SerializeObject(toolResult);

                            // 记录成功
                            var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(toolCall.Function.Arguments);
                            if (args != null && args.ContainsKey("file_name"))
                            {
                                var fileName = Path.GetFileName(args["file_name"]);
                                await ReportSuccess($"已修改文件: {fileName}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await ReportError($"工具调用失败: {ex.Message}");
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
                Type = AgentMessageType.TaskResponse,
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
            // 保存接收到的消息
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            var response = await ProcessReviewHandleTask(message);

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
}