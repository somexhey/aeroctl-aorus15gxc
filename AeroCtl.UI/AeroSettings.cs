using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using AeroCtl.UI.SoftwareFan;

namespace AeroCtl.UI;

/// <summary>
/// Contains the user settings of the app.
/// </summary>
public class AeroSettings
{
	public int FanProfile { get; set; } = 1;
	public double FixedFanSpeed { get; set; }
	public double AutoFanAdjust { get; set; }
	public bool StartMinimized { get; set; }
	public bool AutoStart { get; set; }
	public bool AutoRestart { get; set; } = true;
	public int FanProfileAlt { get; set; }
	public FanConfig SoftwareFanConfig { get; set; }
	public uint DisplayFrequencyAc { get; set; }
	public uint DisplayFrequencyDc { get; set; }
	public int ChargeStop { get; set; } = -1;
	public bool AutoStaticControl { get; set; }

	private static readonly string configPath;
	public static AeroSettings Default { get; } = new();

	/// <summary>
	/// Loads settings from a json file in local app data.
	/// </summary>
	static AeroSettings()
	{
		string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		dir = Path.Combine(dir, "AeroCtl");
		Directory.CreateDirectory(dir);

		configPath = Path.Combine(dir, "AeroCtl.json");

		try
		{
			using Stream stream = new FileStream(configPath, FileMode.Open, FileAccess.Read);
			Default = JsonSerializer.Deserialize<AeroSettings>(stream);
		}
		catch (FileNotFoundException)
		{
			Default = new();
		}
		catch (Exception ex)
		{
			StringBuilder str = new StringBuilder();
			str.AppendLine("Failed to load configuration file:");
			str.AppendLine(configPath);
			str.AppendLine();
			str.Append(ex.Message);
			MessageBox.Show(str.ToString(), "Configuration error", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	/// <summary>
	/// Saves the settings in a json file in local app data.
	/// </summary>
	public static void Save()
	{
		using Stream stream = new FileStream(configPath, FileMode.Create, FileAccess.Write);
		JsonSerializer.Serialize(stream, Default, new JsonSerializerOptions
		{
			WriteIndented = true
		});
	}
}