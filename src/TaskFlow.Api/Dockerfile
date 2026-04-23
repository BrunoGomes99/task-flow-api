# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build images for faster debugging.

# This stage is used when running the app in VS in fast mode (default for Debug configuration).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/TaskFlow.Api/TaskFlow.Api.csproj", "src/TaskFlow.Api/"]
COPY ["src/TaskFlow.Application/TaskFlow.Application.csproj", "src/TaskFlow.Application/"]
COPY ["src/TaskFlow.Domain/TaskFlow.Domain.csproj", "src/TaskFlow.Domain/"]
COPY ["src/TaskFlow.Infrastructure/TaskFlow.Infrastructure.csproj", "src/TaskFlow.Infrastructure/"]
RUN dotnet restore "./src/TaskFlow.Api/TaskFlow.Api.csproj"
COPY . .
WORKDIR "/src/src/TaskFlow.Api"
RUN dotnet build "./TaskFlow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage publishes the service project to be copied into the final stage.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TaskFlow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (default when not using Debug configuration).
FROM base AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish --chown=app:app /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
