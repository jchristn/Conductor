# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY src/Conductor.Core/Conductor.Core.csproj Conductor.Core/
COPY src/Conductor.Server/Conductor.Server.csproj Conductor.Server/

# Restore dependencies
RUN dotnet restore Conductor.Server/Conductor.Server.csproj

# Copy source code
COPY src/Conductor.Core/ Conductor.Core/
COPY src/Conductor.Server/ Conductor.Server/

# Build
WORKDIR /src/Conductor.Server
RUN dotnet build -c Release -o /app/build

# Publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install networking and troubleshooting tools
RUN apt-get update && apt-get install -y --no-install-recommends \
    iputils-ping \
    net-tools \
    traceroute \
    wget \
    curl \
    vim \
    dnsutils \
    netcat-openbsd \
    iproute2 \
    tcpdump \
    && rm -rf /var/lib/apt/lists/*

# Create directories for data and logs
RUN mkdir -p /app/data /app/logs

# Copy published files
COPY --from=build /app/publish .

# Set environment variables
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:9000

# Expose port
EXPOSE 9000

# Entry point
ENTRYPOINT ["dotnet", "Conductor.Server.dll"]
