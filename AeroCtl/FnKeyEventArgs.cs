using System;

namespace AeroCtl;

public class FnKeyEventArgs(FnKey key) : EventArgs
{
	public FnKey Key { get; } = key;
}