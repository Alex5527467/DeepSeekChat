using DeepSeekChat.Models;
using DeepSeekChat.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DeepSeekChat.Services.ToolApiService;

namespace DeepSeekChat.Agent
{
    public class UserIntentAnalysisAgent : Agent
    {
        private readonly ToolApiService _toolApiService;
        private readonly ToolService _toolService;
        private IDisposable _subscription;

        // 添加对话历史记录
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();

        public UserIntentAnalysisAgent(IMessageBus messageBus,
                          ToolApiService toolApiService,
                          ToolService toolService,
                          CancellationToken cancellationToken = default)
            : base("UserIntentAnalysisAgent", "用户目的分析师", messageBus, cancellationToken)
        {
            _toolApiService = toolApiService;
            _toolService = toolService;
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

                        // 处理用户目的分析任务
                        var response = await ProcessUserIntentAnalysisTask(message);

                        // 保存发送的消息
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

        private async Task<AgentResponse> ProcessUserIntentAnalysisTask(AgentMessage message)
        {

            // 构建包含原始需求的完整上下文
            var analysisPrompt = $@"
            请分析用户的以下需求，判断其属于哪种类型：
            
            用户原始需求：{message.Content}
            
            ## 需求类型分类：
            1. **新建工具需求**：用户明确要求创建、开发、编写一个新的工具/功能/系统
               - 关键词：创建、开发、编写、新建、制作、实现
               - 示例：我想做一个xxx工具、帮我开发一个xxx系统
            
            2. * *修改现有工具需求 * *：用户要求修改、优化、改进已存在的工具
               - 关键词：修改、改进、优化、调整、更新、修复
               - 示例：现有的xxx工具需要改进、修改一下xxx功能
            
            3. * *代码审查相关 * *：用户提交代码要求审查、检查、评审
               - 关键词：审查、检查、评审、review、代码质量
               - 示例：帮我review一下这段代码、检查代码问题
            
            5. * *其他需求 * *：不属于以上任何类别，或无法明确分类的需求
            
            ## 分析步骤：
            1.仔细理解用户需求的核心意图
            2.匹配关键词和需求模式
            3.确定最符合的分类
            4.根据分类选择正确的路由
            
            ## 路由规则：
            - 如果属于 * *新建工具需求 * * → 路由到 RequirementAnalysisAgent
            -如果属于 * *代码审查相关 * * → 路由到 ReviewAgent
            -如果属于 * *修改现有工具需求  * * → 路由到 ReviewAgent
            -如果属于 * *其他需求 * *或 * *无法确定分类 * * → 路由到 User 并说明原因

            特别处理规则：
            -当确定需求属于""新建工具需求""、""代码审查相关""或""修改现有工具需求""时：

            -不要对用户需求进行任何处理或回答
            -直接复述用户的原始输入内容
            -不要添加任何分析、建议或解释
            -只有当需求属于""其他需求""或无法确定分类时，才正常回复用户并说明原因
            ";
            
            var conversation = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = "你是用户目的分析师，可以分析出用户目的。"
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = analysisPrompt
                }
            };

            var response = await _toolApiService.SendToolRequestAsync(conversation, ToolApiType.UserNeedsAnalysis);

            // 检查是否需要工具调用
            if (response.HasToolCalls && response.ToolCalls != null && response.ToolCalls.Count > 0)
            {
                // 处理工具调用
                await ProcessToolCalls(response.ToolCalls);

            }
            return new AgentResponse
            {
                Success = response.Success,
                Content = response.Success ? "用户目的已分析" : "用户目的分析失败",
            };
        }

        private Task ProcessToolCalls(List<ToolCall> toolCalls)
        {

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    var toolResult = _toolService.ExecuteTool(toolCall.Function.Name, toolCall.Function.Arguments);
                    var toolJson = JsonConvert.SerializeObject(toolResult);

                    var toolResponse = new ChatMessage
                    {
                        Role = "tool",
                        Content = toolJson,
                        ToolCallId = toolCall.Id,
                        Name = toolCall.Function.Name
                    };

                }
                catch (Exception ex)
                {
                }
            }

            return Task.CompletedTask;
        }


        public override async Task<AgentResponse> ProcessAsync(AgentMessage message)
        {
            // 保存接收到的消息
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            var response = await ProcessUserIntentAnalysisTask(message);

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
