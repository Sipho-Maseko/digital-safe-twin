using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Admin
{
    // helper for generating random numbers with seed...
    public class Mulberry32
    {
        private uint _state;

        public Mulberry32(uint seed)
        {
            _state = seed == 0 ? 1 : seed;
        }

        public double NextDouble()
        {
            _state += 0x6D2B79F5;
            uint t = _state;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return ((t ^ (t >> 14)) >> 0) / 4294967296.0;
        }

        public int Next(int min, int max)
        {
            if (min >= max) return min;
            return min + (int)(NextDouble() * (max - min));
        }
    }

    public partial class Simulation : Page
    {
        protected global::System.Web.UI.WebControls.Button btnPurgeAllDemo;
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.TextBox txtBatchName;
        protected global::System.Web.UI.WebControls.TextBox txtSeed;
        protected global::System.Web.UI.WebControls.TextBox txtCount;
        protected global::System.Web.UI.WebControls.DropDownList ddlTimespan;
        protected global::System.Web.UI.WebControls.Button btnGenerate;
        protected global::System.Web.UI.WebControls.GridView gvBatches;

        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "supervisor");

            if (!IsPostBack)
            {
                LoadBatches();
            }
        }

        private void LoadBatches()
        {
            const string sql = @"
                SELECT s.seed_id, s.name, s.seed_value,
                       to_char(s.generated_at at time zone 'Africa/Johannesburg', 'YYYY-MM-DD HH24:MI') AS generated_fmt,
                       count(i.incident_id) AS incident_count
                FROM seed_records s
                LEFT JOIN incidents i ON i.seed_id = s.seed_id
                GROUP BY s.seed_id, s.name, s.seed_value, s.generated_at
                ORDER BY s.generated_at DESC;";

            var dt = Db.ExecuteDataTable(sql);
            gvBatches.DataSource = dt;
            gvBatches.DataBind();
        }

        protected void btnGenerate_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            string name = txtBatchName.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = "Simulation Run";

            if (!uint.TryParse(txtSeed.Text.Trim(), out uint seed)) seed = 1337;
            if (!int.TryParse(txtCount.Text.Trim(), out int count) || count <= 0) count = 25;
            if (count > 150) count = 150;
            if (!int.TryParse(ddlTimespan.SelectedValue, out int days)) days = 14;

            var prng = new Mulberry32(seed);

            string[] categories = { "theft", "assault", "harassment", "suspicious_activity", "vandalism", "medical", "other" };
            string[] descriptions = {
                "Student bag reported stolen from bench near library steps.",
                "Individual observed attempting locked vehicle doors in parking area.",
                "Verbal altercation and aggressive harassment reported along eastern walkway.",
                "Broken exterior lighting pole and graffiti tagged on wall.",
                "Medical distress: student collapsed due to heat exhaustion.",
                "Suspicious individual loitering near residential gates after dark.",
                "Bicycle lock cut and mountain bike stolen from rack.",
                "Student phone snatched while walking and talking on Wasmer street."
            };

            // coordinates around campus boundaries,,,
            double minLat = -26.6950, maxLat = -26.6830;
            double minLng = 27.0870, maxLng = 27.0990;

            // picks a few hub spots first so incidents cluster into hotspots like real patterns,,,
            int hubCount = 4 + prng.Next(0, 3);
            var hubLats = new double[hubCount];
            var hubLngs = new double[hubCount];
            for (int h = 0; h < hubCount; h++)
            {
                hubLats[h] = minLat + prng.NextDouble() * (maxLat - minLat);
                hubLngs[h] = minLng + prng.NextDouble() * (maxLng - minLng);
            }

            Guid seedId = Guid.NewGuid();
            Guid me = Guid.Parse(Auth.CurrentUserId);
            DateTime now = DateTime.UtcNow;

            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    // saves the batch record in database...
                    const string sqlSeed = @"
                        INSERT INTO seed_records (seed_id, name, seed_value, notes)
                        VALUES (@id, @name, @val, @notes);";

                    using (var cmd = new NpgsqlCommand(sqlSeed, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", seedId);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@val", (int)seed);
                        cmd.Parameters.AddWithValue("@notes", $"Generated {count} records over {days} days");
                        cmd.ExecuteNonQuery();
                    }

                    // generates random incidents across the map,,,
                    for (int i = 0; i < count; i++)
                    {
                        Guid incId = Guid.NewGuid();
                        string cat = categories[prng.Next(0, categories.Length)];
                        string desc = descriptions[prng.Next(0, descriptions.Length)];

                        // three quarters cluster tightly around a hub, rest scatter anywhere...
                        double lat, lng;
                        if (prng.NextDouble() < 0.75)
                        {
                            int hub = prng.Next(0, hubCount);
                            lat = hubLats[hub] + (prng.NextDouble() + prng.NextDouble() - 1.0) * 0.00055;
                            lng = hubLngs[hub] + (prng.NextDouble() + prng.NextDouble() - 1.0) * 0.00062;
                        }
                        else
                        {
                            lat = minLat + prng.NextDouble() * (maxLat - minLat);
                            lng = minLng + prng.NextDouble() * (maxLng - minLng);
                        }

                        // picks a random time in the past...
                        int secondsAgo = prng.Next(300, days * 86400);
                        DateTime occurredTime = now.AddSeconds(-secondsAgo);
                        DateTime reportedTime = occurredTime.AddMinutes(prng.Next(5, 120));

                        // some random emergency alerts,,,
                        bool isEmergency = (prng.NextDouble() < 0.25);
                        string status = "New";
                        double statusDice = prng.NextDouble();
                        if (statusDice > 0.7) status = "Resolved";
                        else if (statusDice > 0.45) status = "In Progress";
                        else if (statusDice > 0.25) status = "Acknowledged";

                        const string sqlInc = @"
                            INSERT INTO incidents (incident_id, reporter_id, category, description, status, is_emergency, latitude, longitude, occurred_at, reported_at, seed_id)
                            VALUES (@id, @me, @cat::incident_category, @desc, @status::incident_status, @isEmergency, @lat, @lng, @occurred, @reported, @seedId);";

                        using (var cmd = new NpgsqlCommand(sqlInc, conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@id", incId);
                            cmd.Parameters.AddWithValue("@me", me);
                            cmd.Parameters.AddWithValue("@cat", cat);
                            cmd.Parameters.AddWithValue("@desc", desc);
                            cmd.Parameters.AddWithValue("@status", status);
                            cmd.Parameters.AddWithValue("@isEmergency", isEmergency);
                            cmd.Parameters.AddWithValue("@lat", lat);
                            cmd.Parameters.AddWithValue("@lng", lng);
                            cmd.Parameters.AddWithValue("@occurred", occurredTime);
                            cmd.Parameters.AddWithValue("@reported", reportedTime);
                            cmd.Parameters.AddWithValue("@seedId", seedId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tran.Commit();
                    pnlSuccess.Visible = true;
                    litSuccess.Text = $"Simulation batch \"{Server.HtmlEncode(name)}\" generated successfully with {count} incidents!";
                    LoadBatches();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    pnlError.Visible = true;
                    litError.Text = "Failed to generate simulation batch: " + Server.HtmlEncode(ex.Message);
                }
            }
        }

        protected void gvBatches_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            Guid seedId = (Guid)gvBatches.DataKeys[e.RowIndex].Value;

            try
            {
                using (var conn = Db.GetConnection())
                using (var tran = conn.BeginTransaction())
                {
                    // deletes incidents linked to this test batch...
                    const string sqlDelIncidents = "DELETE FROM incidents WHERE seed_id = @id;";
                    using (var cmd = new NpgsqlCommand(sqlDelIncidents, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", seedId);
                        cmd.ExecuteNonQuery();
                    }

                    const string sqlDelSeed = "DELETE FROM seed_records WHERE seed_id = @id;";
                    using (var cmd = new NpgsqlCommand(sqlDelSeed, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", seedId);
                        cmd.ExecuteNonQuery();
                    }

                    tran.Commit();
                }

                pnlSuccess.Visible = true;
                litSuccess.Text = "Simulation batch and associated demo incidents removed.";
                LoadBatches();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to remove batch: " + Server.HtmlEncode(ex.Message);
            }
        }

        // clears out all test simulation data from db,,,
        protected void btnPurgeAllDemo_Click(object sender, EventArgs e)
        {
            try
            {
                using (var conn = Db.GetConnection())
                using (var tran = conn.BeginTransaction())
                {
                    const string sqlDel = @"
                        DELETE FROM incidents WHERE seed_id IS NOT NULL;
                        DELETE FROM seed_records;";

                    using (var cmd = new NpgsqlCommand(sqlDel, conn, tran))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    tran.Commit();
                }

                pnlSuccess.Visible = true;
                litSuccess.Text = "All simulation batches and demo incidents purged.";
                LoadBatches();
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to purge demo data: " + Server.HtmlEncode(ex.Message);
            }
        }
    }
}
