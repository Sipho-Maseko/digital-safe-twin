<%@ Page Title="CampusSafe Twin" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="CampusSafeTwin.Default" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <div style="text-align: center; padding: 40px 20px;">
        <img src="<%= ResolveUrl("~/Images/nwu-logo.jpg") %>" alt="NWU CampusSafe" style="height: 72px; width: 72px; border-radius: 14px; margin-bottom: 18px; object-fit: cover; box-shadow: 0 4px 14px rgba(81, 45, 109, 0.18);" />
        <h2>Redirecting to your dashboard...</h2>
        <p class="subtitle">Please wait while CampusSafe Twin directs you to your role workspace.</p>
    </div>
</asp:Content>
