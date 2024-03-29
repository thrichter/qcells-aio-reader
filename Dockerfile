
FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY qcells-aio-reader/qcells-aio-reader.csproj qcells-aio-reader/
RUN dotnet restore "qcells-aio-reader/qcells-aio-reader.csproj"
COPY . .
WORKDIR "/src/qcells-aio-reader"
RUN dotnet build "qcells-aio-reader.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "qcells-aio-reader.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "qcells-aio-reader.dll"]
