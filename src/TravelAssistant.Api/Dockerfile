# APP-8 — TravelAssistant API container image.
# Build args wire the runtime version surface (/api/version):
#   GITHUB_SHA  → exposed as env, returned as "commit"
#   BUILD_TIME  → ISO-8601 UTC, returned as "buildTime"
# `version` is read from version.txt copied into the published content root.

ARG DOTNET_SDK_VERSION=9.0
ARG DOTNET_RUNTIME_VERSION=9.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
WORKDIR /src

# Cache restore separately from source.
COPY TravelAssistant.slnx Directory.Build.props ./
COPY src/TravelAssistant.Api/TravelAssistant.Api.csproj src/TravelAssistant.Api/
RUN dotnet restore src/TravelAssistant.Api/TravelAssistant.Api.csproj

COPY . .
RUN dotnet publish src/TravelAssistant.Api/TravelAssistant.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# APP-8 — inject release metadata at image build time.
ARG GITHUB_SHA=unknown
ARG BUILD_TIME
ENV GITHUB_SHA=${GITHUB_SHA} \
    BUILD_TIME=${BUILD_TIME} \
    ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "TravelAssistant.Api.dll"]
