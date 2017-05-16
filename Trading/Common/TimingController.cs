using System;
using System.Diagnostics;
using System.Timers;

namespace Trading.Common
{
    public class TimingController
    {
        private readonly Timer _timer;
        private readonly int _maxDownTime;
        private volatile bool _isTimingExecutionNeeded;
        private DateTime _lastUpdate;

        public TimingController(Action timingMethod, int timingMethodPeriodicityMs, int maxDownTimeMs)
        {
            _maxDownTime = maxDownTimeMs / 1000;

            _timer = new Timer();
            _timer.Elapsed += (sender, args) =>
            {
                CheckTiming();

                if (_isTimingExecutionNeeded)
                {
                    timingMethod();
                    TimingMethodHappened();
                }
            };

            _timer.Interval += timingMethodPeriodicityMs;
            _timer.Enabled = true;

            _lastUpdate = DateTime.Now;
            _isTimingExecutionNeeded = true;
        }

        public void TimingMethodHappened()
        {
            _lastUpdate = DateTime.Now;
            _isTimingExecutionNeeded = false;
        }

        public void EndTimingControl()
        {
            _timer.Enabled = false;
            _timer.Dispose();
        }

        private void CheckTiming()
        {
            var diff = DateTime.Now.Subtract(_lastUpdate);

            if (diff.Seconds >= _maxDownTime)
            {
                _isTimingExecutionNeeded = true;
            }
        }
    }
}