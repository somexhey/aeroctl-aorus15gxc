using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Win32;
using AeroCtl;

namespace AeroCtl.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
	private const int notificationTimeout = 3000;

	private readonly string title;
	private readonly CancellationTokenSource cancellationTokenSource;
	private NotifyIcon trayIcon;
	private Aero aero;
	private AeroController controller;
	private Task updateTask;

	private readonly object windowLock;
	private MainWindow window;

	public App()
	{
		this.title = typeof(App).Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? typeof(App).Assembly.GetName().Name;
		this.cancellationTokenSource = new CancellationTokenSource();
		this.windowLock = new object();
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		// Create aero and controller.
		this.aero = new Aero();
		this.controller = new AeroController(this.aero);
		this.controller.Load();

		// Create background update task.
		this.updateTask = this.Dispatcher.InvokeAsync(() => this.updateLoop(this.cancellationTokenSource.Token), DispatcherPriority.Normal).Task;

		// Get app icon.
		Icon ico;
		using (Stream stream = typeof(App).Assembly.GetManifestResourceStream("AeroCtl.UI.Main.ico"))
		{
			ico = new Icon(stream!);
		}

		// Create tray icon.
		this.trayIcon = new NotifyIcon
		{
			Icon = ico,
			Text = this.title,
			Visible = true,
		};

		this.trayIcon.DoubleClick += (_, _) => { this.showWindow(); };

		// Build tray context menu.
		var trayMenu = new ContextMenuStrip();

		trayMenu.Items.Add("Show window", null, (_, _) => this.showWindow());

		trayMenu.Items.Add(new ToolStripSeparator());

		// Fan profile submenu.
		var fanMenu = new ToolStripMenuItem("Fan profile");
		foreach (FanProfile fp in Enum.GetValues<FanProfile>())
		{
			var item = new ToolStripMenuItem(fp.ToString());
			item.Click += (_, _) =>
			{
				this.controller.FanProfile = fp;
				this.trayIcon.ShowBalloonTip(3000, this.title, $"Fan profile: {fp}", ToolTipIcon.Info);
			};
			fanMenu.DropDownItems.Add(item);
		}
		trayMenu.Items.Add(fanMenu);

		// GPU mode submenu.
		var gpuMenu = new ToolStripMenuItem("GPU mode");
		var gpuIgu = new ToolStripMenuItem("iGPU");
		gpuIgu.Click += (_, _) => this.Dispatcher.InvokeAsync(async () =>
		{
			if (this.controller.GpuModeSupported && this.controller.Aero.Gpu is P7GpuController gpu)
			{
				await gpu.SetGpuModeAsync(0);
				this.trayIcon.ShowBalloonTip(3000, this.title, "GPU mode: iGPU", ToolTipIcon.Info);
			}
		});
		var gpuDgu = new ToolStripMenuItem("dGPU");
		gpuDgu.Click += (_, _) => this.Dispatcher.InvokeAsync(async () =>
		{
			if (this.controller.GpuModeSupported && this.controller.Aero.Gpu is P7GpuController gpu)
			{
				await gpu.SetGpuModeAsync(1);
				this.trayIcon.ShowBalloonTip(3000, this.title, "GPU mode: dGPU", ToolTipIcon.Info);
			}
		});
		gpuMenu.DropDownItems.Add(gpuIgu);
		gpuMenu.DropDownItems.Add(gpuDgu);
		trayMenu.Items.Add(gpuMenu);

		// GPU Boost toggle.
		var gpuBoostItem = new ToolStripMenuItem("GPU Boost");
		gpuBoostItem.Click += (_, _) => this.Dispatcher.InvokeAsync(() =>
		{
			this.controller.GpuDynamicBoost = !this.controller.GpuDynamicBoost;
			gpuBoostItem.Checked = this.controller.GpuDynamicBoost;
			this.trayIcon.ShowBalloonTip(3000, this.title, $"GPU Boost: {(this.controller.GpuDynamicBoost ? "On" : "Off")}", ToolTipIcon.Info);
		});
		trayMenu.Items.Add(gpuBoostItem);

		// Power Config toggle.
		var powerConfigItem = new ToolStripMenuItem("Power Config (+10W)");
		powerConfigItem.Click += (_, _) => this.Dispatcher.InvokeAsync(() =>
		{
			this.controller.GpuPowerConfig = !this.controller.GpuPowerConfig;
			powerConfigItem.Checked = this.controller.GpuPowerConfig;
			this.trayIcon.ShowBalloonTip(3000, this.title, $"Power Config: {(this.controller.GpuPowerConfig ? "On" : "Off")}", ToolTipIcon.Info);
		});
		trayMenu.Items.Add(powerConfigItem);

		// Charge Stop submenu.
		var chargeMenu = new ToolStripMenuItem("Charge stop");
		var chargeOff = new ToolStripMenuItem("Disabled");
		chargeOff.Click += (_, _) => this.Dispatcher.InvokeAsync(() =>
		{
			this.controller.ChargeStopEnabled = false;
			foreach (ToolStripMenuItem child in chargeMenu.DropDownItems)
				child.Checked = child == chargeOff;
			this.trayIcon.ShowBalloonTip(3000, this.title, "Charge stop: disabled", ToolTipIcon.Info);
		});
		chargeMenu.DropDownItems.Add(chargeOff);

		foreach (int val in new[] { 30, 60, 80 })
		{
			var item = new ToolStripMenuItem($"{val}%");
			item.Click += (_, _) => this.Dispatcher.InvokeAsync(() =>
			{
				this.controller.ChargeStopEnabled = true;
				this.controller.ChargeStop = val;
				foreach (ToolStripMenuItem child in chargeMenu.DropDownItems)
					child.Checked = child == item;
				this.trayIcon.ShowBalloonTip(3000, this.title, $"Charge stop: {val}%", ToolTipIcon.Info);
			});
			chargeMenu.DropDownItems.Add(item);
		}
		trayMenu.Items.Add(chargeMenu);

		// RGB submenu.
		var rgbMenu = new ToolStripMenuItem("Set RGB");
		rgbMenu.DropDownItems.Add("Red", null, async (_, _) => await setRgbAsync(Color.Red));
		rgbMenu.DropDownItems.Add("Green", null, async (_, _) => await setRgbAsync(Color.Green));
		rgbMenu.DropDownItems.Add("Blue", null, async (_, _) => await setRgbAsync(Color.Blue));
		rgbMenu.DropDownItems.Add("Yellow", null, async (_, _) => await setRgbAsync(Color.Yellow));
		rgbMenu.DropDownItems.Add("Cyan", null, async (_, _) => await setRgbAsync(Color.Cyan));
		rgbMenu.DropDownItems.Add("Magenta", null, async (_, _) => await setRgbAsync(Color.Magenta));
		rgbMenu.DropDownItems.Add("White", null, async (_, _) => await setRgbAsync(Color.White));
		rgbMenu.DropDownItems.Add("Orange", null, async (_, _) => await setRgbAsync(Color.Orange));
		trayMenu.Items.Add(rgbMenu);

		trayMenu.Items.Add(new ToolStripSeparator());

		var exitItem = new ToolStripMenuItem("Exit");
		exitItem.Click += (_, _) => this.Shutdown();
		trayMenu.Items.Add(exitItem);

		this.trayIcon.ContextMenuStrip = trayMenu;

		// Handle Fn key events.
		this.aero.Keyboard.FnKeyPressed += (_, e2) => { this.Dispatcher.InvokeAsync(() => this.handleFnKey(e2)); };
		this.aero.Touchpad.EnabledChanged += (_, _) => { this.Dispatcher.InvokeAsync(() => this.onTouchpadEnabledChanged().AsTask()); };

		// To re-apply fan profile after wake up:
		SystemEvents.SessionSwitch += this.onSessionSwitch;
		SystemEvents.PowerModeChanged += this.onPowerModeChanged;

		if (!this.controller.StartMinimized || Debugger.IsAttached)
		{
			// Show window if 'start minimized' isn't active.
			this.showWindow();
		}
	}

	private async Task handleFnKey(FnKeyEventArgs e)
	{
		switch (e.Key)
		{
			case FnKey.IncreaseBrightness:
				this.aero.Display.Brightness = Math.Min(100, this.aero.Display.Brightness + 10);
				await WindowsOsd.ShowBrightnessAsync();
				break;

			case FnKey.DecreaseBrightness:
				this.aero.Display.Brightness = Math.Max(0, this.aero.Display.Brightness - 10);
				await WindowsOsd.ShowBrightnessAsync();
				break;

			case FnKey.ToggleFan:
				FanProfile fanProfile = this.controller.FanProfileAlt;
				this.controller.FanProfileAlt = this.controller.FanProfile;
				this.controller.FanProfile = fanProfile;

				this.trayIcon.ShowBalloonTip(notificationTimeout, this.title, $"Fan profile switched to \"{fanProfile}\".", ToolTipIcon.Info);
				break;

			case FnKey.ToggleWifi:
				bool? currentState = await this.aero.GetWifiEnabledAsync();
				if (currentState == null)
				{
					this.trayIcon.ShowBalloonTip(notificationTimeout, this.title, "Could not determine Wi-Fi state.", ToolTipIcon.Warning);
				}
				else
				{
					bool newState = !currentState.Value;
					await this.aero.SetWifiEnabledAsync(newState);
					this.trayIcon.ShowBalloonTip(notificationTimeout, this.title, $"Wi-Fi {(newState ? "enabled" : "disabled")}.", ToolTipIcon.Info);
				}

				break;

			case FnKey.ToggleScreen:
				await this.aero.Display.ToggleScreenAsync();
				break;

				//case FnKey.ToggleTouchpad:
				//	bool touchPad = !await this.aero.Touchpad.GetEnabledAsync();
				//	await this.aero.Touchpad.SetEnabledAsync(touchPad);
				//	this.trayIcon.ShowBalloonTip(notificationTimeout, this.title, $"Touchpad {(touchPad ? "enabled" : "disabled")}.", ToolTipIcon.Info);
				//	break;
		}
	}

	private async ValueTask onTouchpadEnabledChanged()
	{
		bool touchPad = await this.aero.Touchpad.GetEnabledAsync();
		this.trayIcon.ShowBalloonTip(notificationTimeout, this.title, $"Touchpad {(touchPad ? "enabled" : "disabled")}.", ToolTipIcon.Info);
	}

	private void showWindow()
	{
		lock (this.windowLock)
		{
			if (this.window == null)
			{
				// Create window if it doesn't exist.
				this.window = new MainWindow(this.controller);

				// Register close handler to set window back to null.
				this.window.Closed += (_, _) =>
				{
					lock (this.windowLock)
					{
						this.window = null;
					}
				};
			}

			// Show window and restore if minimized.
			this.window.Show();
			this.window.WindowState = WindowState.Normal;
			this.window.Focus();
		}
	}

	private async Task shutdownAsync()
	{
		// Cancel.
		this.cancellationTokenSource.Cancel();

		// Wait for aero update task to stop.
		try
		{
			await this.updateTask;
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			lock (this.windowLock)
				this.window?.Close();

			// Remove tray icon.
			this.trayIcon.Dispose();

			bool resetFans = this.controller.FanProfile == FanProfile.Software;

			// Unregister events (probably not necessary but whatever).
			SystemEvents.SessionSwitch -= this.onSessionSwitch;
			SystemEvents.PowerModeChanged -= this.onPowerModeChanged;

			// Close controller.
			await this.controller.DisposeAsync();

			// Unregister auto-restart (clean shutdown, no restart needed).
			NativeMethods.UnregisterApplicationRestart();

			// Reset fan profile to normal so the laptop doesn't melt.
			if (resetFans)
				await this.aero.Fans.SetNormalAsync();

			// Close aero.
			this.aero.Dispose();
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		SynchronizationContext.SetSynchronizationContext(null);
		this.shutdownAsync().Wait();
		base.OnExit(e);
	}

	private async Task updateLoop(CancellationToken token)
	{
		bool first = true;

		await Task.Yield();
		try
		{
			Stopwatch watch = new();
			for (; ; )
			{
				watch.Restart();
				int interval = 1000;
				UpdateMode mode;

				if (first)
				{
					mode = UpdateMode.Full;
					first = false;
				}
				else if (this.controller.FanProfileInvalid || (this.window != null && this.window.WindowState != WindowState.Minimized))
				{
					mode = UpdateMode.Normal;
				}
				else
				{
					interval = 2000;
					mode = UpdateMode.Light;
				}

				await this.controller.UpdateAsync(mode);

				double elapsed = watch.Elapsed.TotalMilliseconds;
				Debug.WriteLine($"Aero data updated in {elapsed:F1} ms");

				int pauseFor = (int)(interval - elapsed);
				if (pauseFor > 0)
					await Task.Delay(pauseFor, token);
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			StringBuilder str = new();
			str.AppendLine($"An error occurred while trying to update AERO information. Your model might not be supported (yet). Would you like to open the project's issue page?");
			str.AppendLine();
			str.AppendLine("Exception details:");
			str.Append(ex.ToString());
			if (System.Windows.MessageBox.Show(str.ToString(), "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
			{
				Process.Start(new ProcessStartInfo("https://gitlab.com/wtwrp/aeroctl/issues")
				{
					UseShellExecute = true
				});
			}

			throw;
		}
		finally
		{
			this.Shutdown();
		}
	}

	private void onSessionSwitch(object sender, SessionSwitchEventArgs e)
	{
		// Re-apply fan profile after someone logs in or out (when the laptop comes 
		// back from sleep) because it will default to 'Normal' otherwise.
		if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLock)
			this.controller.FanProfileInvalid = true;
	}

	private void onPowerModeChanged(object sender, PowerModeChangedEventArgs e)
	{
		// Re-apply fan-profile after hibernation resume.
		if (e.Mode == PowerModes.Resume)
			this.controller.FanProfileInvalid = true;
	}

	private async ValueTask setRgbAsync(Color color)
	{
		if (this.controller.Aero.Keyboard.Rgb is not { } rgb)
			return;

		int brightness = (await rgb.GetEffectAsync()).Brightness;
		byte[] image = new byte[512];

		for (int i = 0; i < 128; ++i)
		{
			image[4 * i + 0] = (byte)i;
			image[4 * i + 1] = color.R;
			image[4 * i + 2] = color.G;
			image[4 * i + 3] = color.B;
		}

		await rgb.SetImageAsync(1, image);
		await rgb.SetEffectAsync(new RgbEffect
		{
			Type = RgbEffectType.Custom1,
			Brightness = brightness,
		});

		this.trayIcon.ShowBalloonTip(3000, this.title, $"RGB set to {color.Name}", ToolTipIcon.Info);
	}
}