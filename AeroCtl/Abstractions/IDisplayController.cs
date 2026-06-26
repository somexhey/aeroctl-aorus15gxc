using System.Collections.Generic;
using System.Threading.Tasks;

namespace AeroCtl;

public interface IDisplayController
{
	/// <summary>
	/// Gets or sets the current display brightness.
	/// </summary>
	int Brightness { get; set; }

	/// <summary>
	/// Toggles the screen backlight on/off.
	/// </summary>
	/// <returns></returns>
	Task<bool> ToggleScreenAsync();

	/// <summary>
	/// Gets the lid status.
	/// </summary>
	/// <returns></returns>
	Task<LidStatus> GetLidStatus();

	/// <summary>
	/// Returns the name of the integrated display, if it is connected.
	/// </summary>
	/// <returns>The device name of the integrated display, or null if not connected.</returns>
	string GetIntegratedDisplayName();

	/// <summary>
	/// Returns the current frequency of the integrated display.
	/// </summary>
	/// <returns></returns>
	uint? GetIntegratedDisplayFrequency();

	/// <summary>
	/// Enumerates the supported display frequencies of the built-in display.
	/// </summary>
	/// <returns></returns>
	IEnumerable<uint> GetIntegratedDisplayFrequencies();

	/// <summary>
	/// Changes the display frequency of the built-in display.
	/// </summary>
	/// <param name="newFreq"></param>
	/// <returns></returns>
	bool SetIntegratedDisplayFrequency(uint newFreq);
}