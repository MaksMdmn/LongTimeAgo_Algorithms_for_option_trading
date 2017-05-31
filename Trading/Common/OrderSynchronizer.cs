using System;
using System.Threading;
using System.Threading.Tasks;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;
using Trading.Strategies;

namespace Trading.Common
{
    public class OrderSynchronizer
    {
        private readonly EventWaitHandle _eventWaiter = new EventWaitHandle(false, EventResetMode.AutoReset);
        private readonly PrimaryStrategy _strategy;
        private volatile bool _isOrderRegistering;
        private volatile bool _isOrderCanceling;
        private volatile bool _isAnyOrdersInWork;
        private Order _currentOrder;

        public bool IsAnyOrdersInWork
        {
            get { return _isAnyOrdersInWork; }
            private set { _isAnyOrdersInWork = value; }
        }

        public bool IsOrderRegistering => _isOrderRegistering;

        public bool IsOrderCanceling => _isOrderRegistering;

        public int Timeout { get; set; }

        public OrderSynchronizer(PrimaryStrategy strategy)
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
                    _strategy.AddWarningLog($"trying to stop {_strategy.Name}: {_strategy.ProcessState}");
                    _eventWaiter.Set();
                })
                .Apply(_strategy);
        }

        public void PlaceOrder(Order order)
        {
            if (_strategy.IsPrimaryStoppingStarted())
                return;

            if (_isOrderRegistering)
                return;

            _isOrderRegistering = true;

            _strategy.AddWarningLog($"TRY TO PLACE ORDER {order}, {_strategy.Name}: {_strategy.ProcessState}");

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
                    _strategy.AddWarningLog($"NOTE THAT ORDER REGISTERED {order}, {_strategy.Name}: {_strategy.ProcessState}");
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
            if (_isOrderCanceling || !_isAnyOrdersInWork) // TODO добавил. проверить, не проебать
                return;

            _isOrderCanceling = true;

            _strategy.AddWarningLog($"TRY TO CANCEL ORDER {_currentOrder}, {_strategy.Name}: {_strategy.ProcessState}");

            if (_currentOrder == null)
            {
                _isOrderCanceling = false;
                _strategy.AddErrorLog("have no order to cancel: " + _currentOrder);
                return;
            }

            _currentOrder.WhenChanged(_strategy.Connector)
                .Do(o =>
                {
                    if (o.State == OrderStates.Done || o.State == OrderStates.Failed)
                    {
                        _strategy.AddWarningLog($"NOTE THAT ORDER CANCELED {_currentOrder}, {_strategy.Name}: {_strategy.ProcessState}");
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
                {
                    _strategy.PrimaryStopping();
                    _strategy.AddErrorLog("(OrderSync) Still have no respond from terminal about order transaction, timeout: " + Timeout);
                }

                methodAfterSuccess();
            });
        }
    }
}
