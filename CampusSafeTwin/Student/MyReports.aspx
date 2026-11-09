<%@ Page Title="My Reports - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyReports.aspx.cs" Inherits="CampusSafeTwin.Student.MyReports" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .timeline-box {
            margin-top: 30px;
            padding: 20px;
            background: #faf8fc;
            border-radius: 8px;
            border: 1px solid #e1d8eb;
        }
        .empty-state {
            padding: 40px;
            text-align: center;
            color: #777;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <h1>My Incident Reports</h1>
    <p class="subtitle">View your reported campus incidents and track live status updates</p>

    <asp:GridView ID="gvMyReports" runat="server" AutoGenerateColumns="False" CssClass="grid-view" 
        DataKeyNames="incident_id" OnRowCommand="gvMyReports_RowCommand" OnRowDataBound="gvMyReports_RowDataBound">
        <Columns>
            <asp:BoundField DataField="ref_number" HeaderText="Reference" />
            <asp:BoundField DataField="occurred_fmt" HeaderText="Occurred" />
            <asp:BoundField DataField="category" HeaderText="Category" />
            <asp:TemplateField HeaderText="Status">
                <ItemTemplate>
                    <span class="status-badge status-<%# Eval("status").ToString().ToLower().Replace(" ", "") %>">
                        <%# Eval("status") %>
                    </span>
                </ItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="Action">
                <ItemTemplate>
                    <asp:LinkButton ID="btnViewTimeline" runat="server" CommandName="ViewTimeline" 
                        CommandArgument='<%# Eval("incident_id") %>' Text="View Timeline &rarr;" />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>

    <asp:Panel ID="pnlNoReports" runat="server" Visible="false" CssClass="empty-state">
        <p>You have not submitted any incident reports yet.</p>
        <a href="<%= ResolveUrl("~/Student/Report.aspx") %>" class="btn btn-primary" style="margin-top:12px;">Submit a Report</a>
    </asp:Panel>

    <!-- timeline of updates for chosen report... -->
    <asp:Panel ID="pnlTimeline" runat="server" Visible="false" CssClass="timeline-box">
        <h2>Report Timeline: <asp:Literal ID="litSelectedRef" runat="server" /></h2>
        <p class="subtitle" style="margin-bottom: 16px;">
            <strong>Category:</strong> <asp:Literal ID="litSelectedCat" runat="server" /> &bull; 
            <strong>Reported:</strong> <asp:Literal ID="litSelectedReported" runat="server" /> &bull;
            <strong>Description:</strong> <asp:Literal ID="litSelectedDesc" runat="server" />
        </p>

        <div class="timeline">
            <asp:Repeater ID="rptTimeline" runat="server">
                <ItemTemplate>
                    <div class="timeline-item">
                        <div class="timeline-dot"></div>
                        <div class="timeline-content">
                            <div class="timeline-header">
                                <span class="timeline-status">
                                    <%# Eval("old_status") != DBNull.Value && !string.IsNullOrEmpty(Eval("old_status").ToString()) ? Eval("old_status") + " &rarr; " : "" %>
                                    <%# Eval("new_status") %>
                                </span>
                                <span><%# Eval("changed_fmt") %></span>
                            </div>
                            <div>
                                <small style="color:#555;">Updated by: <strong><%# Eval("changed_by_name") %></strong></small>
                            </div>
                            <%# Eval("note") != DBNull.Value && !string.IsNullOrEmpty(Eval("note").ToString()) ? "<p style='margin-top:6px; font-size:13px; color:#444;'><em>\"" + Server.HtmlEncode(Eval("note").ToString()) + "\"</em></p>" : "" %>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>
    </asp:Panel>
</asp:Content>
