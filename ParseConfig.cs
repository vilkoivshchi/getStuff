using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace getStuff
{
    internal class ParseConfig
    {
        internal ParseConfig(string path)
        {
            _filepath = path;
        }
        private const int ERROR_FILE_NOT_FOUND = 0x2;
        private string _filepath = string.Empty;
        internal string DbHost = string.Empty;
        internal string DbUser = string.Empty;
        internal string DbName = string.Empty;
        internal string DbPassword = string.Empty;
        internal string DbPort = string.Empty;
        internal string NetworkInterface = string.Empty;
        internal List<string> NetworkInterfaces = new();
        internal string StorePeriod = string.Empty;
        internal List<DVBMux> muxes = new();

        private string _xmlString = string.Empty;

        internal void ReadConfig()
        {
            if(File.Exists(_filepath))
                using (StreamReader reader = new StreamReader(_filepath))
                {
                    _xmlString = reader.ReadToEnd();
                }

            else
            {
                //throw new FileNotFoundException($"Config {_filepath} not found!");
                Console.WriteLine($"Config {_filepath} not found!");
                Environment.Exit(ERROR_FILE_NOT_FOUND);
            }

            XmlDocument configFile = new XmlDocument();
            configFile.LoadXml(_xmlString);
            XmlElement xRoot = configFile.DocumentElement;

            foreach (XmlElement xNodes in xRoot)
            {
                foreach (XmlElement xInterface in xNodes)
                {
                    if (xInterface.Attributes.Count > 0)
                    {
                        
                        if (xInterface.Name == "interface")
                        {
                            NetworkInterface = xInterface.GetAttribute("address");
                            
                            foreach(XmlElement xInterfaceChilds in xInterface)
                            {
                                foreach(XmlElement xMux in xInterfaceChilds)
                                {
                                    int tsNum;
                                    int port;
                                    if (xMux.Name == "mux")
                                    {
                                        if (int.TryParse(xMux.GetAttribute("num"), out tsNum) && int.TryParse(xMux.GetAttribute("port"), out port))
                                        {
                                            muxes.Add(new DVBMux(tsNum, xMux.GetAttribute("ip"), port, NetworkInterface));
                                        }
                                    }
                                }
                            }
                            //NetworkInterfaces.Add(NetworkInterface);
                        }

                        if (xInterface.Name == "database")
                        {
                            DbHost = xInterface.GetAttribute("host");
                            DbUser = xInterface.GetAttribute("user");
                            DbName = xInterface.GetAttribute("dbname");
                            DbPassword = xInterface.GetAttribute("password");
                            DbPort = xInterface.GetAttribute("port");
                            StorePeriod = xInterface.GetAttribute("storeperiod");
                        }
                    }
                }
            }

        }

    }
}
