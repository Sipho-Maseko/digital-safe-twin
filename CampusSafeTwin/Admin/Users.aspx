<%@ Page Title="User Management - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Users.aspx.cs" Inherits="CampusSafeTwin.Admin.Users" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .users-layout {
            display: grid;
            grid-template-columns: 1fr 340px;
            gap: 24px;
        }
        @media (max-width: 900px) {
            .users-layout {
                grid-template-columns: 1fr;
            }
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
        <div>
            <h1>User Accounts & Staff Access</h1>
            <p class="subtitle">Manage campus security guards, supervisors, and student accounts, role permissions, and access status</p>
        </div>
    </div>

    <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
        <asp:Literal ID="litSuccess" runat="server" />
    </asp:Panel>

    <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
        <asp:Literal ID="litError" runat="server" />
    </asp:Panel>

    <div class="users-layout">
        <!-- list of all users in system... -->
        <div>
            <div class="card" style="padding:18px;">
                <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
                    <h3>Registered Accounts (<asp:Literal ID="litUserCount" runat="server" Text="0" />)</h3>
                    <div>
                        <asp:DropDownList ID="ddlRoleFilter" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlRoleFilter_SelectedIndexChanged" CssClass="form-control" style="font-size:0.85rem; padding:4px 8px;">
                            <asp:ListItem Value="" Text="All Roles" />
                            <asp:ListItem Value="guard" Text="Guards Only" />
                            <asp:ListItem Value="supervisor" Text="Supervisors Only" />
                            <asp:ListItem Value="student" Text="Students Only" />
                        </asp:DropDownList>
                    </div>
                </div>

                <asp:GridView ID="gvUsers" runat="server" AutoGenerateColumns="False" CssClass="table" DataKeyNames="user_id" OnRowCommand="gvUsers_RowCommand" EmptyDataText="No users found.">
                    <Columns>
                        <asp:BoundField DataField="full_name" HeaderText="Full Name" />
                        <asp:BoundField DataField="email" HeaderText="Email" />
                        <asp:TemplateField HeaderText="Role">
                            <ItemTemplate>
                                <span class="status-badge status-<%# Eval("role").ToString().ToLower() %>"><%# Eval("role") %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Identifier">
                            <ItemTemplate>
                                <%# Eval("badge_number") != DBNull.Value && !string.IsNullOrEmpty(Eval("badge_number").ToString()) ? "Badge: " + Eval("badge_number") : (Eval("student_number") != DBNull.Value ? "Std: " + Eval("student_number") : "-") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Active Status">
                            <ItemTemplate>
                                <span style='color: <%# Convert.ToBoolean(Eval("is_active")) ? "#198754" : "#dc3545" %>; font-weight:600;'>
                                    <%# Convert.ToBoolean(Eval("is_active")) ? "Active" : "Deactivated" %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Actions">
                            <ItemTemplate>
                                <asp:LinkButton ID="btnToggleActive" runat="server" CommandName="ToggleActive" CommandArgument='<%# Eval("user_id") %>' Text='<%# Convert.ToBoolean(Eval("is_active")) ? "Deactivate" : "Activate" %>' CssClass='<%# Convert.ToBoolean(Eval("is_active")) ? "btn btn-danger btn-sm" : "btn btn-secondary btn-sm" %>' />
                                <asp:LinkButton ID="btnResetPass" runat="server" CommandName="ResetPass" CommandArgument='<%# Eval("user_id") %>' Text="Reset PW" CssClass="btn btn-secondary btn-sm" OnClientClick="return confirm('Reset this user\'s password to CampusSafe2026!?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

        <!-- form to add new account,,, -->
        <div>
            <div class="card" style="padding:18px;">
                <h3 style="margin-bottom:14px; color:var(--nwu-purple);">Create New Account</h3>

                <div class="form-group">
                    <label for="txtFullName">Full Name:</label>
                    <asp:TextBox ID="txtFullName" runat="server" CssClass="form-control" placeholder="e.g. Officer John Smith" required="required" />
                </div>

                <div class="form-group">
                    <label for="txtEmail">Email Address:</label>
                    <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" TextMode="Email" placeholder="officer@nwu.ac.za" required="required" />
                </div>

                <div class="form-group">
                    <label for="ddlRole">Role:</label>
                    <asp:DropDownList ID="ddlRole" runat="server" CssClass="form-control">
                        <asp:ListItem Value="guard" Text="Patrol Guard" Selected="True" />
                        <asp:ListItem Value="supervisor" Text="Control Supervisor" />
                        <asp:ListItem Value="student" Text="Student" />
                    </asp:DropDownList>
                </div>

                <div class="form-group">
                    <label for="txtBadge">Badge Number / Student ID:</label>
                    <asp:TextBox ID="txtBadge" runat="server" CssClass="form-control" placeholder="e.g. NWU-G44" />
                </div>

                <div class="form-group">
                    <label for="txtPhone">Phone Number:</label>
                    <asp:TextBox ID="txtPhone" runat="server" CssClass="form-control" placeholder="018 299 1111" />
                </div>

                <div class="form-group">
                    <label for="txtPassword">Temporary Password:</label>
                    <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" Text="CampusSafe2026!" required="required" />
                </div>

                <div style="margin-top:20px;">
                    <asp:Button ID="btnCreateUser" runat="server" Text="Create Account" CssClass="btn btn-primary" Width="100%" OnClick="btnCreateUser_Click" />
                </div>
            </div>
        </div>
    </div>
</asp:Content>
