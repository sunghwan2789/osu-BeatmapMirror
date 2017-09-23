using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Manager;

namespace Summary
{
    class Program
    {
        static void Main(string[] args)
        {
            if (DateTime.Now.Subtract(Settings.LastSummaryTime).TotalDays <= 1.0)
            {
                return;
            }

            var CLAUSE = $"WHERE `at` <= '{DateTime.Now.AddDays(-1.0).ToString("yyyy-MM-dd")}'";

            using (var conn = DB.Connect())
            using (var tr = conn.BeginTransaction())
            using (var query = conn.CreateCommand())
            {
                query.CommandTimeout = 0;

                query.CommandText = $@"INSERT INTO `gosu_download_summary` (`setId`, `date`, `downloads`)
                    SELECT `setId`, `at`, COUNT(DISTINCT `ip`, `at`) FROM `gosu_downloads`
                    {CLAUSE} GROUP BY `setId`, `at`";
                Console.Write("insert ");
                var result = query.ExecuteNonQuery();
                Console.WriteLine(result + " rows");

                if (result > 0)
                {
                    query.CommandText = $"DELETE FROM `gosu_downloads` {CLAUSE}";
                    Console.Write("delete ");
                    Console.WriteLine(query.ExecuteNonQuery() + "rows");
                }

                tr.Commit();
            }

            Settings.LastSummaryTime = DateTime.Now.Date;
            /*
            set_time_limit(0);
            $clause = 'WHERE `at` <= '.date('Y-m-d', strtotime('-1 day'));
            $result = PDB::$conn->prepare('INSERT INTO `gosu_download_summary` (`setId`, `date`, `downloads`) '.
            'SELECT `setId`, `at`, COUNT(DISTINCT `ip`, `at`) FROM `gosu_downloads` '.
            $clause. ' GROUP BY `setId`')->execute();
            if ($result)
            {
                PDB::$conn->prepare('DELETE FROM `gosu_downloads` '. $clause)->execute();
            }
            */
        }
    }
}
