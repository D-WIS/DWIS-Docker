# DWIS-Docker
Facilities for D-WIS containers management

Running the container:
```
docker run --name dwis-docker --user root -P -p 5273:8081/tcp 5272:8080/tcp -v /var/run/docker.sock:/var/run/docker.sock -v C:\Volumes:/home -e DOCKER_HOST=npipe:////./pipe/docker_engine -dit digiwells/dwis-docker:stable
```

Then navigate to `http://localhost:5272`