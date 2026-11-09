using System;
using System.Configuration;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin
{
    public partial class SiteMaster : MasterPage
    {
        public string MapCenterLat => ConfigurationManager.AppSettings["MapCenterLat"] ?? "-26.6890";
        public string MapCenterLng => ConfigurationManager.AppSettings["MapCenterLng"] ?? "27.0935";

        public string UserRole => (Session["Role"] as string) ?? "";
        public string UserName => (Session["FullName"] as string) ?? "";

        // formats role label for display...
        public string UserRoleTitle
        {
            get
            {
                switch (UserRole.ToLowerInvariant())
                {
                    case "student": return "Student";
                    case "guard": return "Security Officer";
                    case "supervisor": return "Supervisor";
                    default: return "";
                }
            }
        }

        public int UnreadCount { get; set; } = 0;
        public string UnreadBadgeHtml => UnreadCount > 0 ? ("<span class=\"badge-counter\">(" + UnreadCount.ToString() + ")</span>") : "";

        // checks user role and shows matching menu links,,,
        protected void Page_Load(object sender, EventArgs e)
        {
            bool isLoggedIn = !string.IsNullOrEmpty(Auth.CurrentUserId);

            phLoggedIn.Visible = isLoggedIn;
            phLoggedOut.Visible = !isLoggedIn;

            if (isLoggedIn)
            {
                litUserName.Text = Server.HtmlEncode(UserName);

                if (Guid.TryParse(Auth.CurrentUserId, out Guid uid))
                {
                    try
                    {
                        const string countSql = "SELECT COUNT(*) FROM notifications WHERE recipient_id = @me AND is_read = false;";
                        UnreadCount = Convert.ToInt32(Db.ExecuteScalar(countSql, Db.Param("@me", uid)));
                    }
                    catch { }
                }

                string role = UserRole.ToLowerInvariant();
                phStudentNav.Visible = (role == "student");
                phGuardNav.Visible = (role == "guard");
                phSupervisorNav.Visible = (role == "supervisor");
            }
            else
            {
                phStudentNav.Visible = false;
                phGuardNav.Visible = false;
                phSupervisorNav.Visible = false;
            }
        }
    }
}
