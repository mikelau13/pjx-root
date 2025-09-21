#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["pjx-api-dotnet.csproj", "pjx_api/"]
RUN dotnet restore "pjx_api/pjx-api-dotnet.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "pjx-api-dotnet.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "pjx-api-dotnet.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ADD ./pjx-sso-identityserver.rsa_2048.cert.crt /usr/local/share/ca-certificates/asp_dev/
RUN chmod -R 644 /usr/local/share/ca-certificates/asp_dev/
RUN update-ca-certificates --fresh
ENTRYPOINT ["dotnet", "Pjx_Api.dll"]