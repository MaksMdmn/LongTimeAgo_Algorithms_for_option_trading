using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public abstract class QuotingStrategy : Strategy
    {
        //TODO lock на ордер нужно сделать, иначе опять буду лупить в наллы или в ордер без айди из наследников
        protected Sides QuotingSide { get; private set; }
        protected MarketDepth MarketDepth { get; private set; }
        protected decimal ExecutedVolume { get; private set; }
        protected decimal RestVolume => Volume - ExecutedVolume;
        protected decimal QuotePriceShift { get; private set; }
        protected Order OrderInWork { get;  set; }

        protected QuotingStrategy(Sides quotingSide, decimal quotingVolume, decimal quotePriceShift)
        {
            QuotingSide = quotingSide;
            Volume = quotingVolume;
            ExecutedVolume = 0;
            QuotePriceShift = quotePriceShift;

            CancelOrdersWhenStopping = true; //не пашет
            CommentOrders = true;
            DisposeOnStop = false;
            MaxErrorCount = 1;
            OrdersKeepTime = TimeSpan.Zero;
            //WaitAllTrades = true;
        }

        protected override void OnStarted()
        {
            //TODO обработчики ретёрнов
            if (Connector == null || Security == null || Portfolio == null) return;
            if (Volume <= 0) return;

            Connector.RegisterMarketDepth(Security);

            MarketDepth = GetMarketDepth(Security);

            //start here
            Security.WhenMarketDepthChanged(Connector)
                .Do(QuotingProcess)
                .Once()
                .Apply(this);

            this.WhenPositionChanged()
                .Do(() =>
                {
                    if (Math.Abs(Position) >= Volume)
                    {
                        //TODO логи
                        Stop();
                    }
                })
                .Apply(this);

            //TODO this.WhenError();  this.Connector.OrderRegisterFailed;

            base.OnStarted();
        }

        protected abstract void QuotingProcess();

        protected abstract bool IsQuotingNeeded(MarketDepth md, decimal marketPrice, decimal currentQuotingPrice, decimal currentQuotingVolume);
    }
}
