using System;
using System.Data;
using System.Security.Cryptography;
using System.Web;
using System.Web.UI;
using Npgsql;

namespace CampusSafeTwin.Classes
{
    public static class Auth
    {
        // hashes the pasword using pbkdf2 with salt...
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(32);
                return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
            }
        }

        // cheks if the pasword matches the hash from db,,,
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            var parts = storedHash.Split(':');
            if (parts.Length != 2)
                return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256))
                {
                    byte[] actualHash = pbkdf2.GetBytes(32);
                    if (actualHash.Length != expectedHash.Length)
                        return false;

                    uint diff = 0;
                    for (int i = 0; i < actualHash.Length; i++)
                    {
                        diff |= (uint)(actualHash[i] ^ expectedHash[i]);
                    }
                    return diff == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        // logs in the user and puts stuff in sesion...
        public static bool Login(string email, string password, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Please enter both email and password.";
                return false;
            }

            const string sql = @"
                SELECT user_id, email, full_name, role::text AS role, password_hash, is_active 
                FROM users 
                WHERE LOWER(email) = LOWER(@email);";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@email", email.Trim()));
            if (dt.Rows.Count == 0)
            {
                errorMessage = "Invalid email or password.";
                return false;
            }

            var row = dt.Rows[0];
            bool isActive = row["is_active"] != DBNull.Value && Convert.ToBoolean(row["is_active"]);
            if (!isActive)
            {
                errorMessage = "This account has been deactivated. Please contact campus security.";
                return false;
            }

            string storedHash = row["password_hash"] != DBNull.Value ? row["password_hash"].ToString() : "";
            if (!VerifyPassword(password, storedHash))
            {
                errorMessage = "Invalid email or password.";
                return false;
            }

            var session = HttpContext.Current.Session;
            session["UserId"] = row["user_id"].ToString();
            session["Role"] = row["role"].ToString();
            session["FullName"] = row["full_name"].ToString();
            session["Email"] = row["email"].ToString();

            return true;
        }

        // clears the sesion when logging out...
        public static void Logout()
        {
            var session = HttpContext.Current.Session;
            if (session != null)
            {
                session.Clear();
                session.Abandon();
            }
        }

        // makes sure usr is logged in and has right role,,,
        public static void Require(Page p, params string[] roles)
        {
            if (p == null) return;

            if (p.Session == null || p.Session["UserId"] == null)
            {
                p.Response.Redirect("~/Login.aspx", true);
                return;
            }

            if (roles != null && roles.Length > 0)
            {
                string currentRole = (p.Session["Role"] as string) ?? "";
                bool isAuthorized = false;
                foreach (var r in roles)
                {
                    if (string.Equals(r, currentRole, StringComparison.OrdinalIgnoreCase))
                    {
                        isAuthorized = true;
                        break;
                    }
                }

                if (!isAuthorized)
                {
                    p.Response.Redirect("~/Default.aspx", true);
                }
            }
        }

        // adds a new student acount to the database...
        public static bool RegisterStudent(string fullName, string email, string studentNumber, string password, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Full name, email, and password are required.";
                return false;
            }

            string passwordHash = HashPassword(password);

            const string sql = @"
                INSERT INTO users (full_name, email, student_number, role, password_hash, is_active)
                VALUES (@full_name, @email, @student_number, 'student'::user_role, @password_hash, true)
                RETURNING user_id;";

            try
            {
                var newId = Db.ExecuteScalar(sql,
                    Db.Param("@full_name", fullName.Trim()),
                    Db.Param("@email", email.Trim().ToLowerInvariant()),
                    Db.Param("@student_number", string.IsNullOrWhiteSpace(studentNumber) ? (object)DBNull.Value : studentNumber.Trim()),
                    Db.Param("@password_hash", passwordHash));

                return newId != null;
            }
            catch (PostgresException ex)
            {
                if (ex.SqlState == "23505")
                {
                    errorMessage = "An account with this email address already exists.";
                }
                else
                {
                    errorMessage = "Registration failed: " + ex.MessageText;
                }
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Registration failed: " + ex.Message;
                return false;
            }
        }

        public static string CurrentUserId => HttpContext.Current?.Session?["UserId"] as string;
        public static string CurrentRole => HttpContext.Current?.Session?["Role"] as string;
        public static string CurrentFullName => HttpContext.Current?.Session?["FullName"] as string;
    }
}
