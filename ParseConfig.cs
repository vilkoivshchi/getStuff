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
            try
            {
                using (StreamReader reader = new StreamReader(_filepath))
                {
                    _xmlString = reader.ReadToEnd();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Config not found");
                Console.WriteLine(e.Message);
            }

            //List<DVBMux> muxes = new List<DVBMux>();
            XmlDocument configFile = new XmlDocument();
            configFile.LoadXml(_xmlString);
            XmlElement xRoot = configFile.DocumentElement;

            foreach (XmlElement xNodes in xRoot)
            {
                foreach (XmlElement xElement in xNodes)
                {
                    if (xElement.Attributes.Count > 0)
                    {
                        int num;
                        int port;
                        if (xElement.Name == "mux")
                        {
                            if (int.TryParse(xElement.GetAttribute("num"), out num) && int.TryParse(xElement.GetAttribute("port"), out port))
                            {
                                muxes.Add(new DVBMux(num, xElement.GetAttribute("ip"), port));
                            }
                        }

                        if (xElement.Name == "interface")
                        {
                            NetworkInterface = xElement.GetAttribute("address");
                            NetworkInterfaces.Add(NetworkInterface);
                        }

                        if (xElement.Name == "database")
                        {
                            DbHost = xElement.GetAttribute("host");
                            DbUser = xElement.GetAttribute("user");
                            DbName = xElement.GetAttribute("dbname");
                            DbPassword = xElement.GetAttribute("password");
                            DbPort = xElement.GetAttribute("port");
                            StorePeriod = xElement.GetAttribute("storeperiod");
                        }
                    }
                }
            }

        }

    }
}
