<%@ Page Title="Data Simulation - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Simulation.aspx.cs" Inherits="CampusSafeTwin.Admin.Simulation" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .sim-grid {
            display: grid;
            grid-template-columns: 360px 1fr;
            gap: 24px;
        }
        @media (max-width: 860px) {
            .sim-grid {
                grid-template-columns: 1fr;
            }
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
        <div>
            <h1>Simulation & Demo Data Generator</h1>
            <p class="subtitle">Create demo incident batches and test data for map testing</p>
        </div>
        <div>
            <asp:Button ID="btnPurgeAllDemo" runat="server" Text="Delete All Demo Batches" CssClass="btn btn-danger btn-sm" OnClick="btnPurgeAllDemo_Click" OnClientClick="return confirm('Delete ALL seeded simulation batches and their incidents?');" />
        </div>
    </div>

    <!-- helpful info for admins... -->
    <div style="background:#eef6ff; border-left:4px solid #0d6efd; color:#084298; padding:12px 16px; margin-bottom:20px; border-radius:4px; font-size:0.92rem; line-height:1.5;">
        <strong>What is this tool?</strong> This is an administrator testing utility. Instead of manually reporting dozens of test incidents one by one to verify hotspots, heatmaps, and response statistics, you can generate simulated batches of campus incidents in one click. All generated batches can be removed at any time with <strong>Delete All Demo Batches</strong> without affecting real user reports.
    </div>

    <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litSuccess" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <div class="sim-grid">
        <!-- controls to generate test data,,, -->
        <div>
            <div class="card" style="padding:18px;">
                <h3 style="margin-bottom:14px; color:var(--nwu-purple);">Generate Batch</h3>

                <div class="form-group">
                    <label for="txtBatchName">Batch Name / Label:</label>
                    <asp:TextBox ID="txtBatchName" runat="server" CssClass="form-control" Text="Potch Campus Demo Run #1" required="required" />
                </div>

                <div class="form-group">
                    <label for="txtSeed">Random Seed Number:</label>
                    <asp:TextBox ID="txtSeed" runat="server" CssClass="form-control" Text="1337" TextMode="Number" required="required" />
                    <small style="color:#666;">Using the same number creates the same incident spots and times.</small>
                </div>

                <div class="form-group">
                    <label for="txtCount">Incident Count:</label>
                    <asp:TextBox ID="txtCount" runat="server" CssClass="form-control" Text="25" TextMode="Number" min="5" max="100" required="required" />
                </div>

                <div class="form-group">
                    <label for="ddlTimespan">Time Span:</label>
                    <asp:DropDownList ID="ddlTimespan" runat="server" CssClass="form-control">
                        <asp:ListItem Value="7" Text="Past 7 Days" />
                        <asp:ListItem Value="14" Text="Past 14 Days" Selected="True" />
                        <asp:ListItem Value="30" Text="Past 30 Days" />
                    </asp:DropDownList>
                </div>

                <div style="margin-top:20px;">
                    <asp:Button ID="btnGenerate" runat="server" Text="Generate Simulation Data" CssClass="btn btn-primary" Width="100%" OnClick="btnGenerate_Click" />
                </div>
            </div>
        </div>

        <!-- list of created batches... -->
        <div>
            <div class="card" style="padding:18px;">
                <h3 style="margin-bottom:14px;">Active Simulation Batches</h3>
                
                <asp:GridView ID="gvBatches" runat="server" AutoGenerateColumns="False" CssClass="table" DataKeyNames="seed_id" OnRowDeleting="gvBatches_RowDeleting" EmptyDataText="No simulation batches exist yet. Click 'Generate Simulation Data' to create demo records.">
                    <Columns>
                        <asp:BoundField DataField="name" HeaderText="Batch Label" />
                        <asp:BoundField DataField="seed_value" HeaderText="Seed" />
                        <asp:BoundField DataField="incident_count" HeaderText="Incidents" />
                        <asp:BoundField DataField="generated_fmt" HeaderText="Created" />
                        <asp:TemplateField HeaderText="Actions">
                            <ItemTemplate>
                                <asp:LinkButton ID="btnDelete" runat="server" CommandName="Delete" Text="Remove Batch" CssClass="btn btn-danger btn-sm" OnClientClick="return confirm('Delete this simulation batch and all its incidents?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    </div>
</asp:Content>
