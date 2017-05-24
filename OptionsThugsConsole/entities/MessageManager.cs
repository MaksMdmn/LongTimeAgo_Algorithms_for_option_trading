using System;
using System.Linq;
using System.Text;
using System.Timers;
using Microsoft.Practices.ObjectBuilder2;
using OptionsThugsConsole.enums;

namespace OptionsThugsConsole.entities
{
    public class MessageManager
    {
        public event Action AutoMessage;

        private static MessageManager Instance;
        private readonly Timer _autoMsgTimer;

        private MessageManager()
        {
            _autoMsgTimer = new Timer();
            _autoMsgTimer.Elapsed += (sender, args) => OnAutoMessage();
        }

        public static MessageManager GetInstance()
        {
            return Instance ?? (Instance = new MessageManager());
        }

        public static string AlignString(string[] words, bool withNewLines = false, int newLineAfterWords = 1, string externalToken = "   ")
        {
            if (words.Length == 0)
                return null;

            var sb = new StringBuilder();
            var innerToken = " ";
            var maxLength = words.OrderByDescending(s => s.Length).First()?.Length;
            var wordsCounter = 0;

            if (maxLength > 0)
            {
                words.ForEach(word =>
                {
                    sb.Append(word);

                    for (int i = 0; i < maxLength - word.Length; i++)
                    {
                        sb.Append(innerToken);
                    }
                    sb.Append(externalToken);

                    if (withNewLines)
                    {
                        wordsCounter++;

                        if (wordsCounter % newLineAfterWords == 0)
                            sb.AppendLine();

                    }
                });
            }


            return sb.ToString();
        }

        public void EnableTimer()
        {
            var interval = Convert.ToDouble(AppConfigManager.GetInstance()
                               .GetSettingValue(UserConfigs.StatusTiming.ToString())) * 1000;

            _autoMsgTimer.Interval = interval;
            _autoMsgTimer.Enabled = !(Math.Abs(interval) < 0.0001);
        }

        public void DisableTimer()
        {
            _autoMsgTimer.Enabled = false;
        }

        public bool IsTimerEnabled()
        {
            return _autoMsgTimer.Enabled;
        }

        public UserCommands ParseUserCommand(string strCmd)
        {
            UserCommands result;

            if (!Enum.TryParse(strCmd, true, out result))
                throw new ArgumentException("cannot parse: " + strCmd);

            return result;
        }

        public string[] ParseUserArgs(string[] strArgs)
        {
            return strArgs.Where(val => val != strArgs[0]).ToArray();
        }

        public static decimal MsgGreeksRounding(decimal value, int extraRoundNumbers = 0)
        {
            return Math.Round(value, 4 + extraRoundNumbers);
        }

        public static decimal MsgVolaRounding(decimal value)
        {
            return Math.Round(value, 2);
        }

        public static decimal MsgPriceRounding(decimal value)
        {
            return Math.Round(value, 2);
        }

        private void OnAutoMessage()
        {
            AutoMessage?.Invoke();
        }
    }
}