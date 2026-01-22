using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Docker.Constants
{
    public static class Names
    {
        public const string LATEST_TAG = "latest";
        public const string STABLE_TAG = "stable";

        public const string BLACKBOARD = "digiwells/ddhubserver:latest";
        public const string BLACKBOARD_NOTAG = "digiwells/ddhubserver";
        public const string MICROSTATES_THRESHOLDS = "digiwells/dwismicrostatethresholdsserver:stable";
        public const string MICROSTATES_THRESHOLDS_NOTAG = "digiwells/dwismicrostatethresholdsserver";
        public const string MICROSTATES_INTERPRETATION_ENGINE = "digiwells/dwismicrostateinterpretationengine:stable";
        public const string MICROSTATES_INTERPRETATION_ENGINE_NOTAG = "digiwells/dwismicrostateinterpretationengine";
        public const string SCHEDULER = "digiwells/dwisschedulerservice-hsm:stable";
        public const string SCHEDULER_NOTAG = "digiwells/dwisschedulerservice-hsm";
        public const string ADVICE_COMPOSER = "digiwells/dwisadvicecomposerservice:stable";
        public const string ADVICE_COMPOSER_NOTAG = "digiwells/dwisadvicecomposerservice";

        private const string COMPOSER_LINUX_LOCALPATH = "home/Volumes/DWISAdviceComposerService";
        private const string COMPOSER_WINDOWS_LOCALPATH = @"C:\Volumes\DWISAdviceComposerService";

        public static string COMPOSER_LOCALPATH => Environment.OSVersion.Platform == PlatformID.Unix ?
    COMPOSER_LINUX_LOCALPATH :
    COMPOSER_WINDOWS_LOCALPATH;


        public const string COMPOSER_CONTAINERPATH = @"/home";
        public const string COMPOSER_CONFIGFILENAME = "config.json";


        private const string MICROSTATE_INTERPRETATION_ENGINE_LINUX_LOCALPATH = "home/Volumes/DWISMicrostateInterpretationEngineService";
        private const string MICROSTATE_INTERPRETATION_ENGINE_WINDOWS_LOCALPATH = @"C:/Volumes/DWISMicrostateInterpretationEngineService";
        public const string MICROSTATE_INTERPRETATION_ENGINE_CONFIGFILENAME = "config.json";
        public const string MICROSTATE_INTERPRETATION_ENGINE_CONTAINERPATH = @"/home";

        public static string MICROSTATE_INTERPRETATION_ENGINE_LOCALPATH => Environment.OSVersion.Platform == PlatformID.Unix ?
MICROSTATE_INTERPRETATION_ENGINE_LINUX_LOCALPATH :
MICROSTATE_INTERPRETATION_ENGINE_WINDOWS_LOCALPATH;


        private const string MICROSTATE_GENERATOR_LINUX_LOCALPATH = "home/Volumes/DWISMicroStateSignalGenerator";
        private const string MICROSTATE_GENERATOR_WINDOWS_LOCALPATH = @"C:/Volumes/DWISMicroStateSignalGenerator";
        public const string MICROSTATE_GENERATOR_CONFIGFILENAME = "config.json";
        public const string MICROSTATE_GENERATOR_CONTAINERPATH = @"/home";

        public static string MICROSTATE_GENERATOR_LOCALPATH => Environment.OSVersion.Platform == PlatformID.Unix ?
MICROSTATE_GENERATOR_LINUX_LOCALPATH :
MICROSTATE_GENERATOR_WINDOWS_LOCALPATH;


        public const string SCHEDULER_CONTAINER_NAME = "schedulerservice";
        public const string ADVICE_COMPOSER_CONTAINER_NAME = "advicecomposer";
        public const string MICROSTATE_INTERPRETATION_ENGINE_CONTAINER_NAME = "microstatesengine";
        public const string MICROSTATE_THRESHOLDS_CONTAINER_NAME = "microstatethresholds";
        public const string BLACKBOARD_CONTAINER_NAME = "blackboard";
        public const string LOCAL_BLACKBOARD_CONTAINER_NAME = "local-blackboard";



        public static string LOCALPATH => Environment.OSVersion.Platform == PlatformID.Unix ?
            COMPOSER_LINUX_LOCALPATH :
            COMPOSER_WINDOWS_LOCALPATH;


        public static  (string moduleName, string containerName, string imageName, string tag, string localFolder, string configFileName, string containerConfigPath, Type? configType)[] BridgeData =
{
       ( "Auto-driller","adcsgenericautodriller", "digiwells/dwisadcsbridgegenericautodriller", "stable", GetLocalPath("DWISADCSBridgeGenericAutoDriller"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.AutoDriller.Configuration)),
        ("Circulation startup", "adcsgenericcirculationstartup", "digiwells/dwisadcsbridgegenericcirculationstartup", "stable", GetLocalPath("DWISADCSBridgeGenericCirculationStartup"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.CirculationStartup.Configuration)),
        ("Circulation stop", "adcsgenericcirculationstop", "digiwells/dwisadcsbridgegenericcirculationstop", "stable",GetLocalPath("DWISADCSBridgeGenericCirculationStop"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.CirculationStop.Configuration)),
        ("Friction test", "adcsgenericfrictiontest", "digiwells/dwisadcsbridgegenericfrictiontest", "stable", GetLocalPath("DWISADCSBridgeGenericFrictionTest"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.FrictionTest.Configuration)),
        ("Max flowrate SOE", "adcsgenericmaxflowratesoe", "digiwells/dwisadcsbridgegenericmaxflowratesoe", "stable",GetLocalPath("DWISADCSBridgeGenericMaxFlowrateSOE"), "", @"/home", null),
        ("Over-pressure FDIR", "adcsgenericoverpressurefdir", "digiwells/dwisadcsbridgegenericoverpressurefdir", "stable", GetLocalPath("DWISADCSBridgeGenericOverPressureFDIR"), "", @"/home", null),
        ("Over torque FDIR", "adcsgenericovertorquefdir", "digiwells/dwisadcsbridgegenericovertorquefdir", "stable", GetLocalPath("DWISADCSBridgeGenericOverTorqueFDIR"), "", @"/home", null),
        ("Overpull underpull FDIR", "adcsgenericoverpullunderpullfdir", "digiwells/dwisadcsbridgegenericoverpullunderpullfdir", "stable",GetLocalPath("DWISADCSBridgeGenericOverpullUnderpullFDIR"), "", @"/home", null),
        ("Pick Off-bottom", "adcsgenericpickoffbottom", "digiwells/dwisadcsbridgegenericpickoffbottom", "stable", GetLocalPath("DWISADCSBridgeGenericPickOffBottom"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.PickOffBottom.Configuration)),
        ("Reciprocation", "adcsgenericreciprocation", "digiwells/dwisadcsbridgegenericreciprocation", "stable", GetLocalPath("DWISADCSBridgeGenericReciprocation"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.Reciprocation.Configuration)),
        ("Rotation startup", "adcsgenericrotationstartup", "digiwells/dwisadcsbridgegenericrotationstartup", "stable", GetLocalPath("DWISADCSBridgeGenericRotationStartup"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.RotationStartup.Configuration)),
        ("Rotation stop", "adcsgenericrotationstop", "digiwells/dwisadcsbridgegenericrotationstop", "stable", GetLocalPath("DWISADCSBridgeGenericRotationStop"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.RotationStop.Configuration)),
        ("Swab-surge SOE", "adcsgenericswabsurgesoe", "digiwells/dwisadcsbridgegenericswabsurgesoe", "stable",GetLocalPath("DWISADCSBridgeGenericSwabSurgeSOE"), "", @"/home", null),
        ("Tag bottom" ,"adcsgenerictagbottom", "digiwells/dwisadcsbridgegenerictagbottom", "stable", GetLocalPath("DWISADCSBridgeGenericTagBottom"), "config.json", @"/home", typeof(DWIS.ADCSBridge.Generic.TagBottom.Configuration))
    };

        private static string GetLocalPath(string folderName)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                return $"/home/Volumes/{folderName}";
            }
            else
            {
                return $@"C:\Volumes\{folderName}";
            }
        }


    }
}
