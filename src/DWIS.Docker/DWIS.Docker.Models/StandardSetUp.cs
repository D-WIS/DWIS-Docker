namespace DWIS.Docker.Models
{
    public class StandardSetUp
    {
        //public bool DuplicationEnabled { get; set; } = true;
        //public string HubGroup { get; set; } = "default";
        public List<StandardSetUpItem> Items { get; set; }
        public StandardSetUp()
        {
            Items = StandardSetUpItem.GetStandardSetUp();
        }
    }
}
