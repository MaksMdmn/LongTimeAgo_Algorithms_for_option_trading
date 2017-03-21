using System;

namespace OptionsThugs.Model.Primary
{
    public class PositionSynchronizer
    {
        private event Action TimeToCheckIfPositionEqual;
        private decimal _absPosVol;
        private decimal _absTradeVol;
        private volatile bool _isPosAndTradesEven;

        public event Action PositionChanged;
        public bool IsPosAndTradesEven => _isPosAndTradesEven;

        public PositionSynchronizer()
        {
            _isPosAndTradesEven = true;

            TimeToCheckIfPositionEqual += () =>
            {
                _isPosAndTradesEven = _absPosVol == _absTradeVol;
                PositionChanged?.Invoke();
            };
        }

        public void NewTradeChange(decimal volume)
        {
            _isPosAndTradesEven = false;

            _absTradeVol += Math.Abs(volume);

            TimeToCheckIfPositionEqual?.Invoke();
        }

        public void NewPositionChange(decimal volume)
        {
            _isPosAndTradesEven = false;

            _absPosVol = Math.Abs(volume);

            TimeToCheckIfPositionEqual?.Invoke();
        }

    }
}