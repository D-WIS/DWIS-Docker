using DWIS.Docker.Constants;
using DWIS.Docker.Models;

namespace DWIS.Docker.Clients
{
    public class DWISModulesConfigurationClient
    {




        public DWIS.AdviceComposer.Service.Configuration? GetComposerConfigFromFile()
        {
            string filePath = Path.Combine(ImageNames.COMPOSER_WINDOWS_LOCALPATH, ImageNames.COMPOSER_CONFIGFILENAME);

            if (!File.Exists(filePath))
            {
                return null;
            }
            else
            {
                string json = System.IO.File.ReadAllText(filePath);
                var config = System.Text.Json.JsonSerializer.Deserialize<DWIS.AdviceComposer.Service.Configuration>(json);
                return config;
            }
        }

        public ConfigType? GetConfigurationFromFile<ConfigType>(string localPath, string fileName) where ConfigType : class
        {
            string filePath = Path.Combine(localPath, fileName);
            if (!File.Exists(filePath))
            {
                return null;
            }
            else
            {
                string json = System.IO.File.ReadAllText(filePath);
                var config = System.Text.Json.JsonSerializer.Deserialize<ConfigType>(json);
                return config;
            }
        }


        public void SaveComposerConfigToFile(string config)
        {
            if (!Directory.Exists(ImageNames.COMPOSER_WINDOWS_LOCALPATH))
            {
                Directory.CreateDirectory(ImageNames.COMPOSER_WINDOWS_LOCALPATH);
            }
            System.IO.File.WriteAllText(Path.Combine(ImageNames.COMPOSER_WINDOWS_LOCALPATH, ImageNames.COMPOSER_CONFIGFILENAME), config);
        }

        public void SaveConfigToFile(string config, string localPath, string fileName)
        {
            if (!Directory.Exists(localPath))
            {
                Directory.CreateDirectory(localPath);
            }
            if(!File.Exists(Path.Combine(localPath, fileName)))
            {
                System.IO.File.WriteAllText(Path.Combine(localPath, fileName), config);
            }
        }

        public void SaveStandardConfigToFile(StandardSetUpItem item)
        {
            if(item.ConfigurationRequired)
                SaveConfigToFile(item.DefaultConfigContent, item.ConfigLocalPath, item.ConfigFileName);
        }
    }
}
