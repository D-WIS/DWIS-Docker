namespace DWIS.Docker.Models
{
    public class HubGroupData
    {
        public string GroupName { get; set; }
        public int NumberOfConnections { get; set; }
        public List<BlackboardContainerData> BlackboardContainers { get; set; } = new List<BlackboardContainerData>();

        public (int containerCount, int runningContainers) GetContainerCount()
        {
            int containerCount = BlackboardContainers.Count;
            int runningContainers = BlackboardContainers.Count(c => c.ContainerStarted);
            return (containerCount, runningContainers);
        }

       

    }
}
