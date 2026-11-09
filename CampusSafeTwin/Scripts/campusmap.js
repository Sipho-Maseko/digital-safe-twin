(function (window) {
    'use strict';

    var CampusMap = {};

    // gets campus center cordinates from meta tags...
    CampusMap.getCampusCenter = function () {
        var metaLat = document.querySelector('meta[name="map-center-lat"]');
        var metaLng = document.querySelector('meta[name="map-center-lng"]');
        var lat = metaLat ? parseFloat(metaLat.getAttribute('content')) : -26.6890;
        var lng = metaLng ? parseFloat(metaLng.getAttribute('content')) : 27.0935;
        if (isNaN(lat)) lat = -26.6890;
        if (isNaN(lng)) lng = 27.0935;
        return [lat, lng];
    };

    // cleans text so it does not break html,,,
    CampusMap.escapeHtml = function (str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    };

    // converts cordinates between geojson and leaflet,,
    CampusMap.toLatLng = function (coords) {
        return [coords[1], coords[0]];
    };

    // converts leaflet latlng to geojson format...
    CampusMap.toGeoJsonCoord = function (latLng) {
        return [latLng.lng, latLng.lat];
    };

    // seting up the map tile providers,,,
    CampusMap.createBaseLayers = function () {
        var esriStreet = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 19,
            attribution: 'Tiles &copy; Esri'
        });

        var esriTopo = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 19,
            attribution: 'Tiles &copy; Esri'
        });

        var satellite = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 19,
            maxNativeZoom: 18, // esri imagery has no deeper tiles here, upscale instead of "map data not yet available",,,
            attribution: '&copy; Esri, Maxar, Earthstar Geographics'
        });

        var osm = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        });

        return {
            street: esriStreet,
            topo: esriTopo,
            satellite: satellite,
            osm: osm
        };
    };

    // picks badge color depending on status,,
    CampusMap.getStatusColor = function (status) {
        switch ((status || '').toLowerCase().replace(/\s+/g, '')) {
            case 'new': return '#dc3545';
            case 'acknowledged': return '#fd7e14';
            case 'inprogress': return '#0d6efd';
            case 'resolved': return '#198754';
            default: return '#6c757d';
        }
    };

    // draws line around the campus border and frames the campus...
    CampusMap.loadBoundaryMask = function (map, fitToCampus) {
        var url = (window.AppRoot || '') + 'Handlers/Api.ashx?action=boundary';
        fetch(url)
            .then(function (r) { if (r.ok) return r.json(); return null; })
            .then(function (geoJson) {
                if (!geoJson || !geoJson.features || !geoJson.features.length) return;
                var coords = geoJson.features[0].geometry.coordinates[0];
                var hole = coords.map(function (c) { return [c[1], c[0]]; });

                var poly = L.polygon(hole, {
                    color: '#512D6D',
                    weight: 3,
                    opacity: 0.85,
                    fillColor: '#512D6D',
                    fillOpacity: 0.04,
                    dashArray: '8, 8',
                    interactive: false
                }).addTo(map);

                // frames the whole campus so the map always opens on nwu potch,,,
                if (fitToCampus) {
                    try {
                        map.fitBounds(poly.getBounds(), { padding: [12, 12], maxZoom: 17 });
                    } catch (e) { }
                }
            })
            .catch(function () {});
    };

    // builds the standard satelite campus map used by every page,,,
    CampusMap.createBaseMap = function (divId, opts) {
        var el = document.getElementById(divId);
        if (!el) return null;
        opts = opts || {};

        var center = opts.center || CampusMap.getCampusCenter();
        var baseLayers = CampusMap.createBaseLayers();

        var map = L.map(divId, {
            center: center,
            zoom: opts.zoom || 16,
            layers: [baseLayers.satellite],
            preferCanvas: true
        });

        L.control.layers({
            "Satellite Imagery": baseLayers.satellite,
            "Street Map (Esri)": baseLayers.street,
            "Topographic Map": baseLayers.topo,
            "OpenStreetMap": baseLayers.osm
        }, null, { position: 'topright' }).addTo(map);

        setTimeout(function () {
            map.invalidateSize();
        }, 200);

        window.addEventListener('resize', function () {
            map.invalidateSize();
        });

        // button to find current locaton on map,,,
        var LocateControl = L.Control.extend({
            options: { position: 'topleft' },
            onAdd: function () {
                var btn = L.DomUtil.create('button', 'leaflet-bar leaflet-control');
                btn.innerHTML = '&#9673;';
                btn.title = 'Locate My Position';
                btn.style.width = '30px';
                btn.style.height = '30px';
                btn.style.lineHeight = '30px';
                btn.style.background = '#fff';
                btn.style.cursor = 'pointer';
                btn.style.fontSize = '16px';
                btn.style.fontWeight = 'bold';
                btn.style.textAlign = 'center';
                btn.style.display = 'block';
                btn.style.border = '2px solid rgba(0,0,0,0.2)';
                btn.onclick = function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    map.locate({ setView: true, maxZoom: 17 });
                };
                return btn;
            }
        });
        map.addControl(new LocateControl());

        // adds the school boundry and frames campus unless page wants its own view...
        CampusMap.loadBoundaryMask(map, opts.fitCampus !== false);

        return map;
    };

    // draws unsafe areas (flagged zones + incident hotspot cells) on any map,,,
    CampusMap.loadRiskOverlay = function (map) {
        var url = (window.AppRoot || '') + 'Handlers/Api.ashx?action=riskoverlay';
        fetch(url)
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data) return;

                // red circles for flagged danger zones...
                (data.zones || []).forEach(function (z) {
                    var circle = L.circle([z.latitude, z.longitude], {
                        radius: z.radius_m,
                        color: '#dc3545',
                        fillColor: '#dc3545',
                        fillOpacity: 0.12,
                        weight: 2,
                        dashArray: '4, 6'
                    }).addTo(map);
                    circle.bindPopup('<strong>&#9888; Flagged Zone: ' + CampusMap.escapeHtml(z.name) + '</strong><br/>Risk level ' + z.risk_level + '/5 &bull; avoided by safe routing');
                });

                // orange boxes for incident hotspot cells,,,
                (data.cells || []).forEach(function (cell) {
                    var latLng = L.latLng(cell.cell_lat, cell.cell_lng);
                    var pt = L.Projection.SphericalMercator.project(latLng);
                    var sw = L.Projection.SphericalMercator.unproject(L.point(pt.x - 20, pt.y - 20));
                    var ne = L.Projection.SphericalMercator.unproject(L.point(pt.x + 20, pt.y + 20));

                    var rect = L.rectangle([sw, ne], {
                        color: '#e07b00',
                        weight: 1,
                        fillColor: '#ff9900',
                        fillOpacity: 0.25
                    }).addTo(map);
                    rect.bindPopup('<strong>Incident hotspot</strong><br/>' + cell.incident_count + ' recent reports in this area');
                });
            })
            .catch(function () {});
    };

    // inits the map with satelite view enabled by default,,,
    CampusMap.init = function (divId, mode, options) {
        var map = CampusMap.createBaseMap(divId, options);
        if (!map) return null;

        if (mode === 'live') {
            window.CampusMapLive = map;
        }

        switch (mode) {
            case 'pick':
                CampusMap.setupPickMode(map, options);
                break;
            case 'live':
                CampusMap.setupLiveMode(map, options);
                break;
            case 'hotspots':
                CampusMap.setupHotspotsMode(map, options);
                break;
            case 'network':
                CampusMap.setupNetworkMode(map, options);
                break;
            case 'route':
                CampusMap.setupRouteMode(map, options);
                break;
        }

        return map;
    };

    // pick mode for report page when clickin on map,,,
    CampusMap.setupPickMode = function (map, options) {
        var marker = null;
        var hfLat = document.getElementById('hfLat');
        var hfLng = document.getElementById('hfLng');
        var lblCoord = document.getElementById('lblSelectedCoords');

        function updateCoords(lat, lng) {
            var latStr = lat.toFixed(6);
            var lngStr = lng.toFixed(6);
            if (hfLat) hfLat.value = latStr;
            if (hfLng) hfLng.value = lngStr;
            if (lblCoord) lblCoord.textContent = latStr + ", " + lngStr;
        }

        if (hfLat && hfLng && hfLat.value && hfLng.value) {
            var initialLat = parseFloat(hfLat.value);
            var initialLng = parseFloat(hfLng.value);
            if (!isNaN(initialLat) && !isNaN(initialLng)) {
                marker = L.marker([initialLat, initialLng], { draggable: true }).addTo(map);
                map.setView([initialLat, initialLng], 17);
                marker.on('dragend', function (e) {
                    var pos = e.target.getLatLng();
                    updateCoords(pos.lat, pos.lng);
                });
            }
        }

        map.on('click', function (e) {
            var lat = e.latlng.lat;
            var lng = e.latlng.lng;
            if (!marker) {
                marker = L.marker([lat, lng], { draggable: true }).addTo(map);
                marker.on('dragend', function (ev) {
                    var pos = ev.target.getLatLng();
                    updateCoords(pos.lat, pos.lng);
                });
            } else {
                marker.setLatLng([lat, lng]);
            }
            updateCoords(lat, lng);
        });
    };

    // live mode for security guard map to see pins...
    CampusMap.setupLiveMode = function (map, options) {
        var markers = {};

        function pollIncidents() {
            if (document.hidden) return;
            var apiUrl = (window.AppRoot || '') + 'Handlers/Api.ashx?action=incidents';
            fetch(apiUrl)
                .then(function (res) { return res.json(); })
                .then(function (data) {
                    if (!data || !data.incidents) return;
                    var activeIds = {};

                    data.incidents.forEach(function (inc) {
                        var id = inc.incident_id;
                        activeIds[id] = true;
                        var color = CampusMap.getStatusColor(inc.status);
                        var popupContent = '<div style="min-width:180px;">' +
                            '<strong style="color:' + color + ';">' + CampusMap.escapeHtml(inc.status) + '</strong> &bull; ' +
                            '<strong>' + CampusMap.escapeHtml(inc.category) + '</strong><br/>' +
                            '<small style="color:#666;">' + CampusMap.escapeHtml(inc.occurred_fmt || inc.reported_fmt) + '</small><br/>' +
                            '<p style="margin:4px 0 8px 0; font-size:13px;">' + CampusMap.escapeHtml(inc.description || 'No description') + '</p>' +
                            '<a href="Incident.aspx?id=' + encodeURIComponent(id) + '" style="font-weight:600; color:#512D6D;">Open Incident &rarr;</a>' +
                            '</div>';

                        if (markers[id]) {
                            // upadte the marker if already on map,,,
                            markers[id].setLatLng([inc.latitude, inc.longitude]);
                            markers[id].setStyle({ color: color, fillColor: color });
                            markers[id].setPopupContent(popupContent);
                        } else {
                            // makes a new circle marker on map...
                            var m = L.circleMarker([inc.latitude, inc.longitude], {
                                radius: 8,
                                color: color,
                                fillColor: color,
                                fillOpacity: 0.8,
                                weight: 2
                            }).addTo(map);

                            m.bindPopup(popupContent);
                            markers[id] = m;
                        }
                    });

                    // clears markers that arent there anymore,,
                    Object.keys(markers).forEach(function (id) {
                        if (!activeIds[id]) {
                            map.removeLayer(markers[id]);
                            delete markers[id];
                        }
                    });
                })
                .catch(function (err) {
                    console.warn("Poll incidents error:", err);
                });
        }

        pollIncidents();
        var timer = setInterval(pollIncidents, 10000);
        window.addEventListener('beforeunload', function () { clearInterval(timer); });
    };

    // shows hotspots on map for admin,,,
    CampusMap.setupHotspotsMode = function (map, options) {
        // also draws the flagged danger zones so all unsafe areas show together...
        fetch((window.AppRoot || '') + 'Handlers/Api.ashx?action=flaggedzones')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data || !data.zones) return;
                data.zones.forEach(function (z) {
                    var c = L.circle([z.latitude, z.longitude], {
                        radius: z.radius_m,
                        color: '#dc3545',
                        fillColor: '#dc3545',
                        fillOpacity: 0.12,
                        weight: 2,
                        dashArray: '4, 6'
                    }).addTo(map);
                    c.bindPopup('<strong>&#9888; Flagged Zone: ' + CampusMap.escapeHtml(z.name) + '</strong><br/>Risk level ' + z.risk_level + '/5<br/>' + CampusMap.escapeHtml(z.reason || ''));
                });
            })
            .catch(function () {});

        var apiUrl = (window.AppRoot || '') + 'Handlers/Api.ashx?action=hotspots';
        fetch(apiUrl)
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (!data) return;

                // draws the heat map layer...
                if (data.heatPoints && data.heatPoints.length > 0 && typeof L.heatLayer === 'function') {
                    var heat = L.heatLayer(data.heatPoints, {
                        radius: 25,
                        blur: 15,
                        maxZoom: 17,
                        max: 1.0,
                        gradient: { 0.4: 'blue', 0.65: 'lime', 0.8: 'orange', 1.0: 'red' }
                    }).addTo(map);
                }

                // draws boxes for hotspot cells with lots of incidents,,
                if (data.cells && data.cells.length > 0) {
                    data.cells.forEach(function (cell) {
                        var latLng = L.latLng(cell.cell_lat, cell.cell_lng);
                        var pt = L.Projection.SphericalMercator.project(latLng);

                        var sw = L.Projection.SphericalMercator.unproject(L.point(pt.x - 20, pt.y - 20));
                        var ne = L.Projection.SphericalMercator.unproject(L.point(pt.x + 20, pt.y + 20));

                        var rect = L.rectangle([sw, ne], {
                            color: '#b30000',
                            weight: 2,
                            fillColor: '#ff2200',
                            fillOpacity: 0.35
                        }).addTo(map);

                        var cats = (cell.categories || []).join(', ');
                        rect.bindPopup(
                            '<strong>Hotspot Cell</strong><br/>' +
                            'Incidents: <strong>' + cell.incident_count + '</strong><br/>' +
                            'Categories: ' + CampusMap.escapeHtml(cats)
                        );
                    });
                }
            })
            .catch(function (err) {
                console.warn("Hotspots load error:", err);
            });
    };

    // network editor mode to connect path nodes,,,
    CampusMap.setupNetworkMode = function (map, options) {
        var nodeMarkers = {};
        var linkLines = {};
        var selectedNodeId = null;

        function loadNetwork() {
            var apiUrl = (window.AppRoot || '') + 'Handlers/Api.ashx?action=network';
            fetch(apiUrl)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (!data) return;

                    // clears old nodes and lines...
                    Object.keys(nodeMarkers).forEach(function (k) { map.removeLayer(nodeMarkers[k]); });
                    Object.keys(linkLines).forEach(function (k) { map.removeLayer(linkLines[k]); });
                    nodeMarkers = {};
                    linkLines = {};
                    selectedNodeId = null;

                    var statusLine = document.getElementById('networkStatus');
                    if (statusLine && data.nodes) {
                        statusLine.textContent = 'Network loaded: ' + data.nodes.length + ' nodes, ' + (data.links ? data.links.length : 0) + ' links. Click map to add a node.';
                    }

                    // draws lines connecting nodes,,
                    if (data.links) {
                        data.links.forEach(function (link) {
                            var color = link.is_lit ? '#0066cc' : '#ff9900';
                            var line = L.polyline([
                                [link.from_lat, link.from_lng],
                                [link.to_lat, link.to_lng]
                            ], {
                                color: color,
                                weight: 4,
                                opacity: 0.8
                            }).addTo(map);

                            var popup = '<div style="min-width:140px;">' +
                                '<strong>Path Link</strong><br/>' +
                                'Length: ' + Math.round(link.length_metres) + ' m<br/>' +
                                'Lighting: ' + (link.is_lit ? '<span style="color:green;">Lit</span>' : '<span style="color:orange;">Unlit</span>') + '<br/>' +
                                '<button type="button" class="btn btn-secondary btn-sm" style="margin-top:6px; margin-right:4px;" onclick="window.CampusMapNetwork.toggleLit(\'' + link.link_id + '\')">Toggle Lit</button>' +
                                '<button type="button" class="btn btn-danger btn-sm" style="margin-top:6px;" onclick="window.CampusMapNetwork.deleteLink(\'' + link.link_id + '\')">Delete</button>' +
                                '</div>';

                            line.bindPopup(popup);
                            linkLines[link.link_id] = line;
                        });
                    }

                    // draws the node circles on the map...
                    if (data.nodes) {
                        data.nodes.forEach(function (node) {
                            var circle = L.circleMarker([node.latitude, node.longitude], {
                                radius: 7,
                                color: '#512D6D',
                                fillColor: '#ffffff',
                                fillOpacity: 0.9,
                                weight: 3
                            }).addTo(map);

                            circle.on('click', function (e) {
                                L.DomEvent.stopPropagation(e);
                                onNodeClick(node.node_id, [node.latitude, node.longitude]);
                            });

                            var nodePopup = '<div style="min-width:130px;">' +
                                '<strong>Node</strong>: ' + CampusMap.escapeHtml(node.name || 'Path Node') + '<br/>' +
                                '<button type="button" class="btn btn-danger btn-sm" style="margin-top:6px;" onclick="window.CampusMapNetwork.deleteNode(\'' + node.node_id + '\')">Delete Node</button>' +
                                '</div>';

                            circle.bindPopup(nodePopup);
                            nodeMarkers[node.node_id] = circle;
                        });
                    }
                })
                .catch(function (err) {
                    console.warn("Network load error:", err);
                });
        }

        // handles clicking on a node point...
        function onNodeClick(nodeId, latLng) {
            if (!selectedNodeId) {
                // first node clickd,,,
                selectedNodeId = nodeId;
                if (nodeMarkers[nodeId]) {
                    nodeMarkers[nodeId].setStyle({ fillColor: '#512D6D' });
                }
                var statusEl = document.getElementById('networkStatus');
                if (statusEl) statusEl.textContent = 'Node A selected. Click node B to create link, or click map to add a new node.';
            } else if (selectedNodeId === nodeId) {
                // deselect if same one clicked...
                if (nodeMarkers[nodeId]) {
                    nodeMarkers[nodeId].setStyle({ fillColor: '#ffffff' });
                }
                selectedNodeId = null;
                var statusEl = document.getElementById('networkStatus');
                if (statusEl) statusEl.textContent = 'Selection cleared.';
            } else {
                // second node clicked so connect them,,
                createLink(selectedNodeId, nodeId);
                if (nodeMarkers[selectedNodeId]) {
                    nodeMarkers[selectedNodeId].setStyle({ fillColor: '#ffffff' });
                }
                selectedNodeId = null;
            }
        }

        // sends api request to link nodes together...
        function createLink(fromId, toId) {
            var apiUrl = (window.AppRoot || '') + 'Handlers/Api.ashx?action=addLink';
            var fd = new FormData();
            fd.append('fromNode', fromId);
            fd.append('toNode', toId);

            fetch(apiUrl, { method: 'POST', body: fd })
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res && res.success) {
                        loadNetwork();
                    } else {
                        alert(res.message || "Failed to create link.");
                    }
                })
                .catch(function (e) { alert("Error adding link: " + e); });
        }

        // click on map to add a new point...
        map.on('click', function (e) {
            var lat = e.latlng.lat;
            var lng = e.latlng.lng;

            var name = prompt("Enter name for this path node (optional):", "Node");
            if (name === null) return;

            var apiUrl = (window.AppRoot || '') + 'Handlers/Api.ashx?action=addNode';
            var fd = new FormData();
            fd.append('latitude', lat.toFixed(6));
            fd.append('longitude', lng.toFixed(6));
            fd.append('name', name);

            fetch(apiUrl, { method: 'POST', body: fd })
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res && res.success) {
                        loadNetwork();
                    } else {
                        alert(res.message || "Failed to add node.");
                    }
                })
                .catch(function (e) { alert("Error adding node: " + e); });
        });

        // helper functions for popup buttons,,,
        window.CampusMapNetwork = {
            reload: loadNetwork,
            deleteNode: function (nodeId) {
                if (!confirm("Delete this node and all its connected links?")) return;
                var apiUrl = (window.AppRoot || '') + 'Handlers/Api.ashx?action=deleteNode';
                var fd = new FormData();
                fd.append('nodeId', nodeId);
                fetch(apiUrl, { method: 'POST', body: fd })
                    .then(function (r) { return r.json(); })
                    .then(function (res) {
                        if (res && res.success) loadNetwork();
                        else alert(res.message || "Failed to delete node.");
                    });
            },
            deleteLink: function (linkId) {
                if (!confirm("Delete this path link?")) return;
                var apiUrl = (window.AppRoot || '') + 'Handlers/Api.ashx?action=deleteLink';
                var fd = new FormData();
                fd.append('linkId', linkId);
                fetch(apiUrl, { method: 'POST', body: fd })
                    .then(function (r) { return r.json(); })
                    .then(function (res) {
                        if (res && res.success) loadNetwork();
                        else alert(res.message || "Failed to delete link.");
                    });
            },
            toggleLit: function (linkId) {
                var apiUrl = (window.AppRoot || '') + 'Handlers/Api.ashx?action=toggleLit';
                var fd = new FormData();
                fd.append('linkId', linkId);
                fetch(apiUrl, { method: 'POST', body: fd })
                    .then(function (r) { return r.json(); })
                    .then(function (res) {
                        if (res && res.success) loadNetwork();
                        else alert(res.message || "Failed to toggle lighting.");
                    });
            }
        };

        loadNetwork();
    };

    // safe routing mode to pick route between points...
    CampusMap.setupRouteMode = function (map, options) {
        var startMarker = null;
        var endMarker = null;
        var shortestLine = null;
        var saferLine = null;
        var connectorLines = [];

        var startCoord = null;
        var endCoord = null;

        var statusEl = document.getElementById('routeStatusText');
        var btnReset = document.getElementById('btnResetRoute');

        // shows unsafe areas so students know what routing avoids,,,
        CampusMap.loadRiskOverlay(map);

        function updateStatus(msg) {
            if (statusEl) statusEl.innerHTML = msg;
        }

        function clearConnectors() {
            connectorLines.forEach(function (l) { map.removeLayer(l); });
            connectorLines = [];
        }

        // round letter pin used for A and B markers,,,
        function makePinIcon(letter, color) {
            return L.divIcon({
                className: 'route-marker-pin',
                html: '<div style="background:' + color + '; color:#fff; border-radius:50%; width:28px; height:28px; display:flex; align-items:center; justify-content:center; font-weight:bold; box-shadow:0 2px 5px rgba(0,0,0,0.3);">' + letter + '</div>',
                iconSize: [28, 28],
                iconAnchor: [14, 14]
            });
        }

        // dotted lines from the A/B pins to where the walkway starts...
        function drawConnectors(routeCoords, aCoord, bCoord) {
            if (!routeCoords || !routeCoords.length) return;
            var style = { color: '#1b6ec2', weight: 3, dashArray: '2, 7', opacity: 0.9, interactive: false };
            if (aCoord) connectorLines.push(L.polyline([[aCoord.lat, aCoord.lng], routeCoords[0]], style).addTo(map));
            if (bCoord) connectorLines.push(L.polyline([routeCoords[routeCoords.length - 1], [bCoord.lat, bCoord.lng]], style).addTo(map));
        }

        function calculateRoute() {
            if (!startCoord || !endCoord) return;

            updateStatus('Calculating safest route...');
            var url = (window.AppRoot || '') + 'Handlers/Api.ashx?action=route' +
                '&startLat=' + startCoord.lat.toFixed(6) +
                '&startLng=' + startCoord.lng.toFixed(6) +
                '&endLat=' + endCoord.lat.toFixed(6) +
                '&endLng=' + endCoord.lng.toFixed(6);

            fetch(url)
                .then(function (res) { return res.json(); })
                .then(function (data) {
                    if (data.ErrorMessage) {
                        updateStatus('<span style="color:#dc3545;">' + CampusMap.escapeHtml(data.ErrorMessage) + '</span>');
                        return;
                    }

                    if (shortestLine) map.removeLayer(shortestLine);
                    if (saferLine) map.removeLayer(saferLine);
                    clearConnectors();

                    // draws shortest path line,,,
                    if (data.Shortest && data.Shortest.Found && data.Shortest.Coordinates) {
                        shortestLine = L.polyline(data.Shortest.Coordinates, {
                            color: '#6c757d',
                            weight: 4,
                            dashArray: '6, 8',
                            opacity: 0.85
                        }).addTo(map);
                    }

                    // draws safer route path...
                    if (data.Safer && data.Safer.Found && data.Safer.Coordinates) {
                        saferLine = L.polyline(data.Safer.Coordinates, {
                            color: '#512D6D',
                            weight: 5,
                            opacity: 0.95
                        }).addTo(map);

                        // dotted walk-up lines from the pins onto the path network,,,
                        drawConnectors(data.Safer.Coordinates, startCoord, endCoord);

                        // zooms map to fit the route and both pins,,
                        var group = L.featureGroup([saferLine]);
                        if (shortestLine) group.addLayer(shortestLine);
                        if (startMarker) group.addLayer(startMarker);
                        if (endMarker) group.addLayer(endMarker);
                        map.fitBounds(group.getBounds(), { padding: [40, 40] });
                    }

                    // updates walking times and numbers on page...
                    var sDist = document.getElementById('shortestDist');
                    var sTime = document.getElementById('shortestTime');
                    var rDist = document.getElementById('saferDist');
                    var rTime = document.getElementById('saferTime');
                    var rRisk = document.getElementById('saferRisk');

                    if (data.Shortest && data.Shortest.Found) {
                        if (sDist) sDist.textContent = data.Shortest.DistanceMetres + ' m';
                        if (sTime) sTime.textContent = data.Shortest.WalkingMinutes + ' min';
                    } else {
                        if (sDist) sDist.textContent = 'Path not found';
                        if (sTime) sTime.textContent = '-';
                    }

                    if (data.Safer && data.Safer.Found) {
                        if (rDist) rDist.textContent = data.Safer.DistanceMetres + ' m';
                        if (rTime) rTime.textContent = data.Safer.WalkingMinutes + ' min';
                        if (rRisk) rRisk.textContent = 'Risk score: ' + data.Safer.RiskScore;
                        updateStatus('<span style="color:#198754;">Route calculated successfully!</span>');

                        var hfPath = document.getElementById('hfNodePathJson');
                        var hfLen = document.getElementById('hfRouteLength');
                        if (hfPath && data.Safer.NodeIds) hfPath.value = JSON.stringify(data.Safer.NodeIds);
                        if (hfLen) hfLen.value = data.Safer.DistanceMetres;
                    } else {
                        if (rDist) rDist.textContent = 'Path not found';
                        if (rTime) rTime.textContent = '-';
                        if (rRisk) rRisk.textContent = '';
                        updateStatus('<span style="color:#dc3545;">No viable path between these points on the network.</span>');
                    }
                })
                .catch(function (err) {
                    updateStatus('<span style="color:#dc3545;">Error computing route: ' + CampusMap.escapeHtml(err) + '</span>');
                });
        }

        // calculates custom route from form...
        window.routeCalculateCustom = function (startLat, startLng, endLat, endLng) {
            if (startMarker) map.removeLayer(startMarker);
            if (endMarker) map.removeLayer(endMarker);
            if (shortestLine) map.removeLayer(shortestLine);
            if (saferLine) map.removeLayer(saferLine);
            clearConnectors();

            startCoord = L.latLng(startLat, startLng);
            endCoord = L.latLng(endLat, endLng);

            startMarker = L.marker(startCoord, { title: 'Start', icon: makePinIcon('A', '#198754') }).addTo(map);
            endMarker = L.marker(endCoord, { title: 'End', icon: makePinIcon('B', '#512D6D') }).addTo(map);

            calculateRoute();
        };

        // renders saved route data on map,,,
        window.renderCustomRouteData = function (pkg) {
            if (!pkg || !pkg.result) return;
            var res = pkg.result;
            if (shortestLine) map.removeLayer(shortestLine);
            if (saferLine) map.removeLayer(saferLine);
            clearConnectors();

            // restores the A and B pins for saved routes,,,
            if (typeof pkg.startLat === 'number' && typeof pkg.startLng === 'number') {
                startCoord = L.latLng(pkg.startLat, pkg.startLng);
                endCoord = L.latLng(pkg.endLat, pkg.endLng);
                if (startMarker) map.removeLayer(startMarker);
                if (endMarker) map.removeLayer(endMarker);
                startMarker = L.marker(startCoord, { title: 'Start', icon: makePinIcon('A', '#198754') }).addTo(map);
                endMarker = L.marker(endCoord, { title: 'End', icon: makePinIcon('B', '#512D6D') }).addTo(map);
            }

            if (res.Shortest && res.Shortest.Coordinates) {
                shortestLine = L.polyline(res.Shortest.Coordinates, {
                    color: '#6c757d',
                    weight: 4,
                    dashArray: '6, 8',
                    opacity: 0.85
                }).addTo(map);
            }

            if (res.Safer && res.Safer.Coordinates) {
                saferLine = L.polyline(res.Safer.Coordinates, {
                    color: '#512D6D',
                    weight: 5,
                    opacity: 0.95
                }).addTo(map);

                drawConnectors(res.Safer.Coordinates, startCoord, endCoord);

                var group = L.featureGroup([saferLine]);
                if (shortestLine) group.addLayer(shortestLine);
                if (startMarker) group.addLayer(startMarker);
                if (endMarker) group.addLayer(endMarker);
                map.fitBounds(group.getBounds(), { padding: [40, 40] });
            }

            var sDist = document.getElementById('shortestDist');
            var sTime = document.getElementById('shortestTime');
            var rDist = document.getElementById('saferDist');
            var rTime = document.getElementById('saferTime');
            var rRisk = document.getElementById('saferRisk');

            if (res.Shortest && res.Shortest.Found) {
                if (sDist) sDist.textContent = res.Shortest.DistanceMetres + ' m';
                if (sTime) sTime.textContent = res.Shortest.WalkingMinutes + ' min';
            }
            if (res.Safer && res.Safer.Found) {
                if (rDist) rDist.textContent = res.Safer.DistanceMetres + ' m';
                if (rTime) rTime.textContent = res.Safer.WalkingMinutes + ' min';
                if (rRisk) rRisk.textContent = 'Risk score: ' + res.Safer.RiskScore;
            }
        };

        // click on map to set start and end spots...
        map.on('click', function (e) {
            if (!startCoord) {
                startCoord = e.latlng;
                startMarker = L.marker(startCoord, { title: 'Start', icon: makePinIcon('A', '#198754') }).addTo(map);
                updateStatus('Start point (A) selected. Now click destination (B).');
            } else if (!endCoord) {
                endCoord = e.latlng;
                endMarker = L.marker(endCoord, { title: 'End', icon: makePinIcon('B', '#512D6D') }).addTo(map);

                calculateRoute();
            }
        });

        // resets the selected points and paths,,
        if (btnReset) {
            btnReset.addEventListener('click', function () {
                if (startMarker) map.removeLayer(startMarker);
                if (endMarker) map.removeLayer(endMarker);
                if (shortestLine) map.removeLayer(shortestLine);
                if (saferLine) map.removeLayer(saferLine);
                clearConnectors();
                startMarker = null;
                endMarker = null;
                shortestLine = null;
                saferLine = null;
                startCoord = null;
                endCoord = null;
                updateStatus('Select campus locations above or click map to set Start point (A).');

                var sDist = document.getElementById('shortestDist');
                var sTime = document.getElementById('shortestTime');
                var rDist = document.getElementById('saferDist');
                var rTime = document.getElementById('saferTime');
                var rRisk = document.getElementById('saferRisk');
                if (sDist) sDist.textContent = '-';
                if (sTime) sTime.textContent = '-';
                if (rDist) rDist.textContent = '-';
                if (rTime) rTime.textContent = '-';
                if (rRisk) rRisk.textContent = '';

                var hfPath = document.getElementById('hfNodePathJson');
                var hfLen = document.getElementById('hfRouteLength');
                var hfFrom = document.getElementById('hfFromLocId');
                var hfTo = document.getElementById('hfToLocId');
                var ddlFrom = document.getElementById('ddlFromLocation');
                var ddlTo = document.getElementById('ddlToLocation');
                if (hfPath) hfPath.value = '';
                if (hfLen) hfLen.value = '';
                if (hfFrom) hfFrom.value = '';
                if (hfTo) hfTo.value = '';
                if (ddlFrom) ddlFrom.selectedIndex = 0;
                if (ddlTo) ddlTo.selectedIndex = 0;
            });
        }
    };

    window.CampusMap = CampusMap;

})(window);
