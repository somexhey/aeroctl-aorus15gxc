using System;
using System.Threading.Tasks;

namespace AeroCtl.Rgb.Rainbow;

public static class Program
{
	private readonly record struct Rgb(byte R, byte G, byte B);

	private static Rgb hsvToRgb(double hue, double saturation, double value)
	{
		int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
		double f = hue / 60 - Math.Floor(hue / 60);

		value *= 255;
		byte v = (byte)(value);
		byte p = (byte)(value * (1 - saturation));
		byte q = (byte)(value * (1 - f * saturation));
		byte t = (byte)(value * (1 - (1 - f) * saturation));

		switch (hi)
		{
			case 0:
				return new Rgb(v, t, p);
			case 1:
				return new Rgb(q, v, p);
			case 2:
				return new Rgb(p, v, t);
			case 3:
				return new Rgb(p, q, v);
			case 4:
				return new Rgb(t, p, v);
			default:
				return new Rgb(v, p, q);
		}
	}

	private static readonly Rgb white = new Rgb(60, 255, 90); // white point.

	public static async Task Main()
	{
		using Aero aero = new Aero();
		Ite829XRgbController rgb = (Ite829XRgbController)aero.Keyboard.Rgb;
		byte[] image = new byte[512];

		await rgb.SetEffectAsync(new RgbEffect { Type = RgbEffectType.Custom0, Brightness = 255 });

		double hStart = 0.0;
		for (;;)
		{
			double h = hStart;
			for (int i = 0; i < 128; ++i)
			{
				Rgb color = hsvToRgb(h, 1.0, 1.0);
				image[4 * i + 0] = (byte)i;
				image[4 * i + 1] = (byte)(color.R * white.R / 255);
				image[4 * i + 2] = (byte)(color.G * white.G / 255);
				image[4 * i + 3] = (byte)(color.B * white.B / 255);
				h += 2.0;
			}

			await rgb.SetImageAsync(0, image);
			hStart += 20.0;
		}
	}
}