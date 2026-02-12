using Docker.DotNet;
using Docker.DotNet.Models;
using DWIS.Docker.Constants;
using DWIS.Docker.Models;
using Microsoft.Extensions.Logging;
using DRD = Docker.Registry.DotNet;
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
            try
            {
                _client = dockerConf.CreateClient();                
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.ToString(), ex);
                throw ex;
            }
        }

        public void UpdateConfiguration(DWISDockerClientConfiguration configuration)
        {
            _configuration = configuration;
            CreateDockerClient();
        }

        public async Task StartContainer(string containerId)
        {
            try
            {
                await _client.Containers.StartContainerAsync(
                    containerId,
                    new ContainerStartParameters()
                    );
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
        }

        public async Task StopContainer(string containerID)
        {
            try
            {
                var stopped = await _client.Containers.StopContainerAsync(
                containerID,
                new ContainerStopParameters(),
                CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
        }

        public async Task DeleteContainer(string containerID)
        {
            try
            {
                await _client.Containers.RemoveContainerAsync(containerID, new ContainerRemoveParameters());
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
        }

        public async Task CreateBlackboardContainer(string hubGroup, string containerName, string port)
        {
            try
            {
                bool exist = await CheckImageExist(Names.BLACKBOARD_NOTAG, Names.LATEST_TAG, true);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
            List<string> cmd;
            if (!string.IsNullOrEmpty(hubGroup))
            {
                cmd = new List<string>() { "--useHub", "--hubURL", "https://dwis.digiwells.no/blackboard/applications", "--hubGroup", hubGroup, "--port", port };
            }
            else { cmd = new List<string>() { "--port", port }; }

            try
            {
                var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
                {
                    Name = containerName,
                    Image = Names.BLACKBOARD,
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
                    Cmd = cmd,
                    Labels = new Dictionary<string, string>() { { "port", port }, { "group", hubGroup } }
                });
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
        }

        public async Task<IEnumerable<BlackboardContainerData>> GetBlackBoardContainers()
        {
            var blackBoardContainers = new List<BlackboardContainerData>();

            IList<ContainerListResponse> containers = null;
            try
            {
                containers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters()
                {
                    All = true
                });
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
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
                            bool useHub = false;
                            if (idx != -1)
                            {
                                if (idx == argsList.Count - 1 || argsList[idx + 1] != "false")
                                {
                                    idx = argsList.IndexOf("--hubURL");
                                    if (idx != -1 && idx < argsList.Count - 1)
                                    {
                                        useHub = true;
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
                            if (!useHub)
                            {
                                string port = "48030";


                                idx = argsList.IndexOf("--port");
                                if (idx != -1 && idx < argsList.Count - 1)
                                {
                                    port = argsList[idx + 1];
                                }

                                BlackboardContainerData containerData = new BlackboardContainerData();
                                containerData.ContainerID = container.ID;
                                containerData.ContainerName = container.Names.First();
                                containerData.ContainerPort = port;
                                containerData.ContainerGroup = "";
                                containerData.ContainerStarted = container.State.ToLower() == "running";
                                blackBoardContainers.Add(containerData);
                            }
                        }
                    }
                }
            }
            return blackBoardContainers;

        }



        public async Task UpdateStandardSetupImageDates(StandardSetUp setup)
        {

            var configuration = new DRD.RegistryClientConfiguration("https://hub.docker.com");
            try
            {
                using var client = configuration.CreateClient();


                foreach (var item in setup.Items)
                {
                    var tags = await client.Repository.ListRepositoryTags("digiwells", item.ImageName.Remove(0, "digiwells/".Length));
                    var tagItem = tags.Tags.FirstOrDefault(t => t.Name == item.ImageTag);
                    if (tagItem != null)
                    {
                        item.Digest = tagItem.Digest;
                        item.ImageRepoTimeStamp = tagItem.LastUpdated;
                    }
                }
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
        }




        public async Task<bool> CheckImageExist(string imageName, string tag, bool loadIfNotExist = false)
        {
            try
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
                        try
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
                        catch (Exception ex)
                        {
                            _logger?.LogError("Error loading image {imageName}:{tag} - {message}", imageName, tag, ex.Message);
                        }
                    }
                    return false;
                }
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
        }

        public async Task<string> CreateStandardItemContainer(StandardSetUpItem item, string? mainBlackBoardIP = null)
        {
            if (mainBlackBoardIP == null)
            {
                return await CreateContainer(item.ImageName, item.ImageTag, item.ConfigLocalPath, item.ConfigContainerPath, item.DefaultContainerName);
            }
            else
            {
                var envVariables = new List<(string key, string val)>
                {
                    (DWIS.Client.ReferenceImplementation.IDWISClientConfiguration.BLACKBOARD_IP_ENVIRONMENT_VARIABLE, mainBlackBoardIP)
                };
                return await CreateContainer(item.ImageName, item.ImageTag, item.ConfigLocalPath, item.ConfigContainerPath, item.DefaultContainerName, envVariables);
            }
        }

        public async Task UpdateStandardItemImages(IEnumerable<StandardSetUpStatusItem> items)
        {
            try
            {
                var groups = items.GroupBy(i => i.SetUpItem);

                List<(string name, string image, string hostname, IList<string> env, HostConfig hConf, IDictionary<string, EmptyStruct> ports)> toRecreate
                    = new List<(string name, string image, string hostname, IList<string> env, HostConfig hConf, IDictionary<string, EmptyStruct> ports)>();

                foreach (var group in groups)
                {
                    foreach (var item in group)
                    {
                        if (!string.IsNullOrEmpty(item.ContainerID))
                        {
                            await StopContainer(item.ContainerID);
                            var ins = await _client.Containers.InspectContainerAsync(item.ContainerID);

                            toRecreate.Add((ins.Name, ins.Config.Image, ins.Config.Hostname, ins.Config.Env, ins.HostConfig, ins.Config.ExposedPorts));

                            await _client.Containers.RemoveContainerAsync(item.ContainerID, new ContainerRemoveParameters() { Force = true, RemoveVolumes = false, RemoveLinks = false });
                        }
                    }

                    await _client.Images.DeleteImageAsync(group.Key.ImageName + ":" + group.Key.ImageTag, new ImageDeleteParameters() { Force = true });

                    await CheckImageExist(group.Key.ImageName, group.Key.ImageTag, true);
                }

                foreach (var item in toRecreate)
                {
                    var ccp = new CreateContainerParameters()
                    {
                        Name = item.name,
                        Image = item.image,
                        HostConfig = item.hConf,
                        Env = item.env,
                        Hostname = item.hostname,
                        ExposedPorts = item.ports
                    };
                    ccp.Tty = true;
                    ccp.OpenStdin = true;
                    var response = await _client.Containers.CreateContainerAsync(ccp);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
        }

        public async Task<string> CreateContainer(string imageNameNoTag, string tag, string localConfigPath, string containerConfigPath, string containerName, IEnumerable<(string key, string val)>? envVariables = null, IEnumerable<(string hostPort, string containerPort)>? portsMapping = null)
        {
            bool exist = await CheckImageExist(imageNameNoTag, tag, true);
            if (exist)
            {
                var ccp = new CreateContainerParameters()
                {
                    Name = containerName,
                    Image = imageNameNoTag + ":" + tag,

                };
                ccp.Tty = true;
                ccp.OpenStdin = true;
                if (envVariables != null)
                {
                    List<string> envs = new List<string>();
                    foreach (var env in envVariables)
                    {
                        envs.Add(env.key + "=" + env.val);
                    }
                    ccp.Env = envs;
                }

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

                if (portsMapping != null)
                {
                    Dictionary<string, EmptyStruct> exposedPorts = new Dictionary<string, EmptyStruct>();
                    Dictionary<string, IList<PortBinding>> portBindings = new Dictionary<string, IList<PortBinding>>();
                    foreach (var pm in portsMapping)
                    {
                        exposedPorts.Add(pm.containerPort, default(EmptyStruct));
                        portBindings.Add(pm.containerPort, new List<PortBinding> { new PortBinding { HostPort = pm.hostPort } });
                    }
                    ccp.ExposedPorts = exposedPorts;

                    if (ccp.HostConfig == null)
                    {
                        ccp.HostConfig = new HostConfig();
                    }
                    ccp.HostConfig.PublishAllPorts = true;
                    ccp.HostConfig.PortBindings = portBindings;



                }
                try
                {
                    var response = await _client.Containers.CreateContainerAsync(ccp);
                    return response.ID;
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception.ToString(), exception);
                    throw exception;
                }
            }
            else { return string.Empty; }
        }

        public async Task<IEnumerable<(string id, string name, bool running)>> GetContainers(string imageName)
        {
            var composerContainers = new List<(string id, string name, bool running)>();
            try
            {
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
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }
        }

        public async Task<StandardSetUpStatus> UpdateStandardSetupStatus(StandardSetUp standardSetUp, bool replicationEnabled, string hubGroup)
        {
            var bbs = await GetBlackBoardContainers();

            StandardSetUpStatus status = new StandardSetUpStatus();


            foreach (var item in standardSetUp.Items)
            {
                if (item.ModuleGroup == StandardSetUpItem.BlackBoardGroup)
                {
                    if (item.ModuleDisplayName == "Default Blackboard")
                    {

                        var dbb = bbs.Where(b =>
                        ((string.IsNullOrEmpty(b.ContainerGroup) && !replicationEnabled)
                        ||( b.ContainerGroup == hubGroup && replicationEnabled)) && b.ContainerPort == item.BlackBoardPort);
                        if (dbb != null && dbb.Count() > 0)
                        {
                            foreach (var container in dbb)
                            {
                                StandardSetUpStatusItem statusItem = new StandardSetUpStatusItem();
                                statusItem.SetUpItem = item;
                                statusItem.ContainerName = container.ContainerName;
                                statusItem.ContainerID = container.ContainerID;
                                statusItem.Started = container.ContainerStarted;
                                status.Items.Add(statusItem);
                            }
                        }
                    }
                    else
                    {
                        var dbb = bbs.Where(b => b.ContainerPort == item.BlackBoardPort);
                        foreach (var container in dbb)
                        {
                            StandardSetUpStatusItem statusItem = new StandardSetUpStatusItem();
                            statusItem.SetUpItem = item;
                            statusItem.ContainerName = container.ContainerName;
                            statusItem.ContainerID = container.ContainerID;
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
                            statusItem.ContainerID = container.id;
                            statusItem.Started = container.running;
                            status.Items.Add(statusItem);
                        }
                    }


                }
            }

            foreach (var standardItem in standardSetUp.Items)
            {
                var existingStatusItems = status.Items.Where(i => i.SetUpItem == standardItem);
                if (existingStatusItems == null || existingStatusItems.Count() == 0)
                {
                    StandardSetUpStatusItem statusItem = new StandardSetUpStatusItem();
                    statusItem.SetUpItem = standardItem;
                    statusItem.ContainerName = string.Empty;
                    statusItem.ContainerID = string.Empty;
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

            try
            {
                foreach (var item in status.Items)
                {
                    DateTime imageDate = DateTime.MinValue;

                    if (!string.IsNullOrEmpty(item.ContainerID))
                    {
                        var container = await _client.Containers.InspectContainerAsync(item.ContainerID);
                        var imageID = container.Image;
                        var image = await _client.Images.InspectImageAsync(imageID);

                        imageDate = image.Created;
                    }

                    if (imageDate != DateTime.MinValue && item.SetUpItem.ImageRepoTimeStamp != DateTime.MinValue)
                    {
                        if (item.SetUpItem.ImageRepoTimeStamp > imageDate + TimeSpan.FromMinutes(1))
                        {
                            item.CurrentImageStatus = StandardSetUpStatusItem.ImageStatus.Outdated;
                        }
                        else
                        {
                            item.CurrentImageStatus = StandardSetUpStatusItem.ImageStatus.Updated;
                        }
                    }
                    else
                    {
                        item.CurrentImageStatus = StandardSetUpStatusItem.ImageStatus.Unknown;
                    }

                }
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception.ToString(), exception);
                throw exception;
            }

            return status;
        }

        public async Task<(DateTime localTime, DateTime repoTime)> GetUpdateInfo(string containerID, string imageName, string tag)
        {
            var configuration = new DRD.RegistryClientConfiguration("https://hub.docker.com");

            using var client = configuration.CreateClient();

            // List tags for a specific repository
            var tags = await client.Repository.ListRepositoryTags("digiwells", imageName.Remove(0, "digiwells/".Length));

            var tagItem = tags.Tags.FirstOrDefault(t => t.Name == tag);


            DateTime imageDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(containerID))
            {
                var container = await _client.Containers.InspectContainerAsync(containerID);
                var imageID = container.Image;
                var image = await _client.Images.InspectImageAsync(imageID);

                imageDate = image.Created;
            }
            if (tagItem != null)
            {
                return (imageDate, tagItem.LastUpdated);
            }
            else return (imageDate, DateTime.MinValue);
        }


    }

    public class DWISDockerClientConfiguration
    {
        public string DockerURI { get; set; } = string.Empty;
    }
}
