using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class OrderSynchronizer
    {
        private readonly EventWaitHandle _eventWaiter = new EventWaitHandle(false, EventResetMode.AutoReset);
        private readonly int _timeout = 1000;
        private readonly Strategy _strategy;
        private readonly PositionSynchronizer _keeper;
        private volatile bool _isAnyOrdersInWork;
        private volatile bool _isOrderCanceling;
        private Order _currentOrder;

        public bool IsAnyOrdersInWork => _isAnyOrdersInWork;
        public bool IsOrderCanceling => _isOrderCanceling;

        public OrderSynchronizer(Strategy strategy)
        {
            _currentOrder = null;
            _isAnyOrdersInWork = false;
            _strategy = strategy;
            _keeper = new PositionSynchronizer();

            _strategy.WhenPositionChanged()
                .Do(p => _keeper.NewPositionChange(p))
                .Apply(_strategy);

            _strategy.WhenNewMyTrade()
                .Do(mt => _keeper.NewTradeChange(mt.Trade.Volume))
                .Apply(_strategy);

            _keeper.PositionChanged += () =>
            {
                if (_keeper.IsPositionEqual)
                    _eventWaiter.Set();
            };
        }

        public void PlaceOrder(Order order)
        {
            _isAnyOrdersInWork = true;

            _currentOrder = order;

            if (_currentOrder == null)
            {
                _isAnyOrdersInWork = false;
                throw new ArgumentNullException(nameof(_currentOrder));
            }

            _currentOrder.WhenRegistered(_strategy.Connector)
                .Do(() =>
                {
                    _eventWaiter.Set();
                })
                .Once()
                .Apply(_strategy);


            Task.Run(() =>
            {
                _strategy.RegisterOrder(_currentOrder);

                ContinueOrTimeout();
            });

        }

        public void CancelCurrentOrder()
        {
            _isOrderCanceling = true;

            if (_currentOrder == null)
            {
                throw new ArgumentNullException("Such an order does not exist: " + _currentOrder);
            }

            _currentOrder.WhenCanceled(_strategy.Connector)
                .Do(() =>
                {
                    _eventWaiter.Set();
                })
                .Once()
                .Apply(_strategy);

            Task.Run(() =>
            {
                _strategy.CancelOrder(_currentOrder);

                ContinueOrTimeout();

                _isAnyOrdersInWork = false;
                _isOrderCanceling = false;
            });


        }

        private void ContinueOrTimeout()
        {
            if (!_eventWaiter.WaitOne(_timeout))
                throw new TimeoutException("Still have no respond, timeout: " + _timeout);
        }

    }
}
