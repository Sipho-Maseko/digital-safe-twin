using System;
using System.Data;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Handlers
{
    public class Sos : IHttpHandler, IRequiresSessionState
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            string userId = context.Session?["UserId"] as string;
            string role = (context.Session?["Role"] as string) ?? "";
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out Guid userGuid))
            {
                context.Response.StatusCode = 401;
                context.Response.Write(Serializer.Serialize(new { error = true, message = "Unauthorized. Please log in." }));
                return;
            }

            bool isStudent = string.Equals(role, "student", StringComparison.OrdinalIgnoreCase);
            bool isStaff = string.Equals(role, "guard", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(role, "supervisor", StringComparison.OrdinalIgnoreCase);

            string action = context.Request["action"] ?? "trigger";

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "trigger":
                        // panic button belongs to students (staff coordinate through dispatches),,,
                        if (!isStudent)
                        {
                            context.Response.StatusCode = 403;
                            context.Response.Write(Serializer.Serialize(new { error = true, message = "Only students can trigger the SOS panic button." }));
                            return;
                        }
                        HandleTrigger(context, userGuid);
                        break;
                    case "poll":
                        HandlePoll(context, userGuid, isStaff);
                        break;
                    case "cancel":
                        HandleCancel(context, userGuid);
                        break;
                    default:
                        context.Response.StatusCode = 400;
                        context.Response.Write(Serializer.Serialize(new { error = true, message = "Unknown action." }));
                        break;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.Write(Serializer.Serialize(new { error = true, message = ex.Message }));
            }
        }

        private static void HandleTrigger(HttpContext context, Guid userGuid)
        {
            string latStr = context.Request["lat"];
            string lngStr = context.Request["lng"];

            if (!Geo.TryParseCoordinate(latStr, out double lat) || !Geo.TryParseCoordinate(lngStr, out double lng))
            {
                context.Response.StatusCode = 400;
                context.Response.Write(Serializer.Serialize(new { error = true, message = "Valid GPS coordinates required." }));
                return;
            }

            // turns off inactive guards...
            const string housekeepingSql = @"
                UPDATE user_last_location 
                SET is_on_duty = false 
                WHERE is_on_duty = true 
                  AND updated_at < NOW() - INTERVAL '10 minutes';";
            Db.ExecuteNonQuery(housekeepingSql);

            Guid incidentId = Guid.NewGuid();
            string refCode = "CS-" + incidentId.ToString("N").Substring(0, 8).ToUpperInvariant();

            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    // creates the panic button alert in db,,,
                    const string insertIncSql = @"
                        INSERT INTO incidents (incident_id, reporter_id, category, description, is_emergency, status, latitude, longitude, occurred_at)
                        VALUES (@id, @me, 'medical'::incident_category, 'EMERGENCY SOS PANIC BUTTON TRIGGERED', true, 'New'::incident_status, @lat, @lng, NOW());";

                    using (var cmd = new NpgsqlCommand(insertIncSql, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incidentId);
                        cmd.Parameters.AddWithValue("@me", userGuid);
                        cmd.Parameters.AddWithValue("@lat", lat);
                        cmd.Parameters.AddWithValue("@lng", lng);
                        cmd.ExecuteNonQuery();
                    }

                    // records history entry for emergency...
                    const string insertHistSql = @"
                        INSERT INTO status_history (incident_id, changed_by, old_status, new_status, note)
                        VALUES (@id, @me, NULL, 'New'::incident_status, 'Emergency SOS Triggered');";

                    using (var cmd = new NpgsqlCommand(insertHistSql, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incidentId);
                        cmd.Parameters.AddWithValue("@me", userGuid);
                        cmd.ExecuteNonQuery();
                    }

                    // finds guard closest to student coords,,,
                    Guid? nearestGuardId = null;
                    string guardName = null;
                    const string findGuardSql = "SELECT guard_id, full_name, distance_m FROM nearest_on_duty_guard(@lat, @lng);";
                    using (var cmd = new NpgsqlCommand(findGuardSql, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@lat", lat);
                        cmd.Parameters.AddWithValue("@lng", lng);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                nearestGuardId = reader.GetGuid(0);
                                guardName = reader.GetString(1);
                            }
                        }
                    }

                    // creates new dispatch job...
                    Guid dispatchId = Guid.NewGuid();
                    if (nearestGuardId.HasValue)
                    {
                        const string insertDispSql = @"
                            INSERT INTO dispatches (dispatch_id, incident_id, guard_id, dispatched_by, status, notes)
                            VALUES (@dispId, @incId, @guardId, NULL, 'assigned'::dispatch_status, 'Auto-dispatched to nearest guard');";

                        using (var cmd = new NpgsqlCommand(insertDispSql, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@dispId", dispatchId);
                            cmd.Parameters.AddWithValue("@incId", incidentId);
                            cmd.Parameters.AddWithValue("@guardId", nearestGuardId.Value);
                            cmd.ExecuteNonQuery();
                        }

                        // alerts the closest officer,,,
                        const string notifyGuardSql = @"
                            INSERT INTO notifications (recipient_id, kind, title, body, incident_id, dispatch_id)
                            VALUES (@to, 'sos'::notification_kind, 'EMERGENCY SOS ALERT', 'Emergency panic button activated near your location. Proceed immediately.', @incId, @dispId);";

                        using (var cmd = new NpgsqlCommand(notifyGuardSql, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@to", nearestGuardId.Value);
                            cmd.Parameters.AddWithValue("@incId", incidentId);
                            cmd.Parameters.AddWithValue("@dispId", dispatchId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // saves as pending if no officer nearby...
                        const string insertPendingDispSql = @"
                            INSERT INTO dispatches (dispatch_id, incident_id, guard_id, dispatched_by, status, notes)
                            VALUES (@dispId, @incId, NULL, NULL, 'pending'::dispatch_status, 'No on-duty guards available in radius');";

                        using (var cmd = new NpgsqlCommand(insertPendingDispSql, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@dispId", dispatchId);
                            cmd.Parameters.AddWithValue("@incId", incidentId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // sends alert to campus supervisors,,,
                    const string notifySupSql = @"
                        INSERT INTO notifications (recipient_id, kind, title, body, incident_id, dispatch_id)
                        SELECT u.user_id, 'sos'::notification_kind, 'STUDENT SOS ALERT', 
                               'Student triggered emergency SOS. Ref ' || @ref, @incId, @dispId
                        FROM users u
                        WHERE u.role = 'supervisor'::user_role AND u.is_active = true;";

                    using (var cmd = new NpgsqlCommand(notifySupSql, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@ref", refCode);
                        cmd.Parameters.AddWithValue("@incId", incidentId);
                        cmd.Parameters.AddWithValue("@dispId", dispatchId);
                        cmd.ExecuteNonQuery();
                    }

                    tran.Commit();

                    context.Response.Write(Serializer.Serialize(new
                    {
                        success = true,
                        incident_id = incidentId.ToString(),
                        ref_number = refCode,
                        is_dispatched = nearestGuardId.HasValue
                    }));
                }
                catch (Exception)
                {
                    tran.Rollback();
                    throw;
                }
            }
        }

        private static void HandlePoll(HttpContext context, Guid userGuid, bool isStaff)
        {
            string incIdStr = context.Request["incident_id"] ?? context.Request["incidentId"];
            if (!Guid.TryParse(incIdStr, out Guid incId))
            {
                context.Response.StatusCode = 400;
                context.Response.Write(Serializer.Serialize(new { error = true, message = "Invalid incident ID." }));
                return;
            }

            // students may only watch their own alerts; staff can watch any,,,
            if (!isStaff)
            {
                long owns = Convert.ToInt64(Db.ExecuteScalar(
                    "SELECT COUNT(*) FROM incidents WHERE incident_id = @id AND reporter_id = @me;",
                    Db.Param("@id", incId), Db.Param("@me", userGuid)));
                if (owns == 0)
                {
                    context.Response.StatusCode = 403;
                    context.Response.Write(Serializer.Serialize(new { error = true, message = "You can only view the status of your own alerts." }));
                    return;
                }
            }

            // checks current status of incident...
            const string sqlInc = @"
                SELECT i.status::text AS incident_status,
                       i.is_emergency,
                       COALESCE(d.status::text, 'unassigned') AS dispatch_status,
                       d.dispatch_id,
                       d.guard_id,
                       d.dispatched_at,
                       i.latitude,
                       i.longitude
                FROM incidents i
                LEFT JOIN dispatches d ON d.incident_id = i.incident_id AND d.status <> 'cancelled'
                WHERE i.incident_id = @id
                ORDER BY d.dispatched_at DESC
                LIMIT 1;";

            var dt = Db.ExecuteDataTable(sqlInc, Db.Param("@id", incId));
            if (dt.Rows.Count == 0)
            {
                context.Response.Write(Serializer.Serialize(new { status = "not_found" }));
                return;
            }

            var r = dt.Rows[0];
            string incStatus = r["incident_status"].ToString();
            string dispStatus = r["dispatch_status"].ToString();

            // checks 90 second timeout if officer did not accept,,,
            if (dispStatus == "assigned" && r["dispatched_at"] != DBNull.Value)
            {
                DateTime dispTime = Convert.ToDateTime(r["dispatched_at"]);
                if ((DateTime.UtcNow - dispTime.ToUniversalTime()).TotalSeconds > 90)
                {
                    // dispatches to next closest guard...
                    ReDispatch(incId, Convert.ToDouble(r["latitude"]), Convert.ToDouble(r["longitude"]));
                    // refreshes dispatch status from db,,,
                    dt = Db.ExecuteDataTable(sqlInc, Db.Param("@id", incId));
                    if (dt.Rows.Count > 0)
                    {
                        dispStatus = dt.Rows[0]["dispatch_status"].ToString();
                    }
                }
            }

            // only shows friendly message to student for privacy...
            string friendlyStatus;
            switch (dispStatus)
            {
                case "assigned":
                    friendlyStatus = "Emergency security officer notified and assigned.";
                    break;
                case "en_route":
                    friendlyStatus = "Officer has accepted and is rushing to your location!";
                    break;
                case "on_scene":
                    friendlyStatus = "Officer is ON SCENE at your coordinates.";
                    break;
                case "resolved":
                    friendlyStatus = "Emergency incident resolved.";
                    break;
                default:
                    friendlyStatus = (incStatus == "Resolved")
                        ? "Incident resolved."
                        : "Emergency team actively coordinating your dispatch.";
                    break;
            }

            context.Response.Write(Serializer.Serialize(new
            {
                incident_status = incStatus,
                dispatch_status = dispStatus,
                friendly_message = friendlyStatus,
                is_resolved = (incStatus == "Resolved" || dispStatus == "resolved")
            }));
        }

        private static void ReDispatch(Guid incId, double lat, double lng)
        {
            try
            {
                using (var conn = Db.GetConnection())
                using (var tran = conn.BeginTransaction())
                {
                    // cancels timed out dispatches,,,
                    using (var cmd = new NpgsqlCommand(@"
                        UPDATE dispatches 
                        SET status = 'cancelled'::dispatch_status, notes = 'Timed out after 90 seconds without acceptance'
                        WHERE incident_id = @id AND status = 'assigned'::dispatch_status;", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incId);
                        cmd.ExecuteNonQuery();
                    }

                    // looks for next nearest officer who was not contacted...
                    Guid? nextGuardId = null;
                    const string findNextSql = @"
                        SELECT u.user_id
                        FROM user_last_location l
                        JOIN users u ON u.user_id = l.user_id
                        WHERE u.role IN ('guard','supervisor')
                          AND l.is_on_duty = true
                          AND u.user_id NOT IN (SELECT guard_id FROM dispatches WHERE incident_id = @id AND guard_id IS NOT NULL)
                        ORDER BY l.geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography
                        LIMIT 1;";

                    using (var cmd = new NpgsqlCommand(findNextSql, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incId);
                        cmd.Parameters.AddWithValue("@lat", lat);
                        cmd.Parameters.AddWithValue("@lng", lng);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value)
                        {
                            nextGuardId = (Guid)obj;
                        }
                    }

                    if (nextGuardId.HasValue)
                    {
                        Guid newDispId = Guid.NewGuid();
                        using (var cmd = new NpgsqlCommand(@"
                            INSERT INTO dispatches (dispatch_id, incident_id, guard_id, dispatched_by, status, notes)
                            VALUES (@dispId, @incId, @guardId, NULL, 'assigned'::dispatch_status, 'Re-dispatched after 90s timeout');", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@dispId", newDispId);
                            cmd.Parameters.AddWithValue("@incId", incId);
                            cmd.Parameters.AddWithValue("@guardId", nextGuardId.Value);
                            cmd.ExecuteNonQuery();
                        }

                        // alerts the next officer,,,
                        using (var cmd = new NpgsqlCommand(@"
                            INSERT INTO notifications (recipient_id, kind, title, body, incident_id, dispatch_id)
                            VALUES (@to, 'sos'::notification_kind, 'URGENT SOS ESCALATION', 'Urgent unaccepted SOS re-routed to you. Proceed immediately!', @incId, @dispId);", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@to", nextGuardId.Value);
                            cmd.Parameters.AddWithValue("@incId", incId);
                            cmd.Parameters.AddWithValue("@dispId", newDispId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tran.Commit();
                }
            }
            catch
            {
                // ignore error if dispatch failed...
            }
        }

        private static void HandleCancel(HttpContext context, Guid userGuid)
        {
            string incIdStr = context.Request["incident_id"] ?? context.Request["incidentId"];
            if (!Guid.TryParse(incIdStr, out Guid incId))
            {
                context.Response.StatusCode = 400;
                context.Response.Write(Serializer.Serialize(new { error = true, message = "Invalid incident ID." }));
                return;
            }

            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    // sets session user id in db,,,
                    using (var cmd = new NpgsqlCommand("SELECT set_config('app.user_id', @me, true);", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@me", userGuid.ToString());
                        cmd.ExecuteNonQuery();
                    }

                    // reads current status so we can step through the allowed flow...
                    string currStatus = null;
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT status::text FROM incidents
                        WHERE incident_id = @id AND reporter_id = @me FOR UPDATE;", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incId);
                        cmd.Parameters.AddWithValue("@me", userGuid);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null) currStatus = obj.ToString();
                    }

                    // only the student who raised the alert may cancel it,,,
                    if (currStatus == null)
                    {
                        tran.Rollback();
                        context.Response.StatusCode = 403;
                        context.Response.Write(Serializer.Serialize(new { error = true, message = "You can only cancel your own SOS alert." }));
                        return;
                    }

                    // walks status one step at a time until resolved (db trigger only allows single steps),,,
                    var flow = new[] { "New", "Acknowledged", "In Progress", "Resolved" };
                    int pos = Array.IndexOf(flow, currStatus);
                    while (pos >= 0 && pos < flow.Length - 1)
                    {
                        pos++;
                        using (var cmd = new NpgsqlCommand(@"
                            UPDATE incidents
                            SET status = @next::incident_status
                            WHERE incident_id = @id AND reporter_id = @me;", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@next", flow[pos]);
                            cmd.Parameters.AddWithValue("@id", incId);
                            cmd.Parameters.AddWithValue("@me", userGuid);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // records why it was closed...
                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO incident_attachments (incident_id, kind, note)
                        VALUES (@id, 'note'::attachment_kind, 'SOS cancelled by student (accidental trigger).');", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incId);
                        cmd.ExecuteNonQuery();
                    }

                    // cancels any open dispatches,,,
                    using (var cmd = new NpgsqlCommand(@"
                        UPDATE dispatches 
                        SET status = 'cancelled'::dispatch_status, closed_at = NOW(), notes = 'Cancelled by student'
                        WHERE incident_id = @id AND status <> 'resolved';", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incId);
                        cmd.ExecuteNonQuery();
                    }

                    tran.Commit();
                    context.Response.Write(Serializer.Serialize(new { success = true }));
                }
                catch (Exception)
                {
                    tran.Rollback();
                    throw;
                }
            }
        }
    }
}
