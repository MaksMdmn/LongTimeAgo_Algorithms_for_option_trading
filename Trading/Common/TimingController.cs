using System;
using System.Diagnostics;
using System.Timers;
using Ecng.Common;

namespace Trading.Common
{
    public class TimingController
    {
        private readonly int _maxDownTime;
        private readonly int _timingMethodPeriodicityMs;
        private volatile bool _isTimingExecutionNeeded;
        private DateTime _lastUpdate;
        private Timer _timer;
        private bool _noNeedTiming;

        public TimingController(int timingMethodPeriodicityMs, int maxDownTimeMs)
        {
            _maxDownTime = maxDownTimeMs / 1000;
            _timingMethodPeriodicityMs = timingMethodPeriodicityMs;
            _lastUpdate = DateTime.Now;
            _isTimingExecutionNeeded = true;
            _noNeedTiming = false;
            _timer = null;
        }

        public void SetTimingMethod(Action timingMethod)
        {
            _timer = new Timer();

            _timer.Elapsed += (sender, args) =>
            {
                CheckTiming();

                if (_isTimingExecutionNeeded)
                {
                    timingMethod();
                    TimingMethodHappened();
                }

                Debug.WriteLine("AUTO ... !!!");
            };

            _timer.Interval = _timingMethodPeriodicityMs;

        }

        public void TimingMethodHappened()
        {
            if (_noNeedTiming)
                throw new InvalidOperationException("method was marked like unnecessary");

            Debug.WriteLine("METHOD HAPPENED");
            _lastUpdate = DateTime.Now;
            _isTimingExecutionNeeded = false;
        }

        public void StartTimingControl()
        {
            if(_noNeedTiming)
                return;

            if (_timer == null)
                throw new NullReferenceException("_timer");

            _timer.Enabled = true;
            Debug.WriteLine("TIMER CREATED");
        }

        public void StopTimingControl()
        {
            if (_noNeedTiming)
                return;

            if (_timer == null)
                return;

            _timer.Enabled = false;
            _timer.Dispose();
            _timer = null;

            Debug.WriteLine("TIMER DESTROYED");
        }

        public void SetTimingUnnecessary()
        {
            _noNeedTiming = true;
        }

        private void CheckTiming()
        {
            var diff = DateTime.Now.Subtract(_lastUpdate);

            if (diff.Seconds >= _maxDownTime)
            {
                Debug.WriteLine("NEED EXECUTION");
                _isTimingExecutionNeeded = true;
            }
        }
    }
}