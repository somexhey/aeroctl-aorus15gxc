using System;

namespace AeroCtl;

public interface IKeyboardController
{
	event EventHandler<FnKeyEventArgs> FnKeyPressed;

	/// <summary>
	/// Gets the <see cref="IRgbController"/> implementation for this keyboard.
	/// </summary>
	IRgbController Rgb { get; }
}