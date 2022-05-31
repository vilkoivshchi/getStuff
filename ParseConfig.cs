using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
        private const int ERROR_BAD_ARGUMENTS = 0xA0;
        private string _filepath = string.Empty;
        internal DbType databaseType = DbType.Sqlite;
        internal string DbPath = string.Empty;
        internal string DbHost = string.Empty;
        internal string DbUser = string.Empty;
        internal string DbName = string.Empty;
        internal string DbPassword = string.Empty;
        internal string DbPort = string.Empty;
        internal string NetworkInterface = string.Empty;
        internal string StorePeriod = string.Empty;
        internal int DbNumber = 0;
        internal List<DVBMux> Muxes = new();
        internal List<DatabaseSettings> DataBases = new();

        private string _xmlString = string.Empty;

        internal void ReadConfig()
        {
            if (File.Exists(_filepath))
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
                foreach (XmlElement xRootNodeChids in xNodes)
                {
                    if (xRootNodeChids.Attributes.Count > 0)
                    {

                        if (xRootNodeChids.Name == "interface")
                        {
                            NetworkInterface = xRootNodeChids.GetAttribute("address");

                            foreach (XmlElement xInterfaceChilds in xRootNodeChids)
                            {
                                foreach (XmlElement xMux in xInterfaceChilds)
                                {
                                    int tsNum;
                                    int port;
                                    int dbNum;
                                    if (xMux.Name == "mux")
                                    {
                                        if (int.TryParse(xMux.GetAttribute("num"), out tsNum) && int.TryParse(xMux.GetAttribute("port"), out port) && int.TryParse(xMux.GetAttribute("dbnumber"), out dbNum))
                                        {
                                            Muxes.Add(new DVBMux(tsNum, xMux.GetAttribute("ip"), port, NetworkInterface, dbNum));
                                        }
                                    }
                                }
                            }
                        }

                        if (xRootNodeChids.Name == "database")
                        {
                            string DbTypeString = xRootNodeChids.GetAttribute("type");
                            switch (DbTypeString)
                            {
                                case "postgres":

                                    DbHost = xRootNodeChids.GetAttribute("host");
                                    DbUser = xRootNodeChids.GetAttribute("user");
                                    DbName = xRootNodeChids.GetAttribute("dbname");
                                    DbPassword = xRootNodeChids.GetAttribute("password");
                                    DbPort = xRootNodeChids.GetAttribute("port");
                                    break;
                                case "sqlite":
                                    DbPath = xRootNodeChids.GetAttribute("path");

                                    break;
                            }

                            StorePeriod = xRootNodeChids.GetAttribute("storeperiod");
                            Regex timeSpanRegex = new Regex(@"^([\d]{1,}[\W]{0,})([\w]{1,})");
                            Match timeSpanMatch = timeSpanRegex.Match(StorePeriod);
                            int timeSpanInt = -1;
                            TimeSpan timeSpan = TimeSpan.Zero;
                            int.TryParse(timeSpanMatch.Groups[1].Value, out timeSpanInt);
                            if (timeSpanInt == -1)
                            {
                                Console.WriteLine($"Errot in storeperiod for database #{DbNumber}");
                                Environment.Exit(ERROR_BAD_ARGUMENTS);
                            }
                            
                            if(timeSpanMatch.Groups[2].Value == "minute" || timeSpanMatch.Groups[2].Value == "minutes")
                            {
                                timeSpan = TimeSpan.FromMinutes(timeSpanInt * 1);
                            }
                            else if(timeSpanMatch.Groups[2].Value == "hour" || timeSpanMatch.Groups[2].Value == "hours")
                            {
                                timeSpan = TimeSpan.FromMinutes(timeSpanInt * 60);
                            }
                            else if(timeSpanMatch.Groups[2].Value == "day" || timeSpanMatch.Groups[2].Value == "days")
                            {
                                timeSpan = TimeSpan.FromMinutes(timeSpanInt * 60 * 24);
                            }
                            
                            int.TryParse(xRootNodeChids.GetAttribute("number"), out DbNumber);
                        }
                        
                    }
                }
            }
            XmlNodeList xMuxesList = configFile.GetElementsByTagName("interfaces");
            /*
            XmlNodeList xDataBasesList = configFile.GetElementsByTagName("database");
            for (int i = 0; i < xDataBasesList.Count; i++)
            {
                XmlNode dataBaseNode = xDataBasesList.Item(i);
                
                XmlAttribute dbNumAttr = (XmlAttribute)dataBaseNode.Attributes.GetNamedItem("number");
                XmlAttribute dbHostNameAttr = (XmlAttribute)dataBaseNode.Attributes.GetNamedItem("host");
                XmlAttribute dbUserAttr = (XmlAttribute)dataBaseNode.Attributes.GetNamedItem("user");
                XmlAttribute dbNameAttr = (XmlAttribute)dataBaseNode.Attributes.GetNamedItem("dbname");
                XmlAttribute dbPasswordAttr = (XmlAttribute)dataBaseNode.Attributes.GetNamedItem("password");
                XmlAttribute dbPortAttr = (XmlAttribute)dataBaseNode.Attributes.GetNamedItem("port");
                XmlAttribute dbStorePeriod = (XmlAttribute)dataBaseNode.Attributes.GetNamedItem("storeperiod");
                PostgresSettings database = new(dbNumAttr.Value, dbHostNameAttr.Value, dbPortAttr.Value, dbNameAttr.Value, dbUserAttr.Value, dbPasswordAttr.Value, dbStorePeriod.Value);
                DataBases.Add(database);
            }
            */
        }

    }
}
