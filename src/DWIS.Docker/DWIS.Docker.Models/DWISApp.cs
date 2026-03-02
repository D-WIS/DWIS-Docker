namespace DWIS.Docker.Models
{
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

}
