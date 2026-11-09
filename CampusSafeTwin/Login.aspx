<%@ Page Title="Login - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="CampusSafeTwin.Login" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .auth-card {
            max-width: 440px;
            margin: 30px auto;
            padding: 30px;
            background: #ffffff;
            border-radius: 8px;
            border: 1px solid #e1d8eb;
            box-shadow: 0 4px 14px rgba(81, 45, 109, 0.08);
        }
        .auth-card h1 {
            font-size: 1.5rem;
            margin-bottom: 8px;
            text-align: center;
        }
        .auth-card .subtitle {
            text-align: center;
            margin-bottom: 24px;
        }
        .auth-footer {
            margin-top: 20px;
            text-align: center;
            font-size: 0.9rem;
            color: #666;
        }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div class="auth-card">
        <div style="text-align: center; margin-bottom: 16px;">
            <img src="<%= ResolveUrl("~/Images/nwu-logo.jpg") %>" alt="NWU CampusSafe" style="height: 60px; width: 60px; border-radius: 12px; object-fit: cover; box-shadow: 0 4px 12px rgba(81, 45, 109, 0.15);" />
        </div>
        <h1>CampusSafe Login</h1>
        <p class="subtitle">Access campus security and incident reporting</p>

        <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
            <asp:Literal ID="litError" runat="server" />
        </asp:Panel>

        <asp:Panel ID="pnlSuccess" runat="server" Visible="false" CssClass="alert alert-success">
            <asp:Literal ID="litSuccess" runat="server" />
        </asp:Panel>

        <div class="form-group">
            <label for="txtEmail">Email Address</label>
            <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" TextMode="Email" placeholder="student@nwualum.co.za or admin@campussafe.ac.za" required="required" />
        </div>

        <div class="form-group">
            <label for="txtPassword">Password</label>
            <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="Enter your password" required="required" />
        </div>

        <div style="margin-top: 24px;">
            <asp:Button ID="btnLogin" runat="server" Text="Sign In" CssClass="btn btn-primary" Width="100%" OnClick="btnLogin_Click" />
        </div>

        <div class="auth-footer">
            <p>New student? <a href="<%= ResolveUrl("~/Register.aspx") %>" style="color:#512D6D; font-weight:600;">Create student account</a></p>
        </div>
    </div>
</asp:Content>
