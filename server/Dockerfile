# Dedicated match server image.
# 1. In Unity: Stormdrop ▸ 4. Build Dedicated Server (Linux)  → unity/StormdropRoyale/Builds/Server/
# 2. From the repository root:  docker build -f server/Dockerfile -t stormdrop-server .
FROM ubuntu:24.04
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && rm -rf /var/lib/apt/lists/*
RUN useradd -m game
WORKDIR /srv/stormdrop
COPY unity/StormdropRoyale/Builds/Server/ ./
RUN chmod +x StormdropServer.x86_64 && chown -R game /srv/stormdrop
USER game
EXPOSE 7777/udp
# One process = one match. When the match ends the process exits and Docker restarts it fresh.
ENTRYPOINT ["./StormdropServer.x86_64", "-batchmode", "-nographics", "-logFile", "-"]
