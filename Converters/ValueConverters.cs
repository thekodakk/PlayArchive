using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PlayArchive.Models;

namespace PlayArchive.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = false;
            if (value is bool flag)
            {
                b = flag;
            }
            else if (value != null)
            {
                b = !string.IsNullOrWhiteSpace(value.ToString());
            }

            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            if (Invert) isNull = !isNull;
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? str = value as string;
            return !string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogEventType type)
            {
                return type switch
                {
                    LogEventType.GameStarted => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F0FF")), // Cyber Cyan
                    LogEventType.GameStopped => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF0055")), // Neon Red
                    LogEventType.TrackingStatus => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")), // Emerald
                    LogEventType.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")), // Amber
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")) // Slate
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogTypeToBadgeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogEventType type)
            {
                return type switch
                {
                    LogEventType.GameStarted => "START",
                    LogEventType.GameStopped => "STOP",
                    LogEventType.TrackingStatus => "SYS",
                    LogEventType.Warning => "WARN",
                    _ => "INFO"
                };
            }
            return "LOG";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
