using System;
using System.Linq;
using System.Text;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugsConsole.enums;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using Trading.Common;
using Trading.Strategies;

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
                case UserCommands.Deals:
                    DoDealsCmd(userParams);
                    break;
                case UserCommands.Calc:

                    break;
                case UserCommands.Settings:
                    DoSettingsCmd(userParams);
                    break;
                case UserCommands.Dconn:
                    DoExit();
                    break;
            }
        }

        private void DoSettingsCmd(string[] userParams)
        {
            if (userParams.Length < 1 || userParams.Length > 2)
            {
                OnNewAnswer("please enter setting name and/or new setting value to change it (or keyword 'all' to show up settings)");
                return;
            }

            var settingName = userParams[0];

            if (userParams.Length == 1)
            {
                try
                {
                    UserKeyWords kw;

                    if (Enum.TryParse(settingName, true, out kw))
                        OnNewAnswer(AppConfigManager.GetInstance().GetAllSettings());
                    else
                        OnNewAnswer(AppConfigManager.GetInstance().GetSettingValue(settingName));
                }
                catch (Exception e1)
                {
                    OnNewAnswer($"unknown exception: {e1.Message}");
                }
            }

            if (userParams.Length == 2)
            {
                var settingValue = userParams[1];
                var oldValue = AppConfigManager.GetInstance().GetSettingValue(settingName);

                AppConfigManager.GetInstance().UpdateConfigFile(settingName, settingValue);

                OnNewAnswer($"Changed, old value: {oldValue} new value: {settingValue}");
            }
        }

        private void DoDealsCmd(string[] userParams)
        {
            if (userParams.Length != 1)
            {
                OnNewAnswer("please enter name one of existing strategies to print its deals (or keyword 'all')");
                return;
            }

            string strategyName = userParams[0];

            try
            {
                UserKeyWords kw;

                if (Enum.TryParse(strategyName, true, out kw))
                {
                    if (kw == UserKeyWords.All)
                        _dataManager.MappedStrategies.ForEach(kvp =>
                        {
                            var sb = new StringBuilder();
                            kvp.Value.MyTrades.ForEach(mt =>
                            {
                                sb
                                .Append(mt.Trade.Time)
                                .Append(" ")
                                .Append(mt.Trade.Security?.ShortName)
                                .Append(" ")
                                .Append(mt.Trade.OrderDirection)
                                .Append(" ")
                                .Append(mt.Trade.Price)
                                .Append(" ")
                                .Append(mt.Trade.Volume)
                                .Append(" ")
                                .Append(mt.Trade.Status)
                                .AppendLine();
                            });

                            OnNewAnswer($"{kvp.Key} deals: {Environment.NewLine} {sb}");
                        });
                }
                else
                {
                    if (_dataManager.MappedStrategies.ContainsKey(strategyName))
                    {
                        var sb = new StringBuilder();

                        _dataManager.MappedStrategies[strategyName].MyTrades.ForEach(mt =>
                        {
                            sb
                            .Append(mt.Trade.Time)
                            .Append(" ")
                            .Append(mt.Trade.Security?.ShortName)
                            .Append(" ")
                            .Append(mt.Trade.OrderDirection)
                            .Append(" ")
                            .Append(mt.Trade.Price)
                            .Append(" ")
                            .Append(mt.Trade.Volume)
                            .Append(" ")
                            .Append(mt.Trade.Status)
                            .AppendLine();
                        });

                        OnNewAnswer($"{strategyName} deals: {Environment.NewLine} {sb}");
                    }
                    else
                    {
                        OnNewAnswer("please choose correct strategy name from following: "
                            + _dataManager.MappedStrategies.Select(kvp => kvp.Key + Environment.NewLine));
                    }
                }
            }
            catch (Exception e1)
            {
                OnNewAnswer($"unknown exception: {e1.Message}");
            }
        }

        private void DoStatusCmd()
        {

            OnNewAnswer($"connection: {_connector.Name}, status: {_connector.ConnectionState}");
            OnNewAnswer($"loaded securities: {_connector.Securities.Count()}");
            OnNewAnswer($"loaded portfolios: {_connector.Portfolios.Count()}");
            OnNewAnswer($"loaded trades: {_connector.Trades.Count()}");

            var posSb = new StringBuilder();
            _connector.Positions.ForEach(p =>
            {
                posSb.AppendLine($"{p.ClientCode} {p.Security.Name} {p.CurrentValue}");
            });

            OnNewAnswer($"terminal positions: {posSb}");

            if (_dataManager.MappedStrategies.Count == 0)
            {
                OnNewAnswer("still no strategies created.");
            }
            else
            {
                _dataManager.MappedStrategies.ForEach(kp =>
                {
                    var sb = new StringBuilder();
                    sb.Append("name: ")
                        .Append(kp.Key)
                        .Append(" state: ")
                        .Append(kp.Value.ProcessState)
                        .Append(" errors: ")
                        .Append(kp.Value.ErrorCount)
                        .Append(" trades: ")
                        .Append(kp.Value.MyTrades.Count())
                        .Append(" pnl: ")
                        .Append(kp.Value.PnL)
                        .Append(" position: ")
                        .Append(kp.Value.Position)
                        .AppendLine();

                    OnNewAnswer(sb.ToString());
                });
            }
        }

        private void DoStopCmd(string[] userParams)
        {
            if (userParams.Length != 1)
            {
                OnNewAnswer("please enter name one of existing strategies  to start (or keyword 'all')");
                return;
            }

            string strategyName = userParams[0];

            try
            {
                UserKeyWords kw;

                if (Enum.TryParse(strategyName, true, out kw))
                {
                    if (kw == UserKeyWords.All)
                        _dataManager.MappedStrategies.ForEach(kvp => { kvp.Value.Stop(); });
                }
                else
                {
                    if (_dataManager.MappedStrategies.ContainsKey(strategyName))
                        _dataManager.MappedStrategies[strategyName].Stop();
                    else
                        OnNewAnswer("please choose correct strategy name from following: "
                            + _dataManager.MappedStrategies.Select(kvp => kvp.Key + Environment.NewLine));
                }
            }
            catch (Exception e1)
            {
                OnNewAnswer($"unknown exception: {e1.Message}");
            }
        }

        private void DoStartCmd(string[] userParams)
        {
            if (userParams.Length != 1)
            {
                OnNewAnswer("please enter one name of strategy to start (or keyword 'all')");
                return;
            }

            string strategyName = userParams[0];

            try
            {
                UserKeyWords kw;

                if (Enum.TryParse(strategyName, true, out kw))
                {
                    if (kw == UserKeyWords.All)
                        _dataManager.MappedStrategies.ForEach(kvp =>
                        {
                            kvp.Value.WhenStarted()
                                .Do(() => NewAnswer($"{kvp.Key} strategy started."))

                                .Apply(kvp.Value);
                            kvp.Value.WhenStopped()
                                .Do(() => NewAnswer($"{kvp.Key} strategy stopped, pos: {kvp.Value.Position}"))
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

                        soughtStrategy.WhenStopped()
                            .Do(() => NewAnswer($"{soughtStrategy} strategy stopped, pos: {soughtStrategy.Position}"))
                            .Apply(soughtStrategy);

                        soughtStrategy.Start();
                    }
                    else
                    {
                        OnNewAnswer("please choose correct strategy name from following: "
                            + _dataManager.MappedStrategies.Select(kvp => kvp.Key + Environment.NewLine));
                    }
                }
            }
            catch (Exception e1)
            {
                OnNewAnswer($"unknown exception: {e1.Message}");
            }
        }

        private void DoCreateCmd(string[] userParams)
        {
            if (userParams.Length < 2)
            {
                OnNewAnswer("please enter type and name of created strategy.");
                return;
            }

            StrategyTypes param1;
            string param2 = userParams[1];

            if (!Enum.TryParse(userParams[0], true, out param1))
            {
                OnNewAnswer("cannot create such a strategy.");
                return;
            }

            if (_dataManager.MappedStrategies.ContainsKey(param2))
            {
                OnNewAnswer("strategy with such name has already exist.");
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
                            "-USE spaces like a separator in array []");
                var strategy = strategyMaker.CreateStrategyFromString(param1, Console.ReadLine());
                _dataManager.MappedStrategies.Add(param2, strategy);
            }
            catch (ArgumentException e1)
            {
                OnNewAnswer(e1.Message);
            }
            catch (Exception e2)
            {
                OnNewAnswer($"unknown exception: {e2.Message}");
            }
        }

        private void DoConnectCmd()
        {
            _connector.Connected += () =>
            {
                OnNewAnswer("connected (success)");
                _dataManager = new DataManager(_connector);
            };

            _connector?.Connect();
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

        private void OnNewAnswer(string msg)
        {
            NewAnswer?.Invoke($"{DateTime.Now}: {msg}");
        }


    }
}
