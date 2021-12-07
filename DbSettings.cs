
namespace getStuff
{
    internal class DbSettings
    {
        internal DbSettings (string number, string dbHost, string dbPort, string dbName, string dbUser, string dbPassword, string dbStorePeriod)
        {
            DbNumber = number;
            DbHost = dbHost;
            DbPort = dbPort;
            DbName = dbName;
            DbUser = dbUser;
            DbPassword = dbPassword;
        }

        private string _dbNumber;
        private string _dbHost;
        private string _dbPort;
        private string _dbName;
        private string _dbUser;
        private string _dbPassword;
        private string _dbStorePeriod;

        public string DbNumber
        {
            get
            {
                return _dbNumber;
            }
            set
            {
                _dbNumber = value;
            }
        }
        public string DbHost
        {
            get
            {
                return _dbHost;
            }
            set
            {
                _dbHost = value;
            }
        }
        public string DbPort
        {
            get
            {
                return _dbPort;
            }
            set
            {
                _dbPort = value;
            }
        }
        public string DbName
        {
            get
            {
                return _dbName;
            }
            set
            {
                _dbName = value;
            }
        }
        public string DbUser
        {
            get
            {
                return _dbUser;
            }
            set
            {
                _dbUser = value;
            }
        }
        public string DbPassword 
        {
            get
            {
                return _dbPassword;
            }
            set
            {
                _dbPassword = value;
            }
        }
        public string DbStorePeriod
        {
            get
            {
                return _dbStorePeriod;
            }
            set
            {
                _dbStorePeriod = value;
            }
        }
    }
}
