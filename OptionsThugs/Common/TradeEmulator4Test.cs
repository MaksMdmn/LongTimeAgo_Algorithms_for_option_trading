using System;
using System.Threading;
using System.Threading.Tasks;

namespace OptionsThugs.Common
{
    public class TradeEmulator4Test
    {
        private readonly int _period;
        private volatile bool _isRunning = false;

        public event Action CancelEvent;
        public event Action OrderChangeEvent;
        public event Action TradeEvent;
        public event Action RegisterEvent;
        public event Action RegisterFailedEvent;
        public event Action CancelFailedEvent;

        public TradeEmulator4Test(int period)
        {
            _period = period;
        }

        public void Start()
        {
            _isRunning = true;
            Task.Run(() => RandomProcess());
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void RandomProcess()
        {
            while (_isRunning)
            {
                int randomEvent = new Random().Next(0, 10);

                switch (randomEvent)
                {
                    case 2:
                        CancelEvent?.Invoke();
                        break;
                    case 3:
                        OrderChangeEvent?.Invoke();
                        break;
                    case 4:
                        TradeEvent?.Invoke();
                        break;
                    case 5:
                        RegisterEvent?.Invoke();
                        break;
                    case 6:
                        RegisterFailedEvent?.Invoke();
                        break;
                    case 7:
                        CancelFailedEvent?.Invoke();
                        break;
                }

                Thread.Sleep(_period);
            }

        }



    }
}