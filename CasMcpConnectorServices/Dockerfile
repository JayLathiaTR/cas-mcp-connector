FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TR_ART_USER
ARG TR_ART_TOKEN
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN dotnet nuget add source https://tr1.jfrog.io/tr1/api/nuget/v3/nuget --name JFrog-Artifactory-Nuget-Audit-Devops --username "$TR_ART_USER" --password "$TR_ART_TOKEN" --store-password-in-clear-text
COPY ["CasMcpConnectorServices/CasMcpConnectorServices.csproj", "CasMcpConnectorServices/"]
RUN dotnet restore "./CasMcpConnectorServices/CasMcpConnectorServices.csproj"
COPY . .
WORKDIR "/src/CasMcpConnectorServices"
RUN dotnet publish "./CasMcpConnectorServices.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "CasMcpConnectorServices.dll"]
