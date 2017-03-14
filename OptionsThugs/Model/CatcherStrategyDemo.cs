using System;
using System.Threading;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class CatcherStrategyDemo : Strategy
    {
        public OrderTypes StrategyOrdersType { get; set; }
        public Sides StrategySide { get; set; }
        public decimal MarketPlank { get; set; }

        private readonly LogManager _logManager;
        private Order _rolloingOrder;
        private Quote _bestQuoteToCatch;
        private decimal _targetSize;


        public CatcherStrategyDemo(Security security, Portfolio strategyPortfolio, decimal sizeToCatch, Sides side, OrderTypes orderTypes, IConnector connector)
        {
            Security = security;
            _targetSize = sizeToCatch;
            StrategyOrdersType = orderTypes;
            StrategySide = side;
            Portfolio = strategyPortfolio;
            Connector = connector;

            CancelOrdersWhenStopping = true; //не пашет
            CommentOrders = true;
            DisposeOnStop = false;
            MaxErrorCount = 1;
            OrdersKeepTime = TimeSpan.Zero;
            //WaitAllTrades = true;

            _logManager = new LogManager();
        }
        protected override void OnStarted()
        {
            Action strategyProcess;

            if (!(StrategyOrdersType == OrderTypes.Limit || StrategyOrdersType == OrderTypes.Market))
                throw new ArgumentException("Strategy type should be limit or market. " + StrategyOrdersType);

            if (StrategyOrdersType == OrderTypes.Market && MarketPlank <= 0)
                throw new ArgumentOutOfRangeException("Market strategy cannot operate with MarketPlank value below zero: " + MarketPlank);

            if (_targetSize <= 0) return;

            if (StrategyOrdersType == OrderTypes.Limit)
            {
                strategyProcess = ProcessToCatchByLimitOrders;
            }
            else
            {
                strategyProcess = ProcessToCatchByMarketOrders;
            }
            Security.WhenMarketDepthChanged(Connector)
                .Do(strategyProcess)
                .Once()
                .Apply(this);

            base.OnStarted();
        }

        private void ProcessToCatchByLimitOrders()
        {
            //TODO определять цену событие мд
            //TODO событие исполнения ордера  делать без ререгистер ---> через регистер и кэнсел
            //TODO событие отмены ордера
            //TODO событие ошибки отмены ордера
            //TODO мониторить позу не набирая лишнего

            IMarketRule catchRule;
            decimal price;
            var step = Security.PriceStep.Value;
            var md = Connector.GetMarketDepth(Security);

            if (StrategySide == Sides.Buy)
            {
                _bestQuoteToCatch = Connector.GetMarketDepth(Security).BestBid;

                if (_bestQuoteToCatch == null)
                    throw new ArgumentNullException("На рынке нет бидов");

                price = Security.ShrinkPrice(_bestQuoteToCatch.Price + step);

                catchRule = Security.WhenMarketDepthChanged(Connector)
                    .Do(() =>
                       {
                           CatchQuote(md.BestBid, step);
                           Thread.Sleep(500);
                       });


            }
            else // 0 быть не может, проверяем в OnStarted
            {
                _bestQuoteToCatch = Connector.GetMarketDepth(Security).BestAsk;

                if (_bestQuoteToCatch == null)
                    throw new ArgumentNullException("На рынке нет асков");

                price = Security.ShrinkPrice(_bestQuoteToCatch.Price - step);

                catchRule = Security.WhenMarketDepthChanged(Connector)
                    .Do(() =>
                    {
                        CatchQuote(md.BestAsk, -1 * step);
                        Thread.Sleep(500);
                    });
            }

            _rolloingOrder = CompleteOrder(this.CreateOrder(StrategySide, price, _targetSize));
            _rolloingOrder.WhenRegistered(Connector)
               .Do(() => catchRule.Apply(this))
               .Once()
               .Apply(this);

            RegisterOrder(_rolloingOrder);








            //_rolloingOrder.WhenPartiallyMatched()
            //_rolloingOrder.WhenMatched()
            //_rolloingOrder.WhenCanceled()
            //_rolloingOrder.WhenCancelFailed()


        }

        private void CatchQuote(Quote quote, decimal stepWithSignValue)
        {
            if (quote == null) return;

            if (_rolloingOrder.Price == quote.Price) return;

            CancelOrder(_rolloingOrder);

            var newVolume = Math.Abs(_targetSize) - Math.Abs(Position);
            var newPrice = quote.Price + stepWithSignValue;

            _rolloingOrder = this.CreateOrder(StrategySide, newPrice, newVolume);
            RegisterOrder(_rolloingOrder);
        }

        private void ProcessToCatchByMarketOrders()
        {
            if (Volume == 0 || MarketPlank == 0) return;

            Sides side = Sides.Buy;
            decimal price = 0;
            decimal volume = Volume - Math.Abs(Position);

            if (Volume > 0)
            {
                _bestQuoteToCatch = Connector.GetMarketDepth(Security).BestAsk;

                if (_bestQuoteToCatch == null) return;

                side = Sides.Buy;

                if (MarketPlank >= _bestQuoteToCatch.Price)
                {
                    price = MarketPlank;
                }
                else
                {
                    return;
                }

            }
            else
            {
                _bestQuoteToCatch = Connector.GetMarketDepth(Security).BestBid;

                if (_bestQuoteToCatch == null) return;

                side = Sides.Sell;

                if (MarketPlank <= _bestQuoteToCatch.Price)
                {
                    price = MarketPlank;
                }
                else
                {
                    return;
                }
            }

            var currentOrder = CompleteOrder(this.CreateOrder(side, price, volume));

            currentOrder.WhenRegistered(Connector)
                .Do(o => CancelOrder(o))
                .Once()
                .Apply(this);

            RegisterOrder(currentOrder);
        }

        private Order CompleteOrder(Order order)
        {
            order.Security = Security;
            order.Portfolio = Portfolio;
            return order;
        }

        private Order ChangeOrder(Order order, decimal price, decimal volume)
        {
            order.Price = price;
            order.Volume = volume;
            return order;
        }
    }
}