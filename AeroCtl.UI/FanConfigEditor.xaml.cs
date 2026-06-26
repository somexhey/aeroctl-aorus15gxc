using System.Windows;
using AeroCtl.UI.SoftwareFan;

namespace AeroCtl.UI;

/// <summary>
/// Interaction logic for FanConfigEditor.xaml
/// </summary>
public partial class FanConfigEditor : Window
{
	public FanConfig Config { get; }

	public FanConfigEditor(FanConfig config)
	{
		this.Config = config;
		this.InitializeComponent();
	}

	private void onOkClicked(object sender, RoutedEventArgs e)
	{
		this.DialogResult = true;
		this.Close();
	}
}