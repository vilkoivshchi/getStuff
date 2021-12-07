
namespace getStuff
{
    internal class DVBMux
    {
        public DVBMux(int tsNum, string multicastAddress, int multicasPort, string interfaceAddress, int dbNumber)
        {
            TransponderNumber = tsNum;
            MulticastAddress = multicastAddress;
            MulticastPort = multicasPort;
            InterfaceAddress = interfaceAddress;
            DbNumber = dbNumber;
        }
        private int _transponderNumber;
        private string _address;
        private int _port;
        private string _interfaceAddress;
        private int _dbNumber;

        public int TransponderNumber
        {
            get
            {
                return _transponderNumber;
            }
            set
            {
                _transponderNumber = value;
            }
        }
        public string MulticastAddress
        {
            get
            {
                return _address;
            }
            set
            {
                _address = value;
            }
        }
        public int MulticastPort
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
            }
        }
        public string InterfaceAddress
        {
            get
            {
                return _interfaceAddress;
            }
            set
            {
                _interfaceAddress = value;
            }
        }
        public int DbNumber
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
    }
}
