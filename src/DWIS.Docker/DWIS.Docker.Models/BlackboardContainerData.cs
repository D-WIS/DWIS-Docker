namespace DWIS.Docker.Models
{
    public class BlackboardContainerData
    {
        public string ContainerName { get; set; }
        public string ContainerID { get; set; }
        public string ContainerGroup { get; set; }
        public string ContainerPort { get; set; }

        public bool ContainerStarted { get; set; }

        public int GetPortNumber()
        {
            if (int.TryParse(ContainerPort, out int port)) { return port; }
            else { return -1; }
        }

        //public void UpdateStartedStatus()

    }
}
