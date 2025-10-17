# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install native build tools required for Native AOT compilation
RUN apt-get update && apt-get install -y \
    clang \
    zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy csproj and restore dependencies
COPY ["src/NordpoolApi/NordpoolApi.csproj", "NordpoolApi/"]
RUN dotnet restore "NordpoolApi/NordpoolApi.csproj"

# Copy everything else and build
COPY src/NordpoolApi/ NordpoolApi/
WORKDIR "/src/NordpoolApi"
RUN dotnet build "NordpoolApi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "NordpoolApi.csproj" -c Release -o /app/publish /p:PublishTrimmed=true /p:SelfContained=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["./NordpoolApi"]
