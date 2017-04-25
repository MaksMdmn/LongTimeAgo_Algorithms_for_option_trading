using System;
using OptionsThugs.Model.Trading;

namespace OptionsThugs.Model.Service
{
    public class StrategyStringCreator
    {
        private readonly StrategyTypes _strategyType;
        private readonly string _separatorBeforVal = "=";
        private readonly string _separatorAfterVal = ";" + System.Environment.NewLine;
        private readonly string _separatorStartArr = "[";
        private readonly string _separatorEndArr = "]";
        private readonly string _defaultMark = "auto";


        public StrategyStringCreator(StrategyTypes strategyType)
        {
            _strategyType = strategyType;
        }

        public string GetHelpDescription()
        {
            switch (_strategyType)
            {
                case StrategyTypes.Dhs:
                    return "Delta hedge strategy automatically gets from terminal futures & options position." +
                           System.Environment.NewLine
                           + "...";
                case StrategyTypes.Lqs:
                    break;
                case StrategyTypes.Mqs:
                    break;
                case StrategyTypes.Pcs:
                    break;
                case StrategyTypes.Sss:
                    break;
            }

            return "incorrect type of strategy;";
        }

        public string GetStrategyStringLayout()
        {
            switch (_strategyType)
            {
                case StrategyTypes.Dhs:
                    return $"fut.pos{_separatorBeforVal}{_defaultMark}{_separatorAfterVal}" +
                           $"opt.pos{_separatorBeforVal}{_defaultMark}{_separatorAfterVal}" +
                           $"deltastep{_separatorBeforVal}1{_separatorAfterVal}" +
                           $"deltabuffer{_separatorBeforVal}0{_separatorAfterVal}" +
                           $"hedgelevels{_separatorBeforVal}{_separatorStartArr}{_separatorEndArr}{_separatorAfterVal}" +
                           $"min.f.pos{_separatorBeforVal}{_defaultMark}{_separatorAfterVal}" +
                           $"max.f.pos{_separatorBeforVal}{_defaultMark}{_separatorAfterVal}";
                case StrategyTypes.Lqs:
                    return $"side{_separatorBeforVal} Buy/Sell{_separatorAfterVal}" +
                           $"volume{_separatorBeforVal}1{_separatorAfterVal}" +
                           $"priceshift{_separatorBeforVal}+-1{_separatorAfterVal}" +
                           $"worstprice{_separatorBeforVal}0{_separatorAfterVal}";
                case StrategyTypes.Mqs:
                    return $"side{_separatorBeforVal} Buy/Sell{_separatorAfterVal}" +
                           $"volume{_separatorBeforVal}1{_separatorAfterVal}" +
                           $"targetprice{_separatorBeforVal} {_separatorAfterVal}";
                case StrategyTypes.Pcs:
                    return $"closeprice{_separatorBeforVal} {_separatorAfterVal}" +
                           $"securityWithSignal{_separatorBeforVal}auto{_separatorAfterVal}" +
                           $"securityDirection{_separatorBeforVal}Up/Down/None{_separatorAfterVal}" +
                           $"positionToclose{_separatorBeforVal}+-{_separatorAfterVal}";
                case StrategyTypes.Sss:
                    return $"cur.pos{_separatorBeforVal}+-{_separatorAfterVal}" +
                           $"cur.pos.price{_separatorBeforVal} {_separatorAfterVal}" +
                           $"spread{_separatorBeforVal} {_separatorAfterVal}" +
                           $"lot{_separatorBeforVal} {_separatorAfterVal}" +
                           $"enterside{_separatorBeforVal}Buy/Sell{_separatorAfterVal}" +
                           $"max.f.number{_separatorBeforVal}+-{_separatorAfterVal}";
            }

            return "incorrect type of strategy;";
        }

        public PrimaryStrategy CompleteStrategyFromString(string strategyParams, out string errMsg)
        {
            errMsg = "ok";
            try
            {
                switch (_strategyType)
                {
                    case StrategyTypes.Dhs:
                        return null;
                    case StrategyTypes.Lqs:
                        break;
                    case StrategyTypes.Mqs:
                        break;
                    case StrategyTypes.Pcs:
                        break;
                    case StrategyTypes.Sss:
                        break;
                }


            }
            catch (Exception e1)
            {
                errMsg = e1.Message;
                return null;
            }

            return null;
        }
    }
}