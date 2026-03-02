namespace DWIS.Docker.Models
{
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
