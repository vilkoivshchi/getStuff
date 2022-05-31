using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using Microsoft.Data.Sqlite;
using System.Linq;

namespace getStuff
{
    internal class StuffingMeasurement
    {
        internal StuffingMeasurement(ParseConfig config)
        {
            _parsedConfig = config;
        }
        private ParseConfig _parsedConfig;
        private Dictionary<int, int> _ccErrorsDict = new();
        internal void MeasureStuffing(List<DVBMux> muxes)
        {
            List<Task> TaskList = new List<Task>();
            for (int i = 0; i < muxes.Count; i++)
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

        internal void StoreBitrateToPostgres(int tsNum, int stuffBitrate)
        {
            string connString = $"Server={_parsedConfig.DbHost};Username={_parsedConfig.DbUser};Database={_parsedConfig.DbName};Port={_parsedConfig.DbPort};Password={_parsedConfig.DbPassword}";
            using (var conn = new NpgsqlConnection(connString))
            {
                Console.Out.WriteLine($"Opening connection to {_parsedConfig.DbHost}");
                conn.Open();
                using (var command = new NpgsqlCommand($"CREATE TABLE IF NOT EXISTS TS{tsNum}(time TIMESTAMP WITHOUT TIME ZONE PRIMARY KEY, bitrate INTEGER)", conn))
                {
                    command.ExecuteNonQuery();
                }
                using (var command = new NpgsqlCommand($"INSERT INTO TS{tsNum} (time, bitrate) VALUES ((now() at time zone 'utc'), @q1)", conn))
                {
                    command.Parameters.AddWithValue("q1", stuffBitrate);
                    Console.Out.WriteLine(String.Format($"Write for ts{tsNum} complete. Number of rows inserted={command.ExecuteNonQuery()}"));
                }

                if (_parsedConfig.StorePeriod != String.Empty)
                {

                    using (var command = new NpgsqlCommand($"DELETE FROM TS{tsNum} WHERE time < ((now() at time zone 'utc') - interval '{_parsedConfig.StorePeriod}')", conn))
                    {
                        Console.Out.WriteLine(String.Format($"Cleaning table ts{tsNum} complete. Number of rows deleted={command.ExecuteNonQuery()}"));
                    }
                }

                conn.Close();
            }
        }

        internal void StoreErrorsToPostgres(int ts, int pid, int errorCount)
        {
            string connString = $"Server={_parsedConfig.DbHost};Username={_parsedConfig.DbUser};Database={_parsedConfig.DbName};Port={_parsedConfig.DbPort};Password={_parsedConfig.DbPassword}";
            string tableName = "cc_errors";
            using (var conn = new NpgsqlConnection(connString))
            {
                Console.Out.WriteLine($"Opening connection to {_parsedConfig.DbHost}");
                conn.Open();
                using (var command = new NpgsqlCommand($"CREATE TABLE IF NOT EXISTS {tableName} (time TIMESTAMP WITHOUT TIME ZONE PRIMARY KEY, ts INTEGER, pid INTEGER, errorcount INTEGER)", conn))
                {
                   command.ExecuteNonQuery();
                    //Console.Out.WriteLine("Finished creating table");
                }
                using (var command = new NpgsqlCommand($"INSERT INTO {tableName} (time, ts, pid, errorcount) VALUES ((now() at time zone 'utc'), @n1, @q1, @v1)", conn))
                {
                    command.Parameters.AddWithValue("n1", ts);
                    command.Parameters.AddWithValue("q1", pid);
                    command.Parameters.AddWithValue("v1", errorCount);

                    Console.Out.WriteLine(String.Format($"Write for {tableName} complete. Number of rows inserted={command.ExecuteNonQuery()}"));
                }
                conn.Close();
            }
        }

        internal void StoreDataToSqlite(MeasurementStorage measurements, Dictionary<int, int> ccErrorsDict)
        {
            DateTime dateTimeNow = DateTime.UtcNow;
            string fullDbPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "db\\", $"{dateTimeNow:yyyy-MM-dd_hh}_utc.db");
            string dbPath = Path.GetDirectoryName(fullDbPath);
            string measurementTableName = "measurement";
            string ccErrorsTableName = "ccerrors";
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }

            using (SqliteConnection sqliteConnection = new SqliteConnection($"Data Source={fullDbPath}"))
            {
                sqliteConnection.Open();
                while(sqliteConnection.State != System.Data.ConnectionState.Open)
                {
                    Thread.Sleep(100);
                }
                SqliteCommand createMeasurementTable = new SqliteCommand($"CREATE TABLE IF NOT EXISTS {measurementTableName} (time TEXT, tsid INTEGER, pid INTEGER, description TEXT, bitrate INTEGER)", sqliteConnection);
                createMeasurementTable.ExecuteNonQuery();
                SqliteCommand insertData = new SqliteCommand($"INSERT INTO {measurementTableName} (time, tsid, pid, description, bitrate) VALUES ($time, $tsid, $pid, $description, $bitrate)", sqliteConnection);
                insertData.Parameters.AddWithValue("$time", measurements.Time);
                insertData.Parameters.AddWithValue("$tsid", measurements.Tsid);
                insertData.Parameters.AddWithValue("$pid", measurements.Pid);
                insertData.Parameters.AddWithValue("$description", measurements.Description);
                insertData.Parameters.AddWithValue("$bitrate", measurements.Bitrate);
                insertData.ExecuteNonQuery();
                SqliteCommand createErrorsTable = new SqliteCommand($"CREATE TABLE IF NOT EXISTS {ccErrorsTableName} (time TEXT, tsid INTEGER, pid INTEGER, errorcount INTEGER)", sqliteConnection);
                createErrorsTable.ExecuteNonQuery();
                for(int i = 0; i < ccErrorsDict.Count; i++)
                {
                    var ccError = ccErrorsDict.ElementAt(i);
                    SqliteCommand insertErrorsData = new SqliteCommand($"INSERT INTO {ccErrorsTableName} (time, tsid, pid, errorcount) VALUES ($time, $tsid, $pid, $errorcount)", sqliteConnection);
                    insertErrorsData.Parameters.AddWithValue("$time", measurements.Time);
                    insertErrorsData.Parameters.AddWithValue("$tsid", measurements.Tsid);
                    insertErrorsData.Parameters.AddWithValue("$pid", ccError.Key);
                    insertErrorsData.Parameters.AddWithValue("$errorcount", ccError.Value);
                    insertErrorsData.ExecuteNonQuery();
                    insertErrorsData.Dispose();
                    ccErrorsDict.Remove(ccError.Key);
                }
                createMeasurementTable.Dispose();
                insertData.Dispose();

            }
                
        }
        /// <summary>
        /// Capture console output
        /// </summary>
        /// <param name="sendingProcess"></param>
        /// <param name="outLine">console data</param>
        internal void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {

            if (!String.IsNullOrEmpty(outLine.Data))
            {
                Process sendProc = (Process)sendingProcess;
                string arguments = sendProc.StartInfo.Arguments;

                Regex argumentRegex = new Regex(@"--tag [\d]{1,}");
                MatchCollection argumentsCollection = argumentRegex.Matches(arguments);
                string tsNumberString = String.Empty;
                tsNumberString = Regex.Replace(argumentsCollection[0].Value, "--tag ", String.Empty);
                int tsNumber = 0;
                int.TryParse(tsNumberString, out tsNumber);

                Regex inputJsonRegex = new Regex(@"(?<=analyze:).{1,}");
                MatchCollection inputJsonMatches = inputJsonRegex.Matches(outLine.Data);
                
                if (inputJsonMatches.Count > 0)
                {
                    string inputJson = inputJsonMatches[0].Value;
                    JObject tsDuckString = JObject.Parse(inputJson);
                    DateTime dateTimeNow = DateTime.UtcNow;

                    foreach(JToken pidToken in tsDuckString.SelectTokens("pids..id"))
                    {
                        int pid = -1;
                        int bitrate = -1;
                        JToken pidBitrateToken = tsDuckString.SelectToken($"pids[?(@.id == {pidToken})].bitrate");
                        pid = (int)pidToken;
                        bitrate = (int)pidBitrateToken;
                        JToken pidDecriptionToken = tsDuckString.SelectToken($"pids[?(@.id == {pidToken})].description");

                        Console.WriteLine($"{dateTimeNow:yyyy-MM-dd HH:mm:ss.fff} ts{tsNumber} {(int)pidToken} ({pidDecriptionToken}): {(int)pidBitrateToken} bps");
                        MeasurementStorage ms = new MeasurementStorage(dateTimeNow, tsNumber, pid, (string)pidDecriptionToken, bitrate);
                        Task.Run(() => StoreDataToSqlite(ms, _ccErrorsDict));
                        
                    }
                }

                
                Regex CCerrorRegex = new Regex(@"(continuity: )|(PID: 0x[\w]{1,})|(missing [\d]{1,})");
                MatchCollection CCerrorCollection = CCerrorRegex.Matches(outLine.Data);

                if (CCerrorCollection.Count == 3)
                {
                    int ccErrorPid = -1;
                    int ccErrorCount = -1;
                    int.TryParse(Regex.Replace(CCerrorCollection[2].Value, "missing ", String.Empty), out ccErrorCount);
                    ccErrorPid = Convert.ToInt32(Regex.Replace(CCerrorCollection[1].Value, "PID: ", String.Empty), 16);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($" PID: {ccErrorPid}, count: {ccErrorCount}");
                    Console.ResetColor();
                    
                    if (ccErrorCount > 0 && ccErrorPid > 0)
                    {
                        //Task.Run(() => StoreErrorsToDatabase(tsNumber, ccErrorPid, ccErrorCount));
                        if (_ccErrorsDict.ContainsKey(ccErrorPid))
                        {
                            _ccErrorsDict[ccErrorPid] += ccErrorCount;
                        }
                        else
                        {
                            _ccErrorsDict.Add(ccErrorPid, ccErrorCount);
                        }
                        
                    }
                    
                }
                /*
                else if (OutputCollection.Count > 0 && argumentsCollection.Count > 0)
                {
                    tsNumberString = Regex.Replace(argumentsCollection[0].Value, "--tag ", String.Empty);
                    int.TryParse(tsNumberString, out tsNumber);
                    string bitrateIteration1 = Regex.Replace(OutputCollection[0].Value, "bitrate: ", String.Empty);
                    string bitrateIteration2 = Regex.Replace(bitrateIteration1, ",", String.Empty);
                    int.TryParse(bitrateIteration2, out bitrate);
                    Console.WriteLine($"ts{tsNumber}, bitrate: {bitrate}");
                    //Task.Run(() => StoreBitrateToDatabase(tsNumber, bitrate));
                }
                else
                {
                    Console.WriteLine(tsDuckOutput);
                }
                */
            
            }
        }

        internal void RunMeasure(DVBMux mux)
        {
            Console.WriteLine($"Begin measure ts{mux.TransponderNumber} at address {mux.MulticastAddress}:{mux.MulticastPort}...");
            Process measureProc = new();

            measureProc.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "tsduck\\tsp.exe"); ;
            //measureProc.StartInfo.Arguments = $"--realtime -v -t -I ip {mux.MulticastAddress}:{mux.MulticastPort} -l {mux.InterfaceAddress} -P continuity -P bitrate_monitor -p 1 -t 1 --pid 8191 --tag {mux.TransponderNumber} -O drop";
            measureProc.StartInfo.Arguments = $"--realtime -v -t -I ip {mux.MulticastAddress}:{mux.MulticastPort} -l {mux.InterfaceAddress} -P analyze -i 1 --json-line -P continuity --tag {mux.TransponderNumber} -O drop";
            measureProc.StartInfo.UseShellExecute = false;
            measureProc.StartInfo.RedirectStandardOutput = true;
            measureProc.StartInfo.RedirectStandardError = true;
            measureProc.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            measureProc.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
            try
            {
                measureProc.Start();
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(2);
            }
            measureProc.BeginOutputReadLine();
            measureProc.BeginErrorReadLine();
            measureProc.WaitForExit();
        }
    }
}
