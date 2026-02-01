using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace DeepSeekChat.Models
{
    public class MessageItem : INotifyPropertyChanged
    {
        private string _message = string.Empty;
        private bool _isExpanded = false; // 默认展开

        public string Sender { get; set; } = string.Empty;

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayMessage));
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayMessage));
            }
        }

        // 显示的文本（支持展开/折叠）
        public string DisplayMessage
        {
            get
            {
                if (IsExpanded || string.IsNullOrEmpty(Message))
                    return Message;

                // 折叠时显示前80个字符
                return Message.Length > 80 ? Message.Substring(0, 80) + "..." : Message;
            }
        }

        public Brush BackgroundColor { get; set; }
        public HorizontalAlignment HorizontalAlignment { get; set; }
        public Brush SenderColor { get; set; }
        public string Timestamp { get; set; } = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}