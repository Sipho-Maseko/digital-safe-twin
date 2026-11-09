using System;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin
{
    public partial class Default : Page
    {
        // sends user to their starting page depending on role,,,
        protected void Page_Load(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(Auth.CurrentUserId))
            {
                Response.Redirect("~/Login.aspx", true);
                return;
            }

            string role = (Auth.CurrentRole ?? "").ToLowerInvariant();
            switch (role)
            {
                case "student":
                    Response.Redirect("~/Student/Report.aspx", true);
                    break;
                case "guard":
                    Response.Redirect("~/Security/Map.aspx", true);
                    break;
                case "supervisor":
                    Response.Redirect("~/Admin/Hotspots.aspx", true);
                    break;
                default:
                    Response.Redirect("~/Login.aspx", true);
                    break;
            }
        }
    }
}
