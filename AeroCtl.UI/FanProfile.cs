namespace AeroCtl.UI;

/// <summary>
/// Fan profiles.
/// </summary>
public enum FanProfile
{
	/// <summary>
	/// Built-in quiet profile.
	/// </summary>
	Quiet,

	/// <summary>
	/// Built-in standard profile.
	/// </summary>
	Normal,

	/// <summary>
	/// Built-in gaming profile.
	/// </summary>
	Gaming,

	/// <summary>
	/// Fixed fan speed.
	/// </summary>
	Fixed,

	/// <summary>
	/// Auto fan speed, has tunable parameter.
	/// </summary>
	Auto,

	/// <summary>
	/// Built-in custom hardware based fan curve.
	/// </summary>
	Custom,

	/// <summary>
	/// Software-based fan curve (internally uses <see cref="Fixed"/> in regular intervals).
	/// </summary>
	Software,
}