using fbognini.Infrastructure.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Reflection;

namespace fbognini.Infrastructure.Persistence.ConnectionString
{
    public static class ConnectionStringExtensions
    {
        private const string ApplicationNameKeyword = "Application Name";

        public static string? GetDatabaseConnectionString(this IConfiguration configuration)
        {
            var databaseSettings = configuration.GetSection(nameof(DatabaseSettings));

            return databaseSettings[nameof(DatabaseSettings.ConnectionString)]
                .WithApplicationName(databaseSettings[nameof(DatabaseSettings.DBProvider)]);
        }

        public static string? WithApplicationName(this string? connectionString, string? dbProvider = DbProviderKeys.SqlServer)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            var applicationName = Assembly.GetEntryAssembly()?.GetName().Name;
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                return connectionString;
            }

            return dbProvider?.ToLower() switch
            {
                DbProviderKeys.SqlServer => AddApplicationNameToSqlConnectionString(connectionString, applicationName),
                _ => connectionString
            };

        }

        private static string AddApplicationNameToSqlConnectionString(string connectionString, string applicationName)
        {

            var builder = new SqlConnectionStringBuilder(connectionString);
            if (builder.ShouldSerialize(ApplicationNameKeyword))
            {
                return connectionString;
            }

            builder.ApplicationName = applicationName;

            return builder.ConnectionString;
        }
    }
}
