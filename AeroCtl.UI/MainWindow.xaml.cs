using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using AeroCtl.UI.SoftwareFan;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AeroCtl.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
	public AeroController Controller { get; }

	public string GitInfo => $"AeroCtl v{ThisAssembly.Git.BaseTag}";

	public MainWindow(AeroController controller)
	{
		this.Controller = controller;
		this.InitializeComponent();
	}

	protected override void OnStateChanged(EventArgs e)
	{
		base.OnStateChanged(e);

		if (this.WindowState == WindowState.Minimized)
			this.Hide();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (this.Controller.AutoRestart)
		{
			e.Cancel = true;
			this.Hide();
			return;
		}

		Application.Current.Shutdown();
	}

	private bool hwEditorOpen;

	private async void onEditHwCurveClicked(object sender, RoutedEventArgs e)
	{
		if (this.hwEditorOpen) return;
		this.hwEditorOpen = true;
		try
		{
			IFanCurve curve;
			FanPoint[] points;

			// Try to read the curve from hardware.
			try
			{
				curve = this.Controller.Aero.Fans.GetFanCurve();
				points = new FanPoint[curve.Count];
				for (int i = 0; i < points.Length; ++i)
					points[i] = await curve.GetPointAsync(i);
			}
			catch (Exception ex)
			{
				this.Controller.FanException = ex;
				return;
			}

			// Open editor.
			FanCurveEditor editor = new FanCurveEditor(points, FanCurveKind.Step);

			editor.CurveApplied += async (_, _) =>
			{
				// Apply curve back to hardware.
				try
				{
					for (int i = 0; i < curve.Count; ++i)
					{
						await curve.SetPointAsync(i, points[i]);
					}
				}
				catch (Exception ex)
				{
					this.Controller.FanException = ex;
				}
			};

			editor.ShowDialog();
		}
		finally
		{
			this.hwEditorOpen = false;
		}
	}

	private void onEditSwCurveClicked(object sender, RoutedEventArgs e)
	{
		FanConfig cfg = new FanConfig(this.Controller.SoftwareFanConfig);
		List<FanPoint> curve = [..cfg.Curve];
		FanCurveEditor editor = new FanCurveEditor(curve, FanCurveKind.Linear);
		editor.CurveApplied += (_, _) =>
		{
			cfg.Curve = [..curve];
			this.Controller.SoftwareFanConfig = cfg;
		};
		editor.ShowDialog();
	}

	private void onEditSwConfigClicked(object sender, RoutedEventArgs e)
	{
		FanConfig cfg = new FanConfig(this.Controller.SoftwareFanConfig);
		FanConfigEditor editor = new FanConfigEditor(cfg);

		if (editor.ShowDialog() == true)
			this.Controller.SoftwareFanConfig = cfg;
	}

	private async void onResetKeyboardClicked(object sender, RoutedEventArgs e)
	{
		MessageBoxResult messageBoxResult = MessageBox.Show(
			"This will reset all keyboard settings (e.g. RGB LED colors). Are you sure?",
			"Reset keyboard",
			MessageBoxButton.YesNo,
			MessageBoxImage.Question);

		if (messageBoxResult == MessageBoxResult.Yes)
			await this.Controller.ResetKeyboard();
	}

	private async void onKeyboardRgbClicked(object sender, RoutedEventArgs e)
	{
		if (this.Controller.Aero.Keyboard.Rgb is not { } rgb)
			return;

		using ColorDialog dialog = new();

		if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
			return;

		// TODO: add more effects and whatnot. This is just basic solid color code but the principle is the same for
		// other effects as well.
		byte[] image = new byte[512];

		void setColor(int key, Color color)
		{
			image[4 * key + 0] = (byte)key;
			image[4 * key + 1] = color.R;
			image[4 * key + 2] = color.G;
			image[4 * key + 3] = color.B;
		}

		for (int i = 0; i < 128; ++i)
			setColor(i, dialog.Color);

		// Read current brightness from controller. This can be changed independently of this app by the user through
		// the keyboard brightness shortcut (Fn + Space).
		int brightness = (await rgb.GetEffectAsync()).Brightness;

		// Set new image.
		await rgb.SetImageAsync(1, image);
		await rgb.SetEffectAsync(new RgbEffect
		{
			Type = RgbEffectType.Custom1,
			Brightness = brightness,
		});
	}

	private void onGitLabLinkClicked(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
	{
		Process.Start(new ProcessStartInfo(e.Uri.ToString())
		{
			UseShellExecute = true
		});
	}

	private void onFanExceptionInfoClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		Exception ex = this.Controller.FanException;
		if (ex == null)
			return;

		MessageBox.Show(ex.ToString(), "Fan exception");
	}
}