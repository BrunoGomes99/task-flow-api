FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/TaskFlow.Scheduler/TaskFlow.Scheduler.csproj", "src/TaskFlow.Scheduler/"]
COPY ["src/TaskFlow.Application/TaskFlow.Application.csproj", "src/TaskFlow.Application/"]
COPY ["src/TaskFlow.Domain/TaskFlow.Domain.csproj", "src/TaskFlow.Domain/"]
COPY ["src/TaskFlow.Infrastructure/TaskFlow.Infrastructure.csproj", "src/TaskFlow.Infrastructure/"]
RUN dotnet restore "./src/TaskFlow.Scheduler/TaskFlow.Scheduler.csproj"
COPY . .
WORKDIR "/src/src/TaskFlow.Scheduler"
RUN dotnet build "./TaskFlow.Scheduler.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TaskFlow.Scheduler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish --chown=app:app /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "TaskFlow.Scheduler.dll"]
