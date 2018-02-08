using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class DB
    {
        private static string connectionString;
        private static string ConnectionString => connectionString
            ?? (connectionString = new MySqlConnectionStringBuilder
            {
                Server = Settings.DBServer,
                ConnectionProtocol = Settings.DBProtocol,
                UserID = Settings.DBUserId,
                Password = Settings.DBPassword,
                Database = Settings.DBDatabase,
            }.ConnectionString);

        public static MySqlConnection Connect()
        {
            var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static MySqlCommand Command
        {
            get
            {
                return new MySqlCommand
                {
                    Connection = Connect(),
                    CommandType = CommandType.Text,
                    CommandTimeout = 0
                };
            }
        }
    }
}
