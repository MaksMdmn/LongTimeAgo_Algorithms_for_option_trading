using System;
using System.Threading;
using System.Threading.Tasks;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace Trading.Common
{
    public class OrderSynchronizer
    {
        private readonly EventWaitHandle _eventWaiter = new EventWaitHandle(false, EventResetMode.AutoReset);
        private readonly Strategy _strategy;
        private volatile bool _isOrderRegistering;
        private volatile bool _isOrderCanceling;
        private Order _currentOrder;

        public bool IsAnyOrdersInWork { get; private set; }

        public int Timeout { get; set; }

        public OrderSynchronizer(Strategy strategy)
        {
            Timeout = 1000;

            _currentOrder = null;
            IsAnyOrdersInWork = false;
            _isOrderRegistering = false;
            _isOrderCanceling = false;
            _strategy = strategy;

            _strategy.WhenStopping()
                .Or(_strategy.WhenStopped())
                .Do(s =>
                {
                    _eventWaiter.Set();
                })
                .Apply(_strategy);
        }

        public void PlaceOrder(Order order)
        {
            if (_isOrderRegistering)
                return;

            _isOrderRegistering = true;

            _currentOrder = order;

            if (_currentOrder == null)
            {
                IsAnyOrdersInWork = false;
                _isOrderRegistering = false;
                throw new ArgumentNullException(nameof(_currentOrder));
            }

            _currentOrder.WhenRegistered(_strategy.Connector)
                .Do(() =>
                {
                    _eventWaiter.Set();
                })
                .Once()
                .Apply(_strategy);

            _strategy.RegisterOrder(_currentOrder);

            ContinueOrTimeout(() =>
            {
                IsAnyOrdersInWork = true;
                _isOrderRegistering = false;
            });

        }

        public void CancelCurrentOrder()
        {
            if (_isOrderCanceling)
                return;

            _isOrderCanceling = true;

            if (_currentOrder == null)
            {
                _isOrderCanceling = false;
                throw new ArgumentNullException("Such an order does not exist: " + _currentOrder);
            }

            _currentOrder.WhenChanged(_strategy.Connector)
                .Do(o =>
                {
                    if (o.State == OrderStates.Done || o.State == OrderStates.Failed)
                    {
                        _eventWaiter.Set();
                    }

                })
                .Until(() => _currentOrder.State == OrderStates.Done || _currentOrder.State == OrderStates.Failed)
                .Apply(_strategy);

            _strategy.CancelOrder(_currentOrder);


            ContinueOrTimeout(() =>
            {
                IsAnyOrdersInWork = false;
                _isOrderCanceling = false;
            });
        }

        private void ContinueOrTimeout(Action methodAfterSuccess)
        {
            Task.Run(() =>
            {
                if (!_eventWaiter.WaitOne(Timeout))
                    throw new TimeoutException(
                        "Still have no respond from terminal about order transaction, timeout: " + Timeout);

                methodAfterSuccess();
            });
        }
    }
}
