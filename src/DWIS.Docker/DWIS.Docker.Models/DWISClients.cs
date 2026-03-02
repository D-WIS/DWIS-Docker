using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Docker.Models
{
    public class DWISClients
    {
        public List<DWISApp> Apps = new List<DWISApp>();
        public DWISClients() 
        {
            Apps.Add(new DWISApp() { AppName = "Log view", ImageNameNoTag = "digiwells/dwis-logview", ImageTag = "stable", HostPort = "5276", ContainerPort = "8080" });
            Apps.Add(new DWISApp() {AppName = "Driller scheduling", ImageNameNoTag = "digiwells/driller-scheduling-app", ImageTag = "stable" ,HostPort = "5274", ContainerPort="8080" });
            Apps.Add(new DWISApp() { AppName = "Advisor scheduling", ImageNameNoTag = "digiwells/advisor-scheduling-app", ImageTag = "stable", HostPort = "5275", ContainerPort = "8080" });
            Apps.Add(new DWISApp() {AppName = "Blackboard explorer",ImageNameNoTag = "digiwells/dwis-blackboard-explorer", ImageTag = "stable", HostPort = "5277", ContainerPort = "8080" });
        }
    }

}
