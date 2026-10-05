# API image (both replicas run this). Build context: repo root.
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY Worktime.sln ./
COPY src/Worktime.Domain/Worktime.Domain.csproj src/Worktime.Domain/
COPY src/Worktime.Application/Worktime.Application.csproj src/Worktime.Application/
COPY src/Worktime.Infrastructure/Worktime.Infrastructure.csproj src/Worktime.Infrastructure/
COPY src/Worktime.Api/Worktime.Api.csproj src/Worktime.Api/
RUN dotnet restore src/Worktime.Api/Worktime.Api.csproj
COPY src/ src/
RUN dotnet publish src/Worktime.Api/Worktime.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
# tzdata: per-user IANA zones (America/Lima…) must resolve inside the container.
RUN apk add --no-cache tzdata
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=0
COPY --from=build /app .
USER app
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=6 CMD wget -qO- http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Worktime.Api.dll"]
