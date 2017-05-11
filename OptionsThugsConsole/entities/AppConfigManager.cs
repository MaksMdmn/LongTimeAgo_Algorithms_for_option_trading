using System;
using System.Configuration;
using System.Text;

namespace OptionsThugsConsole.entities
{
    public class AppConfigManager
    {
        public static string ArrConfigSeparator = ",";
        public event Action<string> NewAnswer;
        public event Action<string> SettingChanged;

        private static AppConfigManager Instance;

        private AppConfigManager()
        {
        }

        public static AppConfigManager GetInstance()
        {
            return Instance ?? (Instance = new AppConfigManager());
        }

        public void UpdateConfigFile(string name, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[name] == null)
                {
                    settings.Add(name, value);
                    OnNewAnswer("value added");
                }
                else
                {
                    settings[name].Value = value;
                    OnNewAnswer("value updated");
                }
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                configFile.Save(ConfigurationSaveMode.Modified);

                SettingChanged?.Invoke(name);
            }
            catch (ConfigurationErrorsException e1)
            {
                OnNewAnswer("cannot update/write setting " + e1.Message, ConsoleColor.Red);
            }
        }

        public void RemoveFromConfigFile(string name)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;

                settings.Remove(name);
                OnNewAnswer("value removed");

                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException e1)
            {
                OnNewAnswer("cannot remove setting " + e1.Message, ConsoleColor.Red);
            }
        }

        public string GetSettingValue(string name)
        {
            try
            {
                return ConfigurationManager.AppSettings[name];
            }
            catch (ConfigurationErrorsException e1)
            {
                OnNewAnswer("cannot get setting " + e1.Message, ConsoleColor.Red);
            }

            throw new NullReferenceException("such setting does not exist. Check config file.");
        }

        public string GetAllSettings()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string appSetting in ConfigurationManager.AppSettings)
            {
                sb.AppendLine(appSetting + ": " + ConfigurationManager.AppSettings[appSetting]);
            }

            return sb.ToString();
        }

        public void PrintAllSettings()
        {
            OnNewAnswer("", ConsoleColor.Yellow);
            OnNewAnswer("---  current settings  ---", ConsoleColor.Yellow, false);
            OnNewAnswer("", ConsoleColor.Yellow,false);
            OnNewAnswer(GetAllSettings(), ConsoleColor.Yellow, false);
            OnNewAnswer("---  current settings  ---", ConsoleColor.Yellow, false);
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
