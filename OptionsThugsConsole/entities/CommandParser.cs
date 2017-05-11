using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ecng.Common;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugsConsole.enums;
using StockSharp.Algo;
using StockSharp.Algo.Derivatives;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Trading.Common;

namespace OptionsThugsConsole.entities
{
    public class CommandParser
    {
        public event Action<string> NewAnswer;

        private static CommandParser Instance;
        private DataManager _dataManager;
        private readonly IConnector _connector;

        private CommandParser(IConnector connector)
        {
            _connector = connector;
        }

        public static CommandParser GetInstance(IConnector connector)
        {
            return Instance ?? (Instance = new CommandParser(connector));
        }

        public void ParseUserMessage(string msg)
        {
            UserCommands cmd;

            var tempMsgArr = msg.Split(' ');

            if (Enum.TryParse(tempMsgArr[0], true, out cmd))
                ParseCommand(cmd, tempMsgArr.Where(val => val != tempMsgArr[0]).ToArray());
            else
                OnNewAnswer("entered command is incorrect. Please try one of follows: "
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, Enum.GetNames(typeof(UserCommands))));
        }

        private void ParseCommand(UserCommands cmd, string[] userParams)
        {
            try
            {
                switch (cmd)
                {
                    case UserCommands.Conn:
                        DoConnectCmd();
                        break;
                    case UserCommands.Create:
                        DoCreateCmd(userParams);
                        break;
                    case UserCommands.Start:
                        DoStartCmd(userParams);
                        break;
                    case UserCommands.Stop:
                        DoStopCmd(userParams);
                        break;
                    case UserCommands.Status:
                        DoStatusCmd();
                        break;
                    //case UserCommands.Deals:
                    //    DoDealsCmd(userParams);
                    //    break;
                    case UserCommands.Calc:
                        DoCalcCmd(userParams);
                        break;
                    case UserCommands.Settings:
                        DoSettingsCmd(userParams);
                        break;
                    case UserCommands.Dconn:
                        DoExit();
                        break;
                }
            }
            catch (Exception e1)
            {
                OnNewAnswer($"unknown exception: {e1.Message}", ConsoleColor.Red);
            }
        }

        private void DoCalcCmd(string[] userParams)
        {
            if (userParams.Length < 1 || userParams.Length > 2)
            {
                OnNewAnswer($"please enter type of calculation that should be done: {UserKeyWords.Grk} {UserKeyWords.Vol} {UserKeyWords.Spr} " +
                            $"by option series (set middle option strike) or {UserKeyWords.Pos} for cur. position", ConsoleColor.Yellow);
                return;
            }

            UserKeyWords kw1;
            var calcType = userParams[0];
            if (!Enum.TryParse(calcType, true, out kw1))
            {
                OnNewAnswer($"please enter correct command: {UserKeyWords.Grk} {UserKeyWords.Vol} {UserKeyWords.Spr}", ConsoleColor.Yellow);
                return;
            }

            if (kw1 == UserKeyWords.Pos && userParams.Length != 1)
            {
                OnNewAnswer($"{UserKeyWords.Pos} command should be used without additional args", ConsoleColor.Yellow);
                return;
            }

            if (kw1 != UserKeyWords.Pos && userParams.Length != 2)
            {
                OnNewAnswer($"{UserKeyWords.Grk} {UserKeyWords.Vol} {UserKeyWords.Spr} commands should be used with sec. code", ConsoleColor.Yellow);
                return;
            }

            var tempSecurityPositionMap = new Dictionary<Security, decimal>();

            var sb = new StringBuilder();
            sb.AppendLine();

            if (userParams.Length == 1)
            {
                _dataManager
                    .LookupAllConnectorsPositions()
                    .ForEach(p => { tempSecurityPositionMap.Add(p.Security, p.CurrentValue ?? 0); });
            }
            else
            {
                var secCodePart = userParams[1];

                _dataManager
                    .LookupCollectionThroughExistingSecurities(secCodePart)
                    .ForEach(s => { tempSecurityPositionMap.Add(s, 1); });
            }



            switch (kw1)
            {
                case UserKeyWords.Pos:
                    decimal delta = 0M;
                    decimal gamma = 0M;
                    decimal vega = 0M;
                    decimal theta = 0M;

                    tempSecurityPositionMap.ForEach(kvp =>
                    {
                        if (kvp.Value == 0)
                        {
                            return;
                        }

                        if (kvp.Key.Type == SecurityTypes.Future)
                        {
                            delta += kvp.Value;
                        }

                        if (kvp.Key.Type == SecurityTypes.Option)
                        {
                            var bs = new BlackScholes(kvp.Key, _dataManager.UnderlyingAsset, _connector);
                            var lastPrice = kvp.Value > 0
                                ? _dataManager.UnderlyingAsset.BestBid.Price
                                : _dataManager.UnderlyingAsset.BestAsk.Price;

                            delta += (bs.Delta(DateTimeOffset.Now, null, lastPrice) ?? 0) * kvp.Value;
                            gamma += (bs.Gamma(DateTimeOffset.Now, null, lastPrice) ?? 0) * kvp.Value;
                            vega += (bs.Vega(DateTimeOffset.Now, null, lastPrice) ?? 0) * kvp.Value;
                            theta += (bs.Theta(DateTimeOffset.Now, null, lastPrice) ?? 0) * kvp.Value;
                        }
                    });

                    sb.Append($"POSITIONS GREEKS " +
                              $"delta: {GreeksRounding(delta)} " +
                              $"gamma: {GreeksRounding(gamma)} " +
                              $"vega: {GreeksRounding(vega)} " +
                              $"theta: {GreeksRounding(theta)}");
                    break;
                case UserKeyWords.Grk:

                    var unAssetCodePart = _dataManager.UnderlyingAsset.Code.Substring(0, 2);

                    tempSecurityPositionMap.ForEach(kvp =>
                    {
                        var curAssetCodePart = kvp.Key.Code.Substring(0, 2);

                        if (!curAssetCodePart.CompareIgnoreCase(unAssetCodePart))
                            return;


                        if (kvp.Key.Type == SecurityTypes.Future)
                        {
                            sb.Append(_dataManager.GetSecurityStringRepresentation(kvp.Key))
                            .Append($" delta: {kvp.Value}")
                            .AppendLine();
                        }

                        if (kvp.Key.Type == SecurityTypes.Option)
                        {
                            var bs = new BlackScholes(kvp.Key, _dataManager.UnderlyingAsset, _connector);
                            var lastPrice = kvp.Value > 0
                                ? _dataManager.UnderlyingAsset.BestBid.Price
                                : _dataManager.UnderlyingAsset.BestAsk.Price;


                            sb.Append(_dataManager.GetSecurityStringRepresentation(kvp.Key))
                            .Append($" delta: {GreeksRounding(bs.Delta(DateTimeOffset.Now, null, lastPrice) ?? 0)}")
                            .Append($" gamma: {GreeksRounding(bs.Gamma(DateTimeOffset.Now, null, lastPrice) ?? 0)}")
                            .Append($" vega: {GreeksRounding(bs.Vega(DateTimeOffset.Now, null, lastPrice) ?? 0)}")
                            .Append($" theta: {GreeksRounding(bs.Theta(DateTimeOffset.Now, null, lastPrice) ?? 0)}")
                            .AppendLine();
                        }
                    });
                    break;
                case UserKeyWords.Spr:
                    tempSecurityPositionMap.ForEach(kvp =>
                    {
                        sb.Append(_dataManager.GetSecurityStringRepresentation(kvp.Key))
                        .Append($" spread: {PriceRounding(kvp.Key.BestPair?.SpreadPrice ?? 0)}")
                        .AppendLine();
                    });
                    break;
                case UserKeyWords.Vol:
                    tempSecurityPositionMap.ForEach(kvp =>
                    {
                        if (kvp.Key.Type == SecurityTypes.Option)
                        {
                            var bs = new BlackScholes(kvp.Key, _dataManager.UnderlyingAsset, _connector);

                            var bidVol = kvp.Key.BestBid == null ? 0 :
                                VolaRounding(bs.ImpliedVolatility(DateTimeOffset.Now, kvp.Key.BestBid.Price) ?? 0);
                            var askVol = kvp.Key.BestAsk == null ? 0 :
                                VolaRounding(bs.ImpliedVolatility(DateTimeOffset.Now, kvp.Key.BestAsk.Price) ?? 0);

                            sb.Append(_dataManager.GetSecurityStringRepresentation(kvp.Key))
                            .Append($" bid vol: {bidVol}")
                            .Append($" ask vol: {askVol}")
                            .AppendLine();
                        }
                    });
                    break;
                default:
                    sb.Append($"please enter correct command: {UserKeyWords.Grk} {UserKeyWords.Vol} {UserKeyWords.Spr} " +
                        "by option series (set middle option strike) or emptpy string for cur. positions");
                    OnNewAnswer(sb.ToString(), ConsoleColor.Yellow);
                    return;
            }

            OnNewAnswer(sb.ToString());
        }

        private void DoSettingsCmd(string[] userParams)
        {
            if (userParams.Length < 1 || userParams.Length > 2)
            {
                OnNewAnswer("please enter setting name and/or new setting value to change it (or keyword 'all' to show up settings)", ConsoleColor.Yellow);
                return;
            }

            var settingName = userParams[0];

            if (userParams.Length == 1)
            {

                UserKeyWords kw;

                if (Enum.TryParse(settingName, true, out kw))
                    AppConfigManager.GetInstance().PrintAllSettings();
                else
                    OnNewAnswer(AppConfigManager.GetInstance().GetSettingValue(settingName), ConsoleColor.Yellow);

            }

            if (userParams.Length == 2)
            {
                var settingValue = userParams[1];
                var oldValue = AppConfigManager.GetInstance().GetSettingValue(settingName);

                AppConfigManager.GetInstance().UpdateConfigFile(settingName, settingValue);

                OnNewAnswer($"Changed, old value: {oldValue} new value: {settingValue}");
            }
        }

        private void DoStatusCmd()
        {
            OnNewAnswer("", ConsoleColor.Green);
            OnNewAnswer($"connection: {_connector.Name}, status: {_connector.ConnectionState}", ConsoleColor.Green, false);
            OnNewAnswer($"loaded securities: {_connector.Securities.Count()}", ConsoleColor.Green, false);
            OnNewAnswer($"loaded portfolios: {_connector.Portfolios.Count()}", ConsoleColor.Green, false);

            var posSb = new StringBuilder();
            _connector.Positions.ForEach(p =>
            {
                posSb.AppendLine($"{p.Portfolio.Name}    {p.CurrentValue}    {p.Security.Name}");
            });

            OnNewAnswer($"terminal positions: {Environment.NewLine}{posSb}", ConsoleColor.Green, false);

            if (_dataManager.MappedStrategies.Count == 0)
            {
                OnNewAnswer("still no strategies created.", ConsoleColor.Green, false);
            }
            else
            {
                var sb = new StringBuilder();
                sb
                .Append("strategies: ")
                .AppendLine();

                _dataManager.MappedStrategies.ForEach(kvp =>
                {
                    sb.Append("name: ")
                        .Append(kvp.Key)
                        .AppendLine()
                        .Append("state: ")
                        .Append(kvp.Value.ProcessState)
                        .Append(" errors: ")
                        .Append(kvp.Value.ErrorCount)
                        .Append(" trades: ")
                        .Append(kvp.Value.MyTrades.Count())
                        .Append(" position: ")
                        .Append(kvp.Value.Position)
                        .Append(" other: ")
                        .Append(kvp.Value.ToString())
                        .AppendLine()
                        .AppendLine();

                    OnNewAnswer(sb.ToString());
                });
            }
            OnNewAnswer($"program underlying asset: {_dataManager.UnderlyingAsset.Code}", ConsoleColor.Green, false);
        }

        private void DoStopCmd(string[] userParams)
        {
            if (userParams.Length != 1)
            {
                OnNewAnswer("please enter name one of existing strategies  to start (or keyword 'all')", ConsoleColor.Yellow);
                return;
            }

            string strategyName = userParams[0];

            UserKeyWords kw;

            var keysToRemove = new List<string>();

            if (Enum.TryParse(strategyName, true, out kw))
            {
                if (kw == UserKeyWords.All)
                    _dataManager.MappedStrategies.ForEach(kvp =>
                    {
                        kvp.Value.Stop();
                        keysToRemove.Add(kvp.Key);
                    });
            }
            else
            {
                if (_dataManager.MappedStrategies.ContainsKey(strategyName))
                {
                    _dataManager.MappedStrategies[strategyName].Stop();
                    keysToRemove.Add(strategyName);
                }

                else
                    OnNewAnswer("please choose correct strategy name from following: "
                        + _dataManager.MappedStrategies.Select(kvp => kvp.Key + Environment.NewLine), ConsoleColor.Yellow);
            }

            keysToRemove.ForEach(s => { _dataManager.MappedStrategies.Remove(s); });
        }

        private void DoStartCmd(string[] userParams)
        {
            if (userParams.Length != 1)
            {
                OnNewAnswer("please enter one name of strategy to start (or keyword 'all')", ConsoleColor.Yellow);
                return;
            }

            string strategyName = userParams[0];

            UserKeyWords kw;

            if (Enum.TryParse(strategyName, true, out kw))
            {
                if (kw == UserKeyWords.All)
                    _dataManager.MappedStrategies.ForEach(kvp =>
                    {
                        kvp.Value.WhenStarted()
                            .Do(() => NewAnswer($"{kvp.Key} strategy started."))

                            .Apply(kvp.Value);
                        kvp.Value.WhenStopping()
                            .Do(() => NewAnswer($"{kvp.Key} strategy stopping, pos: {kvp.Value.Position}"))
                            .Apply(kvp.Value);

                        kvp.Value.Start();
                    });
            }
            else
            {
                if (_dataManager.MappedStrategies.ContainsKey(strategyName))
                {
                    var soughtStrategy = _dataManager.MappedStrategies[strategyName];

                    soughtStrategy.WhenStarted()
                        .Do(() => NewAnswer($"{soughtStrategy} strategy started."))
                        .Apply(soughtStrategy);

                    soughtStrategy.WhenStopping()
                        .Do(() => NewAnswer($"{soughtStrategy} strategy stopping, pos: {soughtStrategy.Position}"))
                        .Apply(soughtStrategy);

                    soughtStrategy.Start();
                }
                else
                {
                    OnNewAnswer("please choose correct strategy name from following: "
                        + _dataManager.MappedStrategies.Select(kvp => kvp.Key + Environment.NewLine), ConsoleColor.Yellow);
                }
            }
        }

        private void DoCreateCmd(string[] userParams)
        {
            if (userParams.Length < 2)
            {
                OnNewAnswer("please enter type and name of created strategy.", ConsoleColor.Yellow);
                return;
            }

            StrategyTypes param1;
            string param2 = userParams[1];

            if (param2.Length < 5
                || Char.IsNumber(param2.ToCharArray()[0]))
            {
                OnNewAnswer("strategy name should have length more that 5 symbols and started from letter (NOT number)", ConsoleColor.Yellow);
                return;
            }

            if (!Enum.TryParse(userParams[0], true, out param1))
            {
                OnNewAnswer($"cannot create such a strategy. Possible types: {Enum.GetNames(typeof(StrategyTypes))}", ConsoleColor.Yellow);
                return;
            }

            if (_dataManager.MappedStrategies.ContainsKey(param2))
            {
                OnNewAnswer("strategy with such name has already exist.", ConsoleColor.Yellow);
                return;
            }

            StrategyManager strategyMaker = new StrategyManager(_connector, _dataManager);

            OnNewAnswer(strategyMaker.GetStrategyStringLayout(param1));

            try
            {
                OnNewAnswer(Environment.NewLine +
                            "Please enter straregy params at the next line like in format above AND:" +
                            Environment.NewLine +
                            "-main separator is semicolon WITHOUT space" + Environment.NewLine +
                            "-after last value DO NOT USE semicolon" + Environment.NewLine +
                            "-USE spaces like a separator in array []", ConsoleColor.Yellow);
                var strategy = strategyMaker.CreateStrategyFromString(param1, Console.ReadLine());
                _dataManager.MappedStrategies.Add(param2, strategy);

                OnNewAnswer("strategy created.");
            }
            catch (ArgumentException e1)
            {
                OnNewAnswer(e1.Message, ConsoleColor.Yellow);
            }
        }

        private void DoConnectCmd()
        {
            _connector.Connected += () =>
            {
                OnNewAnswer("connected (success), loading securities, pls wait...");
                _dataManager = new DataManager(_connector);
            };

            _connector?.Connect();

            Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(3000); //dunno what to do
                    _connector.Securities.ForEach(s =>
                    {
                        _dataManager.MappedSecurities.Add(_dataManager.GetSecurityStringRepresentation(s), s);
                    });

                    _dataManager.UnderlyingAsset = _dataManager.LookupThroughExistingSecurities(
                        AppConfigManager.GetInstance().GetSettingValue(UserConfigs.UnderlyingAsset.ToString()));

                    _dataManager.RegisterMappedUndAssetsSecuruties();

                    OnNewAnswer($"{_dataManager.MappedSecurities.Count} securities loaded.");
                }
                catch (Exception e1)
                {
                    OnNewAnswer($"unknown exception: {e1.Message}", ConsoleColor.Red);
                }
            });
        }

        private void DoExit()
        {
            _connector.Disconnected += () =>
            {
                OnNewAnswer("disconnected (success)");
                _dataManager = null;
            };

            _connector?.Disconnect();
        }

        private decimal GreeksRounding(decimal value)
        {
            return Math.Round(value, 4);
        }

        private decimal VolaRounding(decimal value)
        {
            return Math.Round(value, 2);
        }

        private decimal PriceRounding(decimal value)
        {
            return Math.Round(value, 2);
        }


        private void OnNewAnswer(string msg, ConsoleColor color = ConsoleColor.White, bool showDateTime = true)
        {
            if (showDateTime)
                msg = DateTime.Now + ": " + msg;

            if (color != ConsoleColor.White)
                Console.ForegroundColor = color;

            NewAnswer?.Invoke(msg);
            Console.ResetColor();
        }
    }
}
