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
        private const string OpenWeatherApiKey = "your_openweathermap_api_key_here";
        private const string WeatherApiBaseUrl = "https://api.openweathermap.org/data/2.5/forecast";
        private const int DailyRequestLimit = 500;
        private const string CacheFilePath = "weather_cache.json";
        private const string RequestCountFilePath = "request_count.txt";

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                InitializeFiles();
                UpdateRequestCount();
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка инициализации: {ex.Message}", MessageType.Error);
            }

            CityTextBox.KeyDown += CityTextBox_KeyDown;
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string defaultCity = "Пермь";
            CityTextBox.Text = defaultCity;
            _ = LoadWeatherForCityAsync(defaultCity);
        }

        private async void UpdateWeather_Click(object sender, RoutedEventArgs e)
        {
            string city = CityTextBox.Text.Trim();
            if (string.IsNullOrEmpty(city))
            {
                ShowMessage("Введите название города", MessageType.Warning);
                CityTextBox.Focus();
                return;
            }
            await LoadWeatherForCityAsync(city);
        }

        private async void CityTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await LoadWeatherForCityAsync(CityTextBox.Text.Trim());
            }
        }

        private async Task LoadWeatherForCityAsync(string city)
        {
            if (string.IsNullOrEmpty(city))
            {
                ShowMessage("Введите название города", MessageType.Warning);
                CityTextBox.Focus();
                return;
            }

            try
            {
                SetLoadingState(true);
                int requestCount = GetRequestCountForToday();

                if (requestCount >= DailyRequestLimit)
                {
                    var cachedData = GetWeatherDataFromCache(city);
                    if (cachedData.Count > 0)
                    {
                        WeatherDataGrid.ItemsSource = cachedData;
                        ShowMessage("Данные загружены из кэша (лимит запросов исчерпан)", MessageType.Info);
                        NoDataMessage.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ShowMessage($"Лимит запросов на сегодня превышен ({DailyRequestLimit})", MessageType.Warning);
                        NoDataMessage.Visibility = Visibility.Visible;
                        WeatherDataGrid.ItemsSource = null;
                    }
                }
                else
                {
                    var weatherData = await FetchWeatherDataAsync(city);
                    if (weatherData != null && weatherData.Any())
                    {
                        WeatherDataGrid.ItemsSource = weatherData;
                        NoDataMessage.Visibility = Visibility.Collapsed;

                        SaveWeatherDataToCache(city, weatherData);
                        IncrementRequestCount();

                        ShowMessage($"Данные для {city} успешно обновлены", MessageType.Success);
                    }
                    else
                    {
                        ShowMessage($"Не удалось получить данные для города: {city}", MessageType.Error);
                        NoDataMessage.Visibility = Visibility.Visible;
                        WeatherDataGrid.ItemsSource = null;
                    }
                }
                UpdateRequestCount();
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка загрузки данных: {ex.Message}", MessageType.Error);
                NoDataMessage.Visibility = Visibility.Visible;
                WeatherDataGrid.ItemsSource = null;
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async Task<List<WeatherData>> FetchWeatherDataAsync(string city)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                try
                {
                    string url = $"{WeatherApiBaseUrl}?q={Uri.EscapeDataString(city)}&appid={OpenWeatherApiKey}&units=metric&lang=ru";

                    HttpResponseMessage response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            throw new Exception("Город не найден");
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            throw new Exception("Неверный API ключ");
                        }
                        throw new Exception($"Ошибка API: {response.StatusCode}");
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<dynamic>(responseBody);

                    if (json.list == null || !((IEnumerable<dynamic>)json.list).Any())
                    {
                        return new List<WeatherData>();
                    }

                    var weatherList = new List<WeatherData>();
                    foreach (var item in json.list)
                    {
                        try
                        {
                            double pressureHpa = (double)item.main.pressure;
                            double pressureMmHg = Math.Round(pressureHpa * 0.750062, 0);

                            weatherList.Add(new WeatherData
                            {
                                DateTime = Convert.ToDateTime(item.dt_txt).ToString("dd.MM.yyyy HH:mm"),
                                Temperature = $"{item.main.temp:0.#} °C",
                                Pressure = $"{pressureMmHg} мм рт. ст.",
                                Humidity = $"{item.main.humidity}%",
                                WindSpeed = $"{item.wind.speed:0.#} м/с",
                                FeelsLike = $"{item.main.feels_like:0.#} °C",
                                WeatherDescription = CapitalizeFirstLetter(item.weather[0].description.ToString()),
                            });
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    return weatherList;
                }
                catch (HttpRequestException)
                {
                    throw new Exception("Ошибка соединения с сервером погоды");
                }
                catch (JsonException)
                {
                    throw new Exception("Ошибка обработки данных от сервера");
                }
                catch (TaskCanceledException)
                {
                    throw new Exception("Таймаут запроса");
                }
            }
        }

        private void InitializeFiles()
        {
            if (!File.Exists(CacheFilePath))
            {
                File.WriteAllText(CacheFilePath, "{}");
            }

            if (!File.Exists(RequestCountFilePath))
            {
                File.WriteAllText(RequestCountFilePath, DateTime.Now.ToString("yyyy-MM-dd") + "|0");
            }
        }

        private int GetRequestCountForToday()
        {
            try
            {
                if (File.Exists(RequestCountFilePath))
                {
                    string content = File.ReadAllText(RequestCountFilePath);
                    string[] parts = content.Split('|');

                    if (parts.Length == 2)
                    {
                        string dateStr = parts[0];
                        string countStr = parts[1];

                        if (DateTime.TryParse(dateStr, out DateTime savedDate))
                        {
                            if (savedDate.Date == DateTime.Now.Date)
                            {
                                return int.Parse(countStr);
                            }
                        }
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private void IncrementRequestCount()
        {
            try
            {
                int currentCount = GetRequestCountForToday();
                currentCount++;
                File.WriteAllText(RequestCountFilePath, DateTime.Now.ToString("yyyy-MM-dd") + "|" + currentCount);
            }
            catch
            {
            }
        }

        private List<WeatherData> GetWeatherDataFromCache(string city)
        {
            var result = new List<WeatherData>();

            try
            {
                if (File.Exists(CacheFilePath))
                {
                    string json = File.ReadAllText(CacheFilePath);
                    var cacheData = JsonConvert.DeserializeObject<Dictionary<string, List<WeatherData>>>(json);

                    if (cacheData != null && cacheData.ContainsKey(city))
                    {
                        return cacheData[city];
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        private void SaveWeatherDataToCache(string city, List<WeatherData> data)
        {
            try
            {
                Dictionary<string, List<WeatherData>> cacheData = new Dictionary<string, List<WeatherData>>();

                if (File.Exists(CacheFilePath))
                {
                    string json = File.ReadAllText(CacheFilePath);
                    var existingData = JsonConvert.DeserializeObject<Dictionary<string, List<WeatherData>>>(json);
                    if (existingData != null)
                    {
                        cacheData = existingData;
                    }
                }

                cacheData[city] = data;
                string newJson = JsonConvert.SerializeObject(cacheData, Formatting.Indented);
                File.WriteAllText(CacheFilePath, newJson);
            }
            catch
            {
            }
        }

        private void UpdateRequestCount()
        {
            try
            {
                int requestCount = GetRequestCountForToday();
                int remainingRequests = DailyRequestLimit - requestCount;
                RequestCountTextBlock.Text = $"{requestCount}";
                if (remainingRequests <= 0)
                {
                    RequestCountTextBlock.Foreground = Brushes.Red;
                }
                else if (remainingRequests <= 100)
                {
                    RequestCountTextBlock.Foreground = Brushes.Orange;
                }
                else
                {
                    RequestCountTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(44, 111, 183));
                }
            }
            catch
            {
                RequestCountTextBlock.Text = "0";
                RequestCountTextBlock.Foreground = Brushes.Gray;
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            UpdateButton.IsEnabled = !isLoading;
            CityTextBox.IsEnabled = !isLoading;
            if (isLoading)
            {
                UpdateButton.Content = "Загрузка...";
                UpdateButton.Background = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
            else
            {
                UpdateButton.Content = "Обновить";
                UpdateButton.Background = new SolidColorBrush(Color.FromRgb(74, 144, 226));
            }
            WeatherDataGrid.IsEnabled = !isLoading;
        }

        private void ShowMessage(string message, MessageType type)
        {
            string title = type switch
            {
                MessageType.Success => "Успех",
                MessageType.Error => "Ошибка",
                MessageType.Warning => "Предупреждение",
                MessageType.Info => "Информация",
                _ => "Сообщение"
            };
            MessageBoxImage icon = type switch
            {
                MessageType.Success => MessageBoxImage.Information,
                MessageType.Error => MessageBoxImage.Error,
                MessageType.Warning => MessageBoxImage.Warning,
                MessageType.Info => MessageBoxImage.Information,
                _ => MessageBoxImage.Information
            };
            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }

        private string CapitalizeFirstLetter(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return char.ToUpper(text[0]) + text.Substring(1).ToLower();
        }

        private enum MessageType
        {
            Success,
            Error,
            Warning,
            Info
        }
    }
}