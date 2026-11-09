using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Security
{
    public partial class MyDispatches : Page
    {
        protected global::System.Web.UI.WebControls.Panel pnlNotice;
        protected global::System.Web.UI.WebControls.Literal litNotice;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.Repeater rptDispatches;
        protected global::System.Web.UI.WebControls.Panel pnlNoDispatches;

        public string DispatchesJson { get; set; } = "[]";

        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "guard", "supervisor");

            if (!IsPostBack)
            {
                LoadDispatches();
            }
        }

        private void LoadDispatches()
        {
            Guid me = Guid.Parse(Auth.CurrentUserId);

            const string sql = @"
                SELECT d.dispatch_id,
                       d.incident_id,
                       d.status::text AS dispatch_status,
                       to_char(d.dispatched_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS dispatched_fmt,
                       i.category::text AS category,
                       i.status::text AS incident_status,
                       i.is_emergency,
                       i.description,
                       i.latitude,
                       i.longitude,
                       'CS-' || upper(substring(i.incident_id::text from 1 for 8)) AS ref_number
                FROM dispatches d
                JOIN incidents i ON i.incident_id = d.incident_id
                WHERE d.guard_id = @me
                  AND d.status <> 'cancelled'
                  AND d.status <> 'resolved'
                ORDER BY i.is_emergency DESC, d.dispatched_at DESC;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@me", me));
            rptDispatches.DataSource = dt;
            rptDispatches.DataBind();

            var list = new System.Collections.Generic.List<object>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    dispatch_id = row["dispatch_id"].ToString(),
                    incident_id = row["incident_id"].ToString(),
                    ref_number = row["ref_number"].ToString(),
                    category = row["category"].ToString(),
                    incident_status = row["incident_status"].ToString(),
                    dispatch_status = row["dispatch_status"].ToString(),
                    is_emergency = Convert.ToBoolean(row["is_emergency"]),
                    latitude = Convert.ToDouble(row["latitude"]),
                    longitude = Convert.ToDouble(row["longitude"]),
                    description = row["description"] != DBNull.Value ? row["description"].ToString() : "",
                    dispatched_fmt = row["dispatched_fmt"].ToString()
                });
            }
            DispatchesJson = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(list);

            pnlNoDispatches.Visible = (dt.Rows.Count == 0);
            rptDispatches.Visible = (dt.Rows.Count > 0);
        }

        protected void rptDispatches_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            pnlNotice.Visible = false;
            pnlError.Visible = false;

            if (!Guid.TryParse(e.CommandArgument.ToString(), out Guid dispatchId))
                return;

            string action = e.CommandName;
            Guid me = Guid.Parse(Auth.CurrentUserId);

            // gets dispatch and incident rows from db,,,
            const string fetchSql = @"
                SELECT d.incident_id, d.status::text AS dispatch_status, i.status::text AS incident_status,
                       i.latitude, i.longitude
                FROM dispatches d
                JOIN incidents i ON i.incident_id = d.incident_id
                WHERE d.dispatch_id = @id AND d.guard_id = @me;";

            var dt = Db.ExecuteDataTable(fetchSql, Db.Param("@id", dispatchId), Db.Param("@me", me));
            if (dt.Rows.Count == 0)
            {
                pnlError.Visible = true;
                litError.Text = "Dispatch not found or already closed.";
                LoadDispatches();
                return;
            }

            var r = dt.Rows[0];
            Guid incidentId = (Guid)r["incident_id"];
            string incStatus = r["incident_status"].ToString();

            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    // sets curent user in postgres for audit trigger...
                    using (var cmd = new NpgsqlCommand("SELECT set_config('app.user_id', @me, true);", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@me", me.ToString());
                        cmd.ExecuteNonQuery();
                    }

                    switch (action)
                    {
                        case "EnRoute":
                            // updates dispatch status to en route,,
                            using (var cmd = new NpgsqlCommand(@"
                                UPDATE dispatches 
                                SET status = 'en_route'::dispatch_status, acknowledged_at = NOW()
                                WHERE dispatch_id = @id;", conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@id", dispatchId);
                                cmd.ExecuteNonQuery();
                            }

                            // moves incident along to in progress...
                            if (incStatus == "New")
                            {
                                using (var cmd = new NpgsqlCommand("UPDATE incidents SET status = 'Acknowledged'::incident_status WHERE incident_id = @id;", conn, tran))
                                {
                                    cmd.Parameters.AddWithValue("@id", incidentId);
                                    cmd.ExecuteNonQuery();
                                }
                                using (var cmd = new NpgsqlCommand("UPDATE incidents SET status = 'In Progress'::incident_status WHERE incident_id = @id;", conn, tran))
                                {
                                    cmd.Parameters.AddWithValue("@id", incidentId);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else if (incStatus == "Acknowledged")
                            {
                                using (var cmd = new NpgsqlCommand("UPDATE incidents SET status = 'In Progress'::incident_status WHERE incident_id = @id;", conn, tran))
                                {
                                    cmd.Parameters.AddWithValue("@id", incidentId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tran.Commit();
                            pnlNotice.Visible = true;
                            litNotice.Text = "Status updated: En Route to incident.";
                            break;

                        case "OnScene":
                            using (var cmd = new NpgsqlCommand(@"
                                UPDATE dispatches 
                                SET status = 'on_scene'::dispatch_status, arrived_at = NOW()
                                WHERE dispatch_id = @id;", conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@id", dispatchId);
                                cmd.ExecuteNonQuery();
                            }
                            tran.Commit();
                            pnlNotice.Visible = true;
                            litNotice.Text = "Status updated: Arrived On Scene.";
                            break;

                        case "Resolve":
                            using (var cmd = new NpgsqlCommand(@"
                                UPDATE dispatches 
                                SET status = 'resolved'::dispatch_status, closed_at = NOW()
                                WHERE dispatch_id = @id;", conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@id", dispatchId);
                                cmd.ExecuteNonQuery();
                            }

                            // marks the incident resolved stepwise (db trigger only allows single steps),,,
                            var flow = new[] { "New", "Acknowledged", "In Progress", "Resolved" };
                            int pos = Array.IndexOf(flow, incStatus);
                            while (pos >= 0 && pos < flow.Length - 1)
                            {
                                pos++;
                                using (var cmd = new NpgsqlCommand("UPDATE incidents SET status = @next::incident_status WHERE incident_id = @id;", conn, tran))
                                {
                                    cmd.Parameters.AddWithValue("@next", flow[pos]);
                                    cmd.Parameters.AddWithValue("@id", incidentId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            tran.Commit();
                            pnlNotice.Visible = true;
                            litNotice.Text = "Incident marked as Resolved!";
                            break;

                        case "Decline":
                            using (var cmd = new NpgsqlCommand(@"
                                UPDATE dispatches 
                                SET status = 'cancelled'::dispatch_status, notes = 'Declined by officer'
                                WHERE dispatch_id = @id;", conn, tran))
                            {
                                cmd.Parameters.AddWithValue("@id", dispatchId);
                                cmd.ExecuteNonQuery();
                            }
                            tran.Commit();

                            // tries to dispatch the next available guard...
                            double lat = Convert.ToDouble(r["latitude"]);
                            double lng = Convert.ToDouble(r["longitude"]);
                            ReDispatchIncident(incidentId, lat, lng, me);

                            pnlNotice.Visible = true;
                            litNotice.Text = "Dispatch declined. Task re-routed to next available officer.";
                            break;
                    }

                    LoadDispatches();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    pnlError.Visible = true;
                    litError.Text = "Error updating dispatch: " + Server.HtmlEncode(ex.Message);
                }
            }
        }

        private static void ReDispatchIncident(Guid incidentId, double lat, double lng, Guid declinedGuardId)
        {
            try
            {
                const string sql = @"
                    SELECT u.user_id
                    FROM user_last_location l
                    JOIN users u ON u.user_id = l.user_id
                    WHERE u.role IN ('guard','supervisor')
                      AND l.is_on_duty = true
                      AND u.user_id <> @declined
                    ORDER BY l.geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography
                    LIMIT 1;";

                var nextIdObj = Db.ExecuteScalar(sql,
                    Db.Param("@declined", declinedGuardId),
                    Db.Param("@lat", lat),
                    Db.Param("@lng", lng));

                if (nextIdObj != null && nextIdObj != DBNull.Value)
                {
                    Guid nextGuardId = (Guid)nextIdObj;
                    Guid newDispId = Guid.NewGuid();

                    Db.ExecuteNonQuery(@"
                        INSERT INTO dispatches (dispatch_id, incident_id, guard_id, dispatched_by, status, notes)
                        VALUES (@dispId, @incId, @guardId, NULL, 'assigned'::dispatch_status, 'Re-routed after officer declined');",
                        Db.Param("@dispId", newDispId),
                        Db.Param("@incId", incidentId),
                        Db.Param("@guardId", nextGuardId));

                    Db.ExecuteNonQuery(@"
                        INSERT INTO notifications (recipient_id, kind, title, body, incident_id, dispatch_id)
                        VALUES (@to, 'dispatch'::notification_kind, 'URGENT DISPATCH RE-ASSIGNMENT', 'Incident dispatch reassigned to you.', @incId, @dispId);",
                        Db.Param("@to", nextGuardId),
                        Db.Param("@incId", incidentId),
                        Db.Param("@dispId", newDispId));
                }
            }
            catch { }
        }
    }
}
