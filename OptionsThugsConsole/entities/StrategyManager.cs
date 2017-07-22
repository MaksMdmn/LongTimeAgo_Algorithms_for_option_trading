using System;
using System.Linq;
using Ecng.Collections;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugsConsole.enums;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Trading.Common;
using Trading.Strategies;

namespace OptionsThugsConsole.entities
{
    public class StrategyManager
    {
        private const string SeparatorBeforVal = "=";
        public static readonly string SeparatorAfterVal = ";";
        public static readonly string SeparatorStartArr = "[";
        public static readonly string SeparatorEndArr = "]";
        public static readonly string DefaultMark = "a";
        private readonly IConnector _connector;
        private readonly DataManager _dataManager;

        private Portfolio _defaultPortfolio;

        public StrategyManager(IConnector connector, DataManager dataManager)
        {
            _connector = connector;
            _dataManager = dataManager;

            _defaultPortfolio = dataManager.LookupThroughConnectorsPortfolios(
                ConfigManager.GetInstance().GetSettingValue(
                    UserConfigs.Portfolio.ToString()));
        }

        public string GetStrategyStringLayout(StrategyTypes strategyType)
        {
            switch (strategyType)
            {
                case StrategyTypes.Dhs:
                    return $"security{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"options{SeparatorBeforVal}{SeparatorStartArr}{SeparatorEndArr}{SeparatorAfterVal}" +
                           $"deltastep{SeparatorBeforVal}{DefaultMark}{SeparatorAfterVal}" +
                           $"deltabuffer{SeparatorBeforVal}{DefaultMark}{SeparatorAfterVal}" +
                           $"hedgelevels{SeparatorBeforVal}{SeparatorStartArr}u/d0000 u/d1111 or {DefaultMark}{SeparatorEndArr}{SeparatorAfterVal}" +
                           $"min.f.pos{SeparatorBeforVal}{DefaultMark}{SeparatorAfterVal}" +
                           $"max.f.pos{SeparatorBeforVal}{DefaultMark}";
                case StrategyTypes.Lqs:
                    return $"security{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"side{SeparatorBeforVal}Buy/Sell{SeparatorAfterVal}" +
                           $"volume{SeparatorBeforVal}+{SeparatorAfterVal}" +
                           $"priceshift{SeparatorBeforVal}+-{SeparatorAfterVal}" +
                           $"worstprice{SeparatorBeforVal}{DefaultMark}{SeparatorAfterVal}" +
                           $"orderAlwaysPlaced{SeparatorBeforVal}{DefaultMark}(or true/false)";
                case StrategyTypes.Mqs:
                    return $"security{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"side{SeparatorBeforVal}Buy/Sell{SeparatorAfterVal}" +
                           $"volume{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"targetprice{SeparatorBeforVal}";
                case StrategyTypes.Pcs:
                    return $"securityToClose:{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"signalPrice{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"signalSecurity{SeparatorBeforVal}{DefaultMark}{SeparatorAfterVal}" +
                           $"signalDirection{SeparatorBeforVal}Up/Down/None{SeparatorAfterVal}" +
                           $"positionToClose{SeparatorBeforVal}+-";
                case StrategyTypes.Sss:
                    return $"security{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"cur.pos{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"cur.pos.price{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"spread{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"lot{SeparatorBeforVal}{SeparatorAfterVal}" +
                           $"enterside{SeparatorBeforVal}Buy/Sell{SeparatorAfterVal}" +
                           $"limitedFuturesNumber{SeparatorBeforVal}{DefaultMark}";
            }

            return "incorrect type of strategy;";
        }

        public PrimaryStrategy CreateStrategyFromString(StrategyTypes strategyType, string inputParams)
        {
            PrimaryStrategy resultStrategy = null;
            Security resultStrategySecurity = null;
            var strategyParams = inputParams.Split(SeparatorAfterVal.ToCharArray(), StringSplitOptions.None);

            if (_connector == null)
                throw new NullReferenceException("_connector");

            switch (strategyType)
            {
                case StrategyTypes.Dhs:
                    var dhs1FutCode = strategyParams[0];
                    var dhs2OptCode = strategyParams[1];
                    var dhs3DeltaStep = strategyParams[2];
                    var dhs4DeltaBuffer = strategyParams[3];
                    var dhs5HedgeLevels = strategyParams[4];
                    var dhs6MinFPos = strategyParams[5];
                    var dhs7MaxFPos = strategyParams[6];

                    var tempOptList = ParseToStringArr(dhs2OptCode)
                        .Select(s => _dataManager.LookupThroughExistingSecurities(s));

                    decimal futPosition = _connector.GetSecurityPosition(_defaultPortfolio,
                        _dataManager.LookupThroughExistingSecurities(dhs1FutCode));
                    SynchronizedDictionary<Security, decimal> optionsPositions =
                        _connector.GetSecuritiesPositions(_defaultPortfolio, tempOptList.ToList());

                    var dhsStrategy = new DeltaHedgerStrategy(futPosition, optionsPositions);

                    if (!CheckIfDefault(dhs3DeltaStep))
                        dhsStrategy.DeltaStep = ParseDecimalValue(dhs3DeltaStep);
                    if (!CheckIfDefault(dhs4DeltaBuffer))
                        dhsStrategy.DeltaBuffer = ParseDecimalValue(dhs4DeltaBuffer);
                    if (!CheckIfDefault(dhs5HedgeLevels))
                    {
                        ParseToStringArr(dhs5HedgeLevels).ForEach(s =>
                        {
                            if (s.ToCharArray()[0] == 'u')
                                dhsStrategy.AddHedgeLevel(PriceDirection.Up, ParseDecimalValue(s.Substring(1)));
                            else if (s.ToCharArray()[0] == 'd')
                                dhsStrategy.AddHedgeLevel(PriceDirection.Down, ParseDecimalValue(s.Substring(1)));
                            else
                                throw new ArgumentException("cannot parse such a value into a price level, possible directions are 'u' or 'd'.");

                        });
                    }
                    if (!CheckIfDefault(dhs6MinFPos))
                        dhsStrategy.MinFuturesPositionVal = ParseDecimalValue(dhs6MinFPos);
                    if (!CheckIfDefault(dhs7MaxFPos))
                        dhsStrategy.MaxFuturesPositionVal = ParseDecimalValue(dhs7MaxFPos);

                    resultStrategySecurity = _dataManager.LookupThroughExistingSecurities(dhs1FutCode);
                    resultStrategy = dhsStrategy;
                    break;
                case StrategyTypes.Lqs:
                    var lqs1SecCode = strategyParams[0];
                    var lqs2Side = strategyParams[1];
                    var lqs3Volume = strategyParams[2];
                    var lqs4PriceShift = strategyParams[3];
                    var lqs5WorstPrice = strategyParams[4];
                    var lqs6AlwaysPlaces = strategyParams[5];

                    Sides lSide;
                    var worstPrice = 0M;

                    if (!Enum.TryParse(lqs2Side, true, out lSide))
                        throw new ArgumentException("cannot parse side value (enum exception)");

                    if (!CheckIfDefault(lqs5WorstPrice))
                        worstPrice = ParseDecimalValue(lqs5WorstPrice);

                    var lqsStrategy = new LimitQuoterStrategy(
                        lSide,
                        ParseDecimalValue(lqs3Volume),
                        ParseDecimalValue(lqs4PriceShift),
                        worstPrice);

                    if (!CheckIfDefault(lqs6AlwaysPlaces))
                        lqsStrategy.IsLimitOrdersAlwaysRepresent = ParseBoolValue(lqs6AlwaysPlaces);

                    resultStrategySecurity = _dataManager.LookupThroughExistingSecurities(lqs1SecCode);
                    resultStrategy = lqsStrategy;

                    break;
                case StrategyTypes.Mqs:
                    var mqs1SecCode = strategyParams[0];
                    var mqs2Side = strategyParams[1];
                    var mqs3Volume = strategyParams[2];
                    var mqs4TargetPrice = strategyParams[3];

                    Sides mSide;
                    if (!Enum.TryParse(mqs2Side, true, out mSide))
                        throw new ArgumentException("cannot parse side value (enum exception)");

                    var mqsStrategy = new MarketQuoterStrategy(
                        mSide,
                        ParseDecimalValue(mqs3Volume),
                        ParseDecimalValue(mqs4TargetPrice));

                    resultStrategySecurity = _dataManager.LookupThroughExistingSecurities(mqs1SecCode);
                    resultStrategy = mqsStrategy;

                    break;
                case StrategyTypes.Pcs:
                    var pcs1SecCode = strategyParams[0];
                    var pcs2ClosePrice = strategyParams[1];
                    var pcs3SignalSecCode = strategyParams[2];
                    var pcs4SecDirection = strategyParams[3];
                    var pcs5PosToClose = strategyParams[4];

                    PriceDirection pDirection;
                    if (!Enum.TryParse(pcs4SecDirection, true, out pDirection))
                        throw new ArgumentException("cannot parse price direction value (enum exception)");

                    var pcsStrategy = new PositionCloserStrategy(
                        ParseDecimalValue(pcs2ClosePrice),
                        pDirection,
                        ParseDecimalValue(pcs5PosToClose));

                    if (!CheckIfDefault(pcs3SignalSecCode))
                        pcsStrategy.SecurityWithSignalToClose =
                            _dataManager.LookupThroughExistingSecurities(pcs3SignalSecCode);

                    resultStrategySecurity = _dataManager.LookupThroughExistingSecurities(pcs1SecCode);
                    resultStrategy = pcsStrategy;

                    break;
                case StrategyTypes.Sss:
                    var sss1SecCode = strategyParams[0];
                    var sss2CurPosition = strategyParams[1];
                    var sss3CurPositionPrice = strategyParams[2];
                    var sss4Spread = strategyParams[3];
                    var sss5Lot = strategyParams[4];
                    var sss6EnterSide = strategyParams[5];
                    var sss7LimitedFuturesNumber = strategyParams[6];

                    Sides sSide;
                    if (!Enum.TryParse(sss6EnterSide, true, out sSide))
                        throw new ArgumentException("cannot parse side value (enum exception)");

                    var sssStrategy = new SpreaderStrategy(
                        ParseDecimalValue(sss2CurPosition),
                        ParseDecimalValue(sss3CurPositionPrice),
                        ParseDecimalValue(sss4Spread),
                        ParseDecimalValue(sss5Lot),
                        sSide);

                    if (!CheckIfDefault(sss7LimitedFuturesNumber))
                        sssStrategy.LimitedFuturesValueAbs = ParseDecimalValue(sss7LimitedFuturesNumber);

                    resultStrategySecurity = _dataManager.LookupThroughExistingSecurities(sss1SecCode);
                    resultStrategy = sssStrategy;

                    break;
            }

            resultStrategy?.SetStrategyEntitiesForWork(_connector, resultStrategySecurity, _defaultPortfolio);

            return resultStrategy;
        }

        private bool ParseBoolValue(string value)
        {
            bool result;

            if (!bool.TryParse(value, out result))
                throw new ArgumentException("cannot parse such a value into a bool: " + value);

            return result;
        }

        private decimal ParseDecimalValue(string value)
        {
            decimal result;

            if (!decimal.TryParse(value, out result))
                throw new ArgumentException("cannot parse such a value into a decimal: " + value);

            return result;
        }

        private decimal[] ParseDecimalArrValues(string value)
        {
            return ParseToStringArr(value)
                .Select(ParseDecimalValue)
                .ToArray();
        }

        private string[] ParseToStringArr(string value)
        {
            if (value.Contains(SeparatorStartArr) && value.Contains(SeparatorEndArr))
                return value
                    .Replace(SeparatorStartArr, "")
                    .Replace(SeparatorEndArr, "")
                    .Split(' ');

            throw new ArgumentException("cannot parse array, please enter corrected values, example: [+ + +]");
        }

        private bool CheckIfDefault(string value)
        {
            return value.Equals(DefaultMark);
        }
    }
}
