using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Docker.Constants
{
    public static class ImageNames
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

        public const string COMPOSER_LINUX_LOCALPATH = "home/Volumes/DWISAdviceComposerService";
        public const string COMPOSER_WINDOWS_LOCALPATH = @"C:\Volumes\DWISAdviceComposerService";
        public const string COMPOSER_CONTAINERPATH = @"/home";
        public const string COMPOSER_CONFIGFILENAME = "config.json";


        public const string MICROSTATE_INTERPRETATION_ENGINE_LINUX_LOCALPATH = "home/Volumes/DWISMicrostateInterpretationEngineService";
        public const string MICROSTATE_INTERPRETATION_ENGINE_WINDOWS_LOCALPATH = @"C:/Volumes//DWISMicrostateInterpretationEngineService";
        public const string MICROSTATE_INTERPRETATION_ENGINE_CONFIGFILENAME = "config.json";
        public const string MICROSTATE_INTERPRETATION_ENGINE_CONTAINERPATH = @"/home";
        public const string MICROSTATE_GENERATOR_LINUX_LOCALPATH = "home/Volumes/DWISMicroStateSignalGenerator";
        public const string MICROSTATE_GENERATOR_WINDOWS_LOCALPATH = @"C:/Volumes//DWISMicroStateSignalGenerator";
        public const string MICROSTATE_GENERATOR_CONFIGFILENAME = "config.json";
        public const string MICROSTATE_GENERATOR_CONTAINERPATH = @"/home";
    }
}
