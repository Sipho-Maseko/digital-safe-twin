using System;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Admin
{
    public partial class LiveOps : Page
    {
        // cheks if the logged in person is a supervisor,,,
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "supervisor");
        }
    }
}
