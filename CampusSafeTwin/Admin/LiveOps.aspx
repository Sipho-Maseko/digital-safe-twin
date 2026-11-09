<%@ Page Title="Live Ops Command Center - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LiveOps.aspx.cs" Inherits="CampusSafeTwin.Admin.LiveOps" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .liveops-layout {
            display: grid;
            grid-template-columns: 1fr 340px;
            gap: 20px;
            height: calc(100vh - 210px);
            min-height: 600px;
        }
        @media (max-width: 992px) {
            .liveops-layout {
                grid-template-columns: 1fr;
                height: auto;
            }
        }
        .liveops-stats-bar {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 16px;
            margin-bottom: 16px;
        }
        .stat-card {
            background: #fff;
            padding: 12px 18px;
            border-radius: 8px;
            border: 1px solid #e1d8eb;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }
        .stat-number {
            font-size: 1.8rem;
            font-weight: 700;
            color: var(--nwu-purple);
        }
        .stat-label {
            font-size: 0.82rem;
            color: #666;
            text-transform: uppercase;
            font-weight: 600;
        }
        .sidebar-pane {
            display: flex;
            flex-direction: column;
            gap: 16px;
            overflow-y: auto;
            max-height: calc(100vh - 210px);
        }
        .queue-card {
            background: #fff;
            border: 1px solid #ddd;
            border-left: 4px solid var(--nwu-purple);
            border-radius: 6px;
            padding: 12px;
            margin-bottom: 10px;
            font-size: 0.88rem;
            transition: transform 0.15s ease;
        }
        .queue-card.emergency {
            border-left-color: #dc3545;
            background: #fff8f8;
        }
        .queue-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0,0,0,0.06);
        }
        .beacon-pulse {
            border-radius: 50%;
            height: 24px;
            width: 24px;
            position: absolute;
            left: -12px;
            top: -12px;
            border: 3px solid #dc3545;
            animation: pulse-sos 1.5s infinite ease-out;
            background: rgba(220, 53, 69, 0.4);
        }
        @keyframes pulse-sos {
            0% { transform: scale(0.6); opacity: 1; }
            100% { transform: scale(2.2); opacity: 0; }
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
        <div>
            <h1>Manager Live Operations Command</h1>
            <p class="subtitle">Live map of on-duty guards, active SOS panic alerts, and dispatches</p>
        </div>
        <div style="display:flex; gap:10px; align-items:center;">
            <span id="liveStatusBadge" class="status-badge status-inprogress">&#9679; LIVE UPDATING</span>
            <button type="button" class="btn btn-secondary btn-sm" onclick="fetchLiveOps();">&#8635; Refresh</button>
        </div>
    </div>

    <!-- quick stats bar,,, -->
    <div class="liveops-stats-bar">
        <div class="stat-card">
            <div>
                <div class="stat-label">On-Duty Officers</div>
                <div id="statGuards" class="stat-number">0</div>
            </div>
            <div style="font-size:2rem; opacity:0.3;">&#128737;</div>
        </div>
        <div class="stat-card" style="border-left:4px solid #dc3545;">
            <div>
                <div class="stat-label">Active SOS Panics</div>
                <div id="statSos" class="stat-number" style="color:#dc3545;">0</div>
            </div>
            <div style="font-size:2rem; opacity:0.3; color:#dc3545;">&#128680;</div>
        </div>
        <div class="stat-card">
            <div>
                <div class="stat-label">Active Dispatches</div>
                <div id="statDispatches" class="stat-number" style="color:#0d6efd;">0</div>
            </div>
            <div style="font-size:2rem; opacity:0.3; color:#0d6efd;">&#128660;</div>
        </div>
        <div class="stat-card">
            <div>
                <div class="stat-label">Unassigned Queue</div>
                <div id="statUnassigned" class="stat-number" style="color:#fd7e14;">0</div>
            </div>
            <div style="font-size:2rem; opacity:0.3; color:#fd7e14;">&#9203;</div>
        </div>
    </div>

    <!-- main layout grid... -->
    <div class="liveops-layout">
        <!-- map container,, -->
        <div style="position:relative; height:100%; border-radius:8px; overflow:hidden; border:1px solid #ddd;">
            <div id="liveOpsMap" style="height:100%; width:100%;"></div>
        </div>

        <!-- side pannel lists... -->
        <div class="sidebar-pane">
            <div class="card" style="padding:14px;">
                <h3 style="margin-bottom:10px; font-size:1rem; color:var(--nwu-purple);">&#9203; Unassigned Queue (<span id="queueCount">0</span>)</h3>
                <div id="unassignedList" style="max-height:280px; overflow-y:auto;">
                    <p style="color:#777; font-size:0.85rem; font-style:italic;">No unassigned incidents pending.</p>
                </div>
            </div>

            <div class="card" style="padding:14px; flex:1;">
                <h3 style="margin-bottom:10px; font-size:1rem; color:var(--nwu-purple);">&#128737; On-Duty Guard Roster (<span id="rosterCount">0</span>)</h3>
                <div id="guardRosterList" style="max-height:260px; overflow-y:auto;">
                    <p style="color:#777; font-size:0.85rem; font-style:italic;">No guards currently logged on-duty.</p>
                </div>
            </div>
        </div>
    </div>

    <script>
        var liveMap = null;
        var guardLayerGroup = null;
        var sosLayerGroup = null;
        var dispatchLayerGroup = null;
        var zonesLayerGroup = null;

        var availableGuards = [];

        document.addEventListener("DOMContentLoaded", function () {
            // shared satelite campus map opens framed on nwu potch,,,
            liveMap = CampusMap.createBaseMap('liveOpsMap');

            guardLayerGroup = L.layerGroup().addTo(liveMap);
            sosLayerGroup = L.layerGroup().addTo(liveMap);
            dispatchLayerGroup = L.layerGroup().addTo(liveMap);
            zonesLayerGroup = L.layerGroup().addTo(liveMap);

            // gets the danger zones...
            fetchFlaggedZones();

            // first time data load,,,
            fetchLiveOps();

            // poll every six secconds for updates...
            setInterval(fetchLiveOps, 6000);
        });

        function fetchFlaggedZones() {
            fetch((window.AppRoot || '') + 'Handlers/Api.ashx?action=flaggedzones')
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (data && data.zones) {
                        zonesLayerGroup.clearLayers();
                        data.zones.forEach(function(z) {
                            var circle = L.circle([z.latitude, z.longitude], {
                                radius: z.radius_m,
                                color: '#dc3545',
                                fillColor: '#dc3545',
                                fillOpacity: 0.15,
                                weight: 2
                            });
                            circle.bindPopup('<strong>Flagged Zone: ' + CampusMap.escapeHtml(z.name) + '</strong><br/>Risk Level: ' + z.risk_level + '/5<br/>' + CampusMap.escapeHtml(z.reason));
                            zonesLayerGroup.addLayer(circle);
                        });
                    }
                }).catch(function(e) {});
        }

        function fetchLiveOps() {
            fetch((window.AppRoot || '') + 'Handlers/Api.ashx?action=liveops')
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    renderLiveOps(data);
                })
                .catch(function (err) {
                    console.error("LiveOps fetch error:", err);
                });
        }

        function renderLiveOps(data) {
            if (!data) return;

            availableGuards = data.guards || [];

            // updates top count numbers,,
            document.getElementById('statGuards').textContent = availableGuards.length;
            document.getElementById('statSos').textContent = (data.activeSos || []).length;
            document.getElementById('statDispatches').textContent = (data.dispatches || []).length;
            document.getElementById('statUnassigned').textContent = (data.unassigned || []).length;

            document.getElementById('queueCount').textContent = (data.unassigned || []).length;
            document.getElementById('rosterCount').textContent = availableGuards.length;

            // draws guards on map,,,
            guardLayerGroup.clearLayers();
            availableGuards.forEach(function (g) {
                var isStale = g.is_stale;
                var color = isStale ? '#6c757d' : '#198754';
                var shieldIcon = L.divIcon({
                    className: 'guard-shield-marker',
                    html: '<div style="background:' + color + '; color:#fff; border-radius:4px; padding:4px 6px; font-size:12px; font-weight:bold; box-shadow:0 2px 6px rgba(0,0,0,0.3); border:2px solid #fff; white-space:nowrap; display:flex; align-items:center; gap:4px;">' +
                          '&#128737; ' + CampusMap.escapeHtml(g.badge_number || g.full_name) +
                          '</div>',
                    iconSize: [80, 26],
                    iconAnchor: [40, 13]
                });

                var marker = L.marker([g.lat, g.lng], { icon: shieldIcon });
                marker.bindPopup(
                    '<strong>Officer: ' + CampusMap.escapeHtml(g.full_name) + '</strong><br/>' +
                    'Badge: ' + CampusMap.escapeHtml(g.badge_number || 'N/A') + '<br/>' +
                    'Status: ' + (isStale ? '<span style="color:#dc3545;">Stale GPS (' + g.seconds_ago + 's ago)</span>' : '<span style="color:#198754;">Live (Active)</span>')
                );
                guardLayerGroup.addLayer(marker);
            });

            // draws emergency sos icons on map...
            sosLayerGroup.clearLayers();
            (data.activeSos || []).forEach(function (s) {
                var beaconHtml = '<div style="position:relative;"><div class="beacon-pulse"></div><div style="background:#dc3545; color:#fff; border-radius:50%; width:16px; height:16px; border:2px solid #fff;"></div></div>';
                var sosIcon = L.divIcon({
                    className: 'sos-beacon-marker',
                    html: beaconHtml,
                    iconSize: [16, 16],
                    iconAnchor: [8, 8]
                });

                var marker = L.marker([s.lat, s.lng], { icon: sosIcon });
                marker.bindPopup(
                    '<strong style="color:#dc3545;">&#128680; ACTIVE SOS PANIC</strong><br/>' +
                    'Student: ' + CampusMap.escapeHtml(s.reporter_name) + '<br/>' +
                    'Alert Time: ' + CampusMap.escapeHtml(s.reported_fmt) + '<br/>' +
                    '<a href="../Security/Incident.aspx?id=' + s.incident_id + '" class="btn btn-danger btn-sm" style="margin-top:6px; display:inline-block; color:#fff;">Manage / Dispatch</a>'
                );
                sosLayerGroup.addLayer(marker);
            });

            // draws dispatch lines between guard and incident,,
            dispatchLayerGroup.clearLayers();
            (data.dispatches || []).forEach(function (d) {
                if (d.guard_lat && d.guard_lng && d.incident_lat && d.incident_lng) {
                    var strokeColor = '#fd7e14';
                    if (d.status === 'en_route') strokeColor = '#0d6efd';
                    if (d.status === 'on_scene') strokeColor = '#198754';

                    var line = L.polyline([
                        [d.guard_lat, d.guard_lng],
                        [d.incident_lat, d.incident_lng]
                    ], {
                        color: strokeColor,
                        weight: 3,
                        dashArray: '5, 8',
                        opacity: 0.85
                    });

                    line.bindPopup(
                        '<strong>Dispatch: ' + CampusMap.escapeHtml(d.guard_name) + '</strong><br/>' +
                        'Status: <strong>' + CampusMap.escapeHtml(d.status) + '</strong><br/>' +
                        'Incident: ' + CampusMap.escapeHtml(d.category)
                    );
                    dispatchLayerGroup.addLayer(line);
                }
            });

            // shows list of unassigned incidents...
            renderUnassignedQueue(data.unassigned || []);

            // shows guard roster list,,,
            renderGuardRoster(availableGuards);
        }

        function renderUnassignedQueue(items) {
            var listEl = document.getElementById('unassignedList');
            if (!items.length) {
                listEl.innerHTML = '<p style="color:#777; font-size:0.85rem; font-style:italic;">No unassigned incidents pending.</p>';
                return;
            }

            var html = '';
            items.forEach(function (inc) {
                var cls = inc.is_emergency ? 'queue-card emergency' : 'queue-card';
                html += '<div class="' + cls + '">';
                html += '<div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:4px;">';
                html += '<strong>' + (inc.is_emergency ? '<span style="color:#dc3545;">[EMERGENCY SOS]</span> ' : '') + CampusMap.escapeHtml(inc.category) + '</strong>';
                html += '<small style="color:#777;">' + CampusMap.escapeHtml(inc.reported_fmt) + '</small>';
                html += '</div>';
                html += '<div style="color:#555; margin-bottom:8px;">Reporter: ' + CampusMap.escapeHtml(inc.reporter_name) + '</div>';

                // dropdown to assign guard...
                html += '<div style="display:flex; gap:6px;">';
                html += '<select id="assign_select_' + inc.incident_id + '" class="form-control" style="font-size:12px; padding:4px;">';
                html += '<option value="">-- Choose Officer --</option>';
                availableGuards.forEach(function (g) {
                    html += '<option value="' + g.user_id + '">' + CampusMap.escapeHtml(g.full_name) + '</option>';
                });
                html += '</select>';
                html += '<button type="button" class="btn btn-primary btn-sm" style="font-size:11px; padding:4px 8px;" onclick="quickAssign(\'' + inc.incident_id + '\');">Assign</button>';
                html += '</div>';
                html += '</div>';
            });
            listEl.innerHTML = html;
        }

        function renderGuardRoster(guards) {
            var listEl = document.getElementById('guardRosterList');
            if (!guards.length) {
                listEl.innerHTML = '<p style="color:#777; font-size:0.85rem; font-style:italic;">No guards currently logged on-duty.</p>';
                return;
            }

            var html = '';
            guards.forEach(function (g) {
                var dotColor = g.is_stale ? '#dc3545' : '#198754';
                var statusText = g.is_stale ? 'GPS Stale (' + g.seconds_ago + 's)' : 'Active Patrol';
                html += '<div style="padding:8px; border-bottom:1px solid #eee; display:flex; justify-content:space-between; align-items:center; font-size:0.88rem;">';
                html += '<div><strong>' + CampusMap.escapeHtml(g.full_name) + '</strong><br/><small style="color:#777;">Badge: ' + CampusMap.escapeHtml(g.badge_number || 'N/A') + '</small></div>';
                html += '<div style="text-align:right;"><span style="color:' + dotColor + '; font-weight:600;">&#9679; ' + statusText + '</span></div>';
                html += '</div>';
            });
            listEl.innerHTML = html;
        }

        function quickAssign(incidentId) {
            var sel = document.getElementById('assign_select_' + incidentId);
            if (!sel || !sel.value) {
                alert("Please select an officer from the dropdown.");
                return;
            }

            var guardId = sel.value;
            var fd = new FormData();
            fd.append('incidentId', incidentId);
            fd.append('guardId', guardId);

            fetch((window.AppRoot || '') + 'Handlers/Api.ashx?action=quickassign', {
                method: 'POST',
                body: fd
            })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (res && res.success) {
                    fetchLiveOps();
                } else {
                    alert(res.message || "Failed to dispatch officer.");
                }
            })
            .catch(function (err) {
                alert("Error dispatching officer: " + err);
            });
        }
    </script>
</asp:Content>
