using System;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Security
{
    public partial class Map : Page
    {
        protected global::System.Web.UI.WebControls.GridView gvOpenIncidents;
        protected global::System.Web.UI.WebControls.Panel pnlNoIncidents;

        // makes sure only security or manager can see this map,,,
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "guard", "supervisor");

            if (!IsPostBack)
            {
                LoadOpenIncidents();
            }
        }

        // loads unresolved incidents from the db to show in list...
        private void LoadOpenIncidents()
        {
            const string sql = @"
                SELECT i.incident_id,
                       'CS-' || upper(substring(i.incident_id::text from 1 for 8)) AS ref_number,
                       i.category::text AS category,
                       i.status::text AS status,
                       to_char(i.occurred_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS occurred_fmt,
                       to_char(i.reported_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS reported_fmt,
                       COALESCE(u.full_name, 'Anonymous') AS reporter_name
                FROM incidents i
                LEFT JOIN users u ON u.user_id = i.reporter_id
                WHERE i.invalid_flag = false
                  AND i.status <> 'Resolved'
                ORDER BY i.reported_at DESC;";

            var dt = Db.ExecuteDataTable(sql);
            gvOpenIncidents.DataSource = dt;
            gvOpenIncidents.DataBind();

            pnlNoIncidents.Visible = (dt.Rows.Count == 0);
            gvOpenIncidents.Visible = (dt.Rows.Count > 0);
        }
    }
}
