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
            
            **逻辑分配与需求保留指南：**
            1. 分析用户需求中的所有核心功能点，确保每个功能点都在'包含'部分明确体现
            2. 所有业务逻辑集中在Program.cs，但必须按功能模块清晰描述
            3. 用户需求中的每个具体功能必须映射到'包含'部分的相应逻辑描述
            4. 输入验证逻辑放在Program.cs，需详细说明验证规则
            5. 计算逻辑放在Program.cs，需列出所有计算方法和算法
            6. 输出格式化逻辑放在Program.cs，需描述输出格式要求
            7. 用户交互流程（如菜单、选项等）需在'包含'部分完整描述
            
            **需求分析流程（你内部执行）：**
            1. 提取用户需求中的所有关键功能
            2. 识别数据结构和类型定义需求
            3. 分析业务流程和用户交互步骤
            4. 确定异常处理和边界情况
            5. 整理所有必须实现的逻辑单元
            
            **包含部分编写要求：**
            1. 必须详细列出Program.cs中实现的所有功能逻辑
            2. 按功能模块分段描述，如：主流程、输入处理、核心计算、结果显示等
            3. 每个用户需求功能点都要对应具体的代码逻辑描述
            4. 包含数据验证的具体规则和错误处理方式
            5. 描述程序的主要控制流程和分支逻辑
            6. 如果有状态管理或数据存储，需详细说明
              
            **示例格式（简单版）：**
            用户需求：创建一个计算器，支持加、减、乘、除四则运算
            
            1. Program.cs, 功能：程序入口与计算逻辑, 路径：./SimpleCalculator/, 依赖：, 附加说明:控制台应用程序,
               包含：
               - Main方法：程序入口，循环执行直到用户选择退出
               - 输入处理：读取用户输入的两个数字和运算符
               - 输入验证：
                 * 验证数字格式有效性
                 * 验证除数不为零
                 * 验证运算符为四种之一(+, -, *, /)
               - 计算逻辑：
                 * Add()：加法运算
                 * Subtract()：减法运算
                 * Multiply()：乘法运算
                 * Divide()：除法运算，处理除数为零的情况
               - 输出格式化：显示 结果：{ 数字1}
                        { 运算符}
                        { 数字2} = { 结果}
                        
               - 用户交互：支持连续计算，每次计算后询问是否继续？(Y/N)
               - 错误处理：捕获格式异常并提供友好的错误提示
            
            2.SimpleCalculator.csproj, 功能：项目文件, 路径：./ SimpleCalculator /, 依赖：, 附加说明: 控制台应用程序, 包含：项目配置和依赖项
            
            ** 重要原则：**
            1.只适用于极简的控制台应用
            2.Program.cs必须包含所有业务逻辑，且详细描述不遗漏任何用户需求
            3. '包含'部分必须完整反映用户需求的所有功能点，采用清晰的分段结构
            4.保持代码简洁，不需要分层
            5.每个文件必须有明确详细的'包含'部分，确保需求完全覆盖
            6.如果用户需求复杂，确保在'包含'中分解为可执行的逻辑单元
            7.根据用户需求的复杂程度，调整'包含'部分的详细程度，确保不遗漏任何功能";
        }

        private string CreateComplexToolPrompt()
        {
            return @"你是一个专业的C#项目结构规划师，使用Visual Studio 2022。请根据用户需求的复杂度自动生成项目结构，遵循以下规则：

            **适用场景：**
            简单库/工具：有清晰的业务逻辑分层 → 按Models→Interfaces→Services→Program顺序
            
            **需求分析流程（你内部执行）：**
            1. 提取用户需求中的所有数据实体和结构需求
            2. 识别业务规则、计算逻辑和工作流程
            3. 分析数据操作和持久化需求
            4. 确定外部依赖和第三方集成点
            5. 识别异常处理、验证和边界情况
            6. 整理所有接口契约和抽象定义
            
            **逻辑分配与需求保留指南：**
            1. 数据模型必须完整：Models文件夹包含所有数据结构、枚举、常量
            2. 接口契约必须完整：Interfaces文件夹包含所有抽象定义和服务契约
            3. 业务逻辑必须完整：Services文件夹包含所有实现逻辑，按功能模块拆分
            4. 入口逻辑必须完整：Program.cs包含完整的应用启动、配置和执行流程
            5. 每个用户需求功能点必须映射到具体文件的'包含'部分
            
            **逻辑拆分规则：**
            1. **纯数据对象**（无行为）→ Models文件夹
               - 实体类、DTO、ViewModel
               - 枚举、常量、配置类
               - 数据验证属性、数据注解
            
            2. **抽象定义**（接口、抽象类）→ Interfaces文件夹
               - 服务接口（IService）
               - 仓储接口（IRepository）
               - 工厂接口（IFactory）
               - 策略接口（IStrategy）
               - 数据访问接口（IDataAccess）
            
            3. **具体实现**（含业务逻辑）→ Services文件夹
               - 业务规则实现
               - 计算算法实现
               - 数据持久化实现
               - 外部API调用实现
               - 工作流程协调
               - 异常处理和日志记录
            
            4. **应用入口**（组合所有组件）→ Program.cs
               - 依赖注入配置
               - 服务组合和初始化
               - 主执行流程
               - 错误处理中间件
               - 配置加载
            
            **返回格式要求：**
            必须按照文件优先级顺序，以编号列表形式展示每个文件，每个文件包含以下信息：
            1. [文件名], 功能：[简短功能描述], 路径：[相对路径], 依赖：[依赖的文件名], 附加说明：[相关说明]，包含：[此文件包含的具体逻辑描述]
            
            **完整输出示例（用户需求：创建一个订单管理系统）：**
            用户需求：创建一个订单管理系统，支持创建订单、计算总价、应用折扣、保存订单、生成订单报告
            
            **数据层 - Models：**
            1. Order.cs, 功能：订单实体类, 路径：./OrderSystem/Models/, 依赖：, 附加说明：定义订单核心数据结构,
               包含：
               - 属性定义：OrderId（唯一标识）、CustomerName（客户姓名）、OrderDate（订单日期）
               - 集合属性：OrderItems（订单项列表）、TotalAmount（总金额）、DiscountAmount（折扣金额）
               - 状态属性：OrderStatus（订单状态枚举）、IsPaid（是否已支付）
               - 数据验证：Required、StringLength、Range等数据注解验证
               - 计算方法：CalculateSubtotal()计算小计（纯计算方法，无业务规则）
            
            2. OrderItem.cs, 功能：订单项实体类, 路径：./OrderSystem/Models/, 依赖：Product.cs, 附加说明：订单明细项目,
               包含：
               - 属性定义：ProductId（产品ID）、ProductName（产品名称）、Quantity（数量）、UnitPrice（单价）
               - 计算属性：ItemTotal（单项总价 = Quantity * UnitPrice）
               - 数据验证：Quantity必须大于0，UnitPrice必须大于等于0
            
            3. Product.cs, 功能：产品实体类, 路径：./OrderSystem/Models/, 依赖：, 附加说明：产品信息定义,
               包含：
               - 属性定义：ProductId、Name、Category、Price、StockQuantity
               - 枚举定义：ProductCategory（电子产品、食品、服装等）
            
            4. DiscountType.cs, 功能：折扣类型枚举, 路径：./OrderSystem/Models/, 依赖：, 附加说明：定义可用的折扣类型,
               包含：
               - 枚举值：PercentageDiscount（百分比折扣）、FixedAmountDiscount（固定金额折扣）、BuyOneGetOneFree（买一送一）
            
            **接口层 - Interfaces：**
            5. IOrderService.cs, 功能：订单服务接口, 路径：./OrderSystem/Interfaces/, 依赖：Order.cs, OrderItem.cs, 附加说明：定义订单操作契约,
               包含：
               - 方法签名：CreateOrder(CustomerName, OrderItems)创建新订单
               - 方法签名：CalculateTotal(Order)计算订单总价
               - 方法签名：ApplyDiscount(Order, DiscountType, DiscountValue)应用折扣
               - 方法签名：SaveOrder(Order)保存订单到存储
               - 方法签名：GenerateOrderReport(Order)生成订单报告
            
            6. IDiscountCalculator.cs, 功能：折扣计算器接口, 路径：./OrderSystem/Interfaces/, 依赖：Order.cs, DiscountType.cs, 附加说明：折扣计算策略定义,
               包含：
               - 方法签名：CalculateDiscountAmount(Order, DiscountType, DiscountValue)计算折扣金额
               - 方法签名：ValidateDiscount(Order, DiscountType)验证折扣适用性
            
            7. IOrderRepository.cs, 功能：订单仓储接口, 路径：./OrderSystem/Interfaces/, 依赖：Order.cs, 附加说明：数据持久化抽象,
               包含：
               - 方法签名：Save(Order)保存订单
               - 方法签名：GetById(OrderId)根据ID获取订单
               - 方法签名：GetAll()获取所有订单
               - 方法签名：Delete(OrderId)删除订单
            
            **实现层 - Services：**
            8. OrderService.cs, 功能：订单服务实现, 路径：./OrderSystem/Services/, 依赖：IOrderService, IOrderRepository, IDiscountCalculator, 附加说明：核心业务逻辑实现,
               包含：
               - CreateOrder实现：验证客户信息、初始化订单状态、设置订单日期
               - CalculateTotal实现：调用Order.CalculateSubtotal()，加上税费计算，调用折扣计算器
               - ApplyDiscount实现：验证折扣有效性，调用IDiscountCalculator，更新订单折扣金额
               - SaveOrder实现：验证订单完整性，调用IOrderRepository.Save()
               - GenerateOrderReport实现：格式化订单信息，生成文本报告
               - 业务规则：检查库存数量、验证支付状态、处理订单取消逻辑
            
            9. DiscountCalculator.cs, 功能：折扣计算器实现, 路径：./OrderSystem/Services/, 依赖：IDiscountCalculator, Order.cs, 附加说明：折扣策略实现,
               包含：
               - CalculateDiscountAmount实现：
                 * 百分比折扣：订单总价 * 折扣百分比
                 * 固定金额折扣：直接减去固定金额
                 * 买一送一：计算符合条件的商品数量并减免
               - ValidateDiscount实现：检查折扣码有效期、最小订单金额要求、特定商品限制
               - 折扣逻辑：防止折扣后金额为负数，限制最大折扣金额
            
            10. InMemoryOrderRepository.cs, 功能：内存订单仓储, 路径：./OrderSystem/Services/, 依赖：IOrderRepository, Order.cs, 附加说明：内存数据存储实现,
                包含：
                - Save实现：将订单添加到内存字典，处理重复订单ID
                - GetById实现：从字典中查找订单，处理找不到的情况
                - GetAll实现：返回所有订单列表，支持按日期排序
                - Delete实现：从字典中移除订单，更新相关状态
                - 数据管理：线程安全的数据访问，订单ID生成逻辑
            
            **应用入口：**
            11. Program.cs, 功能：程序入口和配置, 路径：./OrderSystem/, 依赖：IOrderService, IOrderRepository, IDiscountCalculator的所有实现, 附加说明：应用启动和依赖组合,
                包含：
                - 依赖注入配置：注册IOrderService->OrderService、IOrderRepository->InMemoryOrderRepository、IDiscountCalculator->DiscountCalculator
                - 服务初始化：创建服务容器，解析主要服务
                - 主执行逻辑：
                  * 创建测试产品数据
                  * 创建订单并添加多个订单项
                  * 计算订单总价
                  * 应用百分比折扣
                  * 保存订单到仓储
                  * 生成并显示订单报告
                - 异常处理：全局异常捕获，友好错误提示
                - 配置管理：加载应用设置（如默认折扣率）
            
            **项目配置：**
            12. OrderSystem.csproj, 功能：项目配置文件, 路径：./OrderSystem/, 依赖：, 附加说明：项目构建设置和依赖管理,
                包含：
                - 基本配置：输出类型(Exe)、启用可空引用类型、启用隐式using
                - NuGet包依赖：Microsoft.Extensions.DependencyInjection、Microsoft.Extensions.Configuration.Json
                - 构建设置：生成XML文档、启用代码分析、设置版本号1.0.0
                - 编译选项：优化调试/发布配置
            
            **逻辑分配检查清单：**
            1. 所有数据实体是否都在Models中定义？✅
            2. 所有业务抽象是否都在Interfaces中定义？✅
            3. 所有具体实现是否都在Services中实现？✅
            4. 每个用户需求是否都有对应的接口方法？✅
            5. 每个接口方法是否有对应的实现？✅
            6. Program.cs是否组合了所有组件？✅
            7. 异常处理逻辑是否在相应层实现？✅
            8. 验证逻辑是否在合适的层实现？✅
            
            **重要原则：**
            1. 保持单一职责：每个文件只负责一个明确的逻辑领域
            2. 依赖倒置：高层模块不依赖低层模块，都依赖抽象
            3. 完整覆盖：确保用户需求的每个功能点都在相应层有实现
            4. 合理拆分：避免单个文件过大，按功能模块拆分Services
            5. 清晰的依赖链：依赖关系必须真实反映调用关系
            6. 完整的错误处理：每层都要处理本层的异常
            7. 配置完整：csproj包含所有必要的依赖和配置";
        }
        private string CreateSimpleWpfPrompt()
        {
            return @"你是一个专业的C#项目结构规划师，使用Visual Studio 2022。请根据用户需求的复杂度自动生成项目结构，遵循以下规则：

            **适用场景：**
            简单WPF界面：UI逻辑简单，可直接写在xaml.cs文件中 → UI+基础逻辑
            
            **逻辑分配规则：**
            1. **Views文件夹**：分配XAML界面定义
            2. **xaml.cs文件**：分配UI事件处理、业务逻辑和状态管理
            3. **App.xaml/App.xaml.cs**：分配程序入口和启动逻辑
            
            **需求分析流程（你内部执行）：**
            1. 提取用户需求中的所有UI组件和交互要求
            2. 识别所有业务功能点和数据处理需求
            3. 分析用户操作流程和状态变化
            4. 确定事件响应逻辑和验证规则
            5. 整理数据绑定需求（如有）
            
            **逻辑分配与需求保留指南：**
            1. xaml.cs文件必须完整包含所有业务逻辑，按功能模块清晰描述
            2. 用户需求中的每个功能必须映射到'包含'部分的相应逻辑描述
            3. UI事件处理逻辑（如按钮点击、文本变化）→ xaml.cs文件
            4. 业务计算/处理逻辑 → xaml.cs文件
            5. 数据验证和状态管理逻辑 → xaml.cs文件
            6. 简单的数据模型定义（如类、结构体）→ xaml.cs文件
            
            **包含部分编写要求：**
            1. 对xaml.cs文件：必须详细列出实现的所有功能逻辑，按UI交互流程组织
            2. 对xaml文件：详细描述UI布局、控件和绑定关系
            3. 每个用户需求功能点都要对应具体的代码逻辑描述
            4. 包含数据验证的具体规则和错误处理方式
            5. 描述UI状态变化和交互反馈逻辑
            6. 如果有数据操作，需详细说明数据处理流程
            
            **返回格式要求：**
            必须按照文件优先级顺序，以编号列表形式展示每个文件，每个文件包含以下信息：
            1. [文件名], 功能：[简短功能描述], 路径：[相对路径], 依赖：[依赖的文件名], 附加说明：[相关说明]，包含：[此文件包含的具体逻辑描述]
            
            **完整输出示例（用户需求：创建一个学生信息管理系统）：**
            用户需求：创建一个学生信息管理WPF应用，可以添加学生信息（学号、姓名、年龄、专业），显示学生列表，支持按专业筛选，并能删除学生
            
            1. MainWindow.xaml, 功能：主窗口界面, 路径：./StudentManagementApp/, 依赖：, 附加说明：学生信息管理主界面, 
               包含：
               - 窗口布局：Grid主容器，分为顶部操作区和底部显示区
               - 输入区域：四个TextBox用于输入学号、姓名、年龄、专业，带有Label提示
               - 操作按钮：Add按钮（添加学生）、Delete按钮（删除选中学生）、Filter按钮（筛选）
               - 筛选控件：ComboBox用于选择专业，显示所有专业选项
               - 数据显示：DataGrid控件显示学生列表，包含学号、姓名、年龄、专业列
               - 状态显示：TextBlock显示操作反馈信息
               - 事件绑定：所有按钮的Click事件绑定，DataGrid的SelectionChanged事件绑定
            
            2. MainWindow.xaml.cs, 功能：主窗口业务逻辑, 路径：./StudentManagementApp/, 依赖：MainWindow.xaml, 附加说明：包含所有业务逻辑和处理, 
               包含：
               - 数据结构定义：定义Student类，包含学号(Id)、姓名(Name)、年龄(Age)、专业(Major)属性
               - 数据存储：使用ObservableCollection<Student>存储学生数据，支持UI自动更新
               - 添加学生逻辑：
                 * AddButton_Click()：处理添加按钮点击事件
                 * ValidateStudentInput()：验证输入数据（学号不能为空、姓名长度、年龄范围1-100、专业非空）
                 * AddStudent()：创建新Student对象并添加到集合
               - 删除学生逻辑：
                 * DeleteButton_Click()：处理删除按钮点击事件
                 * GetSelectedStudent()：获取DataGrid中选中的学生
                 * DeleteStudent()：从集合中删除选中学生，确认提示逻辑
               - 筛选逻辑：
                 * FilterComboBox_SelectionChanged()：处理专业筛选选择变化
                 * FilterByMajor()：按选定专业过滤学生列表
                 * ResetFilter()：重置筛选显示所有学生
               - 数据显示逻辑：
                 * DataGrid绑定到ObservableCollection<Student>，自动更新
                 * 设置DataGrid列显示格式
               - 状态管理：
                 * UpdateStatusMessage()：更新操作反馈信息（成功/错误提示）
                 * 管理筛选状态和选中状态
               - 初始化逻辑：
                 * Window_Loaded()：窗口加载时初始化数据
                 * InitializeMajorFilter()：初始化专业筛选下拉框
            
            3. App.xaml, 功能：应用程序定义, 路径：./StudentManagementApp/, 依赖：, 附加说明：应用程序入口点, 
               包含：
               - StartupUri设置：指向MainWindow.xaml
               - 应用程序级资源定义
               - 全局样式设置
            
            4. App.xaml.cs, 功能：应用程序逻辑, 路径：./StudentManagementApp/, 依赖：App.xaml, 附加说明：应用程序生命周期管理, 
               包含：
               - OnStartup()：应用程序启动事件处理
               - OnExit()：应用程序退出事件处理
               - 全局异常处理设置
            
            5. StudentManagementApp.csproj, 功能：项目配置文件, 路径：./StudentManagementApp/, 依赖：, 附加说明：定义项目属性和引用, 
               包含：
               - 输出类型：WinExe
               - WPF引用：PresentationCore、PresentationFramework、WindowsBase
               - 项目属性：可空引用类型、编译选项
               - 资源文件包含
            
            **逻辑分配指南：**
            1. UI布局和控件定义 → xaml文件
            2. UI交互事件绑定 → xaml文件
            3. 事件处理方法 → xaml.cs文件
            4. 业务逻辑实现 → xaml.cs文件
            5. 数据模型和状态管理 → xaml.cs文件
            6. 输入验证和错误处理 → xaml.cs文件
            
            **重要原则：**
            1. 适用于UI逻辑简单的WPF应用，所有业务逻辑集中在xaml.cs文件
            2. xaml.cs文件必须详细描述所有业务逻辑实现，不遗漏任何用户需求
            3. 不需要ViewModel层，直接在xaml.cs中实现逻辑
            4. 每个文件必须有明确详细的'包含'部分，确保需求完全覆盖
            5. UI逻辑和业务逻辑分离，但都在同一个xaml.cs文件中实现
            6. 必须包含.csproj项目配置文件
            7. 对于xaml.cs文件，'包含'部分必须按功能模块分段，清晰描述每个需求的实现逻辑";
        }
        // 4. 复杂WPF应用项目提示词
        private string CreateComplexWpfPrompt()
        {
            return @"你是一个专业的C#项目结构规划师，使用Visual Studio 2022。请根据用户需求的复杂度自动生成项目结构，遵循以下规则：

            **适用场景：**
            复杂WPF应用：业务逻辑复杂，需要MVVM模式 → Models→Interfaces→Services→ViewModels→Views→UI
            
            **需求分析流程（你内部执行）：**
            1. 提取用户需求中的所有数据实体和结构定义需求
            2. 识别业务规则、计算算法和工作流程逻辑
            3. 分析数据操作、持久化和外部集成需求
            4. 确定UI界面组件、交互模式和用户操作流程
            5. 识别UI状态管理、数据绑定和命令需求
            6. 整理异常处理、验证规则和边界情况
            7. 分析性能要求和资源管理需求
            
            **逻辑分配与完整性保证：**
            1. **完整数据层**：Models文件夹必须包含需求中的所有数据实体、枚举、常量、配置类
            2. **完整抽象层**：Interfaces文件夹必须包含所有服务契约、仓储接口、工厂模式
            3. **完整业务层**：Services文件夹必须实现所有业务逻辑、计算算法、工作流程
            4. **完整UI逻辑层**：ViewModels文件夹必须包含所有UI状态管理、命令实现、数据格式化
            5. **完整展示层**：Views文件夹必须实现所有界面布局、控件定义、样式绑定
            6. **完整入口层**：Program.cs必须配置所有依赖、初始化所有组件、定义主流程
            
            **逻辑分配规则：**
            1. **Models文件夹**：分配实体类、DTO、枚举、常量、配置类等纯数据对象
            2. **Interfaces文件夹**：分配服务契约、仓库接口、工厂接口、策略接口等抽象定义
            3. **Services文件夹**：分配业务逻辑实现、数据访问、外部API调用、算法实现
            4. **ViewModels文件夹**：分配UI状态管理、命令绑定、数据格式化、视图逻辑
            5. **Views文件夹**：分配XAML界面定义、用户控件、资源字典、样式模板
            6. **Program.cs**：分配程序入口、依赖注入配置、应用启动逻辑
            
            **逻辑详细拆分指南：**
            
            **Models层（纯数据，无行为逻辑）：**
            - 实体类属性定义、构造函数
            - 数据验证特性（DataAnnotations）
            - 简单的计算属性（仅基于自身属性）
            - 枚举类型、常量定义
            - 配置类、设置类
            - DTO（数据传输对象）
            - 事件参数类
            
            **Interfaces层（抽象定义，无实现）：**
            - 服务接口（IService）定义所有业务方法
            - 仓储接口（IRepository）定义数据访问方法
            - 工厂接口（IFactory）定义创建方法
            - 策略接口（IStrategy）定义算法策略
            - 观察者接口（IObserver）定义通知机制
            - 命令接口（ICommand）扩展系统ICommand
            
            **Services层（核心业务逻辑实现）：**
            - 业务规则验证逻辑
            - 复杂计算算法实现
            - 数据转换和映射逻辑
            - 工作流程协调逻辑
            - 外部系统集成逻辑
            - 缓存管理逻辑
            - 日志记录和审计逻辑
            - 异常处理和恢复逻辑
            
            **ViewModels层（UI逻辑和状态管理）：**
            - UI数据绑定属性（ObservableCollection<T>，INotifyPropertyChanged）
            - 命令实现（ICommand）
            - UI状态管理（加载状态、选中状态、编辑状态）
            - 数据格式化逻辑（供UI显示）
            - 用户输入验证逻辑（即时反馈）
            - 导航逻辑（页面/窗口切换）
            - 对话框调用逻辑
            - 事件聚合和处理
            
            **Views层（纯UI展示）：**
            - XAML布局定义（Grid、StackPanel等）
            - 控件定义和属性设置
            - 数据绑定表达式（Binding）
            - 样式和模板定义
            - 触发器（Trigger）定义
            - 资源字典引用
            - 动画和转换定义
            - 少量UI事件处理代码（如Loaded事件）
            
            **返回格式要求：**
            必须按照文件优先级顺序，以编号列表形式展示每个文件，每个文件包含以下信息：
            1. [文件名], 功能：[简短功能描述], 路径：[相对路径], 依赖：[依赖的文件名], 附加说明：[相关说明]，包含：[此文件包含的具体逻辑描述]
            
            **完整输出示例（用户需求：创建图书管理系统）：**
            
            用户需求：创建一个图书管理系统，支持图书信息管理（增删改查）、借阅管理（借书、还书、续借）、读者管理、图书分类、统计报表生成、搜索功能、导入导出功能
            
            **数据层 - Models：**
            1. Book.cs, 功能：图书实体类, 路径：./LibraryManagement/Models/, 依赖：, 附加说明：图书核心数据模型,
               包含：
               - 属性定义：BookId（唯一标识）、ISBN（国际标准书号）、Title（书名）、Author（作者）、Publisher（出版社）
               - 分类属性：Category（图书分类枚举）、PublicationDate（出版日期）、PageCount（页数）
               - 状态属性：IsAvailable（是否可借）、CurrentBorrowerId（当前借阅者ID）、DueDate（应还日期）
               - 数据验证：ISBN格式验证、书名非空验证、出版日期范围验证
               - 简单计算：BookAge计算图书年龄（基于出版日期）
            
            2. BorrowRecord.cs, 功能：借阅记录实体, 路径：./LibraryManagement/Models/, 依赖：Book.cs, Reader.cs, 附加说明：借阅历史记录,
               包含：
               - 属性定义：RecordId、BookId、ReaderId、BorrowDate（借出日期）、DueDate（应还日期）、ReturnDate（实际归还日期）
               - 状态属性：IsReturned（是否已归还）、IsOverdue（是否逾期）、RenewCount（续借次数）
               - 数据验证：借出日期必须早于应还日期，归还日期必须晚于借出日期
            
            3. Reader.cs, 功能：读者实体类, 路径：./LibraryManagement/Models/, 依赖：, 附加说明：读者信息模型,
               包含：
               - 属性定义：ReaderId、Name、Email、Phone、RegistrationDate（注册日期）
               - 限制属性：MaxBorrowCount（最大借阅数量）、CurrentBorrowCount（当前借阅数量）
               - 状态属性：IsActive（是否激活）、ViolationCount（违规次数）
               - 数据验证：邮箱格式验证、手机号格式验证
            
            4. BookCategory.cs, 功能：图书分类枚举, 路径：./LibraryManagement/Models/, 依赖：, 附加说明：图书分类定义,
               包含：
               - 枚举值：Fiction（小说）、NonFiction（非小说）、Science（科学）、Technology（技术）、Art（艺术）、History（历史）
               - 扩展属性：DisplayName（显示名称）、Description（分类描述）
            
            **抽象层 - Interfaces：**
            5. IBookService.cs, 功能：图书服务接口, 路径：./LibraryManagement/Interfaces/, 依赖：Book.cs, BorrowRecord.cs, 附加说明：图书业务操作契约,
               包含：
               - 图书管理：AddBook(Book)、UpdateBook(Book)、DeleteBook(string bookId)、GetBookById(string bookId)
               - 借阅管理：BorrowBook(string bookId, string readerId)、ReturnBook(string bookId)、RenewBook(string bookId)
               - 查询功能：SearchBooks(string keyword, BookCategory? category)、GetAvailableBooks()、GetOverdueBooks()
               - 统计报表：GenerateBorrowReport(DateTime startDate, DateTime endDate)、GetCategoryStatistics()
            
            6. IBookRepository.cs, 功能：图书数据仓储接口, 路径：./LibraryManagement/Interfaces/, 依赖：Book.cs, 附加说明：数据持久化抽象,
               包含：
               - CRUD操作：Insert(Book)、Update(Book)、Delete(string bookId)、GetById(string bookId)
               - 查询操作：GetAll()、GetByCategory(BookCategory)、GetAvailableBooks()
               - 批量操作：BatchInsert(IEnumerable<Book>)、BatchUpdate(IEnumerable<Book>)
            
            7. IBorrowService.cs, 功能：借阅服务接口, 路径：./LibraryManagement/Interfaces/, 依赖：BorrowRecord.cs, Reader.cs, 附加说明：借阅业务逻辑契约,
               包含：
               - 借阅流程：ProcessBorrow(string bookId, string readerId)、ProcessReturn(string bookId)、ProcessRenew(string bookId)
               - 验证逻辑：ValidateBorrowEligibility(string readerId)、ValidateReturnConditions(string bookId)
               - 逾期管理：CalculateOverdueFee(string bookId)、GetOverdueRecords()
            
            **实现层 - Services：**
            8. BookService.cs, 功能：图书服务实现, 路径：./LibraryManagement/Services/, 依赖：IBookService, IBookRepository, 附加说明：图书核心业务逻辑,
               包含：
               - AddBook实现：验证ISBN唯一性、验证图书信息完整性、设置默认可用状态
               - UpdateBook实现：验证图书存在性、更新库存状态、记录修改历史
               - DeleteBook实现：验证图书是否被借出、软删除标记、级联更新借阅记录
               - SearchBooks实现：多字段模糊搜索（书名、作者、出版社）、分类过滤、分页逻辑
               - GenerateBorrowReport实现：按时间范围统计借阅量、生成Excel/PDF报表、图表数据计算
            
            9. BorrowService.cs, 功能：借阅服务实现, 路径：./LibraryManagement/Services/, 依赖：IBorrowService, IBookRepository, IReaderRepository, 附加说明：借阅流程管理,
               包含：
               - ProcessBorrow实现：验证读者借阅资格（未超最大数量）、验证图书可用性、创建借阅记录、更新图书状态
               - ProcessReturn实现：验证借阅记录存在性、计算是否逾期、更新归还日期、重置图书状态
               - ProcessRenew实现：验证续借资格（未逾期、未超续借次数）、计算新应还日期、更新续借计数
               - CalculateOverdueFee实现：按逾期天数计算罚款、考虑节假日排除、最大罚款上限
            
            **UI逻辑层 - ViewModels：**
            10. MainViewModel.cs, 功能：主窗口视图模型, 路径：./LibraryManagement/ViewModels/, 依赖：IBookService, IBorrowService, 附加说明：应用程序主逻辑,
                包含：
                - 数据集合：Books（ObservableCollection<Book>）、Readers（ObservableCollection<Reader>）、CurrentBook（当前选中图书）
                - UI状态：IsLoading（加载状态）、SelectedCategory（选中分类）、SearchKeyword（搜索关键词）
                - 命令实现：LoadBooksCommand（加载图书）、SearchCommand（搜索）、AddBookCommand（添加图书）、BorrowBookCommand（借书）
                - 数据格式化：FormatBookStatus(Book book)格式化状态显示、FormatDueDate(DateTime? dueDate)格式化应还日期
                - 属性通知：实现INotifyPropertyChanged，确保UI自动更新
            
            11. BookManagementViewModel.cs, 功能：图书管理视图模型, 路径：./LibraryManagement/ViewModels/, 依赖：IBookService, 附加说明：图书增删改查逻辑,
                包含：
                - 编辑状态：IsEditing（编辑模式）、IsAdding（添加模式）、OriginalBook（原始数据备份）
                - 验证逻辑：ValidateBookInput()验证用户输入、显示验证错误信息
                - 命令实现：SaveBookCommand（保存图书）、DeleteBookCommand（删除图书）、CancelEditCommand（取消编辑）
                - 对话框协调：ShowBookDetailsDialog()显示详情对话框、ShowConfirmationDialog()显示确认对话框
                - 数据刷新：RefreshBookList()刷新图书列表、保持选中状态
            
            **展示层 - Views：**
            12. MainWindow.xaml, 功能：主窗口界面, 路径：./LibraryManagement/Views/, 依赖：MainViewModel, 附加说明：应用程序主界面,
                包含：
                - 整体布局：DockPanel主容器，分为顶部菜单区、左侧导航区、中间内容区
                - 菜单系统：文件菜单（导入、导出、退出）、编辑菜单（添加、修改、删除）、视图菜单（排序、筛选）
                - 导航面板：TreeView显示图书分类，ListView显示快速访问项
                - 内容区域：TabControl包含图书列表、借阅管理、读者管理、统计报表等标签页
                - 数据绑定：ItemsSource绑定到Books集合，SelectedItem绑定到CurrentBook
                - 样式资源：引用ResourceDictionary中的样式和模板
            
            13. BookListView.xaml, 功能：图书列表视图, 路径：./LibraryManagement/Views/, 依赖：BookManagementViewModel, 附加说明：图书列表展示,
                包含：
                - 数据网格：DataGrid显示图书列表，自定义列模板（书名、作者、状态、操作按钮）
                - 搜索区域：TextBox搜索框、ComboBox分类筛选、Button搜索按钮
                - 操作按钮：添加按钮、编辑按钮、删除按钮、借阅按钮
                - 状态显示：TextBlock显示加载状态、搜索结果数量
                - 命令绑定：按钮Command绑定到ViewModel的对应命令
                - 样式定义：交替行背景色、悬停效果、选中样式
            
            **应用入口：**
            14. App.xaml, 功能：应用程序定义, 路径：./LibraryManagement/, 依赖：, 附加说明：WPF应用程序入口,
                包含：
                - StartupUri设置：指向MainWindow.xaml
                - 全局资源：Application.Resources定义全局样式、颜色、字体
                - 数据模板：定义ViewModel到View的映射关系
                - 合并资源字典：合并多个ResourceDictionary文件
            
            15. App.xaml.cs, 功能：应用程序逻辑, 路径：./LibraryManagement/, 依赖：App.xaml, 附加说明：应用程序配置和启动,
                包含：
                - OnStartup重写：配置依赖注入容器、注册所有服务、初始化数据库连接
                - 异常处理：全局未处理异常捕获、日志记录、用户友好提示
                - 主题设置：加载用户主题偏好、应用主题资源
                - 服务启动：启动后台服务（如定时同步、自动备份）
            
            16. LibraryManagement.csproj, 功能：项目配置文件, 路径：./LibraryManagement/, 依赖：, 附加说明：项目构建设置,
                包含：
                - 基本配置：目标框架(.NET 8.0)、输出类型(WinExe)、启用WPF支持
                - NuGet包：CommunityToolkit.Mvvm（MVVM工具包）、Microsoft.EntityFrameworkCore（ORM）、AutoMapper（对象映射）
                - 构建设置：启用代码分析、生成XML文档、设置版本号
                - 资源包含：XAML文件编译设置、图标资源、本地化资源
            
            **完整性检查清单：**
            1. ✅ 所有数据实体是否都在Models中明确定义？
            2. ✅ 所有业务操作是否都有对应的接口定义？
            3. ✅ 所有接口方法是否有具体的服务实现？
            4. ✅ 所有UI交互逻辑是否都在ViewModels中实现？
            5. ✅ 所有界面布局是否都在Views中定义？
            6. ✅ 所有用户需求功能点是否都有对应的文件实现？
            7. ✅ 数据验证逻辑是否在适当层实现（Models属性验证，ViewModels业务验证）？
            8. ✅ 异常处理是否在各层都有适当实现？
            9. ✅ 依赖注入配置是否完整？
            
            **重要原则：**
            1. 严格遵守MVVM模式：View只负责展示，ViewModel处理UI逻辑，Services处理业务逻辑
            2. 完整覆盖需求：确保用户需求的每个功能点都有对应的实现文件
            3. 依赖关系清晰：明确标注每个文件的依赖关系，反映实际调用链
            4. 单一职责：每个文件专注于一个明确的职责领域
            5. 可测试性：Services和ViewModels应该易于单元测试
            6. 可维护性：遵循SOLID原则，便于后续扩展和维护
            7. 用户体验：考虑UI响应性、错误提示、加载状态等细节";
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