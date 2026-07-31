// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

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

        public float DeltaTime { get; private set; }
        public float TotalTime { get; private set; }
        
        public void Tick()
        {
            double currentElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            DeltaTime = (float)(currentElapsedSeconds - lastElapsedSeconds);
            lastElapsedSeconds = currentElapsedSeconds;
            TotalTime = (float)currentElapsedSeconds;
        }

    }
}
