<%@ Page Title="Notifications - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Notifications.aspx.cs" Inherits="CampusSafeTwin.Notifications" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .notif-item {
            background: #ffffff;
            border: 1px solid #e1d8eb;
            border-left: 5px solid #bbb;
            border-radius: 6px;
            padding: 14px 18px;
            margin-bottom: 12px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .notif-item.unread {
            border-left-color: var(--nwu-purple);
            background: #fbf9fd;
            font-weight: 500;
        }
        .notif-item.sos {
            border-left-color: #dc3545;
            background: #fff5f5;
        }
        .notif-time {
            font-size: 0.82rem;
            color: #777;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:18px;">
        <div>
            <h1>My Notifications</h1>
            <p class="subtitle" style="margin-bottom:0;">Recent alerts and status updates for your account</p>
        </div>
        <div>
            <asp:Button ID="btnMarkAllRead" runat="server" Text="Mark All as Read" CssClass="btn btn-secondary btn-sm" OnClick="btnMarkAllRead_Click" />
        </div>
    </div>

    <asp:Repeater ID="rptNotifications" runat="server" OnItemCommand="rptNotifications_ItemCommand">
        <ItemTemplate>
            <div class="notif-item <%# !Convert.ToBoolean(Eval("is_read")) ? "unread" : "" %> <%# Eval("kind").ToString() == "sos" ? "sos" : "" %>">
                <div>
                    <div style="display:flex; align-items:center; gap:8px;">
                        <span class="status-badge" style="background:#512D6D; font-size:0.75rem;">
                            <%# Eval("kind").ToString().ToUpper() %>
                        </span>
                        <strong style="color:#222; font-size:1.02rem;"><%# Eval("title") %></strong>
                        <%# !Convert.ToBoolean(Eval("is_read")) ? "<span style='color:#dc3545; font-size:0.75rem; font-weight:700;'>NEW</span>" : "" %>
                    </div>
                    <p style="margin:4px 0 0 0; color:#555; font-size:0.92rem;">
                        <%# Eval("body") != DBNull.Value ? Server.HtmlEncode(Eval("body").ToString()) : "" %>
                    </p>
                    <div class="notif-time">
                        Received: <%# Eval("created_fmt") %>
                    </div>
                </div>
                <div style="display:flex; gap:8px; align-items:center;">
                    <asp:LinkButton ID="btnRead" runat="server" Text="Mark Read" CommandName="MarkRead" CommandArgument='<%# Eval("notification_id") %>'
                        CssClass="btn btn-secondary btn-sm" Visible='<%# !Convert.ToBoolean(Eval("is_read")) %>' />

                    <%# Eval("incident_id") != DBNull.Value ? "<a href='" + ResolveUrl("~/Security/Incident.aspx?id=") + Eval("incident_id") + "' class='btn btn-primary btn-sm'>View &rarr;</a>" : "" %>
                </div>
            </div>
        </ItemTemplate>
    </asp:Repeater>

    <asp:Panel ID="pnlNoNotifs" runat="server" Visible="false" style="padding:40px; text-align:center; color:#777;">
        <p>You have no notifications at this time.</p>
    </asp:Panel>
</asp:Content>
