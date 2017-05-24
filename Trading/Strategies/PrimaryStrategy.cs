using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Ecng.Collections;
using Ecng.Common;
using Microsoft.Practices.ObjectBuilder2;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Logging;
using StockSharp.Messages;

namespace Trading.Strategies
{
    public abstract class PrimaryStrategy : Strategy
    {
        public int Timeout { get; set; }

        public event Action PrimaryStrategyStopped;

        private static int GlobalCounter;
        private static readonly TimeSpan DangerPeriodStart;
        private static readonly TimeSpan DangerPeriodEnd;
        protected static readonly TimeSpan AutoUpdatePeriod;

        private bool _isSetDone;
        private volatile bool _isCorrectChild;
        private volatile bool _readyToStop;
        private Security[] _securities;
        private Security[] _marketDepths;
        private Portfolio[] _portfolios;

        static PrimaryStrategy()
        {
            TimeHelper.SyncMarketTime();
            DangerPeriodStart = new TimeSpan(18, 59, 55);
            DangerPeriodEnd = new TimeSpan(19, 5, 5);
            AutoUpdatePeriod = TimeSpan.FromSeconds(2);
            GlobalCounter = 0;
        }

        protected PrimaryStrategy()
        {
            Timeout = 2000;

            _isSetDone = false;
            _isCorrectChild = false;
            _readyToStop = true;

            CancelOrdersWhenStopping = true;
            CommentOrders = true;
            DisposeOnStop = false;
            MaxErrorCount = 10;
            OrdersKeepTime = TimeSpan.Zero;
            Name += GlobalCounter++;
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
            child.Log += message => this.AddWarningLog($"CHILD MESSAGE (parent: {this.Name} child: {child.Name}): {message.Message}");
        }

        protected override void OnStarted()
        {
            ProcessStateChanged += str =>
            {
                if (str.ProcessState == ProcessStates.Stopped)
                    OnStrategyStopped();
            };

            this.WhenOrderChanged().Do(mt =>
                {
                    if (!Orders.Any(o => o.State == OrderStates.Active || o.State == OrderStates.Pending))
                        _readyToStop = true;
                    else
                        _readyToStop = false;

                    Debug.WriteLine("IS STRATEGY READY TO STOP: " + _readyToStop);
                })
            .Apply(this);

            this.WhenError()
                .Do(e =>
                {
                    ShowAppropriateMsgBox("Strategy.WhenError rule: ", e.ToString(), "Strategy error");
                })
                .Apply(this);

            Connector.Error += e => { ShowAppropriateMsgBox("Connector.Error event: ", e.ToString(), "Connection error"); };
            Connector.ConnectionError += e => { ShowAppropriateMsgBox("Connector.ConnectionError event: ", e.ToString(), "Connection error2"); };
            Connector.OrderRegisterFailed += of => { ShowAppropriateMsgBox("Connector.OrderRegisterFaild event: ", of.Error.ToString(), "Order registration failed"); };

            base.OnStarted();
        }


        protected override void OnStopping()
        {
            try
            {
                while (!_readyToStop)
                {
                    /*NOP*/
                }

                Debug.WriteLine("NO MORE ORDERS, CONTINUE STOPPING");

                Rules.Clear();
                base.OnStopping();
            }
            catch (Exception e1)
            {
                this.AddErrorLog(e1);
            }
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

        protected bool IsStrategyStopping()
        {
            return ProcessState == ProcessStates.Stopping;
        }
        //TODO это общий обработчик, подумать может нужно останавливать стратегию и писать разные обработчики.
        private void ShowAppropriateMsgBox(string text, string error, string caption)
        {
            //MessageBox.Show(string.Format(CultureInfo.CurrentCulture, text + "{0}", error),
            //        caption, MessageBoxButton.OK, MessageBoxImage.Error);

            Debug.WriteLine($"{text} {error}. Strategy state:{ProcessState}");
        }

        protected bool IsTradingTime()
        {
            if (Security == null)
                return false;

            var currentTime = TimeHelper.Now;
            var checkingTime = currentTime.Add(TimeSpan.FromSeconds(10));

            if (checkingTime.TimeOfDay > DangerPeriodStart && currentTime.TimeOfDay < DangerPeriodEnd)
                return false;

            return Security.Board.IsTradeTime(checkingTime)
                && Security.Board.IsTradeTime(currentTime);
        }

        private void OnStrategyStopped()
        {
            PrimaryStrategyStopped?.Invoke();
        }

        public override string ToString()
        {
            return "MaxVolume: " + Volume + " ";
        }
    }
}
