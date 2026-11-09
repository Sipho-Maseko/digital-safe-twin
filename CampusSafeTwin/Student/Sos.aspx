<%@ Page Title="Emergency SOS - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Sos.aspx.cs" Inherits="CampusSafeTwin.Student.Sos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .sos-container {
            max-width: 600px;
            margin: 20px auto;
            text-align: center;
        }
        .btn-sos {
            width: 220px;
            height: 220px;
            border-radius: 50%;
            background: radial-gradient(circle, #ff2a2a 0%, #cc0000 100%);
            color: #ffffff;
            font-size: 1.8rem;
            font-weight: 800;
            border: 8px solid rgba(255, 255, 255, 0.4);
            box-shadow: 0 0 35px rgba(230, 0, 0, 0.6);
            cursor: pointer;
            outline: none;
            transition: transform 0.15s, box-shadow 0.15s;
            display: inline-flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
            margin: 24px auto;
            text-transform: uppercase;
            letter-spacing: 1px;
        }
        .btn-sos:hover {
            transform: scale(1.05);
            box-shadow: 0 0 50px rgba(255, 0, 0, 0.85);
        }
        .btn-sos:active {
            transform: scale(0.96);
        }
        .btn-sos small {
            font-size: 0.85rem;
            font-weight: 600;
            opacity: 0.9;
            margin-top: 4px;
        }
        .pulse-active {
            animation: pulse-ring 1.4s infinite;
        }
        @keyframes pulse-ring {
            0% { box-shadow: 0 0 0 0 rgba(220, 53, 69, 0.7); }
            70% { box-shadow: 0 0 0 30px rgba(220, 53, 69, 0); }
            100% { box-shadow: 0 0 0 0 rgba(220, 53, 69, 0); }
        }
        .sos-status-panel {
            background: #ffffff;
            border: 2px solid #dc3545;
            border-radius: 8px;
            padding: 24px;
            margin-top: 24px;
            box-shadow: 0 4px 16px rgba(220, 53, 69, 0.15);
            text-align: left;
        }
        .step-item {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 10px 0;
            border-bottom: 1px solid #f0f0f0;
        }
        .step-indicator {
            width: 28px;
            height: 28px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            font-size: 0.85rem;
            background: #eee;
            color: #888;
        }
        .step-indicator.active {
            background: #0d6efd;
            color: #fff;
        }
        .step-indicator.done {
            background: #198754;
            color: #fff;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div class="sos-container">
        <h1 style="color:#cc0000;">Campus Emergency SOS</h1>
        <p class="subtitle">Press the panic button below to dispatch the nearest campus security officer immediately</p>

        <div id="pnlTriggerSection">
            <button type="button" id="btnPanic" class="btn-sos" onclick="triggerSos()">
                <span>&#128680; SOS</span>
                <small>Tap for Help</small>
            </button>
            <p id="lblGpsStatus" style="font-size:0.9rem; color:#666;">Acquiring GPS location...</p>
        </div>

        <div id="pnlSosActive" class="sos-status-panel" style="display:none;">
            <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
                <h3 style="color:#cc0000; margin-bottom:0;">&#9888; SOS Active: Help is on the way!</h3>
                <span id="sosRefBadge" class="status-badge badge-new">EMERGENCY</span>
            </div>
            <p id="sosFriendlyMsg" style="font-size:1.05rem; font-weight:600; color:#333; margin-bottom:16px;">
                Security command has received your location and is coordinating response.
            </p>

            <div class="step-item">
                <div id="step1" class="step-indicator done">&#10003;</div>
                <div><strong>1. Emergency Signal Sent</strong><br/><small style="color:#666;">Coordinates broadcast to Security Dispatch</small></div>
            </div>
            <div class="step-item">
                <div id="step2" class="step-indicator active">2</div>
                <div><strong>2. Officer Dispatched</strong><br/><small style="color:#666;">Nearest patrol assigned to respond</small></div>
            </div>
            <div class="step-item">
                <div id="step3" class="step-indicator">3</div>
                <div><strong>3. Officer En Route</strong><br/><small style="color:#666;">Patrol is rushing to your pinned location</small></div>
            </div>
            <div class="step-item">
                <div id="step4" class="step-indicator">4</div>
                <div><strong>4. On Scene &amp; Safe</strong><br/><small style="color:#666;">Security on site to secure the area</small></div>
            </div>

            <div style="margin-top:20px; display:flex; justify-content:space-between; align-items:center;">
                <button type="button" class="btn btn-secondary btn-sm" onclick="cancelSos()">Cancel Alert (Accidental)</button>
                <small style="color:#777;">Auto-refreshing status...</small>
            </div>
        </div>
    </div>

    <script>
        var currentLat = -26.6890;
        var currentLng = 27.0935;
        var activeIncidentId = null;
        var pollTimer = null;
        var locationWatchId = null;

        document.addEventListener("DOMContentLoaded", function () {
            if (navigator.geolocation) {
                navigator.geolocation.getCurrentPosition(function (pos) {
                    currentLat = pos.coords.latitude;
                    currentLng = pos.coords.longitude;
                    var el = document.getElementById("lblGpsStatus");
                    if (el) el.textContent = "GPS Ready: " + currentLat.toFixed(5) + ", " + currentLng.toFixed(5);
                }, function (err) {
                    var el = document.getElementById("lblGpsStatus");
                    if (el) el.textContent = "Using default campus center coordinates (GPS unavailable)";
                });
            }
        });

        function playSirenAudio() {
            try {
                var AudioContext = window.AudioContext || window.webkitAudioContext;
                if (!AudioContext) return;
                var ctx = new AudioContext();
                var osc = ctx.createOscillator();
                var gain = ctx.createGain();
                osc.type = 'sawtooth';
                osc.frequency.setValueAtTime(800, ctx.currentTime);
                osc.frequency.exponentialRampToValueAtTime(400, ctx.currentTime + 0.3);
                osc.frequency.exponentialRampToValueAtTime(800, ctx.currentTime + 0.6);
                gain.gain.setValueAtTime(0.3, ctx.currentTime);
                gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.8);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start();
                osc.stop(ctx.currentTime + 0.8);
            } catch (e) { }
        }

        function triggerSos() {
            var btn = document.getElementById("btnPanic");
            if (btn) btn.classList.add("pulse-active");
            playSirenAudio();

            var fd = new FormData();
            fd.append("action", "trigger");
            fd.append("lat", currentLat.toFixed(6));
            fd.append("lng", currentLng.toFixed(6));

            var apiUrl = '<%= ResolveUrl("~/Handlers/Sos.ashx") %>';
            fetch(apiUrl, { method: "POST", body: fd })
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res && res.success) {
                        activeIncidentId = res.incident_id;
                        document.getElementById("pnlTriggerSection").style.display = "none";
                        document.getElementById("pnlSosActive").style.display = "block";
                        document.getElementById("sosRefBadge").textContent = res.ref_number;

                        // sends live gps cordinates while panic mode is on,,,
                        startSosLocationBroadcast();

                        // checks status every few seconds...
                        pollSosStatus();
                        pollTimer = setInterval(pollSosStatus, 4000);
                    } else {
                        alert(res.message || "Failed to trigger SOS.");
                    }
                })
                .catch(function (e) {
                    alert("Error reaching emergency services: " + e);
                });
        }

        function startSosLocationBroadcast() {
            if (navigator.geolocation) {
                locationWatchId = navigator.geolocation.watchPosition(function (pos) {
                    currentLat = pos.coords.latitude;
                    currentLng = pos.coords.longitude;
                    var fd = new FormData();
                    fd.append("lat", currentLat.toFixed(6));
                    fd.append("lng", currentLng.toFixed(6));
                    fd.append("accuracy", pos.coords.accuracy || 10);
                    fetch('<%= ResolveUrl("~/Handlers/MyLocation.ashx") %>', { method: "POST", body: fd });
                }, null, { enableHighAccuracy: true });
            }
        }

        function pollSosStatus() {
            if (!activeIncidentId) return;

            var url = '<%= ResolveUrl("~/Handlers/Sos.ashx?action=poll&incident_id=") %>' + encodeURIComponent(activeIncidentId);
            fetch(url)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (!data) return;

                    var msgEl = document.getElementById("sosFriendlyMsg");
                    if (msgEl && data.friendly_message) {
                        msgEl.textContent = data.friendly_message;
                    }

                    var s1 = document.getElementById("step1");
                    var s2 = document.getElementById("step2");
                    var s3 = document.getElementById("step3");
                    var s4 = document.getElementById("step4");

                    // updates the progress icons,,,
                    var disp = data.dispatch_status;
                    if (disp === "assigned") {
                        s2.className = "step-indicator done"; s2.textContent = String.fromCharCode(10003);
                        s3.className = "step-indicator active";
                    } else if (disp === "en_route") {
                        s2.className = "step-indicator done"; s2.textContent = String.fromCharCode(10003);
                        s3.className = "step-indicator done"; s3.textContent = String.fromCharCode(10003);
                        s4.className = "step-indicator active";
                    } else if (disp === "on_scene" || disp === "resolved" || data.is_resolved) {
                        s2.className = "step-indicator done"; s2.textContent = String.fromCharCode(10003);
                        s3.className = "step-indicator done"; s3.textContent = String.fromCharCode(10003);
                        s4.className = "step-indicator done"; s4.textContent = String.fromCharCode(10003);
                        if (pollTimer) clearInterval(pollTimer);
                        if (locationWatchId && navigator.geolocation) navigator.geolocation.clearWatch(locationWatchId);
                    }
                })
                .catch(function (e) {
                    console.warn("Poll error:", e);
                });
        }

        function cancelSos() {
            if (!confirm("Are you sure you want to cancel this emergency alert?")) return;
            if (pollTimer) clearInterval(pollTimer);
            if (locationWatchId && navigator.geolocation) navigator.geolocation.clearWatch(locationWatchId);

            var fd = new FormData();
            fd.append("action", "cancel");
            fd.append("incident_id", activeIncidentId);

            fetch('<%= ResolveUrl("~/Handlers/Sos.ashx") %>', { method: "POST", body: fd })
                .then(function () {
                    alert("Emergency alert cancelled.");
                    window.location.reload();
                });
        }
    </script>
</asp:Content>
