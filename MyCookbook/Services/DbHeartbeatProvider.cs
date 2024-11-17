using Microsoft.Data.SqlClient;
using MyCookbook.Logging;
using MySql.Data.MySqlClient;
using Serilog;
using System.Reflection;

namespace MyCookbook.Services
{
    public class DbHeartbeatProvider
    {
        private MySqlConnection Connection { get; }
        private MySqlCommand Command { get; }

        public DbHeartbeatProvider(string connectionString)
        {
            Connection = new MySqlConnection(connectionString);
            Command = new MySqlCommand("Select 1", Connection);
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
                using (MySqlDataReader reader = Command.ExecuteReader())
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
