using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Json;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AeroCtl.UI.SoftwareFan;

/// <summary>
/// Configures the software fan controller.
/// </summary>
public class FanConfig : INotifyPropertyChanged
{
	private ImmutableArray<FanPoint> curve;
	private TimeSpan interval;
	private double rampUpSpeed;
	private double rampDownSpeed;
	private FanSchedulingMode schedulingMode;

	/// <summary>
	/// Gets or sets the fan curve.
	/// </summary>
	public ImmutableArray<FanPoint> Curve
	{
		get => this.curve;
		set
		{
			this.curve = value;
			this.OnPropertyChanged();
		}
	}

	/// <summary>
	/// Time between updates.
	/// </summary>
	public TimeSpan Interval
	{
		get => this.interval;
		set
		{
			this.interval = value;
			this.OnPropertyChanged();
		}
	}

	/// <summary>
	/// Maximum fan ramp up speed per second.
	/// </summary>
	public double RampUpSpeed
	{
		get => this.rampUpSpeed;
		set
		{
			this.rampUpSpeed = value;
			this.OnPropertyChanged();
		}
	}

	/// <summary>
	/// Maximum fan ramp down speed per second.
	/// </summary>
	public double RampDownSpeed
	{
		get => this.rampDownSpeed;
		set
		{
			this.rampDownSpeed = value;
			this.OnPropertyChanged();
		}
	}

	/// <summary>
	/// The fan controller scheduling mode.
	/// </summary>
	public FanSchedulingMode SchedulingMode
	{
		get => this.schedulingMode;
		set
		{
			this.schedulingMode = value;
			this.OnPropertyChanged();
		}
	}

	[JsonIgnore]
	public bool IsValid
	{
		get
		{
			if (this.Curve.IsDefaultOrEmpty)
				return false;

			if (this.Curve.Any(p => p.FanSpeed is < 0.0 or > 1.0))
				return false;

			if (this.RampDownSpeed <= 0.0)
				return false;

			if (this.RampUpSpeed <= 0.0)
				return false;

			if (!Enum.IsDefined(typeof(FanSchedulingMode), this.SchedulingMode))
				return false;

			return true;
		}
	}

	public FanConfig()
	{
		this.Curve = [new FanPoint(0.0, 0.0), new FanPoint(100.0, 1.0)];
		this.Interval = TimeSpan.FromSeconds(1.0 / 3.0);
		this.RampUpSpeed = 0.10;
		this.RampDownSpeed = 0.03;
		this.SchedulingMode = FanSchedulingMode.AboveNormalThread;
	}

	public FanConfig(FanConfig other)
	{
		this.Curve = other.Curve;
		this.Interval = other.interval;
		this.RampUpSpeed = other.RampUpSpeed;
		this.RampDownSpeed = other.RampDownSpeed;
		this.SchedulingMode = other.SchedulingMode;
	}

	public JsonObject ToJson()
	{
		JsonArray curve = [];
		foreach (FanPoint p in this.Curve)
		{
			curve.Add(new JsonObject()
			{
				["temperature"] = p.Temperature,
				["speed"] = p.FanSpeed
			});
		}

		return new JsonObject
		{
			["curve"] = curve,
			["rampUpSpeed"] = this.RampUpSpeed,
			["rampDownSpeed"] = this.RampDownSpeed,
			["interval"] = this.Interval.ToString("G", CultureInfo.InvariantCulture),
			["schedMode"] = this.SchedulingMode.ToString()
		};
	}

	public static FanConfig FromJson(JsonObject obj)
	{
		ImmutableArray<FanPoint>.Builder curve = ImmutableArray.CreateBuilder<FanPoint>();

		foreach (JsonObject p in ((JsonArray)obj["curve"]).OfType<JsonObject>())
		{
			curve.Add(new FanPoint((double)p["temperature"], (double)p["speed"]));
		}

		return new FanConfig
		{
			Curve = curve.ToImmutable(),
			RampUpSpeed = obj["rampUpSpeed"],
			RampDownSpeed = obj["rampDownSpeed"],
			Interval = TimeSpan.Parse(obj["interval"], CultureInfo.InvariantCulture),
			SchedulingMode = (FanSchedulingMode)Enum.Parse(typeof(FanSchedulingMode), obj["schedMode"], true)
		};
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}