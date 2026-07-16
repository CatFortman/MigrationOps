using Microsoft.Data.SqlClient;

namespace MigrationOps.Dashboard.Data
{
    // Owns the dashboard's own login table (__DashboardUsers), kept in a dedicated
    // database separate from the migration-target databases in dbconfig.json.
    public class DashboardStoreService
    {
        private const string CreateUsersTable = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__DashboardUsers')
            BEGIN
                CREATE TABLE __DashboardUsers (
                    UserId INT PRIMARY KEY IDENTITY(1,1),
                    Username NVARCHAR(255) NOT NULL UNIQUE,
                    PasswordHash NVARCHAR(255) NOT NULL,
                    CreatedOn DATETIME NOT NULL
                );
            END";

        private readonly string _connectionString;

        public DashboardStoreService(IConfiguration configuration)
        {
            _connectionString = configuration["DashboardStore:ConnectionString"]
                ?? throw new InvalidOperationException("DashboardStore:ConnectionString is not configured.");

            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(CreateUsersTable, connection);
            command.ExecuteNonQuery();
        }

        public bool HasAnyUsers()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT COUNT(1) FROM __DashboardUsers", connection);
            return (int)command.ExecuteScalar() > 0;
        }

        public void CreateUser(string username, string password)
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "INSERT INTO __DashboardUsers (Username, PasswordHash, CreatedOn) VALUES (@Username, @PasswordHash, GETDATE())",
                connection);
            command.Parameters.AddWithValue("@Username", username);
            command.Parameters.AddWithValue("@PasswordHash", passwordHash);
            command.ExecuteNonQuery();
        }

        public bool ValidateCredentials(string username, string password)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "SELECT PasswordHash FROM __DashboardUsers WHERE Username = @Username",
                connection);
            command.Parameters.AddWithValue("@Username", username);

            var hash = command.ExecuteScalar() as string;
            return hash != null && BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}
