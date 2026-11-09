using System;
using System.Collections.Generic;
using System.Data;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin
{
    public partial class SafeRoute : Page
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        protected global::System.Web.UI.WebControls.DropDownList ddlFromLocation;
        protected global::System.Web.UI.WebControls.DropDownList ddlToLocation;
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.Panel pnlConditionNotice;
        protected global::System.Web.UI.WebControls.Literal litConditionNotice;
        protected global::System.Web.UI.WebControls.Panel pnlSaveRoute;
        protected global::System.Web.UI.WebControls.TextBox txtRouteLabel;
        protected global::System.Web.UI.WebControls.HiddenField hfNodePathJson;
        protected global::System.Web.UI.WebControls.HiddenField hfFromLocId;
        protected global::System.Web.UI.WebControls.HiddenField hfToLocId;
        protected global::System.Web.UI.WebControls.HiddenField hfRouteLength;
        protected global::System.Web.UI.WebControls.Button btnSaveRoute;
        protected global::System.Web.UI.WebControls.GridView gvSavedRoutes;

        public string InitialRouteJson { get; set; } = "null";

        // makes sure user is loged in before finding safe routes,,,
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this);
            RouteService.EnsureCampusNetwork();

            if (!IsPostBack)
            {
                LoadLocationsDropdowns();
                LoadSavedRoutes();
            }
        }

        // fills dropdown boxes with campus places from db...
        private void LoadLocationsDropdowns()
        {
            const string sql = @"
                SELECT location_id, name, kind, latitude, longitude
                FROM campus_locations
                ORDER BY name ASC;";

            var dt = Db.ExecuteDataTable(sql);
            ddlFromLocation.Items.Clear();
            ddlToLocation.Items.Clear();

            ddlFromLocation.Items.Add(new ListItem("-- Select Departure Location --", ""));
            ddlToLocation.Items.Add(new ListItem("-- Select Destination Location --", ""));

            foreach (DataRow r in dt.Rows)
            {
                string id = r["location_id"].ToString();
                string name = r["name"].ToString();
                string lat = Convert.ToDouble(r["latitude"]).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string lng = Convert.ToDouble(r["longitude"]).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string val = $"{id}|{lat}|{lng}";

                ddlFromLocation.Items.Add(new ListItem(name, val));
                ddlToLocation.Items.Add(new ListItem(name, val));
            }
        }

        private void LoadSavedRoutes()
        {
            Guid me = Guid.Parse(Auth.CurrentUserId);
            const string sql = @"
                SELECT sr.route_id, sr.label, sr.length_metres,
                       c1.name AS from_name, c2.name AS to_name,
                       to_char(sr.created_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD') AS created_fmt
                FROM saved_routes sr
                LEFT JOIN campus_locations c1 ON c1.location_id = sr.from_location_id
                LEFT JOIN campus_locations c2 ON c2.location_id = sr.to_location_id
                WHERE sr.user_id = @me
                ORDER BY sr.created_at DESC;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@me", me));
            gvSavedRoutes.DataSource = dt;
            gvSavedRoutes.DataBind();
        }

        protected void btnSaveRoute_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            string label = txtRouteLabel.Text.Trim();
            string nodePathJson = hfNodePathJson.Value.Trim();
            string fromIdStr = hfFromLocId.Value.Trim();
            string toIdStr = hfToLocId.Value.Trim();
            string lengthStr = hfRouteLength.Value.Trim();

            if (string.IsNullOrEmpty(label))
            {
                pnlError.Visible = true;
                litError.Text = "Please enter a descriptive label for this route.";
                return;
            }

            if (string.IsNullOrEmpty(nodePathJson) || nodePathJson == "[]")
            {
                pnlError.Visible = true;
                litError.Text = "No calculated route path to save. Please calculate a route first.";
                return;
            }

            Guid me = Guid.Parse(Auth.CurrentUserId);
            Guid? fromId = Guid.TryParse(fromIdStr, out Guid f) ? (Guid?)f : null;
            Guid? toId = Guid.TryParse(toIdStr, out Guid t) ? (Guid?)t : null;
            double? length = double.TryParse(lengthStr, out double l) ? (double?)l : null;

            try
            {
                const string sql = @"
                    INSERT INTO saved_routes (user_id, label, from_location_id, to_location_id, node_path, length_metres)
                    VALUES (@me, @label, @fromId, @toId, @nodePath::jsonb, @length);";

                Db.ExecuteNonQuery(sql,
                    Db.Param("@me", me),
                    Db.Param("@label", label),
                    Db.Param("@fromId", (object)fromId ?? DBNull.Value),
                    Db.Param("@toId", (object)toId ?? DBNull.Value),
                    Db.Param("@nodePath", nodePathJson),
                    Db.Param("@length", (object)length ?? DBNull.Value));

                pnlSuccess.Visible = true;
                litSuccess.Text = $"Route \"{Server.HtmlEncode(label)}\" saved successfully!";
                txtRouteLabel.Text = "";
                LoadSavedRoutes();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to save route: " + Server.HtmlEncode(ex.Message);
            }
        }

        protected void gvSavedRoutes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Reevaluate")
            {
                Guid routeId = Guid.Parse(e.CommandArgument.ToString());
                ReevaluateSavedRoute(routeId);
            }
        }

        protected void gvSavedRoutes_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            Guid routeId = (Guid)gvSavedRoutes.DataKeys[e.RowIndex].Value;
            Guid me = Guid.Parse(Auth.CurrentUserId);

            try
            {
                const string sql = "DELETE FROM saved_routes WHERE route_id = @id AND user_id = @me;";
                Db.ExecuteNonQuery(sql, Db.Param("@id", routeId), Db.Param("@me", me));
                pnlSuccess.Visible = true;
                litSuccess.Text = "Saved route deleted.";
                LoadSavedRoutes();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to delete route: " + Server.HtmlEncode(ex.Message);
            }
        }

        private void ReevaluateSavedRoute(Guid routeId)
        {
            pnlConditionNotice.Visible = false;
            pnlError.Visible = false;
            pnlSuccess.Visible = false;

            const string sql = @"
                SELECT sr.label, sr.node_path::text AS saved_path_json,
                       c1.latitude AS from_lat, c1.longitude AS from_lng,
                       c2.latitude AS to_lat, c2.longitude AS to_lng
                FROM saved_routes sr
                LEFT JOIN campus_locations c1 ON c1.location_id = sr.from_location_id
                LEFT JOIN campus_locations c2 ON c2.location_id = sr.to_location_id
                WHERE sr.route_id = @id;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@id", routeId));
            if (dt.Rows.Count == 0)
            {
                pnlError.Visible = true;
                litError.Text = "Saved route not found.";
                return;
            }

            var r = dt.Rows[0];
            string label = r["label"].ToString();
            string savedPathJson = r["saved_path_json"] != DBNull.Value ? r["saved_path_json"].ToString() : "[]";

            if (r["from_lat"] == DBNull.Value || r["to_lat"] == DBNull.Value)
            {
                pnlError.Visible = true;
                litError.Text = "Saved route endpoints are no longer available in campus locations.";
                return;
            }

            double fromLat = Convert.ToDouble(r["from_lat"]);
            double fromLng = Convert.ToDouble(r["from_lng"]);
            double toLat = Convert.ToDouble(r["to_lat"]);
            double toLng = Convert.ToDouble(r["to_lng"]);

            var routeResult = RouteService.CalculateRoutes(fromLat, fromLng, toLat, toLng);
            if (!routeResult.Safer.Found)
            {
                pnlError.Visible = true;
                litError.Text = "Unable to compute route between endpoints on current network.";
                return;
            }

            // compares saved path with new recalculated path,,,
            List<string> savedNodes;
            try
            {
                savedNodes = Serializer.Deserialize<List<string>>(savedPathJson) ?? new List<string>();
            }
            catch
            {
                savedNodes = new List<string>();
            }

            bool pathMatches = (savedNodes.Count == routeResult.Safer.NodeIds.Count);
            if (pathMatches)
            {
                for (int i = 0; i < savedNodes.Count; i++)
                {
                    if (!string.Equals(savedNodes[i], routeResult.Safer.NodeIds[i], StringComparison.OrdinalIgnoreCase))
                    {
                        pathMatches = false;
                        break;
                    }
                }
            }

            pnlConditionNotice.Visible = true;
            if (pathMatches)
            {
                pnlConditionNotice.CssClass = "alert alert-success";
                litConditionNotice.Text = $"<strong>Route Verified for \"{Server.HtmlEncode(label)}\":</strong> Conditions normal! The recommended safer path is identical to your saved route ({routeResult.Safer.DistanceMetres:F0} m).";
            }
            else
            {
                pnlConditionNotice.CssClass = "alert alert-warning";
                litConditionNotice.Text = $"<strong>Conditions Changed for \"{Server.HtmlEncode(label)}\":</strong> Safety conditions or flagged zones have changed. The route has been dynamically recalculated to guide you along the safest alternative path.";
            }

            // converts route to json so the satelite map can draw it...
            InitialRouteJson = Serializer.Serialize(new
            {
                startLat = fromLat,
                startLng = fromLng,
                endLat = toLat,
                endLng = toLng,
                result = routeResult
            });
        }
    }
}
