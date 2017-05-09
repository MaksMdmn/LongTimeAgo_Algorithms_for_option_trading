using System;
using System.Configuration;
using System.Text;

namespace OptionsThugsConsole.entities
{
    public class AppConfigManager
    {
        public static string ArrConfigSeparator = ",";
        public event Action<string> NewAnswer;

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
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException)
            {
                OnNewAnswer("Error writing app settings");
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
            catch (ConfigurationErrorsException)
            {
                OnNewAnswer("Error removing app settings");
            }
        }

        public string GetSettingValue(string name)
        {
            try
            {
                return ConfigurationManager.AppSettings[name];
            }
            catch (ConfigurationErrorsException)
            {
                OnNewAnswer("Error getting app setting");
            }

            throw new NullReferenceException("such setting does not exist. Check config file.");
        }

        public string GetAllSettings()
        {
            OnNewAnswer("current program setup:");
            StringBuilder sb = new StringBuilder();
            foreach (string appSetting in ConfigurationManager.AppSettings)
            {
                sb.AppendLine(appSetting + ": " + ConfigurationManager.AppSettings[appSetting]);
            }

            return sb.ToString();
        }


        private void OnNewAnswer(string msg)
        {
            NewAnswer?.Invoke($"{DateTime.Now}: {msg}");
        }

    }
}
