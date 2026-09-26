using System.Web.Http;

namespace Michaelhouse
{
    /// <summary>
    /// Web API Configuration
    /// 
    /// This configures how the REST API works:
    /// - Enables attribute routing (so we can use [Route("api/...")])
    /// - Returns JSON by default (not XML)
    /// - Sets up the default API route
    /// </summary>
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Enable attribute routing - allows [Route("api/menu/today")] style
            config.MapHttpAttributeRoutes();

            // Default API route (fallback for [Route] attributes)
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Return JSON by default (not XML)
            config.Formatters.JsonFormatter.SerializerSettings.Formatting =
                Newtonsoft.Json.Formatting.Indented;
            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }
    }
}