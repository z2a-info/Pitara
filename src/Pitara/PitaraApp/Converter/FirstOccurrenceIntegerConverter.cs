using System;
using System.Linq;
using System.Windows.Data;

namespace PitaraApp.Converter
{
    public class FirstOccurrenceIntegerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string s = value as string;
            if (s != null && s.Length > 0)
            {
                string digits = new string(s.Where(char.IsDigit).Take(1).ToArray());
                int result;
                if (int.TryParse(digits, out result))
                {
                    return result;
                }
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
