<%@ Page Title="Live Security Map - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Map.aspx.cs" Inherits="CampusSafeTwin.Security.Map" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .map-section {
            margin-bottom: 24px;
        }
        .legend-bar {
            display: flex;
            gap: 16px;
            align-items: center;
            padding: 8px 12px;
            background: #faf8fc;
            border-radius: 4px;
            margin-top: 10px;
            font-size: 0.88rem;
            flex-wrap: wrap;
        }
        .legend-item {
            display: flex;
            align-items: center;
            gap: 6px;
        }
        .legend-dot {
            width: 12px;
            height: 12px;
            border-radius: 50%;
            display: inline-block;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <!-- siren warning banner for guards,,, -->
    <div id="sirenBanner" style="display:none; background:#fff3f3; border:2px solid #dc3545; color:#842029; padding:16px 20px; border-radius:8px; margin-bottom:18px; box-shadow:0 4px 12px rgba(220,53,69,0.18); animation:pulse-siren 1.5s infinite;">
        <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:12px;">
            <div>
                <div style="display:flex; align-items:center; gap:8px;">
                    <span style="background:#dc3545; color:#fff; font-weight:700; font-size:0.8rem; padding:3px 8px; border-radius:4px; letter-spacing:0.5px;">EMERGENCY ALERT</span>
                    <strong style="font-size:1.15rem; color:#842029;">CRITICAL SOS DISPATCHED TO YOU</strong>
                </div>
                <p id="sirenMsg" style="margin:6px 0 0 0; font-size:0.95rem; color:#491217;">You have an active emergency call requiring immediate response.</p>
            </div>
            <div style="display:flex; gap:10px; align-items:center;">
                <button type="button" class="btn btn-danger" style="font-weight:700; padding:8px 16px;" onclick="focusEmergencyIncident();">Focus on Map &rarr;</button>
                <a href="MyDispatches.aspx" class="btn btn-outline-danger" style="font-weight:600; padding:8px 14px;">Open Dispatch Queue</a>
            </div>
        </div>
    </div>
    <style>
        @keyframes pulse-siren {
            0% { box-shadow: 0 0 0 0 rgba(220, 53, 69, 0.6); }
            70% { box-shadow: 0 0 0 16px rgba(220, 53, 69, 0); }
            100% { box-shadow: 0 0 0 0 rgba(220, 53, 69, 0); }
        }
    </style>

    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px; flex-wrap:wrap; gap:12px;">
        <div>
            <h1>Live Security Incidents Map</h1>
            <p class="subtitle" style="margin-bottom:0;">Live spatial view of reported incidents across campus (last 30 days)</p>
        </div>
        <div style="display:flex; align-items:center; gap:14px;">
            <!-- on duty toggle button... -->
            <button type="button" id="btnToggleDuty" class="btn btn-secondary" onclick="toggleDuty()">
                Status: <span id="dutyStatusText">Off Duty</span>
            </button>
            <a href="MyDispatches.aspx" class="btn btn-primary">My Dispatches</a>
        </div>
    </div>

    <div class="map-section">
        <div id="securityMap" class="map-frame" style="min-height: 520px;"></div>
        <div class="legend-bar">
            <strong>Incident Status:</strong>
            <div class="legend-item"><span class="legend-dot" style="background:#dc3545;"></span> New</div>
            <div class="legend-item"><span class="legend-dot" style="background:#fd7e14;"></span> Acknowledged</div>
            <div class="legend-item"><span class="legend-dot" style="background:#0d6efd;"></span> In Progress</div>
            <div class="legend-item"><span class="legend-dot" style="background:#198754;"></span> Resolved</div>
        </div>
    </div>

    <h2>Open Incidents Queue</h2>
    <p class="subtitle">Incidents awaiting response or currently in progress</p>

    <asp:GridView ID="gvOpenIncidents" runat="server" AutoGenerateColumns="False" CssClass="grid-view">
        <Columns>
            <asp:BoundField DataField="ref_number" HeaderText="Ref" />
            <asp:BoundField DataField="category" HeaderText="Category" />
            <asp:TemplateField HeaderText="Status">
                <ItemTemplate>
                    <span class="status-badge status-<%# Eval("status").ToString().ToLower().Replace(" ", "") %>">
                        <%# Eval("status") %>
                    </span>
                </ItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="occurred_fmt" HeaderText="Occurred" />
            <asp:BoundField DataField="reported_fmt" HeaderText="Reported" />
            <asp:BoundField DataField="reporter_name" HeaderText="Reporter" />
            <asp:TemplateField HeaderText="Action">
                <ItemTemplate>
                    <a href="Incident.aspx?id=<%# Eval("incident_id") %>" class="btn btn-primary" style="padding:4px 10px; font-size:0.85rem;">
                        Manage &rarr;
                    </a>
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>

    <asp:Panel ID="pnlNoIncidents" runat="server" Visible="false" style="padding:24px; text-align:center; color:#666;">
        <p>No open incidents at this time. All campus incidents are resolved.</p>
    </asp:Panel>

    <script>
        var isOnDuty = false;
        var watchId = null;
        var myLat = -26.6890;
        var myLng = 27.0935;
        var sirenPlayed = false;

        document.addEventListener("DOMContentLoaded", function () {
            CampusMap.init('securityMap', 'live');
            checkEmergencyDispatches();
            setInterval(checkEmergencyDispatches, 10000);
        });

        function toggleDuty() {
            isOnDuty = !isOnDuty;
            var btn = document.getElementById("btnToggleDuty");
            var txt = document.getElementById("dutyStatusText");

            if (isOnDuty) {
                btn.className = "btn btn-success";
                txt.textContent = "ON DUTY (Active)";
                startDutyTracking();
            } else {
                btn.className = "btn btn-secondary";
                txt.textContent = "Off Duty";
                stopDutyTracking();
            }
        }

        function startDutyTracking() {
            if (!navigator.geolocation) {
                postLocation(myLat, myLng, true);
                return;
            }

            navigator.geolocation.getCurrentPosition(function (p) {
                myLat = p.coords.latitude;
                myLng = p.coords.longitude;
                postLocation(myLat, myLng, true);
            });

            watchId = navigator.geolocation.watchPosition(function (p) {
                myLat = p.coords.latitude;
                myLng = p.coords.longitude;
                postLocation(myLat, myLng, true);
            }, null, { enableHighAccuracy: true });

            // sends current gps every fifteen seconds,,
            window.dutyInterval = setInterval(function () {
                if (isOnDuty) postLocation(myLat, myLng, true);
            }, 15000);
        }

        function stopDutyTracking() {
            if (watchId && navigator.geolocation) {
                navigator.geolocation.clearWatch(watchId);
                watchId = null;
            }
            if (window.dutyInterval) {
                clearInterval(window.dutyInterval);
            }
            postLocation(myLat, myLng, false);
        }

        function postLocation(lat, lng, onDuty) {
            var fd = new FormData();
            fd.append("lat", lat.toFixed(6));
            fd.append("lng", lng.toFixed(6));
            fd.append("is_on_duty", onDuty ? "true" : "false");

            fetch('<%= ResolveUrl("~/Handlers/MyLocation.ashx") %>', {
                method: "POST",
                body: fd
            }).catch(function (e) { console.warn("Duty location post error:", e); });
        }

        var activeEmergencyInc = null;
        var emergencyMarker = null;

        function checkEmergencyDispatches() {
            var url = '<%= ResolveUrl("~/Handlers/Api.ashx?action=incidents") %>';
            fetch(url)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (!data || !data.incidents) return;
                    var hasEmergency = false;
                    for (var i = 0; i < data.incidents.length; i++) {
                        var inc = data.incidents[i];
                        if (inc.status !== 'Resolved' && ((inc.description || '').indexOf('SOS') >= 0 || inc.is_emergency)) {
                            hasEmergency = true;
                            activeEmergencyInc = inc;
                            break;
                        }
                    }

                    var banner = document.getElementById("sirenBanner");
                    if (banner) {
                        banner.style.display = hasEmergency ? "block" : "none";
                        if (hasEmergency) {
                            var msg = document.getElementById("sirenMsg");
                            if (msg && activeEmergencyInc) {
                                msg.textContent = "SOS Alert near (" + activeEmergencyInc.latitude.toFixed(4) + ", " + activeEmergencyInc.longitude.toFixed(4) + ") : " + (activeEmergencyInc.description || 'Emergency assistance requested.');
                            }
                            if (!sirenPlayed) {
                                playSirenAudio();
                                sirenPlayed = true;
                            }
                        }
                    }
                });
        }

        function focusEmergencyIncident() {
            if (!activeEmergencyInc || !window.CampusMapLive) return;
            var lat = parseFloat(activeEmergencyInc.latitude);
            var lng = parseFloat(activeEmergencyInc.longitude);
            if (isNaN(lat) || isNaN(lng)) return;

            window.CampusMapLive.flyTo([lat, lng], 18, { duration: 1.2 });

            if (emergencyMarker) {
                window.CampusMapLive.removeLayer(emergencyMarker);
            }

            var emergencyIcon = L.divIcon({
                className: 'emergency-live-beacon',
                html: '<div style="background:#dc3545; color:#fff; border-radius:50%; width:36px; height:36px; display:flex; align-items:center; justify-content:center; font-weight:bold; font-size:12px; box-shadow:0 0 0 10px rgba(220,53,69,0.35); border:2px solid #fff; animation:pulse-siren 1s infinite;">SOS</div>',
                iconSize: [36, 36],
                iconAnchor: [18, 18]
            });

            emergencyMarker = L.marker([lat, lng], { icon: emergencyIcon }).addTo(window.CampusMapLive);
            emergencyMarker.bindPopup('<div style="min-width:210px;">' +
                '<strong style="color:#dc3545; font-size:1.05rem;">CRITICAL EMERGENCY SOS</strong><br/>' +
                '<small style="color:#666;">Incident Ref: ' + (activeEmergencyInc.ref_number || activeEmergencyInc.incident_id.substr(0, 8)) + '</small>' +
                '<p style="margin:6px 0 10px 0; font-size:0.92rem;">' + (activeEmergencyInc.description || 'Emergency assistance requested.') + '</p>' +
                '<a href="Incident.aspx?id=' + encodeURIComponent(activeEmergencyInc.incident_id) + '" class="btn btn-sm btn-primary" style="color:#fff; display:inline-block;">Open Incident Details &rarr;</a>' +
                '</div>').openPopup();
        }

        function playSirenAudio() {
            try {
                var AudioContext = window.AudioContext || window.webkitAudioContext;
                if (!AudioContext) return;
                var ctx = new AudioContext();
                var osc = ctx.createOscillator();
                var gain = ctx.createGain();
                osc.type = 'sawtooth';
                osc.frequency.setValueAtTime(850, ctx.currentTime);
                osc.frequency.exponentialRampToValueAtTime(450, ctx.currentTime + 0.35);
                osc.frequency.exponentialRampToValueAtTime(850, ctx.currentTime + 0.7);
                gain.gain.setValueAtTime(0.3, ctx.currentTime);
                gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.9);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start();
                osc.stop(ctx.currentTime + 0.9);
            } catch (e) { }
        }
    </script>
</asp:Content>
