<%@ Page Title="My Dispatches - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyDispatches.aspx.cs" Inherits="CampusSafeTwin.Security.MyDispatches" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .dispatch-card {
            background: #ffffff;
            border: 1px solid #e1d8eb;
            border-left: 6px solid var(--nwu-purple);
            border-radius: 6px;
            padding: 18px 20px;
            margin-bottom: 16px;
            box-shadow: 0 2px 6px rgba(0,0,0,0.04);
        }
        .dispatch-card.emergency {
            border-left-color: #dc3545;
            background: #fff8f8;
        }
        .dispatch-meta {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 8px;
        }
        .dispatch-actions {
            margin-top: 14px;
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Officer Dispatches Queue</h1>
    <p class="subtitle">Respond to emergency calls and patrol assignments across campus</p>

    <asp:Panel ID="pnlNotice" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litNotice" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <!-- dispatch map container,,, -->
    <div class="card" style="padding:16px; margin-bottom:20px; background:#fff; border:1px solid #e1d8eb; border-radius:8px;">
        <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px; flex-wrap:wrap; gap:10px;">
            <div>
                <h3 style="margin:0; color:var(--nwu-purple); font-size:1.15rem;">Tactical Incident Map</h3>
                <small style="color:#666;">Shows your current patrol location and all dispatched callouts across campus</small>
            </div>
            <div style="display:flex; gap:14px; font-size:0.85rem; align-items:center; flex-wrap:wrap;">
                <span style="display:flex; align-items:center; gap:6px;"><span style="width:12px; height:12px; border-radius:50%; background:#dc3545; display:inline-block;"></span> Emergency SOS</span>
                <span style="display:flex; align-items:center; gap:6px;"><span style="width:12px; height:12px; border-radius:50%; background:#512D6D; display:inline-block;"></span> Standard Callout</span>
                <span style="display:flex; align-items:center; gap:6px;"><span style="width:12px; height:12px; border-radius:50%; background:#0d6efd; display:inline-block;"></span> Your Patrol Pin</span>
            </div>
        </div>
        <div id="dispatchTacticalMap" style="height: 380px; width: 100%; border-radius: 6px; border: 1px solid #ddd;"></div>
    </div>

    <asp:Repeater ID="rptDispatches" runat="server" OnItemCommand="rptDispatches_ItemCommand">
        <ItemTemplate>
            <div class="dispatch-card <%# Convert.ToBoolean(Eval("is_emergency")) ? "emergency" : "" %>">
                <div class="dispatch-meta">
                    <div>
                        <span class="status-badge status-<%# Eval("incident_status").ToString().ToLower().Replace(" ", "") %>">
                            <%# Eval("incident_status") %>
                        </span>
                        <strong style="margin-left:8px; font-size:1.1rem; color:var(--nwu-purple);">
                            <%# Eval("ref_number") %> &bull; <%# Eval("category") %>
                        </strong>
                        <%# Convert.ToBoolean(Eval("is_emergency")) ? "<span class='status-badge badge-new' style='margin-left:6px; background:#dc3545; color:#fff;'>EMERGENCY SOS</span>" : "" %>
                    </div>
                    <div style="font-size:0.85rem; color:#666;">
                        Dispatched: <strong><%# Eval("dispatched_fmt") %></strong>
                    </div>
                </div>

                <p style="margin: 8px 0; color:#333; font-size:0.95rem;">
                    <%# Eval("description") != DBNull.Value && !string.IsNullOrEmpty(Eval("description").ToString()) ? Server.HtmlEncode(Eval("description").ToString()) : "No description provided." %>
                </p>

                <div style="font-size:0.88rem; color:#555;">
                    <span>Coordinates: <strong><%# Eval("latitude", "{0:F5}") %>, <%# Eval("longitude", "{0:F5}") %></strong></span> &bull;
                    <span>Dispatch Status: <strong><%# Eval("dispatch_status") %></strong></span>
                </div>

                <div class="dispatch-actions">
                    <button type="button" class="btn btn-outline-primary btn-sm" onclick="zoomToDispatch('<%# Eval("incident_id") %>', <%# string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F6}", Eval("latitude")) %>, <%# string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F6}", Eval("longitude")) %>);">
                        View on Map &uarr;
                    </button>

                    <asp:LinkButton ID="btnAccept" runat="server" Text="Accept &amp; En Route" CssClass="btn btn-primary btn-sm"
                        CommandName="EnRoute" CommandArgument='<%# Eval("dispatch_id") %>'
                        Visible='<%# Eval("dispatch_status").ToString() == "assigned" %>' />

                    <asp:LinkButton ID="btnArrived" runat="server" Text="Arrived On Scene" CssClass="btn btn-primary btn-sm"
                        CommandName="OnScene" CommandArgument='<%# Eval("dispatch_id") %>'
                        Visible='<%# Eval("dispatch_status").ToString() == "en_route" %>' />

                    <asp:LinkButton ID="btnResolve" runat="server" Text="Resolve Incident" CssClass="btn btn-success btn-sm"
                        CommandName="Resolve" CommandArgument='<%# Eval("dispatch_id") %>'
                        Visible='<%# Eval("dispatch_status").ToString() == "on_scene" %>' />

                    <asp:LinkButton ID="btnDecline" runat="server" Text="Decline" CssClass="btn btn-secondary btn-sm"
                        CommandName="Decline" CommandArgument='<%# Eval("dispatch_id") %>'
                        Visible='<%# Eval("dispatch_status").ToString() == "assigned" %>'
                        OnClientClick="return confirm('Decline this dispatch? It will be re-assigned to the next available officer.');" />

                    <a href="Incident.aspx?id=<%# Eval("incident_id") %>" class="btn btn-secondary btn-sm" style="margin-left:auto;">
                        Open Incident Details &rarr;
                    </a>
                </div>
            </div>
        </ItemTemplate>
    </asp:Repeater>

    <asp:Panel ID="pnlNoDispatches" runat="server" Visible="false" style="padding:40px; text-align:center; color:#777;">
        <p>No active dispatches currently assigned to you.</p>
    </asp:Panel>

    <script>
        var tacticalMap = null;
        var dispatchMarkers = {};
        var officerMarker = null;

        document.addEventListener("DOMContentLoaded", function () {
            var dispatches = <%= DispatchesJson %>;
            var center = CampusMap.getCampusCenter();
            if (dispatches && dispatches.length > 0) {
                center = [dispatches[0].latitude, dispatches[0].longitude];
            }

            // shared satelite campus map, only frames whole campus when nothing to show,,,
            tacticalMap = CampusMap.createBaseMap('dispatchTacticalMap', {
                center: center,
                fitCampus: !(dispatches && dispatches.length > 0)
            });

            var boundsGroup = [];

            // draws dispatches on map...
            if (dispatches && dispatches.length > 0) {
                dispatches.forEach(function (d) {
                    var isEmerg = d.is_emergency;
                    var color = isEmerg ? '#dc3545' : '#512D6D';
                    var iconHtml = isEmerg ?
                        '<div style="background:#dc3545; color:#fff; border-radius:50%; width:34px; height:34px; display:flex; align-items:center; justify-content:center; font-weight:bold; font-size:12px; border:2px solid #fff; box-shadow:0 2px 6px rgba(0,0,0,0.35); animation:pulse-siren 1.2s infinite;">SOS</div>' :
                        '<div style="background:#512D6D; color:#fff; border-radius:50%; width:30px; height:30px; display:flex; align-items:center; justify-content:center; font-weight:bold; font-size:12px; border:2px solid #fff; box-shadow:0 2px 6px rgba(0,0,0,0.35);">#' + d.ref_number.substr(-3) + '</div>';

                    var markerIcon = L.divIcon({
                        className: 'tactical-dispatch-pin',
                        html: iconHtml,
                        iconSize: [34, 34],
                        iconAnchor: [17, 17]
                    });

                    var m = L.marker([d.latitude, d.longitude], { icon: markerIcon }).addTo(tacticalMap);
                    m.bindPopup('<div style="min-width:200px;">' +
                        '<strong style="color:' + color + ';">' + (isEmerg ? 'EMERGENCY SOS' : d.category) + '</strong><br/>' +
                        '<span>Ref: <strong>' + d.ref_number + '</strong></span><br/>' +
                        '<span>Status: <strong>' + d.dispatch_status + '</strong></span>' +
                        '<p style="margin:6px 0 8px 0; font-size:0.9rem;">' + (d.description || 'No description') + '</p>' +
                        '<a href="Incident.aspx?id=' + encodeURIComponent(d.incident_id) + '" class="btn btn-sm btn-primary" style="color:#fff; display:inline-block;">Open Incident &rarr;</a>' +
                        '</div>');

                    dispatchMarkers[d.incident_id] = m;
                    boundsGroup.push(m);
                });
            }

            // plots current officer locaton on map,,
            if (navigator.geolocation) {
                navigator.geolocation.getCurrentPosition(function (pos) {
                    var oLat = pos.coords.latitude;
                    var oLng = pos.coords.longitude;
                    var officerIcon = L.divIcon({
                        className: 'officer-gps-pin',
                        html: '<div style="background:#0d6efd; color:#fff; border-radius:50%; width:28px; height:28px; display:flex; align-items:center; justify-content:center; font-weight:bold; font-size:13px; border:2px solid #fff; box-shadow:0 2px 6px rgba(0,0,0,0.35);">&#128737;</div>',
                        iconSize: [28, 28],
                        iconAnchor: [14, 14]
                    });

                    officerMarker = L.marker([oLat, oLng], { icon: officerIcon }).addTo(tacticalMap);
                    officerMarker.bindPopup('<strong>Your Current Patrol Location</strong><br/>Coords: ' + oLat.toFixed(5) + ', ' + oLng.toFixed(5));
                    boundsGroup.push(officerMarker);

                    if (boundsGroup.length > 1) {
                        var fg = L.featureGroup(boundsGroup);
                        tacticalMap.fitBounds(fg.getBounds(), { padding: [50, 50], maxZoom: 17 });
                    }
                });
            }

            if (boundsGroup.length > 0) {
                var fg = L.featureGroup(boundsGroup);
                tacticalMap.fitBounds(fg.getBounds(), { padding: [50, 50], maxZoom: 17 });
            }
        });

        function zoomToDispatch(incId, lat, lng) {
            if (!tacticalMap) return;
            var el = document.getElementById('dispatchTacticalMap');
            if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });

            tacticalMap.flyTo([lat, lng], 18, { duration: 1.0 });

            if (dispatchMarkers[incId]) {
                setTimeout(function () {
                    dispatchMarkers[incId].openPopup();
                }, 1100);
            }
        }
    </script>
</asp:Content>
