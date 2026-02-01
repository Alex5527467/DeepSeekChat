using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DeepSeekChat.Converters
{
    public class IntToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int indent)
            {
                // 左侧缩进，其他边为0
                return new Thickness(indent, 0, 0, 0);
            }
            return new Thickness(4, 0, 0, 0); // 默认缩进
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}