using System;
using System.Data;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Admin
{
    public partial class Hotspots : Page
    {
        protected global::System.Web.UI.WebControls.Literal litActiveHotspotsCount;
        protected global::System.Web.UI.WebControls.Literal litTopAreaName;
        protected global::System.Web.UI.WebControls.GridView gvTopAreas;
        protected global::System.Web.UI.WebControls.Panel pnlNoHotspots;

        // patrol intelligence is for security staff: guards and supervisors,,,
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "guard", "supervisor");

            if (!IsPostBack)
            {
                LoadTopAreas();
            }
        }

        // gets top ten hot spots with the most incidents...
        private void LoadTopAreas()
        {
            const string sql = @"
                SELECT h.incident_count,
                       array_to_string(h.categories, ', ') AS categories_str,
                       h.cell_lat,
                       h.cell_lng,
                       COALESCE(
                         (SELECT cl.name 
                          FROM campus_locations cl 
                          ORDER BY cl.geom <-> ST_SetSRID(ST_MakePoint(h.cell_lng, h.cell_lat), 4326)::geography 
                          LIMIT 1),
                         'Campus Sector (' || round(h.cell_lat::numeric, 4) || ', ' || round(h.cell_lng::numeric, 4) || ')'
                       ) AS nearest_location
                FROM hotspot_cells h
                WHERE h.incident_count >= 2
                ORDER BY h.incident_count DESC
                LIMIT 10;";

            var dt = Db.ExecuteDataTable(sql);

            gvTopAreas.DataSource = dt;
            gvTopAreas.DataBind();

            litActiveHotspotsCount.Text = dt.Rows.Count.ToString();
            if (dt.Rows.Count > 0)
            {
                litTopAreaName.Text = Server.HtmlEncode(dt.Rows[0]["nearest_location"].ToString());
            }

            pnlNoHotspots.Visible = (dt.Rows.Count == 0);
            gvTopAreas.Visible = (dt.Rows.Count > 0);
        }
    }
}
