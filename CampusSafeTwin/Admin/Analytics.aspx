<%@ Page Title="Campus Safety Analytics - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Analytics.aspx.cs" Inherits="CampusSafeTwin.Admin.Analytics" EnableEventValidation="false" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .kpi-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 16px;
            margin-bottom: 24px;
        }
        .kpi-card {
            background: #fff;
            padding: 16px 20px;
            border-radius: 8px;
            border: 1px solid #e1d8eb;
            box-shadow: 0 2px 4px rgba(0,0,0,0.04);
        }
        .kpi-value {
            font-size: 2rem;
            font-weight: 700;
            color: var(--nwu-purple);
            margin-top: 4px;
        }
        .kpi-title {
            font-size: 0.85rem;
            color: #666;
            text-transform: uppercase;
            font-weight: 600;
        }
        .analytics-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 24px;
            margin-bottom: 24px;
        }
        @media (max-width: 860px) {
            .analytics-grid {
                grid-template-columns: 1fr;
            }
        }
        .bar-container {
            margin-bottom: 12px;
        }
        .bar-header {
            display: flex;
            justify-content: space-between;
            font-size: 0.88rem;
            margin-bottom: 4px;
        }
        .bar-track {
            height: 12px;
            background: #eee;
            border-radius: 6px;
            overflow: hidden;
        }
        .bar-fill {
            height: 100%;
            background: var(--nwu-purple);
            border-radius: 6px;
            transition: width 0.3s ease;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
        <div>
            <h1>Campus Safety Intelligence & Analytics</h1>
            <p class="subtitle">Overview of safety metrics, incident types and officer response times</p>
        </div>
        <div>
            <asp:Button ID="btnExportCsv" runat="server" Text="Export All Incidents (CSV)" CssClass="btn btn-primary" OnClick="btnExportCsv_Click" />
        </div>
    </div>

    <!-- top stats cards,,, -->
    <div class="kpi-row">
        <div class="kpi-card">
            <div class="kpi-title">Total Logged Incidents</div>
            <div class="kpi-value"><asp:Literal ID="litTotalIncidents" runat="server" Text="0" /></div>
        </div>
        <div class="kpi-card" style="border-left: 4px solid #dc3545;">
            <div class="kpi-title">Emergency SOS Alerts</div>
            <div class="kpi-value" style="color:#dc3545;"><asp:Literal ID="litSosCount" runat="server" Text="0" /></div>
        </div>
        <div class="kpi-card" style="border-left: 4px solid #0d6efd;">
            <div class="kpi-title">Avg Response Time</div>
            <div class="kpi-value" style="color:#0d6efd;"><asp:Literal ID="litAvgResponseMin" runat="server" Text="-" /></div>
        </div>
        <div class="kpi-card" style="border-left: 4px solid #198754;">
            <div class="kpi-title">Avg Resolution Time</div>
            <div class="kpi-value" style="color:#198754;"><asp:Literal ID="litAvgResolveMin" runat="server" Text="-" /></div>
        </div>
    </div>

    <div class="analytics-grid">
        <!-- incidents grouped by category... -->
        <div class="card" style="padding:18px;">
            <h3 style="margin-bottom:16px; color:var(--nwu-purple);">Incident Category Distribution</h3>
            <asp:Repeater ID="rptCategories" runat="server">
                <ItemTemplate>
                    <div class="bar-container">
                        <div class="bar-header">
                            <span style="font-weight:600;"><%# Eval("category_name") %></span>
                            <span style="color:#666;"><%# Eval("count") %> incidents (<%# Eval("pct") %>%)</span>
                        </div>
                        <div class="bar-track">
                            <div class="bar-fill" style='width: <%# Eval("pct") %>%;'></div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>

        <!-- incident status breakdown,,, -->
        <div class="card" style="padding:18px;">
            <h3 style="margin-bottom:16px; color:var(--nwu-purple);">Incident Resolution Status</h3>
            <asp:Repeater ID="rptStatuses" runat="server">
                <ItemTemplate>
                    <div class="bar-container">
                        <div class="bar-header">
                            <span style="font-weight:600;"><%# Eval("status") %></span>
                            <span style="color:#666;"><%# Eval("count") %> incidents (<%# Eval("pct") %>%)</span>
                        </div>
                        <div class="bar-track">
                            <div class="bar-fill" style='width: <%# Eval("pct") %>%; background: <%# Eval("bar_color") %>;'></div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>
    </div>

    <!-- table of officer dispatches and response times... -->
    <div class="card" style="padding:18px;">
        <h3 style="margin-bottom:12px; color:var(--nwu-purple);">Officer Response Times & Dispatch History</h3>
        
        <asp:GridView ID="gvDispatches" runat="server" AutoGenerateColumns="False" CssClass="table" EmptyDataText="No dispatch logs recorded yet.">
            <Columns>
                <asp:BoundField DataField="dispatch_ref" HeaderText="Ref / Dispatch" />
                <asp:TemplateField HeaderText="Type">
                    <ItemTemplate>
                        <%# Convert.ToBoolean(Eval("is_emergency")) ? "<span style='color:#dc3545; font-weight:bold;'>[SOS EMERGENCY]</span> " : "" %>
                        <%# Eval("category") %>
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:BoundField DataField="guard_name" HeaderText="Responding Guard" NullDisplayText="(Auto/Unassigned)" />
                <asp:BoundField DataField="dispatch_status" HeaderText="Dispatch Status" />
                <asp:BoundField DataField="dispatched_fmt" HeaderText="Dispatched At" />
                <asp:TemplateField HeaderText="Time to Arrive">
                    <ItemTemplate>
                        <%# Eval("response_minutes") != DBNull.Value ? Eval("response_minutes") + " min" : "<span style='color:#999;'>Pending</span>" %>
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:TemplateField HeaderText="Time to Resolve">
                    <ItemTemplate>
                        <%# Eval("resolve_minutes") != DBNull.Value ? Eval("resolve_minutes") + " min" : "<span style='color:#999;'>In Progress</span>" %>
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
        </asp:GridView>
    </div>
</asp:Content>
