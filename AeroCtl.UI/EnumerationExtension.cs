using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Markup;

namespace AeroCtl.UI;

public class EnumerationExtension : MarkupExtension
{
	private Type enumType;

	public Type EnumType
	{
		get => this.enumType;
		private set
		{
			if (this.enumType == value)
				return;

			Type t = Nullable.GetUnderlyingType(value) ?? value;

			if (t.IsEnum == false)
				throw new ArgumentException("Type must be an Enum.");

			this.enumType = value;
		}
	}

	public EnumerationExtension(Type enumType)
	{
		this.EnumType = enumType ?? throw new ArgumentNullException(nameof(enumType));
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		Array enumValues = Enum.GetValues(EnumType);

		return (
			from object enumValue in enumValues
			select new EnumerationMember(enumValue, this.getDescription(enumValue))).ToArray();
	}

	private string getDescription(object enumValue)
	{
		return this.EnumType
			.GetField(enumValue.ToString()!)!
			.GetCustomAttributes(typeof(DescriptionAttribute), false)
			.FirstOrDefault() is DescriptionAttribute descriptionAttribute
			? descriptionAttribute.Description
			: enumValue.ToString();
	}

	public record EnumerationMember(object Value, string Description);
}