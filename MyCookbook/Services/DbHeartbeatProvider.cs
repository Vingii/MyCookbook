using Microsoft.Data.SqlClient;
using MyCookbook.Logging;
using Serilog;
using System.Reflection;

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
            using var logger = new TimeLogger(MethodBase.GetCurrentMethod());
            try
            {
                Connection.Open();
                using (SqlDataReader reader = Command.ExecuteReader())
                {
                    reader.Read();
                }
                Connection.Close();
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Error sending SQL heartbeat.");
            }
        }
    }
}
