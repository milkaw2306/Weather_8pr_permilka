using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Weather_8pr_permilka.Classes;

namespace Weather_8pr_permilka
{
    public partial class MainWindow : Window
    {
        private const string ApiKey = "0ffbe7794a62fc2ffd608e2a1af54f8f";
        private const string ApiUrl = "https://api.openweathermap.org/data/2.5/forecast?q={0}&appid={1}&units=metric&lang=ru";

        public MainWindow()
        {
            InitializeComponent();
            WeatherCache.InitializeDatabase();
        }

        private async void UpdateWeather_Click(object sender, RoutedEventArgs e)
        {
            string city = CityTextBox.Text.Trim();

            if (string.IsNullOrEmpty(city))
            {
                MessageBox.Show("Введите название города.");
                return;
            }

            await UpdateWeather(city);
            UpdateRequestCount();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string defaultCity = "Пермь";

        }

        private async Task<List<WeatherData>> FetchWeatherData(string city)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = string.Format(ApiUrl, city, ApiKey);
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка {response.StatusCode}: {response.ReasonPhrase}");
                }
                string responseBody = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<dynamic>(responseBody);

                var weatherList = new List<WeatherData>();

                foreach (var item in json.list)
                {
                    weatherList.Add(new WeatherData
                    {
                        DateTime = Convert.ToDateTime(item.dt_txt).ToString("dd.MM.yyyy HH:mm"),
                        Temperature = $"{item.main.temp} °C",
                        Pressure = $"{item.main.pressure} мм рт. ст.",
                        Humidity = $"{item.main.humidity}%",
                        WindSpeed = $"{item.wind.speed} м/с",
                        FeelsLike = $"{item.main.feels_like} °C",
                        WeatherDescription = item.weather[0].description.ToString(),
                    });
                }
                return weatherList;
            }
        }

        private async Task UpdateWeather(string city)
        {
            try
            {
                int requestCount = WeatherCache.GetRequestCountForToday();

                if (requestCount >= 500)
                {
                    var cachedData = WeatherCache.GetWeatherData(city);
                    if (cachedData.Count > 0)
                    {
                        WeatherDataGrid.ItemsSource = cachedData;
                        MessageBox.Show("Данные для города получены из кэша");
                    }
                    else
                    {
                        MessageBox.Show("Лимит запросов на сегодня превышен");
                    }
                }
                else
                {
                    var weatherData = await FetchWeatherData(city);
                    WeatherDataGrid.ItemsSource = weatherData;

                    foreach (var data in weatherData)
                    {
                        WeatherCache.SaveWeatherData(city, data.DateTime, data.Temperature, data.Pressure, data.Humidity, data.WindSpeed, data.FeelsLike, data.WeatherDescription);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void UpdateRequestCount()
        {
            int requestCount = WeatherCache.GetRequestCountForToday();
            RequestCountTextBlock.Text = $"Количество запросов сегодня: {requestCount}";
        }
    }
}
    
            