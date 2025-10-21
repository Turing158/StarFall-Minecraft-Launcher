using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StarFallMC.Util;

public class StringEmptyToVisibilityCollapsedConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value != null && value is string str) {
            if (!string.IsNullOrEmpty(str)) {
                return Visibility.Collapsed;
            }
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return Visibility.Visible;
    }
}

public class StringNotEmptyToVisibilityVisibleConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value != null && value is string str) {
            if (!string.IsNullOrEmpty(str)) {
                return Visibility.Visible;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return Visibility.Collapsed;
    }
}

public class BooleanTrueToVisibilityVisibleConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value != null && value is bool) {
            if ((bool)value) {
                return Visibility.Visible;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return Visibility.Collapsed;
    }
}

public class BooleanFalseToVisibilityVisibleConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value != null && value is bool) {
            if ((bool)value) {
                return Visibility.Collapsed;
            }
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return Visibility.Visible;
    }
}

public class NullObjectToVisibilityCollapsedConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value == null) {
            return Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return Visibility.Visible;
    }
}

public class NullObjectToVisibilityVisibleConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value == null) {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return Visibility.Collapsed;
    }
}

public class MinecraftDownloaderTypeToLabelConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value != null && value is string str) {
            return str[0].ToString().ToUpper();
        }
        return "N";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return "N";
    }
}

public class ListEmptyToCollapsedConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value != null && value is IList list) {
            return list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return Visibility.Visible;
    }
}

public class ListToLabelConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value != null && value is IList list) {
            if (list != null && list.Count == 1) {
                return $"{list[0]}";
            }
            if (list != null && list.Count > 1) {
                return $"{list[0]}+";
            }
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return "";
    }
}
