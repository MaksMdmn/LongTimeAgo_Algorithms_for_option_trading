using System;
using System.Net;
using System.Security;
using System.Threading;
using Ecng.Common;
using OptionsThugsConsole.entities;
using OptionsThugsConsole.enums;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.Quik;

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

            AppConfigManager configManager = AppConfigManager.GetInstance();
            CommandParser parser = CommandParser.GetInstance(connector);

            configManager.NewAnswer += Console.WriteLine;
            parser.NewAnswer += Console.WriteLine;

            Console.WriteLine(configManager.GetAllSettings());

            while (true)
            {
                var userMessage = Console.ReadLine()?.ToLower();

                parser.ParseUserMessage(userMessage);

                if (userMessage.CompareIgnoreCase(UserCommands.Dconn.ToString()))
                    break;
            }

            var counter = 10;
            while (connector.ConnectionState == ConnectionStates.Connected
                || counter <= 0)
            {
                Console.WriteLine("Trying to close safety for {0} sec.", counter);
                Thread.Sleep(1000);
                counter--;
            }

            Console.WriteLine(counter > 0 ? "connection closed safety." : "connection closed forcibly");
            Console.WriteLine("Press any key to exit");
            Console.ReadLine();

        }
    }
}
