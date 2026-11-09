using System;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Security
{
    public partial class FlaggedZones : Page
    {
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.TextBox txtName;
        protected global::System.Web.UI.WebControls.DropDownList ddlRiskLevel;
        protected global::System.Web.UI.WebControls.TextBox txtRadius;
        protected global::System.Web.UI.WebControls.TextBox txtReason;
        protected global::System.Web.UI.WebControls.HiddenField hfZoneLat;
        protected global::System.Web.UI.WebControls.HiddenField hfZoneLng;
        protected global::System.Web.UI.WebControls.Button btnSaveZone;
        protected global::System.Web.UI.WebControls.GridView gvZones;

        // cheks if guard or supervisor is logged in...
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "guard", "supervisor");

            if (!IsPostBack)
            {
                LoadZones();
            }
        }

        // gets the list of active danger zones from db,,,
        private void LoadZones()
        {
            const string sql = @"
                SELECT zone_id, name, risk_level, radius_m, reason, latitude, longitude
                FROM flagged_zones
                WHERE is_active = true
                ORDER BY created_at DESC;";

            var dt = Db.ExecuteDataTable(sql);
            gvZones.DataSource = dt;
            gvZones.DataBind();
        }

        // saves new flagged zone into the database...
        protected void btnSaveZone_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            string name = txtName.Text.Trim();
            string reason = txtReason.Text.Trim();
            short riskLevel = short.Parse(ddlRiskLevel.SelectedValue);
            double radius = 50.0;
            double.TryParse(txtRadius.Text.Trim(), out radius);

            if (!Geo.TryParseCoordinate(hfZoneLat.Value, out double lat) || !Geo.TryParseCoordinate(hfZoneLng.Value, out double lng))
            {
                pnlError.Visible = true;
                litError.Text = "Please click on the map to set the zone center coordinates.";
                return;
            }

            try
            {
                Guid zoneId = Guid.NewGuid();
                Guid me = Guid.Parse(Auth.CurrentUserId);

                const string sql = @"
                    INSERT INTO flagged_zones (zone_id, created_by, name, reason, risk_level, latitude, longitude, radius_m, is_active)
                    VALUES (@id, @me, @name, @reason, @risk, @lat, @lng, @rad, true);";

                Db.ExecuteNonQuery(sql,
                    Db.Param("@id", zoneId),
                    Db.Param("@me", me),
                    Db.Param("@name", name),
                    Db.Param("@reason", string.IsNullOrEmpty(reason) ? (object)DBNull.Value : reason),
                    Db.Param("@risk", riskLevel),
                    Db.Param("@lat", lat),
                    Db.Param("@lng", lng),
                    Db.Param("@rad", radius));

                pnlSuccess.Visible = true;
                litSuccess.Text = $"Flagged zone '{Server.HtmlEncode(name)}' created successfully!";
                txtName.Text = "";
                txtReason.Text = "";
                hfZoneLat.Value = "";
                hfZoneLng.Value = "";
                LoadZones();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to create flagged zone: " + Server.HtmlEncode(ex.Message);
            }
        }

        // deactivates a flagged zone so it does not show,,
        protected void gvZones_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Deactivate")
            {
                if (Guid.TryParse(e.CommandArgument.ToString(), out Guid zoneId))
                {
                    const string sql = "UPDATE flagged_zones SET is_active = false WHERE zone_id = @id;";
                    Db.ExecuteNonQuery(sql, Db.Param("@id", zoneId));
                    LoadZones();
                }
            }
        }
    }
}
