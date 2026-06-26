using System;
using System.Threading.Tasks;

namespace AeroCtl;

public interface IRgbController
{
	/// <summary>
	/// Reads the keyboard firmware version.
	/// </summary>
	/// <returns></returns>
	ValueTask<Version> GetFirmwareVersionAsync();
		
	/// <summary>
	/// Sets the RGB light effect.
	/// </summary>
	/// <param name="effect"></param>
	/// <returns></returns>
	ValueTask SetEffectAsync(RgbEffect effect);
		
	/// <summary>
	/// Gets the current light effect.
	/// </summary>
	/// <returns></returns>
	ValueTask<RgbEffect> GetEffectAsync();

	/// <summary>
	/// Sets the image for a custom effect.
	/// </summary>
	/// <param name="index">The custom effect index, corresponds to the "Custom" entries in <see cref="RgbEffectType"/>.</param>
	/// <param name="image">The RGB values (4 bytes per pixel/key).</param>
	/// <returns></returns>
	ValueTask SetImageAsync(int index, ReadOnlyMemory<byte> image);

	/// <summary>
	/// Gets the image for a custom effect.
	/// </summary>
	/// <param name="index"></param>
	/// <param name="image"></param>
	/// <returns></returns>
	ValueTask GetImageAsync(int index, Memory<byte> image);

	/// <summary>
	/// Performs a factory reset.
	/// </summary>
	/// <returns></returns>
	ValueTask ResetAsync();
}