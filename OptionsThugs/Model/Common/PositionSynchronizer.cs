using System;
using System.Threading;
using System.Threading.Tasks;

namespace OptionsThugs.Model.Common
{
    public class PositionSynchronizer
    {
        private readonly EventWaitHandle _eventWaiter = new EventWaitHandle(false, EventResetMode.AutoReset);

        private decimal _absPosVol;
        private decimal _absTradeVol;
        private volatile bool _isPosAndTradesEven;
        private event Action TimeToCheckIfPositionEqual;

        public event Action PositionChanged;
        public bool IsPosAndTradesEven => _isPosAndTradesEven;

        public int Timeout { get; set; }

        public PositionSynchronizer()
        {
            Timeout = 1000;

            _isPosAndTradesEven = true;

            TimeToCheckIfPositionEqual += () =>
            {
                _isPosAndTradesEven = _absPosVol == _absTradeVol;
                PositionChanged?.Invoke();

                if (IsPosAndTradesEven)
                    _eventWaiter.Set();
            };
        }

        public void NewTradeChange(decimal volume)
        {
            _isPosAndTradesEven = false;

            _absTradeVol += Math.Abs(volume);

            ContinueOrTimeout();

            TimeToCheckIfPositionEqual?.Invoke();
        }

        public void NewPositionChange(decimal volume)
        {
            _isPosAndTradesEven = false;

            _absPosVol = Math.Abs(volume);

            TimeToCheckIfPositionEqual?.Invoke();
        }

        private void ContinueOrTimeout()
        {
            Task.Run(() =>
            {
                if (!_eventWaiter.WaitOne(Timeout))
                    throw new TimeoutException(
                        "Still have no respond from terminal about order transaction, timeout: " + Timeout);
            });
        }
    }
}