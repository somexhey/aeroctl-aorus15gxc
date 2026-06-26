using System.ComponentModel;

namespace AeroCtl.UI.SoftwareFan;

public enum FanSchedulingMode
{
	[Description("Async task")]
	AsyncTask,

	[Description("Thread (normal prio)")]
	NormalThread,

	[Description("Thread (above normal prio)")]
	AboveNormalThread,

	[Description("Thread (highest prio)")]
	HighestThread,
}