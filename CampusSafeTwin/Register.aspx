<%@ Page Title="Register Student - CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Register.aspx.cs" Inherits="CampusSafeTwin.Register" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .auth-card {
            max-width: 480px;
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
        <h1>Student Registration</h1>
        <p class="subtitle">Join CampusSafe Twin to report incidents and access safe walking routes</p>

        <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger">
            <asp:Literal ID="litError" runat="server" />
        </asp:Panel>

        <div class="form-group">
            <label for="txtFullName">Full Name</label>
            <asp:TextBox ID="txtFullName" runat="server" CssClass="form-control" placeholder="e.g. Lerato Khumalo" required="required" />
        </div>

        <div class="form-group">
            <label for="txtEmail">University / Student Email</label>
            <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" TextMode="Email" placeholder="student@nwualum.co.za" required="required" />
        </div>

        <div class="form-group">
            <label for="txtStudentNumber">Student Number</label>
            <asp:TextBox ID="txtStudentNumber" runat="server" CssClass="form-control" placeholder="e.g. 34918274" required="required" />
        </div>

        <div class="form-group">
            <label for="txtPassword">Password</label>
            <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="Choose a secure password" required="required" />
        </div>

        <div class="form-group">
            <label for="txtConfirmPassword">Confirm Password</label>
            <asp:TextBox ID="txtConfirmPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="Confirm your password" required="required" />
        </div>

        <div style="margin-top: 24px;">
            <asp:Button ID="btnRegister" runat="server" Text="Create Student Account" CssClass="btn btn-primary" Width="100%" OnClick="btnRegister_Click" />
        </div>

        <div class="auth-footer">
            <p>Already have an account? <a href="<%= ResolveUrl("~/Login.aspx") %>" style="color:#512D6D; font-weight:600;">Sign in here</a></p>
        </div>
    </div>
</asp:Content>
