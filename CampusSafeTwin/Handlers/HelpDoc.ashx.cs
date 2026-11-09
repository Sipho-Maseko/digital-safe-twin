using System;
using System.IO;
using System.Web;
using System.Web.SessionState;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Handlers
{
    public class HelpDoc : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable => false;

        // streams a help guide pdf to any logged in user,,,
        public void ProcessRequest(HttpContext context)
        {
            string userId = context.Session?["UserId"] as string;

            if (string.IsNullOrEmpty(userId))
            {
                context.Response.StatusCode = 401;
                context.Response.End();
                return;
            }

            string idStr = context.Request.QueryString["id"];
            if (!Guid.TryParse(idStr, out Guid docId))
            {
                context.Response.StatusCode = 400;
                context.Response.End();
                return;
            }

            // looks up the stored file name for this guide...
            const string sql = "SELECT storage_key, original_name FROM help_documents WHERE doc_id = @id;";
            var dt = Db.ExecuteDataTable(sql, Db.Param("@id", docId));
            if (dt.Rows.Count == 0)
            {
                context.Response.StatusCode = 404;
                context.Response.End();
                return;
            }

            string storageKey = dt.Rows[0]["storage_key"].ToString();
            string originalName = dt.Rows[0]["original_name"] != DBNull.Value ? dt.Rows[0]["original_name"].ToString() : "help-guide.pdf";

            // finds the pdf inside the help folder, stripped so nobody can path traverse,,,
            string helpDir = Path.Combine(context.Server.MapPath("~/App_Data"), "help");
            string filePath = Path.Combine(helpDir, Path.GetFileName(storageKey));

            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.End();
                return;
            }

            // shows the pdf inline in the browser tab...
            context.Response.ContentType = "application/pdf";
            context.Response.AddHeader("Content-Disposition", "inline; filename=\"" + Path.GetFileName(originalName).Replace("\"", "") + "\"");
            context.Response.TransmitFile(filePath);
        }
    }
}
