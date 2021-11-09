using System;


namespace getStuff
{
    class Program
    {
        static void Main(string[] args)
        {
            ParseConfig parsedConfig = new($"{AppContext.BaseDirectory}\\muxes.xml");
            parsedConfig.ReadConfig();
            StuffingMeasurement stuffingMeasurement = new(parsedConfig);
            stuffingMeasurement.MeasureStuffing(parsedConfig.muxes);
            
        }

    }
     
}
