using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Text;
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
            List<UserPosition> _userPositions = UserPosition.LoadFromXml();

            _userPositions[0].AddNewDeal(Sides.Buy, 1.32M, -13);

            UserPosition.SaveToXml(_userPositions);

            Console.ReadLine();
            //UserPosition tempPosition = new UserPosition("testcode");   BR52BE7
            //tempPosition.Money = 35M;


            //UserPosition tempPosition1 = new UserPosition("testcod22e");
            //tempPosition1.Money = 665M;


            //UserPosition.SaveToXml(new List<UserPosition>()
            //{
            //   tempPosition1, tempPosition
            //});



            IConnector connector = new QuikTrader()
            {
                LuaFixServerAddress = "127.0.0.1:5001".To<EndPoint>(),
                LuaLogin = "quik",
                LuaPassword = "quik".To<SecureString>()
            };

            AppConfigManager configManager = AppConfigManager.GetInstance();
            CommandHandler handler = CommandHandler.GetInstance(connector);

            configManager.NewAnswer += Console.WriteLine;
            handler.NewAnswer += Console.WriteLine;

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
                || counter <= 0)
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
