using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Admin
{
    public partial class Locations : Page
    {
        protected global::System.Web.UI.WebControls.Button btnSeedDefaults;
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.Literal litCount;
        protected global::System.Web.UI.WebControls.GridView gvLocations;
        protected global::System.Web.UI.WebControls.TextBox txtName;
        protected global::System.Web.UI.WebControls.DropDownList ddlKind;
        protected global::System.Web.UI.WebControls.TextBox txtDescription;
        protected global::System.Web.UI.WebControls.TextBox txtLat;
        protected global::System.Web.UI.WebControls.TextBox txtLng;
        protected global::System.Web.UI.WebControls.Button btnAddLocation;

        // checks supervisor role and loads locations...
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "supervisor");

            if (!IsPostBack)
            {
                LoadLocations();
            }
        }

        // fetches campus places from db and binds to grid,,,
        private void LoadLocations()
        {
            const string sql = @"
                SELECT location_id, name, kind, description, latitude, longitude
                FROM campus_locations
                ORDER BY name ASC;";

            var dt = Db.ExecuteDataTable(sql);
            gvLocations.DataSource = dt;
            gvLocations.DataBind();
            litCount.Text = dt.Rows.Count.ToString();
        }

        // saves a new place to the db...
        protected void btnAddLocation_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            string name = txtName.Text.Trim();
            string kind = ddlKind.SelectedValue;
            string desc = txtDescription.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                pnlError.Visible = true;
                litError.Text = "Please enter a location name.";
                return;
            }

            if (!Geo.TryParseCoordinate(txtLat.Text.Trim(), out double lat) ||
                !Geo.TryParseCoordinate(txtLng.Text.Trim(), out double lng))
            {
                pnlError.Visible = true;
                litError.Text = "Please enter valid numeric latitude and longitude.";
                return;
            }

            try
            {
                Guid locId = Guid.NewGuid();
                const string sql = @"
                    INSERT INTO campus_locations (location_id, name, kind, description, latitude, longitude)
                    VALUES (@id, @name, @kind, @desc, @lat, @lng);

                    -- Automatically associate closest unlinked path node within 60 metres
                    UPDATE path_nodes
                    SET location_id = @id
                    WHERE node_id = (
                        SELECT node_id
                        FROM path_nodes
                        WHERE location_id IS NULL
                          AND ST_DWithin(geom, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography, 60)
                        ORDER BY geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography
                        LIMIT 1
                    );";

                Db.ExecuteNonQuery(sql,
                    Db.Param("@id", locId),
                    Db.Param("@name", name),
                    Db.Param("@kind", kind),
                    Db.Param("@desc", string.IsNullOrEmpty(desc) ? (object)DBNull.Value : desc),
                    Db.Param("@lat", lat),
                    Db.Param("@lng", lng));

                pnlSuccess.Visible = true;
                litSuccess.Text = $"Location \"{Server.HtmlEncode(name)}\" added successfully.";
                txtName.Text = "";
                txtDescription.Text = "";
                txtLat.Text = "";
                txtLng.Text = "";
                LoadLocations();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to add location: " + Server.HtmlEncode(ex.Message);
            }
        }

        // deletes a landmark from the db,,,
        protected void gvLocations_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            Guid locId = (Guid)gvLocations.DataKeys[e.RowIndex].Value;

            try
            {
                // unlinks any paths conected before deleting,,,
                const string sqlUnlink = "UPDATE path_nodes SET location_id = NULL WHERE location_id = @id;";
                Db.ExecuteNonQuery(sqlUnlink, Db.Param("@id", locId));

                const string sqlDel = "DELETE FROM campus_locations WHERE location_id = @id;";
                Db.ExecuteNonQuery(sqlDel, Db.Param("@id", locId));

                pnlSuccess.Visible = true;
                litSuccess.Text = "Location deleted.";
                LoadLocations();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to delete location: " + Server.HtmlEncode(ex.Message);
            }
        }

        // seeds standard landmarks if empty...
        protected void btnSeedDefaults_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            var defaultLocations = new[]
            {
                ("Ferdinand Postma Library", "building", "Main academic campus library and study area", -26.6890, 27.0935),
                ("Student Centre / Food Court", "building", "Central student hub, dining and cafeteria", -26.6885, 27.0945),
                ("Wasmer Security Main Gate", "gate", "Main vehicular and pedestrian access gate", -26.6870, 27.0915),
                ("Heimat Building (F7)", "building", "Historical academic and lecture hall", -26.6905, 27.0920),
                ("Fanie du Toit Sports Grounds", "sports", "Athletics track, rugby fields and pool", -26.6920, 27.0960),
                ("Veritas / Heide Residences", "residence", "North campus student residential complex", -26.6875, 27.0970),
                ("Jasmyn / Laureus Residences", "residence", "South campus residential housing", -26.6910, 27.0940),
                ("West Gate Access Point", "gate", "Patrol station and turnstile entrance", -26.6895, 27.0900)
            };

            int inserted = 0;
            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    foreach (var loc in defaultLocations)
                    {
                        Guid locId = Guid.NewGuid();
                        const string sql = @"
                            INSERT INTO campus_locations (location_id, name, kind, description, latitude, longitude)
                            VALUES (@id, @name, @kind, @desc, @lat, @lng)
                            ON CONFLICT DO NOTHING;

                            UPDATE path_nodes
                            SET location_id = @id
                            WHERE node_id = (
                                SELECT node_id
                                FROM path_nodes
                                WHERE location_id IS NULL
                                  AND ST_DWithin(geom, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography, 100)
                                ORDER BY geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography
                                LIMIT 1
                            );";

                        using (var cmd = new NpgsqlCommand(sql, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@id", locId);
                            cmd.Parameters.AddWithValue("@name", loc.Item1);
                            cmd.Parameters.AddWithValue("@kind", loc.Item2);
                            cmd.Parameters.AddWithValue("@desc", loc.Item3);
                            cmd.Parameters.AddWithValue("@lat", loc.Item4);
                            cmd.Parameters.AddWithValue("@lng", loc.Item5);
                            cmd.ExecuteNonQuery();
                        }
                        inserted++;
                    }

                    tran.Commit();
                    pnlSuccess.Visible = true;
                    litSuccess.Text = $"Seeded standard campus POIs successfully.";
                    LoadLocations();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    pnlError.Visible = true;
                    litError.Text = "Failed to seed default locations: " + Server.HtmlEncode(ex.Message);
                }
            }
        }
    }
}
