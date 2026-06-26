using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AeroCtl.UI.SoftwareFan;

public class SoftwareFanController
{
	private readonly FanConfig config;
	private readonly ISoftwareFanProvider provider;
	private readonly ImmutableArray<FanPoint> curve;

	private readonly CancellationTokenSource cts;
	private readonly Task task;
	private readonly Thread thread;

	private readonly Stopwatch watch;
	private double currentSpeed;

	public SoftwareFanController(FanConfig config, ISoftwareFanProvider provider)
	{
		if (config == null)
			throw new ArgumentNullException(nameof(config));

		if (!config.IsValid)
			throw new ArgumentException(@"Invalid fan config.", nameof(config));

		this.config = new FanConfig(config);
		this.curve = config.Curve;
		this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

		this.watch = new Stopwatch();
		this.currentSpeed = double.NaN;

		this.cts = new CancellationTokenSource();

		switch (config.SchedulingMode)
		{
			case FanSchedulingMode.AsyncTask:
				this.task = this.runAsync(this.cts.Token);
				break;

			case FanSchedulingMode.NormalThread:
				this.thread = new Thread(() => this.runThread(this.cts.Token))
				{
					Priority = ThreadPriority.Normal
				};
				break;

			case FanSchedulingMode.AboveNormalThread:
				this.thread = new Thread(() => this.runThread(this.cts.Token))
				{
					Priority = ThreadPriority.AboveNormal
				};
				break;

			case FanSchedulingMode.HighestThread:
				this.thread = new Thread(() => this.runThread(this.cts.Token))
				{
					Priority = ThreadPriority.Highest
				};
				break;
		}

		this.thread?.Start();
	}

	public async Task StopAsync()
	{
		this.cts.Cancel();

		try
		{
			if (this.task != null)
				await this.task;
		}
		catch (OperationCanceledException)
		{

		}

		this.thread?.Join();
	}

	private async ValueTask update(CancellationToken cancellationToken)
	{
		const double epsilon = 0.002;

		cancellationToken.ThrowIfCancellationRequested();

		double secondsPassed = this.watch.Elapsed.TotalSeconds;
		this.watch.Restart();

		double temperature = await this.provider.GetTemperatureAsync(cancellationToken);
		double newTarget;

		int index = 0;

		for (int i = 0; i < this.curve.Length; ++i)
		{
			if (this.curve[i].Temperature < temperature)
				index = i;
		}

		if (index < this.curve.Length - 1)
		{
			double t = (temperature - this.curve[index].Temperature) / (this.curve[index + 1].Temperature - this.curve[index].Temperature);
			newTarget = this.curve[index].FanSpeed * (1.0 - t) + this.curve[index + 1].FanSpeed * t;
		}
		else
		{
			newTarget = this.curve[index].FanSpeed;
		}

		if (double.IsNaN(this.currentSpeed))
		{
			this.currentSpeed = newTarget;
		}
		else
		{
			double diff = newTarget - this.currentSpeed;

			if (diff > epsilon)
				diff = Math.Min(diff, this.config.RampUpSpeed * secondsPassed);
			else if (diff < -epsilon)
				diff = -Math.Min(-diff, this.config.RampDownSpeed * secondsPassed);
			else
				return; // Change too small, don't bother updating the fan.

			this.currentSpeed += diff;
		}

		await this.provider.SetSpeedAsync(this.currentSpeed, cancellationToken);
	}

	private async Task runAsync(CancellationToken cancellationToken)
	{
		for (; ; )
		{
			await Task.Delay(this.config.Interval, cancellationToken);
			await this.update(cancellationToken);
		}
	}

	private void runThread(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				Thread.Sleep(this.config.Interval);

				ValueTask t = this.update(cancellationToken);
				if (!t.IsCompletedSuccessfully)
					t.AsTask().Wait(cancellationToken);
			}
		}
		catch (OperationCanceledException)
		{

		}
	}
}