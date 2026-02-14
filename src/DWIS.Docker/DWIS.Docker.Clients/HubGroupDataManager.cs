using DWIS.Docker.Models;
//using DWIS.SignalR.DTO;
using Microsoft.AspNetCore.SignalR.Client;

namespace DWIS.Docker.Clients
{
  

    public class HubGroupDataManager
    {
        private HubConnection? _connection;

        private DWISDockerClient _client;
        public HubMonitoringData HubMonitoringData { get; private set; } = new HubMonitoringData() ;

        public string HubAddress { get; set; } = "https://dwis.digiwells.no/blackboard/applications";


        public HubGroupDataManager(DWISDockerClient client, HubConnection? hubConnection) 
        {
            HubMonitoringData.HubURL = HubAddress;

            _connection = hubConnection;

            

            _client = client;
            InitHubConnection();

            //InitDockerClient("http://localhost:2375");
        }

        //private void InitDockerClient(string uri = "")
        //{
        //    DockerClientConfiguration dockerConf = 
        //        string.IsNullOrEmpty(uri) ? 
        //            new DockerClientConfiguration() :  
        //            new DockerClientConfiguration(new Uri(uri));
        //    _client = dockerConf.CreateClient();
        //}

        private async void InitHubConnection()
        {
            if (_connection != null && _connection.State != HubConnectionState.Connecting && _connection.State != HubConnectionState.Connected)
            {
                try
                {
                    await _connection.StartAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public HubGroupData? GetGroupData(string groupName)
        {
            if (HubMonitoringData.HubGroups.ContainsKey(groupName))
            {
                return HubMonitoringData.HubGroups[groupName];
            }
            else return null;
        }

        public (string containerName, string containerPort)? SuggestContainerData(string groupName)
        {
            if (HubMonitoringData.HubGroups.ContainsKey(groupName))
            {
                var groupData = HubMonitoringData.HubGroups[groupName];

                if (groupData != null)
                {
                    int containerCount = groupData.BlackboardContainers.Count;
                   


                    var allPorts = new List<int>();
                    foreach (var group in HubMonitoringData.HubGroups.Values)
                    {
                        allPorts.AddRange(group.BlackboardContainers.Select(container => { if (int.TryParse(container.ContainerPort, out int port)) { return port; } else return 0; }));
                    }
                    int port = 48030;
                    while (allPorts.Contains(port))
                    {
                        port++;
                    }

                    string containerName = "blackboard-" + groupName.Replace(' ', '_') + "-" + port.ToString();
                    //if (containerCount > 0)
                    //{
                    //    containerName += "-" + containerCount;
                    //}

                    return (containerName, port.ToString());
                }
            }
            return null;
        }

        public async Task CreateGroup(string groupName)
        {
            if (!HubMonitoringData.HubGroups.ContainsKey(groupName))
            {
                HubGroupData hubGroupData = new HubGroupData() { GroupName = groupName };
                HubMonitoringData.HubGroups.Add(groupName, hubGroupData);
            }
            await UpdateManagerData();
        }

        public async Task StartContainer(string containerId)
        {
            await _client.StartContainer(containerId);
        //    await _client.Containers.StartContainerAsync(
        //containerId,
        //new ContainerStartParameters()
        //);
        }

        public async Task StopContainer(string containerID)
        {
            await _client.StopContainer(containerID);
        //    var stopped = await _client.Containers.StopContainerAsync(
        //containerID,
        //new ContainerStopParameters(),
        //CancellationToken.None);
        }

        public async Task DeleteContainer(string containerID)
        {
            await _client.DeleteContainer(containerID);// .Containers.RemoveContainerAsync(containerID, new ContainerRemoveParameters());

            var group = HubMonitoringData.HubGroups.Values.FirstOrDefault(g => g.BlackboardContainers.FirstOrDefault(c => c.ContainerID == containerID)!=null);
            if (group != null) 
            {
                var containerData = group.BlackboardContainers.FirstOrDefault(c => c.ContainerID == containerID);
                group.BlackboardContainers.Remove(containerData);
            }

            await UpdateManagerData();
        }

        public async Task CreateContainer(string hubGroup, string containerName, string port)
        {
            await _client.CreateBlackboardContainer(hubGroup, containerName, port);

            //var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
            //{
            //    Name = containerName,
            //    Image = "digiwells/ddhubserver:latest",
            //    Hostname = "localhost",
            //    ExposedPorts = new Dictionary<string, EmptyStruct>
            //    {
            //    {port, default(EmptyStruct) }
            //    },
            //    HostConfig = new HostConfig
            //    {
            //        PortBindings = new Dictionary<string, IList<PortBinding>>
            //{
            //    {port, new List<PortBinding> {new PortBinding {HostPort = port}}}
            //},
            //        PublishAllPorts = true
            //    },
            //    Cmd = new List<string>() { "--useHub", "--hubURL", "https://dwis.digiwells.no/blackboard/applications", "--hubGroup", hubGroup, "--port", port },
            //    Labels = new Dictionary<string, string>() { { "port", port }, { "group", hubGroup } }
            //});

            await UpdateManagerData();
        }

        public async Task<bool> ClearGroupManifests(string groupName)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected) return false;
            else
            {
               return  await _connection.InvokeAsync<bool>("ClearGroupManifests", groupName);
            }
        }
        public async Task UpdateManagerData()
        {
            GroupDataDTO? groupData = null;

            if (_connection?.State == HubConnectionState.Connected)
            {
                groupData = await _connection.InvokeAsync<GroupDataDTO>("GetGroupData");
            }
            if (groupData != null && groupData.GroupDatas != null)
            {
                foreach (var gd in groupData.GroupDatas)
                {
                    if (gd != null)
                    {
                        if (HubMonitoringData.HubGroups.ContainsKey(gd.GroupName))
                        {
                            HubMonitoringData.HubGroups[gd.GroupName].NumberOfConnections = gd.GroupCount;
                        }
                        else
                        {
                            HubMonitoringData.HubGroups.Add(gd.GroupName, new HubGroupData() { GroupName = gd.GroupName, NumberOfConnections = gd.GroupCount });
                        }
                    }
                }
            }


            var blackBoardContainers = await _client.GetBlackBoardContainers();
            foreach (var blackBoardContainer in blackBoardContainers) 
            {                
                string hubGroup = blackBoardContainer.ContainerGroup;
                HubGroupData hubGroupData = null;

                if (HubMonitoringData.HubGroups.ContainsKey(hubGroup))
                {
                    hubGroupData = HubMonitoringData.HubGroups[hubGroup];
                }
                else
                {
                    hubGroupData = new HubGroupData();
                    hubGroupData.GroupName = hubGroup;
                    hubGroupData.NumberOfConnections = 0;
                    HubMonitoringData.HubGroups.Add(hubGroup, hubGroupData);
                }

                BlackboardContainerData? containerData = hubGroupData.BlackboardContainers.FirstOrDefault(c => c.ContainerID ==blackBoardContainer.ContainerID);
                if (containerData == null)
                {
                    hubGroupData.BlackboardContainers.Add(blackBoardContainer);
                }
                else 
                {
                    containerData.ContainerStarted = blackBoardContainer.ContainerStarted;
                }
            }
            /*
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
                                        if (hubURL == HubMonitoringData.HubURL)
                                        {
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
                                        }

                                        //ContainerData containerData = new ContainerData();
                                        //containerData.ContainerID = container.ID;
                                        //containerData.ContainerPort = port;
                                        //containerData.ContainerGroup = hubGroup;
                                        //containerData.ContainerStarted = container.ss

                                        HubGroupData hubGroupData = null;

                                        if (HubMonitoringData.HubGroups.ContainsKey(hubGroup))
                                        {
                                            hubGroupData = HubMonitoringData.HubGroups[hubGroup];
                                        }
                                        else
                                        { 
                                            hubGroupData = new HubGroupData();  
                                            hubGroupData.GroupName = hubGroup;
                                            hubGroupData.NumberOfConnections = 0;
                                            HubMonitoringData.HubGroups.Add(hubGroup, hubGroupData);
                                        }

                                        BlackboardContainerData? containerData = hubGroupData.BlackboardContainers.FirstOrDefault(c => c.ContainerID == container.ID);

                                        if (containerData == null) 
                                        {
                                            containerData = new BlackboardContainerData();
                                            containerData.ContainerID = container.ID;
                                            containerData.ContainerName = container.Names.First();
                                            containerData.ContainerPort = port;
                                            containerData.ContainerGroup = hubGroup;
                                            hubGroupData.BlackboardContainers.Add(containerData);
                                        }
                                        containerData.ContainerStarted = container.State.ToLower() == "running";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            */
            //List<HubGroupData> toRemove = new List<HubGroupData>();


        }   

        /*
        public async void LaunchBlackBoard(string containerName, string hubGroup, string port)
        {
            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Name = containerName,
                Image = "digiwells/ddhubserver:latest",
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
            var startResponse = await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
        }
        */
        public IEnumerable<BlackboardContainerData> GetAllContainers()
        {
            var allContainersLists = HubMonitoringData.HubGroups.Select(hg => hg.Value.BlackboardContainers);

            return allContainersLists.Aggregate(new List<BlackboardContainerData>(), (l, l2) => { l.AddRange(l2); return l; });
        }

        public Dictionary<string, IEnumerable<BlackboardContainerData>> GetContainersPerPort()
        {
            var allContainers = GetAllContainers();
            var groups = allContainers.GroupBy(c => c.ContainerPort);

            Dictionary<string, IEnumerable<BlackboardContainerData>> dictionary = new Dictionary<string, IEnumerable<BlackboardContainerData>>();

            foreach (var containerGroup in groups) 
            {
                dictionary.Add(containerGroup.Key, containerGroup.ToList());
            }
            return dictionary;
        }
    }
    public class GroupData
    {
        public string? GroupName { get; set; }
        public int GroupCount { get; set; }
    }
    public class GroupDataDTO
    {
        public GroupData[]? GroupDatas { get; set; }
    }
}
