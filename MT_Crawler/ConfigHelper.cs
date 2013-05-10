using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Reflection;
using System.IO;

namespace Microsoft.Advertising.Analytics.SharedService
{
    public class ConfigHelper
    {
        Configuration configuration;

        public ConfigHelper(Type t)
        {
            Assembly a = Assembly.GetAssembly(t);
            string configFile = a.Location + ".config";

            if (!File.Exists(configFile))
            {
                // try to get the config from the codebase path.
                Uri codeBase = new Uri(a.CodeBase);
                configFile = codeBase.LocalPath + ".config";
            }

            this.Init(configFile);
        }
        public ConfigHelper(string configFile)
        {
            this.Init(configFile);
        }

        private void Init(string configFile)
        {
            if (!File.Exists(configFile))
            {
                throw new FileNotFoundException(string.Empty, configFile);
            }

            ExeConfigurationFileMap filemap = new ExeConfigurationFileMap();
            filemap.ExeConfigFilename = configFile;
            configuration = ConfigurationManager.OpenMappedExeConfiguration(filemap, ConfigurationUserLevel.None);
        }

        public string ConfigFile
        {
            get { return configuration.FilePath; }
        }

        public T GetConfigValue<T>(string keyName)
        {
            KeyValueConfigurationElement element = configuration.AppSettings.Settings[keyName];
            if (element == null)
            {
                throw new ApplicationException("Configuration file do not have key " + keyName);
            }

            try
            {
                T t = (T)Convert.ChangeType(element.Value, typeof(T));
                return t;
            }
            catch
            {
                throw new InvalidCastException("Cannot convert Key " + keyName + " Value " + element.Value + " to " + typeof(T).ToString());
            }
        }
    }
}
