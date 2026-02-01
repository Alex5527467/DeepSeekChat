using DeepSeekChat.Models;
using DeepSeekChat.Services;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeekChat.Agent
{
    public class DesignAgent : Agent
    {
        private readonly ChatService _chatService;
        private IDisposable _subscription;

        // 添加对话历史记录
        private readonly List<AgentMessage> _messageHistory = new List<AgentMessage>();

        public DesignAgent(IMessageBus messageBus,
                          ChatService chatService,
                          CancellationToken cancellationToken = default)
            : base("DesignAgent", "系统架构师", messageBus, cancellationToken)
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

                        // 处理设计任务
                        var response = await ProcessDesignTask(message);

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

        private async Task<AgentResponse> ProcessDesignTask(AgentMessage message)
        {
            // 使用 ChatService 创建项目结构
            var projectStructure = await CreateProjectStructureAsync(message);

            var taskMessage = new AgentMessage
            {
                Sender = Name,
                Recipient = "TaskCreateAgent",
                Content = projectStructure,
                Type = AgentMessageType.TaskRequest
            };

            _messageHistory.Add(taskMessage);
            RaiseMessageSent(taskMessage);

            await _messageBus.PublishAsync(taskMessage);

            return new AgentResponse
            {
                Success = true,
                Content = $"设计方案已生成，并发送给任务管理者",
                NextAgent = "TaskCreateAgent"
            };
        }

        private async Task<string> CreateProjectStructureAsync(AgentMessage agentMessage)
        {
            // 判断设计复杂度并选择合适的提示词
            var complexity = DetermineComplexity(agentMessage.Metadata);

            string technologyStackContent = $" 使用框架.NET8.0";
            if(agentMessage.Metadata["TechnologyStack"].ToString() == "DotNetFramework")
            {
                technologyStackContent = $"使用框架.NETFramework 4.8";
            }

            string prompt = complexity switch
            {
                ProjectComplexity.SimpleConsole => CreateSimpleConsolePrompt(),
                ProjectComplexity.ComplexConsole => CreateComplexToolPrompt(),
                ProjectComplexity.SimpleWpf => CreateSimpleWpfPrompt(),
                ProjectComplexity.ComplexWpf => CreateComplexWpfPrompt(),
                _ => CreateComplexWpfPrompt() // 默认使用复杂模式
            };

            var conversation = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = prompt
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = agentMessage.Metadata["UserInput"].ToString() + technologyStackContent
                }
            };

            var response = await _chatService.SendChatRequestAsync(conversation);
            return response.Success ? response.Content : "生成项目结构失败";
        }

        // 1. 极简控制台项目提示词
        private string CreateSimpleConsolePrompt()
        {
            return @"你是一个专业的C#项目结构规划师，使用Visual Studio 2022。请根据用户需求的复杂度自动生成项目结构，遵循以下规则：

            **适用场景：**
            极简项目：只有基础逻辑，无需分层 → 所有逻辑集中在Program.cs
            
            **返回格式要求：**
            必须按照文件优先级顺序，以编号列表形式展示每个文件，每个文件包含以下信息：
            1. [文件名], 功能：[简短功能描述], 路径：[相对路径], 依赖：[依赖的文件名], 附加说明：[相关说明]，包含：[此文件包含的具体逻辑描述]
            
            **示例格式：**
            1. Program.cs, 功能：程序入口与逻辑, 路径：./SimpleCalculator/, 依赖：, 附加说明:控制台应用程序, 包含：主方法、用户输入处理、计算逻辑、结果输出
            2. SimpleCalculator.csproj, 功能：项目文件, 路径：./SimpleCalculator/, 依赖：, 附加说明:控制台应用程序, 包含：项目配置
            
            **逻辑分配指南：**
            1. 所有业务逻辑集中在Program.cs
            2. 输入验证逻辑放在Program.cs
            3. 计算逻辑放在Program.cs
            4. 输出格式化逻辑放在Program.cs
            
            **重要原则：**
            1. 只适用于极简的控制台应用
            2. Program.cs必须包含所有业务逻辑
            3. 保持代码简洁，不需要分层
            4. 每个文件必须有明确的'包含'部分描述具体逻辑";
        }

        private string CreateComplexToolPrompt()
        {
            return @"你是一个专业的C#项目结构规划师，使用Visual Studio 2022。请根据用户需求的复杂度自动生成项目结构，遵循以下规则：

            **适用场景：**
            简单库/工具：有清晰的业务逻辑分层 → 按Models→Interfaces→Services→Program顺序
            
            **逻辑分配规则：**
            1. **Models文件夹**：分配实体类、DTO、枚举等纯数据对象
            2. **Interfaces文件夹**：分配服务契约、仓库接口等抽象定义
            3. **Services文件夹**：分配业务逻辑实现、数据访问、外部API调用
            4. **Program.cs**：分配程序入口和简单的启动逻辑
            5. **项目配置文件**：分配项目依赖和构建设置
            
            **返回格式要求：**
            必须按照文件优先级顺序，以编号列表形式展示每个文件，每个文件包含以下信息：
            1. [文件名], 功能：[简短功能描述], 路径：[相对路径], 依赖：[依赖的文件名], 附加说明：[相关说明]，包含：[此文件包含的具体逻辑描述]
            
            **示例格式：**
            1. CsvRecord.cs, 功能：CSV记录模型, 路径：./CsvReaderTool/Models/, 依赖：, 附加说明：定义数据模型, 包含：Id、Name、Age等属性定义，数据验证逻辑
            2. ICsvReader.cs, 功能：CSV读取接口, 路径：./CsvReaderTool/Interfaces/, 依赖：CsvRecord.cs, 附加说明：定义读取方法签名, 包含：文件读取、数据解析、查找方法的接口定义
            3. CsvFileReader.cs, 功能：CSV读取实现, 路径：./CsvReaderTool/Services/, 依赖：ICsvReader.cs, CsvRecord.cs, 附加说明：实现文件读取逻辑, 包含：文件IO操作、CSV解析算法、数据转换、错误处理
            4. Program.cs, 功能：程序入口, 路径：./CsvReaderTool/, 依赖：ICsvReader.cs, CsvFileReader.cs, 附加说明：应用程序启动点, 包含：依赖注入配置、服务初始化、主执行逻辑
            5. CsvReaderTool.csproj, 功能：项目配置文件, 路径：./CsvReaderTool/, 依赖：, 附加说明：定义项目属性和NuGet包依赖, 包含：目标框架、输出类型、包引用、项目引用配置
            
            **csproj文件配置规范：**
            1. **基本配置**：
               - 输出类型：Console/Class Library（根据需求选择）
               - 启用可空引用类型（Nullable enable）
               - 启用隐式using（ImplicitUsings enable）
               - 启用顶级语句（根据项目复杂度选择）
            
            2. **NuGet包依赖**（根据实际需要选择）：
               - 数据处理：CsvHelper, Newtonsoft.Json/System.Text.Json
               - 日志记录：Serilog, Microsoft.Extensions.Logging
               - 依赖注入：Microsoft.Extensions.DependencyInjection
               - 配置管理：Microsoft.Extensions.Configuration
               - 测试框架：xUnit/NUnit, Moq（如果是测试项目）
               - 工具类：morelinq, Humanizer
            
            3. **构建设置**：
               - 生成XML文档（GenerateDocumentationFile）
               - 启用代码分析（EnableNETAnalyzers）
               - 配置调试/发布优化设置
               - 设置版本号和程序集信息
            
            **逻辑分配指南：**
            1. 数据处理逻辑 → Models
            2. 业务规则逻辑 → Services
            3. 数据持久化逻辑 → Services
            4. 输入验证逻辑 → Models
            5. 计算逻辑 → Services
            6. 工作流程逻辑 → Services
            
            **文件创建优先级顺序：**
            1. Models文件夹及文件（数据基础）
            2. Interfaces文件夹及文件（抽象定义）
            3. Services文件夹及文件（实现逻辑）
            4. Program.cs（应用入口）
            5. ProjectName.csproj（项目配置）
            
            **重要原则：**
            1. 每个文件必须有明确的'包含'部分描述具体逻辑
            2. 依赖关系要真实反映代码调用
            3. 保持单一职责，不要在一个文件中堆积过多逻辑
            4. 优先将可复用的逻辑提取到Services中
            5. 纯数据对象放在Models中
            6. 项目配置(.csproj)应包含必要的NuGet包和构建设置
            7. 考虑版本兼容性和依赖管理";
        }
        private string CreateSimpleWpfPrompt()
        {
            return @"你是一个专业的C#项目结构规划师，使用Visual Studio 2022。请根据用户需求的复杂度自动生成项目结构，遵循以下规则：

            **适用场景：**
            简单WPF界面：UI逻辑简单，可直接写在xaml.cs文件中 → UI+基础逻辑
            
            **逻辑分配规则：**
            1. **Views文件夹**：分配XAML界面定义
            2. **xaml.cs文件**：分配UI事件处理和简单控制逻辑
            3. **Program.cs**：分配程序入口和简单的启动逻辑
            
            **返回格式要求：**
            必须按照文件优先级顺序，以编号列表形式展示每个文件，每个文件包含以下信息：
            1. [文件名], 功能：[简短功能描述], 路径：[相对路径], 依赖：[依赖的文件名], 附加说明：[相关说明]，包含：[此文件包含的具体逻辑描述]
            
            **完整输出示例：**
            1. MainWindow.xaml, 功能：主窗口界面, 路径：./CounterApp/, 依赖：, 附加说明：包含按钮和文本框, 包含：Grid布局、Button控件、TextBlock控件、事件绑定
            2. MainWindow.xaml.cs, 功能：主窗口逻辑, 路径：./CounterApp/, 依赖：MainWindow.xaml, 附加说明：处理用户交互, 包含：按钮点击事件处理、计数逻辑、文本更新、简单的状态管理
            3. App.xaml, 功能：应用程序定义, 路径：./CounterApp/, 依赖：, 附加说明：应用程序入口点, 包含：StartupUri设置、应用程序级资源
            4. App.xaml.cs, 功能：应用程序逻辑, 路径：./CounterApp/, 依赖：App.xaml, 附加说明：应用程序生命周期管理, 包含：应用程序启动和退出事件处理
            5. AssemblyInfo.cs, 功能：程序集信息, 路径：./CounterApp/Properties/, 依赖：, 附加说明：包含程序集元数据, 包含：程序集版本、公司信息、版权信息、程序集描述
            6. CounterApp.csproj, 功能：项目配置文件, 路径：./CounterApp/, 依赖：, 附加说明：定义项目属性和引用, 包含：目标框架、输出类型、WPF引用、项目引用、资源文件包含
            
            **逻辑分配指南：**
            1. UI布局逻辑 → xaml文件
            2. UI交互逻辑 → xaml.cs文件
            3. 简单业务逻辑 → xaml.cs文件
            4. 状态管理逻辑 → xaml.cs文件
            5. 事件处理逻辑 → xaml.cs文件
            
            **重要原则：**
            1. 适用于UI逻辑简单的WPF应用
            2. xaml.cs文件可以包含业务逻辑
            3. 不需要ViewModel层
            4. 每个文件必须有明确的'包含'部分描述具体逻辑
            5. 保持UI逻辑和业务逻辑的分离
            6. 必须包含AssemblyInfo.cs和.csproj项目配置文件";
        }
        // 4. 复杂WPF应用项目提示词
        private string CreateComplexWpfPrompt()
        {
            return @"你是一个专业的C#项目结构规划师，使用Visual Studio 2022。请根据用户需求的复杂度自动生成项目结构，遵循以下规则：

            **适用场景：**
            复杂WPF应用：业务逻辑复杂，需要MVVM模式 → Models→Interfaces→Services→ViewModels→Views→UI
            
            **逻辑分配规则：**
            1. **Models文件夹**：分配实体类、DTO、枚举等纯数据对象
            2. **Interfaces文件夹**：分配服务契约、仓库接口等抽象定义
            3. **Services文件夹**：分配业务逻辑实现、数据访问、外部API调用
            4. **ViewModels文件夹**：分配UI状态管理、命令绑定、数据格式化
            5. **Views文件夹**：分配XAML界面定义和少量UI代码
            6. **Program.cs**：分配程序入口和简单的启动逻辑
            
            **返回格式要求：**
            必须按照文件优先级顺序，以编号列表形式展示每个文件，每个文件包含以下信息：
            1. [文件名], 功能：[简短功能描述], 路径：[相对路径], 依赖：[依赖的文件名], 附加说明：[相关说明]，包含：[此文件包含的具体逻辑描述]
            
            **示例格式：**
            1. Student.cs, 功能：学生模型, 路径：./StudentManagement/Models/, 依赖：, 附加说明：学生实体类, 包含：Id、Name、Age、Grade、Class等属性定义，数据验证方法
            2. IStudentRepository.cs, 功能：学生数据接口, 路径：./StudentManagement/Interfaces/, 依赖：Student.cs, 附加说明：数据访问抽象, 包含：CRUD操作方法定义，查询接口
            3. StudentRepository.cs, 功能：学生数据实现, 路径：./StudentManagement/Services/, 依赖：IStudentRepository.cs, Student.cs, 附加说明：数据访问实现, 包含：数据库操作、数据缓存、查询优化
            
            **逻辑分配指南：**
            1. 数据处理逻辑 → Models/Services
            2. 业务规则逻辑 → Services
            3. UI交互逻辑 → ViewModels
            4. 数据持久化逻辑 → Services
            5. 输入验证逻辑 → Models/ViewModels
            6. 计算逻辑 → Services/Models
            7. 工作流程逻辑 → Services
            8. UI状态管理 → ViewModels
            9. 命令绑定 → ViewModels
            
            **重要原则：**
            1. 严格遵守MVVM模式
            2. 业务逻辑必须放在Services中
            3. UI状态管理必须放在ViewModels中
            4. Views中只包含UI布局和少量代码
            5. 每个文件必须有明确的'包含'部分描述具体逻辑
            6. 依赖关系要真实反映代码调用
            7. 保持单一职责，不要在一个文件中堆积过多逻辑";
        }
        // 辅助方法：判断项目复杂度
        private ProjectComplexity DetermineComplexity(Dictionary<string,Object> metadata)
        {
            if (metadata["ProjectType"].ToString() == "Console" && metadata["Complexity"].ToString() == "Simple")
            {
                return ProjectComplexity.SimpleConsole;
            }
            else if (metadata["ProjectType"].ToString() == "Console" && metadata["Complexity"].ToString() != "WithModelsAndInterfaces")
            {
                return ProjectComplexity.ComplexConsole;
            }
            else if (metadata["ProjectType"].ToString() == "WPF" && metadata["Complexity"].ToString() == "Simple")
            {
                return ProjectComplexity.SimpleWpf;
            }
            else if (metadata["ProjectType"].ToString() == "WPF" && metadata["Complexity"].ToString() != "Simple")
            {
                return ProjectComplexity.ComplexWpf;
            }
            else {
                return ProjectComplexity.ComplexWpf;
            }

        }

        // 复杂度枚举
        private enum ProjectComplexity
        {
            SimpleConsole,      // 极简控制台项目
            ComplexConsole,   // 复杂控制台
            SimpleWpf,       // 简单WPF界面
            ComplexWpf       // 复杂WPF应用
        }
        public override async Task<AgentResponse> ProcessAsync(AgentMessage message)
        {
            // 保存接收到的消息
            _messageHistory.Add(message);
            RaiseMessageReceived(message);

            var response = await ProcessDesignTask(message);

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