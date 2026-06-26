using System;
using System.Threading.Tasks;

namespace AeroCtl;

public interface ITouchpadController
{
	event EventHandler EnabledChanged;

	ValueTask<bool> GetEnabledAsync();
}