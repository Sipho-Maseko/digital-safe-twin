<%@ Page Title="Report Incident - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Report.aspx.cs" Inherits="CampusSafeTwin.Student.Report" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .report-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 24px;
        }
        @media (max-width: 860px) {
            .report-grid {
                grid-template-columns: 1fr;
            }
        }
        .pin-instruction {
            padding: 8px 12px;
            background: #f7f3fa;
            border-left: 4px solid var(--nwu-purple);
            margin-bottom: 12px;
            font-size: 0.9rem;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Report a Safety Incident</h1>
    <p class="subtitle">Pin the location on the campus map and provide incident details to notify campus safety</p>

    <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litSuccess" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <div class="report-grid">
        <!-- form inputs column,,, -->
        <div class="report-form-pane">
            <div class="form-group">
                <label for="ddlCategory">Incident Category</label>
                <asp:DropDownList ID="ddlCategory" runat="server" CssClass="form-control" required="required">
                    <asp:ListItem Value="" Text="-- Select Category --" />
                    <asp:ListItem Value="theft" Text="Theft" />
                    <asp:ListItem Value="assault" Text="Assault" />
                    <asp:ListItem Value="harassment" Text="Harassment" />
                    <asp:ListItem Value="suspicious_activity" Text="Suspicious Activity" />
                    <asp:ListItem Value="vandalism" Text="Vandalism" />
                    <asp:ListItem Value="medical" Text="Medical Emergency" />
                    <asp:ListItem Value="other" Text="Other Incident" />
                </asp:DropDownList>
            </div>

            <div class="form-row">
                <div class="form-col">
                    <div class="form-group">
                        <label for="txtOccurredDate">Date Happened</label>
                        <asp:TextBox ID="txtOccurredDate" runat="server" CssClass="form-control" TextMode="Date" required="required" />
                    </div>
                </div>
                <div class="form-col">
                    <div class="form-group">
                        <label for="txtOccurredTime">Time Happened</label>
                        <asp:TextBox ID="txtOccurredTime" runat="server" CssClass="form-control" TextMode="Time" required="required" />
                    </div>
                </div>
            </div>

            <div class="form-group">
                <label for="txtDescription">Incident Description</label>
                <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="4" 
                    placeholder="Describe what occurred, any individuals involved, and relevant surroundings..." required="required" />
            </div>

            <div class="form-group">
                <label>Pinned Location</label>
                <div style="font-size:0.92rem; color:#444; background:#f4f6f9; padding:8px 12px; border-radius:4px; border:1px solid #ddd;">
                    Selected Coordinates: <strong id="lblSelectedCoords">Not set, click map to pin</strong>
                </div>
                <asp:HiddenField ID="hfLat" runat="server" ClientIDMode="Static" />
                <asp:HiddenField ID="hfLng" runat="server" ClientIDMode="Static" />
            </div>

            <div class="form-group">
                <label for="fuPhotos">Evidence Photos (Up to 3, JPG/PNG, max 5 MB each):</label>
                <asp:FileUpload ID="fuPhotos" runat="server" AllowMultiple="true" accept="image/jpeg,image/png" CssClass="form-control" />
                <small style="color:#666;">Optional photo evidence attached securely to your incident report.</small>
            </div>

            <div style="margin-top: 24px;">
                <asp:Button ID="btnSubmitReport" runat="server" Text="Submit Incident Report" CssClass="btn btn-primary" Width="100%" OnClick="btnSubmitReport_Click" />
            </div>
        </div>

        <!-- map column to drop pin... -->
        <div class="report-map-pane">
            <div class="pin-instruction">
                <strong>Map Location:</strong> Click anywhere on the campus map to drop or move your incident pin.
            </div>
            <div id="reportMap" class="map-frame"></div>
        </div>
    </div>

    <script>
        document.addEventListener("DOMContentLoaded", function () {
            // initializes the pin picker on satelite map...
            CampusMap.init('reportMap', 'pick');
        });
    </script>
</asp:Content>
