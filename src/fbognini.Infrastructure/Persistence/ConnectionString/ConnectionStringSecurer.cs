using Microsoft.Data.SqlClient;
using fbognini.Infrastructure.Common;
using Microsoft.Extensions.Options;
using Npgsql;

namespace fbognini.Infrastructure.Persistence.ConnectionString
{

    internal class ConnectionStringSecurer : IConnectionStringSecurer
    {
        private const string HiddenValueDefault = "*******";
        private readonly DatabaseSettings dbSettings;

        public ConnectionStringSecurer(IOptions<DatabaseSettings> dbSettings) =>
            this.dbSettings = dbSettings.Value;

        public string MakeSecure(string connectionString, string? dbProvider = null)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return connectionString;
            }

            if (string.IsNullOrWhiteSpace(dbProvider))
            {
                dbProvider = dbSettings.DBProvider;
            }

            return dbProvider?.ToLower() switch
            {
                DbProviderKeys.Npgsql => MakeSecureNpgsqlConnectionString(connectionString),
                DbProviderKeys.SqlServer => MakeSecureSqlConnectionString(connectionString),
                _ => connectionString
            };
        }

        private string MakeSecureSqlConnectionString(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);

            if (!string.IsNullOrEmpty(builder.Password) || !builder.IntegratedSecurity)
            {
                builder.Password = HiddenValueDefault;
            }

            if (!string.IsNullOrEmpty(builder.UserID) || !builder.IntegratedSecurity)
            {
                builder.UserID = HiddenValueDefault;
            }

            return builder.ToString();
        }

        private string MakeSecureNpgsqlConnectionString(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);

            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = HiddenValueDefault;
            }

            if (!string.IsNullOrEmpty(builder.Username))
            {
                builder.Username = HiddenValueDefault;
            }

            return builder.ToString();
        }
    }

}