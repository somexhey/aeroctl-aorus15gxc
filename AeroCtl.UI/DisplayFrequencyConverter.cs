using System;
using System.Globalization;
using System.Windows.Data;

namespace AeroCtl.UI;

public class DisplayFrequencyConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not uint v)
			return "?";

		return v == 0
			? "No change"
			: $"{v} Hz";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}