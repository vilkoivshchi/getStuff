using System;

namespace getStuff
{
    internal class MeasurementStorage
    {
        internal MeasurementStorage(DateTime time, int tsid, int pid, string description, int bitrate)
        {
            Time = time;
            Tsid = tsid;
            Pid = pid;
            Description = description;
            Bitrate = bitrate;
        }

        internal DateTime Time { get; set; }
        internal int Tsid { get; set; }
        internal int Pid { get; set; }
        internal string Description { get; set; }
        internal int Bitrate { get; set; }

    }
}
