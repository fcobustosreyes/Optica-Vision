FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/OpticaVision.Web/OpticaVision.Web.csproj src/OpticaVision.Web/
RUN dotnet restore src/OpticaVision.Web/OpticaVision.Web.csproj

COPY . .
RUN dotnet publish src/OpticaVision.Web/OpticaVision.Web.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "OpticaVision.Web.dll"]
