using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Handlers
{
    public class Api : IHttpHandler, IRequiresSessionState
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            string userId = context.Session?["UserId"] as string;
            string role = (context.Session?["Role"] as string) ?? "";

            if (string.IsNullOrEmpty(userId))
            {
                SendError(context, 401, "Unauthorized. Please log in.");
                return;
            }

            string action = context.Request.QueryString["action"] ?? context.Request.Form["action"] ?? "";

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "incidents":
                        RequireRole(role, "guard", "supervisor");
                        HandleIncidents(context);
                        break;

                    case "hotspots":
                        RequireRole(role, "guard", "supervisor");
                        HandleHotspots(context);
                        break;

                    case "network":
                        RequireRole(role, "supervisor");
                        HandleNetwork(context);
                        break;

                    case "startergrid":
                    case "rebuildnetwork":
                        RequireRole(role, "supervisor");
                        HandleRebuildNetwork(context);
                        break;

                    case "riskoverlay":
                        HandleRiskOverlay(context);
                        break;

                    case "addnode":
                        RequireRole(role, "supervisor");
                        HandleAddNode(context);
                        break;

                    case "deletenode":
                        RequireRole(role, "supervisor");
                        HandleDeleteNode(context);
                        break;

                    case "addlink":
                        RequireRole(role, "supervisor");
                        HandleAddLink(context);
                        break;

                    case "deletelink":
                        RequireRole(role, "supervisor");
                        HandleDeleteLink(context);
                        break;

                    case "togglelit":
                        RequireRole(role, "supervisor");
                        HandleToggleLit(context);
                        break;

                    case "route":
                        HandleRoute(context);
                        break;

                    case "flaggedzones":
                        // staff detail list; students get zones via riskoverlay instead,,,
                        RequireRole(role, "guard", "supervisor");
                        HandleFlaggedZones(context);
                        break;

                    case "liveops":
                        RequireRole(role, "supervisor");
                        HandleLiveOps(context);
                        break;

                    case "quickassign":
                        RequireRole(role, "supervisor");
                        HandleQuickAssign(context, userId);
                        break;

                    case "boundary":
                        HandleBoundary(context);
                        break;

                    default:
                        SendError(context, 400, "Unknown action: " + action);
                        break;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                SendError(context, 403, ex.Message);
            }
            catch (Exception ex)
            {
                SendError(context, 500, "Server error: " + ex.Message);
            }
        }

        private static void RequireRole(string userRole, params string[] allowedRoles)
        {
            foreach (var r in allowedRoles)
            {
                if (string.Equals(r, userRole, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            throw new UnauthorizedAccessException("Forbidden. Insufficient permissions for this action.");
        }

        private static void HandleIncidents(HttpContext context)
        {
            const string sql = @"
                SELECT incident_id, category::text AS category, status::text AS status,
                       description, latitude, longitude, is_emergency,
                       to_char(occurred_at at time zone 'Africa/Johannesburg','YYYY-MM-DD HH24:MI') as occurred_fmt,
                       to_char(reported_at at time zone 'Africa/Johannesburg','YYYY-MM-DD HH24:MI') as reported_fmt
                FROM incidents
                WHERE invalid_flag = false
                  AND reported_at >= NOW() - INTERVAL '30 days'
                ORDER BY reported_at DESC;";

            var dt = Db.ExecuteDataTable(sql);
            var list = new List<Dictionary<string, object>>();

            foreach (DataRow r in dt.Rows)
            {
                list.Add(new Dictionary<string, object>
                {
                    { "incident_id", r["incident_id"].ToString() },
                    { "category", r["category"].ToString() },
                    { "status", r["status"].ToString() },
                    { "description", r["description"] != DBNull.Value ? r["description"].ToString() : "" },
                    { "latitude", Convert.ToDouble(r["latitude"]) },
                    { "longitude", Convert.ToDouble(r["longitude"]) },
                    { "is_emergency", Convert.ToBoolean(r["is_emergency"]) },
                    { "occurred_fmt", r["occurred_fmt"] != DBNull.Value ? r["occurred_fmt"].ToString() : "" },
                    { "reported_fmt", r["reported_fmt"] != DBNull.Value ? r["reported_fmt"].ToString() : "" }
                });
            }

            context.Response.Write(Serializer.Serialize(new { incidents = list }));
        }

        private static void HandleHotspots(HttpContext context)
        {
            // gets cells with two or more incidents (categories cast to text so npgsql can read them),,,
            const string cellsSql = @"
                SELECT cell_lat, cell_lng, incident_count, categories::text[] AS categories
                FROM hotspot_cells
                WHERE incident_count >= 2
                ORDER BY incident_count DESC;";

            var dtCells = Db.ExecuteDataTable(cellsSql);
            var cells = new List<Dictionary<string, object>>();

            foreach (DataRow r in dtCells.Rows)
            {
                var catsObj = r["categories"];
                var catStrings = new List<string>();
                if (catsObj is string[] catArr)
                {
                    catStrings.AddRange(catArr);
                }
                else if (catsObj is string catStr)
                {
                    catStrings.Add(catStr);
                }

                cells.Add(new Dictionary<string, object>
                {
                    { "cell_lat", Convert.ToDouble(r["cell_lat"]) },
                    { "cell_lng", Convert.ToDouble(r["cell_lng"]) },
                    { "incident_count", Convert.ToInt32(r["incident_count"]) },
                    { "categories", catStrings }
                });
            }

            // gets incident points for the heatmap...
            const string pointsSql = @"
                SELECT latitude, longitude,
                       EXTRACT(EPOCH FROM (NOW() - reported_at)) / 86400.0 AS age_days
                FROM incidents
                WHERE invalid_flag = false
                  AND reported_at >= NOW() - INTERVAL '60 days';";

            var dtPoints = Db.ExecuteDataTable(pointsSql);
            var heatPoints = new List<double[]>();

            foreach (DataRow r in dtPoints.Rows)
            {
                double lat = Convert.ToDouble(r["latitude"]);
                double lng = Convert.ToDouble(r["longitude"]);
                double ageDays = Math.Max(0.0, Convert.ToDouble(r["age_days"]));
                double weight = Math.Round(Math.Pow(0.5, ageDays / 14.0), 3);
                heatPoints.Add(new[] { lat, lng, weight });
            }

            context.Response.Write(Serializer.Serialize(new
            {
                cells = cells,
                heatPoints = heatPoints
            }));
        }

        private static void HandleNetwork(HttpContext context)
        {
            const string nodesSql = "SELECT node_id, name, latitude, longitude FROM path_nodes;";
            var dtNodes = Db.ExecuteDataTable(nodesSql);
            var nodes = new List<Dictionary<string, object>>();

            foreach (DataRow r in dtNodes.Rows)
            {
                nodes.Add(new Dictionary<string, object>
                {
                    { "node_id", r["node_id"].ToString() },
                    { "name", r["name"] != DBNull.Value ? r["name"].ToString() : "Node" },
                    { "latitude", Convert.ToDouble(r["latitude"]) },
                    { "longitude", Convert.ToDouble(r["longitude"]) }
                });
            }

            const string linksSql = @"
                SELECT l.link_id, l.from_node, l.to_node, l.length_metres, l.base_risk_weight, COALESCE(l.is_lit, true) AS is_lit,
                       n1.latitude AS from_lat, n1.longitude AS from_lng,
                       n2.latitude AS to_lat, n2.longitude AS to_lng
                FROM path_links l
                JOIN path_nodes n1 ON n1.node_id = l.from_node
                JOIN path_nodes n2 ON n2.node_id = l.to_node;";

            var dtLinks = Db.ExecuteDataTable(linksSql);
            var links = new List<Dictionary<string, object>>();

            foreach (DataRow r in dtLinks.Rows)
            {
                links.Add(new Dictionary<string, object>
                {
                    { "link_id", r["link_id"].ToString() },
                    { "from_node", r["from_node"].ToString() },
                    { "to_node", r["to_node"].ToString() },
                    { "length_metres", Convert.ToDouble(r["length_metres"]) },
                    { "base_risk_weight", Convert.ToDouble(r["base_risk_weight"]) },
                    { "is_lit", Convert.ToBoolean(r["is_lit"]) },
                    { "from_lat", Convert.ToDouble(r["from_lat"]) },
                    { "from_lng", Convert.ToDouble(r["from_lng"]) },
                    { "to_lat", Convert.ToDouble(r["to_lat"]) },
                    { "to_lng", Convert.ToDouble(r["to_lng"]) }
                });
            }

            context.Response.Write(Serializer.Serialize(new
            {
                nodes = nodes,
                links = links
            }));
        }

        private static void HandleRebuildNetwork(HttpContext context)
        {
            try
            {
                int nodes = RouteService.RebuildCampusNetwork();
                long links = Convert.ToInt64(Db.ExecuteScalar("SELECT COUNT(*) FROM path_links;"));
                context.Response.Write(Serializer.Serialize(new
                {
                    success = true,
                    message = $"Campus walkway network loaded from OpenStreetMap data: {nodes} nodes and {links} path links."
                }));
            }
            catch (Exception ex)
            {
                context.Response.Write(Serializer.Serialize(new { success = false, message = "Failed to load campus walkway network: " + ex.Message }));
            }
        }

        // light overlay of unsafe areas (flagged zones + hotspot cells) for every role,,,
        private static void HandleRiskOverlay(HttpContext context)
        {
            const string zonesSql = "SELECT name, risk_level, radius_m, latitude, longitude FROM flagged_zones WHERE is_active = true;";
            var dtZones = Db.ExecuteDataTable(zonesSql);
            var zones = new List<Dictionary<string, object>>();
            foreach (DataRow r in dtZones.Rows)
            {
                zones.Add(new Dictionary<string, object>
                {
                    { "name", r["name"] != DBNull.Value ? r["name"].ToString() : "Flagged zone" },
                    { "risk_level", Convert.ToInt32(r["risk_level"]) },
                    { "radius_m", Convert.ToDouble(r["radius_m"]) },
                    { "latitude", Convert.ToDouble(r["latitude"]) },
                    { "longitude", Convert.ToDouble(r["longitude"]) }
                });
            }

            const string cellsSql = @"
                SELECT cell_lat, cell_lng, incident_count
                FROM hotspot_cells
                WHERE incident_count >= 2
                ORDER BY incident_count DESC
                LIMIT 60;";
            var dtCells = Db.ExecuteDataTable(cellsSql);
            var cells = new List<Dictionary<string, object>>();
            foreach (DataRow r in dtCells.Rows)
            {
                cells.Add(new Dictionary<string, object>
                {
                    { "cell_lat", Convert.ToDouble(r["cell_lat"]) },
                    { "cell_lng", Convert.ToDouble(r["cell_lng"]) },
                    { "incident_count", Convert.ToInt32(r["incident_count"]) }
                });
            }

            context.Response.Write(Serializer.Serialize(new { zones = zones, cells = cells }));
        }

        private static void HandleAddNode(HttpContext context)
        {
            string latStr = context.Request["latitude"];
            string lngStr = context.Request["longitude"];
            string name = context.Request["name"] ?? "Node";

            if (!Geo.TryParseCoordinate(latStr, out double lat) || !Geo.TryParseCoordinate(lngStr, out double lng))
            {
                context.Response.Write(Serializer.Serialize(new { success = false, message = "Invalid latitude/longitude." }));
                return;
            }

            Guid id = Guid.NewGuid();
            const string sql = "INSERT INTO path_nodes (node_id, name, latitude, longitude) VALUES (@id, @name, @lat, @lng);";
            Db.ExecuteNonQuery(sql,
                Db.Param("@id", id),
                Db.Param("@name", name.Trim()),
                Db.Param("@lat", lat),
                Db.Param("@lng", lng));

            context.Response.Write(Serializer.Serialize(new { success = true, nodeId = id.ToString() }));
        }

        private static void HandleDeleteNode(HttpContext context)
        {
            string nodeIdStr = context.Request["nodeId"];
            if (!Guid.TryParse(nodeIdStr, out Guid nodeId))
            {
                context.Response.Write(Serializer.Serialize(new { success = false, message = "Invalid node ID." }));
                return;
            }

            // removes path links before removing node...
            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = new NpgsqlCommand("DELETE FROM path_links WHERE from_node = @nodeId OR to_node = @nodeId;", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@nodeId", nodeId);
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new NpgsqlCommand("DELETE FROM path_nodes WHERE node_id = @nodeId;", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@nodeId", nodeId);
                        cmd.ExecuteNonQuery();
                    }

                    tran.Commit();
                    context.Response.Write(Serializer.Serialize(new { success = true }));
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    context.Response.Write(Serializer.Serialize(new { success = false, message = ex.Message }));
                }
            }
        }

        private static void HandleAddLink(HttpContext context)
        {
            string fromStr = context.Request["fromNode"];
            string toStr = context.Request["toNode"];

            if (!Guid.TryParse(fromStr, out Guid fromId) || !Guid.TryParse(toStr, out Guid toId))
            {
                context.Response.Write(Serializer.Serialize(new { success = false, message = "Invalid from/to node ID." }));
                return;
            }

            if (fromId == toId)
            {
                context.Response.Write(Serializer.Serialize(new { success = false, message = "Cannot link a node to itself." }));
                return;
            }

            // gets cordinates to measure path length,,,
            const string sqlCoord = "SELECT node_id, latitude, longitude FROM path_nodes WHERE node_id IN (@a, @b);";
            var dt = Db.ExecuteDataTable(sqlCoord, Db.Param("@a", fromId), Db.Param("@b", toId));
            if (dt.Rows.Count < 2)
            {
                context.Response.Write(Serializer.Serialize(new { success = false, message = "Could not find both nodes." }));
                return;
            }

            double aLat = 0, aLng = 0, bLat = 0, bLng = 0;
            foreach (DataRow r in dt.Rows)
            {
                if ((Guid)r["node_id"] == fromId)
                {
                    aLat = Convert.ToDouble(r["latitude"]);
                    aLng = Convert.ToDouble(r["longitude"]);
                }
                else
                {
                    bLat = Convert.ToDouble(r["latitude"]);
                    bLng = Convert.ToDouble(r["longitude"]);
                }
            }

            double length = Math.Round(Geo.Haversine(aLat, aLng, bLat, bLng), 1);
            Guid linkId = Guid.NewGuid();

            const string insertSql = @"
                INSERT INTO path_links (link_id, from_node, to_node, length_metres, base_risk_weight, is_lit)
                VALUES (@id, @from, @to, @len, 0.0, true);";

            Db.ExecuteNonQuery(insertSql,
                Db.Param("@id", linkId),
                Db.Param("@from", fromId),
                Db.Param("@to", toId),
                Db.Param("@len", length));

            context.Response.Write(Serializer.Serialize(new { success = true, linkId = linkId.ToString(), length_metres = length }));
        }

        private static void HandleDeleteLink(HttpContext context)
        {
            string linkIdStr = context.Request["linkId"];
            if (!Guid.TryParse(linkIdStr, out Guid linkId))
            {
                context.Response.Write(Serializer.Serialize(new { success = false, message = "Invalid link ID." }));
                return;
            }

            Db.ExecuteNonQuery("DELETE FROM path_links WHERE link_id = @id;", Db.Param("@id", linkId));
            context.Response.Write(Serializer.Serialize(new { success = true }));
        }

        private static void HandleToggleLit(HttpContext context)
        {
            string linkIdStr = context.Request["linkId"];
            if (!Guid.TryParse(linkIdStr, out Guid linkId))
            {
                context.Response.Write(Serializer.Serialize(new { success = false, message = "Invalid link ID." }));
                return;
            }

            const string sql = "UPDATE path_links SET is_lit = NOT COALESCE(is_lit, true) WHERE link_id = @id;";
            Db.ExecuteNonQuery(sql, Db.Param("@id", linkId));
            context.Response.Write(Serializer.Serialize(new { success = true }));
        }

        private static void HandleRoute(HttpContext context)
        {
            string startLatStr = context.Request["startLat"];
            string startLngStr = context.Request["startLng"];
            string endLatStr = context.Request["endLat"];
            string endLngStr = context.Request["endLng"];

            if (!Geo.TryParseCoordinate(startLatStr, out double startLat) ||
                !Geo.TryParseCoordinate(startLngStr, out double startLng) ||
                !Geo.TryParseCoordinate(endLatStr, out double endLat) ||
                !Geo.TryParseCoordinate(endLngStr, out double endLng))
            {
                context.Response.Write(Serializer.Serialize(new { ErrorMessage = "Invalid start or end coordinates provided." }));
                return;
            }

            var result = RouteService.CalculateRoutes(startLat, startLng, endLat, endLng);
            context.Response.Write(Serializer.Serialize(result));
        }

        private static void HandleFlaggedZones(HttpContext context)
        {
            const string sql = "SELECT zone_id, name, risk_level, radius_m, reason, latitude, longitude FROM flagged_zones WHERE is_active = true;";
            var dt = Db.ExecuteDataTable(sql);
            var list = new List<Dictionary<string, object>>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new Dictionary<string, object>
                {
                    { "zone_id", r["zone_id"].ToString() },
                    { "name", r["name"].ToString() },
                    { "risk_level", Convert.ToInt32(r["risk_level"]) },
                    { "radius_m", Convert.ToDouble(r["radius_m"]) },
                    { "reason", r["reason"] != DBNull.Value ? r["reason"].ToString() : "" },
                    { "latitude", Convert.ToDouble(r["latitude"]) },
                    { "longitude", Convert.ToDouble(r["longitude"]) }
                });
            }
            context.Response.Write(Serializer.Serialize(new { zones = list }));
        }

        private static void HandleLiveOps(HttpContext context)
        {
            // gets active guards on duty right now...
            const string sqlGuards = @"
                SELECT u.user_id, u.full_name, COALESCE(u.badge_number, '') AS badge_number,
                       l.latitude, l.longitude,
                       (now() - l.updated_at > interval '5 minutes') AS is_stale,
                       round(extract(epoch from (now() - l.updated_at)))::int AS seconds_ago
                FROM user_last_location l
                JOIN users u ON u.user_id = l.user_id
                WHERE l.is_on_duty = true AND u.role IN ('guard','supervisor') AND u.is_active = true;";

            var dtGuards = Db.ExecuteDataTable(sqlGuards);
            var guards = new List<Dictionary<string, object>>();
            foreach (DataRow r in dtGuards.Rows)
            {
                guards.Add(new Dictionary<string, object>
                {
                    { "user_id", r["user_id"].ToString() },
                    { "full_name", r["full_name"].ToString() },
                    { "badge_number", r["badge_number"].ToString() },
                    { "lat", Convert.ToDouble(r["latitude"]) },
                    { "lng", Convert.ToDouble(r["longitude"]) },
                    { "is_stale", Convert.ToBoolean(r["is_stale"]) },
                    { "seconds_ago", Convert.ToInt32(r["seconds_ago"]) }
                });
            }

            // gets active emergency sos alerts,,,
            const string sqlSos = @"
                SELECT i.incident_id, i.category::text AS category, i.status::text AS status,
                       COALESCE(sl.latitude, i.latitude) AS latitude,
                       COALESCE(sl.longitude, i.longitude) AS longitude,
                       to_char(i.reported_at at time zone 'Africa/Johannesburg', 'HH24:MI:SS') AS reported_fmt,
                       COALESCE(u.full_name, 'Student') AS reporter_name
                FROM incidents i
                LEFT JOIN users u ON u.user_id = i.reporter_id
                LEFT JOIN user_last_location sl ON sl.user_id = i.reporter_id
                WHERE i.is_emergency = true AND i.status <> 'Resolved' AND i.invalid_flag = false;";

            var dtSos = Db.ExecuteDataTable(sqlSos);
            var sosList = new List<Dictionary<string, object>>();
            foreach (DataRow r in dtSos.Rows)
            {
                sosList.Add(new Dictionary<string, object>
                {
                    { "incident_id", r["incident_id"].ToString() },
                    { "category", r["category"].ToString() },
                    { "status", r["status"].ToString() },
                    { "lat", Convert.ToDouble(r["latitude"]) },
                    { "lng", Convert.ToDouble(r["longitude"]) },
                    { "reported_fmt", r["reported_fmt"].ToString() },
                    { "reporter_name", r["reporter_name"].ToString() }
                });
            }

            // gets currently active dispatches...
            const string sqlDispatches = @"
                SELECT d.dispatch_id, d.incident_id, d.status::text AS dispatch_status,
                       COALESCE(u.full_name, 'Officer') AS guard_name,
                       gl.latitude AS guard_lat, gl.longitude AS guard_lng,
                       i.latitude AS incident_lat, i.longitude AS incident_lng,
                       i.category::text AS incident_category,
                       i.is_emergency
                FROM dispatches d
                JOIN incidents i ON i.incident_id = d.incident_id
                LEFT JOIN users u ON u.user_id = d.guard_id
                LEFT JOIN user_last_location gl ON gl.user_id = d.guard_id
                WHERE d.status IN ('pending', 'assigned', 'en_route', 'on_scene')
                  AND i.invalid_flag = false;";

            var dtDisp = Db.ExecuteDataTable(sqlDispatches);
            var dispatches = new List<Dictionary<string, object>>();
            foreach (DataRow r in dtDisp.Rows)
            {
                dispatches.Add(new Dictionary<string, object>
                {
                    { "dispatch_id", r["dispatch_id"].ToString() },
                    { "incident_id", r["incident_id"].ToString() },
                    { "status", r["dispatch_status"].ToString() },
                    { "guard_name", r["guard_name"].ToString() },
                    { "guard_lat", r["guard_lat"] != DBNull.Value ? Convert.ToDouble(r["guard_lat"]) : (double?)null },
                    { "guard_lng", r["guard_lng"] != DBNull.Value ? Convert.ToDouble(r["guard_lng"]) : (double?)null },
                    { "incident_lat", Convert.ToDouble(r["incident_lat"]) },
                    { "incident_lng", Convert.ToDouble(r["incident_lng"]) },
                    { "category", r["incident_category"].ToString() },
                    { "is_emergency", Convert.ToBoolean(r["is_emergency"]) }
                });
            }

            // gets unassigned incident queue,,,
            const string sqlUnassigned = @"
                SELECT i.incident_id, i.category::text AS category, i.status::text AS status,
                       i.latitude, i.longitude, i.is_emergency,
                       to_char(i.reported_at at time zone 'Africa/Johannesburg', 'HH24:MI') AS reported_fmt,
                       COALESCE(u.full_name, 'Anonymous') AS reporter_name
                FROM incidents i
                LEFT JOIN users u ON u.user_id = i.reporter_id
                WHERE i.status IN ('New', 'Acknowledged')
                  AND i.invalid_flag = false
                  AND NOT EXISTS (
                      SELECT 1 FROM dispatches d
                      WHERE d.incident_id = i.incident_id AND d.status NOT IN ('resolved', 'cancelled')
                  )
                ORDER BY i.is_emergency DESC, i.reported_at DESC
                LIMIT 20;";

            var dtUnassigned = Db.ExecuteDataTable(sqlUnassigned);
            var unassigned = new List<Dictionary<string, object>>();
            foreach (DataRow r in dtUnassigned.Rows)
            {
                unassigned.Add(new Dictionary<string, object>
                {
                    { "incident_id", r["incident_id"].ToString() },
                    { "category", r["category"].ToString() },
                    { "status", r["status"].ToString() },
                    { "lat", Convert.ToDouble(r["latitude"]) },
                    { "lng", Convert.ToDouble(r["longitude"]) },
                    { "is_emergency", Convert.ToBoolean(r["is_emergency"]) },
                    { "reported_fmt", r["reported_fmt"].ToString() },
                    { "reporter_name", r["reporter_name"].ToString() }
                });
            }

            context.Response.Write(Serializer.Serialize(new
            {
                guards = guards,
                activeSos = sosList,
                dispatches = dispatches,
                unassigned = unassigned
            }));
        }

        private static void HandleQuickAssign(HttpContext context, string supervisorId)
        {
            string incidentIdStr = context.Request.Form["incidentId"];
            string guardIdStr = context.Request.Form["guardId"];

            if (!Guid.TryParse(incidentIdStr, out Guid incidentId) ||
                !Guid.TryParse(guardIdStr, out Guid guardId))
            {
                SendError(context, 400, "Invalid incident or guard identifier.");
                return;
            }

            Guid supId = Guid.Parse(supervisorId);

            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    // tells the audit trigger who is making changes,,,
                    using (var cmd = new NpgsqlCommand("SELECT set_config('app.user_id', @me, true);", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@me", supId.ToString());
                        cmd.ExecuteNonQuery();
                    }

                    // creates the dispatch record...
                    Guid dispId = Guid.NewGuid();
                    const string sqlDisp = @"
                        INSERT INTO dispatches (dispatch_id, incident_id, guard_id, dispatched_by, status)
                        VALUES (@dispId, @incidentId, @guardId, @supId, 'assigned'::dispatch_status);";

                    using (var cmd = new NpgsqlCommand(sqlDisp, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@dispId", dispId);
                        cmd.Parameters.AddWithValue("@incidentId", incidentId);
                        cmd.Parameters.AddWithValue("@guardId", guardId);
                        cmd.Parameters.AddWithValue("@supId", supId);
                        cmd.ExecuteNonQuery();
                    }

                    // updates status to acknowledged if still new,,,
                    const string sqlCheck = "SELECT status::text FROM incidents WHERE incident_id = @id FOR UPDATE;";
                    string currStatus = "";
                    using (var cmd = new NpgsqlCommand(sqlCheck, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incidentId);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null) currStatus = obj.ToString();
                    }

                    if (currStatus == "New")
                    {
                        // trigger logs the change itself so we only add the note afterwards,,,
                        const string sqlAdv = @"
                            UPDATE incidents SET status = 'Acknowledged'::incident_status WHERE incident_id = @id;
                            UPDATE status_history SET note = 'Officer assigned via Live Ops'
                            WHERE incident_id = @id AND new_status = 'Acknowledged'::incident_status AND changed_at = now();";
                        using (var cmd = new NpgsqlCommand(sqlAdv, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@id", incidentId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // sends notification to guard...
                    const string sqlNotify = @"
                        INSERT INTO notifications (recipient_id, kind, title, body, incident_id, dispatch_id)
                        VALUES (@guardId, 'dispatch'::notification_kind, 'New Dispatch Assignment',
                                'You have been dispatched by supervisor.', @incidentId, @dispId);";

                    using (var cmd = new NpgsqlCommand(sqlNotify, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@guardId", guardId);
                        cmd.Parameters.AddWithValue("@incidentId", incidentId);
                        cmd.Parameters.AddWithValue("@dispId", dispId);
                        cmd.ExecuteNonQuery();
                    }

                    tran.Commit();
                    context.Response.Write(Serializer.Serialize(new { success = true }));
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    SendError(context, 500, "Failed to assign guard: " + ex.Message);
                }
            }
        }

        private static void HandleBoundary(HttpContext context)
        {
            string path = context.Server.MapPath("~/App_Data/campus-boundary.geojson");
            if (System.IO.File.Exists(path))
            {
                context.Response.ContentType = "application/geo+json; charset=utf-8";
                context.Response.TransmitFile(path);
            }
            else
            {
                SendError(context, 404, "Boundary file not found.");
            }
        }

        private static void SendError(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.Write(Serializer.Serialize(new { error = true, message = message }));
        }
    }
}
