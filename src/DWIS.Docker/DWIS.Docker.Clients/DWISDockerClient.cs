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


        public async Task<bool> CheckImageExist(string imageName,string tag, bool loadIfNotExist = false)
        {

            var images = await _client.Images.ListImagesAsync(new ImagesListParameters()
            {
                Filters = new System.Collections.Generic.Dictionary<string, IDictionary<string, bool>>
            {
                { "reference", new Dictionary<string, bool> { { imageName, true } } }
            }
            });
            if (images != null && images.Count > 0 )
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
            bool exist = await CheckImageExist(ImageNames.SCHEDULER_NOTAG,"stable", true); 


            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Name = name,
                Image = ImageNames.SCHEDULER
            });
            return response.ID;
        }



        public async Task<string> CreateContainer(string imageNameNoTag, string tag, string localConfigPath, string containerConfigPath, string containerName)
        {
            bool exist = await CheckImageExist(imageNameNoTag, tag, true);

            var ccp = new CreateContainerParameters()
            {
                Name = containerName,
                Image = imageNameNoTag + ":" + tag
            };

            if(!string.IsNullOrEmpty(localConfigPath) && !string.IsNullOrEmpty(containerConfigPath))
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
            bool exist = await CheckImageExist(ImageNames.ADVICE_COMPOSER_NOTAG,ImageNames.STABLE_TAG, true);

            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Name = name,
                Image = ImageNames.ADVICE_COMPOSER,
                HostConfig = new HostConfig
                {
                    Binds = new List<string>
            {
               DWISModulesConfigurationClient.COMPOSER_WINDOWS_LOCALPATH + ":" + DWISModulesConfigurationClient.COMPOSER_CONTAINERPATH// Format: host-path:container-path
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

        public async Task<IEnumerable<(string id, string name)>> GetContainers(string imageName)
        {
            var composerContainers = new List<(string id, string name)>();
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
                    composerContainers.Add((container.ID, container.Names.First()));
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
    }

    public class DWISDockerClientConfiguration
    {
        public string DockerURI { get; set; } = string.Empty;
    }


    public class DWISModulesConfigurationClient
    {
        public const string COMPOSER_LINUX_LOCALPATH = "home/Volumes/DWISAdviceComposerService";
        public const string COMPOSER_WINDOWS_LOCALPATH = @"C:\Volumes\DWISAdviceComposerService";
        public const string COMPOSER_CONTAINERPATH = @"/home";
        public const string COMPOSER_CONFIGFILENAME = "config.json";


        public ComposerConfig? GetComposerConfigFromFile()
        {
            string filePath = Path.Combine(COMPOSER_WINDOWS_LOCALPATH, COMPOSER_CONFIGFILENAME);

            if (!File.Exists(filePath))
            {
                return null;
            }
            else
            {
                string json = System.IO.File.ReadAllText(filePath);
                var config = System.Text.Json.JsonSerializer.Deserialize<ComposerConfig>(json);
                return config;
            }
        }
        
        public ConfigType? GetConfigurationFromFile<ConfigType>(string localPath, string fileName) where ConfigType : class
        {
            string filePath = Path.Combine(localPath, fileName);
            if (!File.Exists(filePath))
            {
                return null;
            }
            else
            {
                string json = System.IO.File.ReadAllText(filePath);
                var config = System.Text.Json.JsonSerializer.Deserialize<ConfigType>(json);
                return config;
            }
        }


        public void SaveComposerConfigToFile(string config)
        {
            if (!Directory.Exists(COMPOSER_WINDOWS_LOCALPATH))
            {
                Directory.CreateDirectory(COMPOSER_WINDOWS_LOCALPATH);
            }
            System.IO.File.WriteAllText(Path.Combine(COMPOSER_WINDOWS_LOCALPATH, COMPOSER_CONFIGFILENAME), config);
        }

        public void SaveConfigToFile(string config, string localPath, string fileName)
        {
            if (!Directory.Exists(localPath))
            {
                Directory.CreateDirectory(localPath);
            }
            System.IO.File.WriteAllText(Path.Combine(localPath, fileName), config);
        }



        public class ComposerConfig
        {
            public TimeSpan LoopDuration { get; set; } = TimeSpan.FromSeconds(1.0);
            public string? OPCUAURL { get; set; } = "opc.tcp://localhost:48030";
            public TimeSpan ControllerObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
            public TimeSpan ProcedureObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
            public TimeSpan FaultDetectionIsolationAndRecoveryObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
            public TimeSpan SafeOperatingEnvelopeObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
        }
    }
}
