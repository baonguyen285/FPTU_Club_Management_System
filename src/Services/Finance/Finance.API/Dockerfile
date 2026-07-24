FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Shared/Shared.Kernel/Shared.Kernel.csproj", "src/Shared/Shared.Kernel/"]
COPY ["src/Services/Finance/Finance.Domain/Finance.Domain.csproj", "src/Services/Finance/Finance.Domain/"]
COPY ["src/Services/Finance/Finance.Application/Finance.Application.csproj", "src/Services/Finance/Finance.Application/"]
COPY ["src/Services/Finance/Finance.Infrastructure/Finance.Infrastructure.csproj", "src/Services/Finance/Finance.Infrastructure/"]
COPY ["src/Services/Finance/Finance.API/Finance.API.csproj", "src/Services/Finance/Finance.API/"]

RUN dotnet restore "src/Services/Finance/Finance.API/Finance.API.csproj"

COPY . .
WORKDIR "/src/src/Services/Finance/Finance.API"
RUN dotnet build "Finance.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Finance.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Finance.API.dll"]
