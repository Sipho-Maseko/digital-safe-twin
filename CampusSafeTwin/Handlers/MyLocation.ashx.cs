using System;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using CampusSafeTwin.Classes;
using Npgsql;

namespace CampusSafeTwin.Handlers
{
    public class MyLocation : IHttpHandler, IRequiresSessionState
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            string userId = context.Session?["UserId"] as string;
            string role = (context.Session?["Role"] as string) ?? "";

            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out Guid userGuid))
            {
                context.Response.StatusCode = 401;
                context.Response.Write(Serializer.Serialize(new { error = true, message = "Unauthorized." }));
                return;
            }

            // students can only post location if sos is open,,,
            if (string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
            {
                const string checkSosSql = @"
                    SELECT COUNT(*) 
                    FROM incidents 
                    WHERE reporter_id = @me 
                      AND is_emergency = true 
                      AND status <> 'Resolved';";

                long openSos = Convert.ToInt64(Db.ExecuteScalar(checkSosSql, Db.Param("@me", userGuid)));
                if (openSos == 0)
                {
                    context.Response.StatusCode = 403;
                    context.Response.Write(Serializer.Serialize(new { error = true, message = "Students may only post location during an active SOS." }));
                    return;
                }
            }

            string latStr = context.Request["lat"];
            string lngStr = context.Request["lng"];
            string accStr = context.Request["accuracy"];
            string onDutyStr = context.Request["is_on_duty"];

            if (!Geo.TryParseCoordinate(latStr, out double lat) || !Geo.TryParseCoordinate(lngStr, out double lng))
            {
                context.Response.StatusCode = 400;
                context.Response.Write(Serializer.Serialize(new { error = true, message = "Invalid lat/lng coordinates." }));
                return;
            }

            double? accuracy = null;
            if (double.TryParse(accStr, out double acc)) accuracy = acc;

            bool isOnDuty = false;
            if (!string.IsNullOrEmpty(onDutyStr))
            {
                bool.TryParse(onDutyStr, out isOnDuty);
            }

            // only security staff can ever be on duty,,,
            if (string.Equals(role, "student", StringComparison.OrdinalIgnoreCase))
            {
                isOnDuty = false;
            }

            // marks guards off duty if inactive for 10 minutes...
            const string housekeepingSql = @"
                UPDATE user_last_location 
                SET is_on_duty = false 
                WHERE is_on_duty = true 
                  AND updated_at < NOW() - INTERVAL '10 minutes';";
            Db.ExecuteNonQuery(housekeepingSql);

            // saves or updates location in db,,,
            const string upsertSql = @"
                INSERT INTO user_last_location (user_id, latitude, longitude, accuracy_m, is_on_duty, updated_at)
                VALUES (@uid, @lat, @lng, @acc, @onDuty, NOW())
                ON CONFLICT (user_id) DO UPDATE SET
                    latitude = EXCLUDED.latitude,
                    longitude = EXCLUDED.longitude,
                    accuracy_m = EXCLUDED.accuracy_m,
                    is_on_duty = EXCLUDED.is_on_duty,
                    updated_at = NOW();";

            Db.ExecuteNonQuery(upsertSql,
                Db.Param("@uid", userGuid),
                Db.Param("@lat", lat),
                Db.Param("@lng", lng),
                Db.Param("@acc", (object)accuracy ?? DBNull.Value),
                Db.Param("@onDuty", isOnDuty));

            context.Response.Write(Serializer.Serialize(new
            {
                success = true,
                is_on_duty = isOnDuty,
                updated_at = DateTime.UtcNow.ToString("o")
            }));
        }
    }
}
