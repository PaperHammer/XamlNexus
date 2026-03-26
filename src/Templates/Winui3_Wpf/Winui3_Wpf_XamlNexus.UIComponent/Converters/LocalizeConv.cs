using System;
using Microsoft.UI.Xaml.Data;
using Winui3_Wpf_XamlNexus.UIComponent.Utils;

namespace Winui3_Wpf_XamlNexus.UIComponent.Converters {
    public partial class LocalizeConv : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            if (value == null || value is not string key) return null;

            if (string.IsNullOrEmpty(key)) return key;

            return LanguageUtil.GetI18n(key);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
