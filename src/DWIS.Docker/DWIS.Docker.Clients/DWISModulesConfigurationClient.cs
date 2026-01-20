using DWIS.Docker.Constants;

namespace DWIS.Docker.Clients
{
    public class DWISModulesConfigurationClient
    {




        public ComposerConfig? GetComposerConfigFromFile()
        {
            string filePath = Path.Combine(ImageNames.COMPOSER_WINDOWS_LOCALPATH, ImageNames.COMPOSER_CONFIGFILENAME);

            if (!File.Exists(filePath))
            {
                return null;
            }
            else
            {
                string json = System.IO.File.ReadAllText(filePath);
                var config = System.Text.Json.JsonSerializer.Deserialize<ComposerConfig>(json);
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
            System.IO.File.WriteAllText(Path.Combine(localPath, fileName), config);
        }



        public class ComposerConfig
        {
            public TimeSpan LoopDuration { get; set; } = TimeSpan.FromSeconds(1.0);
            public string? OPCUAURL { get; set; } = "opc.tcp://localhost:48030";
            public TimeSpan ControllerObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
            public TimeSpan ProcedureObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
            public TimeSpan FaultDetectionIsolationAndRecoveryObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
            public TimeSpan SafeOperatingEnvelopeObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
        }
    }
}
