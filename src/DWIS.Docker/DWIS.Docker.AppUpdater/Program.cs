// See https://aka.ms/new-console-template for more information

if(args.Length == 0)
{
    Console.WriteLine("Please provide the image name as an argument.");
    return;
}

string imageName = args[0];


await new DWIS.Docker.Clients.DWISDockerClient().UpdateContainersByImage(imageName);



