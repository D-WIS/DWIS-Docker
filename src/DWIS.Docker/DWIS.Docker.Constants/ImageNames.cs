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
    }
}
