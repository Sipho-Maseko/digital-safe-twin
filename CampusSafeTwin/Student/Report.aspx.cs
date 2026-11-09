using System;
using System.Web.UI;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Student
{
    public partial class Report : Page
    {
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.DropDownList ddlCategory;
        protected global::System.Web.UI.WebControls.TextBox txtOccurredDate;
        protected global::System.Web.UI.WebControls.TextBox txtOccurredTime;
        protected global::System.Web.UI.WebControls.TextBox txtDescription;
        protected global::System.Web.UI.WebControls.HiddenField hfLat;
        protected global::System.Web.UI.WebControls.HiddenField hfLng;
        protected global::System.Web.UI.WebControls.FileUpload fuPhotos;
        protected global::System.Web.UI.WebControls.Button btnSubmitReport;

        // checks student login,,,
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "student");

            if (!IsPostBack)
            {
                var now = DateTime.Now;
                txtOccurredDate.Text = now.ToString("yyyy-MM-dd");
                txtOccurredTime.Text = now.ToString("HH:mm");
            }
        }

        // processes submitted report form...
        protected void btnSubmitReport_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            string category = ddlCategory.SelectedValue;
            string description = txtDescription.Text.Trim();
            string dateStr = txtOccurredDate.Text.Trim();
            string timeStr = txtOccurredTime.Text.Trim();

            if (string.IsNullOrEmpty(category))
            {
                pnlError.Visible = true;
                litError.Text = "Please select an incident category.";
                return;
            }

            if (string.IsNullOrEmpty(dateStr) || string.IsNullOrEmpty(timeStr))
            {
                pnlError.Visible = true;
                litError.Text = "Please provide the date and time when the incident occurred.";
                return;
            }

            if (!Geo.TryParseCoordinate(hfLat.Value, out double lat) || !Geo.TryParseCoordinate(hfLng.Value, out double lng))
            {
                pnlError.Visible = true;
                litError.Text = "Please click on the campus map to pin the incident location.";
                return;
            }

            // cheks any uploaded photos first,,,
            if (fuPhotos != null && fuPhotos.HasFiles)
            {
                if (fuPhotos.PostedFiles.Count > 3)
                {
                    pnlError.Visible = true;
                    litError.Text = "You may upload a maximum of 3 photos.";
                    return;
                }

                foreach (var file in fuPhotos.PostedFiles)
                {
                    if (file.ContentLength > 5 * 1024 * 1024)
                    {
                        pnlError.Visible = true;
                        litError.Text = "Each photo must be 5 MB or smaller.";
                        return;
                    }

                    byte[] header = new byte[4];
                    file.InputStream.Position = 0;
                    int bytesRead = file.InputStream.Read(header, 0, 4);
                    file.InputStream.Position = 0;

                    if (bytesRead < 4)
                    {
                        pnlError.Visible = true;
                        litError.Text = "Invalid image file format.";
                        return;
                    }

                    bool isJpg = (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF);
                    bool isPng = (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47);

                    if (!isJpg && !isPng)
                    {
                        pnlError.Visible = true;
                        litError.Text = "Only JPG and PNG images are allowed.";
                        return;
                    }
                }
            }

            string occurred = dateStr + " " + timeStr;
            Guid incidentId = Guid.NewGuid();
            Guid me = Guid.Parse(Auth.CurrentUserId);
            string refCode = "CS-" + incidentId.ToString("N").Substring(0, 8).ToUpperInvariant();

            using (var conn = Db.GetConnection())
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    // saves incident into database...
                    const string sqlIncident = @"
                        INSERT INTO incidents (incident_id, reporter_id, category, description, latitude, longitude, occurred_at)
                        VALUES (@id, @me, @category::incident_category, @desc, @lat, @lng, (@occurred || '+02')::timestamptz);";

                    using (var cmd = new NpgsqlCommand(sqlIncident, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incidentId);
                        cmd.Parameters.AddWithValue("@me", me);
                        cmd.Parameters.AddWithValue("@category", category);
                        cmd.Parameters.AddWithValue("@desc", string.IsNullOrEmpty(description) ? (object)DBNull.Value : description);
                        cmd.Parameters.AddWithValue("@lat", lat);
                        cmd.Parameters.AddWithValue("@lng", lng);
                        cmd.Parameters.AddWithValue("@occurred", occurred);
                        cmd.ExecuteNonQuery();
                    }

                    // adds starting history record for audit,,
                    const string sqlHistory = @"
                        INSERT INTO status_history (incident_id, changed_by, old_status, new_status, note)
                        VALUES (@id, @me, NULL, 'New'::incident_status, 'Report submitted');";

                    using (var cmd = new NpgsqlCommand(sqlHistory, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@id", incidentId);
                        cmd.Parameters.AddWithValue("@me", me);
                        cmd.ExecuteNonQuery();
                    }

                    // saves photo files to disk and logs in db...
                    if (fuPhotos != null && fuPhotos.HasFiles)
                    {
                        string uploadDir = Server.MapPath("~/App_Data/uploads");
                        if (!System.IO.Directory.Exists(uploadDir))
                        {
                            System.IO.Directory.CreateDirectory(uploadDir);
                        }

                        foreach (var file in fuPhotos.PostedFiles)
                        {
                            byte[] header = new byte[4];
                            file.InputStream.Position = 0;
                            file.InputStream.Read(header, 0, 4);
                            file.InputStream.Position = 0;

                            string ext = (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) ? ".jpg" : ".png";
                            string fileName = Guid.NewGuid().ToString("N") + ext;
                            string filePath = System.IO.Path.Combine(uploadDir, fileName);
                            file.SaveAs(filePath);

                            const string sqlAtt = @"
                                INSERT INTO incident_attachments (incident_id, kind, storage_key, note)
                                VALUES (@incident_id, 'photo'::attachment_kind, @storage_key, @note);";

                            using (var cmdAtt = new NpgsqlCommand(sqlAtt, conn, tran))
                            {
                                cmdAtt.Parameters.AddWithValue("@incident_id", incidentId);
                                cmdAtt.Parameters.AddWithValue("@storage_key", fileName);
                                cmdAtt.Parameters.AddWithValue("@note", System.IO.Path.GetFileName(file.FileName));
                                cmdAtt.ExecuteNonQuery();
                            }
                        }
                    }

                    // sends notification to supervisor accounts,,,
                    const string sqlNotifySupervisors = @"
                        INSERT INTO notifications (recipient_id, kind, title, body, incident_id)
                        SELECT user_id, 'system'::notification_kind, 'New Incident Reported',
                               'A new incident (' || @category || ') has been reported at ' || @occurred || '.', @incident_id
                        FROM users
                        WHERE role = 'supervisor' AND is_active = true;";

                    using (var cmdNotify = new NpgsqlCommand(sqlNotifySupervisors, conn, tran))
                    {
                        cmdNotify.Parameters.AddWithValue("@category", category);
                        cmdNotify.Parameters.AddWithValue("@occurred", occurred);
                        cmdNotify.Parameters.AddWithValue("@incident_id", incidentId);
                        cmdNotify.ExecuteNonQuery();
                    }

                    tran.Commit();

                    pnlSuccess.Visible = true;
                    litSuccess.Text = "Report submitted successfully! Reference: <strong>" + Server.HtmlEncode(refCode) + "</strong>";

                    // clears input boxes for next time...
                    ddlCategory.SelectedIndex = 0;
                    txtDescription.Text = "";
                    hfLat.Value = "";
                    hfLng.Value = "";
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    pnlError.Visible = true;
                    litError.Text = "Failed to submit report: " + Server.HtmlEncode(ex.Message);
                }
            }
        }
    }
}
