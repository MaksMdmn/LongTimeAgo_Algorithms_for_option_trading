using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Ecng.Common;
using OptionsThugsConsole.enums;
using StockSharp.Messages;

namespace OptionsThugsConsole.entities
{
    public class UserPosition
    {
        private static readonly string PathToXmlFile =
            AppConfigManager.GetInstance().GetSettingValue(UserConfigs.XmlPath.ToString())
                .CompareIgnoreCase("root")
                    ? Directory.GetCurrentDirectory() + "\\pos.xml"
                    : AppConfigManager.GetInstance().GetSettingValue(UserConfigs.XmlPath.ToString());

        public string SecCode { get; set; }
        public string CreatedTime { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Money { get; set; }

        public UserPosition()
        {
        }

        public UserPosition(string secCode)
        {
            SecCode = secCode;
            CreatedTime = $"{DateTime.Now}";
            Quantity = 0M;
            Price = 0M;
            Money = 0M;
        }

        public static List<UserPosition> LoadFromXml()
        {
            List<UserPosition> result;
            var serializer = new XmlSerializer(typeof(List<UserPosition>));

            if (!File.Exists(PathToXmlFile))
            {
                SaveToXml(new List<UserPosition>());
                return null;
            }

            using (var reader = new StreamReader(PathToXmlFile))
            {
                result = (List<UserPosition>)serializer.Deserialize(reader);
            }

            return result;
        }

        public static void SaveToXml(List<UserPosition> userPositions)
        {
            var serializer = new XmlSerializer(typeof(List<UserPosition>));

            using (var writer = new StreamWriter(PathToXmlFile))
            {
                serializer.Serialize(writer, userPositions);
            }
        }

        public void AddNewDeal(Sides side, decimal price, decimal size)
        {
            var tempSize = side == Sides.Sell ? size * -1 : size;

            Quantity += tempSize;
            Money += tempSize * price * -1;

            Price = Quantity == 0 ? 0 : Math.Round(Money / Quantity * -1, 4);

            CreatedTime = $"{DateTime.Now}";
        }

        public bool IsPositionClosed()
        {
            return Quantity == 0;
        }

        public bool IsPositionClosed(out decimal pnl)
        {
            pnl = Money;
            return IsPositionClosed();
        }

    }
}