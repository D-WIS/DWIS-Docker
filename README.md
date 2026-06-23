# DWIS-Docker — Getting Started

DWIS-Docker is a container management tool for D-WIS services. It runs as a Docker container and exposes a web UI to manage other containers on the same host.

> **Note:** This application is a work in progress and is not free of bugs. Use it with that in mind and feel free to report any issues.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

## 1. Launch the app

Create a folder named `C:\Volumes` on your machine:

```powershell
mkdir C:\Volumes
```

> You can use a different path, but make sure to update the `-v C:\Volumes:/home` part of the command below accordingly.

Run the following command to pull and start the container:

```bash
docker run --name dwis-docker --user root -P \
  -p 5273:8081/tcp \
  -p 5272:8080/tcp \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v C:\Volumes:/home \
  -e DOCKER_HOST=npipe:////./pipe/docker_engine \
  -dit digiwells/dwis-docker:stable
```

Then open your browser and navigate to:

```
http://localhost:5272
```

## 2. First test

This test will launch a **Blackboard**, replicated against a **Blackboard Hub** hosted by NORCE. Some real-time data are constantly streaming in the Hub, which makes it a good environment for early testing of any D-WIS installation. 


The app opens on the **Standard Set-Up** tab. Before proceeding, verify the following settings:

- **Replication** checkbox is checked
- **Replication group** is set to `default`
- **Blackboard host IP** is set to `host.docker.internal`

> Replication uses a secure WebSocket (WSS over HTTPS) connection to `https://dwis.digiwells.no/blackboard/applications`. Depending on your network configuration, you may need to open the appropriate firewall rules to allow this outbound connection.

In the table below, find the **Local Blackboard** entry and click the **CREATE** button.

A blackboard container should now have been created and should appear in your Docker container list.

Click the **START** button. The container should now be up and running. Give it a couple of minutes to warm up.

Navigate to the **Clients** tab.

Start the **Log-View** and **Blackboard Explorer** applications. Docker images will be downloaded and containers will be launched — this can take a few minutes.

Once started, click the **Port** links (5276 and 5277) to open the applications in your browser. The two apps should display:
- **Log-View**: some standard drilling logs
- **Blackboard Explorer**: a simple view of the contents of the blackboard

## 3. Test OPC-UA connection

You can connect any OPC-UA client to the blackboard. By default, a no-security connection is available at:

```
opc.tcp://localhost:48030
```

Once connected, you can test subscribing to streaming data.
