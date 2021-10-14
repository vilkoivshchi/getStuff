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

            // temp vars
            int currentTuner = 0;
            int currentTrasponder = 0;
            while (true)
            {
                foreach (KeyValuePair<int, bool> kvp in tuners)
                {
                    if (kvp.Value == false)
                    {
                        currentTuner = kvp.Key;
                        tuners[kvp.Key] = true;


                        DateTime timeStamp = DateTime.UtcNow;
                        string tmpFileName = $"{tmpFolder}\\TS{transponders[currentTrasponder].TransponderNumber}.tmp";
                        Console.WriteLine($"Begin measure ts{transponders[currentTrasponder].TransponderNumber} at tuner #{currentTuner}...");

                        await Task.Run(() => RunMeasure(currentTuner, transponders[currentTrasponder], tmpFileName));
                        try
                        {
                            string stuffingLine = String.Empty;
                            string stuffingBitrateLine = String.Empty;
                            int bitrate = -1;
                            using (StreamReader tmpReader = new StreamReader(tmpFileName))
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
                            if (File.Exists(tmpFileName))
                            {
                                File.Delete(tmpFileName);
                            }
                            Console.WriteLine($"date:{timeStamp}, bitrate:{bitrate}");
                            if (bitrate >= 0)
                            {
                                StoreToDatabase(transponders[currentTrasponder].TransponderNumber, timeStamp, bitrate);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"{tmpFileName} not found");
                            Console.WriteLine(e.Message);
                        }
                        //Task.WaitAll();
                        Console.WriteLine($"Measure of ts{transponders[currentTrasponder].TransponderNumber} complete");
                        currentTrasponder++;
                        tuners[kvp.Key] = false;
                        if (currentTrasponder == transponders.Count - 1) currentTrasponder = 0;
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
                    Console.Out.WriteLine("Finished creating table");
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
            measureProc.StartInfo.Arguments = $"-I dvb -d :{tuner} --bandwidth 8 --delivery-system DVB-C --frequency {transponder.Frequency} --modulation {transponder.Modulation}-QAM -P skip 500 -P until -s 1 -P analyze --normalized -o {tempFile} -O drop";
            measureProc.StartInfo.UseShellExecute = false;
            measureProc.StartInfo.RedirectStandardOutput = true;
            measureProc.Start();
            while (!measureProc.StandardOutput.EndOfStream)
            {
                Console.WriteLine(measureProc.StandardOutput.ReadLine());
            }

        }
        static Dictionary<int, bool> GetDevicesList()
        {
            Process devlist = new Process();
            devlist.StartInfo.FileName = "tslsdvb";
            devlist.StartInfo.UseShellExecute = false;
            devlist.StartInfo.RedirectStandardOutput = true;
            Dictionary<int, bool> devicesList = new Dictionary<int, bool>();
            devlist.Start();
            int tunersCount = 0;
            while (!devlist.StandardOutput.EndOfStream)
            {
                string currentStr = devlist.StandardOutput.ReadLine();
                Regex regex = new Regex(@"DVB-C", RegexOptions.IgnoreCase);
                Match tunersMatch = regex.Match(currentStr);
                if (tunersMatch.Success)
                {
                    devicesList.Add(tunersCount, false);
                    tunersCount++;
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
