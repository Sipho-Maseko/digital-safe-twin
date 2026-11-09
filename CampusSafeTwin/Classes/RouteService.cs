using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web.Hosting;
using System.Web.Script.Serialization;
using Npgsql;
using NpgsqlTypes;

namespace CampusSafeTwin.Classes
{
    public class RouteNode
    {
        public Guid NodeId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Name { get; set; }
    }

    public class RouteEdge
    {
        public Guid LinkId { get; set; }
        public Guid TargetNodeId { get; set; }
        public double LengthMetres { get; set; }
        public double BaseRiskWeight { get; set; }
        public bool IsLit { get; set; }
        public double ComputedRisk { get; set; }
        public double ShortestCost => LengthMetres;
        public double SaferCost => LengthMetres * (1.0 + 1.5 * ComputedRisk);
    }

    // result data for a walking route...
    public class RouteResult
    {
        public List<double[]> Coordinates { get; set; } = new List<double[]>();
        public List<string> NodeIds { get; set; } = new List<string>();
        public double DistanceMetres { get; set; }
        public double WalkingMinutes { get; set; }
        public double RiskScore { get; set; }
        public bool Found { get; set; }
    }

    // holds both shortest and safer paths,,,
    public class DualRouteResponse
    {
        public RouteResult Shortest { get; set; }
        public RouteResult Safer { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class RouteService
    {
        private static readonly object NetworkSeedLock = new object();
        private static bool _networkChecked;

        // makes sure the real campus walkway network is loaded...
        public static void EnsureCampusNetwork()
        {
            if (_networkChecked) return;

            lock (NetworkSeedLock)
            {
                if (_networkChecked) return;

                try
                {
                    long nodeCount = Convert.ToInt64(Db.ExecuteScalar("SELECT COUNT(*) FROM path_nodes;"));

                    if (nodeCount > 0)
                    {
                        // replaces the old synthetic demo grid with real walkways,,,
                        long synthetic = Convert.ToInt64(Db.ExecuteScalar(
                            "SELECT COUNT(*) FROM path_nodes WHERE name LIKE 'Campus Pathway (%' OR name LIKE 'Grid (%';"));

                        if (synthetic < nodeCount)
                        {
                            // network contains user-made nodes so leave it alone...
                            _networkChecked = true;
                            return;
                        }
                    }

                    RebuildCampusNetwork();
                    _networkChecked = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("EnsureCampusNetwork failed: " + ex.Message);
                }
            }
        }

        // wipes the network and loads walkways from the osm data file,,,
        public static int RebuildCampusNetwork()
        {
            string path = HostingEnvironment.MapPath("~/App_Data/campus-paths.json");
            if (path == null || !System.IO.File.Exists(path))
                throw new InvalidOperationException("Campus paths data file (App_Data/campus-paths.json) not found.");

            string json = System.IO.File.ReadAllText(path);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var data = serializer.Deserialize<Dictionary<string, object>>(json);

            var rawNodes = (System.Collections.ArrayList)data["nodes"];
            var rawLinks = (System.Collections.ArrayList)data["links"];

            // gives every node a fresh id for the db...
            var nodeIds = new Guid[rawNodes.Count];
            for (int i = 0; i < rawNodes.Count; i++) nodeIds[i] = Guid.NewGuid();

            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = new NpgsqlCommand(
                        "DELETE FROM path_links; UPDATE path_nodes SET location_id = NULL; DELETE FROM path_nodes;", conn, tran))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // bulk copies the walkway points,,,
                    using (var import = conn.BeginBinaryImport(
                        "COPY path_nodes (node_id, latitude, longitude) FROM STDIN (FORMAT BINARY)"))
                    {
                        for (int i = 0; i < rawNodes.Count; i++)
                        {
                            var pt = (System.Collections.ArrayList)rawNodes[i];
                            import.StartRow();
                            import.Write(nodeIds[i], NpgsqlDbType.Uuid);
                            import.Write(Convert.ToDouble(pt[0]), NpgsqlDbType.Double);
                            import.Write(Convert.ToDouble(pt[1]), NpgsqlDbType.Double);
                        }
                        import.Complete();
                    }

                    // bulk copies the path connections...
                    using (var import = conn.BeginBinaryImport(
                        "COPY path_links (link_id, from_node, to_node, length_metres, base_risk_weight, is_lit) FROM STDIN (FORMAT BINARY)"))
                    {
                        foreach (System.Collections.ArrayList lk in rawLinks)
                        {
                            int a = Convert.ToInt32(lk[0]);
                            int b = Convert.ToInt32(lk[1]);
                            if (a < 0 || b < 0 || a >= nodeIds.Length || b >= nodeIds.Length || a == b) continue;

                            import.StartRow();
                            import.Write(Guid.NewGuid(), NpgsqlDbType.Uuid);
                            import.Write(nodeIds[a], NpgsqlDbType.Uuid);
                            import.Write(nodeIds[b], NpgsqlDbType.Uuid);
                            import.Write(Convert.ToDouble(lk[2]), NpgsqlDbType.Double);
                            import.Write(0.0, NpgsqlDbType.Double);
                            import.Write(Convert.ToInt32(lk[3]) != 0, NpgsqlDbType.Boolean);
                        }
                        import.Complete();
                    }

                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }
            }

            LinkLocationsToNearestNodes();
            _networkChecked = true;
            return rawNodes.Count;
        }

        // ties each campus location to its closest walkway node,,,
        public static void LinkLocationsToNearestNodes()
        {
            try
            {
                const string sql = @"
                    UPDATE path_nodes pn
                    SET location_id = x.location_id
                    FROM (
                        SELECT cl.location_id,
                               (SELECT n.node_id FROM path_nodes n ORDER BY n.geom <-> cl.geom LIMIT 1) AS node_id
                        FROM campus_locations cl
                    ) x
                    WHERE pn.node_id = x.node_id;";
                Db.ExecuteNonQuery(sql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("LinkLocationsToNearestNodes failed: " + ex.Message);
            }
        }

        // old name kept so existing callers still work...
        public static void EnsureStarterGrid()
        {
            EnsureCampusNetwork();
        }

        // calculates both shortest and safest routes,,,
        public static DualRouteResponse CalculateRoutes(double startLat, double startLng, double endLat, double endLng)
        {
            var response = new DualRouteResponse();

            EnsureCampusNetwork();

            // finds closest points to start and finish...
            var startNode = SnapToNearestNode(startLat, startLng);
            var endNode = SnapToNearestNode(endLat, endLng);

            if (startNode == null || endNode == null)
            {
                response.ErrorMessage = "No network nodes found. Please initialize the network or starter grid first.";
                return response;
            }

            // gets all points from db,,,
            var nodes = LoadAllNodes();
            if (!nodes.ContainsKey(startNode.NodeId) || !nodes.ContainsKey(endNode.NodeId))
            {
                response.ErrorMessage = "Snapped nodes could not be loaded from database.";
                return response;
            }

            // cheks if its night time right now...
            bool isNight = IsNightInSouthAfrica();

            // gets recent incidents from past month,,,
            var recentIncidents = LoadRecentIncidents();

            // gets warning zones from db...
            var flaggedZones = LoadActiveFlaggedZones();

            // connects everything and calculates risk numbers,,,
            var adj = LoadAdjacencyGraph(nodes, recentIncidents, flaggedZones, isNight);

            // finds shortest way to walk...
            var shortestPath = RunDijkstra(startNode.NodeId, endNode.NodeId, adj, nodes, e => e.ShortestCost);

            // finds safest path with least danger,,,
            var saferPath = RunDijkstra(startNode.NodeId, endNode.NodeId, adj, nodes, e => e.SaferCost);

            response.Shortest = shortestPath;
            response.Safer = saferPath;

            return response;
        }

        // snaps location to closest point in network...
        public static RouteNode SnapToNearestNode(double lat, double lng)
        {
            EnsureCampusNetwork();

            const string sql = @"
                SELECT node_id, name, latitude, longitude
                FROM path_nodes
                ORDER BY geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography
                LIMIT 1;";

            var dt = Db.ExecuteDataTable(sql,
                Db.Param("@lat", lat),
                Db.Param("@lng", lng));

            if (dt.Rows.Count == 0) return null;

            var r = dt.Rows[0];
            return new RouteNode
            {
                NodeId = (Guid)r["node_id"],
                Name = r["name"] != DBNull.Value ? r["name"].ToString() : null,
                Latitude = Convert.ToDouble(r["latitude"]),
                Longitude = Convert.ToDouble(r["longitude"])
            };
        }

        // loads all path points from database,,,
        private static Dictionary<Guid, RouteNode> LoadAllNodes()
        {
            const string sql = "SELECT node_id, name, latitude, longitude FROM path_nodes;";
            var dt = Db.ExecuteDataTable(sql);
            var dict = new Dictionary<Guid, RouteNode>();
            foreach (DataRow r in dt.Rows)
            {
                var id = (Guid)r["node_id"];
                dict[id] = new RouteNode
                {
                    NodeId = id,
                    Name = r["name"] != DBNull.Value ? r["name"].ToString() : null,
                    Latitude = Convert.ToDouble(r["latitude"]),
                    Longitude = Convert.ToDouble(r["longitude"])
                };
            }
            return dict;
        }

        // stores incident coords and how old it is...
        private class IncidentPoint
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
            public double AgeDays { get; set; }
            public double RecencyWeight => Math.Pow(0.5, AgeDays / 14.0);
        }

        // loads recent incidents for danger calculation,,,
        private static List<IncidentPoint> LoadRecentIncidents()
        {
            const string sql = @"
                SELECT latitude, longitude,
                       EXTRACT(EPOCH FROM (NOW() - reported_at)) / 86400.0 AS age_days
                FROM incidents
                WHERE invalid_flag = false
                  AND reported_at >= NOW() - INTERVAL '30 days';";

            var list = new List<IncidentPoint>();
            var dt = Db.ExecuteDataTable(sql);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new IncidentPoint
                {
                    Lat = Convert.ToDouble(r["latitude"]),
                    Lng = Convert.ToDouble(r["longitude"]),
                    AgeDays = Math.Max(0.0, Convert.ToDouble(r["age_days"]))
                });
            }
            return list;
        }

        // cheks if south africa time is night time...
        private static bool IsNightInSouthAfrica()
        {
            try
            {
                var tzi = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
                var saTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);
                return saTime.Hour >= 18 || saTime.Hour < 6;
            }
            catch
            {
                // fallback if timezone not found...
                var saTime = DateTime.UtcNow.AddHours(2);
                return saTime.Hour >= 18 || saTime.Hour < 6;
            }
        }

        // danger zone details,,,
        private class FlaggedZoneItem
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
            public double RadiusM { get; set; }
            public short RiskLevel { get; set; }
        }

        // loads active danger zones...
        private static List<FlaggedZoneItem> LoadActiveFlaggedZones()
        {
            const string sql = "SELECT latitude, longitude, radius_m, risk_level FROM flagged_zones WHERE is_active = true;";
            var list = new List<FlaggedZoneItem>();
            try
            {
                var dt = Db.ExecuteDataTable(sql);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new FlaggedZoneItem
                    {
                        Lat = Convert.ToDouble(r["latitude"]),
                        Lng = Convert.ToDouble(r["longitude"]),
                        RadiusM = Convert.ToDouble(r["radius_m"]),
                        RiskLevel = Convert.ToInt16(r["risk_level"])
                    });
                }
            }
            catch { }
            return list;
        }

        // builds map connections with danger scores,,,
        private static Dictionary<Guid, List<RouteEdge>> LoadAdjacencyGraph(
            Dictionary<Guid, RouteNode> nodes,
            List<IncidentPoint> incidents,
            List<FlaggedZoneItem> flaggedZones,
            bool isNight)
        {
            const string sql = @"
                SELECT link_id, from_node, to_node, length_metres, base_risk_weight, COALESCE(is_lit, true) AS is_lit
                FROM path_links;";

            var dt = Db.ExecuteDataTable(sql);
            var adj = new Dictionary<Guid, List<RouteEdge>>();

            foreach (var nId in nodes.Keys)
            {
                adj[nId] = new List<RouteEdge>();
            }

            foreach (DataRow r in dt.Rows)
            {
                var linkId = (Guid)r["link_id"];
                var fromId = (Guid)r["from_node"];
                var toId = (Guid)r["to_node"];
                double length = Convert.ToDouble(r["length_metres"]);
                double baseRisk = Convert.ToDouble(r["base_risk_weight"]);
                bool isLit = Convert.ToBoolean(r["is_lit"]);

                if (!nodes.ContainsKey(fromId) || !nodes.ContainsKey(toId))
                    continue;

                var nodeA = nodes[fromId];
                var nodeB = nodes[toId];

                // adds danger if incidents happend nearby,,,
                double incidentSum = 0.0;
                foreach (var inc in incidents)
                {
                    double d = Geo.DistanceToSegment(inc.Lat, inc.Lng, nodeA.Latitude, nodeA.Longitude, nodeB.Latitude, nodeB.Longitude);
                    if (d <= 75.0)
                    {
                        incidentSum += inc.RecencyWeight * (1.0 - d / 75.0);
                    }
                }

                // adds penalty if dark and night time...
                double nightPenalty = (!isLit && isNight) ? 2.0 : 0.0;

                // adds extra risk for flagged zones,,,
                double flaggedZonePenalty = 0.0;
                foreach (var z in flaggedZones)
                {
                    double dZone = Geo.DistanceToSegment(z.Lat, z.Lng, nodeA.Latitude, nodeA.Longitude, nodeB.Latitude, nodeB.Longitude);
                    if (dZone <= z.RadiusM)
                    {
                        flaggedZonePenalty += z.RiskLevel * 1.5;
                    }
                }

                double totalRisk = baseRisk + incidentSum + nightPenalty + flaggedZonePenalty;

                // can walk both ways on path...
                adj[fromId].Add(new RouteEdge
                {
                    LinkId = linkId,
                    TargetNodeId = toId,
                    LengthMetres = length,
                    BaseRiskWeight = baseRisk,
                    IsLit = isLit,
                    ComputedRisk = totalRisk
                });

                adj[toId].Add(new RouteEdge
                {
                    LinkId = linkId,
                    TargetNodeId = fromId,
                    LengthMetres = length,
                    BaseRiskWeight = baseRisk,
                    IsLit = isLit,
                    ComputedRisk = totalRisk
                });
            }

            return adj;
        }

        // runs dijkstra pathfinding algorithm,,,
        private static RouteResult RunDijkstra(
            Guid startNodeId,
            Guid endNodeId,
            Dictionary<Guid, List<RouteEdge>> adj,
            Dictionary<Guid, RouteNode> nodes,
            Func<RouteEdge, double> costSelector)
        {
            var distances = new Dictionary<Guid, double>();
            var previousNode = new Dictionary<Guid, Guid?>();
            var previousEdge = new Dictionary<Guid, RouteEdge>();
            var visited = new HashSet<Guid>();

            foreach (var nId in adj.Keys)
            {
                distances[nId] = double.PositiveInfinity;
                previousNode[nId] = null;
            }

            distances[startNodeId] = 0;

            var queue = new SortedSet<(double distance, Guid nodeId)>(
                Comparer<(double distance, Guid nodeId)>.Create((a, b) =>
                {
                    int cmp = a.distance.CompareTo(b.distance);
                    return cmp != 0 ? cmp : a.nodeId.CompareTo(b.nodeId);
                }));

            queue.Add((0.0, startNodeId));

            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);

                var u = current.nodeId;
                if (visited.Contains(u)) continue;
                visited.Add(u);

                if (u == endNodeId) break;

                if (!adj.ContainsKey(u)) continue;

                foreach (var edge in adj[u])
                {
                    var v = edge.TargetNodeId;
                    if (visited.Contains(v)) continue;

                    double edgeCost = costSelector(edge);
                    double newDist = distances[u] + edgeCost;

                    if (newDist < distances[v])
                    {
                        if (distances[v] < double.PositiveInfinity)
                        {
                            queue.Remove((distances[v], v));
                        }

                        distances[v] = newDist;
                        previousNode[v] = u;
                        previousEdge[v] = edge;
                        queue.Add((newDist, v));
                    }
                }
            }

            var result = new RouteResult();

            if (double.IsPositiveInfinity(distances[endNodeId]))
            {
                result.Found = false;
                return result;
            }

            result.Found = true;

            // builds the route backwards from finish...
            var pathNodes = new List<Guid>();
            Guid? curr = endNodeId;
            while (curr.HasValue)
            {
                pathNodes.Insert(0, curr.Value);
                curr = previousNode[curr.Value];
            }

            double totalMetres = 0.0;
            double totalRisk = 0.0;

            for (int i = 0; i < pathNodes.Count; i++)
            {
                var n = nodes[pathNodes[i]];
                result.Coordinates.Add(new double[] { n.Latitude, n.Longitude });
                result.NodeIds.Add(pathNodes[i].ToString());

                if (i > 0)
                {
                    var prevId = pathNodes[i - 1];
                    var thisId = pathNodes[i];
                    if (previousEdge.TryGetValue(thisId, out var edge))
                    {
                        totalMetres += edge.LengthMetres;
                        totalRisk += edge.ComputedRisk;
                    }
                }
            }

            result.DistanceMetres = Math.Round(totalMetres, 1);
            // estimates walking time in minutes,,,
            result.WalkingMinutes = Math.Round(totalMetres / (1.3 * 60.0), 1);
            result.RiskScore = Math.Round(totalRisk, 2);

            return result;
        }
    }
}
