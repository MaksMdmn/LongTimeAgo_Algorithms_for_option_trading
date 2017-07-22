using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using Ecng.Common;
using OptionsThugsConsole.entities;
using OptionsThugsConsole.enums;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.Quik;
using Trading.Strategies;

namespace OptionsThugsConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            IConnector connector = new QuikTrader()
            {
                LuaFixServerAddress = "127.0.0.1:5001".To<EndPoint>(),
                LuaLogin = "quik",
                LuaPassword = "quik".To<SecureString>()
            };

            ConfigManager configManager = ConfigManager.GetInstance();
            CommandHandler handler = CommandHandler.GetInstance(connector);

            configManager.SetOutput(Console.WriteLine);
            handler.SetOutput(Console.WriteLine);

            configManager.PrintAllSettings();

            while (true)
            {
                var userMessage = Console.ReadLine()?.ToLower();

                handler.ParseUserMessage(userMessage);

                if (userMessage.CompareIgnoreCase(UserCommands.Dconn.ToString()))
                    break;
            }

            var counter = 10;
            while (connector.ConnectionState == ConnectionStates.Connected
                && counter > 0)
            {
                Console.WriteLine("Trying to close safety for {0} sec.", counter);
                Thread.Sleep(1000);
                counter--;
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadLine();

        }
    }
}
