using System;
using System.Net.Mail;
using System.Windows;
using WordBridge.Database;

namespace WordBridge
{
    public partial class ForgotPasswordWindow : Window
    {
        private readonly Connect db = new Connect();
        private string generatedCode;
        private string userEmail;
        private DateTime codeGeneratedTime;

        public ForgotPasswordWindow()
        {
            InitializeComponent();
            // Khởi tạo trạng thái ban đầu
            txtVerificationCode.IsEnabled = false;
            btnVerifyCode.IsEnabled = false;
            passwordPanel.Visibility = Visibility.Collapsed;
        }

        private void BtnSendCode_Click(object sender, RoutedEventArgs e)
        {
            userEmail = txtEmail.Text.Trim();
            if (string.IsNullOrEmpty(userEmail))
            {
                MessageBox.Show("Vui lòng nhập email.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Connect.IsValidMail(userEmail))
            {
                MessageBox.Show("Email không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!db.IsEmailExists(userEmail))
            {
                MessageBox.Show("Email không tồn tại trong hệ thống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                btnSendCode.IsEnabled = false;
                btnSendCode.Content = "Đang gửi...";
                generatedCode = new Random().Next(100000, 999999).ToString();
                codeGeneratedTime = DateTime.Now;

                SendVerificationEmail(userEmail, generatedCode);
                MessageBox.Show("Mã xác nhận đã được gửi đến email của bạn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                txtVerificationCode.IsEnabled = true;
                btnVerifyCode.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi gửi mã xác nhận: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSendCode.IsEnabled = true;
                btnSendCode.Content = "Gửi mã xác nhận";
            }
        }

        private void BtnVerifyCode_Click(object sender, RoutedEventArgs e)
        {
            string code = txtVerificationCode.Text.Trim();

            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("Vui lòng nhập mã xác nhận.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (code != generatedCode)
            {
                MessageBox.Show("Mã xác nhận không đúng.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if ((DateTime.Now - codeGeneratedTime).TotalMinutes > 5)
            {
                MessageBox.Show("Mã xác nhận đã hết hạn. Vui lòng gửi lại mã mới.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                generatedCode = null;
                return;
            }

            // Hiển thị panel mật khẩu mới
            passwordPanel.Visibility = Visibility.Visible;
            txtVerificationCode.IsEnabled = false;
            btnVerifyCode.IsEnabled = false;
            btnSendCode.IsEnabled = false;
            txtEmail.IsEnabled = false; // Ngăn chỉnh sửa email
        }

        private void BtnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            string newPassword = txtNewPassword.Password.Trim();

            if (string.IsNullOrEmpty(newPassword))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu mới.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Connect.IsValidPassword(newPassword))
            {
                MessageBox.Show("Mật khẩu phải ít nhất 6 ký tự, gồm chữ và số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                bool success = db.ResetPassword(userEmail, newPassword);
                if (success)
                {
                    MessageBox.Show("Đặt lại mật khẩu thành công. Bạn có thể đăng nhập với mật khẩu mới.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show("Lỗi khi đặt lại mật khẩu. Vui lòng thử lại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đặt lại mật khẩu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendVerificationEmail(string toEmail, string code)
        {
            string fromEmail = "doanquy610@gmail.com"; // Thay bằng email của bạn
            string appPassword = "fupx cenl wdjn pbnh"; // Thay bằng App Password của Gmail

            MailMessage mail = new MailMessage
            {
                From = new MailAddress(fromEmail),
                Subject = "Mã xác nhận đặt lại mật khẩu - WordBridge",
                Body = $"Mã xác nhận của bạn là: <b>{code}</b><br>Mã này có hiệu lực trong 5 phút.",
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            SmtpClient smtp = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new System.Net.NetworkCredential(fromEmail, appPassword),
                EnableSsl = true
            };

            smtp.Send(mail);
        }
    }
}
