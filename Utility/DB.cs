using MySql.Data.MySqlClient;
using System.Data;

namespace Utility
{
    public class DB
    {
        public static MySqlConnection Connect()
        {
            var conn = new MySqlConnection(Settings.DBConnectionString);
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