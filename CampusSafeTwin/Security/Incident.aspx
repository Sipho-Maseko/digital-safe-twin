<%@ Page Title="Manage Incident - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Incident.aspx.cs" Inherits="CampusSafeTwin.Security.Incident" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .incident-layout {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 24px;
        }
        @media (max-width: 860px) {
            .incident-layout {
                grid-template-columns: 1fr;
            }
        }
        .detail-card {
            background: #faf8fc;
            padding: 20px;
            border-radius: 8px;
            border: 1px solid #e1d8eb;
            margin-bottom: 20px;
        }
        .action-card {
            background: #ffffff;
            padding: 20px;
            border-radius: 8px;
            border: 2px solid var(--nwu-purple);
            margin-bottom: 20px;
        }
        .detail-row {
            display: flex;
            justify-content: space-between;
            padding: 8px 0;
            border-bottom: 1px solid #ebe5f2;
        }
        .detail-label {
            font-weight: 600;
            color: #555;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
        <div>
            <h1>Incident Details: <asp:Literal ID="litRefNumber" runat="server" /></h1>
            <p class="subtitle">Reference: <strong><asp:Literal ID="litId" runat="server" /></strong></p>
        </div>
        <div>
            <a href="Map.aspx" class="btn btn-secondary">&larr; Back to Security Map</a>
        </div>
    </div>

    <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litSuccess" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlInvalidBanner" runat="server" Visible="false" CssClass="alert alert-danger">
        <strong>Flagged Bogus/Invalid:</strong> This incident has been marked as invalid by a supervisor and is excluded from live patrol maps.
    </asp:Panel>

    <div class="incident-layout">
        <!-- details and actions column,,, -->
        <div>
            <div class="detail-card">
                <h3>Incident Information</h3>
                <div class="detail-row">
                    <span class="detail-label">Status</span>
                    <span>
                        <span id="badgeCurrentStatus" runat="server" class="status-badge">
                            <asp:Literal ID="litStatus" runat="server" />
                        </span>
                    </span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Category</span>
                    <span><asp:Literal ID="litCategory" runat="server" /></span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Occurred</span>
                    <span><asp:Literal ID="litOccurred" runat="server" /></span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Reported</span>
                    <span><asp:Literal ID="litReported" runat="server" /></span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Reporter</span>
                    <span><asp:Literal ID="litReporter" runat="server" /></span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Coordinates</span>
                    <span><asp:Literal ID="litCoords" runat="server" /></span>
                </div>
                <div style="margin-top: 14px;">
                    <span class="detail-label">Description:</span>
                    <p style="margin-top:6px; background:#fff; padding:10px; border-radius:4px; border:1px solid #ddd;">
                        <asp:Literal ID="litDescription" runat="server" />
                    </p>
                </div>
            </div>

            <!-- evidence photos and notes... -->
            <div class="detail-card">
                <h3>Evidence Photos & Notes</h3>
                <asp:Repeater ID="rptAttachments" runat="server">
                    <HeaderTemplate><div style="display:flex; flex-wrap:wrap; gap:12px; margin-bottom:12px;"></HeaderTemplate>
                    <ItemTemplate>
                        <div style="border:1px solid #ddd; border-radius:6px; padding:6px; background:#fff; text-align:center; box-shadow:0 1px 3px rgba(0,0,0,0.08);">
                            <a href='<%# ResolveUrl("~/Handlers/Attachment.ashx?id=" + Eval("attachment_id")) %>' target="_blank">
                                <img src='<%# ResolveUrl("~/Handlers/Attachment.ashx?id=" + Eval("attachment_id")) %>' 
                                     alt="Evidence" style="width:140px; height:100px; object-fit:cover; border-radius:4px; display:block;" />
                            </a>
                            <small style="display:block; font-size:11px; margin-top:4px; color:#555; max-width:140px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;">
                                <%# Server.HtmlEncode(Eval("note") != DBNull.Value ? Eval("note").ToString() : "Photo Evidence") %>
                            </small>
                        </div>
                    </ItemTemplate>
                    <FooterTemplate></div></FooterTemplate>
                </asp:Repeater>

                <asp:Panel ID="pnlNoPhotos" runat="server" Visible="false">
                    <p style="font-size:0.88rem; color:#777; font-style:italic; margin-bottom:12px;">No photo evidence attached.</p>
                </asp:Panel>

                <!-- attached notes list,, -->
                <asp:Repeater ID="rptNotes" runat="server">
                    <HeaderTemplate><div style="margin-bottom:12px;"><strong>Staff Notes:</strong></HeaderTemplate>
                    <ItemTemplate>
                        <div style="background:#fff; border-left:3px solid var(--nwu-purple); padding:6px 10px; margin-top:6px; font-size:0.88rem;">
                            <%# Server.HtmlEncode(Eval("note").ToString()) %>
                            <span style="font-size:0.75rem; color:#888; display:block;"><%# Eval("uploaded_fmt") %></span>
                        </div>
                    </ItemTemplate>
                    <FooterTemplate></div></FooterTemplate>
                </asp:Repeater>

                <div style="margin-top:12px;">
                    <label for="txtNewNote" style="font-size:0.88rem; font-weight:600;">Add Investigation Note:</label>
                    <div style="display:flex; gap:8px; margin-top:4px;">
                        <asp:TextBox ID="txtNewNote" runat="server" CssClass="form-control" placeholder="Add note or observation..." />
                        <asp:Button ID="btnAddNote" runat="server" Text="Add Note" CssClass="btn btn-secondary btn-sm" OnClick="btnAddNote_Click" />
                    </div>
                </div>
            </div>

            <!-- next status action box... -->
            <asp:Panel ID="pnlActionBox" runat="server" CssClass="action-card">
                <h3>Lifecycle Action</h3>
                <p style="font-size:0.92rem; color:#555; margin-bottom:12px;">
                    Move incident to next status: <strong><asp:Literal ID="litNextStatusName" runat="server" /></strong>
                </p>

                <div class="form-group">
                    <label for="txtStatusNote">Action Note (Optional):</label>
                    <asp:TextBox ID="txtStatusNote" runat="server" CssClass="form-control" placeholder="e.g. Dispatched officer or resolved on scene..." />
                </div>

                <asp:Button ID="btnAdvanceStatus" runat="server" Text="Update Status" CssClass="btn btn-primary" Width="100%" OnClick="btnAdvanceStatus_Click" />
            </asp:Panel>

            <asp:Panel ID="pnlResolvedNotice" runat="server" Visible="false" CssClass="alert alert-success">
                <strong>Incident Resolved:</strong> This incident is fully resolved and closed.
            </asp:Panel>

            <!-- supervisor guard dispatch and mark invalid,,, -->
            <asp:Panel ID="pnlSupervisorControls" runat="server" Visible="false" style="margin-top: 14px; padding:16px; background:#fbf8ff; border:1px solid #d4c4ea; border-radius:6px;">
                <h4 style="color:var(--nwu-purple); margin-bottom:8px;">Officer Dispatch Control</h4>
                
                <div style="margin-bottom:12px; font-size:0.9rem;">
                    Current Dispatch: <strong><asp:Literal ID="litDispatchInfo" runat="server" Text="None" /></strong>
                </div>

                <div class="form-group" style="margin-bottom:10px;">
                    <label for="ddlGuards">Assign Officer (Sorted by Proximity):</label>
                    <asp:DropDownList ID="ddlGuards" runat="server" CssClass="form-control" />
                </div>

                <div style="display:flex; gap:8px; margin-bottom:16px;">
                    <asp:Button ID="btnAssignGuard" runat="server" Text="Assign Selected" CssClass="btn btn-primary btn-sm" OnClick="btnAssignGuard_Click" />
                    <asp:Button ID="btnAutoNearest" runat="server" Text="Auto Nearest" CssClass="btn btn-secondary btn-sm" OnClick="btnAutoNearest_Click" />
                </div>

                <hr style="border:0; border-top:1px solid #ebe5f2; margin:12px 0;" />
                <h4 style="color:#842029; margin-bottom:6px;">Flag Bogus / Invalid</h4>
                <p style="font-size:0.85rem; color:#666; margin-bottom:10px;">Mark false alarms as invalid to filter them out of security heat maps.</p>
                <asp:Button ID="btnMarkInvalid" runat="server" Text="Mark Bogus / Invalid" CssClass="btn btn-danger btn-sm" OnClick="btnMarkInvalid_Click" OnClientClick="return confirm('Are you sure you want to mark this incident as invalid?');" />
            </asp:Panel>
        </div>

        <!-- location map and timeline column... -->
        <div>
            <h3>Incident Location Pin</h3>
            <div id="incidentMiniMap" class="map-frame map-frame-sm" style="margin-bottom: 24px;"></div>

            <h3>Status History Timeline</h3>
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
        </div>
    </div>

    <script>
        document.addEventListener("DOMContentLoaded", function () {
            var lat = <%= IncidentLat %>;
            var lng = <%= IncidentLng %>;
            if (!isNaN(lat) && !isNaN(lng)) {
                // puts pin on satelite map centered on the incident...
                var map = CampusMap.createBaseMap('incidentMiniMap', { center: [lat, lng], zoom: 17, fitCampus: false });

                L.marker([lat, lng]).addTo(map)
                    .bindPopup("<strong>Incident Location</strong><br/><%= Server.HtmlEncode(CategoryName) %>")
                    .openPopup();
            }
        });
    </script>
</asp:Content>
