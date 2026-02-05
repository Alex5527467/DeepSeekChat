using AgentApp1.Models;
using DeepSeekChat.Agent;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeepSeekChat.Services
{
    public class CreateTaskService
    {
        private readonly InMemoryMessageBus _messageBus;
        private readonly List<AgentMessage> _messageHistory;
        public CreateTaskService(InMemoryMessageBus msgBus)
        {
            _messageBus = msgBus;
            _messageHistory = new List<AgentMessage>();
        }

        public async Task<ToolCallResult> DistributeCodingTasksAsync(string tasksJson)
        {
            try
            {
                // 1. 解析 JSON 字符串
                List<TaskDefinition> tasks;
                try
                {
                    var taskResponse = JsonConvert.DeserializeObject<TaskResponse>(
                        tasksJson,
                        new JsonSerializerSettings
                        {
                            // 属性名不区分大小写
                            ContractResolver = new CamelCasePropertyNamesContractResolver(),

                            // 忽略大小写
                            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                            // 如果需要忽略空值
                            NullValueHandling = NullValueHandling.Ignore
                        });

                    tasks = taskResponse.Tasks;
                }
                catch (JsonException ex)
                {
                    return new ToolCallResult
                    {
                        Success = false,
                        Content = $"JSON解析失败: {ex.Message}",
                        Error = "INVALID_JSON_FORMAT"
                    };
                }

                if (tasks == null || !tasks.Any())
                {
                    return new ToolCallResult
                    {
                        Success = false,
                        Content = "任务列表为空或格式不正确",
                        Error = "EMPTY_TASK_LIST"
                    };
                }

                // 1. 根据策略处理任务排序
                var orderedTasks = OrderTasksByStrategy(tasks, "sequential_by_dependency");

                // 2. 分批处理
                var batches = CreateTaskBatches(orderedTasks, 1);

                // 3. 将每批任务分配给 CodingAgent
                var results = new List<DistributionResult>();

                foreach (var batch in batches)
                {
                    var result = await DistributeBatchToCodingAgent(batch);
                    results.Add(result);
                }

                // 4. 返回分配结果
                return new ToolCallResult
                {
                    Success = true,
                    Content = GenerateDistributionReport(results, orderedTasks),
                    Data = results
                };
            }
            catch (Exception ex)
            {
                return new ToolCallResult
                {
                    Success = false,
                    Content = $"任务分配失败: {ex.Message}",
                    Error = ex.Message
                };
            }
        }

        private async Task<DistributionResult> DistributeBatchToCodingAgent(List<TaskDefinition> batch)
        {
            // 创建项目结构描述
            var taskComment = CreateProjectStructureFromBatch(batch);

            // 发送给编码 Agent
            var codingMessage = new AgentMessage
            {
                Sender = "TaskManager",
                Recipient = "CodingAgent",
                Content = taskComment,
                Type = AgentMessageType.CodingRequest,
            };

            // 添加到历史
            _messageHistory.Add(codingMessage);

            // 发布到消息总线
            await _messageBus.PublishAsync(codingMessage);

            return new DistributionResult
            {
                BatchId = Guid.NewGuid().ToString(),
                TaskCount = batch.Count,
                Tasks = batch,
                MessageId = codingMessage.Id,
                Timestamp = DateTime.UtcNow,
                Status = "dispatched"
            };
        }


        private string CreateProjectStructureFromBatch(List<TaskDefinition> batch)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(batch, settings);
        }

        private List<TaskDefinition> OrderTasksByStrategy(List<TaskDefinition> tasks, string strategy)
        {
            return strategy switch
            {
                "sequential_by_dependency" => OrderByDependency(tasks),
                "parallel_when_possible" => OrderForParallelExecution(tasks),
                "complexity_based" => OrderByComplexity(tasks),
                "priority_based" => OrderByPriority(tasks),
                _ => OrderByDependency(tasks)
            };
        }

        private List<TaskDefinition> OrderByDependency(List<TaskDefinition> tasks)
        {
            // 根据依赖关系排序（拓扑排序）
            var result = new List<TaskDefinition>();
            var visited = new HashSet<string>();
            var temporaryMarked = new HashSet<string>();

            void Visit(TaskDefinition task)
            {
                if (temporaryMarked.Contains(task.TaskId))
                    throw new InvalidOperationException("存在循环依赖");

                if (visited.Contains(task.TaskId))
                    return;

                temporaryMarked.Add(task.TaskId);

                // 先处理依赖
                foreach (var dependency in task.Dependencies)
                {
                    var depTask = tasks.FirstOrDefault(t => t.FileName == dependency);
                    if (depTask != null)
                        Visit(depTask);
                }

                temporaryMarked.Remove(task.TaskId);
                visited.Add(task.TaskId);
                result.Add(task);
            }

            foreach (var task in tasks)
            {
                if (!visited.Contains(task.TaskId))
                    Visit(task);
            }

            return result;
        }

        private List<List<TaskDefinition>> CreateTaskBatches(List<TaskDefinition> orderedTasks, int batchSize)
        {
            var batches = new List<List<TaskDefinition>>();

            for (int i = 0; i < orderedTasks.Count; i += batchSize)
            {
                var batch = orderedTasks.Skip(i).Take(batchSize).ToList();
                batches.Add(batch);
            }

            return batches;
        }

        private string GenerateDistributionReport(List<DistributionResult> results, List<TaskDefinition> allTasks)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("# 任务分配报告");
            report.AppendLine();
            report.AppendLine($"## 总览");
            report.AppendLine($"- 总任务数: {allTasks.Count}");
            report.AppendLine($"- 分配批次: {results.Count}");
            report.AppendLine($"- 开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            report.AppendLine($"## 批次详情");
            foreach (var result in results)
            {
                report.AppendLine($"### 批次 {results.IndexOf(result) + 1}");
                report.AppendLine($"- 批次ID: {result.BatchId}");
                report.AppendLine($"- 任务数量: {result.TaskCount}");
                report.AppendLine($"- 分配时间: {result.Timestamp:HH:mm:ss}");
                report.AppendLine($"- 状态: {result.Status}");
                report.AppendLine($"- 包含文件:");
                foreach (var task in result.Tasks)
                {
                    report.AppendLine($"  - {task.FilePath}{task.FileName} ({task.EstimatedComplexity})");
                }
                report.AppendLine();
            }

            report.AppendLine($"## 依赖关系图");
            foreach (var task in allTasks)
            {
                if (task.Dependencies.Any())
                {
                    report.AppendLine($"- {task.FileName} 依赖于: {string.Join(", ", task.Dependencies)}");
                }
            }

            return report.ToString();
        }

        private List<TaskDefinition> OrderForParallelExecution(List<TaskDefinition> tasks)
        {
            // 识别可以并行执行的任务（没有依赖关系的任务）
            var independentTasks = tasks.Where(t => !t.Dependencies.Any()).ToList();
            var dependentTasks = tasks.Where(t => t.Dependencies.Any()).ToList();

            // 独立任务可以并行执行，依赖任务按依赖关系排序
            var result = new List<TaskDefinition>();
            result.AddRange(independentTasks);
            result.AddRange(OrderByDependency(dependentTasks));

            return result;
        }

        private List<TaskDefinition> OrderByComplexity(List<TaskDefinition> tasks)
        {
            // 按复杂度排序：low -> medium -> high
            var complexityOrder = new Dictionary<string, int> { { "low", 1 }, { "medium", 2 }, { "high", 3 } };
            return tasks.OrderBy(t => complexityOrder.GetValueOrDefault(t.EstimatedComplexity.ToLower(), 2)).ToList();
        }

        private List<TaskDefinition> OrderByPriority(List<TaskDefinition> tasks)
        {
            // 可以根据业务逻辑添加优先级字段
            return tasks.OrderByDescending(t => t.Dependencies.Count).ToList();
        }


    }

    public class ToolCallResult
    {
        public bool Success { get; set; }
        public string Content { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }
    }

    public class DistributionResult
    {
        public string BatchId { get; set; }
        public int TaskCount { get; set; }
        public List<TaskDefinition> Tasks { get; set; }
        public string MessageId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; }
    }
}