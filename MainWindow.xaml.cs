using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using Weather_8pr_permilka.Classes;

namespace Weather_8pr_permilka
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string ApiKey = "992a69bc60315f46a2587400486e1f4f";
        private const string ApiUrl = "https://api.openweathermap.org/data/2.5/forecast?q={0}&appid={1}&units=metric&lang=ru";
        private const int DailyRequestLimit = 500;

        public MainWindow()
        {
            InitializeComponent();

            // Инициализация базы данных
            try
            {
                WeatherCache.InitializeDatabase();
                UpdateRequestCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации базы данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Обработчики событий
            CityTextBox.KeyDown += CityTextBox_KeyDown;
            this.Loaded += Window_LoadedAsync;
        }

        private async void Window_LoadedAsync(object sender, RoutedEventArgs e)
        {
            string defaultCity = "Пермь";
            await LoadWeatherForCity(defaultCity);
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

            await LoadWeatherForCity(city);
        }

        private async void CityTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                await UpdateWeather_Click(null, null);
            }
        }

        private async Task LoadWeatherForCity(string city)
        {
            try
            {
                // Показать индикатор загрузки
                SetLoadingState(true);

                // Проверяем лимит запросов
                int requestCount = WeatherCache.GetRequestCountForToday();

                if (requestCount >= DailyRequestLimit)
                {
                    // Пытаемся получить данные из кэша
                    var cachedData = WeatherCache.GetWeatherData(city);

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
                    }
                }
                else
                {
                    // Загружаем свежие данные
                    var weatherData = await FetchWeatherDataAsync(city);

                    if (weatherData != null && weatherData.Any())
                    {
                        WeatherDataGrid.ItemsSource = weatherData;
                        NoDataMessage.Visibility = Visibility.Collapsed;

                        // Сохраняем в кэш
                        foreach (var data in weatherData)
                        {
                            WeatherCache.SaveWeatherData(
                                city,
                                data.DateTime,
                                data.Temperature,
                                data.Pressure,
                                data.Humidity,
                                data.WindSpeed,
                                data.FeelsLike,
                                data.WeatherDescription
                            );
                        }

                        ShowMessage($"Данные для {city} успешно обновлены", MessageType.Success);
                    }
                    else
                    {
                        ShowMessage($"Не удалось получить данные для города: {city}", MessageType.Error);
                        NoDataMessage.Visibility = Visibility.Visible;
                    }
                }

                UpdateRequestCount();
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка загрузки данных: {ex.Message}", MessageType.Error);
                NoDataMessage.Visibility = Visibility.Visible;
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
                    string url = string.Format(ApiUrl, city, ApiKey);
                    HttpResponseMessage response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            throw new Exception("Город не найден");
                        }
                        throw new Exception($"Ошибка API: {response.StatusCode}");
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<dynamic>(responseBody);

                    // Проверяем, есть ли данные
                    if (json.list == null || !((IEnumerable<dynamic>)json.list).Any())
                    {
                        return new List<WeatherData>();
                    }

                    var weatherList = new List<WeatherData>();

                    foreach (var item in json.list)
                    {
                        try
                        {
                            weatherList.Add(new WeatherData
                            {
                                DateTime = Convert.ToDateTime(item.dt_txt).ToString("dd.MM.yyyy HH:mm"),
                                Temperature = $"{item.main.temp:0.#} °C",
                                Pressure = $"{item.main.pressure} мм рт. ст.",
                                Humidity = $"{item.main.humidity}%",
                                WindSpeed = $"{item.wind.speed:0.#} м/с",
                                FeelsLike = $"{item.main.feels_like:0.#} °C",
                                WeatherDescription = CapitalizeFirstLetter(item.weather[0].description.ToString()),
                            });
                        }
                        catch
                        {
                            // Пропускаем некорректные записи
                            continue;
                        }
                    }

                    return weatherList;
                }
                catch (HttpRequestException httpEx)
                {
                    throw new Exception("Ошибка соединения с сервером погоды");
                }
                catch (JsonException jsonEx)
                {
                    throw new Exception("Ошибка обработки данных от сервера");
                }
                catch (TaskCanceledException)
                {
                    throw new Exception("Таймаут запроса");
                }
            }
        }

        private void UpdateRequestCount()
        {
            try
            {
                int requestCount = WeatherCache.GetRequestCountForToday();
                int remainingRequests = DailyRequestLimit - requestCount;

                RequestCountTextBlock.Text = $"{requestCount}";

                // Обновляем цвет счетчика в зависимости от количества запросов
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
            catch (Exception ex)
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
                UpdateButton.Content = "⏳ Загрузка...";
                UpdateButton.Background = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
            else
            {
                UpdateButton.Content = "🔄 Обновить";
                UpdateButton.Background = new SolidColorBrush(Color.FromRgb(74, 144, 226));
            }

            WeatherDataGrid.IsEnabled = !isLoading;
        }

        private void ShowMessage(string message, MessageType type)
        {
            // Можно заменить на более красивый Toast/Snackbar
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

            return char.ToUpper(text[0]) + text.Substring(1);
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