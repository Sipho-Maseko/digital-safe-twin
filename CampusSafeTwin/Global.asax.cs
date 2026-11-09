using System;
using System.Configuration;
using System.Diagnostics;
using System.Web;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin
{
    public class Global : HttpApplication
    {
        // runs when the website starts up...
        protected void Application_Start(object sender, EventArgs e)
        {
            EnsureSchemaFixes();
            SeedSupervisorIfNoneExists();
            SeedDemoUsers();
            SeedCampusLocationsIfEmpty();
            SeedHelpGuidesIfMissing();
            RouteService.EnsureCampusNetwork();
        }

        // applies small database fixes so older databases heal themselfs...
        public static void EnsureSchemaFixes()
        {
            try
            {
                // centers hotspot cells instead of using the grid corner,,,
                const string viewSql = @"
                    create or replace view hotspot_cells as
                    with grid as (
                      select ST_SnapToGrid(ST_Transform(geom::geometry, 3857), 40) as cell_3857,
                             category
                      from incidents
                      where invalid_flag = false
                    )
                    select ST_Y(ST_Transform(ST_Translate(cell_3857, 20, 20), 4326)) as cell_lat,
                           ST_X(ST_Transform(ST_Translate(cell_3857, 20, 20), 4326)) as cell_lng,
                           count(*)::int                        as incident_count,
                           array_agg(distinct category)         as categories
                    from grid
                    group by cell_3857;";
                Db.ExecuteNonQuery(viewSql);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("EnsureSchemaFixes failed: " + ex.Message);
            }

            try
            {
                // makes the help guides table if an older db dosent have it yet,,,
                const string helpSql = @"
                    create table if not exists help_documents (
                      doc_id        uuid primary key default gen_random_uuid(),
                      title         text not null,
                      description   text,
                      category      text not null default 'general',
                      storage_key   text not null unique,
                      original_name text,
                      file_size     bigint,
                      uploaded_by   uuid references users(user_id),
                      created_at    timestamptz not null default now()
                    );
                    create index if not exists help_documents_title_idx on help_documents (lower(title));";
                Db.ExecuteNonQuery(helpSql);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("EnsureSchemaFixes (help_documents) failed: " + ex.Message);
            }
        }

        // registers the bundled help guide pdfs in the db so students can find them...
        public static void SeedHelpGuidesIfMissing()
        {
            try
            {
                string helpDir = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/help");
                if (helpDir == null || !System.IO.Directory.Exists(helpDir)) return;

                // bundled guides shiped with the app: file name, title, category, description,,,
                var bundledGuides = new[]
                {
                    ("getting-started.pdf", "Getting Started with CampusSafe Twin", "getting_started", "How to register an account, log in, and find your way around the portal."),
                    ("reporting-an-incident.pdf", "How to Report an Incident", "reporting", "Step by step guide to reporting theft, harassment, suspicious activity and other incidents with photos and a map pin."),
                    ("using-the-sos-button.pdf", "Using the SOS Emergency Button", "sos", "What the red SOS button does, when to use it, and what happens after you press it."),
                    ("finding-a-safe-route.pdf", "Finding a Safe Route Across Campus", "safe_route", "How to plan a walking route that avoids recent incident hotspots using the Safe Route planner."),
                    ("my-reports-and-notifications.pdf", "Tracking My Reports & Notifications", "notifications", "How to follow up on reports you submitted and understand the notifications the system sends you."),
                    ("security-officer-guide.pdf", "Security Officer Field Guide", "security", "For security staff: using the live security map, responding to dispatches, and flagging risky zones."),
                    ("supervisor-admin-guide.pdf", "Supervisor & Administration Guide", "admin", "For supervisors: live ops, hotspots, managing staff and locations, analytics, and uploading help guides.")
                };

                foreach (var guide in bundledGuides)
                {
                    string filePath = System.IO.Path.Combine(helpDir, guide.Item1);
                    if (!System.IO.File.Exists(filePath)) continue;

                    long size = new System.IO.FileInfo(filePath).Length;

                    // only inserts when the file isnt registered yet so restarts stay idempotent,,,
                    const string sql = @"
                        INSERT INTO help_documents (title, category, description, storage_key, original_name, file_size)
                        VALUES (@title, @category, @desc, @key, @orig, @size)
                        ON CONFLICT (storage_key) DO NOTHING;";

                    Db.ExecuteNonQuery(sql,
                        Db.Param("@title", guide.Item2),
                        Db.Param("@category", guide.Item3),
                        Db.Param("@desc", guide.Item4),
                        Db.Param("@key", guide.Item1),
                        Db.Param("@orig", guide.Item1),
                        Db.Param("@size", size));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("SeedHelpGuidesIfMissing failed: " + ex.Message);
            }
        }

        // loads real campus places from data file when table is empty,,,
        public static void SeedCampusLocationsIfEmpty()
        {
            try
            {
                long count = Convert.ToInt64(Db.ExecuteScalar("SELECT COUNT(*) FROM campus_locations;"));
                if (count > 0) return;

                string path = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/campus-locations.json");
                if (path == null || !System.IO.File.Exists(path)) return;

                string json = System.IO.File.ReadAllText(path);
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var items = serializer.Deserialize<System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>>(json);
                if (items == null) return;

                foreach (var item in items)
                {
                    const string sql = @"
                        INSERT INTO campus_locations (name, kind, description, latitude, longitude)
                        VALUES (@name, @kind, @desc, @lat, @lng);";

                    Db.ExecuteNonQuery(sql,
                        Db.Param("@name", item["name"].ToString()),
                        Db.Param("@kind", item["kind"].ToString()),
                        Db.Param("@desc", item.ContainsKey("description") ? item["description"].ToString() : ""),
                        Db.Param("@lat", Convert.ToDouble(item["lat"])),
                        Db.Param("@lng", Convert.ToDouble(item["lng"])));
                }

                RouteService.LinkLocationsToNearestNodes();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("SeedCampusLocationsIfEmpty failed: " + ex.Message);
            }
        }

        public static void SeedDemoUsers()
        {
            try
            {
                string hash = Auth.HashPassword("CampusSafe2026!");

                // creates supervisor test user,,,
                const string sqlPieta = @"
                    INSERT INTO users (email, full_name, role, password_hash, is_active, badge_number)
                    VALUES ('pieta@security.nwu.ac.za', 'Pieta Supervisor', 'supervisor'::user_role, @hash, true, 'NWU-S01')
                    ON CONFLICT (email) DO NOTHING;";
                Db.ExecuteNonQuery(sqlPieta, Db.Param("@hash", hash));

                // creates officer test user...
                const string sqlKobus = @"
                    INSERT INTO users (email, full_name, role, password_hash, is_active, badge_number)
                    VALUES ('kobus@guard.nwu.ac.za', 'Officer Kobus van der Merwe', 'guard'::user_role, @hash, true, 'NWU-G01')
                    ON CONFLICT (email) DO NOTHING;";
                Db.ExecuteNonQuery(sqlKobus, Db.Param("@hash", hash));

                // creates student test user,,,
                const string sqlSipho = @"
                    INSERT INTO users (email, full_name, role, password_hash, is_active, student_number)
                    VALUES ('sipho@student.nwu.ac.za', 'Sipho Dlamini', 'student'::user_role, @hash, true, '29384721')
                    ON CONFLICT (email) DO NOTHING;";
                Db.ExecuteNonQuery(sqlSipho, Db.Param("@hash", hash));

                // sets default officer on duty with starting gps...
                const string sqlLoc = @"
                    INSERT INTO user_last_location (user_id, latitude, longitude, accuracy_m, is_on_duty)
                    SELECT user_id, -26.6890, 27.0935, 5.0, true
                    FROM users
                    WHERE email = 'kobus@guard.nwu.ac.za'
                    ON CONFLICT (user_id) DO UPDATE SET is_on_duty = true, updated_at = now();";
                Db.ExecuteNonQuery(sqlLoc);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Could not seed demo users: " + ex.Message);
            }
        }

        // creates default admin if table is empty,,,
        public static void SeedSupervisorIfNoneExists()
        {
            try
            {
                const string countSql = "SELECT COUNT(*) FROM users WHERE role = 'supervisor'::user_role;";
                long count = Convert.ToInt64(Db.ExecuteScalar(countSql));

                if (count == 0)
                {
                    string email = ConfigurationManager.AppSettings["DefaultAdminEmail"] ?? "supervisor@campussafe.ac.za";
                    string rawPass = ConfigurationManager.AppSettings["DefaultAdminPassword"] ?? "SupervisorPass123!";
                    string hash = Auth.HashPassword(rawPass);

                    const string insertSql = @"
                        INSERT INTO users (email, full_name, role, password_hash, is_active)
                        VALUES (@email, 'Campus Supervisor', 'supervisor'::user_role, @hash, true);";

                    Db.ExecuteNonQuery(insertSql,
                        Db.Param("@email", email.Trim().ToLowerInvariant()),
                        Db.Param("@hash", hash));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Could not seed supervisor during application startup: " + ex.Message);
            }
        }
    }
}
