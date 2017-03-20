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
    public abstract class MyQuotingStrategy : Strategy
    {
        protected Sides QuotingSide { get; private set; }
        protected MarketDepth MarketDepth { get; private set; }

        protected OrderSynchronizer OrderSynchronizer { get; private set; }
        protected PositionSynchronizer PositionSynchronizer { get; private set; }

        protected MyQuotingStrategy(Sides quotingSide, decimal quotingVolume)
        {
            QuotingSide = quotingSide;
            Volume = quotingVolume;
            OrderSynchronizer = new OrderSynchronizer(this);
            PositionSynchronizer = new PositionSynchronizer();

            this.WhenPositionChanged()
                .Do(p => PositionSynchronizer.NewPositionChange(p))
                .Apply(this);

            this.WhenNewMyTrade()
                .Do(mt => PositionSynchronizer.NewTradeChange(mt.Trade.Volume))
                .Apply(this);
           

            CancelOrdersWhenStopping = true; 
            CommentOrders = true;
            DisposeOnStop = false;
            MaxErrorCount = 10;
            OrdersKeepTime = TimeSpan.Zero;
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

    }
}
