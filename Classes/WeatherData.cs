using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace Weather_8pr_permilka.Classes
{
    public class WeatherData
    {
        public string DateTime { get; set; }
        public string Temperature { get; set; }
        public string Pressure { get; set; }
        public string Humidity { get; set; }
        public string WindSpeed { get; set; }
        public string FeelsLike { get; set; }
        public string WeatherDescription { get; set; }
    }
}
