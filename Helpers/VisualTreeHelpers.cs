using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Nelir.Helpers
{
    public static class VisualTreeHelpers
    {
        public static T? FindVisualChild<T>(this DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T target) return target;
                var result = child.FindVisualChild<T>();
                if (result != null) return result;
            }
            return null;
        }

        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T target) yield return target;
                foreach (var nested in child.FindVisualChildren<T>())
                    yield return nested;
            }
        }
    }
}
