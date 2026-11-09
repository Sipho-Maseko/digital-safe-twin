using System;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin
{
    public partial class Register : Page
    {
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.TextBox txtFullName;
        protected global::System.Web.UI.WebControls.TextBox txtEmail;
        protected global::System.Web.UI.WebControls.TextBox txtStudentNumber;
        protected global::System.Web.UI.WebControls.TextBox txtPassword;
        protected global::System.Web.UI.WebControls.TextBox txtConfirmPassword;
        protected global::System.Web.UI.WebControls.Button btnRegister;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && !string.IsNullOrEmpty(Auth.CurrentUserId))
            {
                Response.Redirect("~/Default.aspx", true);
            }
        }

        // validates form inputs and registers new student account...
        protected void btnRegister_Click(object sender, EventArgs e)
        {
            pnlError.Visible = false;

            string fullName = txtFullName.Text.Trim();
            string email = txtEmail.Text.Trim();
            string studentNum = txtStudentNumber.Text.Trim();
            string password = txtPassword.Text;
            string confirm = txtConfirmPassword.Text;

            if (password != confirm)
            {
                pnlError.Visible = true;
                litError.Text = "Passwords do not match.";
                return;
            }

            if (password.Length < 6)
            {
                pnlError.Visible = true;
                litError.Text = "Password must be at least 6 characters long.";
                return;
            }

            if (Auth.RegisterStudent(fullName, email, studentNum, password, out string errorMsg))
            {
                Response.Redirect("~/Login.aspx?registered=1", true);
            }
            else
            {
                pnlError.Visible = true;
                litError.Text = Server.HtmlEncode(errorMsg);
            }
        }
    }
}
