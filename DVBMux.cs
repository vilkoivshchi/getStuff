
namespace getStuff
{
    internal class DVBMux
    {
        public DVBMux(int tsNum, string multicastAddress, int port, string interfaceAddress)
        {
            TransponderNumber = tsNum;
            MulticastAddress = multicastAddress;
            Port = port;
            InterfaceAddress = interfaceAddress;
        }
        private int _transponderNumber;
        private string _address;
        private int _port;
        private string _interfaceAddress;
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
        public int Port
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
    }
}
