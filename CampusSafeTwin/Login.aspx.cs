using System;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin
{
    public partial class Login : Page
    {
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.TextBox txtEmail;
        protected global::System.Web.UI.WebControls.TextBox txtPassword;
        protected global::System.Web.UI.WebControls.Button btnLogin;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!string.IsNullOrEmpty(Auth.CurrentUserId))
                {
                    Response.Redirect("~/Default.aspx", true);
                    return;
                }

                if (Request.QueryString["registered"] == "1")
                {
                    pnlSuccess.Visible = true;
                    litSuccess.Text = "Registration successful! You may now sign in with your email and password.";
                }
            }
        }

        // checks email and pasword to sign user in...
        protected void btnLogin_Click(object sender, EventArgs e)
        {
            pnlError.Visible = false;

            string email = txtEmail.Text;
            string password = txtPassword.Text;

            if (Auth.Login(email, password, out string errorMsg))
            {
                Response.Redirect("~/Default.aspx", true);
            }
            else
            {
                pnlError.Visible = true;
                litError.Text = Server.HtmlEncode(errorMsg);
            }
        }
    }
}
