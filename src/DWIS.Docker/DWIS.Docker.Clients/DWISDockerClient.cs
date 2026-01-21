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
        private DWISDockerClientConfiguration? _configuration;

        public DWISDockerClient(DWISDockerClientConfiguration? configuration = null, ILogger<DockerClient>? logger = null)
        {
            _logger = logger;
            _configuration = configuration;
            CreateDockerClient();
        }

        private void CreateDockerClient()
        {
            string? uri = _configuration != null ? _configuration.DockerURI : null;

            DockerClientConfiguration dockerConf =
               string.IsNullOrEmpty(uri) ?
                   new DockerClientConfiguration() :
                   new DockerClientConfiguration(new Uri(uri));

            _client = dockerConf.CreateClient();
        }

        public void UpdateConfiguration(DWISDockerClientConfiguration configuration)
        {
            _configuration = configuration;
            CreateDockerClient();
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

            bool exist = await CheckImageExist(ImageNames.BLACKBOARD_NOTAG, ImageNames.LATEST_TAG, true);
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


        public async Task<bool> CheckImageExist(string imageName, string tag, bool loadIfNotExist = false)
        {

            var images = await _client.Images.ListImagesAsync(new ImagesListParameters()
            {
                Filters = new System.Collections.Generic.Dictionary<string, IDictionary<string, bool>>
            {
                { "reference", new Dictionary<string, bool> { { imageName, true } } }
            }
            });
            if (images != null && images.Count > 0)
            {
                return true;
            }
            else
            {
                if (loadIfNotExist)
                {
                    await _client.Images.CreateImageAsync(
                        new ImagesCreateParameters()
                        {
                            FromImage = imageName,
                            Tag = tag
                        },
                        null,
                        new Progress<JSONMessage>());
                    return true;
                }
                return false;
            }
        }


        public async Task<string> CreateSchedulerContainer(string name = "scheduler")
        {
            bool exist = await CheckImageExist(ImageNames.SCHEDULER_NOTAG, "stable", true);


            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Name = name,
                Image = ImageNames.SCHEDULER
            });
            return response.ID;
        }


        public async Task<string> CreateStandardItemContainer(StandardSetUpItem item) 
        {
        return await CreateContainer(item.ImageName, item.ImageTag, item.ConfigLocalPath, item.ConfigContainerPath, item.DefaultContainerName);
        }



        public async Task<string> CreateContainer(string imageNameNoTag, string tag, string localConfigPath, string containerConfigPath, string containerName)
        {
            bool exist = await CheckImageExist(imageNameNoTag, tag, true);

            var ccp = new CreateContainerParameters()
            {
                Name = containerName,
                Image = imageNameNoTag + ":" + tag
            };

            if (!string.IsNullOrEmpty(localConfigPath) && !string.IsNullOrEmpty(containerConfigPath))
            {
                ccp.HostConfig = new HostConfig
                {
                    Binds = new List<string>
                    {
                        localConfigPath + ":" + containerConfigPath// Format: host-path:container-path
                        // Add options like ":ro" for read-only if needed
                    }
                };
            }



            var response = await _client.Containers.CreateContainerAsync(ccp);
            return response.ID;
        }


        public async Task<string> CreateComposerContainer(string name = "advicecomposer")
        {
            bool exist = await CheckImageExist(ImageNames.ADVICE_COMPOSER_NOTAG, ImageNames.STABLE_TAG, true);

            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Name = name,
                Image = ImageNames.ADVICE_COMPOSER,
                HostConfig = new HostConfig
                {
                    Binds = new List<string>
            {
               ImageNames.COMPOSER_WINDOWS_LOCALPATH + ":" + ImageNames.COMPOSER_CONTAINERPATH// Format: host-path:container-path
                // Add options like ":ro" for read-only if needed
            }
                }
            });
            return response.ID;
        }

        public async Task<IEnumerable<(string id, string name)>> GetComposerContainers()
        {
            var composerContainers = new List<(string id, string name)>();
            var containers = await _client.Containers.ListContainersAsync(
             new ContainersListParameters()
             {
                 All = true
             });
            if (containers != null && containers.Count > 0)
            {
                var myContainers = containers.Where(c => c.Image == ImageNames.ADVICE_COMPOSER);
                foreach (var container in myContainers)
                {
                    composerContainers.Add((container.ID, container.Names.First()));
                }
            }
            return composerContainers;
        }

        public async Task<IEnumerable<(string id, string name, bool running)>> GetContainers(string imageName)
        {
            var composerContainers = new List<(string id, string name, bool running)>();
            var containers = await _client.Containers.ListContainersAsync(
             new ContainersListParameters()
             {
                 All = true
             });
            if (containers != null && containers.Count > 0)
            {
                var myContainers = containers.Where(c => c.Image == imageName);
                foreach (var container in myContainers)
                {
                    composerContainers.Add((container.ID, container.Names.First(), container.State.ToLower() == "running"));
                }
            }
            return composerContainers;
        }



        public async Task<IEnumerable<(string id, string name)>> GetSchedulerContainers()
        {
            var composerContainers = new List<(string id, string name)>();
            var containers = await _client.Containers.ListContainersAsync(
             new ContainersListParameters()
             {
                 All = true
             });
            if (containers != null && containers.Count > 0)
            {
                var myContainers = containers.Where(c => c.Image == ImageNames.SCHEDULER);
                foreach (var container in myContainers)
                {
                    composerContainers.Add((container.ID, container.Names.First()));
                }
            }
            return composerContainers;
        }


        public async Task<StandardSetUpStatus> UpdateStandardSetupStatus(StandardSetUp standardSetUp)
        {
            var bbs = await GetBlackBoardContainers();          
            
            StandardSetUpStatus status = new StandardSetUpStatus();


            foreach (var item in standardSetUp.Items)
            {
                if (item.ModuleGroup == StandardSetUpItem.BlackBoardGroup)
                {
                    if (item.ModuleName == "Default Blackboard")
                    {
                        var dbb = bbs.Where(b => b.ContainerGroup == standardSetUp.HubGroup && b.ContainerPort == "48030");
                        if (dbb != null && dbb.Count() > 0)
                        {
                            foreach (var container in dbb)
                            {
                                StandardSetUpStatusItem statusItem = new StandardSetUpStatusItem();
                                statusItem.SetUpItem = item;
                                statusItem.ContainerName = container.ContainerName;
                                statusItem.ID = container.ContainerID;
                                statusItem.Started = container.ContainerStarted;
                                status.Items.Add(statusItem);
                            }
                        }
                    }
                    else
                    {
                        var dbb = bbs.Where(b => b.ContainerPort == "48031");
                        foreach (var container in dbb)
                        {
                            StandardSetUpStatusItem statusItem = new StandardSetUpStatusItem();
                            statusItem.SetUpItem = item;
                            statusItem.ContainerName = container.ContainerName;
                            statusItem.ID = container.ContainerID;
                            statusItem.Started = container.ContainerStarted;
                            status.Items.Add(statusItem);
                        }
                    }
                }
                else
                {
                    var containers = await GetContainers(item.ImageName + ":" + item.ImageTag);
                    if (containers != null && containers.Count() > 0)
                    {
                        foreach (var container in containers)
                        {
                            StandardSetUpStatusItem statusItem = new StandardSetUpStatusItem();
                            statusItem.SetUpItem = item;
                            statusItem.ContainerName = container.name;
                            statusItem.ID = container.id;
                            statusItem.Started = container.running;
                            status.Items.Add(statusItem);
                        }
                    }

                
                }
            }

           foreach(var standardItem in standardSetUp.Items)
           {
                var existingStatusItems = status.Items.Where(i => i.SetUpItem == standardItem);
                if (existingStatusItems == null || existingStatusItems.Count() == 0)
                {
                    StandardSetUpStatusItem statusItem = new StandardSetUpStatusItem();
                    statusItem.SetUpItem = standardItem;
                    statusItem.ContainerName = string.Empty;
                    statusItem.ID = string.Empty;
                    statusItem.Started = false;
                    statusItem.ConfigurationExists = false;
                    status.Items.Add(statusItem);
                }
           }

            foreach (var item in standardSetUp.Items)
            {
                if (item.ConfigurationRequired)
                {
                    if (System.IO.File.Exists(Path.Combine(item.ConfigLocalPath, item.ConfigFileName)))
                    {
                        foreach (var statusItem in status.Items.Where(i => i.SetUpItem == item))
                        {
                            statusItem.ConfigurationExists = true;
                        }
                    }
                }
            }


            return status;
        }
    }

    public class DWISDockerClientConfiguration
    {
        public string DockerURI { get; set; } = string.Empty;
    }
}
