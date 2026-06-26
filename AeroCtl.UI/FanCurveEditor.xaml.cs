using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;

namespace AeroCtl.UI;

/// <summary>
/// Interaction logic for FanCurveEditor.xaml
/// </summary>
public partial class FanCurveEditor : Window
{
	#region Fields

	private List<UIElement> graphElements;

	#endregion

	#region Properties

	public IList<FanPoint> Curve { get; }
	public FanCurveKind CurveKind { get; }
	public bool IsCurveEditable => !this.Curve.IsReadOnly;
	public Canvas Canvas => this.canvas;
	public FanGraphPoint[] Points { get; private set; }
	public FanGraphPoint[] GridPoints { get; }

	#endregion

	#region Constructors

	public FanCurveEditor(IList<FanPoint> curve, FanCurveKind curveKind)
	{
		this.Curve = curve;
		this.CurveKind = curveKind;

		this.InitializeComponent();

		#region Grid

		this.GridPoints = new FanGraphPoint[11];
		for (int i = 0; i < this.GridPoints.Length; ++i)
		{
			FanPoint p = new FanPoint(i * 10.0, i * 0.1);
			this.GridPoints[i] = new FanGraphPoint(this, () => p, null);
		}

		for (int i = 0; i < this.GridPoints.Length; ++i)
		{
			Line vLine = new Line
			{
				Stroke = Brushes.Gray
			};

			this.canvas.Children.Add(vLine);

			vLine.SetBinding(Line.X1Property, $"GridPoints[{i}].X");
			vLine.Y1 = 0.0;
			vLine.SetBinding(Line.X2Property, $"GridPoints[{i}].X");
			vLine.SetBinding(Line.Y2Property, $"Canvas.ActualHeight");

			Line hLine = new Line
			{
				Stroke = Brushes.Gray
			};

			this.canvas.Children.Add(hLine);

			hLine.X1 = 0.0;
			hLine.SetBinding(Line.Y1Property, $"GridPoints[{i}].Y");
			hLine.SetBinding(Line.X2Property, $"Canvas.ActualWidth");
			hLine.SetBinding(Line.Y2Property, $"GridPoints[{i}].Y");

			TextBlock text = new TextBlock();
			this.canvas.Children.Add(text);
			text.SetBinding(TextBlock.TextProperty, $"GridPoints[{i}].Temperature");
			text.SetBinding(Canvas.LeftProperty, $"GridPoints[{i}].X");
			Canvas.SetBottom(text, 0.0);
		}

		#endregion

		this.updatePoints();
	}

	#endregion

	#region Methods

	private void updatePoints(int? focused = null)
	{
		if (this.graphElements != null)
		{
			foreach (UIElement el in this.graphElements)
				this.canvas.Children.Remove(el);
		}

		this.graphElements = [];

		this.Points = new FanGraphPoint[this.Curve.Count];
		for (int i = 0; i < this.Points.Length; ++i)
		{
			int j = i;
			this.Points[i] = new FanGraphPoint(this, () => this.Curve[j], v => this.Curve[j] = v);
		}

		Brush lineBrush = new SolidColorBrush(Colors.DodgerBlue);
		Ellipse focusedEllipse = null;

		for (int i = 0; i < this.Points.Length; ++i)
		{
			int j = i;
			Ellipse ellipse = new Ellipse
			{
				Focusable = true,
				Style = (Style)this.Resources["EllipseStyle"],
				Tag = i
			};

			if (focused != null && focused.Value == i)
				focusedEllipse = ellipse;

			ellipse.SetBinding(Canvas.LeftProperty, $"Points[{i}].EllipseX");
			ellipse.SetBinding(Canvas.TopProperty, $"Points[{i}].EllipseY");
			ellipse.SetBinding(WidthProperty, $"Points[{i}].EllipseW");
			ellipse.SetBinding(HeightProperty, $"Points[{i}].EllipseH");

			if (i < this.Points.Length - 1)
			{
				if (this.CurveKind == FanCurveKind.Step)
				{
					Line line1 = new Line
					{
						StrokeThickness = 2,
						Stroke = lineBrush,
					};
					this.graphElements.Add(line1);

					Line line2 = new Line
					{
						StrokeThickness = 2,
						Stroke = lineBrush,
					};
					this.graphElements.Add(line2);

					line1.SetBinding(Line.X1Property, $"Points[{i}].X");
					line1.SetBinding(Line.Y1Property, $"Points[{i}].Y");
					line1.SetBinding(Line.X2Property, $"Points[{i + 1}].X");
					line1.SetBinding(Line.Y2Property, $"Points[{i}].Y");

					line2.SetBinding(Line.X1Property, $"Points[{i + 1}].X");
					line2.SetBinding(Line.Y1Property, $"Points[{i}].Y");
					line2.SetBinding(Line.X2Property, $"Points[{i + 1}].X");
					line2.SetBinding(Line.Y2Property, $"Points[{i + 1}].Y");
				}
				else if (this.CurveKind == FanCurveKind.Linear)
				{
					Line line = new Line
					{
						StrokeThickness = 2,
						Stroke = lineBrush,
					};
					this.graphElements.Add(line);

					line.SetBinding(Line.X1Property, $"Points[{i}].X");
					line.SetBinding(Line.Y1Property, $"Points[{i}].Y");
					line.SetBinding(Line.X2Property, $"Points[{i + 1}].X");
					line.SetBinding(Line.Y2Property, $"Points[{i + 1}].Y");
				}
			}

			this.graphElements.Add(ellipse);

			void updateLabel()
			{
				this.infoLabel.Text = $"Point {j}: {this.Points[j].Point.FanSpeed * 100.0:F1}% at {this.Points[j].Point.Temperature:F1}°C";
			}

			ellipse.MouseDown += (_, _) =>
			{
				ellipse.Focus();
				ellipse.CaptureMouse();
				updateLabel();
			};

			ellipse.MouseUp += (_, _) =>
			{
				ellipse.ReleaseMouseCapture();
				this.infoLabel.Text = "";
			};

			ellipse.MouseMove += (_, e) =>
			{
				if (!ellipse.IsMouseCaptured)
					return;

				FanPoint p = this.canvasToPoint(e.GetPosition(this.canvas));
				p.Temperature = Math.Max(0.0, Math.Min(100.0, p.Temperature));
				p.FanSpeed = Math.Max(0.0, Math.Min(1.0, p.FanSpeed));

				if (j == 0)
				{
					p.Temperature = 0.0;
				}

				if (j > 0)
				{
					FanPoint p2 = this.Points[j - 1].Point;
					if (p.Temperature < p2.Temperature)
						p.Temperature = p2.Temperature;
					//if (p.Y < p2.Y)
					//	p.Y = p2.Y;
				}

				if (j < this.Points.Length - 1)
				{
					FanPoint p2 = this.Points[j + 1].Point;
					if (p.Temperature > p2.Temperature)
						p.Temperature = p2.Temperature;
					//if (p.Y > p2.Y)
					//	p.Y = p2.Y;
				}

				if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
				{
					p.FanSpeed = Math.Round(p.FanSpeed, 2);
					p.Temperature = Math.Round(p.Temperature);
				}

				this.Points[j].Point = p;
				updateLabel();
			};
		}

		foreach (UIElement el in this.graphElements)
			this.canvas.Children.Add(el);

		focusedEllipse?.Focus();
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);

		for (int i = 0; i < this.GridPoints.Length; ++i)
		{
			this.GridPoints[i].Invalidate();
		}

		for (int i = 0; i < this.Points.Length; ++i)
		{
			this.Points[i].Invalidate();
		}
	}

	private Point getGraphSize()
	{
		return new Point(this.canvas.ActualWidth, this.canvas.ActualHeight);
	}

	private Point pointToCanvas(FanPoint p)
	{
		Point s = this.getGraphSize();
		return new Point(p.Temperature / 100 * s.X, (1.0 - p.FanSpeed) * s.Y);
	}

	private FanPoint canvasToPoint(Point p)
	{
		Point s = this.getGraphSize();
		return new FanPoint(p.X / s.X * 100, 1.0 - p.Y / s.Y);
	}

	public event EventHandler CurveApplied;

	private void applyButton_OnClick(object sender, RoutedEventArgs e)
	{
		for (int i = 0; i < this.Points.Length; ++i)
		{
			this.Curve[i] = new FanPoint
			{
				Temperature = this.Points[i].Temperature,
				FanSpeed = this.Points[i].FanSpeed
			};
		}

		this.CurveApplied?.Invoke(this, EventArgs.Empty);
	}

	private void addButton_OnClick(object sender, RoutedEventArgs e)
	{
		int insertIndex;
		if (this.Curve.Count == 0)
		{
			this.Curve.Add(new FanPoint
			{
				Temperature = 0.0,
				FanSpeed = 0.25,
			});
			insertIndex = 0;
		}
		else if (this.Curve.Count == 1)
		{
			this.Curve.Add(new FanPoint(100.0, 1.0));
			insertIndex = 1;
		}
		else
		{
			if (Keyboard.FocusedElement is Ellipse { Tag: int index })
			{
				FanPoint p1;
				FanPoint p2;
				if (index == this.Curve.Count - 1)
				{
					p1 = this.Curve[^1];
					p2 = this.Curve[^2];
					insertIndex = this.Curve.Count - 1;
				}
				else
				{
					p1 = this.Curve[index];
					p2 = this.Curve[index + 1];
					insertIndex = index + 1;
				}

				this.Curve.Insert(insertIndex, new FanPoint(0.5 * (p1.Temperature + p2.Temperature), 0.5 * (p1.FanSpeed + p2.FanSpeed)));
			}
			else
			{
				FanPoint p1;
				FanPoint p2;
				p1 = this.Curve[^1];
				p2 = this.Curve[^2];
				insertIndex = this.Curve.Count - 1;

				this.Curve.Insert(insertIndex, new FanPoint(0.5 * (p1.Temperature + p2.Temperature), 0.5 * (p1.FanSpeed + p2.FanSpeed)));
			}
		}

		this.updatePoints(insertIndex);
	}

	private void deleteButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (!(Keyboard.FocusedElement is Ellipse ellipse) || !(ellipse.Tag is int index))
			return;

		this.Curve.RemoveAt(index);
		this.updatePoints(Math.Max(0, index - 1));
	}

	#endregion

	#region Nested Types

	public class FanGraphPoint : INotifyPropertyChanged
	{
		private readonly FanCurveEditor editor;
		private readonly Func<FanPoint> get;
		private readonly Action<FanPoint> set;

		public FanPoint Point
		{
			get => this.get();
			set
			{
				this.set(value);
				this.OnPropertyChanged();
				this.OnPropertyChanged(nameof(this.X));
				this.OnPropertyChanged(nameof(this.EllipseX));
				this.OnPropertyChanged(nameof(this.Y));
				this.OnPropertyChanged(nameof(this.EllipseY));
			}
		}

		public double Temperature
		{
			get => this.Point.Temperature;
			set => this.Point = new FanPoint(value, this.Temperature);
		}

		public double FanSpeed
		{
			get => this.Point.FanSpeed;
			set => this.Point = new FanPoint(value, this.FanSpeed);
		}


		public double X => this.editor.pointToCanvas(this.Point).X;

		public double Y => this.editor.pointToCanvas(this.Point).Y;

		public double EllipseX => this.X - this.EllipseW * 0.5;

		public double EllipseY => this.Y - this.EllipseH * 0.5;

		public double EllipseW => 10.0;

		public double EllipseH => 10.0;

		public FanGraphPoint(FanCurveEditor editor, Func<FanPoint> get, Action<FanPoint> set)
		{
			this.editor = editor;
			this.get = get;
			this.set = set;
		}

		public void Invalidate()
		{
			this.OnPropertyChanged(nameof(this.X));
			this.OnPropertyChanged(nameof(this.Y));
			this.OnPropertyChanged(nameof(this.EllipseX));
			this.OnPropertyChanged(nameof(this.EllipseY));
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	#endregion
}