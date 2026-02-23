// ToolApiService.cs - 专门处理工具调用
using DeepSeekChat.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static DeepSeekChat.Services.ToolApiService;

namespace DeepSeekChat.Services
{
    public class ToolApiService : IToolApiService
    {
        // 工具服务名称常量定义
        public static class ToolServices
        {
            public const string FileRead = "FileRead";
            public const string FileCreate = "FileCreate";
            public const string FileWrite = "FileWrite";
            public const string FileSystem = "FileSystem";
            public const string Compile = "Compile";
            public const string FolderBrowser = "FolderBrowser";
            public const string CommandLine = "CommandLine";
        }

        private readonly HttpClient _httpClient;
        private readonly string _logFilePath;
        private const string ApiUrl = "https://api.deepseek.com/chat/completions";

        public ToolApiService(string apiKey, string logFilePath = null)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // 设置日志文件路径
            _logFilePath = logFilePath ?? GetDefaultLogFilePath();

            // 确保日志目录存在
            EnsureLogDirectory();

            LogInfo($"ToolApiService 初始化完成，日志文件: {_logFilePath}");
        }

        public async Task<ToolCallResponse> SendToolRequestAsync(List<ChatMessage> conversationHistory, List<string> toolServices)
        {
            LogInfo($"开始处理工具调用请求");
            LogInfo($"传入的工具服务: {string.Join(", ", toolServices ?? new List<string>())}");
            LogInfo($"对话历史消息数: {conversationHistory?.Count ?? 0}");

            // 如果没有指定工具服务，返回空工具列表
            if (toolServices == null || toolServices.Count == 0)
            {
                LogInfo("未指定工具服务，使用空工具列表");
                return await SendToolRequestAsync(conversationHistory, new List<object>());
            }

            // 收集所有选择的工具
            var allTools = new List<object>();

            foreach (var toolService in toolServices.Distinct())
            {
                var tools = GetToolsByService(toolService);
                if (tools != null)
                {
                    allTools.AddRange(tools);
                    LogInfo($"添加工具服务 '{toolService}'，包含 {tools.Count} 个工具");
                }
                else
                {
                    LogWarning($"未找到工具服务: {toolService}");
                }
            }

            LogInfo($"总计添加 {allTools.Count} 个工具");
            return await SendToolRequestAsync(conversationHistory, allTools);
        }

        // 私有方法：使用具体的工具列表发送请求
        private async Task<ToolCallResponse> SendToolRequestAsync(List<ChatMessage> conversationHistory, List<object> tools)
        {
            LogInfo($"开始发送API请求，工具数量: {tools.Count}");

            var requestData = new ChatRequest
            {
                Messages = conversationHistory,
                MaxTokens = 5000,
                Temperature = 0.3,
                Tools = tools,
                ToolChoice = tools.Count > 0 ? "auto" : "none"
            };

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                },
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented // 为日志美化JSON
            };

            var jsonContent = JsonConvert.SerializeObject(requestData, settings);

            // 记录发送给API的请求内容
            LogRequest(jsonContent, tools.Count > 0);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                LogInfo($"正在发送请求到 DeepSeek API: {ApiUrl}");
                var response = await _httpClient.PostAsync(ApiUrl, content);

                LogInfo($"收到API响应，状态码: {(int)response.StatusCode} {response.StatusCode}");
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();

                // 记录API返回的内容
                LogResponse(responseJson, (int)response.StatusCode);

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
                        result.ToolCalls = message.ToolCalls;
                        LogInfo($"API返回了 {message.ToolCalls.Count} 个工具调用");
                        for (int i = 0; i < message.ToolCalls.Count; i++)
                        {
                            var toolCall = message.ToolCalls[i];
                            LogInfo($"工具调用 #{i + 1}: {toolCall.Function?.Name}");
                        }
                    }
                    else
                    {
                        LogInfo($"API返回了文本回复，长度: {result.Content.Length} 字符");
                    }
                }
                else
                {
                    result.Content = "没有生成有效的回复。";
                    result.Success = false;
                    LogWarning("API返回了空响应或无有效选择");
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                LogError($"工具调用请求失败: {ex.Message}");
                return new ToolCallResponse
                {
                    Content = $"工具调用请求失败: {ex.Message}",
                    Success = false
                };
            }
            catch (Exception ex)
            {
                LogError($"处理API响应时发生错误: {ex.Message}");
                return new ToolCallResponse
                {
                    Content = $"处理API响应时发生错误: {ex.Message}",
                    Success = false
                };
            }
        }

        // 根据服务名称获取对应的工具列表
        private List<object> GetToolsByService(string toolService)
        {
            LogDebug($"获取工具服务: {toolService}");
            return toolService switch
            {
                ToolServices.FileRead => GetFileReadTools(),
                ToolServices.FileCreate => GetFileCreateTools(),
                ToolServices.FileWrite => GetFileWriteTools(),
                ToolServices.FolderBrowser => GetFolderBrowserTools(),
                ToolServices.CommandLine => GetCommandLineTools(),
                _ => null
            };
        }

        public List<string> GetToolsNameByService(string toolService)
        {
            LogDebug($"获取工具名称: {toolService}");
            return toolService switch
            {
                ToolServices.FileRead => ["read_file"],
                ToolServices.FileCreate => ["create_file"],
                ToolServices.FileWrite => ["write_file"],
                ToolServices.FolderBrowser => ["get_folder_structure_description"],
                ToolServices.CommandLine => ["execute_command"],
                _ => [""]
            };
        }

        #region 日志记录方法

        private string GetDefaultLogFilePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDir = Path.Combine(appDataPath, "DeepSeekChat", "Logs");
            var timestamp = DateTime.Now.ToString("yyyyMMdd");
            return Path.Combine(logDir, $"tool_api_{timestamp}.log");
        }

        private void EnsureLogDirectory()
        {
            var logDir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
        }

        private void LogRequest(string jsonContent, bool hasTools)
        {
            try
            {
                var logEntry = new StringBuilder();
                logEntry.AppendLine("=".PadRight(80, '='));
                logEntry.AppendLine($"请求时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                logEntry.AppendLine($"目标URL: {ApiUrl}");
                logEntry.AppendLine($"包含工具: {(hasTools ? "是" : "否")}");
                logEntry.AppendLine("请求内容:");
                logEntry.AppendLine(jsonContent);
                logEntry.AppendLine();

                File.AppendAllText(_logFilePath, logEntry.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无法写入日志文件: {ex.Message}");
            }
        }

        private void LogResponse(string jsonContent, int statusCode)
        {
            try
            {
                var logEntry = new StringBuilder();
                logEntry.AppendLine($"响应时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                logEntry.AppendLine($"状态码: {statusCode}");
                logEntry.AppendLine("响应内容:");

                // 尝试美化JSON响应
                try
                {
                    var jsonObject = JsonConvert.DeserializeObject(jsonContent);
                    var formattedJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                    logEntry.AppendLine(formattedJson);
                }
                catch
                {
                    logEntry.AppendLine(jsonContent);
                }

                logEntry.AppendLine("=".PadRight(80, '='));
                logEntry.AppendLine();

                File.AppendAllText(_logFilePath, logEntry.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无法写入日志文件: {ex.Message}");
            }
        }

        private void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        private void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        private void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        private void LogDebug(string message)
        {
            WriteLog("DEBUG", message);
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"{timestamp} [{level}] {message}";

                File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);

                // 同时输出到控制台以便调试
                Console.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无法写入日志文件: {ex.Message}");
            }
        }

        #endregion

        #region 工具定义方法

        private List<object> GetFolderInfoTools()
        {
            return new List<object>
            {
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
                }
            };
        }

        private List<object> DeleteFileTools()
        {
            return new List<object>
            {
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

        private List<object> GetFileWriteTools()
        {
            return new List<object>
            {
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
                                mode = new
                                {
                                    type = "string",
                                    description = "写入模式：create=仅创建新文件（文件存在则失败）、overwrite=覆盖文件（文件存在则覆盖）、append=在文件末尾追加内容，默认为create",
                                    @enum = new[] { "create", "overwrite", "append" }
                                }
                            },
                            required = new[] { "file_path", "content" ,"mode"}
                        }
                    }
                }
            };
        }

        private List<object> GetFolderBrowserTools()
        {
            return new List<object>
    {
        new
        {
            type = "function",
            function = new
            {
                name = "get_folder_structure_description",
                description = "浏览本地文件夹，获取文件列表，可以过滤特定文件类型并排除指定文件夹",
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
                            description = "要过滤的文件扩展名列表，如：['.txt', '.pdf', '.jpg']，只返回这些扩展名的文件"
                        },
                        exclude_paths = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "要排除的文件夹名称或路径列表，如：['node_modules', 'bin', 'obj', '.git']。这些文件夹及其所有内容将被忽略"
                        }
                    },
                    required = new[] { "folder_path" }
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

        private List<object> GetCommandLineTools()
        {
            return new List<object>
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "execute_command",
                        description = "执行Windows命令行程序或系统命令。注意：show_window参数只在wait_for_exit为false时有效，当wait_for_exit为true时始终不显示窗口",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                command = new
                                {
                                    type = "string",
                                    description = "要执行的命令，如：ipconfig、dir、ping 127.0.0.1 等"
                                },
                                arguments = new
                                {
                                    type = "string",
                                    description = "命令行参数，默认为空字符串"
                                },
                                working_directory = new
                                {
                                    type = "string",
                                    description = "命令执行的工作目录，默认为当前程序运行目录"
                                },
                                wait_for_exit = new
                                {
                                    type = "boolean",
                                    description = "是否等待命令执行完成并获取输出。如果为true，则必须等待执行完成并捕获输出信息（此时不会显示控制台窗口）；如果为false，则立即返回进程ID（此时可以通过show_window控制是否显示窗口）",
                                    @default = true
                                },
                                timeout_seconds = new
                                {
                                    type = "number",
                                    description = "命令执行超时时间（秒），默认为300秒。仅在wait_for_exit为true时有效",
                                    @default = 300
                                },
                                show_window = new
                                {
                                    type = "boolean",
                                    description = "是否显示控制台窗口。仅在wait_for_exit为false时有效：为true时显示窗口，为false时后台运行。当wait_for_exit为true时此参数无效，始终不显示窗口",
                                    @default = false
                                }
                            },
                            required = new[] { "command" }
                        }
                    }
                }
            };
        }
        #endregion

        public void Dispose()
        {
            LogInfo("ToolApiService 正在释放资源");
            _httpClient?.Dispose();
        }
    }

    public interface IToolApiService : IDisposable
    {
        Task<ToolCallResponse> SendToolRequestAsync(List<ChatMessage> conversationHistory, List<string> toolServices);
    }
}