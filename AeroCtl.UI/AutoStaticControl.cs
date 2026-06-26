using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace AeroCtl.UI;

/// <summary>
/// Implements OLED "Auto Static Control" features reverse-engineered from ControlCenter.
/// </summary>
public class AutoStaticControl
{
    private readonly AeroController controller;
    private readonly DispatcherTimer idleTimer;
    private readonly DispatcherTimer fadeTimer;
    
    // State
    private int storedBrightness;
    private int targetBrightness;
    private int currentFadeBrightness;
    private bool isDimmed;

    public AutoStaticControl(AeroController controller)
    {
        this.controller = controller;

        this.idleTimer = new DispatcherTimer();
        this.idleTimer.Interval = TimeSpan.FromSeconds(1);
        this.idleTimer.Tick += this.AscAdjustTick;

        this.fadeTimer = new DispatcherTimer();
        this.fadeTimer.Tick += this.AscAdjustBrightnessTick;
    }

    public bool IsEnabled
    {
        get => this.idleTimer.IsEnabled;
        set
        {
            if (value)
            {
                this.idleTimer.Start();
            }
            else
            {
                this.idleTimer.Stop();
                this.fadeTimer.Stop();
                
                if (this.storedBrightness > 0)
                {
                    this.controller.DisplayBrightness = this.storedBrightness;
                }
                this.storedBrightness = 0;
                this.isDimmed = false;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    private void AscAdjustTick(object sender, EventArgs e)
    {
        var lastInputInfo = new LASTINPUTINFO();
        lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
        lastInputInfo.dwTime = 0;

        int tickCount = Environment.TickCount;
        if (GetLastInputInfo(ref lastInputInfo))
        {
            int dwTime = (int)lastInputInfo.dwTime;
            int idleSeconds = (tickCount - dwTime) / 1000;

            if (idleSeconds <= 1)
            {
                if (this.isDimmed)
                {
                    // Stop fading if currently fading
                    if (this.fadeTimer.IsEnabled)
                    {
                        this.fadeTimer.Stop();
                    }

                    // Restore brightness
                    if (this.storedBrightness > 0)
                    {
                        this.controller.DisplayBrightness = this.storedBrightness;
                    }
                    this.storedBrightness = 0;
                    this.isDimmed = false;
                }
            }
            else
            {
                if (idleSeconds == 300) // 5 minutes
                {
                    this.AscAdjustFunction(5);
                }
                else if (idleSeconds == 600) // 10 minutes
                {
                    this.AscAdjustFunction(10);
                }
            }
        }
    }

    private void AscAdjustFunction(int val)
    {
        int current = this.controller.DisplayBrightness;
        
        // If screen is off or 0 brightness, nothing to dim
        if (current == 0) return;

        if (val == 5)
        {
            this.storedBrightness = current;
            this.targetBrightness = (int)(current * 0.7);
        }
        else if (val == 10)
        {
             this.targetBrightness = (int)(current * 0.5);
        }

        // Calculate interval: 3000ms total duration / steps
        int diff = Math.Abs(current - this.targetBrightness);
        if (diff == 0) return;

        this.currentFadeBrightness = current;
        
        // Avoid division by zero, ensure min interval
        int intervalMs = diff > 0 ? 3000 / diff : 100;
        this.fadeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, intervalMs));
        this.fadeTimer.Start();
    }

    private void AscAdjustBrightnessTick(object sender, EventArgs e)
    {
        if (this.currentFadeBrightness > this.targetBrightness)
        {
            this.currentFadeBrightness--;
        }
        else if (this.currentFadeBrightness < this.targetBrightness)
        {
            this.currentFadeBrightness++;
        }
        
        // Update brightness
        this.controller.DisplayBrightness = this.currentFadeBrightness;

        if (this.currentFadeBrightness == this.targetBrightness)
        {
            this.isDimmed = true; // Mark as dimmed
            this.fadeTimer.Stop();
        }
    }
}
