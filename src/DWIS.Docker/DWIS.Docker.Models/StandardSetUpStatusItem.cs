namespace DWIS.Docker.Models
{
    public class StandardSetUpStatusItem
    {
        public StandardSetUpItem SetUpItem { get; set; }
        public string ContainerName { get; set; }
        public bool Started { get; set; }
        public string ContainerID { get; set; }
        public bool ConfigurationExists { get; set; }

        public ImageStatus CurrentImageStatus { get; set; } = ImageStatus.Unknown;

        public enum ImageStatus 
        {
            Unknown,
            Outdated,
            Updated
        }


        public string ConfigurationStatus() 
        {
        if(SetUpItem != null && SetUpItem.ConfigurationRequired)
            {
                if (ConfigurationExists)
                    return "Exists";
                else
                    return "Missing";
            }
        else
            {
                return "Not required";
            }
        }
    }
}
