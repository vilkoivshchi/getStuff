using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Npgsql;

namespace getStuff
{
    class Program
    {
        const int ERROR_BAD_UNIT = 0x14;

        private static string Host = "10.253.55.21";
        private static string User = "stuffing";
        private static string DBname = "stuffing_db";
        private static string Password = "13691505";
        private static string Port = "5432";
        


        static void Main(string[] args)
        {
            Dictionary<int, bool> tunersList = GetDevicesList();
            if(tunersList.Count > 0)
            {
                List<DVBTransponder> DVBTransponders = ParseConfig($"{AppContext.BaseDirectory}\\transponders.xml");
                Console.WriteLine($"DVB-C tuners found: {tunersList.Count}");
                
                
                MeasureStuffing(tunersList, DVBTransponders, AppContext.BaseDirectory.ToString());
            }
            else
            {
                Environment.Exit(ERROR_BAD_UNIT);
            }
            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
        }

        static async void MeasureStuffing(Dictionary<int, bool> tuners, List<DVBTransponder> transponders, string tmpFolder)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // temp vars
            int currentTuner = 0;
            int currentTrasponder = 0;
            Dictionary<int, Task> TaskDict = new Dictionary<int, Task>();
            List<Task> TaskList = new List<Task>();
            Task[] TaskArr;
            while (true)
            {
                foreach (KeyValuePair<int, bool> kvp in tuners)
                {
                    if (kvp.Value == false)
                    {
                        currentTuner = kvp.Key;
                        tuners[kvp.Key] = true;

                        string tmpFileName = $"{tmpFolder}\\TS{transponders[currentTrasponder].TransponderNumber}.tmp";
                        Console.WriteLine($"Begin measure ts{transponders[currentTrasponder].TransponderNumber} at tuner #{currentTuner}...");

                         Task task = Task.Run(() => RunMeasure(currentTuner, transponders[currentTrasponder], tmpFileName));
                         //await Task.Run(() => RunMeasure(currentTuner, transponders[currentTrasponder], tmpFileName));
                        //currentTrasponder++;
                        //tuners[kvp.Key] = false;
                        //if (currentTrasponder == transponders.Count) currentTrasponder = 0;
                        //TaskDict.Add(kvp.Key, task);
                        TaskList.Add(task);
                        TaskArr = TaskList.ToArray();
                        //List<Task<int>> Tasks = TaskDict 

                        //Task FinishedTask = await Task.WaitAny(TaskArr);
                        /*
                        foreach(KeyValuePair<int, Task> taskKvp in TaskList)
                        {
                            if(taskKvp.Value == Task.CompletedTask)
                            {
                                currentTrasponder++;
                                tuners[kvp.Key] = false;
                                if (currentTrasponder == transponders.Count) currentTrasponder = 0;
                                Console.WriteLine($"task {taskKvp.Value.Id} complete");
                            }
                            
                        }
                        */
                    }
                }
            }
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

        static void RunMeasure(int tuner, DVBTransponder transponder, string tempFile)
        {
            Process measureProc = new Process();
            measureProc.StartInfo.FileName = "tsp";
            //measureProc.StartInfo.Arguments = $"-I dvb -d :{tuner} --bandwidth 8 --delivery-system DVB-C --frequency {transponder.Frequency} --modulation {transponder.Modulation}-QAM -P skip 500 -P until -s 1 -P analyze --normalized -o {tempFile} -O drop";
            measureProc.StartInfo.Arguments = $"--realtime -v -t -I ip {} -P bitrate_monitor -p 1 --pid 8191 -O drop";
            measureProc.StartInfo.UseShellExecute = false;
            measureProc.StartInfo.RedirectStandardOutput = true;
            measureProc.Start();
            while (!measureProc.StandardOutput.EndOfStream)
            {
                Console.WriteLine(measureProc.StandardOutput.ReadLine());
            }
            try
            {
                DateTime timeStamp = DateTime.UtcNow;
                string stuffingLine = String.Empty;
                string stuffingBitrateLine = String.Empty;
                int bitrate = -1;
                using (StreamReader tmpReader = new StreamReader(tempFile))
                {
                    List<string> tmpFileContent = new List<string>();

                    while (tmpReader.Peek() >= 0)
                    {
                        tmpFileContent.Add(tmpReader.ReadLine());
                    }

                    foreach (string line in tmpFileContent)
                    {
                        Regex tmpRegex = new Regex(@":pid=8191:");
                        MatchCollection pidMatches = tmpRegex.Matches(line);
                        if (pidMatches.Count > 0)
                        {
                            stuffingLine = line;
                        }
                    }

                    if (stuffingLine.Length > 0)
                    {
                        Regex pidRegex = new Regex(@"bitrate=[\d]{1,}");
                        MatchCollection pidMatchCollection = pidRegex.Matches(stuffingLine);
                        stuffingBitrateLine = Regex.Replace(pidMatchCollection[0].Value, "bitrate=", String.Empty);
                        int.TryParse(stuffingBitrateLine, out bitrate);

                    }
                }
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
                Console.WriteLine($"date:{timeStamp}, bitrate:{bitrate}");
                if (bitrate >= 0)
                {
                    StoreToDatabase(transponder.TransponderNumber, timeStamp, bitrate);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{tempFile} not found");
                Console.WriteLine(e.Message);
            }
            //Task.WaitAll();
            Console.WriteLine($"Measure of ts{transponder.TransponderNumber} complete");
        }
        static Dictionary<int, bool> GetDevicesList()
        {
            Process devlist = new Process();
            devlist.StartInfo.FileName = "tslsdvb";
            devlist.StartInfo.UseShellExecute = false;
            devlist.StartInfo.RedirectStandardOutput = true;
            Dictionary<int, bool> devicesList = new Dictionary<int, bool>();
            devlist.Start();
            while (!devlist.StandardOutput.EndOfStream)
            {
                string currentStr = devlist.StandardOutput.ReadLine();
                Regex regex = new Regex(@"DVB-C", RegexOptions.IgnoreCase);
                Match tunersMatch = regex.Match(currentStr);
                if (tunersMatch.Success)
                {
                    Regex getTunerNumRegex = new Regex(@"^[\d]{1,}");
                    MatchCollection tunersMatchCollection = getTunerNumRegex.Matches(currentStr);
                    int tunerNumber;
                    int.TryParse(tunersMatchCollection[0].Value, out tunerNumber);
                    devicesList.Add(tunerNumber, false);
                }
            }
            return devicesList;
        }

        static List<DVBTransponder> ParseConfig(string path)
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
            List<DVBTransponder> transponders = new List<DVBTransponder>();
            XmlDocument configFile = new XmlDocument();
            configFile.LoadXml(xmlString);
            XmlElement xRoot = configFile.DocumentElement;
            foreach (XmlElement xNode in xRoot)
            {
                if(xNode.Attributes.Count > 0)
                {
                    int num;
                    int freq;
                    int qam;
                    
                    if(int.TryParse(xNode.GetAttribute("num"), out num) && int.TryParse(xNode.GetAttribute("freq"), out freq) && int.TryParse(xNode.GetAttribute("qam"), out qam))
                    {
                        transponders.Add(new DVBTransponder(num, freq, qam));
                    }
                }
            }
            return transponders;
        }
    }

    public class DVBTransponder
    {
        public DVBTransponder(int tsNum, int freq, int qam)
        {
            TransponderNumber = tsNum;
            Frequency = freq;
            Modulation = qam;
        }
        private int _transponderNumber;
        private int _frequency;
        private int _modultaion;
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
        public int Frequency
        {
            get
            {
                return _frequency;
            }
            set
            {
                _frequency = value;
            }
        }
        public int Modulation
        {
            get
            {
                return _modultaion;
            }
            set
            {
                _modultaion = value;
            }
        }
    }
}
