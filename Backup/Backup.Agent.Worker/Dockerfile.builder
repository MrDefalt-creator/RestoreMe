# One-shot image that publishes self-contained, single-file agent binaries
# for every RID we ship. Mounted volume `/output` is where the resulting
# files end up — the docker-compose `agent-builder` service mounts the
# shared `agent_binaries` volume there so the backend can serve them.
#
# Triggered manually (it's a build-agents-profile service in compose):
#   docker compose --profile build-agents up agent-builder
#
# Re-run whenever Backup.Agent.Worker code changes.
FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

# Context is repo root (set by compose). Restore project graph first for
# better layer caching on subsequent rebuilds.
COPY ["Backup/Backup.Agent.Worker/Backup.Agent.Worker.csproj", "Backup/Backup.Agent.Worker/"]
COPY ["Backup/Backup.Shared.Contracts/Backup.Shared.Contracts.csproj", "Backup/Backup.Shared.Contracts/"]
RUN dotnet restore "Backup/Backup.Agent.Worker/Backup.Agent.Worker.csproj"

COPY Backup/ Backup/
WORKDIR /src/Backup/Backup.Agent.Worker

# Three publishes, one per RID we currently ship. The .csproj already sets
# PublishSingleFile=true + SelfContained=true + InvariantGlobalization so
# `dotnet publish -r <RID>` yields a single ~85 MB executable per target.
# win-x64 cross-publishes cleanly from the linux SDK image.
ENTRYPOINT ["/bin/sh","-c","\
set -e; \
echo '==> Publishing linux-x64'; \
dotnet publish -c Release -r linux-x64 -o /tmp/out-linux-x64 --nologo -v minimal; \
cp /tmp/out-linux-x64/Backup.Agent.Worker /output/restoreme-agent-linux-x64; \
echo '==> Publishing linux-arm64'; \
dotnet publish -c Release -r linux-arm64 -o /tmp/out-linux-arm64 --nologo -v minimal; \
cp /tmp/out-linux-arm64/Backup.Agent.Worker /output/restoreme-agent-linux-arm64; \
echo '==> Publishing win-x64'; \
dotnet publish -c Release -r win-x64 -o /tmp/out-win-x64 --nologo -v minimal; \
cp /tmp/out-win-x64/Backup.Agent.Worker.exe /output/restoreme-agent-win-x64.exe; \
chmod 0755 /output/restoreme-agent-linux-x64 /output/restoreme-agent-linux-arm64; \
echo; echo 'Published binaries:'; ls -lh /output"]
