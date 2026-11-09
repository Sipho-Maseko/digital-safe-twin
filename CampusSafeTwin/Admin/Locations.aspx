<%@ Page Title="Campus Locations - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Locations.aspx.cs" Inherits="CampusSafeTwin.Admin.Locations" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .locations-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 24px;
        }
        @media (max-width: 860px) {
            .locations-grid {
                grid-template-columns: 1fr;
            }
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
        <div>
            <h1>Campus Locations & Landmarks</h1>
            <p class="subtitle">Manage Points of Interest (POIs) used for Safe Route departure/destination points and hotspot references</p>
        </div>
        <div>
            <asp:Button ID="btnSeedDefaults" runat="server" Text="Seed Standard Campus POIs" CssClass="btn btn-secondary btn-sm" OnClick="btnSeedDefaults_Click" />
        </div>
    </div>

    <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litSuccess" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <div class="locations-grid">
        <!-- list of current places,,, -->
        <div>
            <div class="card" style="padding:16px;">
                <h3 style="margin-bottom:12px;">Active Campus Landmarks (<asp:Literal ID="litCount" runat="server" Text="0" />)</h3>
                
                <asp:GridView ID="gvLocations" runat="server" AutoGenerateColumns="False" CssClass="table" DataKeyNames="location_id" OnRowDeleting="gvLocations_RowDeleting" EmptyDataText="No campus locations configured yet. Add one or click 'Seed Standard Campus POIs'.">
                    <Columns>
                        <asp:BoundField DataField="name" HeaderText="Name" />
                        <asp:BoundField DataField="kind" HeaderText="Category" />
                        <asp:TemplateField HeaderText="Coordinates">
                            <ItemTemplate>
                                <small><%# Convert.ToDouble(Eval("latitude")).ToString("F5") %>, <%# Convert.ToDouble(Eval("longitude")).ToString("F5") %></small>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:LinkButton ID="btnDelete" runat="server" CommandName="Delete" Text="Delete" CssClass="btn btn-danger btn-sm" OnClientClick="return confirm('Delete this campus location?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

        <!-- add a place form... -->
        <div>
            <div class="card" style="padding:16px;">
                <h3 style="margin-bottom:12px;">Add New Campus POI</h3>
                
                <div class="form-group">
                    <label for="txtName">Location / Building Name:</label>
                    <asp:TextBox ID="txtName" runat="server" CssClass="form-control" placeholder="e.g. Ferdinand Postma Library" required="required" />
                </div>

                <div class="form-group">
                    <label for="ddlKind">Category:</label>
                    <asp:DropDownList ID="ddlKind" runat="server" CssClass="form-control">
                        <asp:ListItem Value="building" Text="Academic / Building" />
                        <asp:ListItem Value="gate" Text="Security Gate / Access" />
                        <asp:ListItem Value="residence" Text="Student Residence" />
                        <asp:ListItem Value="parking" Text="Parking Lot" />
                        <asp:ListItem Value="sports" Text="Sports Field / Complex" />
                        <asp:ListItem Value="other" Text="Other Landmark" />
                    </asp:DropDownList>
                </div>

                <div class="form-group">
                    <label for="txtDescription">Description (Optional):</label>
                    <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control" placeholder="e.g. North campus gate opposite student centre" />
                </div>

                <div class="form-row">
                    <div class="form-col">
                        <div class="form-group">
                            <label for="txtLat">Latitude:</label>
                            <asp:TextBox ID="txtLat" runat="server" ClientIDMode="Static" CssClass="form-control" required="required" placeholder="-26.6890" />
                        </div>
                    </div>
                    <div class="form-col">
                        <div class="form-group">
                            <label for="txtLng">Longitude:</label>
                            <asp:TextBox ID="txtLng" runat="server" ClientIDMode="Static" CssClass="form-control" required="required" placeholder="27.0935" />
                        </div>
                    </div>
                </div>

                <div style="font-size:0.85rem; color:#666; margin-bottom:8px;">
                    Click anywhere on the mini map to auto-fill latitude and longitude:
                </div>
                <div id="locPickerMap" class="map-frame map-frame-sm" style="height:220px; margin-bottom:16px;"></div>

                <asp:Button ID="btnAddLocation" runat="server" Text="Save Campus Location" CssClass="btn btn-primary" Width="100%" OnClick="btnAddLocation_Click" />
            </div>
        </div>
    </div>

    <script>
        document.addEventListener("DOMContentLoaded", function () {
            // shared satelite campus map opens framed on nwu potch,,,
            var map = CampusMap.createBaseMap('locPickerMap');

            var marker = null;
            // click on map puts pin and sets cordinates,,,
            map.on('click', function (e) {
                var lat = e.latlng.lat;
                var lng = e.latlng.lng;
                document.getElementById('txtLat').value = lat.toFixed(6);
                document.getElementById('txtLng').value = lng.toFixed(6);
                if (marker) {
                    marker.setLatLng(e.latlng);
                } else {
                    marker = L.marker(e.latlng).addTo(map);
                }
            });
        });
    </script>
</asp:Content>
