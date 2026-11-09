<%@ Page Title="Campus Hotspots - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Hotspots.aspx.cs" Inherits="CampusSafeTwin.Admin.Hotspots" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .hotspot-info {
            display: flex;
            gap: 20px;
            margin-bottom: 20px;
        }
        .stat-card {
            background: #faf8fc;
            padding: 16px 20px;
            border-radius: 6px;
            border-left: 4px solid var(--nwu-purple);
            flex: 1;
        }
        .stat-val {
            font-size: 1.6rem;
            font-weight: 700;
            color: var(--nwu-purple);
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Campus Incident Hotspots & Patrol Intelligence</h1>
    <p class="subtitle">Heat map and areas with frequent incident reports across campus</p>

    <div class="hotspot-info">
        <div class="stat-card">
            <div class="stat-val"><asp:Literal ID="litActiveHotspotsCount" runat="server" Text="0" /></div>
            <div style="color:#666; font-size:0.9rem;">Active Hotspot Clusters (&ge; 2 incidents)</div>
        </div>
        <div class="stat-card">
            <div class="stat-val"><asp:Literal ID="litTopAreaName" runat="server" Text="None" /></div>
            <div style="color:#666; font-size:0.9rem;">Primary Priority Patrol Zone</div>
        </div>
    </div>

    <!-- heat map showing frequent incident areas,,, -->
    <div style="margin-bottom: 30px;">
        <div id="hotspotMap" class="map-frame" style="min-height: 520px;"></div>
        <div style="margin-top:8px; font-size:0.85rem; color:#666; display:flex; justify-content:space-between;">
            <span><strong style="color:#b30000;">Red Rectangles:</strong> Hotspot cells (count &ge; 2)</span>
            <span><strong>Heat Layer:</strong> Recency-weighted density (14-day half-life decay)</span>
        </div>
    </div>

    <!-- list of top areas to patrol... -->
    <h2>Top Areas to Patrol</h2>
    <p class="subtitle">Prioritized campus zones ranked by incident density and mapped to nearest landmarks</p>

    <asp:GridView ID="gvTopAreas" runat="server" AutoGenerateColumns="False" CssClass="grid-view">
        <Columns>
            <asp:BoundField DataField="nearest_location" HeaderText="Nearest Landmark / Area" />
            <asp:BoundField DataField="incident_count" HeaderText="Total Incidents" />
            <asp:BoundField DataField="categories_str" HeaderText="Reported Categories" />
            <asp:TemplateField HeaderText="Coordinates">
                <ItemTemplate>
                    <%# string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4}, {1:F4}", Eval("cell_lat"), Eval("cell_lng")) %>
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>

    <asp:Panel ID="pnlNoHotspots" runat="server" Visible="false" style="padding:24px; text-align:center; color:#666;">
        <p>No high-density hotspots (&ge; 2 incidents) currently identified on campus.</p>
    </asp:Panel>

    <script>
        document.addEventListener("DOMContentLoaded", function () {
            CampusMap.init('hotspotMap', 'hotspots');
        });
    </script>
</asp:Content>
