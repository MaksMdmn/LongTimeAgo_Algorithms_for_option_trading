using System;
using System.Collections.Generic;
using System.Timers;
using Ecng.Collections;
using Ecng.Common;
using Microsoft.Practices.ObjectBuilder2;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;

namespace Trading.Strategies
{
    public abstract class PrimaryStrategy : Strategy
    {
        public int Timeout { get; set; }

        private bool _isSetDone;
        private volatile bool _isCorrectChild;
        private Security[] _securities;
        private Security[] _marketDepths;
        private Portfolio[] _portfolios;

        //private readonly Timer _workingTimeControl;

        protected PrimaryStrategy()
        {
            Timeout = 2000;

            _isSetDone = false;
            _isCorrectChild = false;

            CancelOrdersWhenStopping = true;
            CommentOrders = true;
            DisposeOnStop = false;
            MaxErrorCount = 10;
            OrdersKeepTime = TimeSpan.Zero;

            TimeHelper.SyncMarketTime();

            //_workingTimeControl = new Timer();
            //_workingTimeControl.Elapsed += (sender, args) =>
            //{
            //    //TODO делать саспенд на клиринг и восстанавливать после (беда в том что на фортс клиринг иногда больше, чем зашито в либе)

            //    ExchangeBoard.Forts.WorkingTime.Periods[0].Times

            //    TimeHelper.Now
            //};
            //_workingTimeControl.Interval = 1000;
            //_workingTimeControl.Enabled = true;
        }

        public void SetStrategyEntitiesForWork(IConnector connector, Security security, Portfolio portfolio)
        {
            Connector = connector;
            Security = security;
            Portfolio = portfolio;

            if (Connector == null || Security == null || Portfolio == null)
                throw new NullReferenceException($"Some of important fields is null: connector {Connector}, security {Security}, portfolio {Portfolio}");

            _isSetDone = true;
        }

        protected void MarkStrategyLikeChild(PrimaryStrategy child)
        {
            child._isCorrectChild = true;
        }

        protected override void OnStarted()
        {
            this.WhenError()
                .Do(e =>
                {
                    ShowAppropriateMsgBox("Strategy.WhenError rule: ", e.ToString(), "Strategy error");
                })
                .Apply(this);

            Connector.Error += e => { ShowAppropriateMsgBox("Connector.Error event: ", e.ToString(), "Connection error"); };
            Connector.ConnectionError += e => { ShowAppropriateMsgBox("Connector.ConnectionError event: ", e.ToString(), "Connection error2"); };
            Connector.OrderRegisterFailed += of => { ShowAppropriateMsgBox("Connector.OrderRegisterFaild event: ", of.Error.ToString(), "Order registration failed"); };

            //this.WhenStopping()
            //    .Do(() => _workingTimeControl.Enabled = false)
            //    .Once()
            //    .Apply(this);

            base.OnStarted();
        }

        protected override void OnStopped()
        {
            if (!_isCorrectChild)
            {
                if (Connector == null)
                    throw new NullReferenceException($"Cannot unregister strategy entities, cause connector is null: {Connector}");

                if (_securities.Length > 0)
                    _securities.ForEach(s => Connector.UnRegisterSecurity(s));

                if (_marketDepths.Length > 0)
                    _marketDepths.ForEach(mds => Connector.UnRegisterMarketDepth(mds));

                if (_portfolios.Length > 0)
                    _portfolios.ForEach(p => Connector.UnRegisterPortfolio(p));
            }

            base.OnStopped();
        }

        protected void DoStrategyPreparation(Security[] securities, Security[] marketDepths, Portfolio[] portfolios)
        {
            if (_isCorrectChild) return;

            if (!_isSetDone)
                throw new ArgumentException($"You should set following entities: Connector: {Connector}, Portfolio: {Portfolio} and strategy Security: {Security}");

            _securities = securities;
            _marketDepths = marketDepths;
            _portfolios = portfolios;

            if (securities.Length > 0)
                securities.ForEach(s => Connector.RegisterSecurity(s));

            if (marketDepths.Length > 0)
                marketDepths.ForEach(mds => Connector.RegisterMarketDepth(mds));

            if (portfolios.Length > 0)
                portfolios.ForEach(p => Connector.RegisterPortfolio(p));
        }

        //TODO это общий обработчик, подумать может нужно останавливать стратегию и писать разные обработчики.
        private void ShowAppropriateMsgBox(string text, string error, string caption)
        {
            //MessageBox.Show(string.Format(CultureInfo.CurrentCulture, text + "{0}", error),
            //        caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
