using System;
using System.Windows.Media;

namespace DeepSeekChat.ViewModels
{
    /// <summary>
    /// Agent窗口消息项
    /// </summary>
    public class AgentMessageItem
    {
        /// <summary>
        /// 消息发送者
        /// </summary>
        public string Sender { get; set; }

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 消息背景颜色
        /// </summary>
        public Brush Background { get; set; } = Brushes.White;

        /// <summary>
        /// 发送者名称颜色
        /// </summary>
        public Brush SenderColor { get; set; } = Brushes.Black;

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss");

        /// <summary>
        /// 消息类型（用于区分不同类型的消息）
        /// </summary>
        public MessageType MessageType { get; set; } = MessageType.Normal;

        /// <summary>
        /// 是否显示时间戳
        /// </summary>
        public bool ShowTimestamp { get; set; } = true;

        /// <summary>
        /// 是否显示发送者
        /// </summary>
        public bool ShowSender { get; set; } = true;

        /// <summary>
        /// 消息图标（可选）
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 格式化后的消息内容（带格式）
        /// </summary>
        public string FormattedContent => FormatContent();

        /// <summary>
        /// 消息是否重要（用于特殊显示）
        /// </summary>
        public bool IsImportant { get; set; }

        /// <summary>
        /// 是否已读（用于消息状态跟踪）
        /// </summary>
        public bool IsRead { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public AgentMessageItem()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="content">内容</param>
        public AgentMessageItem(string sender, string content)
        {
            Sender = sender;
            Content = content;
            SetDefaultColors(sender);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="content">内容</param>
        /// <param name="backgroundColor">背景色</param>
        /// <param name="senderColor">发送者颜色</param>
        public AgentMessageItem(string sender, string content, Color backgroundColor, Color senderColor)
        {
            Sender = sender;
            Content = content;
            Background = new SolidColorBrush(backgroundColor);
            SenderColor = new SolidColorBrush(senderColor);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="content">内容</param>
        /// <param name="backgroundColor">背景色</param>
        /// <param name="senderColor">发送者颜色</param>
        /// <param name="messageType">消息类型</param>
        public AgentMessageItem(string sender, string content, Color backgroundColor,
                               Color senderColor, MessageType messageType)
        {
            Sender = sender;
            Content = content;
            Background = new SolidColorBrush(backgroundColor);
            SenderColor = new SolidColorBrush(senderColor);
            MessageType = messageType;
        }

        /// <summary>
        /// 设置默认颜色
        /// </summary>
        /// <param name="sender">发送者</param>
        private void SetDefaultColors(string sender)
        {
            switch (sender?.ToLower())
            {
                case "系统":
                case "system":
                    Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    SenderColor = Brushes.DarkGray;
                    break;

                case "用户":
                case "user":
                case "你":
                    Background = new SolidColorBrush(Color.FromRgb(220, 240, 255));
                    SenderColor = Brushes.Blue;
                    break;

                case "deepseek":
                case "ai":
                case "assistant":
                    Background = new SolidColorBrush(Color.FromRgb(240, 248, 255));
                    SenderColor = Brushes.DarkBlue;
                    break;

                case "错误":
                case "error":
                    Background = new SolidColorBrush(Color.FromRgb(255, 240, 240));
                    SenderColor = Brushes.Red;
                    MessageType = MessageType.Error;
                    break;

                case "成功":
                case "success":
                    Background = new SolidColorBrush(Color.FromRgb(240, 255, 240));
                    SenderColor = Brushes.Green;
                    MessageType = MessageType.Success;
                    break;

                case "警告":
                case "warning":
                    Background = new SolidColorBrush(Color.FromRgb(255, 250, 240));
                    SenderColor = Brushes.Orange;
                    MessageType = MessageType.Warning;
                    break;

                default:
                    // 为不同Agent设置不同的颜色
                    Background = GetAgentBackground(sender);
                    SenderColor = GetAgentColor(sender);
                    break;
            }
        }

        /// <summary>
        /// 根据发送者名称获取背景颜色
        /// </summary>
        private Brush GetAgentBackground(string sender)
        {
            if (string.IsNullOrEmpty(sender))
                return Brushes.White;

            // 使用发送者名称的哈希值生成颜色
            int hash = Math.Abs(sender.GetHashCode());
            byte r = (byte)((hash % 100) + 150); // 150-250 较浅的颜色
            byte g = (byte)(((hash / 100) % 100) + 150);
            byte b = (byte)(((hash / 10000) % 100) + 150);

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        /// <summary>
        /// 根据发送者名称获取发送者颜色
        /// </summary>
        private Brush GetAgentColor(string sender)
        {
            if (string.IsNullOrEmpty(sender))
                return Brushes.Black;

            // 为常见Agent设置固定颜色
            if (sender.Contains("Design") || sender.Contains("设计"))
                return Brushes.Purple;

            if (sender.Contains("Coding") || sender.Contains("编程") || sender.Contains("编码"))
                return Brushes.Blue;

            if (sender.Contains("Testing") || sender.Contains("测试"))
                return Brushes.Orange;

            if (sender.Contains("Coordinator") || sender.Contains("协调"))
                return Brushes.Green;

            // 其他Agent使用深色
            int hash = Math.Abs(sender.GetHashCode());
            byte r = (byte)(hash % 128); // 0-127 较深的颜色
            byte g = (byte)((hash / 100) % 128);
            byte b = (byte)((hash / 10000) % 128);

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        /// <summary>
        /// 格式化消息内容
        /// </summary>
        private string FormatContent()
        {
            if (string.IsNullOrEmpty(Content))
                return string.Empty;

            switch (MessageType)
            {
                case MessageType.Error:
                    return $"❌ {Content}";

                case MessageType.Success:
                    return $"✅ {Content}";

                case MessageType.Warning:
                    return $"⚠️ {Content}";

                case MessageType.Info:
                    return $"ℹ️ {Content}";

                case MessageType.Code:
                    return $"```\n{Content}\n```";

                default:
                    return Content;
            }
        }

        /// <summary>
        /// 创建系统消息
        /// </summary>
        public static AgentMessageItem CreateSystemMessage(string content, MessageType type = MessageType.Info)
        {
            return new AgentMessageItem("系统", content)
            {
                MessageType = type,
                Icon = GetIconForType(type)
            };
        }

        /// <summary>
        /// 创建错误消息
        /// </summary>
        public static AgentMessageItem CreateErrorMessage(string content)
        {
            return new AgentMessageItem("错误", content,
                Colors.LightPink, Colors.Red, MessageType.Error)
            {
                IsImportant = true,
                Icon = "❌"
            };
        }

        /// <summary>
        /// 创建成功消息
        /// </summary>
        public static AgentMessageItem CreateSuccessMessage(string content)
        {
            return new AgentMessageItem("成功", content,
                Colors.LightGreen, Colors.Green, MessageType.Success)
            {
                Icon = "✅"
            };
        }

        /// <summary>
        /// 创建警告消息
        /// </summary>
        public static AgentMessageItem CreateWarningMessage(string content)
        {
            return new AgentMessageItem("警告", content,
                Colors.LightYellow, Colors.Orange, MessageType.Warning)
            {
                Icon = "⚠️"
            };
        }

        /// <summary>
        /// 创建代码消息
        /// </summary>
        public static AgentMessageItem CreateCodeMessage(string content, string language = "csharp")
        {
            return new AgentMessageItem("代码", content)
            {
                MessageType = MessageType.Code,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                SenderColor = Brushes.DarkSlateGray
            };
        }

        /// <summary>
        /// 根据消息类型获取图标
        /// </summary>
        private static string GetIconForType(MessageType type)
        {
            return type switch
            {
                MessageType.Error => "❌",
                MessageType.Success => "✅",
                MessageType.Warning => "⚠️",
                MessageType.Info => "ℹ️",
                MessageType.Code => "💻",
                _ => string.Empty
            };
        }

        /// <summary>
        /// 标记为已读
        /// </summary>
        public void MarkAsRead()
        {
            IsRead = true;
        }

        /// <summary>
        /// 复制消息
        /// </summary>
        public AgentMessageItem Clone()
        {
            return new AgentMessageItem
            {
                Sender = Sender,
                Content = Content,
                Background = Background,
                SenderColor = SenderColor,
                Timestamp = Timestamp,
                MessageType = MessageType,
                ShowTimestamp = ShowTimestamp,
                ShowSender = ShowSender,
                Icon = Icon,
                IsImportant = IsImportant,
                IsRead = IsRead
            };
        }

        /// <summary>
        /// 返回消息的字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"[{Timestamp}] {Sender}: {Content}";
        }
    }

    /// <summary>
    /// 消息类型枚举
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// 普通消息
        /// </summary>
        Normal,

        /// <summary>
        /// 错误消息
        /// </summary>
        Error,

        /// <summary>
        /// 成功消息
        /// </summary>
        Success,

        /// <summary>
        /// 警告消息
        /// </summary>
        Warning,

        /// <summary>
        /// 信息消息
        /// </summary>
        Info,

        /// <summary>
        /// 代码消息
        /// </summary>
        Code,

        /// <summary>
        /// 系统消息
        /// </summary>
        System,

        /// <summary>
        /// 调试消息
        /// </summary>
        Debug,

        /// <summary>
        /// 任务消息
        /// </summary>
        Task
    }
}