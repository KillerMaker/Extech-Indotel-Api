FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Exatech-Indotel-API.csproj Directory.Build.props ./
RUN dotnet restore Exatech-Indotel-API.csproj
COPY . .
RUN dotnet publish Exatech-Indotel-API.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
USER root
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends curl ca-certificates tzdata \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV TZ=America/Santo_Domingo
COPY --from=build /app/publish .
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 CMD curl --fail --silent -H "Host: ${PUBLIC_DOMAIN:-localhost}" http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "Exatech-Indotel-API.dll"]
