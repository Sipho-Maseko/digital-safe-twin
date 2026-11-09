<%@ Page Title="Routing Network Editor - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Network.aspx.cs" Inherits="CampusSafeTwin.Admin.Network" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .editor-controls {
            display: flex;
            justify-content: space-between;
            align-items: center;
            background: #faf8fc;
            padding: 12px 18px;
            border-radius: 6px;
            margin-bottom: 16px;
            border: 1px solid #e1d8eb;
            flex-wrap: wrap;
            gap: 10px;
        }
        .instruction-box {
            font-size: 0.9rem;
            color: #555;
        }
        .instruction-box strong {
            color: var(--nwu-purple);
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
        <div>
            <h1>Walkway Routing Network Editor</h1>
            <p class="subtitle" style="margin-bottom:0;">Manage campus walkway nodes and bidirectional path links for safe routing</p>
        </div>
        <div>
            <button type="button" id="btnStarterGrid" class="btn btn-success" onclick="triggerStarterGrid()">
                &#128506; Load Real Campus Walkways (OpenStreetMap)
            </button>
        </div>
    </div>

    <div class="editor-controls">
        <div class="instruction-box">
            <strong>Controls:</strong> Click map to add a node &bull; Click Node A then Node B to link &bull; Click a link to toggle lit or delete &bull; Click a node to delete
        </div>
        <div id="networkStatus" style="font-weight:600; color:var(--nwu-purple); font-size:0.92rem;">
            Ready. Click anywhere to add a path node.
        </div>
    </div>

    <div id="networkMap" class="map-frame" style="min-height: 580px;"></div>

    <script>
        document.addEventListener("DOMContentLoaded", function () {
            // initializes the network editor on satelite map...
            CampusMap.init('networkMap', 'network');
        });

        // reloads the real campus walkway network from the osm data file,,,
        function triggerStarterGrid() {
            if (!confirm("Load the real NWU Potchefstroom walkway network from OpenStreetMap data?\n\nThis REPLACES all current nodes and links with ~2,500 real walkway points.")) return;
            var apiUrl = '<%= ResolveUrl("~/Handlers/Api.ashx?action=rebuildNetwork") %>';
            fetch(apiUrl, { method: 'POST' })
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    alert(res.message);
                    if (window.CampusMapNetwork) {
                        window.CampusMapNetwork.reload();
                    }
                })
                .catch(function (e) {
                    alert("Error creating starter grid: " + e);
                });
        }
    </script>
</asp:Content>
