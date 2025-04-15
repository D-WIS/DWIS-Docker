using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Docker.Constants
{
    public static class ImageNames
    {
        public const string BLACKBOARD = "digiwells/ddhubserver:latest";
        public const string MICROSTATES_THRESHOLDS = "digiwells/dwismicrostatethresholdsserver:stable";
        public const string MICROSTATES_INTERPRETATION_ENGINE = "digiwells/dwismicrostateinterpretationengine:stable";
        public const string SCHEDULER = "digiwells/dwisschedulerservice:stable";
        public const string ADVICE_COMPOSER = "digiwells/dwisadvicecomposerservice:stable";
    }
}
