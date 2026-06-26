using System;
using System.Threading.Tasks;

namespace AeroCtl.Rgb.Calibrate;

public static class Program
{
	public static async Task Main(string[] args)
	{
		using Aero aero = new Aero();
		IRgbController rgb = aero.Keyboard.Rgb;

		byte r = 40;
		byte g = 255;
		byte b = 90;
		bool showOriginalWhite = false;
				
		byte[] image = new byte[512];

		for (;;)
		{
			for (int i = 0; i < 128; ++i)
			{
				image[4 * i + 0] = (byte)i;
				image[4 * i + 1] = r;
				image[4 * i + 2] = g;
				image[4 * i + 3] = b;
			}


			if (showOriginalWhite)
			{
				Console.WriteLine("white");
				await rgb.SetEffectAsync(new RgbEffect {Type = RgbEffectType.Static, Color = RgbEffectColor.White, Brightness = 51});
			}
			else
			{
				Console.WriteLine($"{r}, {g}, {b}");
				await rgb.SetEffectAsync(new RgbEffect {Type = RgbEffectType.Custom0, Brightness = 51});
				await rgb.SetImageAsync(0, image);
			}

			await Task.Delay(50);

			switch (Console.ReadKey(true).KeyChar)
			{
				case ' ':
					showOriginalWhite = !showOriginalWhite;
					break;

				case 'r': 
					--r;
					break;
				case 'R': 
					++r;
					break;

				case 'g': 
					--g;
					break;
				case 'G': 
					++g;
					break;


				case 'b': 
					--b;
					break;
				case 'B': 
					++b;
					break;
			}
		}
	}
}