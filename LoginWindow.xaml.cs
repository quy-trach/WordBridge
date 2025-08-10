using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using WordBridge.Database;
using System.Configuration;

namespace WordBridge
{
    public partial class LoginWindow : Window
    {
        private readonly Connect db = new Connect();
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("1234567890123456"); // Khóa 16 byte
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("1234567890123456"); // Vector khởi tạo 16 byte

        public string LoggedInUsername { get; private set; } = "";

        public LoginWindow()
        {
            InitializeComponent();
            LoadSavedCredentials();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string fullname;
                if (db.ValidateLogin(username, password, out fullname))
                {
                    if (chkRememberMe.IsChecked == true)
                    {
                        Properties.Settings.Default.Username = username;
                        Properties.Settings.Default.Password = EncryptString(password);
                        Properties.Settings.Default.RememberMe = true;
                        Properties.Settings.Default.Save();
                    }
                    else
                    {
                        Properties.Settings.Default.Username = string.Empty;
                        Properties.Settings.Default.Password = string.Empty;
                        Properties.Settings.Default.RememberMe = false;
                        Properties.Settings.Default.Save();
                    }

                    MessageBox.Show($"Đăng nhập thành công! Xin chào {fullname}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoggedInUsername = username;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Tên đăng nhập hoặc mật khẩu không đúng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đăng nhập: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var forgotPasswordWindow = new ForgotPasswordWindow();
            forgotPasswordWindow.ShowDialog();
        }

        private void LoadSavedCredentials()
        {
            try
            {
                if (Properties.Settings.Default.RememberMe)
                {
                    txtUsername.Text = Properties.Settings.Default.Username;
                    try
                    {
                        txtPassword.Password = DecryptString(Properties.Settings.Default.Password);
                    }
                    catch
                    {
                        txtPassword.Password = string.Empty;
                    }
                    chkRememberMe.IsChecked = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải thông tin đăng nhập: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                byte[] encrypted;
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] input = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(input, 0, input.Length);
                    }
                    encrypted = ms.ToArray();
                }
                return Convert.ToBase64String(encrypted);
            }
        }

        private string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (var ms = new MemoryStream(cipherBytes))
                {
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (var sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}