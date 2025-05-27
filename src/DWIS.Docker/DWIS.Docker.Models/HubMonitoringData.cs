namespace DWIS.Docker.Models
{
    public class HubMonitoringData 
    {
        public string HubURL { get; set; }
        public Dictionary<string, HubGroupData> HubGroups { get; set; } = new Dictionary<string, HubGroupData>();
    }
}
