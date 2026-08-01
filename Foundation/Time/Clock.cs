// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Foundation.Time
{
    public class Clock
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private double lastElapsedSeconds = 0.0;
        

        public float TargetFrameTime { get; set; } = 1.0f / 60.0f; // Default to 60 FPS

        public float DeltaTime { get; private set; }
        public float TotalTime { get; private set; }
        
        public void Tick()
        {
            double currentElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            DeltaTime = (float)(currentElapsedSeconds - lastElapsedSeconds);
            lastElapsedSeconds = currentElapsedSeconds;
            TotalTime = (float)currentElapsedSeconds;
        }

        public void LimitFrameRate()
        {
            double elapsedThisFrame = stopwatch.Elapsed.TotalSeconds - lastElapsedSeconds;
            double remaining = TargetFrameTime - elapsedThisFrame;

            if (remaining <= 0)
                return;

            const double spinThreshold = 0.002; // az utolsó 2ms-et pontosan várjuk ki

            if (remaining > spinThreshold)
                Thread.Sleep((int)((remaining - spinThreshold) * 1000));

            while (stopwatch.Elapsed.TotalSeconds - lastElapsedSeconds < TargetFrameTime)
            {
                // szoros várakozás a pontos időzítésért
            }
        }
        public float GetFPS()
        {
            return 1.0f / DeltaTime;
        }

        public void SetTargetFPS(float fps)
        {
            if (fps <= 0)
            {
                Log.Error($"Invalid FPS value: {fps}. FPS must be greater than zero.");
                throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be greater than zero.");
            }
            TargetFrameTime = 1.0f / fps;
        }

        public void Reset()
        {
            stopwatch.Restart();
            lastElapsedSeconds = 0.0;
            DeltaTime = 0.0f;
            TotalTime = 0.0f;
        }
    }
}
