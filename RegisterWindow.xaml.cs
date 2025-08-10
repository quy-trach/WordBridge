using System;
using System.Windows;
using WordBridge.Database;

namespace WordBridge
{
    public partial class RegisterWindow : Window
    {
        private readonly Connect db = new Connect();

        public string Username { get; private set; } = "";
        public string Password { get; private set; } = "";

        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string fullname = txtFullname.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Password.Trim();
            string confirmPassword = txtConfirmPassword.Password.Trim();

            // Kiểm tra các trường không được để trống
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(confirmPassword) || string.IsNullOrEmpty(fullname) ||
                string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Kiểm tra mật khẩu xác nhận
            if (password != confirmPassword)
            {
                MessageBox.Show("Mật khẩu xác nhận không khớp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (db.Register(username, password, fullname, email))
                {
                    MessageBox.Show("Đăng ký thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Username = username;
                    Password = password;
                    DialogResult = true;
                }
                else
                {
                    // Lỗi đã được hiển thị trong phương thức Register của Connect
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đăng ký: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}