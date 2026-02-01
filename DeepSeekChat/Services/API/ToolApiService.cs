// ToolApiService.cs - 专门处理工具调用
using DeepSeekChat.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static DeepSeekChat.Services.ToolApiService;

namespace DeepSeekChat.Services
{
    public class ToolApiService : IToolApiService
    {
        public enum ToolApiType
        {
            FileRead,
            FileCreate,
            FileSystem,
            TaskManager,
            Compile
        }
        private readonly HttpClient _httpClient;
        private const string ApiUrl = "https://api.deepseek.com/chat/completions";

        public ToolApiService(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<ToolCallResponse> SendToolRequestAsync(List<ChatMessage> conversationHistory, ToolApiType toolApiType)
        {
            List<Object> tools = new List<Object>();
            switch(toolApiType)
            {
                case ToolApiType.FileRead:
                    tools = GetFileReadTools();
                    break;
                case ToolApiType.FileCreate:
                    tools = GetFileCreateTools();
                    break;
                case ToolApiType.TaskManager:
                    tools = GetTaskCreationTools();
                    break;
                case ToolApiType.Compile:
                    tools = GetCompileTools();
                    break;
                default:
                    break;
            }
            var requestData = new ChatRequest
            {
                Messages = conversationHistory,
                MaxTokens = 5000,
                Temperature = 0.7,
                Tools = tools,
                ToolChoice = "auto"
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

                var result = new ToolCallResponse();

                if (apiResponse?.Choices?.Count > 0)
                {
                    var message = apiResponse.Choices[0].Message;

                    result.Content = message.Content ?? string.Empty;
                    result.ToolCalls = message.ToolCalls;
                    result.HasToolCalls = message.ToolCalls != null && message.ToolCalls.Count > 0;
                    result.Success = true;

                    if (result.HasToolCalls)
                    {
                        // 提取工具调用信息
                        result.ToolCallInfos = ExtractToolCallInfos(message.ToolCalls);
                    }
                }
                else
                {
                    result.Content = "没有生成有效的回复。";
                    result.Success = false;
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                return new ToolCallResponse
                {
                    Content = $"工具调用请求失败: {ex.Message}",
                    Success = false
                };
            }
        }

        private List<ToolCallInfo> ExtractToolCallInfos(List<ToolCall> toolCalls)
        {
            var infos = new List<ToolCallInfo>();

            foreach (var toolCall in toolCalls)
            {
                var info = new ToolCallInfo
                {
                    Id = toolCall.Id,
                    FunctionName = toolCall.Function?.Name,
                    Arguments = toolCall.Function?.Arguments
                };
                infos.Add(info);
            }

            return infos;
        }

        private List<object> GetFileSystemTools()
        {
            return new List<object>
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "browse_local_folder",
                        description = "浏览本地文件夹，获取文件列表",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                folder_path = new
                                {
                                    type = "string",
                                    description = "要浏览的文件夹完整路径，如：C:\\Users\\Username\\Documents"
                                },
                                include_subdirectories = new
                                {
                                    type = "boolean",
                                    description = "是否包含子目录，默认为false"
                                },
                                filter_extensions = new
                                {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "要过滤的文件扩展名列表，如：[.txt, .pdf, .jpg]"
                                }
                            },
                            required = new[] { "folder_path" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_folder_info",
                        description = "获取文件夹详细信息，包括文件数量、大小统计、常见文件类型等",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                folder_path = new
                                {
                                    type = "string",
                                    description = "要获取信息的文件夹完整路径，如：C:\\Users\\Username\\Documents"
                                }
                            },
                            required = new[] { "folder_path" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "create_file",
                        description = "创建新文件",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                file_path = new
                                {
                                    type = "string",
                                    description = "要创建的文件完整路径，如：C:\\Users\\Username\\Documents\\new_file.txt"
                                },
                                content = new
                                {
                                    type = "string",
                                    description = "文件内容，默认为空字符串"
                                },
                                overwrite = new
                                {
                                    type = "boolean",
                                    description = "是否覆盖已存在的文件，默认为false"
                                }
                            },
                            required = new[] { "file_path" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "delete_file",
                        description = "删除文件",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                file_path = new
                                {
                                    type = "string",
                                    description = "要删除的文件完整路径，如：C:\\Users\\Username\\Documents\\file_to_delete.txt"
                                }
                            },
                            required = new[] { "file_path" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "read_file",
                        description = "读取文件内容",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                file_path = new
                                {
                                    type = "string",
                                    description = "要读取的文件完整路径，如：C:\\Users\\Username\\Documents\\file_to_read.txt"
                                },
                                encoding = new
                                {
                                    type = "string",
                                    description = "文件编码格式，支持：UTF-8、ASCII、Unicode、UTF-32、UTF-7、BigEndianUnicode、Default，默认为UTF-8"
                                },
                                max_size_bytes = new
                                {
                                    type = "integer",
                                    description = "最大读取文件大小（字节），超过此大小的文件将不会被读取，默认为10MB (10485760字节)"
                                }
                            },
                            required = new[] { "file_path" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "write_file",
                        description = "写入文件内容",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                file_path = new
                                {
                                    type = "string",
                                    description = "要写入的文件完整路径，如：C:\\Users\\Username\\Documents\\file_to_write.txt"
                                },
                                content = new
                                {
                                    type = "string",
                                    description = "要写入的文件内容"
                                },
                                encoding = new
                                {
                                    type = "string",
                                    description = "文件编码格式，支持：UTF-8、ASCII、Unicode、UTF-32、UTF-7、BigEndianUnicode、Default，默认为UTF-8"
                                },
                                append = new
                                {
                                    type = "boolean",
                                    description = "是否以追加模式写入，true=在文件末尾追加内容，false=覆盖整个文件，默认为false"
                                }
                            },
                            required = new[] { "file_path", "content" }
                        }
                    }
                }
            };
        }

        private List<object> GetFileCreateTools()
        {
            return new List<object>
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "create_file",
                        description = "创建新文件",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                file_path = new
                                {
                                    type = "string",
                                    description = "要创建的文件完整路径，如：C:\\Users\\Username\\Documents\\new_file.txt"
                                },
                                content = new
                                {
                                    type = "string",
                                    description = "文件内容，默认为空字符串"
                                },
                                overwrite = new
                                {
                                    type = "boolean",
                                    description = "是否覆盖已存在的文件，默认为false"
                                }
                            },
                            required = new[] { "file_path" }
                        }
                    }
                }
            };
        }

        private List<object> GetFileReadTools()
        {
            return new List<object>
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "read_file",
                        description = "读取文件内容",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                file_path = new
                                {
                                    type = "string",
                                    description = "要读取的文件完整路径，如：C:\\Users\\Username\\Documents\\file_to_read.txt"
                                },
                                encoding = new
                                {
                                    type = "string",
                                    description = "文件编码格式，支持：UTF-8、ASCII、Unicode、UTF-32、UTF-7、BigEndianUnicode、Default，默认为UTF-8"
                                },
                                max_size_bytes = new
                                {
                                    type = "integer",
                                    description = "最大读取文件大小（字节），超过此大小的文件将不会被读取，默认为10MB (10485760字节)"
                                }
                            },
                            required = new[] { "file_path" }
                        }
                    }
                }
            };
        }

        private List<object> GetCompileTools()
        {
            return new List<object>
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "compile_csharp",
                        description = "编译C#代码并可选地执行程序。支持生成可执行文件或在内存中直接执行代码。",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                code = new
                                {
                                    type = "string",
                                    description = "要编译的C#源代码。必须是完整的程序，包含Main方法或入口点。"
                                },
                                outputPath = new
                                {
                                    type = "string",
                                    description = "编译输出的程序集文件路径。如果不指定，则仅在内存中编译。示例：C:\\Output\\Program.dll"
                                },
                                execute = new
                                {
                                    type = "boolean",
                                    description = "是否在编译后立即执行程序。如果为true，将在内存中执行并返回执行结果。默认为false。"
                                },
                                includeReferences = new
                                {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "额外的程序集引用路径列表。示例：[\"Newtonsoft.Json.dll\", \"System.Data.dll\"]"
                                },
                                targetFramework = new
                                {
                                    type = "string",
                                    description = "目标框架版本。支持：net8.0。默认为当前运行时版本。"
                                }
                            },
                            required = new[] { "code" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "compile_and_execute",
                        description = "编译C#代码并立即执行，返回执行结果。适用于快速测试代码片段。",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                code = new
                                {
                                    type = "string",
                                    description = "要编译执行的C#源代码。必须包含Main方法。示例：'using System; class Program { static void Main() { Console.WriteLine(\"Hello\"); } }'"
                                },
                                output_path = new
                                {
                                    type = "string",
                                    description = "编译输出的程序集文件路径。如果不指定，则仅在内存中编译和执行。示例：C:\\Temp\\MyProgram.dll"
                                },
                                arguments = new
                                {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "传递给Main方法的命令行参数。示例：[\"arg1\", \"arg2\"]"
                                }
                            },
                            required = new[] { "code" }
                        }
                    }
                }
            };
        }


        public List<object> GetTaskCreationTools()
        {
            // 构建 TaskDefinition 的 JSON Schema 属性
            var taskDefinitionProperties = new Dictionary<string, object>
            {
                ["task_id"] = new
                {
                    type = "string",
                    description = "任务ID，建议使用GUID"
                },
                ["project_name"] = new
                {
                    type = "string",
                    description = "工程文件名"
                },
                ["file_name"] = new
                {
                    type = "string",
                    description = "文件名"
                },
                ["file_path"] = new
                {
                    type = "string",
                    description = "文件路径"
                },
                ["function"] = new
                {
                    type = "string",
                    description = "文件功能描述"
                },
                ["dependencies"] = new
                {
                    type = "array",
                    description = "依赖的文件名列表",
                    items = new { type = "string" }
                },
                ["requirements"] = new
                {
                    type = "string",
                    description = "创建该文件的具体要求"
                },
                ["estimated_complexity"] = new
                {
                    type = "string",
                    description = "估计复杂度",
                    @enum = new[] { "low", "medium", "high" }
                },
                ["technology_requirements"] = new
                {
                    type = "array",
                    description = "技术需求列表",
                    items = new { type = "string" }
                },
                ["testing_requirements"] = new
                {
                    type = "string",
                    description = "测试要求"
                },
                ["status"] = new
                {
                    type = "string",
                    description = "任务状态",
                    @default = "pending"
                },
                ["assigned_to"] = new
                {
                    type = "string",
                    description = "分配给谁",
                    @default = "CodingAgent"
                }
            };

            // 任务定义所需的必填字段
            var requiredTaskProperties = new[] {
                "task_id","project_name", "file_name", "file_path", "function", "requirements"
            };

                    return new List<object>
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "distribute_coding_tasks",
                        description = "将文件创建和编码任务分配给CodingAgent，输入为任务列表",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                tasks = new
                                {
                                    type = "array",
                                    description = "任务定义列表，符合TaskDefinition格式",
                                    items = new
                                    {
                                        type = "object",
                                        properties = taskDefinitionProperties,
                                        required = requiredTaskProperties
                                    }
                                },
                                distribution_strategy = new
                                {
                                    type = "string",
                                    description = "分配策略",
                                    @enum = new[] {
                                        "sequential_by_dependency",
                                        "parallel_when_possible",
                                        "complexity_based",
                                        "priority_based"
                                    },
                                    @default = "sequential_by_dependency"
                                },
                                execution_order = new
                                {
                                    type = "boolean",
                                    description = "是否返回任务执行顺序建议",
                                    @default = true
                                },
                                batch_size = new
                                {
                                    type = "integer",
                                    description = "批次大小，一次分配多少个任务给CodingAgent",
                                    @default = 1,
                                    minimum = 1,
                                    maximum = 10
                                }
                            },
                            required = new[] { "tasks" }
                        }
                    }
                }
            };
        }
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public interface IToolApiService : IDisposable
    {
        Task<ToolCallResponse> SendToolRequestAsync(List<ChatMessage> conversationHistory, ToolApiType toolApiType);
    }

    // 工具调用响应模型
    public class ToolCallResponse
    {
        public string Content { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
        public List<ToolCallInfo> ToolCallInfos { get; set; }
        public bool HasToolCalls { get; set; }
        public bool Success { get; set; }
    }

    public class ToolCallInfo
    {
        public string Id { get; set; }
        public string FunctionName { get; set; }
        public string Arguments { get; set; }
    }
}