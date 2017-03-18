using System;

namespace OptionsThugs.Model
{
    public class PositionSynchronizer
    {
        private event Action TimeToCheckIfPositionEqual;
        private decimal _absPosVol;
        private decimal _absTradeVol;
        private volatile bool _isPositionEqual;

        public event Action PositionChanged;
        public bool IsPositionEqual => _isPositionEqual;

        public PositionSynchronizer()
        {
            _isPositionEqual = false;

            TimeToCheckIfPositionEqual += () =>
            {
                _isPositionEqual = _absPosVol == _absTradeVol;
                PositionChanged?.Invoke();
            };
        }

        public void NewTradeChange(decimal volume)
        {
            _isPositionEqual = false;

            _absTradeVol += Math.Abs(volume);

            TimeToCheckIfPositionEqual?.Invoke();
        }

        public void NewPositionChange(decimal volume)
        {
            _isPositionEqual = false;

            _absPosVol += Math.Abs(volume);

            TimeToCheckIfPositionEqual?.Invoke();
        }

    }
}