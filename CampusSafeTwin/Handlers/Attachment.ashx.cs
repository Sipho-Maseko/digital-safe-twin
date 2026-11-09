using System;
using System.Data;
using System.IO;
using System.Web;
using System.Web.SessionState;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Handlers
{
    public class Attachment : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            string userId = context.Session?["UserId"] as string;
            string role = (context.Session?["Role"] as string) ?? "";

            if (string.IsNullOrEmpty(userId))
            {
                context.Response.StatusCode = 401;
                context.Response.End();
                return;
            }

            string idStr = context.Request.QueryString["id"];
            if (!Guid.TryParse(idStr, out Guid attachmentId))
            {
                context.Response.StatusCode = 400;
                context.Response.End();
                return;
            }

            // gets attachment file info and who reported it,,,
            const string sql = @"
                SELECT a.storage_key, a.kind::text AS kind, i.reporter_id
                FROM incident_attachments a
                JOIN incidents i ON i.incident_id = a.incident_id
                WHERE a.attachment_id = @id;";

            var dt = Db.ExecuteDataTable(sql, Db.Param("@id", attachmentId));
            if (dt.Rows.Count == 0)
            {
                context.Response.StatusCode = 404;
                context.Response.End();
                return;
            }

            var r = dt.Rows[0];
            string storageKey = r["storage_key"].ToString();
            string reporterId = r["reporter_id"] != DBNull.Value ? r["reporter_id"].ToString() : "";

            // cheks if current user is owner or guard...
            bool isStaff = string.Equals(role, "guard", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(role, "supervisor", StringComparison.OrdinalIgnoreCase);
            bool isOwner = string.Equals(userId, reporterId, StringComparison.OrdinalIgnoreCase);

            if (!isStaff && !isOwner)
            {
                context.Response.StatusCode = 403;
                context.Response.End();
                return;
            }

            // finds file path in uploads folder,,,
            string uploadDir = Path.Combine(context.Server.MapPath("~/App_Data"), "uploads");
            string filePath = Path.Combine(uploadDir, Path.GetFileName(storageKey));

            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.End();
                return;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg")
                context.Response.ContentType = "image/jpeg";
            else if (ext == ".png")
                context.Response.ContentType = "image/png";
            else
                context.Response.ContentType = "application/octet-stream";

            context.Response.TransmitFile(filePath);
        }
    }
}
