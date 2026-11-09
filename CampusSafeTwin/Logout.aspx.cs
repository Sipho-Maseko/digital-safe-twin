using System;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin
{
    public partial class Logout : Page
    {
        // logs out current session and sends to login page,,,
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Logout();
            Response.Redirect("~/Login.aspx", true);
        }
    }
}
