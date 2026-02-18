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
        }
    }

    public class DWISApp 
    {
        public string AppName { get; set; }
        public string ImageNameNoTag { get; set; }
        public string ImageTag { get; set; }
        public string ContainerID { get; set; }
        public string HostPort { get; set; }
        public string ContainerPort { get; set; }
        public bool Running { get; set; }
    }


    public class DWISAdvisors 
    {
        public List<DWISApp> Apps = new List<DWISApp>();
        public DWISAdvisors()
        {
            Apps.Add(new DWISApp() { AppName = "WOB correction", ImageNameNoTag = "digiwells/dwisservicewobcorrectionsserver", ImageTag = "stable", HostPort = "", ContainerPort = "8080" });
            Apps.Add(new DWISApp() { AppName = "Downhole ecd service", ImageNameNoTag = "digiwells/dwisservicedownholeecdserver", ImageTag = "stable", HostPort = "", ContainerPort = "8080" });
            Apps.Add(new DWISApp() { AppName = "Drill-string", ImageNameNoTag = "digiwells/dwiscontextualdatabridgebhadrillstringserver", ImageTag = "stable", HostPort = "5278", ContainerPort = "8080" });
            Apps.Add(new DWISApp() { AppName = "Trajectory", ImageNameNoTag = "digiwells/dwiscontextualdatabridgetrajectoryserver", ImageTag = "stable", HostPort = "5279", ContainerPort = "8080" });
            Apps.Add(new DWISApp() { AppName = "Arhitecture", ImageNameNoTag = "digiwells/dwiscontextualdatabridgewellborearchitectureserver", ImageTag = "stable", HostPort = "5280", ContainerPort = "8080" });
            Apps.Add(new DWISApp() { AppName = "Wellbores", ImageNameNoTag = "digiwells/dwiscontextualdatawellboreselectorwebapp", ImageTag = "stable", HostPort = "5281", ContainerPort = "8080" });
            //Apps.Add(new DWISApp() { AppName = "Driller scheduling", ImageNameNoTag = "digiwells/dwisservicewobcorrectionsserver", ImageTag = "stable", HostPort = "5274", ContainerPort = "8080" });
            //Apps.Add(new DWISApp() { AppName = "Advisor scheduling", ImageNameNoTag = "digiwells/dwisservicedownholeecdserver", ImageTag = "stable", HostPort = "5275", ContainerPort = "8080" });
            //Apps.Add(new DWISApp() { AppName = "Driller scheduling", ImageNameNoTag = "digiwells/dwisservicewobcorrectionsserver", ImageTag = "stable", HostPort = "5274", ContainerPort = "8080" });
            //Apps.Add(new DWISApp() { AppName = "Advisor scheduling", ImageNameNoTag = "digiwells/dwisservicedownholeecdserver", ImageTag = "stable", HostPort = "5275", ContainerPort = "8080" });
        }
    }

}
