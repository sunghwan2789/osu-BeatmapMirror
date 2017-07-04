using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manager
{
    public class DB
    {
        private static string ConnectionString = null;

        public static MySqlCommand Command
        {
            get
            {
                if (ConnectionString == null)
                {
                    ConnectionString = string.Format("Server={0};UserId={1};Password={2};Database={3}",
                        Settings.DBServer, Settings.DBUserId, Settings.DBPassword, Settings.DBDatabase);
                }
                var conn = new MySqlConnection(ConnectionString);
                conn.Open();
                return new MySqlCommand
                {
                    Connection = conn,
                    CommandType = CommandType.Text,
                    CommandTimeout = 0
                };
            }
        }
    }
}