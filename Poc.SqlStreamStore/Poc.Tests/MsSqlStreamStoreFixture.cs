namespace SqlStreamStore
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using SqlStreamStore.Infrastructure;

    public class MsSqlStreamStoreFixture : StreamStoreAcceptanceTestFixture
    {
        public readonly string ConnectionString;
        private readonly string _schema;
        private readonly bool _deleteDatabaseOnDispose;
        public readonly string DatabaseName;
        private readonly ISqlServerDatabase _localInstance;

        public MsSqlStreamStoreFixture(string schema, bool deleteDatabaseOnDispose = false)
        {
            _schema = "es";
            _deleteDatabaseOnDispose = deleteDatabaseOnDispose;
            DatabaseName = $"StreamStoreTests";
            //DatabaseName = $"StreamStoreTests-{Guid.NewGuid():n}";
            _localInstance =  (ISqlServerDatabase) new LocalSqlServerDatabase(DatabaseName);

            ConnectionString = CreateConnectionString();
        }

        public override long MinPosition => 0;

        public override async Task<IStreamStore> GetStreamStore()
        {
            await CreateDatabase();

            return await GetStreamStore(_schema);
        }

        public async Task<IStreamStore> GetStreamStore(string schema)
        {
            var settings = new MsSqlStreamStoreSettings(ConnectionString)
            {
                Schema = schema,
                GetUtcNow = () => GetUtcNow()
            };
            var store = new MsSqlStreamStore(settings);

            await store.CreateSchema();

            return store;
        }

       
        public async Task<MsSqlStreamStore> GetUninitializedStreamStore()
        {
            await CreateDatabase();
            
            return new MsSqlStreamStore(new MsSqlStreamStoreSettings(ConnectionString)
            {
                Schema = _schema,
                GetUtcNow = () => GetUtcNow()
            });
        }

        public async Task<MsSqlStreamStore> GetMsSqlStreamStore()
        {
            await CreateDatabase();

            var settings = new MsSqlStreamStoreSettings(ConnectionString)
            {
                Schema = _schema,
                GetUtcNow = () => GetUtcNow()
            };

            var store = new MsSqlStreamStore(settings);
            await store.CreateSchema();
            return store;
        }

        public override void Dispose()
        {
            if(!_deleteDatabaseOnDispose)
            {
                return;
            }
            using(var sqlConnection = new SqlConnection(ConnectionString))
            {
                // Fixes: "Cannot drop database because it is currently in use"
                SqlConnection.ClearPool(sqlConnection);
            }
            using (var connection = _localInstance.CreateConnection())
            {
                connection.Open();
                using (var command = new SqlCommand($"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", connection))
                {
                    command.ExecuteNonQuery();
                }
                using (var command = new SqlCommand($"DROP DATABASE [{DatabaseName}]", connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private Task CreateDatabase() => _localInstance.CreateDatabase();

        private string CreateConnectionString()
        {
            var connectionStringBuilder = _localInstance.CreateConnectionStringBuilder();
            connectionStringBuilder.MultipleActiveResultSets = true;
            connectionStringBuilder.InitialCatalog = DatabaseName;

            return connectionStringBuilder.ToString();
        }

        private interface ISqlServerDatabase
        {
            SqlConnection CreateConnection();
            SqlConnectionStringBuilder CreateConnectionStringBuilder();
            Task CreateDatabase(CancellationToken cancellationToken = default);
        }

        private class LocalSqlServerDatabase : ISqlServerDatabase
        {
            private readonly string _databaseName;
            private readonly string _connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=SSPI;";

            public LocalSqlServerDatabase(string databaseName)
            {
                _databaseName = databaseName;
            }
            public SqlConnection CreateConnection()
            {
                return new SqlConnection(_connectionString);
            }

            public SqlConnectionStringBuilder CreateConnectionStringBuilder()
            {
                return new SqlConnectionStringBuilder(_connectionString);
            }

            public async Task CreateDatabase(CancellationToken cancellationToken = default)
            {
                using (var connection = CreateConnection())
                {
                    await connection.OpenAsync(cancellationToken).NotOnCapturedContext();
                    var tempPath = Environment.GetEnvironmentVariable("Temp");
                    var createDatabase = $"CREATE DATABASE [{_databaseName}] on (name='{_databaseName}', "
                                         + $"filename='{tempPath}\\{_databaseName}.mdf')";
                    using (var command = new SqlCommand(createDatabase, connection))
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken).NotOnCapturedContext();
                    }
                }
            }
        }

    
    }
}