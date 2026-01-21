using DWIS.Docker.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Docker.Models
{
    public class StandardSetUpStatus
    {
        public List<StandardSetUpStatusItem> Items { get; set; } = new List<StandardSetUpStatusItem>();
    }
    public class StandardSetUpStatusItem
    {
        public StandardSetUpItem SetUpItem { get; set; }
        public string ContainerName { get; set; }
        public bool Started { get; set; }
        public string ID { get; set; }
        public bool ConfigurationExists { get; set; }


        public string ConfigurationStatus() 
        {
        if(SetUpItem != null && SetUpItem.ConfigurationRequired)
            {
                if (ConfigurationExists)
                    return "Exists";
                else
                    return "Missing";
            }
        else
            {
                return "Not required";
            }
        }
    }


    public class StandardSetUp
    {
        public string HubGroup { get; set; } = "default";
        public List<StandardSetUpItem> Items { get; set; }
        public StandardSetUp()
        {
            Items = StandardSetUpItem.GetStandardSetUp();
        }
    }





    public class StandardSetUpItem
    {
        public const string BlackBoardGroup = "blackboards";
        public const string SchedulerGroup = "schedulers";
        public const string MicrostateGroup = "microstates";
        public const string ComposerGroup = "composers";
        public const string BridgeGroup = "bridges";



       // public List<(string containerName, string containerID, bool running)> RunningContainers { get; set; } = new List<(string containerName, string containerID, bool running)>();

        public string ModuleName { get; set; }
        public string ModuleGroup { get; set; }
        public string ImageName { get; set; }
        public string ImageTag { get; set; }
        public string DefaultContainerName { get; set; }
        public string ConfigFileName { get; set; }
        public string ConfigLocalPath { get; set; }
        public string ConfigContainerPath { get; set; }

        public string DefaultConfigContent { get; set; }
        public bool ConfigurationRequired { get; set; }

       // public bool Started => RunningContainers.Any(c => c.running);

        public string BlackBoardPort { get; set; } = "48030";

        public string ExtraArgs { get; set; }

        public static List<StandardSetUpItem> GetStandardSetUp()
        {
            List<StandardSetUpItem> standardSetUpItems = new List<StandardSetUpItem>();

            //blackboards
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleName = "Default Blackboard",
                ModuleGroup = BlackBoardGroup,
                ImageName = DWIS.Docker.Constants.ImageNames.BLACKBOARD_NOTAG,
                ImageTag = DWIS.Docker.Constants.ImageNames.LATEST_TAG,
                DefaultContainerName = "blackboard",
                ConfigurationRequired = false
            });

            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleName = "Local Blackboard",
                ModuleGroup = BlackBoardGroup,
                ImageName = DWIS.Docker.Constants.ImageNames.BLACKBOARD_NOTAG,
                ImageTag = DWIS.Docker.Constants.ImageNames.LATEST_TAG,
                DefaultContainerName = "local-blackboard",
                BlackBoardPort = "48031",
                ConfigurationRequired = false
            });

            //scheduler
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleName = "Scheduler",
                ModuleGroup = SchedulerGroup,
                ImageName = DWIS.Docker.Constants.ImageNames.SCHEDULER_NOTAG,
                ImageTag = DWIS.Docker.Constants.ImageNames.STABLE_TAG,
                DefaultContainerName = "dwis-scheduler",
                ConfigurationRequired = false
            });
            //microstates
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleName = "Microstate engine",
                ModuleGroup = MicrostateGroup,
                ImageName = DWIS.Docker.Constants.ImageNames.MICROSTATES_INTERPRETATION_ENGINE_NOTAG,
                ImageTag = DWIS.Docker.Constants.ImageNames.STABLE_TAG,
                DefaultContainerName = "dwis-microstate-engine",
                ConfigFileName = ImageNames.MICROSTATE_INTERPRETATION_ENGINE_CONFIGFILENAME,
                ConfigLocalPath = ImageNames.MICROSTATE_INTERPRETATION_ENGINE_WINDOWS_LOCALPATH,
                ConfigContainerPath = ImageNames.MICROSTATE_INTERPRETATION_ENGINE_CONTAINERPATH,
                ConfigurationRequired = true,
                DefaultConfigContent = System.Text.Json.JsonSerializer.Serialize(
                    new DWIS.MicroState.InterpretationEngine.Configuration(),
                    new System.Text.Json.JsonSerializerOptions() { WriteIndented = true })
            });
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleName = "Microstate thresholds",
                ModuleGroup = MicrostateGroup,
                ImageName = DWIS.Docker.Constants.ImageNames.MICROSTATES_THRESHOLDS_NOTAG,
                ImageTag = DWIS.Docker.Constants.ImageNames.STABLE_TAG,
                DefaultContainerName = "dwis-thresholds-engine",
                ConfigFileName = ImageNames.MICROSTATE_GENERATOR_CONFIGFILENAME,
                ConfigLocalPath = ImageNames.MICROSTATE_GENERATOR_WINDOWS_LOCALPATH,
                ConfigContainerPath = ImageNames.MICROSTATE_GENERATOR_CONTAINERPATH,
                ConfigurationRequired = true,
                DefaultConfigContent = System.Text.Json.JsonSerializer.Serialize(
                    new DWIS.MicroState.ThresholdsServer.Configuration(),
                    new System.Text.Json.JsonSerializerOptions() { WriteIndented = true })
            });
            //composer
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleName = "Composer",
                ModuleGroup = ComposerGroup,
                ImageName = DWIS.Docker.Constants.ImageNames.ADVICE_COMPOSER_NOTAG,
                ImageTag = DWIS.Docker.Constants.ImageNames.STABLE_TAG,
                DefaultContainerName = "dwis-composer",
                ConfigFileName = ImageNames.COMPOSER_CONFIGFILENAME,
                ConfigLocalPath = ImageNames.COMPOSER_WINDOWS_LOCALPATH,
                ConfigContainerPath = ImageNames.COMPOSER_CONTAINERPATH,
                ConfigurationRequired = true,
                DefaultConfigContent = System.Text.Json.JsonSerializer.Serialize(
                    new DWIS.AdviceComposer.Service.Configuration(),
                    new System.Text.Json.JsonSerializerOptions() { WriteIndented = true })
            });
        
            //bridges
            (string tabName, string containerName, string imageName, string tag, string localFolder, string configFileName, string containerConfigPath, Type? configType)[] _bridges =
{
       ( "Auto-driller","adcsgenericautodriller", "digiwells/dwisadcsbridgegenericautodriller", "stable", @"C:\Volumes\DWISADCSBridgeGenericAutoDriller", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.AutoDriller.Configuration)),
        ("Circulation startup", "adcsgenericcirculationstartup", "digiwells/dwisadcsbridgegenericcirculationstartup", "stable", @"C:\Volumes\DWISADCSBridgeGenericCirculationStartup", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.CirculationStartup.Configuration)),
        ("Circulation stop", "adcsgenericcirculationstop", "digiwells/dwisadcsbridgegenericcirculationstop", "stable", @"C:\Volumes\DWISADCSBridgeGenericCirculationStop", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.CirculationStop.Configuration)),
        ("Friction test", "adcsgenericfrictiontest", "digiwells/dwisadcsbridgegenericfrictiontest", "stable", @"C:\Volumes\DWISADCSBridgeGenericFrictionTest", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.FrictionTest.Configuration)),
        ("Max flowrate SOE", "adcsgenericmaxflowratesoe", "digiwells/dwisadcsbridgegenericmaxflowratesoe", "stable", @"C:\Volumes\DWISADCSBridgeGenericMaxFlowrateSOE", "", @"/home", null),
        ("Over-pressure FDIR", "adcsgenericoverpressurefdir", "digiwells/dwisadcsbridgegenericoverpressurefdir", "stable", @"C:\Volumes\DWISADCSBridgeGenericOverPressureFDIR", "", @"/home", null),
        ("Over torque FDIR", "adcsgenericovertorquefdir", "digiwells/dwisadcsbridgegenericovertorquefdir", "stable", @"C:\Volumes\DWISADCSBridgeGenericOverTorqueFDIR", "", @"/home", null),
        ("Overpull underpull FDIR", "adcsgenericoverpullunderpullfdir", "digiwells/dwisadcsbridgegenericoverpullunderpullfdir", "stable", @"C:\Volumes\DWISADCSBridgeGenericOverpullUnderpullFDIR", "", @"/home", null),
        ("Pick Off-bottom", "adcsgenericpickoffbottom", "digiwells/dwisadcsbridgegenericpickoffbottom", "stable", @"C:\Volumes\DWISADCSBridgeGenericPickOffBottom", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.PickOffBottom.Configuration)),
        ("Reciprocation", "adcsgenericreciprocation", "digiwells/dwisadcsbridgegenericreciprocation", "stable", @"C:\Volumes\DWISADCSBridgeGenericReciprocation", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.Reciprocation.Configuration)),
        ("Rotation startup", "adcsgenericrotationstartup", "digiwells/dwisadcsbridgegenericrotationstartup", "stable", @"C:\Volumes\DWISADCSBridgeGenericRotationStartup", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.RotationStartup.Configuration)),
        ("Rotation stop", "adcsgenericrotationstop", "digiwells/dwisadcsbridgegenericrotationstop", "stable", @"C:\Volumes\DWISADCSBridgeGenericRotationStop", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.RotationStop.Configuration)),
        ("Swab-surge SOE", "adcsgenericswabsurgesoe", "digiwells/dwisadcsbridgegenericswabsurgesoe", "stable", @"C:\Volumes\DWISADCSBridgeGenericSwabSurgeSOE", "", @"/home", null),
        ("Tag bottom" ,"adcsgenerictagbottom", "digiwells/dwisadcsbridgegenerictagbottom", "stable", @"C:\Volumes\DWISADCSBridgeGenericTagBottom", "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.TagBottom.Configuration))
    };


            foreach(var bridge in _bridges)
            {
                standardSetUpItems.Add(new StandardSetUpItem
                {
                    ModuleName = bridge.tabName,
                    ModuleGroup = BridgeGroup,
                    ImageName = bridge.imageName,
                    ImageTag = bridge.tag,
                    DefaultContainerName = bridge.containerName,
                    ConfigFileName = bridge.configFileName,
                    ConfigLocalPath = bridge.localFolder,
                    ConfigContainerPath = bridge.containerConfigPath,
                    ConfigurationRequired = bridge.configType != null,
                    DefaultConfigContent = bridge.configType != null ? System.Text.Json.JsonSerializer.Serialize(
                        Activator.CreateInstance(bridge.configType!),
                        new System.Text.Json.JsonSerializerOptions() { WriteIndented = true }) : ""
                });
            }




            return standardSetUpItems;
        }
    }
}
