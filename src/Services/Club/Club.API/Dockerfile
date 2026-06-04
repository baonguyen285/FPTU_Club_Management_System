FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 9001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy projects and restore
COPY ["src/Shared/Shared.Kernel/Shared.Kernel.csproj", "src/Shared/Shared.Kernel/"]
COPY ["src/Services/Club/Club.Domain/Club.Domain.csproj", "src/Services/Club/Club.Domain/"]
COPY ["src/Services/Club/Club.Application/Club.Application.csproj", "src/Services/Club/Club.Application/"]
COPY ["src/Services/Club/Club.Infrastructure/Club.Infrastructure.csproj", "src/Services/Club/Club.Infrastructure/"]
COPY ["src/Services/Club/Club.API/Club.API.csproj", "src/Services/Club/Club.API/"]

RUN dotnet restore "src/Services/Club/Club.API/Club.API.csproj"

# Copy remaining source code
COPY . .

WORKDIR "/src/src/Services/Club/Club.API"
RUN dotnet build "Club.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Club.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Club.API.dll"]
