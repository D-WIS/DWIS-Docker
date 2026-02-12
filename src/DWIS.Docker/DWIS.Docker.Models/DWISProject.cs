namespace DWIS.Docker.Models
{
    public class DWISProject : DWISProjectBase
    {
        public StandardSetUpStatus? Status { get; set; }

        public DWISClients Clients { get; set; } = new DWISClients();


        public static DWISProject LoadFromBase(string path = "project.json")
        {
            if (System.IO.File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var baseP = System.Text.Json.JsonSerializer.Deserialize<DWISProjectBase>(json);
                if (baseP != null)
                {
                    DWISProject project = new DWISProject() { BlackBoardHostIP = baseP.BlackBoardHostIP, HubGroup = baseP.HubGroup, ReplicationEnabled = baseP.ReplicationEnabled };
                    return project;
                }
            }
            return new DWISProject();
        }

        public static void SaveBase(DWISProject project, string path = "project.json")
        {
            var baseP = new DWISProjectBase() { BlackBoardHostIP = project.BlackBoardHostIP, HubGroup = project.HubGroup, ReplicationEnabled = project.ReplicationEnabled };
            var json = System.Text.Json.JsonSerializer.Serialize(baseP);
            System.IO.File.WriteAllText(path, json);
        }
    }

    public class DWISProjectBase
    {
        public bool ReplicationEnabled { get; set; } = true;
        public string HubGroup { get; set; } = "default";
        public string BlackBoardHostIP { get; set; } = "host.docker.internal";
    }
}
