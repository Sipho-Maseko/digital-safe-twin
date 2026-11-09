using System;
using System.Web.UI;
using CampusSafeTwin.Classes;

namespace CampusSafeTwin.Student
{
    public partial class Sos : Page
    {
        // makes sure only students can access panic button...
        protected void Page_Load(object sender, EventArgs e)
        {
            Auth.Require(this, "student");
        }
    }
}
