using DWIS.Docker.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Docker.Models
{
    public class DWISProject 
    {
        public bool ReplicationEnabled { get; set; }
        public string HubGroup { get; set; } = "default";
        public string BlackBoardHostIP { get; set; }
        public StandardSetUpStatus? Status { get; set; }
    }



    public class StandardSetUpStatus
    {
        public List<StandardSetUpStatusItem> Items { get; set; } = new List<StandardSetUpStatusItem>();
    }
    public class StandardSetUpStatusItem
    {
        public StandardSetUpItem SetUpItem { get; set; }
        public string ContainerName { get; set; }
        public bool Started { get; set; }
        public string ContainerID { get; set; }
        public bool ConfigurationExists { get; set; }

        public ImageStatus CurrentImageStatus { get; set; } = ImageStatus.Unknown;

        public enum ImageStatus 
        {
            Unknown,
            Outdated,
            Updated
        }


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
        //public bool DuplicationEnabled { get; set; } = true;
        //public string HubGroup { get; set; } = "default";
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

        public string ModuleDisplayName { get; set; }
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
        public DateTime ImageRepoTimeStamp { get; set; } = DateTime.MinValue;

        public static List<StandardSetUpItem> GetStandardSetUp()
        {
            List<StandardSetUpItem> standardSetUpItems = new List<StandardSetUpItem>();

            //blackboards
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleDisplayName = "Default Blackboard",
                ModuleGroup = BlackBoardGroup,
                ImageName = DWIS.Docker.Constants.Names.BLACKBOARD_NOTAG,
                ImageTag = DWIS.Docker.Constants.Names.LATEST_TAG,
                DefaultContainerName = Names.BLACKBOARD_CONTAINER_NAME,
                ConfigurationRequired = false
            });

            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleDisplayName = "Local Blackboard",
                ModuleGroup = BlackBoardGroup,
                ImageName = DWIS.Docker.Constants.Names.BLACKBOARD_NOTAG,
                ImageTag = DWIS.Docker.Constants.Names.LATEST_TAG,
                DefaultContainerName = Names.LOCAL_BLACKBOARD_CONTAINER_NAME,
                BlackBoardPort = "48031",
                ConfigurationRequired = false
            });

            //scheduler
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleDisplayName = "Scheduler",
                ModuleGroup = SchedulerGroup,
                ImageName = DWIS.Docker.Constants.Names.SCHEDULER_NOTAG,
                ImageTag = DWIS.Docker.Constants.Names.STABLE_TAG,
                DefaultContainerName = Names.SCHEDULER_CONTAINER_NAME,
                ConfigurationRequired = false
            });
            //microstates
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleDisplayName = "Microstate engine",
                ModuleGroup = MicrostateGroup,
                ImageName = DWIS.Docker.Constants.Names.MICROSTATES_INTERPRETATION_ENGINE_NOTAG,
                ImageTag = DWIS.Docker.Constants.Names.STABLE_TAG,
                DefaultContainerName = Names.MICROSTATE_INTERPRETATION_ENGINE_CONTAINER_NAME,
                ConfigFileName = Names.MICROSTATE_INTERPRETATION_ENGINE_CONFIGFILENAME,
                ConfigLocalPath = Names.MICROSTATE_INTERPRETATION_ENGINE_LOCALPATH,
                ConfigContainerPath = Names.MICROSTATE_INTERPRETATION_ENGINE_CONTAINERPATH,
                ConfigurationRequired = true,
                DefaultConfigContent = System.Text.Json.JsonSerializer.Serialize(
                    new DWIS.MicroState.InterpretationEngine.Configuration(),
                    new System.Text.Json.JsonSerializerOptions() { WriteIndented = true })
            });
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleDisplayName = "Microstate thresholds",
                ModuleGroup = MicrostateGroup,
                ImageName = DWIS.Docker.Constants.Names.MICROSTATES_THRESHOLDS_NOTAG,
                ImageTag = DWIS.Docker.Constants.Names.STABLE_TAG,
                DefaultContainerName =Names.MICROSTATE_THRESHOLDS_CONTAINER_NAME,
                ConfigFileName = Names.MICROSTATE_GENERATOR_CONFIGFILENAME,
                ConfigLocalPath = Names.MICROSTATE_GENERATOR_LOCALPATH,
                ConfigContainerPath = Names.MICROSTATE_GENERATOR_CONTAINERPATH,
                ConfigurationRequired = true,
                DefaultConfigContent = System.Text.Json.JsonSerializer.Serialize(
                    new DWIS.MicroState.ThresholdsServer.Configuration(),
                    new System.Text.Json.JsonSerializerOptions() { WriteIndented = true })
            });
            //composer
            standardSetUpItems.Add(new StandardSetUpItem
            {
                ModuleDisplayName = "Composer",
                ModuleGroup = ComposerGroup,
                ImageName = DWIS.Docker.Constants.Names.ADVICE_COMPOSER_NOTAG,
                ImageTag = DWIS.Docker.Constants.Names.STABLE_TAG,
                DefaultContainerName = Names.ADVICE_COMPOSER_CONTAINER_NAME,
                ConfigFileName = Names.COMPOSER_CONFIGFILENAME,
                ConfigLocalPath = Names.COMPOSER_LOCALPATH,
                ConfigContainerPath = Names.COMPOSER_CONTAINERPATH,
                ConfigurationRequired = true,
                DefaultConfigContent = System.Text.Json.JsonSerializer.Serialize(
                    new DWIS.AdviceComposer.Service.Configuration(),
                    new System.Text.Json.JsonSerializerOptions() { WriteIndented = true })
            });




            foreach(var bridge in Names.BridgeData)
            {
                standardSetUpItems.Add(new StandardSetUpItem
                {
                    ModuleDisplayName = bridge.moduleName,
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
