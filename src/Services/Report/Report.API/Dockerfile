FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy projects and restore
COPY ["src/Shared/Shared.Kernel/Shared.Kernel.csproj", "src/Shared/Shared.Kernel/"]
COPY ["src/Services/Report/Report.Domain/Report.Domain.csproj", "src/Services/Report/Report.Domain/"]
COPY ["src/Services/Report/Report.Application/Report.Application.csproj", "src/Services/Report/Report.Application/"]
COPY ["src/Services/Report/Report.Infrastructure/Report.Infrastructure.csproj", "src/Services/Report/Report.Infrastructure/"]
COPY ["src/Services/Report/Report.API/Report.API.csproj", "src/Services/Report/Report.API/"]

RUN dotnet restore "src/Services/Report/Report.API/Report.API.csproj"

# Copy remaining source code
COPY . .

WORKDIR "/src/src/Services/Report/Report.API"
RUN dotnet build "Report.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Report.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Report.API.dll"]
