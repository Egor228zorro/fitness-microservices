FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# context: ./UserService, поэтому пути без UserService/
COPY ["User.UI.Api/User.UI.Api.csproj", "User.UI.Api/"]
COPY ["User.Application/User.Application.csproj", "User.Application/"]
COPY ["User.Domain/User.Domain.csproj", "User.Domain/"]
COPY ["User.Infrastructure/User.Infrastructure.csproj", "User.Infrastructure/"]

RUN dotnet restore "User.UI.Api/User.UI.Api.csproj"

# Копируем весь код (UserService/ - это текущая директория)
COPY . .

WORKDIR "/src/User.UI.Api"
RUN dotnet build "User.UI.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "User.UI.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "User.UI.Api.dll"]
