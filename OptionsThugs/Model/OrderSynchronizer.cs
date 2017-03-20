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
        private volatile bool _isOrderRegistering;
        private volatile bool _isOrderCanceling;
        private Order _currentOrder;

        public bool IsAnyOrdersInWork { get; private set; }

        public OrderSynchronizer(Strategy strategy)
        {
            _currentOrder = null;
            IsAnyOrdersInWork = false;
            _isOrderRegistering = false;
            _isOrderCanceling = false;
            _strategy = strategy;

            _strategy.WhenStopping()
                .Or(_strategy.WhenStopped())
                .Do(s =>
                {
                    Debug.WriteLine(s.ProcessState + " event");
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
                    Debug.WriteLine("register event");
                    _eventWaiter.Set();
                })
                .Once()
                .Apply(_strategy);

            _strategy.RegisterOrder(_currentOrder);

            Task.Run(() =>
            {
                Debug.WriteLine("registration...");
                ContinueOrTimeout();

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
                        Debug.WriteLine("cancel event");
                        _eventWaiter.Set();
                    }
                })
                .Until(() => _currentOrder.State == OrderStates.Done || _currentOrder.State == OrderStates.Failed)
                .Apply(_strategy);

            _strategy.CancelOrder(_currentOrder);

            Task.Run(() =>
            {
                Debug.WriteLine("canceling...");
                ContinueOrTimeout();

                IsAnyOrdersInWork = false;
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
