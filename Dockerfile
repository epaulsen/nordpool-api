# Build stage - use SDK for building but with Alpine
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Install native build tools required for Native AOT compilation on Alpine
RUN apk add --no-cache \
    clang \
    gcc \
    g++ \
    musl-dev \
    lld \
    llvm \
    zlib-dev \
    libgcc \
    libstdc++ \
    make

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

# Install tzdata for timezone support
RUN apk add --no-cache tzdata

COPY --from=publish /app/publish .
ENTRYPOINT ["./NordpoolApi"]
