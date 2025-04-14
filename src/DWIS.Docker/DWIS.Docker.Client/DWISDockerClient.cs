using Docker.DotNet;
using Docker.DotNet.Models;
using DWIS.Container.Model;
using Microsoft.Extensions.Logging;
using System.Collections;
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

        public async Task<IList<BlackBoardContainer>>? GetBlackBoards()
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
    }
}
