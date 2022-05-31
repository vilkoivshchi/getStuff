
using System;

namespace getStuff
{
    internal class DatabaseSettings
    {
        internal DatabaseSettings (string number, TimeSpan dbStorePeriod, string dbHost = "", string dbPort = "", string dbName = "", string dbUser = "", string dbPassword = "", string dbPath = "")
        {
            DbNumber = number;
            DbHost = dbHost;
            DbPort = dbPort;
            DbName = dbName;
            DbUser = dbUser;
            DbPassword = dbPassword;
            DbStorePeriod = dbStorePeriod;
            DbPath = dbPath;
        }

        internal string DbNumber { get; set; }
        internal string DbHost { get; set; }
        internal string DbPort { get; set; }
        internal string DbName { get; set; }
        internal string DbUser { get; set; }
        internal string DbPassword { get; set; }
        internal string DbPath { get; set; }
        internal TimeSpan DbStorePeriod { get; set; }
    }
}
