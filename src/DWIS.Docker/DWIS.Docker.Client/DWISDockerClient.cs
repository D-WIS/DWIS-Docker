using Docker.DotNet;
using Docker.DotNet.Models;
using DWIS.Container.Model;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Xml.Linq;
namespace DWIS.Docker.Client
{
    public class DWISDockerClient
    {
        private DockerClient _client;
        private ILogger<DockerClient>? _logger;
        public DWISDockerClient(ILogger<DockerClient>? logger = null)
        {
            _logger = logger;
            _client = new DockerClientConfiguration().CreateClient();
        }

        public async Task<IList<BlackBoardContainer>?> GetBlackBoards()
        {
            List<BlackBoardContainer> containerData = new List<BlackBoardContainer>();
            var containers = await _client.Containers.ListContainersAsync(
                         new ContainersListParameters()
                         {
                             All = true
                         });
            if (containers != null && containers.Count > 0)
            {
                var myContainers = containers.Where(c => c.Image == Constants.ImageNames.BLACKBOARD);

                foreach (var container in myContainers)
                {
                    bool useHub = false;
                    int port = 48030;
                    string hubGroup = "default";
                    string hubURL = "https://dwis.digiwells.no/blackboard/applications";

                    string cmd = container.Command;
                    if (!string.IsNullOrEmpty(cmd))
                    {
                        var args = cmd.Split(' ');
                        if (args != null)
                        {
                            var argsList = args.ToList();
                            int idx = argsList.IndexOf("--useHub");
                            if (idx != -1)
                            {
                                useHub = idx == argsList.Count - 1 || argsList[idx + 1] != "false";
                            }
                            idx = argsList.IndexOf("--hubURL");
                            if (idx != -1 && idx < argsList.Count - 1)
                            {
                                hubURL = argsList[idx + 1];
                            }
                            idx = argsList.IndexOf("--hubGroup");
                            if (idx != -1 && idx < argsList.Count - 1)
                            {
                                hubGroup = argsList[idx + 1];
                            }
                            idx = argsList.IndexOf("--port");
                            if (idx != -1 && idx < argsList.Count - 1)
                            {
                                if (int.TryParse(argsList[idx + 1], out int temp)) 
                                {
                                    port = temp;
                                }
                            }

                            BlackBoardContainer blackBoard = new BlackBoardContainer();
                            blackBoard.ID = container.ID;
                            blackBoard.Name = container.Names.First();
                            blackBoard.Port = port;
                            blackBoard.HubGroup = hubGroup;
                            blackBoard.UseHub = useHub;
                            blackBoard.Started = container.State.ToLower() == "running";
                            containerData.Add(blackBoard);
                        }
                    }
                }
            }                            
            return containerData;
        }

        public async void CreateBlackboardContainer(string name = "", bool useHub = true, string hubGroup = "default", string port = "48030")
        {
            if (string.IsNullOrEmpty(name))
            {            
                name = "blackboard-";
                if (useHub)
                {
                    name += hubGroup;
                }
                else { name += "noHub"; }
                name += "-" + port;
            }

            var containers = await _client.Containers.ListContainersAsync(
                         new ContainersListParameters()
                         {
                             All = true
                         });
            if (containers?.Count > 0)
            {
                int count = 0;
                string baseName = name;
                while (containers.Any(c => c.Names.Contains(name)))
                {
                    name = baseName + "_" + count++;
                    _logger?.LogWarning($"Original name already in use, try {name} instead");
                }
            }

            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Name = name,
                Image = Constants.ImageNames.BLACKBOARD,
                Hostname = "localhost",
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                {port, default(EmptyStruct) }
                },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                {port, new List<PortBinding> {new PortBinding {HostPort = port}}}
            },
                    PublishAllPorts = true
                },
                Cmd = new List<string>() { "--useHub", useHub.ToString(), "--hubURL", "https://dwis.digiwells.no/blackboard/applications", "--hubGroup", hubGroup, "--port", port },
                Labels = new Dictionary<string, string>() { { "port", port }, { "group", hubGroup } }
            });
            
        }

        public async Task<bool> IsContainerNameInUse(string name)
        {
            var containers = await _client.Containers.ListContainersAsync(
                         new ContainersListParameters()
                         {
                             All = true
                         });
            if (containers?.Count > 0)
            { 
            return containers.Any(c => c.Names.Contains(name));
            }
            return false;
        }

        public async Task<(string containerName, string containerPort)?> SuggestBlackBoardParameters(bool useHub = true, string hubGroup = "default")
        {
            int port = 48030;
            var blackBoards = await GetBlackBoards();
            if (blackBoards != null && blackBoards.Count>0)
            {
                while (blackBoards.Any(bb => bb.Port == port))
                {
                    port++;
                }
            }

            string name = "blackboard-";
            if (useHub)
            {
                name += hubGroup;
            }
            else { name += "noHub"; }
            name += "-" + port;

            var containers = await _client.Containers.ListContainersAsync(
                         new ContainersListParameters()
                         {
                             All = true
                         });
            if (containers?.Count > 0)
            {
                int count = 0;
                string baseName = name;
                while (containers.Any(c => c.Names.Contains(name)))
                {
                    name = baseName + "_" + count++;
                    _logger?.LogWarning($"Original name already in use, try {name} instead");
                }
            }

            return (name, port.ToString());
        }

        public async Task StartContainer(string containerId)
        {
            await _client.Containers.StartContainerAsync(
        containerId,
        new ContainerStartParameters()
        );
        }

        public async Task StopContainer(string containerID)
        {
            var stopped = await _client.Containers.StopContainerAsync(
        containerID,
        new ContainerStopParameters(),
        CancellationToken.None);
        }

        public async Task DeleteContainer(string containerID)
        {
            await _client.Containers.RemoveContainerAsync(containerID, new ContainerRemoveParameters());      
        }
    }
}
