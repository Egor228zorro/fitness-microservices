FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src


# 2. ИЛИ монтируем как volume (в docker-compose.yml)
# volumes:
#   - ~/.nuget/packages/pepegov.unitofwork:/root/.nuget/packages/pepegov.unitofwork:ro
#   - ~/.nuget/packages/pepegov.unitofwork.entityframework:/root/.nuget/packages/pepegov.unitofwork.entityframework:ro

# 3. Копируем файлы проекта
COPY ["PublicWorkout.UI.Api/PublicWorkout.UI.Api.csproj", "PublicWorkout.UI.Api/"]
COPY ["PublicWorkout.Application/PublicWorkout.Application.csproj", "PublicWorkout.Application/"]
COPY ["PublicWorkout.Domain/PublicWorkout.Domain.csproj", "PublicWorkout.Domain/"]
COPY ["PublicWorkout.Infrastructure/PublicWorkout.Infrastructure.csproj", "PublicWorkout.Infrastructure/"]

# 4. Восстанавливаем зависимости
RUN dotnet restore "PublicWorkout.UI.Api/PublicWorkout.UI.Api.csproj"

# 5. Копируем остальной код
COPY . .

WORKDIR "/src/PublicWorkout.UI.Api"
RUN dotnet build "PublicWorkout.UI.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PublicWorkout.UI.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /logs && chmod 777 /logs
ENTRYPOINT ["dotnet", "PublicWorkout.UI.Api.dll"]