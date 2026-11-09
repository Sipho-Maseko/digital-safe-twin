using System;
using System.Data;
using System.IO;
using System.Web.UI;
using System.Web.UI.WebControls;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin
{
    public partial class Help : Page
    {
        protected global::System.Web.UI.WebControls.Panel pnlSuccess;
        protected global::System.Web.UI.WebControls.Literal litSuccess;
        protected global::System.Web.UI.WebControls.Panel pnlError;
        protected global::System.Web.UI.WebControls.Literal litError;
        protected global::System.Web.UI.WebControls.Panel pnlSearch;
        protected global::System.Web.UI.WebControls.TextBox txtSearch;
        protected global::System.Web.UI.WebControls.Button btnSearch;
        protected global::System.Web.UI.WebControls.Button btnClear;
        protected global::System.Web.UI.WebControls.Literal litResultCount;
        protected global::System.Web.UI.WebControls.Repeater rptDocs;
        protected global::System.Web.UI.WebControls.Panel pnlEmpty;
        protected global::System.Web.UI.WebControls.Panel pnlAdmin;
        protected global::System.Web.UI.WebControls.TextBox txtTitle;
        protected global::System.Web.UI.WebControls.TextBox txtDesc;
        protected global::System.Web.UI.WebControls.DropDownList ddlCategory;
        protected global::System.Web.UI.WebControls.FileUpload fuPdf;
        protected global::System.Web.UI.WebControls.Button btnUpload;

        // only supervisors see upload and delete controls,,,
        protected bool IsSupervisor => string.Equals(Auth.CurrentRole, "supervisor", StringComparison.OrdinalIgnoreCase);

        // any logged in user can read the guides...
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this);

            pnlAdmin.Visible = IsSupervisor;

            if (!IsPostBack)
            {
                LoadDocs("");
            }
        }

        // fetches guides from db, filtered when user searched something,,,
        private void LoadDocs(string search)
        {
            DataTable dt;
            if (string.IsNullOrWhiteSpace(search))
            {
                const string sql = @"
                    SELECT doc_id, title, description, category, storage_key, original_name, file_size, created_at
                    FROM help_documents
                    ORDER BY category ASC, title ASC;";
                dt = Db.ExecuteDataTable(sql);
            }
            else
            {
                const string sql = @"
                    SELECT doc_id, title, description, category, storage_key, original_name, file_size, created_at
                    FROM help_documents
                    WHERE title ILIKE @term OR description ILIKE @term OR category ILIKE @term
                    ORDER BY category ASC, title ASC;";
                dt = Db.ExecuteDataTable(sql, Db.Param("@term", "%" + search.Trim() + "%"));
            }

            rptDocs.DataSource = dt;
            rptDocs.DataBind();

            pnlEmpty.Visible = dt.Rows.Count == 0;
            litResultCount.Text = string.IsNullOrWhiteSpace(search)
                ? $"Showing all {dt.Rows.Count} help guide(s)."
                : $"Found {dt.Rows.Count} guide(s) matching \"{Server.HtmlEncode(search.Trim())}\".";
        }

        // runs the search when user clicks or presses enter...
        protected void btnSearch_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;
            LoadDocs(txtSearch.Text);
        }

        // clears the search box and shows everything again,,,
        protected void btnClear_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;
            txtSearch.Text = "";
            LoadDocs("");
        }

        // saves a new pdf guide uploaded by the admin...
        protected void btnUpload_Click(object sender, EventArgs e)
        {
            pnlSuccess.Visible = false;
            pnlError.Visible = false;

            // double cheks role server side so nobody sneaky uploads,,,
            if (!IsSupervisor)
            {
                pnlError.Visible = true;
                litError.Text = "Only supervisors may upload help guides.";
                LoadDocs(txtSearch.Text);
                return;
            }

            string title = txtTitle.Text.Trim();
            string desc = txtDesc.Text.Trim();
            string category = ddlCategory.SelectedValue;

            if (string.IsNullOrEmpty(title))
            {
                pnlError.Visible = true;
                litError.Text = "Please enter a title for the guide.";
                LoadDocs(txtSearch.Text);
                return;
            }

            if (fuPdf == null || !fuPdf.HasFile)
            {
                pnlError.Visible = true;
                litError.Text = "Please choose a PDF file to upload.";
                LoadDocs(txtSearch.Text);
                return;
            }

            var file = fuPdf.PostedFile;

            if (file.ContentLength > 15 * 1024 * 1024)
            {
                pnlError.Visible = true;
                litError.Text = "The PDF must be 15 MB or smaller.";
                LoadDocs(txtSearch.Text);
                return;
            }

            // cheks the magic bytes so only real pdfs get through...
            byte[] header = new byte[4];
            file.InputStream.Position = 0;
            int bytesRead = file.InputStream.Read(header, 0, 4);
            file.InputStream.Position = 0;

            bool isPdf = bytesRead == 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46; // %PDF
            string ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();

            if (!isPdf || ext != ".pdf")
            {
                pnlError.Visible = true;
                litError.Text = "Only PDF files are allowed for help guides.";
                LoadDocs(txtSearch.Text);
                return;
            }

            try
            {
                string helpDir = Server.MapPath("~/App_Data/help");
                if (!Directory.Exists(helpDir))
                {
                    Directory.CreateDirectory(helpDir);
                }

                // saves with a random name so file names never clash,,,
                string storageKey = Guid.NewGuid().ToString("N") + ".pdf";
                string filePath = Path.Combine(helpDir, storageKey);
                file.SaveAs(filePath);

                Guid me = Guid.Parse(Auth.CurrentUserId);
                const string sql = @"
                    INSERT INTO help_documents (title, description, category, storage_key, original_name, file_size, uploaded_by)
                    VALUES (@title, @desc, @category, @key, @orig, @size, @me);";

                Db.ExecuteNonQuery(sql,
                    Db.Param("@title", title),
                    Db.Param("@desc", string.IsNullOrEmpty(desc) ? (object)DBNull.Value : desc),
                    Db.Param("@category", category),
                    Db.Param("@key", storageKey),
                    Db.Param("@orig", Path.GetFileName(file.FileName)),
                    Db.Param("@size", (long)file.ContentLength),
                    Db.Param("@me", me));

                pnlSuccess.Visible = true;
                litSuccess.Text = $"Help guide \"{Server.HtmlEncode(title)}\" uploaded successfully. All users can now see it.";
                txtTitle.Text = "";
                txtDesc.Text = "";
                txtSearch.Text = "";
                LoadDocs("");
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to upload the guide: " + Server.HtmlEncode(ex.Message);
                LoadDocs(txtSearch.Text);
            }
        }

        // handles the delete button inside the repeater cards,,,
        protected void rptDocs_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName != "del") return;

            if (!IsSupervisor)
            {
                pnlError.Visible = true;
                litError.Text = "Only supervisors may delete help guides.";
                return;
            }

            if (!Guid.TryParse((e.CommandArgument ?? "").ToString(), out Guid docId))
            {
                return;
            }

            try
            {
                // gets the file name first so we can clean the disk too...
                const string sqlGet = "SELECT storage_key FROM help_documents WHERE doc_id = @id;";
                var dt = Db.ExecuteDataTable(sqlGet, Db.Param("@id", docId));

                const string sqlDel = "DELETE FROM help_documents WHERE doc_id = @id;";
                Db.ExecuteNonQuery(sqlDel, Db.Param("@id", docId));

                if (dt.Rows.Count > 0)
                {
                    string helpDir = Server.MapPath("~/App_Data/help");
                    string filePath = Path.Combine(helpDir, Path.GetFileName(dt.Rows[0]["storage_key"].ToString()));
                    if (File.Exists(filePath))
                    {
                        try { File.Delete(filePath); } catch { }
                    }
                }

                pnlSuccess.Visible = true;
                litSuccess.Text = "Help guide deleted.";
                LoadDocs(txtSearch.Text);
            }
            catch (Exception ex)
            {
                pnlError.Visible = true;
                litError.Text = "Failed to delete the guide: " + Server.HtmlEncode(ex.Message);
                LoadDocs(txtSearch.Text);
            }
        }

        // turns category codes into friendly labels for the chips,,,
        protected string CategoryLabel(object categoryObj)
        {
            string category = (categoryObj ?? "").ToString();
            switch (category)
            {
                case "getting_started": return "Getting Started";
                case "reporting": return "Reporting";
                case "sos": return "SOS";
                case "safe_route": return "Safe Route";
                case "notifications": return "Notifications";
                case "security": return "Security Ops";
                case "admin": return "Admin";
                default: return "General";
            }
        }

        // makes byte counts readable like 240 KB...
        protected string FormatSize(object sizeObj)
        {
            if (sizeObj == null || sizeObj == DBNull.Value) return "PDF";
            long bytes = Convert.ToInt64(sizeObj);
            if (bytes >= 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("F0") + " KB";
            return bytes + " B";
        }
    }
}
