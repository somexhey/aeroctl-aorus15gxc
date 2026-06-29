using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AeroCtl.UI.SoftwareFan;

namespace AeroCtl.UI;

/// <summary>
/// Contain the current state of the laptop for data binding and controls its various properties.
/// </summary>
public class AeroController : INotifyPropertyChanged
{
	#region Fields

	private SoftwareFanController swFanController;
	private readonly AutoStaticControl autoStaticControl;
	private readonly AsyncLocal<bool> updating;
	private bool loading;
	private readonly ConcurrentQueue<Func<Task>> updates;

	#endregion

	#region Aero

	/// <summary>
	/// The wrapped <see cref="Aero"/> instance.
	/// </summary>
	public Aero Aero { get; }

	#endregion

	#region StartMinimized

	private bool startMinimized;

	public bool StartMinimized
	{
		get => this.startMinimized;
		set
		{
			this.startMinimized = value;
			this.OnPropertyChanged();

			if (!this.loading)
			{
				AeroSettings.Default.StartMinimized = value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region AutoStart

	private bool autoStart;

	public bool AutoStart
	{
		get => this.autoStart;
		set
		{
			this.autoStart = value;
			this.OnPropertyChanged();

			if (!this.loading)
			{
				using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
				if (value)
					key?.SetValue("AeroCtl.UI", $"\"{Environment.ProcessPath}\"");
				else
					key?.DeleteValue("AeroCtl.UI", false);

				AeroSettings.Default.AutoStart = value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region AutoRestart

	private bool autoRestart = true;

	public bool AutoRestart
	{
		get => this.autoRestart;
		set
		{
			this.autoRestart = value;
			this.OnPropertyChanged();

			if (!this.loading)
			{
				if (value)
					RegisterForRestart();
				else
					UnregisterForRestart();

				AeroSettings.Default.AutoRestart = value;
				AeroSettings.Save();
			}
		}
	}

	/// <summary>
	/// Registers the process for automatic restart via Windows Application Restart &amp; Recovery.
	/// </summary>
	private static void RegisterForRestart()
	{
		try
		{
			string exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "AeroCtl.UI.exe");
			NativeMethods.RegisterApplicationRestart($"\"{exe}\"", 0);
		}
		catch { }
	}

	private static void UnregisterForRestart()
	{
		try { NativeMethods.UnregisterApplicationRestart(); }
		catch { }
	}

	#endregion

	#region BaseBoard

	private string baseBoard;

	public string BaseBoard
	{
		get => this.baseBoard;
		private set
		{
			this.baseBoard = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region Sku

	private string sku;

	public string Sku
	{
		get => this.sku;
		private set
		{
			this.sku = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region BiosVersion

	private string biosVersion;

	public string BiosVersion
	{
		get => this.biosVersion;
		private set
		{
			this.biosVersion = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region SerialNumber

	private string serialNumber;

	public string SerialNumber
	{
		get => this.serialNumber;
		private set
		{
			this.serialNumber = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region KeyboardFWVersion

	private Version keyboardFwVersion;

	public Version KeyboardFwVersion
	{
		get => this.keyboardFwVersion;
		private set
		{
			this.keyboardFwVersion = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region CpuTemperature

	private double cpuTemperature;

	public double CpuTemperature
	{
		get => this.cpuTemperature;
		private set
		{
			this.cpuTemperature = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region GpuTemperature

	private double gpuTemperature;

	public double GpuTemperature
	{
		get => this.gpuTemperature;
		private set
		{
			this.gpuTemperature = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region FanRpm1

	private int fanRpm1;

	public int FanRpm1
	{
		get => this.fanRpm1;
		private set
		{
			this.fanRpm1 = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region FanRpm2

	private int fanRpm2;

	public int FanRpm2
	{
		get => this.fanRpm2;
		private set
		{
			this.fanRpm2 = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region FanPwm

	private double fanPwm;

	public double FanPwm
	{
		get => this.fanPwm;
		private set
		{
			this.fanPwm = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region DisplayBrightness

	private int displayBrightness;

	public int DisplayBrightness
	{
		get => this.displayBrightness;
		set
		{
			this.displayBrightness = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.Aero.Display.Brightness = value;
		}
	}

	#endregion

	#region AutoStaticControl

	public bool AutoStaticControl
	{
		get => this.autoStaticControl.IsEnabled;
		set
		{
			this.autoStaticControl.IsEnabled = value;
			this.OnPropertyChanged();

			if (!this.loading)
			{
				AeroSettings.Default.AutoStaticControl = value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region WifiEnabled

	private bool? wifiEnabled;

	public bool? WifiEnabled
	{
		get => this.wifiEnabled;
		set
		{
			this.wifiEnabled = value;
			this.OnPropertyChanged();

			if (this.updating.Value) return;
			if (!value.HasValue) return;

			bool en = value.Value;
			this.updates.Enqueue(async () => await this.Aero.SetWifiEnabledAsync(en));
		}
	}

	#endregion

	#region CameraEnabled

	private bool? cameraEnabled;

	public bool? CameraEnabled
	{
		get => this.cameraEnabled;
		set
		{
			this.cameraEnabled = value;
			this.OnPropertyChanged();

			if (this.updating.Value) return;
			if (!value.HasValue) return;

			bool en = value.Value;
			this.updates.Enqueue(async () => await this.Aero.SetCameraEnabledAsync(en));
		}
	}

	#endregion

	#region SleepUsbCharge

	private bool? sleepUsbCharge;

	public bool? SleepUsbCharge
	{
		get => this.sleepUsbCharge;
		set
		{
			this.sleepUsbCharge = value;
			this.OnPropertyChanged();

			if (this.updating.Value) return;
			if (!value.HasValue) return;

			bool en = value.Value;
			this.updates.Enqueue(async () => await this.Aero.SetSleepUsbCharge(en));
		}
	}

	private bool sleepUsbChargeSupported;

	public bool SleepUsbChargeSupported
	{
		get => this.sleepUsbChargeSupported;
		private set
		{
			this.sleepUsbChargeSupported = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region HibernationUsbCharge

	private bool? hibernationUsbCharge;

	public bool? HibernationUsbCharge
	{
		get => this.hibernationUsbCharge;
		set
		{
			this.hibernationUsbCharge = value;
			this.OnPropertyChanged();

			if (this.updating.Value) return;
			if (!value.HasValue) return;

			bool en = value.Value;
			this.updates.Enqueue(async () => await this.Aero.SetHibernationUsbCharge(en));
		}
	}

	private bool hibernationUsbChargeSupported;

	public bool HibernationUsbChargeSupported
	{
		get => this.hibernationUsbChargeSupported;
		private set
		{
			this.hibernationUsbChargeSupported = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region BluetoothEnabled

	private bool bluetoothEnabled;

	public bool BluetoothEnabled
	{
		get => this.bluetoothEnabled;
		set
		{
			this.bluetoothEnabled = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.updates.Enqueue(() => this.Aero.Bluetooth.SetEnabledAsync(value));
		}
	}

	#endregion

	#region PowerLineStatus

	private BatteryState batteryState;

	public BatteryState BatteryState
	{
		get => this.batteryState;
		private set
		{
			this.batteryState = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region BatteryString

	public string BatteryString
	{
		get
		{
			StringBuilder str = new StringBuilder();

			str.Append("Charge: ");
			str.Append(this.BatteryChargePercent);
			str.Append(" % (");
			str.Append(this.BatteryCharge.ToString("F1", CultureInfo.InvariantCulture));
			str.Append(" Wh");

			if (Math.Abs(this.BatteryChargeRate) > 0.0)
			{
				str.Append(" +");
				str.Append(this.batteryChargeRate.ToString("F1", CultureInfo.InvariantCulture));
				str.Append(" W");
			}

			if (Math.Abs(this.BatteryDischargeRate) > 0.0)
			{
				str.Append(" -");
				str.Append(this.BatteryDischargeRate.ToString("F1", CultureInfo.InvariantCulture));
				str.Append(" W");
			}

			if (Math.Abs(this.BatteryVoltage) > 0.0)
			{
				str.Append(" @ ");
				str.Append(this.BatteryVoltage.ToString("F2", CultureInfo.InvariantCulture));
				str.Append(" V");
			}

			str.Append(")");

			return str.ToString();
		}
	}

	#endregion

	#region BatteryCycles

	private int? batteryCycles;

	public int? BatteryCycles
	{
		get => this.batteryCycles;
		private set
		{
			this.batteryCycles = value;
			this.OnPropertyChanged();
			this.OnPropertyChanged(nameof(this.BatteryString));
		}
	}

	#endregion

	#region BatteryChargePercent

	private int batteryChargePercent;

	public int BatteryChargePercent
	{
		get => this.batteryChargePercent;
		private set
		{
			this.batteryChargePercent = value;
			this.OnPropertyChanged();
			this.OnPropertyChanged(nameof(this.BatteryString));
		}
	}

	#endregion

	#region BatteryCharge

	private double batteryCharge;

	public double BatteryCharge
	{
		get => this.batteryCharge;
		private set
		{
			this.batteryCharge = value;
			this.OnPropertyChanged();
			this.OnPropertyChanged(nameof(this.BatteryString));
		}
	}

	#endregion

	#region BatteryChargeRate

	private double batteryChargeRate;

	public double BatteryChargeRate
	{
		get => this.batteryChargeRate;
		private set
		{
			this.batteryChargeRate = value;
			this.OnPropertyChanged();
			this.OnPropertyChanged(nameof(this.BatteryString));
		}
	}

	#endregion

	#region BatteryDischargeRate

	private double batteryDischargeRate;

	public double BatteryDischargeRate
	{
		get => this.batteryDischargeRate;
		private set
		{
			this.batteryDischargeRate = value;
			this.OnPropertyChanged();
			this.OnPropertyChanged(nameof(this.BatteryString));
		}
	}

	#endregion

	#region BatteryVoltage

	private double batteryVoltage;

	public double BatteryVoltage
	{
		get => this.batteryVoltage;
		private set
		{
			this.batteryVoltage = value;
			this.OnPropertyChanged();
			this.OnPropertyChanged(nameof(this.BatteryString));
		}
	}

	#endregion

	#region BatteryHealth

	private int? batteryHealth;

	public int? BatteryHealth
	{
		get => this.batteryHealth;
		private set
		{
			this.batteryHealth = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region SmartCharge

	private bool smartCharge;

	public bool SmartCharge
	{
		get => this.smartCharge;
		set
		{
			this.smartCharge = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.updates.Enqueue(() => this.Aero.Battery.SetSmargeChargeAsync(value));
		}
	}

	#endregion

	#region ChargeStopEnabled

	private bool chargeStopEnabled;

	public bool ChargeStopEnabled
	{
		get => this.chargeStopEnabled;
		set
		{
			this.chargeStopEnabled = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.updates.Enqueue(() => this.Aero.Battery.SetChargePolicyAsync(value ? ChargePolicy.CustomStop : ChargePolicy.Full));

			if (!this.loading && !this.updating.Value)
			{
				AeroSettings.Default.ChargeStop = this.ChargeStopEnabled ? this.ChargeStop : -1;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region ChargeStop

	private int chargeStop;

	public int ChargeStop
	{
		get => this.chargeStop;
		set
		{
			this.chargeStop = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.updates.Enqueue(() => this.Aero.Battery.SetChargeStopAsync(value));

			if (!this.loading && !this.updating.Value)
			{
				AeroSettings.Default.ChargeStop = this.ChargeStopEnabled ? this.ChargeStop : -1;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region FanProfileInvalid

	private bool fanProfileInvalid;

	public bool FanProfileInvalid
	{
		get => this.fanProfileInvalid;
		set
		{
			this.fanProfileInvalid = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region FanProfile

	private FanProfile fanProfile;

	public FanProfile FanProfile
	{
		get => this.fanProfile;
		set
		{
			this.fanProfile = value;
			this.OnPropertyChanged();

			this.FanProfileInvalid = true;

			if (!this.loading)
			{
				AeroSettings.Default.FanProfile = (int)value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region FanException

	private Exception fanException;

	public Exception FanException
	{
		get => this.fanException;
		set
		{
			this.fanException = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region FanProfileAlt

	private FanProfile fanProfileAlt;

	public FanProfile FanProfileAlt
	{
		get => this.fanProfileAlt;
		set
		{
			this.fanProfileAlt = value;
			this.OnPropertyChanged();

			if (!this.loading)
			{
				AeroSettings.Default.FanProfileAlt = (int)value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region FixedFanSpeed

	private double fixedFanSpeed = 0.25;

	public double FixedFanSpeed
	{
		get => this.fixedFanSpeed;
		set
		{
			this.fixedFanSpeed = value;
			this.OnPropertyChanged();

			this.FanProfileInvalid = true;

			if (!this.loading)
			{
				AeroSettings.Default.FixedFanSpeed = value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region AutoFanAdjust

	private double autoFanAdjust = 0.25;

	public double AutoFanAdjust
	{
		get => this.autoFanAdjust;
		set
		{
			this.autoFanAdjust = value;
			this.OnPropertyChanged();

			this.FanProfileInvalid = true;

			if (!this.loading)
			{
				AeroSettings.Default.AutoFanAdjust = value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region SoftwareFanConfig

	private FanConfig softwareFanConfig;

	public FanConfig SoftwareFanConfig
	{
		get => this.softwareFanConfig;
		set
		{
			this.softwareFanConfig = value;
			this.OnPropertyChanged();

			this.FanProfileInvalid = true;

			if (!this.loading)
			{
				AeroSettings.Default.SoftwareFanConfig = value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region GpuConfigAvailable

	private bool gpuConfigAvailable;

	public bool GpuConfigAvailable
	{
		get => this.gpuConfigAvailable;
		private set
		{
			this.gpuConfigAvailable = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region GpuAiBoost

	private bool gpuAiBoost;
	private bool gpuAiBoostSupported;

	public bool GpuAiBoost
	{
		get => this.gpuAiBoost;
		set
		{
			this.gpuAiBoost = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.updates.Enqueue(() => ((P7GpuController)this.Aero.Gpu).SetAiBoostEnabledAsync(value));
		}
	}

	public bool GpuAiBoostSupported
	{
		get => this.gpuAiBoostSupported;
		private set
		{
			this.gpuAiBoostSupported = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region GpuAiBoost

	private bool gpuDynamicBoost;
	private bool gpuDynamicBoostSupported;

	public bool GpuDynamicBoost
	{
		get => this.gpuDynamicBoost;
		set
		{
			this.gpuDynamicBoost = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.updates.Enqueue(() => ((P7GpuController)this.Aero.Gpu).SetDynamicBoostAsync(value));
		}
	}

	public bool GpuDynamicBoostSupported
	{
		get => this.gpuDynamicBoostSupported;
		private set
		{
			this.gpuDynamicBoostSupported = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region GpuPowerConfig

	private bool gpuPowerConfig;
	private bool gpuPowerConfigSupported;

	public bool GpuPowerConfig
	{
		get => this.gpuPowerConfig;
		set
		{
			this.gpuPowerConfig = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.updates.Enqueue(() => ((P7GpuController)this.Aero.Gpu).SetPowerConfigAsync(value));
		}
	}

	public bool GpuPowerConfigSupported
	{
		get => this.gpuPowerConfigSupported;
		private set
		{
			this.gpuPowerConfigSupported = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region GpuThermalTarget

	private bool gpuThermalTarget;
	private bool gpuThermalTargetSupported;

	public bool GpuThermalTarget
	{
		get => this.gpuThermalTarget;
		set
		{
			this.gpuThermalTarget = value;
			this.OnPropertyChanged();

			if (!this.updating.Value)
				this.updates.Enqueue(() => ((P7GpuController)this.Aero.Gpu).SetThermalTargetEnabledAsync(value));
		}
	}

	public bool GpuThermalTargetSupported
	{
		get => this.gpuThermalTargetSupported;
		private set
		{
			this.gpuThermalTargetSupported = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region GpuMode

	private int gpuMode = -1;
	private bool gpuModeSupported;

	public int GpuMode
	{
		get => this.gpuMode;
		set
		{
			if (this.gpuMode == value)
				return;
			this.gpuMode = value;
			this.OnPropertyChanged();

			if (value >= 0 && value <= 1 && !this.updating.Value)
				this.updates.Enqueue(() => ((P7GpuController)this.Aero.Gpu).SetGpuModeAsync(value));
		}
	}

	public bool GpuModeSupported
	{
		get => this.gpuModeSupported;
		private set
		{
			this.gpuModeSupported = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region DisplayAvailable

	private bool displayAvailable;

	public bool DisplayAvailable
	{
		get => this.displayAvailable;
		private set
		{
			this.displayAvailable = value;
			this.OnPropertyChanged();
		}
	}

	#endregion

	#region DisplayFrequency

	private uint? displayFrequency;

	public uint? DisplayFrequency
	{
		get => this.displayFrequency;
		set
		{
			this.displayFrequency = value;
			this.OnPropertyChanged();

			if (!this.updating.Value && value.HasValue)
				this.updates.Enqueue(() => Task.Run(() => { this.Aero.Display.SetIntegratedDisplayFrequency(value.Value); }));
		}
	}

	#endregion

	#region DisplayFrequencies

	private IReadOnlyList<uint> displayFrequencies;

	public IReadOnlyList<uint> DisplayFrequencies
	{
		get => this.displayFrequencies;
		private set
		{
			this.displayFrequencies = value;
			this.OnPropertyChanged();
			this.OnPropertyChanged(nameof(this.DisplayFrequencyChoices));
		}
	}

	public IReadOnlyList<uint> DisplayFrequencyChoices
	{
		get
		{
			List<uint> frequencies = new((this.DisplayFrequencies?.Count ?? 0) + 1) { 0 };

			if (this.DisplayFrequencies != null)
			{
				frequencies.AddRange(this.DisplayFrequencies);
			}

			return frequencies;
		}
	}

	#endregion

	#region DisplayFrequencyAc

	private uint displayFrequencyAc;

	public uint DisplayFrequencyAc
	{
		get => this.displayFrequencyAc;
		set
		{
			this.displayFrequencyAc = value;
			this.OnPropertyChanged();

			if (!this.loading)
			{
				AeroSettings.Default.DisplayFrequencyAc = value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region DisplayFrequencyDc

	private uint displayFrequencyDc;

	public uint DisplayFrequencyDc
	{
		get => this.displayFrequencyDc;
		set
		{
			this.displayFrequencyDc = value;
			this.OnPropertyChanged();

			if (!this.loading)
			{
				AeroSettings.Default.DisplayFrequencyDc = value;
				AeroSettings.Save();
			}
		}
	}

	#endregion

	#region RgbControlAvailable

	public bool RgbControlAvailable => this.Aero.Keyboard.Rgb != null;

	#endregion

	#region Constructors

	public AeroController(Aero aero)
	{
		this.Aero = aero;
		this.autoStaticControl = new AutoStaticControl(this);
		this.updating = new AsyncLocal<bool>();
		this.updates = new ConcurrentQueue<Func<Task>>();
	}

	#endregion

	#region Methods

	public void Load()
	{
		this.loading = true;
		try
		{
			AeroSettings s = AeroSettings.Default;
			this.StartMinimized = s.StartMinimized;
			this.AutoStart = s.AutoStart;
			this.AutoRestart = s.AutoRestart;
			this.FanProfile = (FanProfile)s.FanProfile;
			this.FanProfileAlt = (FanProfile)s.FanProfileAlt;
			this.FixedFanSpeed = s.FixedFanSpeed;
			this.AutoFanAdjust = s.AutoFanAdjust;
			this.DisplayFrequencyAc = s.DisplayFrequencyAc;
			this.DisplayFrequencyDc = s.DisplayFrequencyDc;
			this.ChargeStopEnabled = s.ChargeStop >= 0;
			this.ChargeStop = s.ChargeStop >= 0 ? s.ChargeStop : 97;
			this.SoftwareFanConfig = s.SoftwareFanConfig ?? new FanConfig();
			this.AutoStaticControl = s.AutoStaticControl;
		}
		finally
		{
			this.FanProfileInvalid = true;
			this.loading = false;
		}

		if (this.autoRestart)
			RegisterForRestart();
	}

	private async Task applyFanProfileAsync()
	{
		FanProfile newProfile = this.FanProfile;
		Debug.WriteLine($"Applying fan profile {newProfile}");

		if (this.swFanController != null)
		{
			SoftwareFanController swCtl = this.swFanController;
			this.swFanController = null;

			await swCtl.StopAsync();
		}

		switch (newProfile)
		{
			case FanProfile.Quiet:
				await this.Aero.Fans.SetQuietAsync();
				break;
			case FanProfile.Normal:
				await this.Aero.Fans.SetNormalAsync();
				break;
			case FanProfile.Gaming:
				await this.Aero.Fans.SetGamingAsync();
				break;
			case FanProfile.Fixed:
				await this.Aero.Fans.SetFixedAsync(this.FixedFanSpeed);
				break;
			case FanProfile.Auto:
				await this.Aero.Fans.SetAutoAsync(this.AutoFanAdjust);
				break;
			case FanProfile.Custom:
				await this.Aero.Fans.SetCustomAsync();
				break;
			case FanProfile.Software:
				this.swFanController = new SoftwareFanController(this.SoftwareFanConfig, new FanProviderImpl(this));
				break;
			default:
				throw new InvalidEnumArgumentException(nameof(this.FanProfile), (int)newProfile, typeof(FanProfile));
		}
	}

	public async Task UpdateAsync(UpdateMode mode)
	{
		while (this.updates.TryDequeue(out Func<Task> updateFunc))
			await updateFunc();

		Debug.Assert(!this.updating.Value);

		this.updating.Value = true;
		try
		{
			if (mode >= UpdateMode.Full)
			{
				this.BaseBoard = this.Aero.BaseBoard;
				this.Sku = this.Aero.Sku;
				this.SerialNumber = this.Aero.SerialNumber;
				this.BiosVersion = string.Join("; ", this.Aero.BiosVersions);
				this.BatteryState = this.Aero.Battery.State;

				if (this.Aero.Keyboard.Rgb != null)
					this.KeyboardFwVersion = await this.Aero.Keyboard.Rgb.GetFirmwareVersionAsync();
			}

			if (mode >= UpdateMode.Normal)
			{
				if (this.FanProfileInvalid)
				{
					this.FanProfileInvalid = false;
					try
					{
						await this.applyFanProfileAsync();
						this.FanException = null;
					}
					catch (Exception ex)
					{
						this.FanException = ex;
					}
				}

				if (this.Aero.Gpu is P7GpuController newGpu)
				{
					this.GpuConfigAvailable = true;

					// Default to true for undetermined support status.
					this.GpuAiBoostSupported = newGpu.AiBoostSupported ?? true;
					this.GpuPowerConfigSupported = newGpu.PowerConfigSupported ?? true;
					this.GpuDynamicBoostSupported = newGpu.DynamicBoostSupported ?? true;
					this.GpuThermalTargetSupported = newGpu.ThermalTargetSupported ?? true;
					this.GpuModeSupported = newGpu.GpuModeSupported ?? true;

					// Update GPU settings for those that are supported.
					try
					{
						this.GpuAiBoost = this.GpuAiBoostSupported && await newGpu.GetAiBoostEnabledAsync();
					}
					catch (Exception ex)
					{
						File.AppendAllText("aeroctl_debug.log", $"{DateTime.Now:HH:mm:ss.fff} GetAiBoostEnabledAsync threw: {ex}\n");
					}

					try
					{
						this.GpuPowerConfig = this.GpuPowerConfigSupported && await newGpu.GetPowerConfigAsync();
					}
					catch (Exception ex)
					{
						File.AppendAllText("aeroctl_debug.log", $"{DateTime.Now:HH:mm:ss.fff} GetPowerConfigAsync threw: {ex}\n");
					}

					try
					{
						this.GpuDynamicBoost = this.GpuDynamicBoostSupported && await newGpu.GetDynamicBoostAsync();
					}
					catch (Exception ex)
					{
						File.AppendAllText("aeroctl_debug.log", $"{DateTime.Now:HH:mm:ss.fff} GetDynamicBoostAsync threw: {ex}\n");
					}

					try
					{
						this.GpuThermalTarget = this.GpuThermalTargetSupported && await newGpu.GetThermalTargetEnabledAsync();
					}
					catch (Exception ex)
					{
						File.AppendAllText("aeroctl_debug.log", $"{DateTime.Now:HH:mm:ss.fff} GetThermalTargetEnabledAsync threw: {ex}\n");
					}

					try
					{
						this.GpuMode = await newGpu.GetGpuModeAsync();
					}
					catch (Exception ex)
					{
						File.AppendAllText("aeroctl_debug.log", $"{DateTime.Now:HH:mm:ss.fff} GetGpuModeAsync threw: {ex}\n");
					}
				}

				(this.FanRpm1, this.FanRpm2) = await this.Aero.Fans.GetRpmAsync();
				this.FanPwm = await this.Aero.Fans.GetPwmAsync() * 100;
				this.DisplayBrightness = this.Aero.Display.Brightness;
				this.DisplayFrequency = this.Aero.Display.GetIntegratedDisplayFrequency();
				this.DisplayAvailable = this.DisplayFrequency != null;
				this.DisplayFrequencies = this.Aero.Display.GetIntegratedDisplayFrequencies().OrderBy(hz => hz).ToImmutableArray();

				this.SmartCharge = await this.Aero.Battery.GetSmartChargeAsync();
				this.ChargeStopEnabled = await this.Aero.Battery.GetChargePolicyAsync() == ChargePolicy.CustomStop;
				this.ChargeStop = await this.Aero.Battery.GetChargeStopAsync();
				this.BatteryCycles = await this.Aero.Battery.GetCyclesAsync();
				this.BatteryHealth = await this.Aero.Battery.GetHealthAsync();

				BatteryStatus status = await this.Aero.Battery.GetStatusAsync();
				this.BatteryCharge = status.Charge;
				this.BatteryChargePercent = status.ChargePercent;
				this.BatteryChargeRate = status.ChargeRate;
				this.BatteryDischargeRate = status.DischargeRate;
				this.BatteryVoltage = status.Voltage;

				this.WifiEnabled = await this.Aero.GetWifiEnabledAsync();
				this.BluetoothEnabled = await this.Aero.Bluetooth.GetEnabledAsync();
				this.CameraEnabled = await this.Aero.GetCameraEnabledAsync();
				this.SleepUsbCharge = await this.Aero.GetSleepUsbCharge();
				this.SleepUsbChargeSupported = this.SleepUsbCharge != null;
				this.HibernationUsbCharge = await this.Aero.GetHibernationUsbCharge();
				this.HibernationUsbChargeSupported = this.HibernationUsbCharge != null;
			}

			if (mode >= UpdateMode.Normal || this.fanProfile == FanProfile.Software)
			{
				// Only update if UI is visible or software fan is on.
				this.CpuTemperature = await this.Aero.Cpu.GetTemperatureAsync();
				this.GpuTemperature = await this.Aero.Gpu.GetTemperatureAsync() ?? 0.0;
			}

			if (mode >= UpdateMode.Normal || this.DisplayFrequencyDc > 0 || this.DisplayFrequencyAc > 0)
			{
				// Only update if UI is visible or display Hz per battery mode is set.
				BatteryState prevBatteryState = this.BatteryState;
				this.BatteryState = this.Aero.Battery.State;

				if (this.BatteryState != prevBatteryState)
				{
					if (this.BatteryState == BatteryState.DC && this.DisplayFrequencyDc > 0)
					{
						Debug.WriteLine($"Changing display frequency to {this.DisplayFrequencyDc}");
						this.Aero.Display.SetIntegratedDisplayFrequency(this.DisplayFrequencyDc);
					}

					if (this.BatteryState != BatteryState.DC && this.DisplayFrequencyAc > 0)
					{
						Debug.WriteLine($"Changing display frequency to {this.DisplayFrequencyAc}");
						this.Aero.Display.SetIntegratedDisplayFrequency(this.DisplayFrequencyAc);
					}
				}
			}
		}
		finally
		{
			this.updating.Value = false;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public async ValueTask DisposeAsync()
	{
		if (this.swFanController != null)
		{
			await this.swFanController.StopAsync();
			this.swFanController = null;
		}
	}

	public async ValueTask<bool> ResetKeyboard()
	{
		await this.Aero.Keyboard.Rgb.ResetAsync();
		return true;
	}

	#endregion

	#region Nested Types

	/// <summary>
	/// <see cref="ISoftwareFanProvider"/> implementation for the software fan.
	/// </summary>
	private sealed class FanProviderImpl : ISoftwareFanProvider
	{
		private readonly AeroController controller;

		public FanProviderImpl(AeroController controller)
		{
			this.controller = controller;
		}

		public ValueTask<double> GetTemperatureAsync(CancellationToken cancellationToken)
		{
			return new ValueTask<double>(Math.Max(this.controller.CpuTemperature, this.controller.GpuTemperature));
		}

		public async ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken)
		{
			if (this.controller.Aero.Fans is IFanControllerSync syncController)
			{
				syncController.SetFixed(speed);
			}
			else
			{
				await this.controller.Aero.Fans.SetFixedAsync(speed);
			}
		}
	}

	#endregion
}

internal static class NativeMethods
{
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	internal static extern uint RegisterApplicationRestart(string pwzCommandline, uint dwFlags);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	internal static extern uint UnregisterApplicationRestart();
}