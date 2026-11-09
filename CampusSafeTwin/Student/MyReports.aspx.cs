using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Student
{
    public partial class MyReports : Page
    {
        protected global::System.Web.UI.WebControls.GridView gvMyReports;
        protected global::System.Web.UI.WebControls.Panel pnlNoReports;
        protected global::System.Web.UI.WebControls.Panel pnlTimeline;
        protected global::System.Web.UI.WebControls.Literal litSelectedRef;
        protected global::System.Web.UI.WebControls.Literal litSelectedCat;
        protected global::System.Web.UI.WebControls.Literal litSelectedReported;
        protected global::System.Web.UI.WebControls.Literal litSelectedDesc;
        protected global::System.Web.UI.WebControls.Repeater rptTimeline;

        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "student");

            if (!IsPostBack)
            {
                LoadReports();
            }
        }

        // gets past reports logged by current student...
        private void LoadReports()
        {
            Guid me = Guid.Parse(Auth.CurrentUserId);

            const string sql = @"
                SELECT incident_id,
                       'CS-' || upper(substring(incident_id::text from 1 for 8)) AS ref_number,
                       to_char(occurred_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS occurred_fmt,
                       to_char(reported_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS reported_fmt,
                       category::text AS category,
                       status::text AS status,
                       description
                FROM incidents
                WHERE reporter_id = @me
                ORDER BY reported_at DESC;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@me", me));

            gvMyReports.DataSource = dt;
            gvMyReports.DataBind();

            pnlNoReports.Visible = (dt.Rows.Count == 0);
            gvMyReports.Visible = (dt.Rows.Count > 0);
        }

        protected void gvMyReports_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ViewTimeline")
            {
                string incidentIdStr = e.CommandArgument.ToString();
                if (Guid.TryParse(incidentIdStr, out Guid incidentId))
                {
                    LoadTimeline(incidentId);
                }
            }
        }

        protected void gvMyReports_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            // custom table formatting if needed,,,
        }

        private void LoadTimeline(Guid incidentId)
        {
            Guid me = Guid.Parse(Auth.CurrentUserId);

            // loads incident details but only if it belongs to this student...
            const string sqlInc = @"
                SELECT 'CS-' || upper(substring(incident_id::text from 1 for 8)) AS ref_number,
                       category::text AS category,
                       description,
                       to_char(reported_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS reported_fmt
                FROM incidents
                WHERE incident_id = @id AND reporter_id = @me;";

            var dtInc = Db.ExecuteDataTable(sqlInc, Db.Param("@id", incidentId), Db.Param("@me", me));
            if (dtInc.Rows.Count == 0)
            {
                // not this students report so nothing gets shown,,,
                pnlTimeline.Visible = false;
                return;
            }

            var r = dtInc.Rows[0];
            litSelectedRef.Text = Server.HtmlEncode(r["ref_number"].ToString());
            litSelectedCat.Text = Server.HtmlEncode(r["category"].ToString());
            litSelectedReported.Text = Server.HtmlEncode(r["reported_fmt"].ToString());
            litSelectedDesc.Text = Server.HtmlEncode(r["description"] != DBNull.Value ? r["description"].ToString() : "None");

            // loads status update history,,,
            const string sqlHist = @"
                SELECT sh.old_status::text AS old_status,
                       sh.new_status::text AS new_status,
                       to_char(sh.changed_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS changed_fmt,
                       COALESCE(u.full_name, 'System') AS changed_by_name,
                       sh.note
                FROM status_history sh
                LEFT JOIN users u ON u.user_id = sh.changed_by
                WHERE sh.incident_id = @id
                ORDER BY sh.changed_at ASC;";

            var dtHist = Db.ExecuteDataTable(sqlHist, Db.Param("@id", incidentId));

            rptTimeline.DataSource = dtHist;
            rptTimeline.DataBind();
            pnlTimeline.Visible = true;
        }
    }
}
