using System;
using System.Net.Http;
using System.Text;
using System.Windows;
using Newtonsoft.Json.Linq;
using System.Windows.Media;
using NAudio.Wave;
using System.Collections.Generic;
using System.Threading.Tasks;
using WordBridge.Database;
using static WordBridge.Database.Connect;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WpfAnimatedGif; // Added for KeyEventArgs

namespace WordBridge
{
    public partial class MainWindow : Window
    {
        #region Const
        private string loggedInUser = null;
        private readonly HttpClient httpClient = new HttpClient();
        private string currentAudioUrl = null;
        private bool isHistoryVisible = false;
        private bool isFavoritesVisible = false;
       
        private IWavePlayer waveOut;
        private MediaFoundationReader reader;
        private readonly Dictionary<string, string> translationCache = new Dictionary<string, string>();

        private readonly Connect db = new Connect();

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            UpdateUserInterface();

            var imageUri = new Uri("pack://application:,,,/img/maintenance.gif");
            var image = new BitmapImage(imageUri);
            ImageBehavior.SetAnimatedSource(gifImage, image);

            var imageUri2 = new Uri("pack://application:,,,/img/list.gif");
            var image2 = new BitmapImage(imageUri2);
            ImageBehavior.SetAnimatedSource(gifImage2, image2);
        }

        #region Translate
        private async void TxtWord_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string word = txtWord.Text.Trim();
                if (string.IsNullOrEmpty(word))
                {
                    MessageBox.Show("Vui lòng nhập từ cần tra.");
                    return;
                }

                // Ẩn các DataGrid và hiển thị txtResult trước khi tra từ
                dataGridHistory.Visibility = Visibility.Collapsed;
                dataGridFavorites.Visibility = Visibility.Collapsed;
                txtResult.Visibility = Visibility.Visible;
                isHistoryVisible = false;
                isFavoritesVisible = false;

                txtWordDisplay.Text = "Đang tra cứu...";
                txtTranslationDisplay.Text = "";
                txtResult.Text = "";

                currentAudioUrl = null;

                string dictionaryResult = await GetDictionaryData(word);
                string translation = await TranslateToVietnamese(word);

                txtWordDisplay.Text = word;
                txtTranslationDisplay.Text = $"Dịch: {translation}";
                txtResult.Text = dictionaryResult;

                if (!string.IsNullOrEmpty(loggedInUser))
                {
                    db.SaveHistory(loggedInUser, word);

                    if (dataGridHistory.Visibility == Visibility.Visible)
                    {
                        dataGridHistory.ItemsSource = null;
                        dataGridHistory.ItemsSource = db.GetHistory(loggedInUser);
                    }
                }

                btnAddFavorite.IsEnabled = !string.IsNullOrEmpty(word);
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string word = txtWord.Text.Trim();
            if (string.IsNullOrEmpty(word))
            {
                MessageBox.Show("Vui lòng nhập từ cần tra.");
                return;
            }

            // Ẩn các DataGrid và hiển thị txtResult trước khi tra từ
            dataGridHistory.Visibility = Visibility.Collapsed;
            dataGridFavorites.Visibility = Visibility.Collapsed;
            txtResult.Visibility = Visibility.Visible;
            isHistoryVisible = false;
            isFavoritesVisible = false;

            txtWordDisplay.Text = "Đang tra cứu...";
            txtTranslationDisplay.Text = "";
            txtResult.Text = "";

            currentAudioUrl = null;

            string dictionaryResult = await GetDictionaryData(word);
            string translation = await TranslateToVietnamese(word);

            txtWordDisplay.Text = word;
            txtTranslationDisplay.Text = $"Dịch: {translation}";
            txtResult.Text = dictionaryResult;

            if (!string.IsNullOrEmpty(loggedInUser))
            {
                db.SaveHistory(loggedInUser, word);

                if (dataGridHistory.Visibility == Visibility.Visible)
                {
                    dataGridHistory.ItemsSource = null;
                    dataGridHistory.ItemsSource = db.GetHistory(loggedInUser);
                }
            }

            btnAddFavorite.IsEnabled = !string.IsNullOrEmpty(word);
        }

        private async Task<string> GetDictionaryData(string word)
        {
            string url = $"https://api.dictionaryapi.dev/api/v2/entries/en/{word}";
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var data = JArray.Parse(json);
                var firstEntry = data[0];

                string audioUrl = null;
                var phonetics = firstEntry["phonetics"] as JArray;

                if (phonetics != null)
                {
                    foreach (var phonetic in phonetics)
                    {
                        var audioToken = phonetic["audio"];
                        if (audioToken != null)
                        {
                            string urlAudio = audioToken.ToString();
                            if (!string.IsNullOrWhiteSpace(urlAudio) &&
                                (urlAudio.StartsWith("http://") || urlAudio.StartsWith("https://")))
                            {
                                audioUrl = urlAudio;
                                break;
                            }
                        }
                    }
                }

                currentAudioUrl = audioUrl ?? null;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"🔤 Từ: {firstEntry["word"]}");

                var phoneticText = phonetics?[0]?["text"]?.ToString();
                if (!string.IsNullOrEmpty(phoneticText))
                    sb.AppendLine($"📣 Phát âm: {phoneticText}");

                var meanings = firstEntry["meanings"];
                foreach (var meaning in meanings)
                {
                    string partOfSpeech = meaning["partOfSpeech"]?.ToString();
                    sb.AppendLine($"\n📚 ({partOfSpeech})");

                    var definitions = meaning["definitions"];
                    foreach (var def in definitions)
                    {
                        string definition = def["definition"]?.ToString();
                        string example = def["example"]?.ToString();

                        sb.AppendLine($"• {definition}");
                        if (!string.IsNullOrEmpty(example))
                            sb.AppendLine($"   ➤ Ví dụ: {example}");
                    }
                }

                if (string.IsNullOrEmpty(audioUrl))
                {
                    sb.AppendLine("\n[Không tìm thấy âm thanh]");
                }

                return sb.ToString();
            }
            catch
            {
                return "Không tìm thấy nghĩa hoặc lỗi gọi API.";
            }
        }

        private async Task<string> TranslateToVietnamese(string text)
        {
            if (translationCache.TryGetValue(text, out string cachedTranslation))
                return cachedTranslation;

            try
            {
                string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair=en|vi";
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonString);

                string translation = json["responseData"]?["translatedText"]?.ToString() ?? "Không có bản dịch.";
                translationCache[text] = translation;
                return translation;
            }
            catch (Exception ex)
            {
                return $"Lỗi dịch: {ex.Message}";
            }
        }
        #endregion

        #region Audio
        private void BtnPlayAudio_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentAudioUrl))
            {
                MessageBox.Show("Không có âm thanh để phát.");
                return;
            }

            try
            {
                waveOut?.Stop();
                waveOut?.Dispose();
                reader?.Dispose();

                reader = new MediaFoundationReader(currentAudioUrl);
                waveOut = new WaveOutEvent();
                waveOut.Init(reader);
                waveOut.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi phát âm thanh: {ex.Message}");
            }
        }

        private void mediaPronounce_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            string errorMessage = e.ErrorException != null ? e.ErrorException.Message : "Không xác định";
            MessageBox.Show($"Lỗi phát âm thanh: {errorMessage}");
        }
        #endregion

        #region Account
        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            AccountButton.ContextMenu.IsOpen = true;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            bool? result = loginWindow.ShowDialog();

            if (result == true)
            {
                loggedInUser = loginWindow.LoggedInUsername;
                MessageBox.Show($"Đăng nhập thành công với tài khoản: {loggedInUser}");
                UpdateUserInterface();
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = new RegisterWindow();
            bool? result = registerWindow.ShowDialog();

            if (result == true)
            {
                string newUsername = registerWindow.Username;
                MessageBox.Show($"Đăng ký tài khoản '{newUsername}' thành công! Bạn có thể đăng nhập ngay.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            loggedInUser = null;
            MessageBox.Show("Bạn đã đăng xuất.");
            UpdateUserInterface();

            dataGridHistory.ItemsSource = null;
            dataGridHistory.Visibility = Visibility.Collapsed;

            dataGridFavorites.ItemsSource = null;
            dataGridFavorites.Visibility = Visibility.Collapsed;

            txtResult.Visibility = Visibility.Visible;
        }

        private void UpdateUserInterface()
        {
            if (string.IsNullOrEmpty(loggedInUser))
            {
                txtCurrentUser.Text = "Chưa đăng nhập";

                LoginMenuItem.Visibility = Visibility.Visible;
                RegisterMenuItem.Visibility = Visibility.Visible;
                LogoutMenuItem.Visibility = Visibility.Collapsed;

                dataGridHistory.Visibility = Visibility.Collapsed;
                dataGridFavorites.Visibility = Visibility.Collapsed;
            }
            else
            {
                txtCurrentUser.Text = $"Xin chào, {loggedInUser}";

                LoginMenuItem.Visibility = Visibility.Collapsed;
                RegisterMenuItem.Visibility = Visibility.Collapsed;
                LogoutMenuItem.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region History
        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(loggedInUser))
            {
                MessageBox.Show("Bạn cần đăng nhập để xem lịch sử.");
                return;
            }

            if (!isHistoryVisible)
            {
                var historyData = db.GetHistory(loggedInUser);
                dataGridHistory.ItemsSource = null;
                dataGridHistory.ItemsSource = historyData;
                dataGridHistory.Visibility = Visibility.Visible;

                dataGridFavorites.Visibility = Visibility.Collapsed;
                txtResult.Visibility = Visibility.Collapsed;

                isHistoryVisible = true;
                isFavoritesVisible = false;
            }
            else
            {
                dataGridHistory.Visibility = Visibility.Collapsed;
                txtResult.Visibility = Visibility.Visible;
                isHistoryVisible = false;
            }
        }

        private void DeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var historyItem = button?.DataContext as HistoryItem;

            if (historyItem == null)
            {
                MessageBox.Show("Không thể xóa. Vui lòng thử lại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show($"Bạn có chắc muốn xóa từ '{historyItem.Word}' khỏi lịch sử?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                bool success = db.DeleteHistory(loggedInUser, historyItem.Word, historyItem.LookupDate);
                if (success)
                {
                    var historyData = db.GetHistory(loggedInUser);
                    dataGridHistory.ItemsSource = null;
                    dataGridHistory.ItemsSource = historyData;

                    if (historyData.Count == 0)
                    {
                        dataGridHistory.Visibility = Visibility.Collapsed;
                        txtResult.Visibility = Visibility.Visible;
                        isHistoryVisible = false;
                    }
                }
                else
                {
                    MessageBox.Show("Không thể xóa từ khỏi lịch sử.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region Dark/Light Mode
        private bool IsDarkMode { get; set; }

        private void ToggleDarkMode_Checked(object sender, RoutedEventArgs e)
        {
            ApplyTheme(isDarkMode: true);
        }

        private void ToggleDarkMode_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyTheme(isDarkMode: false);
        }

        private bool ResourceExists(string key)
        {
            return this.Resources.Contains(key) || Application.Current.Resources.Contains(key);
        }

        private void ApplyTheme(bool isDarkMode)
        {
            try
            {
                IsDarkMode = isDarkMode;

                // Update Window and General Elements
                if (ResourceExists(isDarkMode ? "WindowBackgroundDark" : "WindowBackgroundLight"))
                    this.Background = (SolidColorBrush)FindResource(isDarkMode ? "WindowBackgroundDark" : "WindowBackgroundLight");
                if (ResourceExists(isDarkMode ? "TextForegroundDark" : "TextForegroundLight"))
                    txtCurrentUser.Foreground = (SolidColorBrush)FindResource(isDarkMode ? "TextForegroundDark" : "TextForegroundLight");
                if (ResourceExists(isDarkMode ? "AccentBlueDark" : "AccentBlueLight"))
                    txtWordDisplay.Foreground = (SolidColorBrush)FindResource(isDarkMode ? "AccentBlueDark" : "AccentBlueLight");
                if (ResourceExists(isDarkMode ? "AccentGreenDark" : "AccentGreenLight"))
                    txtTranslationDisplay.Foreground = (SolidColorBrush)FindResource(isDarkMode ? "AccentGreenDark" : "AccentGreenLight");
                if (ResourceExists(isDarkMode ? "TextForegroundDark" : "TextForegroundLight"))
                    Footer.Foreground = (SolidColorBrush)FindResource(isDarkMode ? "TextForegroundDark" : "TextForegroundLight");

                // Update TextBox and Search Button
                if (ResourceExists(isDarkMode ? "SearchTextBoxStyleDark" : "SearchTextBoxStyle"))
                    txtWord.Style = (Style)FindResource(isDarkMode ? "SearchTextBoxStyleDark" : "SearchTextBoxStyle");
                if (ResourceExists(isDarkMode ? "ResultTextBoxStyleDark" : "ResultTextBoxStyle"))
                    txtResult.Style = (Style)FindResource(isDarkMode ? "ResultTextBoxStyleDark" : "ResultTextBoxStyle");

                // Update DataGrids
                if (isDarkMode && ResourceExists("DataGridDark"))
                {
                    dataGridHistory.Style = (Style)FindResource("DataGridDark");
                    dataGridFavorites.Style = (Style)FindResource("DataGridDark");
                }
                else
                {
                    dataGridHistory.Style = null; // Use default style
                    dataGridFavorites.Style = null; // Use default style
                }

                // Update Buttons
                var buttonStyle = isDarkMode ? "ActionButtonStyleDark" : "ActionButtonStyle";
                if (ResourceExists(buttonStyle))
                {
                    btnPlayAudio.Style = (Style)FindResource(buttonStyle);
                    btnHistory.Style = (Style)FindResource(buttonStyle);
                    btnAddFavorite.Style = (Style)FindResource(buttonStyle);
                    btnShowFavorites.Style = (Style)FindResource(buttonStyle);
                    btnDeleteAll.Style = (Style)FindResource(buttonStyle);
                }

                // Update AccountButton with fixed white background style
                if (ResourceExists(isDarkMode ? "AccountButtonStyleDark" : "AccountButtonStyle"))
                    AccountButton.Style = (Style)FindResource(isDarkMode ? "AccountButtonStyleDark" : "AccountButtonStyle");

                // Update ContextMenu and Items
                if (AccountContextMenu != null && AccountContextMenu.Items != null && AccountContextMenu.Items.Count > 0)
                {
                    if (ResourceExists(isDarkMode ? "RoundedContextMenuDark" : "RoundedContextMenu"))
                        AccountContextMenu.Style = (Style)FindResource(isDarkMode ? "RoundedContextMenuDark" : "RoundedContextMenu");
                    foreach (MenuItem item in AccountContextMenu.Items)
                    {
                        if (ResourceExists(isDarkMode ? "ContextMenuItemStyleDark" : "ContextMenuItemStyle"))
                            item.Style = (Style)FindResource(isDarkMode ? "ContextMenuItemStyleDark" : "ContextMenuItemStyle");
                    }
                }

                // Save Theme Preference
                Properties.Settings.Default.IsDarkMode = isDarkMode;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("error.log", $"{DateTime.Now}: ApplyTheme failed - {ex.Message}\n{ex.StackTrace}\n");
                MessageBox.Show($"Lỗi khi áp dụng theme: {ex.Message}", "Lỗi Theme", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load saved theme preference
                IsDarkMode = Properties.Settings.Default.IsDarkMode;
                toggleDarkMode.IsChecked = IsDarkMode;
                ApplyTheme(IsDarkMode);

                // Ensure DataGrids are initially collapsed
                dataGridHistory.Visibility = Visibility.Collapsed;
                dataGridFavorites.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("error.log", $"{DateTime.Now}: Window_Loaded failed - {ex.Message}\n{ex.StackTrace}\n");
                MessageBox.Show($"Lỗi khi tải theme: {ex.Message}", "Lỗi Khởi động", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Favorites
        private void BtnAddFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(loggedInUser))
            {
                MessageBox.Show("Bạn cần đăng nhập để thêm từ yêu thích.");
                return;
            }

            string word = txtWord.Text.Trim();
            if (string.IsNullOrEmpty(word))
            {
                MessageBox.Show("Không có từ để thêm.");
                return;
            }

            bool success = db.AddFavorite(loggedInUser, word);
            MessageBox.Show(success ? $"Đã thêm '{word}' vào yêu thích." : $"Từ '{word}' đã có trong yêu thích.");

            if (success && dataGridFavorites.Visibility == Visibility.Visible)
            {
                ShowFavorites();
            }
        }

        private void ShowFavorites()
        {
            if (string.IsNullOrEmpty(loggedInUser))
            {
                MessageBox.Show("Bạn cần đăng nhập để xem danh sách yêu thích.", "Thông báo");
                return;
            }

            var favorites = db.GetFavorites(loggedInUser);

            if (favorites.Count == 0)
            {
                MessageBox.Show("Chưa có từ yêu thích nào.", "Thông báo");
                dataGridFavorites.Visibility = Visibility.Collapsed;
                return;
            }

            dataGridFavorites.ItemsSource = null;
            dataGridFavorites.ItemsSource = favorites;
            dataGridFavorites.Visibility = Visibility.Visible;

            dataGridHistory.Visibility = Visibility.Collapsed;
        }

        private void BtnShowFavorites_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(loggedInUser))
            {
                MessageBox.Show("Bạn cần đăng nhập để xem danh sách yêu thích.", "Thông báo");
                return;
            }

            if (!isFavoritesVisible)
            {
                var favorites = db.GetFavorites(loggedInUser);
                if (favorites.Count == 0)
                {
                    MessageBox.Show("Chưa có từ yêu thích nào.", "Thông báo");
                    dataGridFavorites.Visibility = Visibility.Collapsed;
                    txtResult.Visibility = Visibility.Visible;
                    return;
                }

                dataGridFavorites.ItemsSource = null;
                dataGridFavorites.ItemsSource = favorites;
                dataGridFavorites.Visibility = Visibility.Visible;

                dataGridHistory.Visibility = Visibility.Collapsed;
                txtResult.Visibility = Visibility.Collapsed;

                isFavoritesVisible = true;
                isHistoryVisible = false;
            }
            else
            {
                dataGridFavorites.Visibility = Visibility.Collapsed;
                txtResult.Visibility = Visibility.Visible;
                isFavoritesVisible = false;
            }
        }

        private void DeleteFavorite_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var favoriteItem = button?.DataContext as Connect.Favorite;

            if (favoriteItem == null)
            {
                MessageBox.Show("Không thể xóa. Vui lòng thử lại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show($"Bạn có chắc muốn xóa từ '{favoriteItem.Word}' khỏi danh sách yêu thích?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                bool success = db.DeleteFavorite(loggedInUser, favoriteItem.Word, favoriteItem.FavoriteDate);
                if (success)
                {
                    var favorites = db.GetFavorites(loggedInUser);
                    dataGridFavorites.ItemsSource = null;
                    dataGridFavorites.ItemsSource = favorites;

                    if (favorites.Count == 0)
                    {
                        dataGridFavorites.Visibility = Visibility.Collapsed;
                        txtResult.Visibility = Visibility.Visible;
                        isFavoritesVisible = false;
                    }
                }
                else
                {
                    MessageBox.Show("Không thể xóa từ khỏi danh sách yêu thích.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region DeleteAll
        private void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(loggedInUser))
            {
                MessageBox.Show("Bạn cần đăng nhập để xóa tất cả.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Bạn có chắc muốn xóa TẤT CẢ lịch sử và từ yêu thích?", "Xác nhận xóa tất cả", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                bool historyDeleted = db.DeleteAllHistory(loggedInUser);
                bool favoritesDeleted = db.DeleteAllFavorites(loggedInUser);

                if (historyDeleted || favoritesDeleted)
                {
                    // Cập nhật giao diện
                    if (dataGridHistory.Visibility == Visibility.Visible)
                    {
                        dataGridHistory.ItemsSource = null;
                        dataGridHistory.Visibility = Visibility.Collapsed;
                        txtResult.Visibility = Visibility.Visible;
                        isHistoryVisible = false;
                    }

                    if (dataGridFavorites.Visibility == Visibility.Visible)
                    {
                        dataGridFavorites.ItemsSource = null;
                        dataGridFavorites.Visibility = Visibility.Collapsed;
                        txtResult.Visibility = Visibility.Visible;
                        isFavoritesVisible = false;
                    }

                    MessageBox.Show("Đã xóa tất cả lịch sử và từ yêu thích thành công.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Không có dữ liệu để xóa hoặc xảy ra lỗi.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

      
    }
}