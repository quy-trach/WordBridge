using Org.BouncyCastle.Crypto.Generators;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace WordBridge.Database
{
	public class Connect
	{
		#region DBconnect
		// Chuỗi kết nối
		private string connectionString =
			"Data Source=laptop-567tvrsm;Initial Catalog=WordBridgeDB;User ID=sa;Password=123456;Encrypt=False;TrustServerCertificate=True";
        #endregion

        #region Account
        public bool IsUsernameExists(string username)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username = @Username", conn);
                    cmd.Parameters.AddWithValue("@Username", username);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kiểm tra tên đăng nhập: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return true;
            }
        }

        public bool Register(string username, string password, string fullname, string email)
        {
            try
            {
                // Kiểm tra định dạng email
                if (!IsValidMail(email))
                {
                    MessageBox.Show("Email không hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Kiểm tra độ mạnh mật khẩu
                if (!IsValidPassword(password))
                {
                    MessageBox.Show("Mật khẩu phải ít nhất 6 ký tự, gồm chữ và số!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Kiểm tra username đã tồn tại
                    var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username = @Username", conn);
                    checkCmd.Parameters.AddWithValue("@Username", username);
                    int count = (int)checkCmd.ExecuteScalar();
                    if (count > 0)
                    {
                        MessageBox.Show("Tên đăng nhập đã tồn tại. Vui lòng chọn tên khác!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    // Kiểm tra email đã tồn tại
                    var checkEmailCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", conn);
                    checkEmailCmd.Parameters.AddWithValue("@Email", email);
                    int emailCount = (int)checkEmailCmd.ExecuteScalar();
                    if (emailCount > 0)
                    {
                        MessageBox.Show("Email đã được sử dụng. Vui lòng dùng email khác!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    string hashedPassword = HashPassword(password); // Mã hóa mật khẩu với BCrypt

                    var cmd = new SqlCommand("INSERT INTO Users (Username, Password, FullName, Email) VALUES (@Username, @Password, @FullName, @Email)", conn);
                    cmd.Parameters.AddWithValue("@Username", username);
                    cmd.Parameters.AddWithValue("@Password", hashedPassword);
                    cmd.Parameters.AddWithValue("@FullName", fullname);
                    cmd.Parameters.AddWithValue("@Email", email);

                    int rows = cmd.ExecuteNonQuery();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đăng ký: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool ValidateLogin(string username, string password, out string fullname)
        {
            fullname = null; // Giá trị mặc định nếu không tìm thấy
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Lấy mật khẩu
                    var cmd = new SqlCommand("SELECT Password FROM Users WHERE Username = @Username", conn);
                    cmd.Parameters.AddWithValue("@Username", username);
                    var storedHash = cmd.ExecuteScalar()?.ToString();

                    if (storedHash == null)
                        return false;

                    // Nếu mật khẩu đúng thì lấy FullName
                    if (VerifyPassword(password, storedHash))
                    {
                        var fullnameCmd = new SqlCommand("SELECT FullName FROM Users WHERE Username = @Username", conn);
                        fullnameCmd.Parameters.AddWithValue("@Username", username);
                        fullname = fullnameCmd.ExecuteScalar()?.ToString();
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đăng nhập: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region History
        // Lưu lịch sử tra từ
        public void SaveHistory(string username, string word)
		{
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();
					var cmd = new SqlCommand("INSERT INTO History (Username, Word, LookupDate) VALUES (@Username, @Word, @LookupDate)", conn);
					cmd.Parameters.AddWithValue("@Username", username);
					cmd.Parameters.AddWithValue("@Word", word);
					cmd.Parameters.AddWithValue("@LookupDate", DateTime.Now);
					cmd.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Lỗi lưu lịch sử: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		// Lấy danh sách lịch sử
		public List<HistoryItem> GetHistory(string username)
		{
			var list = new List<HistoryItem>();
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();
					var cmd = new SqlCommand("SELECT Word, LookupDate FROM History WHERE Username = @Username ORDER BY LookupDate DESC", conn);
					cmd.Parameters.AddWithValue("@Username", username);
					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							list.Add(new HistoryItem
							{
								Word = reader["Word"].ToString(),
								LookupDate = reader.GetDateTime(1)
							});
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Lỗi lấy lịch sử: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			return list;
		}

		// Xóa lịch sử
		public bool DeleteHistory(string username, string word, DateTime lookupDate)
		{
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();
					var cmd = new SqlCommand("DELETE FROM History WHERE Username = @Username AND Word = @Word AND LookupDate = @LookupDate", conn);
					cmd.Parameters.AddWithValue("@Username", username);
					cmd.Parameters.AddWithValue("@Word", word);
					cmd.Parameters.AddWithValue("@LookupDate", lookupDate);
					int rows = cmd.ExecuteNonQuery();
					return rows > 0;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Lỗi xóa lịch sử: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
		}

		// Xóa tất cả
		public bool DeleteAllHistory(string username)
		{
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();
					var cmd = new SqlCommand("DELETE FROM History WHERE Username = @Username", conn);
					cmd.Parameters.AddWithValue("@Username", username);
					int rows = cmd.ExecuteNonQuery();
					return rows > 0;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Lỗi xóa tất cả lịch sử: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
		}
		#endregion

		#region Favorites
		//Thêm yêu thích
		public bool AddFavorite(string username, string word)
		{
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();

					string checkQuery = "SELECT COUNT(*) FROM Favorites WHERE Username = @Username AND Word = @Word";
					using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
					{
						checkCmd.Parameters.AddWithValue("@Username", username);
						checkCmd.Parameters.AddWithValue("@Word", word);
						int count = (int)checkCmd.ExecuteScalar();
						if (count > 0)
							return false;
					}

					string insertQuery = "INSERT INTO Favorites (Username, Word, FavoriteDate) VALUES (@Username, @Word, @FavoriteDate)";
					using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
					{
						var currentTime = DateTime.Now;
						insertCmd.Parameters.AddWithValue("@Username", username);
						insertCmd.Parameters.AddWithValue("@Word", word);
						insertCmd.Parameters.AddWithValue("@FavoriteDate", currentTime);
						int rows = insertCmd.ExecuteNonQuery();
						return rows > 0;
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Lỗi thêm yêu thích: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
		}

		//Lấy danh sách yêu thích
		public List<Favorite> GetFavorites(string username)
		{
			var favorites = new List<Favorite>();

			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();
					string query = "SELECT Word, FavoriteDate FROM Favorites WHERE Username = @username ORDER BY FavoriteDate DESC";
					using (SqlCommand cmd = new SqlCommand(query, conn))
					{
						cmd.Parameters.AddWithValue("@username", username);
						using (var reader = cmd.ExecuteReader())
						{
							while (reader.Read())
							{
								favorites.Add(new Favorite
								{
									Word = reader.GetString(0),
									FavoriteDate = reader.GetDateTime(1)
								});
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Lỗi lấy danh sách yêu thích: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			return favorites;
		}

		// Xóa từng từ yêu thích
		public bool DeleteFavorite(string username, string word, DateTime favoriteDate)
		{
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();
					var cmd = new SqlCommand("DELETE FROM Favorites WHERE Username = @Username AND Word = @Word AND FavoriteDate = @FavoriteDate", conn);
					cmd.Parameters.AddWithValue("@Username", username);
					cmd.Parameters.AddWithValue("@Word", word);
					cmd.Parameters.AddWithValue("@FavoriteDate", favoriteDate);
					int rows = cmd.ExecuteNonQuery();
					return rows > 0;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Lỗi xóa yêu thích: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
		}

		//Xóa tất cả
		public bool DeleteAllFavorites(string username)
		{
			try
			{
				using (SqlConnection conn = new SqlConnection(connectionString))
				{
					conn.Open();
					var cmd = new SqlCommand("DELETE FROM Favorites WHERE Username = @Username", conn);
					cmd.Parameters.AddWithValue("@Username", username);
					int rows = cmd.ExecuteNonQuery();
					return rows > 0;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Lỗi xóa tất cả yêu thích: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
		}
        #endregion

        #region Class Helper
        public class Favorite
        {
            public string Word { get; set; }
            public DateTime FavoriteDate { get; set; }
        }

        public class HistoryItem
        {
            public string Word { get; set; }
            public DateTime LookupDate { get; set; }
        }

        // Kiểm tra email tồn tại
        public bool IsEmailExists(string email)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", conn);
                    cmd.Parameters.AddWithValue("@Email", email);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kiểm tra email: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Đặt lại mật khẩu
        public bool ResetPassword(string email, string newPassword)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string hashedPassword = HashPassword(newPassword);
                    var cmd = new SqlCommand("UPDATE Users SET Password = @Password WHERE Email = @Email", conn);
                    cmd.Parameters.AddWithValue("@Password", hashedPassword);
                    cmd.Parameters.AddWithValue("@Email", email);
                    int rows = cmd.ExecuteNonQuery();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đặt lại mật khẩu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password); // Sử dụng BCrypt.Net
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }

        public static bool IsValidMail(string strMail)
        {
            var r = new Regex(@"^([0-9a-zA-Z]([-\.\w]*[0-9a-zA-Z])*@([0-9a-zA-Z][-\w]*[0-9a-zA-Z]\.)+[a-zA-Z]{2,9})$");
            return !string.IsNullOrEmpty(strMail) && r.IsMatch(strMail);
        }

        public static bool IsValidPassword(string password)
        {
            return !string.IsNullOrEmpty(password) &&
                   password.Length >= 6 &&
                   password.Any(char.IsLetter) &&
                   password.Any(char.IsDigit);
        }
        #endregion
    }
}