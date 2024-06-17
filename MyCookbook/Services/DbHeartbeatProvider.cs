using Microsoft.Data.SqlClient;

namespace MyCookbook.Services
{
    public class DbHeartbeatProvider
    {
        private SqlConnection Connection { get; }
        private SqlCommand Command { get; }

        public DbHeartbeatProvider(string connectionString)
        {
            Connection = new SqlConnection(connectionString);
            Command = new SqlCommand("Select 1", Connection);
        }

        public void Start()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    SendHeartbeat();
                    Thread.Sleep(TimeSpan.FromMinutes(5));
                }
            });
        }

        private void SendHeartbeat()
        {
            Connection.Open();
            using (SqlDataReader reader = Command.ExecuteReader())
            {
                reader.Read();
            }
            Connection.Close();
        }
    }
}
