FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY backend/InformesAvanzar.Api.csproj backend/
RUN dotnet restore backend/InformesAvanzar.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/InformesAvanzar.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "InformesAvanzar.Api.dll"]
