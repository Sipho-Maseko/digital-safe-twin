using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Admin
{
    public partial class Users : Page
    {
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.Literal litUserCount;
        protected global::System.Web.UI.WebControls.DropDownList ddlRoleFilter;
        protected global::System.Web.UI.WebControls.GridView gvUsers;
        protected global::System.Web.UI.WebControls.TextBox txtFullName;
        protected global::System.Web.UI.WebControls.TextBox txtEmail;
        protected global::System.Web.UI.WebControls.DropDownList ddlRole;
        protected global::System.Web.UI.WebControls.TextBox txtBadge;
        protected global::System.Web.UI.WebControls.TextBox txtPhone;
        protected global::System.Web.UI.WebControls.TextBox txtPassword;
        protected global::System.Web.UI.WebControls.Button btnCreateUser;

        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "supervisor");

            if (!IsPostBack)
            {
                LoadUsers();
            }
        }

        protected void ddlRoleFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadUsers();
        }

        // loads user accounts list from db,,,
        private void LoadUsers()
        {
            string roleFilter = ddlRoleFilter.SelectedValue;
            string sql = @"
                SELECT user_id, email, full_name, role::text AS role,
                       phone, student_number, badge_number, is_active,
                       to_char(created_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD') AS created_fmt
                FROM users";

            if (!string.IsNullOrEmpty(roleFilter))
            {
                sql += " WHERE role = @role::user_role";
            }
            sql += " ORDER BY role ASC, full_name ASC;";

            DataTable dt;
            if (!string.IsNullOrEmpty(roleFilter))
            {
                dt = Db.ExecuteDataTable(sql, Db.Param("@role", roleFilter));
            }
            else
            {
                dt = Db.ExecuteDataTable(sql);
            }

            gvUsers.DataSource = dt;
            gvUsers.DataBind();
            litUserCount.Text = dt.Rows.Count.ToString();
        }

        // creates a new user account...
        protected void btnCreateUser_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            string fullName = txtFullName.Text.Trim();
            string email = txtEmail.Text.Trim().ToLowerInvariant();
            string role = ddlRole.SelectedValue;
            string badge = txtBadge.Text.Trim();
            string phone = txtPhone.Text.Trim();
            string password = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                pnlError.Visible = true;
                litError.Text = "Full name, email, and password are required.";
                return;
            }

            try
            {
                string hash = Auth.HashPassword(password);
                string studentNumber = (role == "student") ? badge : null;
                string badgeNumber = (role != "student") ? badge : null;

                const string sql = @"
                    INSERT INTO users (email, full_name, role, phone, student_number, badge_number, password_hash)
                    VALUES (@email, @fullName, @role::user_role, @phone, @studentNumber, @badgeNumber, @hash);";

                Db.ExecuteNonQuery(sql,
                    Db.Param("@email", email),
                    Db.Param("@fullName", fullName),
                    Db.Param("@role", role),
                    Db.Param("@phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone),
                    Db.Param("@studentNumber", string.IsNullOrEmpty(studentNumber) ? (object)DBNull.Value : studentNumber),
                    Db.Param("@badgeNumber", string.IsNullOrEmpty(badgeNumber) ? (object)DBNull.Value : badgeNumber),
                    Db.Param("@hash", hash));

                pnlSuccess.Visible = true;
                litSuccess.Text = $"User account for \"{Server.HtmlEncode(fullName)}\" ({role}) created successfully!";
                txtFullName.Text = "";
                txtEmail.Text = "";
                txtBadge.Text = "";
                txtPhone.Text = "";
                txtPassword.Text = "CampusSafe2026!";
                LoadUsers();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to create user: " + Server.HtmlEncode(ex.Message);
            }
        }

        // handles deactivate and reset pasword buttons,,,
        protected void gvUsers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActive")
            {
                Guid targetId = Guid.Parse(e.CommandArgument.ToString());

                // stops a supervisor locking themselfs out of the system,,,
                if (string.Equals(targetId.ToString(), Auth.CurrentUserId, StringComparison.OrdinalIgnoreCase))
                {
                    pnlError.Visible = true;
                    litError.Text = "You cannot deactivate your own account.";
                    return;
                }

                try
                {
                    const string sql = "UPDATE users SET is_active = NOT is_active WHERE user_id = @id;";
                    Db.ExecuteNonQuery(sql, Db.Param("@id", targetId));
                    pnlSuccess.Visible = true;
                    litSuccess.Text = "User active status updated.";
                    LoadUsers();
                }
                catch (Exception ex)
                {
                    pnlError.Visible = true;
                    litError.Text = "Error updating user status: " + Server.HtmlEncode(ex.Message);
                }
            }
            else if (e.CommandName == "ResetPass")
            {
                Guid targetId = Guid.Parse(e.CommandArgument.ToString());
                try
                {
                    string newHash = Auth.HashPassword("CampusSafe2026!");
                    const string sql = "UPDATE users SET password_hash = @hash WHERE user_id = @id;";
                    Db.ExecuteNonQuery(sql, Db.Param("@hash", newHash), Db.Param("@id", targetId));
                    pnlSuccess.Visible = true;
                    litSuccess.Text = "Password reset to \"CampusSafe2026!\".";
                    LoadUsers();
                }
                catch (Exception ex)
                {
                    pnlError.Visible = true;
                    litError.Text = "Error resetting password: " + Server.HtmlEncode(ex.Message);
                }
            }
        }
    }
}
