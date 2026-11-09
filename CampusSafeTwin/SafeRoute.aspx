<%@ Page Title="Safer Walking Route - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SafeRoute.aspx.cs" Inherits="CampusSafeTwin.SafeRoute" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .route-dashboard {
            margin-bottom: 20px;
        }
        .route-instructions {
            padding: 12px 16px;
            background: #faf8fc;
            border-left: 4px solid var(--nwu-purple);
            border-radius: 4px;
            margin-bottom: 16px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            flex-wrap: wrap;
            gap: 10px;
        }
        .route-legend {
            display: flex;
            gap: 20px;
            align-items: center;
            font-size: 0.9rem;
            margin-top: 10px;
        }
        .legend-line {
            display: inline-block;
            width: 28px;
            height: 4px;
            vertical-align: middle;
            margin-right: 6px;
        }
        .line-safer {
            background-color: var(--nwu-purple);
        }
        .line-shortest {
            background-color: #6c757d;
            border-top: 2px dashed #6c757d;
            height: 0;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Safer Campus Walking Route</h1>
    <p class="subtitle">Dynamic Dijkstra path routing incorporating historical incident proximity, lighting conditions, and active flagged zones</p>

    <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litSuccess" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlConditionNotice" runat="server" Visible="false">
        <asp:Literal ID="litConditionNotice" runat="server" />
    </asp:Panel>

    <!-- choose starting and destination spots,,, -->
    <div class="card" style="padding:16px; margin-bottom:16px; background:#fff; border:1px solid #ddd;">
        <h3 style="margin-bottom:12px; font-size:1.1rem; color:var(--nwu-purple);">Select Campus Endpoints</h3>
        <div style="display:flex; flex-wrap:wrap; gap:16px; align-items:flex-end;">
            <div style="flex:1; min-width:220px;">
                <label style="font-weight:600; font-size:0.9rem;">From (Start Location):</label>
                <asp:DropDownList ID="ddlFromLocation" runat="server" CssClass="form-control" ClientIDMode="Static" />
            </div>
            <div style="flex:1; min-width:220px;">
                <label style="font-weight:600; font-size:0.9rem;">To (Destination Location):</label>
                <asp:DropDownList ID="ddlToLocation" runat="server" CssClass="form-control" ClientIDMode="Static" />
            </div>
            <div>
                <button type="button" id="btnRouteDropdowns" class="btn btn-primary" onclick="routeFromDropdowns();">Find Safest Route</button>
                <button type="button" id="btnResetRoute" class="btn btn-secondary">&#8635; Clear Map</button>
            </div>
        </div>
        <small style="color:#666; display:block; margin-top:8px;">Or click anywhere on the campus map below to drop custom Start (A) and Destination (B) pins.</small>
    </div>

    <div class="route-instructions">
        <div>
            <span id="routeStatusText">Select campus locations above or click map to set <strong>Start point (A)</strong>.</span>
        </div>
    </div>

    <!-- comparison cards between safer and shortest... -->
    <div class="route-cards-container">
        <div class="route-card route-card-safer">
            <h3 style="color:var(--nwu-purple);">&#128737; Recommended Safer Route</h3>
            <p style="font-size:0.85rem; color:#666; margin-bottom:8px;">Minimizes proximity to recent incidents and avoids dark unlit pathways</p>
            <div style="display:flex; justify-content:space-between; margin-top:10px;">
                <div>
                    <span style="font-size:0.8rem; color:#777; display:block;">Distance</span>
                    <span id="saferDist" class="route-metric">-</span>
                </div>
                <div>
                    <span style="font-size:0.8rem; color:#777; display:block;">Est. Walking Time (1.3 m/s)</span>
                    <span id="saferTime" class="route-metric">-</span>
                </div>
            </div>
            <div id="saferRisk" style="margin-top:8px; font-size:0.85rem; color:#512D6D; font-weight:600;"></div>
        </div>

        <div class="route-card route-card-shortest">
            <h3 style="color:#6c757d;">Shortest Direct Route</h3>
            <p style="font-size:0.85rem; color:#666; margin-bottom:8px;">Direct geometric shortest path without risk weighting</p>
            <div style="display:flex; justify-content:space-between; margin-top:10px;">
                <div>
                    <span style="font-size:0.8rem; color:#777; display:block;">Distance</span>
                    <span id="shortestDist" class="route-metric">-</span>
                </div>
                <div>
                    <span style="font-size:0.8rem; color:#777; display:block;">Est. Walking Time (1.3 m/s)</span>
                    <span id="shortestTime" class="route-metric">-</span>
                </div>
            </div>
        </div>
    </div>

    <!-- save route box,, -->
    <asp:Panel ID="pnlSaveRoute" runat="server" CssClass="card" style="padding:14px; margin-bottom:16px; background:#fbf8ff; border:1px solid #d4c4ea;">
        <div style="display:flex; flex-wrap:wrap; gap:12px; align-items:flex-end;">
            <div style="flex:1; min-width:240px;">
                <label style="font-weight:600; font-size:0.9rem;">Save this route for quick access & condition alerts:</label>
                <asp:TextBox ID="txtRouteLabel" runat="server" CssClass="form-control" placeholder="e.g. Evening walk from Library to Residence" ClientIDMode="Static" />
            </div>
            <asp:HiddenField ID="hfNodePathJson" runat="server" ClientIDMode="Static" />
            <asp:HiddenField ID="hfFromLocId" runat="server" ClientIDMode="Static" />
            <asp:HiddenField ID="hfToLocId" runat="server" ClientIDMode="Static" />
            <asp:HiddenField ID="hfRouteLength" runat="server" ClientIDMode="Static" />
            <div>
                <asp:Button ID="btnSaveRoute" runat="server" Text="Save Route" CssClass="btn btn-primary" OnClick="btnSaveRoute_Click" />
            </div>
        </div>
    </asp:Panel>

    <!-- map view container... -->
    <div id="routeMap" class="map-frame" style="min-height: 520px;"></div>

    <div class="route-legend">
        <div><span class="legend-line line-safer"></span> <strong>Solid Purple:</strong> Safer Recommended Walking Route</div>
        <div><span class="legend-line line-shortest"></span> <strong>Dashed Grey:</strong> Shortest Route Along Walkways</div>
        <div><span class="legend-line" style="background:#dc3545; opacity:0.5;"></span> <strong>Red Circles:</strong> Flagged Unsafe Zones</div>
        <div><span class="legend-line" style="background:#ff9900; opacity:0.6;"></span> <strong>Orange Cells:</strong> Incident Hotspots</div>
    </div>

    <!-- saved routes list,,, -->
    <div class="card" style="padding:16px; margin-top:24px;">
        <h3 style="margin-bottom:12px; color:var(--nwu-purple);">My Saved Routes</h3>
        <asp:GridView ID="gvSavedRoutes" runat="server" AutoGenerateColumns="False" CssClass="table" DataKeyNames="route_id" OnRowCommand="gvSavedRoutes_RowCommand" OnRowDeleting="gvSavedRoutes_RowDeleting" EmptyDataText="No saved routes yet. Pick points above and click 'Save Route'.">
            <Columns>
                <asp:BoundField DataField="label" HeaderText="Route Label" />
                <asp:BoundField DataField="from_name" HeaderText="From" NullDisplayText="(Map Pin)" />
                <asp:BoundField DataField="to_name" HeaderText="To" NullDisplayText="(Map Pin)" />
                <asp:TemplateField HeaderText="Distance">
                    <ItemTemplate>
                        <%# Eval("length_metres") != DBNull.Value ? Convert.ToDouble(Eval("length_metres")).ToString("F0") + " m" : "-" %>
                    </ItemTemplate>
                </asp:TemplateField>
                <asp:BoundField DataField="created_fmt" HeaderText="Saved Date" />
                <asp:TemplateField HeaderText="Actions">
                    <ItemTemplate>
                        <asp:LinkButton ID="btnLoad" runat="server" CommandName="Reevaluate" CommandArgument='<%# Eval("route_id") %>' Text="Load & Check Conditions" CssClass="btn btn-primary btn-sm" />
                        <asp:LinkButton ID="btnDelete" runat="server" CommandName="Delete" Text="Delete" CssClass="btn btn-danger btn-sm" OnClientClick="return confirm('Delete this saved route?');" />
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
        </asp:GridView>
    </div>

    <script>
        var gMap = null;
        var gStartMarker = null;
        var gEndMarker = null;
        var gShortestLine = null;
        var gSaferLine = null;

        document.addEventListener("DOMContentLoaded", function () {
            CampusMap.init('routeMap', 'route');

            var initialData = <%= InitialRouteJson %>;
            if (initialData && initialData.result) {
                renderPrecalculatedRoute(initialData);
            }
        });

        function routeFromDropdowns() {
            var fromVal = document.getElementById('ddlFromLocation').value;
            var toVal = document.getElementById('ddlToLocation').value;

            if (!fromVal || !toVal) {
                alert("Please select both a departure and destination location.");
                return;
            }

            var fParts = fromVal.split('|');
            var tParts = toVal.split('|');

            document.getElementById('hfFromLocId').value = fParts[0];
            document.getElementById('hfToLocId').value = tParts[0];

            var startLat = parseFloat(fParts[1]);
            var startLng = parseFloat(fParts[2]);
            var endLat = parseFloat(tParts[1]);
            var endLng = parseFloat(tParts[2]);

            if (window.routeCalculateCustom) {
                window.routeCalculateCustom(startLat, startLng, endLat, endLng);
            }
        }

        function renderPrecalculatedRoute(data) {
            // draws route on satelite map when page loads,,
            setTimeout(function () {
                if (window.renderCustomRouteData) {
                    window.renderCustomRouteData(data);
                }
            }, 300);
        }
    </script>
</asp:Content>
