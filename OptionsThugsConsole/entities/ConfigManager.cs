using System;
using System.Configuration;
using System.Text;

namespace OptionsThugsConsole.entities
{
    public class ConfigManager
    {
        public static string ArrConfigSeparator = ",";
        public event Action<string> SettingChanged;
        private readonly MessageManager _messageManager;

        private static ConfigManager Instance;
        

        private ConfigManager()
        {
            _messageManager = new MessageManager();
        }

        public static ConfigManager GetInstance()
        {
            return Instance ?? (Instance = new ConfigManager());
        }

        public void SetOutput(Action<string> outputMethod)
        {
            _messageManager.NewAnswer += outputMethod;
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
                    _messageManager.ProceedAnswer("value added");
                }
                else
                {
                    settings[name].Value = value;
                    _messageManager.ProceedAnswer("value updated");
                }
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                configFile.Save(ConfigurationSaveMode.Modified);

                SettingChanged?.Invoke(name);
            }
            catch (ConfigurationErrorsException e1)
            {
                _messageManager.ProceedAnswer("cannot update/write setting " + e1.Message, ConsoleColor.Red);
            }
        }

        public void RemoveFromConfigFile(string name)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;

                settings.Remove(name);
                _messageManager.ProceedAnswer("value removed");

                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException e1)
            {
                _messageManager.ProceedAnswer("cannot remove setting " + e1.Message, ConsoleColor.Red);
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
                _messageManager.ProceedAnswer("cannot get setting " + e1.Message, ConsoleColor.Red);
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
            _messageManager.ProceedAnswer("", ConsoleColor.Yellow);
            _messageManager.ProceedAnswer("---  current settings  ---", ConsoleColor.Yellow, false);
            _messageManager.ProceedAnswer("", ConsoleColor.Yellow,false);
            _messageManager.ProceedAnswer(GetAllSettings(), ConsoleColor.Yellow, false);
            _messageManager.ProceedAnswer("---  current settings  ---", ConsoleColor.Yellow, false);
        }
    }
}
