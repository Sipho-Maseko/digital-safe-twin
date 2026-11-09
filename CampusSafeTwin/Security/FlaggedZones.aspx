<%@ Page Title="Flagged Safety Zones - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="FlaggedZones.aspx.cs" Inherits="CampusSafeTwin.Security.FlaggedZones" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .zone-layout {
            display: grid;
            grid-template-columns: 360px 1fr;
            gap: 24px;
        }
        @media (max-width: 900px) {
            .zone-layout { grid-template-columns: 1fr; }
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Flagged Safety Zones</h1>
    <p class="subtitle">Designate hazard areas and general campus risk perimeters that safe walking routes must actively avoid</p>

    <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litSuccess" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <div class="zone-layout">
        <div>
            <div style="background:#faf8fc; padding:20px; border-radius:8px; border:1px solid #e1d8eb;">
                <h3>Add New Flagged Zone</h3>
                <p style="font-size:0.85rem; color:#666; margin-bottom:14px;">Click the map to place the zone center.</p>

                <div class="form-group">
                    <label for="txtName">Zone Name</label>
                    <asp:TextBox ID="txtName" runat="server" CssClass="form-control" placeholder="e.g. Construction Area / Poor Lighting" required="required" />
                </div>

                <div class="form-group">
                    <label for="ddlRiskLevel">Risk Severity (1 = Low, 5 = Critical)</label>
                    <asp:DropDownList ID="ddlRiskLevel" runat="server" CssClass="form-control">
                        <asp:ListItem Value="1" Text="Level 1 - Minor Hazard" />
                        <asp:ListItem Value="2" Text="Level 2 - Caution Advised" />
                        <asp:ListItem Value="3" Text="Level 3 - Moderate Risk" Selected="True" />
                        <asp:ListItem Value="4" Text="Level 4 - High Risk Zone" />
                        <asp:ListItem Value="5" Text="Level 5 - Extreme Danger" />
                    </asp:DropDownList>
                </div>

                <div class="form-group">
                    <label for="txtRadius">Radius (Metres)</label>
                    <asp:TextBox ID="txtRadius" runat="server" CssClass="form-control" TextMode="Number" Text="50" min="15" max="300" required="required" />
                </div>

                <div class="form-group">
                    <label for="txtReason">Reason / Notes</label>
                    <asp:TextBox ID="txtReason" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2" placeholder="e.g. Unlit path, reported muggings..." />
                </div>

                <div class="form-group">
                    <label>Center Coordinates</label>
                    <div style="font-size:0.88rem; color:#555; background:#fff; padding:6px 10px; border:1px solid #ddd; border-radius:4px;">
                        <span id="lblZoneCoords">Click map to set location</span>
                    </div>
                    <asp:HiddenField ID="hfZoneLat" runat="server" ClientIDMode="Static" />
                    <asp:HiddenField ID="hfZoneLng" runat="server" ClientIDMode="Static" />
                </div>

                <asp:Button ID="btnSaveZone" runat="server" Text="Create Flagged Zone" CssClass="btn btn-primary" Width="100%" OnClick="btnSaveZone_Click" />
            </div>

            <!-- table showing danger zones,,, -->
            <div style="margin-top:20px;">
                <h3>Active Zones</h3>
                <asp:GridView ID="gvZones" runat="server" AutoGenerateColumns="False" CssClass="grid-view" OnRowCommand="gvZones_RowCommand">
                    <Columns>
                        <asp:BoundField DataField="name" HeaderText="Zone" />
                        <asp:BoundField DataField="risk_level" HeaderText="Risk" />
                        <asp:BoundField DataField="radius_m" HeaderText="Radius" />
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:LinkButton ID="btnDeactivate" runat="server" Text="Remove" CommandName="Deactivate" CommandArgument='<%# Eval("zone_id") %>'
                                    CssClass="btn btn-danger btn-sm" OnClientClick="return confirm('Remove this flagged zone?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

        <div>
            <div id="flaggedMap" class="map-frame" style="min-height: 560px;"></div>
        </div>
    </div>

    <script>
        document.addEventListener("DOMContentLoaded", function () {
            // shared satelite campus map opens framed on nwu potch,,,
            var map = CampusMap.createBaseMap('flaggedMap');

            var clickMarker = null;
            var clickCircle = null;

            // clicking on map places red zone circle,,
            map.on('click', function (e) {
                var lat = e.latlng.lat;
                var lng = e.latlng.lng;
                var radInput = document.getElementById('<%= txtRadius.ClientID %>');
                var radius = radInput ? parseInt(radInput.value, 10) : 50;
                if (isNaN(radius)) radius = 50;

                if (clickMarker) map.removeLayer(clickMarker);
                if (clickCircle) map.removeLayer(clickCircle);

                clickMarker = L.marker([lat, lng]).addTo(map);
                clickCircle = L.circle([lat, lng], { radius: radius, color: '#dc3545', fillColor: '#dc3545', fillOpacity: 0.25 }).addTo(map);

                document.getElementById('hfZoneLat').value = lat.toFixed(6);
                document.getElementById('hfZoneLng').value = lng.toFixed(6);
                document.getElementById('lblZoneCoords').textContent = lat.toFixed(5) + ", " + lng.toFixed(5);
            });

            // loads the danger zones from the backend api...
            fetch('<%= ResolveUrl("~/Handlers/Api.ashx?action=flaggedZones") %>')
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data || !data.zones) return;
                    data.zones.forEach(function(z) {
                        var c = L.circle([z.latitude, z.longitude], {
                            radius: z.radius_m,
                            color: '#b30000',
                            fillColor: '#ff4444',
                            fillOpacity: 0.3
                        }).addTo(map);
                        c.bindPopup("<strong>" + CampusMap.escapeHtml(z.name) + "</strong><br/>Risk Level: " + z.risk_level + "<br/>" + CampusMap.escapeHtml(z.reason || ''));
                    });
                });
        });
    </script>
</asp:Content>
