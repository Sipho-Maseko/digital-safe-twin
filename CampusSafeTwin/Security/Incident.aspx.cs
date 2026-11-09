using System;
using System.Data;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Security
{
    public partial class Incident : Page
    {
        protected global::System.Web.UI.WebControls.Literal litRefNumber;
        protected global::System.Web.UI.WebControls.Literal litId;
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.Panel pnlInvalidBanner;
        protected global::System.Web.UI.HtmlControls.HtmlGenericControl badgeCurrentStatus;
        protected global::System.Web.UI.WebControls.Literal litStatus;
        protected global::System.Web.UI.WebControls.Literal litCategory;
        protected global::System.Web.UI.WebControls.Literal litOccurred;
        protected global::System.Web.UI.WebControls.Literal litReported;
        protected global::System.Web.UI.WebControls.Literal litReporter;
        protected global::System.Web.UI.WebControls.Literal litCoords;
        protected global::System.Web.UI.WebControls.Literal litDescription;
        protected global::System.Web.UI.WebControls.Panel pnlActionBox;
        protected global::System.Web.UI.WebControls.Literal litNextStatusName;
        protected global::System.Web.UI.WebControls.TextBox txtStatusNote;
        protected global::System.Web.UI.WebControls.Button btnAdvanceStatus;
        protected global::System.Web.UI.WebControls.Panel pnlResolvedNotice;
        protected global::System.Web.UI.WebControls.Panel pnlSupervisorControls;
        protected global::System.Web.UI.WebControls.Button btnMarkInvalid;
        protected global::System.Web.UI.WebControls.Literal litDispatchInfo;
        protected global::System.Web.UI.WebControls.DropDownList ddlGuards;
        protected global::System.Web.UI.WebControls.Button btnAssignGuard;
        protected global::System.Web.UI.WebControls.Button btnAutoNearest;
        protected global::System.Web.UI.WebControls.Repeater rptAttachments;
        protected global::System.Web.UI.WebControls.Panel pnlNoPhotos;
        protected global::System.Web.UI.WebControls.Repeater rptNotes;
        protected global::System.Web.UI.WebControls.TextBox txtNewNote;
        protected global::System.Web.UI.WebControls.Button btnAddNote;
        protected global::System.Web.UI.WebControls.Repeater rptTimeline;

        public string IncidentLat { get; set; } = "-26.6890";
        public string IncidentLng { get; set; } = "27.0935";
        public string CategoryName { get; set; } = "";

        private Guid IncidentId
        {
            get
            {
                if (Guid.TryParse(Request.QueryString["id"], out Guid id))
                    return id;
                return Guid.Empty;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "guard", "supervisor");

            if (IncidentId == Guid.Empty)
            {
                Response.Redirect("~/Security/Map.aspx", true);
                return;
            }

            pnlSupervisorControls.Visible = (string.Equals(Auth.CurrentRole, "supervisor", StringComparison.OrdinalIgnoreCase));

            if (!IsPostBack)
            {
                LoadIncident();
            }
        }

        private void LoadIncident()
        {
            const string sql = @"
                SELECT i.incident_id,
                       'CS-' || upper(substring(i.incident_id::text from 1 for 8)) AS ref_number,
                       i.category::text AS category,
                       i.status::text AS status,
                       i.description,
                       i.latitude,
                       i.longitude,
                       i.invalid_flag,
                       to_char(i.occurred_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS occurred_fmt,
                       to_char(i.reported_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS reported_fmt,
                       COALESCE(u.full_name, 'Anonymous') AS reporter_name
                FROM incidents i
                LEFT JOIN users u ON u.user_id = i.reporter_id
                WHERE i.incident_id = @id;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@id", IncidentId));
            if (dt.Rows.Count == 0)
            {
                Response.Redirect("~/Security/Map.aspx", true);
                return;
            }

            var r = dt.Rows[0];
            string status = r["status"].ToString();
            string category = r["category"].ToString();
            bool isInvalid = Convert.ToBoolean(r["invalid_flag"]);

            double lat = Convert.ToDouble(r["latitude"]);
            double lng = Convert.ToDouble(r["longitude"]);

            IncidentLat = lat.ToString("F6", CultureInfo.InvariantCulture);
            IncidentLng = lng.ToString("F6", CultureInfo.InvariantCulture);
            CategoryName = category;

            litRefNumber.Text = Server.HtmlEncode(r["ref_number"].ToString());
            litId.Text = Server.HtmlEncode(IncidentId.ToString());
            litStatus.Text = Server.HtmlEncode(status);
            badgeCurrentStatus.Attributes["class"] = "status-badge status-" + status.ToLowerInvariant().Replace(" ", "");

            litCategory.Text = Server.HtmlEncode(category);
            litOccurred.Text = Server.HtmlEncode(r["occurred_fmt"] != DBNull.Value ? r["occurred_fmt"].ToString() : "N/A");
            litReported.Text = Server.HtmlEncode(r["reported_fmt"].ToString());
            litReporter.Text = Server.HtmlEncode(r["reporter_name"].ToString());
            litCoords.Text = $"{IncidentLat}, {IncidentLng}";
            litDescription.Text = Server.HtmlEncode(r["description"] != DBNull.Value ? r["description"].ToString() : "None");

            pnlInvalidBanner.Visible = isInvalid;
            if (isInvalid)
            {
                pnlSupervisorControls.Visible = false;
            }

            // figure out what status comes next,,,
            string nextStatus = GetNextStatus(status);
            if (!string.IsNullOrEmpty(nextStatus) && !isInvalid)
            {
                pnlActionBox.Visible = true;
                pnlResolvedNotice.Visible = false;
                litNextStatusName.Text = Server.HtmlEncode(nextStatus);
                btnAdvanceStatus.Text = GetButtonTextForNextStatus(nextStatus);
            }
            else
            {
                pnlActionBox.Visible = false;
                pnlResolvedNotice.Visible = (status == "Resolved");
            }

            LoadDispatchInfo();
            LoadGuardsDropdown(lat, lng);
            LoadTimeline();
            LoadAttachments();
            LoadNotes();
        }

        private void LoadDispatchInfo()
        {
            const string sql = @"
                SELECT d.dispatch_id, d.status::text AS dispatch_status,
                       to_char(d.dispatched_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS dispatched_fmt,
                       COALESCE(u.full_name, 'Unassigned') AS guard_name
                FROM dispatches d
                LEFT JOIN users u ON u.user_id = d.guard_id
                WHERE d.incident_id = @id AND d.status <> 'cancelled'
                ORDER BY d.dispatched_at DESC
                LIMIT 1;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@id", IncidentId));
            if (dt.Rows.Count > 0)
            {
                var r = dt.Rows[0];
                litDispatchInfo.Text = $"{Server.HtmlEncode(r["guard_name"].ToString())} ({r["dispatch_status"]}, {r["dispatched_fmt"]})";
            }
            else
            {
                litDispatchInfo.Text = "Not dispatched";
            }
        }

        private void LoadGuardsDropdown(double lat, double lng)
        {
            if (!string.Equals(Auth.CurrentRole, "supervisor", StringComparison.OrdinalIgnoreCase))
                return;

            const string sql = @"
                SELECT u.user_id, u.full_name,
                       ROUND(ST_Distance(l.geom, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography)::numeric) AS distance_m
                FROM user_last_location l
                JOIN users u ON u.user_id = l.user_id
                WHERE u.role IN ('guard','supervisor')
                  AND l.is_on_duty = true
                ORDER BY l.geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@lat", lat), Db.Param("@lng", lng));
            ddlGuards.Items.Clear();

            if (dt.Rows.Count == 0)
            {
                ddlGuards.Items.Add(new ListItem("-- No On-Duty Guards Online --", ""));
                btnAssignGuard.Enabled = false;
                btnAutoNearest.Enabled = false;
            }
            else
            {
                btnAssignGuard.Enabled = true;
                btnAutoNearest.Enabled = true;
                foreach (DataRow r in dt.Rows)
                {
                    string id = r["user_id"].ToString();
                    string text = $"{r["full_name"]} ({r["distance_m"]} m away)";
                    ddlGuards.Items.Add(new ListItem(text, id));
                }
            }
        }

        protected void btnAssignGuard_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            // only supervisors may dispatch officers,,,
            if (!string.Equals(Auth.CurrentRole, "supervisor", StringComparison.OrdinalIgnoreCase))
            {
                pnlError.Visible = true;
                litError.Text = "Only supervisors can dispatch officers.";
                return;
            }

            string guardIdStr = ddlGuards.SelectedValue;
            if (!Guid.TryParse(guardIdStr, out Guid guardId))
            {
                pnlError.Visible = true;
                litError.Text = "Please select an officer to dispatch.";
                return;
            }

            try
            {
                Guid me = Guid.Parse(Auth.CurrentUserId);
                Guid dispId = Guid.NewGuid();

                // inserts dispatch record into db...
                const string sql = @"
                    INSERT INTO dispatches (dispatch_id, incident_id, guard_id, dispatched_by, status, notes)
                    VALUES (@dispId, @incId, @guardId, @me, 'assigned'::dispatch_status, 'Assigned by supervisor');";

                Db.ExecuteNonQuery(sql,
                    Db.Param("@dispId", dispId),
                    Db.Param("@incId", IncidentId),
                    Db.Param("@guardId", guardId),
                    Db.Param("@me", me));

                // notifies the officer of new dispatch,,
                const string notifySql = @"
                    INSERT INTO notifications (recipient_id, kind, title, body, incident_id, dispatch_id)
                    VALUES (@to, 'dispatch'::notification_kind, 'SUPERVISOR DISPATCH ASSIGNMENT', 
                            'You have been dispatched by supervisor to an incident.', @incId, @dispId);";

                Db.ExecuteNonQuery(notifySql,
                    Db.Param("@to", guardId),
                    Db.Param("@incId", IncidentId),
                    Db.Param("@dispId", dispId));

                pnlSuccess.Visible = true;
                litSuccess.Text = "Officer successfully dispatched!";
                LoadIncident();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Dispatch failed: " + Server.HtmlEncode(ex.Message);
            }
        }

        protected void btnAutoNearest_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            // only supervisors may dispatch officers,,,
            if (!string.Equals(Auth.CurrentRole, "supervisor", StringComparison.OrdinalIgnoreCase))
            {
                pnlError.Visible = true;
                litError.Text = "Only supervisors can dispatch officers.";
                return;
            }

            try
            {
                if (!Geo.TryParseCoordinate(IncidentLat, out double lat) || !Geo.TryParseCoordinate(IncidentLng, out double lng))
                {
                    pnlError.Visible = true;
                    litError.Text = "Invalid incident coordinates.";
                    return;
                }

                const string findSql = "SELECT guard_id, full_name, distance_m FROM nearest_on_duty_guard(@lat, @lng);";
                var dt = Db.ExecuteDataTable(findSql, Db.Param("@lat", lat), Db.Param("@lng", lng));

                if (dt.Rows.Count == 0)
                {
                    pnlError.Visible = true;
                    litError.Text = "No on-duty guards found in active location registry.";
                    return;
                }

                Guid guardId = (Guid)dt.Rows[0]["guard_id"];
                string guardName = dt.Rows[0]["full_name"].ToString();
                Guid me = Guid.Parse(Auth.CurrentUserId);
                Guid dispId = Guid.NewGuid();

                const string sql = @"
                    INSERT INTO dispatches (dispatch_id, incident_id, guard_id, dispatched_by, status, notes)
                    VALUES (@dispId, @incId, @guardId, @me, 'assigned'::dispatch_status, 'Auto-assigned by supervisor');";

                Db.ExecuteNonQuery(sql,
                    Db.Param("@dispId", dispId),
                    Db.Param("@incId", IncidentId),
                    Db.Param("@guardId", guardId),
                    Db.Param("@me", me));

                const string notifySql = @"
                    INSERT INTO notifications (recipient_id, kind, title, body, incident_id, dispatch_id)
                    VALUES (@to, 'dispatch'::notification_kind, 'SUPERVISOR DISPATCH ASSIGNMENT', 
                            'You have been auto-dispatched by supervisor to an incident.', @incId, @dispId);";

                Db.ExecuteNonQuery(notifySql,
                    Db.Param("@to", guardId),
                    Db.Param("@incId", IncidentId),
                    Db.Param("@dispId", dispId));

                pnlSuccess.Visible = true;
                litSuccess.Text = $"Officer {Server.HtmlEncode(guardName)} auto-dispatched!";
                LoadIncident();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Auto dispatch failed: " + Server.HtmlEncode(ex.Message);
            }
        }

        private string GetNextStatus(string current)
        {
            switch (current)
            {
                case "New": return "Acknowledged";
                case "Acknowledged": return "In Progress";
                case "In Progress": return "Resolved";
                default: return null;
            }
        }

        private string GetButtonTextForNextStatus(string nextStatus)
        {
            switch (nextStatus)
            {
                case "Acknowledged": return "Acknowledge Incident";
                case "In Progress": return "Mark In Progress";
                case "Resolved": return "Mark Resolved";
                default: return "Update Status";
            }
        }

        private void LoadTimeline()
        {
            const string sql = @"
                SELECT sh.old_status::text AS old_status,
                       sh.new_status::text AS new_status,
                       to_char(sh.changed_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS changed_fmt,
                       COALESCE(u.full_name, 'System') AS changed_by_name,
                       sh.note
                FROM status_history sh
                LEFT JOIN users u ON u.user_id = sh.changed_by
                WHERE sh.incident_id = @id
                ORDER BY sh.changed_at ASC;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@id", IncidentId));
            rptTimeline.DataSource = dt;
            rptTimeline.DataBind();
        }

        protected void btnAdvanceStatus_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            // grabs curent status from table...
            string currentStatus = Db.ExecuteScalar("SELECT status::text FROM incidents WHERE incident_id = @id;", Db.Param("@id", IncidentId))?.ToString();
            string nextStatus = GetNextStatus(currentStatus);

            if (string.IsNullOrEmpty(nextStatus))
            {
                pnlError.Visible = true;
                litError.Text = "This incident has already reached its final status or cannot be updated.";
                LoadIncident();
                return;
            }

            string note = txtStatusNote.Text.Trim();
            string me = Auth.CurrentUserId;

            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    // sets curent user in postgres sesion,,,
                    using (var cmd = new NpgsqlCommand("SELECT set_config('app.user_id', @me, true);", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@me", me);
                        cmd.ExecuteNonQuery();
                    }

                    // locks row so no one else edits at same time...
                    using (var cmd = new NpgsqlCommand("SELECT status FROM incidents WHERE incident_id = @id FOR UPDATE;", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", IncidentId);
                        cmd.ExecuteNonQuery();
                    }

                    // updates incident to next step,,
                    using (var cmd = new NpgsqlCommand("UPDATE incidents SET status = @next::incident_status WHERE incident_id = @id;", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@next", nextStatus);
                        cmd.Parameters.AddWithValue("@id", IncidentId);
                        cmd.ExecuteNonQuery();
                    }

                    // adds the note into status history...
                    using (var cmd = new NpgsqlCommand(@"
                        UPDATE status_history 
                        SET note = @note 
                        WHERE incident_id = @id 
                          AND new_status = @next::incident_status 
                          AND changed_at = now();", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@note", string.IsNullOrEmpty(note) ? (object)DBNull.Value : note);
                        cmd.Parameters.AddWithValue("@id", IncidentId);
                        cmd.Parameters.AddWithValue("@next", nextStatus);
                        cmd.ExecuteNonQuery();
                    }

                    tran.Commit();

                    pnlSuccess.Visible = true;
                    litSuccess.Text = $"Status updated to <strong>{Server.HtmlEncode(nextStatus)}</strong>.";
                    txtStatusNote.Text = "";
                    LoadIncident();
                }
                catch (PostgresException pex) when (pex.MessageText.Contains("Illegal status change") || pex.Message.Contains("Illegal status change"))
                {
                    tran.Rollback();
                    pnlError.Visible = true;
                    litError.Text = "Someone already updated this. Refresh.";
                    LoadIncident();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    pnlError.Visible = true;
                    litError.Text = "Error updating status: " + Server.HtmlEncode(ex.Message);
                    LoadIncident();
                }
            }
        }

        protected void btnMarkInvalid_Click(object sender, EventArgs e)
        {
            if (!string.Equals(Auth.CurrentRole, "supervisor", StringComparison.OrdinalIgnoreCase))
            {
                pnlError.Visible = true;
                litError.Text = "Only supervisors can mark reports as invalid.";
                return;
            }

            try
            {
                const string sql = "UPDATE incidents SET invalid_flag = true WHERE incident_id = @id;";
                Db.ExecuteNonQuery(sql, Db.Param("@id", IncidentId));

                pnlSuccess.Visible = true;
                litSuccess.Text = "Incident marked as invalid / bogus.";
                LoadIncident();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to mark invalid: " + Server.HtmlEncode(ex.Message);
            }
        }

        private void LoadAttachments()
        {
            const string sql = @"
                SELECT attachment_id, storage_key, note,
                       to_char(uploaded_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS uploaded_fmt
                FROM incident_attachments
                WHERE incident_id = @id AND kind = 'photo'::attachment_kind
                ORDER BY uploaded_at ASC;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@id", IncidentId));
            rptAttachments.DataSource = dt;
            rptAttachments.DataBind();
            pnlNoPhotos.Visible = (dt.Rows.Count == 0);
        }

        private void LoadNotes()
        {
            const string sql = @"
                SELECT attachment_id, note,
                       to_char(uploaded_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS uploaded_fmt
                FROM incident_attachments
                WHERE incident_id = @id AND kind = 'note'::attachment_kind
                ORDER BY uploaded_at ASC;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@id", IncidentId));
            rptNotes.DataSource = dt;
            rptNotes.DataBind();
        }

        protected void btnAddNote_Click(object sender, EventArgs e)
        {
            string note = txtNewNote.Text.Trim();
            if (string.IsNullOrEmpty(note))
                return;

            try
            {
                const string sql = @"
                    INSERT INTO incident_attachments (incident_id, kind, note)
                    VALUES (@id, 'note'::attachment_kind, @note);";

                Db.ExecuteNonQuery(sql, Db.Param("@id", IncidentId), Db.Param("@note", note));
                txtNewNote.Text = "";
                pnlSuccess.Visible = true;
                litSuccess.Text = "Note added successfully.";
                LoadNotes();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to add note: " + Server.HtmlEncode(ex.Message);
            }
        }
    }
}
