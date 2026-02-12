namespace DWIS.Docker.Models
{
    public class DWISProject 
    {
        public bool ReplicationEnabled { get; set; } = true;
        public string HubGroup { get; set; } = "default";
        //public string BlackBoardHostIP { get; set; }
        public StandardSetUpStatus? Status { get; set; }

        public DWISClients Clients { get; set; } = new DWISClients();
    }
}
