using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Weather_8pr_permilka.Classes
{
    public class WeatherCache
    {
        private const string ConnectionString = "server=localhost;database=weatherdb;uid=root;pwd=;";

        public static void InitializeDatabase()
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();

                string createDbQuery = "CREATE DATABASE IF NOT EXISTS weatherdb";
                var dbCommand = new MySqlCommand(createDbQuery, connection);
                dbCommand.ExecuteNonQuery();
                connection.ChangeDatabase("weatherdb");

                string createTableQuery = @"
            CREATE TABLE IF NOT EXISTS WeatherData (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                City VARCHAR(100) NOT NULL,
                DateTime VARCHAR(50) NOT NULL,
                Temperature VARCHAR(20),
                Pressure VARCHAR(20),
                Humidity VARCHAR(20),
                WindSpeed VARCHAR(20),
                FeelsLike VARCHAR(20),
                WeatherDescription VARCHAR(255),
                RequestDate DATE NOT NULL,
                CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_city_date (City, RequestDate)
            )";

                var command = new MySqlCommand(createTableQuery, connection);
                command.ExecuteNonQuery();
            }
        }

        public static void SaveWeatherData(string city, string dateTime, string temperature, string pressure,
                                           string humidity, string windSpeed, string feelsLike, string weatherDescription)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                string insertQuery = @"
            INSERT INTO WeatherData (City, DateTime, Temperature, Pressure, Humidity, 
                                     WindSpeed, FeelsLike, WeatherDescription, RequestDate)
            VALUES (@City, @DateTime, @Temperature, @Pressure, @Humidity, 
                    @WindSpeed, @FeelsLike, @WeatherDescription, @RequestDate)";

                var command = new MySqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@City", city);
                command.Parameters.AddWithValue("@DateTime", dateTime);
                command.Parameters.AddWithValue("@Temperature", temperature);
                command.Parameters.AddWithValue("@Pressure", pressure);
                command.Parameters.AddWithValue("@Humidity", humidity);
                command.Parameters.AddWithValue("@WindSpeed", windSpeed);
                command.Parameters.AddWithValue("@FeelsLike", feelsLike);
                command.Parameters.AddWithValue("@WeatherDescription", weatherDescription);
                command.Parameters.AddWithValue("@RequestDate", DateTime.Now.Date);

                command.ExecuteNonQuery();
            }
        }

        public static List<WeatherData> GetWeatherData(string city)
        {
            List<WeatherData> weatherDataList = new List<WeatherData>();

            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                string selectQuery = @"
            SELECT * FROM WeatherData
            WHERE City = @City AND RequestDate = @RequestDate
            ORDER BY DateTime";

                var command = new MySqlCommand(selectQuery, connection);
                command.Parameters.AddWithValue("@City", city);
                command.Parameters.AddWithValue("@RequestDate", DateTime.Now.Date);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        weatherDataList.Add(new WeatherData
                        {
                            DateTime = reader["DateTime"].ToString(),
                            Temperature = reader["Temperature"].ToString(),
                            Pressure = reader["Pressure"].ToString(),
                            Humidity = reader["Humidity"].ToString(),
                            WindSpeed = reader["WindSpeed"].ToString(),
                            FeelsLike = reader["FeelsLike"].ToString(),
                            WeatherDescription = reader["WeatherDescription"].ToString()
                        });
                    }
                }
            }
            return weatherDataList;
        }

        public static int GetRequestCountForToday()
        {
            int requestCount = 0;

            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                string selectQuery = "SELECT COUNT(*) FROM WeatherData WHERE RequestDate = @RequestDate";
                var command = new MySqlCommand(selectQuery, connection);
                command.Parameters.AddWithValue("@RequestDate", DateTime.Now.Date);

                var result = command.ExecuteScalar();
                requestCount = result != null ? Convert.ToInt32(result) : 0;
            }

            return requestCount;
        }
        public static void CleanOldData(int daysToKeep = 30)
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                string deleteQuery = "DELETE FROM WeatherData WHERE RequestDate < @CutoffDate";
                var command = new MySqlCommand(deleteQuery, connection);
                command.Parameters.AddWithValue("@CutoffDate", DateTime.Now.Date.AddDays(-daysToKeep));

                command.ExecuteNonQuery();
            }
        }
    }
}
