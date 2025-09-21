#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["src/Pjx_Api/pjx-api-dotnet.csproj", "src/Pjx_Api/"]
COPY ["src/Pjx.CalendarLibrary/Pjx.CalendarLibrary.csproj", "src/Pjx.CalendarLibrary/"]
RUN dotnet restore "src/Pjx_Api/pjx-api-dotnet.csproj"
COPY . .
WORKDIR "/src/src/Pjx_Api"
RUN dotnet build "pjx-api-dotnet.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "pjx-api-dotnet.csproj" -c Release -o /app/publish
COPY /src/Pjx_Api/PjxCalendar.db /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ADD ./src/Pjx_Api/pjx-sso-identityserver.rsa_2048.cert.crt /usr/local/share/ca-certificates/asp_dev/
RUN chmod -R 644 /usr/local/share/ca-certificates/asp_dev/
RUN update-ca-certificates --fresh

ENTRYPOINT ["dotnet", "Pjx_Api.dll"]