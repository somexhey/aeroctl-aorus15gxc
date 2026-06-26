using System;
using System.Globalization;
using System.Windows.Data;

namespace AeroCtl.UI;

public class PercentageConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is double d)
		{
			return (d * 100.0).ToString();
		}

		return "?";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string s)
		{
			double d = double.Parse(s) / 100.0;
			return Math.Clamp(d, 0.0, 1.0);
		}

		return Binding.DoNothing;
	}
}