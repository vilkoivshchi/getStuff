using System;
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
        private static string Host = "10.253.55.21";
        private static string User = "stuffing";
        private static string DBname = "stuffing_db";
        private static string Password = "13691505";
        private static string Port = "5432";
        
        static void Main(string[] args)
        {
            List<DVBMux> DVBMuxes = ParseConfig($"{AppContext.BaseDirectory}\\muxes.xml");
            MeasureStuffing(DVBMuxes);
        }

        static void MeasureStuffing(List<DVBMux> muxes)
        {
            List<Task> TaskList = new List<Task>();
            for(int i = 0; i < 3; i++)
            {
                Console.WriteLine(muxes[i].Address);
                TaskList.Add(Task.Run(() => RunMeasure(muxes[i])));
                
                while(TaskList[i].Status != TaskStatus.Running)
                {
                    Thread.Sleep(100);
                }
            }
                Task.WaitAll(TaskList.ToArray());
        }

        static void StoreToDatabase(int tsNum, DateTime timeStamp, int stuffBitrate)
        {
            string connString = $"Server={Host};Username={User};Database={DBname};Port={Port};Password={Password}";
            using (var conn = new NpgsqlConnection(connString))
            {
                Console.Out.WriteLine($"Opening connection to {Host}");
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

                    Console.Out.WriteLine(String.Format($"Number of rows inserted={command.ExecuteNonQuery()}"));
                }
            }
        }

        static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (outLine.Data.Length > 0)
            {
                Process sendProc = (Process)sendingProcess;
                string arguments = sendProc.StartInfo.Arguments;
                Regex argumentRegex = new Regex(@"--set-label-normal [\d]{1,}");
                MatchCollection argumentsCollection = argumentRegex.Matches(arguments);
                string tsNumberString = String.Empty;
                int tsNumber = 0;
                int bitrate = -1;
                string tsDuckOutput = outLine.Data;
                Regex OutputRegex = new Regex(@"bitrate: [\d]{0,}[\,]{0,}[\d]{0,}[\,]{0,}[\d]{0,}");
                MatchCollection OutputCollection = OutputRegex.Matches(tsDuckOutput);
                
                if (OutputCollection.Count > 0 && argumentsCollection.Count > 0)
                {
                    tsNumberString = Regex.Replace(argumentsCollection[0].Value, "--set-label-normal ", String.Empty);
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
            Console.WriteLine($"Begin measure ts{mux.TransponderNumber} at address {mux.Address}...");
            Process measureProc = new();

            measureProc.StartInfo.FileName = "tsp";
            measureProc.StartInfo.Arguments = $"--realtime -v -t -I ip {mux.Address}:{mux.Port} -P bitrate_monitor -p 1 -t 1 --pid 8191 --set-label-normal {mux.TransponderNumber} -O drop";
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
            foreach (XmlElement xNode in xRoot)
            {
                if(xNode.Attributes.Count > 0)
                {
                    int num;
                    int port;
                    
                    if(int.TryParse(xNode.GetAttribute("num"), out num) && int.TryParse(xNode.GetAttribute("port"), out port))
                    {
                        muxes.Add(new DVBMux(num, xNode.GetAttribute("ip"), port));
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
