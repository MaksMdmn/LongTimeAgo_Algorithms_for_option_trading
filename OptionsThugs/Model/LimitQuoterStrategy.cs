using System;
using System.Linq;
using OptionsThugs.Model.Primary;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class LimitQuoterStrategy : QuoterStrategy
    {
        public decimal QuotePriceShift { get; }
        public decimal StopQuotingPrice { get; }
        public bool IsLimitOrdersAlwaysRepresent { get; set; }

        public LimitQuoterStrategy(Sides quotingSide, decimal quotingVolume, decimal quotePriceShift)
            : this(quotingSide, quotingVolume, quotePriceShift, 0)
        { }

        public LimitQuoterStrategy(Sides quotingSide, decimal quotingVolume, decimal quotePriceShift, decimal stopQuotingPrice)
            : base(quotingSide, quotingVolume)
        {
            QuotePriceShift = quotePriceShift;
            StopQuotingPrice = stopQuotingPrice;
            IsLimitOrdersAlwaysRepresent = true;
        }

        protected sealed override void QuotingProcess()
        {
            try
            {
                if (!OrderSynchronizer.IsAnyOrdersInWork
                    && PositionSynchronizer.IsPosAndTradesEven)
                {
                    Quote bestQuote = GetSuitableBestLimitQuote();

                    if (bestQuote == null) return;

                    decimal price = bestQuote.Price + QuotePriceShift;
                    decimal volume = Math.Abs(Volume) - Math.Abs(Position);

                    if (volume <= 0) return;

                    if (IsLimitPriceAcceptableForQuoting(price))
                    {
                        var order = this.CreateOrder(QuotingSide, price, volume);

                        order.WhenRegistered(Connector)
                            .Do(() => ProcessFastOrder(order))
                            .Once()
                            .Apply(this);

                        OrderSynchronizer.PlaceOrder(order);
                    }
                    else if (IsLimitOrdersAlwaysRepresent && StopQuotingPrice > 0)
                    {
                        var order = this.CreateOrder(QuotingSide, StopQuotingPrice, volume);

                        order.WhenRegistered(Connector)
                            .Do(() => ProcessSlowOrder(order))
                            .Once()
                            .Apply(this);

                        OrderSynchronizer.PlaceOrder(order);
                    }
                }
            }
            catch (Exception ex)
            {
                this.AddErrorLog(ex);
            }
        }

        private void ProcessFastOrder(Order order)
        {
            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (OrderSynchronizer.IsAnyOrdersInWork
                    && IsQuotingNeeded(order.Price))
                    {
                        try
                        {
                            OrderSynchronizer.CancelCurrentOrder();
                        }
                        catch (InvalidOperationException ex)
                        {
                            IncrMaxErrorCountIfNotScared();
                            this.AddWarningLog("MaxErrorCount was incremented");
                            this.AddErrorLog(ex);
                        }
                    }
                })
                .Until(() => !OrderSynchronizer.IsAnyOrdersInWork || ProcessState == ProcessStates.Stopping)
                .Apply(this);
        }

        private void ProcessSlowOrder(Order order)
        {
            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    if (OrderSynchronizer.IsAnyOrdersInWork
                    && IsBestQuoteMyQuote(order, GetSuitableBestLimitQuote()))
                    {
                        try
                        {
                            OrderSynchronizer.CancelCurrentOrder();
                        }
                        catch (InvalidOperationException ex)
                        {
                            IncrMaxErrorCountIfNotScared();
                            this.AddWarningLog("MaxErrorCount was incremented");
                            this.AddErrorLog(ex);
                        }
                    }
                })
                .Until(() => !OrderSynchronizer.IsAnyOrdersInWork || ProcessState == ProcessStates.Stopping)
                .Apply(this);
        }

        private bool IsQuotingNeeded(decimal currentQuotingPrice)
        {
            Quote bestQuote = GetSuitableBestLimitQuote();
            Quote preBestQuote = GetSuitableLimitQuotes()[1]; // 2ая лучшая котировка

            if (bestQuote == null || preBestQuote == null)
                return true; // снять заявку

            if (!IsLimitPriceAcceptableForQuoting(bestQuote.Price))
                return true; // снять заявку

            if (bestQuote.Price != currentQuotingPrice)
                return true; // цена выше бида или ниже аска

            if (Math.Abs(currentQuotingPrice - preBestQuote.Price) > Security.PriceStep.Value)
                return true; //есть гэп котировок в стакане и мы стоим выше чем на 1 шаг от лучшей котировки

            return false;
        }

        private bool IsBestQuoteMyQuote(Order order, Quote bestQuote)
        {
            var bestSize = bestQuote.Volume;
            var bestPrice = bestQuote.Price;
            var ordSize = order.Volume - order.GetTrades(Connector).Sum(mt => mt.Order.Volume);
            var ordPrice = order.Price;

            if (bestSize == ordSize && bestPrice == ordPrice)
                return true;

            return false;
        }

        private bool IsLimitPriceAcceptableForQuoting(decimal currentPrice)
        {
            if (StopQuotingPrice == 0)
                return true;

            return IsPriceAcceptableForQuoting(currentPrice, StopQuotingPrice);
        }
    }
}
