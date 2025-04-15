using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Docker.Constants
{
    public static class Commands
    {
        public static string GetBlackBoardDockerCommand(string containerName, bool useHub, string portNumber, string groupName="default")
        {
            return $"docker run  -dit --name {containerName} -P -p {portNumber}:{portNumber}/tcp --hostname localhost  {ImageNames.BLACKBOARD} --useHub {useHub} --hubURL https://dwis.digiwells.no/blackboard/applications --hubGroup {groupName} --port {portNumber}";
        }
    }
}
