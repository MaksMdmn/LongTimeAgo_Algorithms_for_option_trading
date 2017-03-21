using System;
using Microsoft.Practices.ObjectBuilder2;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Strategies.Quoting;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace OptionsThugs.Model
{
    public class MySpreadStrategy : Strategy
    {
        private readonly decimal _minSpread;
        private readonly decimal _minPos;
        private readonly decimal _maxPos;
        private readonly decimal _lot;
        private readonly bool _isCloseInLossModeEnabled;

        private volatile bool _isBuyPartActive;
        private volatile bool _isSellPartActive;

        public MySpreadStrategy(decimal minSpread, decimal minPos, decimal maxPos) : this(minSpread, minPos, maxPos, 1, false) { }

        public MySpreadStrategy(decimal minSpread, decimal minPos, decimal maxPos, decimal lot,
            bool isCloseInLossModeEnabled)
        {
            _minSpread = minSpread;
            _minPos = minPos;
            _maxPos = maxPos;
            _lot = lot;
            _isCloseInLossModeEnabled = isCloseInLossModeEnabled;

            _isBuyPartActive = false;
            _isSellPartActive = false;

            CancelOrdersWhenStopping = true;
            CommentOrders = true;
            DisposeOnStop = false;
            MaxErrorCount = 10;
            OrdersKeepTime = TimeSpan.Zero;
        }

        protected override void OnStarted()
        {
            if (Connector == null || Security == null || Portfolio == null) return;

            Connector.RegisterMarketDepth(Security);

            Security.WhenMarketDepthChanged(Connector)
                .Do(md =>
                {
                    var currentSpread = md.BestAsk.Price - md.BestBid.Price;

                    if (currentSpread >= _minSpread)
                    {

                        //TODO:
                        ProcessBuyPart();
                        ProcessSellPart();
                    }
                    else
                    {
                        //TODO check if strategies'll stopped through .Clear()
                        ChildStrategies.Clear();
                        _isBuyPartActive = false;
                        _isSellPartActive = false;
                    }



                })
                .Apply(this);


            base.OnStarted();
        }

        private void ProcessSellPart()
        {
            if (_isSellPartActive)
            {
                //TODO WhenNewTrades WhenStopped - signal to MySpread and check pos, direction, lot etc..
            }
            else
            {


                _isSellPartActive = true;
            }

        }

        private void ProcessBuyPart()
        {
            if (_isBuyPartActive)
            {

            }
            else
            {


                _isBuyPartActive = true;
            }
        }
    }
}