using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DeepSeekChat.Converters
{
    /// <summary>
    /// 布尔值转画刷转换器
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = Brushes.Green;
        public Brush FalseBrush { get; set; } = Brushes.Red;
        public Brush NullBrush { get; set; } = Brushes.Gray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? TrueBrush : FalseBrush;

            return NullBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}