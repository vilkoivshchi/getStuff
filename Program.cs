﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Npgsql;

namespace getStuff
{
    class Program
    {
        private static string _dbHost = String.Empty;
        private static string _dbUser = String.Empty;
        private static string _dbName = String.Empty;
        private static string _dbPassword = String.Empty;
        private static string _dbPort = String.Empty;
        private static string _networkInterface = String.Empty;
        private static List<string> _networkInterfaces = new();

        static void Main(string[] args)
        {
            List<DVBMux> DVBMuxes = ParseConfig($"{AppContext.BaseDirectory}\\muxes.xml");
            MeasureStuffing(DVBMuxes);
        }

        static void MeasureStuffing(List<DVBMux> muxes)
        {
            List<Task> TaskList = new List<Task>();
            for(int i = 0; i < muxes.Count; i++)
            {
                Task task = Task.Run(() => RunMeasure(muxes[i]));
                TaskList.Add(task);

                while (task.Status != TaskStatus.Running)
                {
                    Thread.Sleep(100);
                }
                
            }
            Task.WaitAll(TaskList.ToArray());
        }

        static void StoreToDatabase(int tsNum, DateTime timeStamp, int stuffBitrate)
        {
            string connString = $"Server={_dbHost};Username={_dbUser};Database={_dbName};Port={_dbPort};Password={_dbPassword}";
            using (var conn = new NpgsqlConnection(connString))
            {
                Console.Out.WriteLine($"Opening connection to {_dbHost}");
                conn.Open();
                using (var command = new NpgsqlCommand($"CREATE TABLE IF NOT EXISTS TS{tsNum}(time TIMESTAMP WITHOUT TIME ZONE PRIMARY KEY, bitrate INTEGER)", conn))
                {
                    command.ExecuteNonQuery();
                    //Console.Out.WriteLine("Finished creating table");
                }
                using (var command = new NpgsqlCommand($"INSERT INTO TS{tsNum} (time, bitrate) VALUES (@n1, @q1)", conn))
                {
                    command.Parameters.AddWithValue("n1", NpgsqlTypes.NpgsqlDbType.Timestamp, timeStamp);
                    command.Parameters.AddWithValue("q1", stuffBitrate);

                    Console.Out.WriteLine(String.Format($"Write for ts{tsNum} complete. Number of rows inserted={command.ExecuteNonQuery()}"));
                }
                conn.Close();
            }
        }

        static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                Process sendProc = (Process)sendingProcess;
                string arguments = sendProc.StartInfo.Arguments;
                Regex argumentRegex = new Regex(@"--tag [\d]{1,}");
                MatchCollection argumentsCollection = argumentRegex.Matches(arguments);
                string tsNumberString = String.Empty;
                int tsNumber = 0;
                int bitrate = -1;
                string tsDuckOutput = outLine.Data;
                Regex OutputRegex = new Regex(@"bitrate: [\d]{0,}[\,]{0,}[\d]{0,}[\,]{0,}[\d]{0,}");
                MatchCollection OutputCollection = OutputRegex.Matches(tsDuckOutput);
                
                if (OutputCollection.Count > 0 && argumentsCollection.Count > 0)
                {
                    tsNumberString = Regex.Replace(argumentsCollection[0].Value, "--tag ", String.Empty);
                    int.TryParse(tsNumberString, out tsNumber);
                    DateTime timeStamp = DateTime.UtcNow;
                    string bitrateIteration1 = Regex.Replace(OutputCollection[0].Value, "bitrate: ", String.Empty);
                    string bitrateIteration2 = Regex.Replace(bitrateIteration1, ",", String.Empty);
                    int.TryParse(bitrateIteration2, out bitrate);
                    //Console.WriteLine($"time: {timeStamp}, ts{tsNumber}, bitrate: {bitrate}");
                    StoreToDatabase(tsNumber, timeStamp, bitrate);
                }
            }
        }

       

        static  void RunMeasure(DVBMux mux)
        {
            Console.WriteLine($"Begin measure ts{mux.TransponderNumber} at address {mux.Address}:{mux.Port}...");
            Process measureProc = new();

            measureProc.StartInfo.FileName = "tsp";
            measureProc.StartInfo.Arguments = $"--realtime -v -t -I ip {mux.Address}:{mux.Port} -l {_networkInterface} -P bitrate_monitor -p 1 -t 1 --pid 8191 --tag {mux.TransponderNumber} -O drop";
            measureProc.StartInfo.UseShellExecute = false;
            measureProc.StartInfo.RedirectStandardOutput = true;
            measureProc.StartInfo.RedirectStandardError = true;
            measureProc.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            measureProc.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
            measureProc.Start();
            measureProc.BeginOutputReadLine();
            measureProc.BeginErrorReadLine();
            measureProc.WaitForExit();
        }

        static List<DVBMux> ParseConfig(string path)
        {
            string xmlString = string.Empty;
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    xmlString = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Config not found");
                Console.WriteLine(e.Message);
            }
            List<DVBMux> muxes = new List<DVBMux>();
            XmlDocument configFile = new XmlDocument();
            configFile.LoadXml(xmlString);
            XmlElement xRoot = configFile.DocumentElement;
            
            foreach (XmlElement xNodes in xRoot)
            {
                foreach (XmlElement xElement in xNodes) 
                {
                    if (xElement.Attributes.Count > 0)
                    {
                        int num;
                        int port;
                        if(xElement.Name == "mux")
                        {
                            if (int.TryParse(xElement.GetAttribute("num"), out num) && int.TryParse(xElement.GetAttribute("port"), out port))
                            {
                                muxes.Add(new DVBMux(num, xElement.GetAttribute("ip"), port));
                            }
                        }
                        
                        if (xElement.Name == "interface")
                        {
                            _networkInterface = xElement.GetAttribute("address");
                            _networkInterfaces.Add(_networkInterface);
                        }

                        if (xElement.Name == "database")
                        {
                            _dbHost = xElement.GetAttribute("host");
                            _dbUser = xElement.GetAttribute("user");
                            _dbName = xElement.GetAttribute("dbname");
                            _dbPassword = xElement.GetAttribute("password");
                            _dbPort = xElement.GetAttribute("port");
                        }
                    }
                }
            }
            
            return muxes;
        }
    }

    public class DVBMux
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
