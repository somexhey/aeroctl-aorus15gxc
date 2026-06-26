namespace AeroCtl.UI;

/// <summary>
/// Kinds of fan "curves".
/// </summary>
public enum FanCurveKind
{
	/// <summary>
	/// Step fan curve used by the hardware controller.
	/// </summary>
	Step,

	/// <summary>
	/// Linear interpolated fan curve used by software controller.
	/// </summary>
	Linear
}