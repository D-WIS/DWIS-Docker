namespace DWIS.Container.Model
{
    public class DWISContainer
    {
        public string Image { get; set; }
        public string ImageID { get; set; }
        public string Name { get; set; }    
        public string ID { get; set; }
        public bool Started { get; set; }
    }

    public class BlackBoardContainer : DWISContainer
    { 
    public bool UseHub { get; set; }
        public string HubUrl { get; set; }
        public string HubGroup { get; set; }
        public int Port { get; set; }
    }
}
