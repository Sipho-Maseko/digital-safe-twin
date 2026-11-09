using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin
{
    public partial class Notifications : Page
    {
        protected global::System.Web.UI.WebControls.Button btnMarkAllRead;
        protected global::System.Web.UI.WebControls.Repeater rptNotifications;
        protected global::System.Web.UI.WebControls.Panel pnlNoNotifs;

        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this);

            if (!IsPostBack)
            {
                LoadNotifications();
            }
        }

        // gets unread and recent notifications for user...
        private void LoadNotifications()
        {
            Guid me = Guid.Parse(Auth.CurrentUserId);

            const string sql = @"
                SELECT notification_id, kind::text AS kind, title, body, incident_id, dispatch_id, is_read,
                       to_char(created_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS created_fmt
                FROM notifications
                WHERE recipient_id = @me
                ORDER BY is_read ASC, created_at DESC
                LIMIT 50;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@me", me));
            rptNotifications.DataSource = dt;
            rptNotifications.DataBind();

            pnlNoNotifs.Visible = (dt.Rows.Count == 0);
            rptNotifications.Visible = (dt.Rows.Count > 0);
        }

        protected void rptNotifications_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "MarkRead")
            {
                if (Guid.TryParse(e.CommandArgument.ToString(), out Guid notifId))
                {
                    const string sql = "UPDATE notifications SET is_read = true WHERE notification_id = @id AND recipient_id = @me;";
                    Db.ExecuteNonQuery(sql, Db.Param("@id", notifId), Db.Param("@me", Guid.Parse(Auth.CurrentUserId)));
                    LoadNotifications();
                }
            }
        }

        // marks every notification as read,,,
        protected void btnMarkAllRead_Click(object sender, EventArgs e)
        {
            const string sql = "UPDATE notifications SET is_read = true WHERE recipient_id = @me;";
            Db.ExecuteNonQuery(sql, Db.Param("@me", Guid.Parse(Auth.CurrentUserId)));
            LoadNotifications();
        }
    }
}
