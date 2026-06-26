using System;
using System.Globalization;
using System.Windows.Data;

namespace AeroCtl.UI;

public class BatteryStateConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not BatteryState state)
			return null;

		return state switch
		{
			BatteryState.NoBattery => "No battery",
			BatteryState.AC => "AC",
			BatteryState.DC => "Battery",
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}