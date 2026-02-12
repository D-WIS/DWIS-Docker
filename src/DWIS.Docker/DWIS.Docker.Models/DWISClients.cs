using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.Docker.Models
{
    public class DWISClients
    {
        public List<DWISClientApp> Apps = new List<DWISClientApp>();
        public DWISClients() 
        {
            Apps.Add(new DWISClientApp() {AppName = "Driller scheduling", ImageNameNoTag = "digiwells/driller-scheduling-app", ImageTag = "stable" ,HostPort = "5274", ContainerPort="8080" });
            Apps.Add(new DWISClientApp() { AppName = "Advisor scheduling", ImageNameNoTag = "digiwells/advisor-scheduling-app", ImageTag = "stable", HostPort = "5275", ContainerPort = "8080" });
        }
    }

    public class DWISClientApp 
    {
        public string AppName { get; set; }
        public string ImageNameNoTag { get; set; }
        public string ImageTag { get; set; }
        public string ContainerID { get; set; }
        public string HostPort { get; set; }
        public string ContainerPort { get; set; }
        public bool Running { get; set; }
    }
}
