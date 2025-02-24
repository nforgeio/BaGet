FROM mcr.microsoft.com/dotnet/aspnet:6.0.6-jammy-amd64 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0.400-jammy-amd64 AS build
WORKDIR /src
COPY /src .
RUN dotnet restore BaGet
RUN dotnet build BaGet -c Release -o /app

FROM build AS publish
RUN dotnet publish BaGet -c Release -o /app

FROM base AS final
LABEL org.opencontainers.image.source="https://github.com/nforgeio/baget"
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "BaGet.dll"]
