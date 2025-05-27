using Docker.DotNet;
using Docker.DotNet.Models;
using DWIS.Docker.Constants;
using DWIS.Docker.Models;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Globalization;
using System.Xml.Linq;
namespace DWIS.Docker.Clients
{
    public class DWISDockerClient
    {
        private DockerClient _client;
        private ILogger<DockerClient>? _logger;
        public DWISDockerClient(DWISDockerClientConfiguration? configuration = null, ILogger<DockerClient>? logger = null)
        {
            _logger = logger;

            string? uri = configuration != null ? configuration.DockerURI : null;

            DockerClientConfiguration dockerConf =
               string.IsNullOrEmpty(uri) ?
                   new DockerClientConfiguration() :
                   new DockerClientConfiguration(new Uri(uri));

            _client = dockerConf.CreateClient();
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

        public async Task CreateBlackboardContainer(string hubGroup, string containerName, string port)
        {
            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Name = containerName,
                Image = ImageNames.BLACKBOARD,
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
                Cmd = new List<string>() { "--useHub", "--hubURL", "https://dwis.digiwells.no/blackboard/applications", "--hubGroup", hubGroup, "--port", port },
                Labels = new Dictionary<string, string>() { { "port", port }, { "group", hubGroup } }
            });            
        }

        public async Task<IEnumerable<BlackboardContainerData>> GetBlackBoardContainers()
        {
            var blackBoardContainers = new List<BlackboardContainerData>();
            var containers = await _client.Containers.ListContainersAsync(
             new ContainersListParameters()
             {
                 All = true
             });
            if (containers != null && containers.Count > 0)
            {
                var myContainers = containers.Where(c => c.Image == "digiwells/ddhubserver:latest");

                foreach (var container in myContainers)
                {
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
                                if (idx == argsList.Count - 1 || argsList[idx + 1] != "false")
                                {
                                    idx = argsList.IndexOf("--hubURL");
                                    if (idx != -1 && idx < argsList.Count - 1)
                                    {
                                        string hubURL = argsList[idx + 1];
                                        string hubGroup = "default";
                                        string port = "48030";
                                       
                                        idx = argsList.IndexOf("--hubGroup");
                                        if (idx != -1 && idx < argsList.Count - 1)
                                        {
                                            hubGroup = argsList[idx + 1];
                                        }
                                        idx = argsList.IndexOf("--port");
                                        if (idx != -1 && idx < argsList.Count - 1)
                                        {
                                            port = argsList[idx + 1];
                                        }

                                        BlackboardContainerData containerData = new BlackboardContainerData();
                                        containerData.ContainerID = container.ID;
                                        containerData.ContainerName = container.Names.First();
                                        containerData.ContainerPort = port;
                                        containerData.ContainerGroup = hubGroup;
                                        containerData.ContainerStarted = container.State.ToLower() == "running";
                                        blackBoardContainers.Add(containerData);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return blackBoardContainers;

        }



    }

    public class DWISDockerClientConfiguration
    {
        public string DockerURI { get; set; } = string.Empty;
    }
}
