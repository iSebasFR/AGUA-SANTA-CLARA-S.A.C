FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["AguaSantaClara.Web/AguaSantaClara.Web.csproj", "AguaSantaClara.Web/"]
RUN dotnet restore "AguaSantaClara.Web/AguaSantaClara.Web.csproj"

COPY . .
RUN dotnet publish "AguaSantaClara.Web/AguaSantaClara.Web.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "AguaSantaClara.Web.dll"]

