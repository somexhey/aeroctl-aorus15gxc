namespace AeroCtl;

/// <summary>
/// Represents a point on a fan curve.
/// </summary>
public record struct FanPoint(double Temperature, double FanSpeed)
{
	/// <summary>
	/// Gets or sets the temperature (X coordinate).
	/// </summary>
	public double Temperature { get; set; } = Temperature;

	/// <summary>
	/// Gets or sets the fan speed (Y coordinate).
	/// </summary>
	public double FanSpeed { get; set; } = FanSpeed;
}