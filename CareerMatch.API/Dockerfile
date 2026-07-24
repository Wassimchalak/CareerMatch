FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

WORKDIR /app

EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["CareerMatch.API.csproj", "./"]

RUN dotnet restore "CareerMatch.API.csproj"

COPY . .

RUN dotnet publish "CareerMatch.API.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}

ENTRYPOINT ["dotnet", "CareerMatch.API.dll"]