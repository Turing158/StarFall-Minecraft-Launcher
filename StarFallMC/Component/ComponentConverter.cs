using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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

public class ListToVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is IList list) {
            return list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return null;
    }
}

public class NaviItemTextBlockFontSizeConverter : IMultiValueConverter {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        if (values[0] is double CompFontSize && values[1] is double EntityFontSize) {
            if (EntityFontSize > 0) {
                return EntityFontSize;
            }
            return CompFontSize;
        }
        return 14;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        return null;
    }
}

public class NaviSecondMenuWidthConverter : IMultiValueConverter {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        if (values[0] is Orientation orientation && values[1] is double width) {
            if (orientation == Orientation.Vertical) {
                return width;
            }
        }
        return double.NaN;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        return null;
    }
}

public class NaviSecondMenuHeightConverter : IMultiValueConverter {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        if (values[0] is Orientation orientation && values[1] is double height) {
            if (orientation == Orientation.Horizontal) {
                return height;
            }
        }
        return double.NaN;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        return null;
    }
}

public class ListEmptyToVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is IList list) {
            return list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return null;
    }
}

public class ObjectEmptyToCollapsedConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return null;
    }
}

public class ComboBoxChoiceEnabledConverter : IMultiValueConverter {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        if (values[1] == null) {
            return true;
        }
        if (values[0] is ListViewItem item && values[1] is IList disabledItemsSource) {
            if (disabledItemsSource.Count == 0) {
                return true;
            }

            Console.WriteLine($"{item.DataContext}-{disabledItemsSource.Contains(item.DataContext)}");
            if (disabledItemsSource.Contains(item.DataContext)) {
                return false;
            }
            return true;
        }
        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        return null;
    }
}

 public class ComboBoxTextMaxWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double containerWidth)
        {
            // 计算TextBlock的最大可用宽度
            // 需要考虑多个因素：

            // 1. TextBlock本身的Margin (30 5)，現在不需要那麽寬
            double textBlockMarginLeft = 5;
            double textBlockMarginRight = 0;

            // 2. ContentControl的Margin (0 0 10 0)
            double contentControlMarginRight = 10;

            // 3. 当有删除按钮时，ContentControl的Margin会变成0 0 30 0
            //    所以我们额外减少20像素的宽度
            double deleteButtonSpace = 20;

            // 4. ListView的Margin (5)
            double listViewMargin = 5;

            // 总计算：容器宽度减去所有Margin和预留空间
            double totalMargin = textBlockMarginLeft + textBlockMarginRight + contentControlMarginRight + deleteButtonSpace + listViewMargin;

            double availableWidth = containerWidth - totalMargin;

            // 确保最小宽度
            return availableWidth;
        }
        return double.NaN;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}

public class ComboBoxListWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double comboBoxWidth)
        {
            // ListView有Margin="5"，所以左右各减去5，总共减去10
            double listViewMarginReduction = 10; // 左右Margin各5

            // 计算ListView的宽度：ComboBox宽度减去ListView的Margin
            double listViewWidth = comboBoxWidth - listViewMarginReduction;

            // 确保最小宽度
            return listViewWidth > 100 ? listViewWidth : 100;
        }
        return double.NaN;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}

public class ComboBoxListItemWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is double listViewWidth && values[1] is Thickness gridMargin)
        {
            // Grid的宽度应该是ListView宽度减去Grid的左右Margin
            // 因为Grid有HorizontalAlignment="Stretch"，但我们需要考虑它的Margin

            double leftMargin = gridMargin.Left;
            double rightMargin = gridMargin.Right;

            // Grid的实际内容宽度
            double gridContentWidth = listViewWidth - leftMargin - rightMargin;

            // 确保最小宽度
            return gridContentWidth > 50 ? gridContentWidth : 50;
        }
        return double.NaN;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return null;
    }
}

public class ComboBoxGridMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // value 是 DeleteButtonVisibility
        if (value is Visibility visibility && visibility == Visibility.Visible)
        {
            // 当删除按钮可见时，Grid需要有更大的右边距
            return new Thickness(0, 0, 2, 0); // 右边距30
        }
        // 默认右边距
        return new Thickness(0, 0, 20, 0); // 右边距20
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}

public class ComboBoxGridMaxWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double listViewWidth)
        {
            // Grid的最大宽度应该是ListView的宽度
            return listViewWidth;
        }
        return double.NaN;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}

