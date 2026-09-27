FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Chainbound.BattleService/ backend/Chainbound.BattleService/
COPY Assets/Scripts/Combat/Core/ Assets/Scripts/Combat/Core/
RUN dotnet publish backend/Chainbound.BattleService/Chainbound.BattleService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Chainbound.BattleService.dll"]
