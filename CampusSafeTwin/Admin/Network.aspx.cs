using System;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Admin
{
    public partial class Network : Page
    {
        // cheks if the person logged in is a supervisor...
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "supervisor");
        }
    }
}
