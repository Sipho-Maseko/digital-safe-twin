<%@ Page Title="Help Centre - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Help.aspx.cs" Inherits="CampusSafeTwin.Help" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .help-toolbar {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }
        .help-toolbar .form-control {
            flex: 1;
            min-width: 220px;
        }
        .help-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 16px;
            margin-top: 16px;
        }
        .help-card {
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 16px;
            background: #fff;
            display: flex;
            flex-direction: column;
            gap: 8px;
            transition: box-shadow 0.15s, transform 0.1s;
        }
        .help-card:hover {
            box-shadow: 0 4px 12px rgba(81, 45, 109, 0.12);
            transform: translateY(-1px);
        }
        .help-card-top {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 8px;
        }
        .help-pdf-icon {
            font-size: 1.6rem;
        }
        .help-chip {
            display: inline-block;
            padding: 2px 10px;
            border-radius: 12px;
            font-size: 0.72rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.4px;
            background: #ede6f5;
            color: var(--nwu-purple);
        }
        .help-card h3 {
            margin: 0;
            font-size: 1.05rem;
        }
        .help-card .help-desc {
            color: #555;
            font-size: 0.88rem;
            flex: 1;
        }
        .help-card .help-meta {
            color: #999;
            font-size: 0.78rem;
        }
        .help-card-actions {
            display: flex;
            gap: 8px;
            align-items: center;
        }
        .help-upload-card {
            margin-top: 28px;
            border: 1px dashed var(--nwu-purple);
            border-radius: 8px;
            padding: 20px;
            background: #faf8fc;
        }
        .help-upload-row {
            display: grid;
            grid-template-columns: 2fr 1fr;
            gap: 16px;
        }
        @media (max-width: 760px) {
            .help-upload-row {
                grid-template-columns: 1fr;
            }
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div>
        <h1>&#128218; Help Centre</h1>
        <p class="subtitle">Not sure how something works? Search the user guides below and open the PDF that covers the part of the system you need help with.</p>
    </div>

    <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litSuccess" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <!-- search bar for finding guides,,, -->
    <asp:Panel ID="pnlSearch" runat="server" DefaultButton="btnSearch" CssClass="help-toolbar">
        <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" placeholder="Search guides... e.g. report, SOS, safe route, notifications" />
        <asp:Button ID="btnSearch" runat="server" Text="Search" CssClass="btn btn-primary" OnClick="btnSearch_Click" />
        <asp:Button ID="btnClear" runat="server" Text="Show All" CssClass="btn btn-secondary" OnClick="btnClear_Click" />
    </asp:Panel>

    <div style="color:#666; font-size:0.9rem;">
        <asp:Literal ID="litResultCount" runat="server" />
    </div>

    <!-- list of help guide pdf cards... -->
    <div class="help-grid">
        <asp:Repeater ID="rptDocs" runat="server" OnItemCommand="rptDocs_ItemCommand">
            <ItemTemplate>
                <div class="help-card">
                    <div class="help-card-top">
                        <span class="help-pdf-icon">&#128196;</span>
                        <span class="help-chip"><%# CategoryLabel(Eval("category")) %></span>
                    </div>
                    <h3><%# Server.HtmlEncode(Eval("title").ToString()) %></h3>
                    <div class="help-desc"><%# Server.HtmlEncode((Eval("description") ?? "").ToString()) %></div>
                    <div class="help-meta">PDF &bull; <%# FormatSize(Eval("file_size")) %> &bull; Added <%# Eval("created_at", "{0:dd MMM yyyy}") %></div>
                    <div class="help-card-actions">
                        <a href='<%# ResolveUrl("~/Handlers/HelpDoc.ashx") + "?id=" + Eval("doc_id") %>' target="_blank" class="btn btn-primary btn-sm">Open Guide</a>
                        <asp:LinkButton ID="btnDeleteDoc" runat="server" Visible='<%# IsSupervisor %>'
                            CommandName="del" CommandArgument='<%# Eval("doc_id") %>'
                            Text="Delete" CssClass="btn btn-danger btn-sm"
                            OnClientClick="return confirm('Delete this help guide? Students will no longer see it.');" />
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>
    </div>

    <asp:Panel ID="pnlEmpty" runat="server" Visible="false" style="text-align:center; padding:40px 20px; color:#888;">
        <div style="font-size:2.2rem;">&#128269;</div>
        <p>No guides matched your search. Try a different word like <em>"report"</em>, <em>"SOS"</em> or <em>"route"</em> &mdash; or click <strong>Show All</strong>.</p>
    </asp:Panel>

    <!-- admin only section for uploading new guides,,, -->
    <asp:Panel ID="pnlAdmin" runat="server" Visible="false" CssClass="help-upload-card">
        <h3 style="margin-bottom:4px;">&#128228; Upload a New Help Guide (Admin)</h3>
        <p class="subtitle" style="margin-bottom:14px;">Upload a PDF explaining how to use a part of the system. It becomes visible to all users immediately.</p>

        <div class="help-upload-row">
            <div>
                <div class="form-group">
                    <label for="txtTitle">Guide Title:</label>
                    <asp:TextBox ID="txtTitle" runat="server" CssClass="form-control" placeholder="e.g. How to Report an Incident" />
                </div>
                <div class="form-group">
                    <label for="txtDesc">Short Description (what will the user learn?):</label>
                    <asp:TextBox ID="txtDesc" runat="server" CssClass="form-control" placeholder="e.g. Step by step guide to submitting an incident report with photos" />
                </div>
            </div>
            <div>
                <div class="form-group">
                    <label for="ddlCategory">Category:</label>
                    <asp:DropDownList ID="ddlCategory" runat="server" CssClass="form-control">
                        <asp:ListItem Value="getting_started" Text="Getting Started" />
                        <asp:ListItem Value="reporting" Text="Reporting Incidents" />
                        <asp:ListItem Value="sos" Text="SOS &amp; Emergencies" />
                        <asp:ListItem Value="safe_route" Text="Safe Route" />
                        <asp:ListItem Value="notifications" Text="Reports &amp; Notifications" />
                        <asp:ListItem Value="security" Text="Security Operations" />
                        <asp:ListItem Value="admin" Text="Administration" />
                        <asp:ListItem Value="general" Text="General" />
                    </asp:DropDownList>
                </div>
                <div class="form-group">
                    <label for="fuPdf">PDF File (max 15 MB):</label>
                    <asp:FileUpload ID="fuPdf" runat="server" accept="application/pdf" CssClass="form-control" />
                </div>
            </div>
        </div>

        <asp:Button ID="btnUpload" runat="server" Text="Upload Help Guide" CssClass="btn btn-primary" OnClick="btnUpload_Click" />
    </asp:Panel>
</asp:Content>
