
namespace getStuff
{
    internal class DVBMux
    {
        public DVBMux(int tsNum, string address, int port)
        {
            TransponderNumber = tsNum;
            Address = address;
            Port = port;
        }
        private int _transponderNumber;
        private string _address;
        private int _port;
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
        public string Address
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
    }
}
