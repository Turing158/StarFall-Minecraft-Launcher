using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StarFallMC.Component;

public class ComboBoxChoiceVisibleConverter : IMultiValueConverter {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        if (values[1] is IList list && values[2] is int length) {
            if (list.Count <= length || list.IndexOf(values[0]) + 1 <= length) {
                return Visibility.Collapsed;
            }
        }
        return Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        return null;
    }
}

public class ComboBoxChoiceMarginAnimationConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is Visibility visibility) {
            return visibility == Visibility.Collapsed ? new Thickness(0,0,10,0) : new Thickness(0, 0, 30, 0) ;
        }
        return new Thickness(0, 0, 30, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return null;
    }
}

public class ComboBoxTitleContentMarginConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is Visibility visibility) {
            return visibility == Visibility.Collapsed ? new Thickness(0,0,5,0): new Thickness(0, 0, 25, 0);
        }
        return new Thickness(0, 0, 25, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return null;
    }
}
