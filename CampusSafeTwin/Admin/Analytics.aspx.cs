using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Admin
{
    public partial class Analytics : Page
    {
        protected global::System.Web.UI.WebControls.Button btnExportCsv;
        protected global::System.Web.UI.WebControls.Literal litTotalIncidents;
        protected global::System.Web.UI.WebControls.Literal litSosCount;
        protected global::System.Web.UI.WebControls.Literal litAvgResponseMin;
        protected global::System.Web.UI.WebControls.Literal litAvgResolveMin;
        protected global::System.Web.UI.WebControls.Repeater rptCategories;
        protected global::System.Web.UI.WebControls.Repeater rptStatuses;
        protected global::System.Web.UI.WebControls.GridView gvDispatches;

        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "supervisor");

            if (!IsPostBack)
            {
                LoadMetrics();
            }
        }

        private void LoadMetrics()
        {
            // counts total and emergency incidents,,,
            const string sqlCounts = @"
                SELECT count(*) AS total_count,
                       count(*) FILTER (WHERE is_emergency = true) AS emergency_count
                FROM incidents;";

            var dtCounts = Db.ExecuteDataTable(sqlCounts);
            int total = 0;
            if (dtCounts.Rows.Count > 0)
            {
                total = Convert.ToInt32(dtCounts.Rows[0]["total_count"]);
                int emergency = Convert.ToInt32(dtCounts.Rows[0]["emergency_count"]);
                litTotalIncidents.Text = total.ToString();
                litSosCount.Text = emergency.ToString();
            }

            // gets average response and resolve times...
            const string sqlAvgTimes = @"
                SELECT ROUND(AVG(EXTRACT(EPOCH FROM (arrived_at - dispatched_at))/60)::numeric, 1) AS avg_resp,
                       ROUND(AVG(EXTRACT(EPOCH FROM (closed_at - dispatched_at))/60)::numeric, 1) AS avg_res
                FROM dispatches
                WHERE arrived_at IS NOT NULL;";

            var dtAvg = Db.ExecuteDataTable(sqlAvgTimes);
            if (dtAvg.Rows.Count > 0 && dtAvg.Rows[0]["avg_resp"] != DBNull.Value)
            {
                litAvgResponseMin.Text = dtAvg.Rows[0]["avg_resp"] + " min";
                litAvgResolveMin.Text = dtAvg.Rows[0]["avg_res"] != DBNull.Value ? dtAvg.Rows[0]["avg_res"] + " min" : "-";
            }
            else
            {
                // no dispatch has logged an arrival yet, so there is nothing to average,,,
                const string noneYet = "<span style=\"font-size:0.95rem; font-weight:600;\">No completed<br/>dispatches yet</span>";
                litAvgResponseMin.Text = noneYet;
                litAvgResolveMin.Text = noneYet;
            }

            // counts incidents per category,,,
            const string sqlCat = @"
                SELECT category::text AS category_name, count(*) AS count
                FROM incidents
                GROUP BY category
                ORDER BY count DESC;";

            // anonymous objects because Eval cant read dictionary keys,,,
            var dtCat = Db.ExecuteDataTable(sqlCat);
            var catList = new List<object>();
            foreach (DataRow r in dtCat.Rows)
            {
                int c = Convert.ToInt32(r["count"]);
                int pct = total > 0 ? (int)Math.Round((double)c / total * 100.0) : 0;
                catList.Add(new
                {
                    category_name = r["category_name"].ToString().Replace("_", " ").ToUpperInvariant(),
                    count = c,
                    pct = pct
                });
            }
            rptCategories.DataSource = catList;
            rptCategories.DataBind();

            // gets counts per incident status...
            const string sqlStat = @"
                SELECT status::text AS status, count(*) AS count
                FROM incidents
                GROUP BY status
                ORDER BY array_position(array['New','Acknowledged','In Progress','Resolved']::text[], status::text);";

            var dtStat = Db.ExecuteDataTable(sqlStat);
            var statList = new List<object>();
            foreach (DataRow r in dtStat.Rows)
            {
                string s = r["status"].ToString();
                int c = Convert.ToInt32(r["count"]);
                int pct = total > 0 ? (int)Math.Round((double)c / total * 100.0) : 0;
                string color = "#6c757d";
                if (s == "New") color = "#dc3545";
                else if (s == "Acknowledged") color = "#fd7e14";
                else if (s == "In Progress") color = "#0d6efd";
                else if (s == "Resolved") color = "#198754";

                statList.Add(new
                {
                    status = s,
                    count = c,
                    pct = pct,
                    bar_color = color
                });
            }
            rptStatuses.DataSource = statList;
            rptStatuses.DataBind();

            // loads recent dispatch history with response times,,,
            const string sqlDisp = @"
                SELECT substring(d.dispatch_id::text from 1 for 8) AS dispatch_ref,
                       d.status::text AS dispatch_status,
                       to_char(d.dispatched_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS dispatched_fmt,
                       COALESCE(u.full_name, 'Auto-assigned') AS guard_name,
                       i.category::text AS category,
                       i.is_emergency,
                       ROUND((EXTRACT(EPOCH FROM (d.arrived_at - d.dispatched_at))/60)::numeric, 1) AS response_minutes,
                       ROUND((EXTRACT(EPOCH FROM (d.closed_at - d.dispatched_at))/60)::numeric, 1) AS resolve_minutes
                FROM dispatches d
                JOIN incidents i ON i.incident_id = d.incident_id
                LEFT JOIN users u ON u.user_id = d.guard_id
                ORDER BY d.dispatched_at DESC
                LIMIT 25;";

            var dtDisp = Db.ExecuteDataTable(sqlDisp);
            gvDispatches.DataSource = dtDisp;
            gvDispatches.DataBind();
        }

        protected void btnExportCsv_Click(object sender, EventArgs e)
        {
            const string sql = @"
                SELECT i.incident_id,
                       'CS-' || upper(substring(i.incident_id::text from 1 for 8)) AS ref_code,
                       i.category::text AS category,
                       i.status::text AS status,
                       i.is_emergency,
                       i.latitude,
                       i.longitude,
                       to_char(i.occurred_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI:SS') AS occurred_at,
                       to_char(i.reported_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI:SS') AS reported_at,
                       COALESCE(u.full_name, 'Anonymous') AS reporter_name,
                       i.invalid_flag,
                       COALESCE(i.description, '') AS description
                FROM incidents i
                LEFT JOIN users u ON u.user_id = i.reporter_id
                ORDER BY i.reported_at DESC;";

            var dt = Db.ExecuteDataTable(sql);

            var sb = new StringBuilder();
            sb.AppendLine("Incident ID,Reference,Category,Status,Is Emergency,Latitude,Longitude,Occurred At,Reported At,Reporter,Invalid Flag,Description");

            foreach (DataRow r in dt.Rows)
            {
                sb.Append(EscapeCsv(r["incident_id"].ToString())).Append(",");
                sb.Append(EscapeCsv(r["ref_code"].ToString())).Append(",");
                sb.Append(EscapeCsv(r["category"].ToString())).Append(",");
                sb.Append(EscapeCsv(r["status"].ToString())).Append(",");
                sb.Append(Convert.ToBoolean(r["is_emergency"]) ? "TRUE" : "FALSE").Append(",");
                sb.Append(Convert.ToDouble(r["latitude"]).ToString("F6", System.Globalization.CultureInfo.InvariantCulture)).Append(",");
                sb.Append(Convert.ToDouble(r["longitude"]).ToString("F6", System.Globalization.CultureInfo.InvariantCulture)).Append(",");
                sb.Append(EscapeCsv(r["occurred_at"].ToString())).Append(",");
                sb.Append(EscapeCsv(r["reported_at"].ToString())).Append(",");
                sb.Append(EscapeCsv(r["reporter_name"].ToString())).Append(",");
                sb.Append(Convert.ToBoolean(r["invalid_flag"]) ? "TRUE" : "FALSE").Append(",");
                sb.AppendLine(EscapeCsv(r["description"].ToString()));
            }

            Response.Clear();
            Response.Buffer = true;
            Response.ContentType = "text/csv; charset=utf-8";
            Response.AddHeader("Content-Disposition", "attachment;filename=campussafe_incidents_export.csv");
            Response.Charset = "utf-8";
            Response.Output.Write(sb.ToString());
            Response.Flush();
            Response.SuppressContent = true;
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        private static string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return "\"\"";
            return "\"" + val.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }
    }
}
