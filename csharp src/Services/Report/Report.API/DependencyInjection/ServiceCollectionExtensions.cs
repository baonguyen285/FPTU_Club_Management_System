using System;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Report.Infrastructure.Messaging;

namespace Report.API.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddReportInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Redis
            var redisConn = configuration.GetConnectionString("Redis");
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<IEventPublisher, RedisEventPublisher>();

            // Hangfire (SQL Server storage example)
            var sqlConn = configuration.GetConnectionString("DefaultConnection");
            services.AddHangfire(x => x.UseSqlServerStorage(sqlConn));
            services.AddHangfireServer();

            // gRPC client registration placeholder (adjust generated client type/namespace)
            var clubGrpcUrl = configuration["GrpcSettings:ClubServiceUrl"];
            if (!string.IsNullOrEmpty(clubGrpcUrl))
            {
                services.AddGrpcClient<Club.Grpc.ClubService.ClubServiceClient>(o => o.Address = new Uri(clubGrpcUrl));
                services.AddSingleton<IClubGrpcClient, ClubGrpcClient>();
            }

            return services;
        }
    }
}