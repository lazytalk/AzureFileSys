using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FileService.Api.Services
{
    /// <summary>
    /// Modern lightweight OpenID 2.0 Relying Party implementation for PowerSchool authentication.
    /// Converted from Node.js Express implementation.
    /// </summary>
    public class OpenIdRelyingPartyService
    {
        private readonly string _ipHostname;
        private readonly int _port;
        private static readonly HttpClient _httpClient = new HttpClient();

        public OpenIdRelyingPartyService(string ipHostname, int port)
        {
            if (string.IsNullOrEmpty(ipHostname) || port <= 0)
            {
                throw new ArgumentException("OpenIdRelyingPartyService requires ip_hostname and port");
            }

            _ipHostname = ipHostname;
            _port = port;
        }

        public string GetReturnUrl()
        {
            return $"https://{_ipHostname}:{_port}/verify";
        }

        /// <summary>
        /// Discover OpenID endpoint from identifier using XRDS (Yadis), Link headers, or HTML link tags.
        /// </summary>
        public async Task<string?> DiscoverOpenIdEndpointAsync(string identifier)
        {
                try
                {
                    var url = identifier.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                              identifier.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? identifier
                        : "https://" + identifier;

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Accept.Clear();
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xrds+xml"));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

                    using var response = await _httpClient.SendAsync(request);
                    var baseUri = response.RequestMessage?.RequestUri;

                    // 1) Check X-XRDS-Location header
                    if (response.Headers.TryGetValues("X-XRDS-Location", out var xrdsHeaders))
                    {
                        var xrdsLocation = xrdsHeaders.FirstOrDefault();
                        var endpointFromXrds = await FetchAndParseXrdsAsync(xrdsLocation!, baseUri);
                        if (!string.IsNullOrEmpty(endpointFromXrds)) return endpointFromXrds;
                    }

                    // 2) Check Link header rel="openid2.provider" or "openid.server"
                    if (response.Headers.TryGetValues("Link", out var linkHeaders))
                    {
                        foreach (var link in linkHeaders)
                        {
                            var provider = ParseLinkHeaderForRel(link, "openid2.provider") ?? ParseLinkHeaderForRel(link, "openid.server");
                            if (!string.IsNullOrEmpty(provider)) return MakeAbsolute(baseUri, provider);
                        }
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    var body = await response.Content.ReadAsStringAsync();

                    // 3) If response is XRDS already
                    if (contentType.Contains("application/xrds+xml", StringComparison.OrdinalIgnoreCase))
                    {
                        var endpointFromBody = ParseXrds(body);
                        if (!string.IsNullOrEmpty(endpointFromBody)) return endpointFromBody;
                    }

                    // 4) HTML <meta http-equiv="x-xrds-location" ...>
                    var metaMatch = System.Text.RegularExpressions.Regex.Match(body,
                        @"<meta[^>]*http-equiv=['\""]x-xrds-location['\""][^>]*content=['\""]([^'\""]+)['\""]",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (metaMatch.Success)
                    {
                        var loc = metaMatch.Groups[1].Value;
                        var endpointFromMeta = await FetchAndParseXrdsAsync(loc, baseUri);
                        if (!string.IsNullOrEmpty(endpointFromMeta)) return endpointFromMeta;
                    }

                    // 5) HTML <link rel="openid2.provider" ...> or openid.server
                    var match = System.Text.RegularExpressions.Regex.Match(body,
                        @"<link[^>]*rel=['\""]openid2\.provider['\""][^>]*href=['\""]([^'\""]+)['\""]",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return MakeAbsolute(baseUri, match.Groups[1].Value);
                    }
                    match = System.Text.RegularExpressions.Regex.Match(body,
                        @"<link[^>]*rel=['\""]openid\.server['\""][^>]*href=['\""]([^'\""]+)['\""]",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return MakeAbsolute(baseUri, match.Groups[1].Value);
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }

            private static string? ParseLinkHeaderForRel(string linkHeader, string rel)
            {
                // Very simple parser for: <url>; rel="openid2.provider"
                var parts = linkHeader.Split(',');
                foreach (var part in parts)
                {
                    var seg = part.Trim();
                    var m = System.Text.RegularExpressions.Regex.Match(seg, @"<([^>]+)>;\s*rel=""([^""]+)""");
                    if (m.Success && string.Equals(m.Groups[2].Value, rel, StringComparison.OrdinalIgnoreCase))
                    {
                        return m.Groups[1].Value;
                    }
                }
                return null;
            }

            private static string MakeAbsolute(Uri? baseUri, string url)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var abs)) return abs.ToString();
                if (baseUri != null && Uri.TryCreate(baseUri, url, out var rel)) return rel.ToString();
                return url;
            }

            private static async Task<string?> FetchAndParseXrdsAsync(string xrdsLocation, Uri? baseUri)
            {
                try
                {
                    var absolute = MakeAbsolute(baseUri, xrdsLocation);
                    var xrds = await _httpClient.GetStringAsync(absolute);
                    return ParseXrds(xrds);
                }
                catch
                {
                    return null;
                }
            }

            private static string? ParseXrds(string xrds)
            {
                try
                {
                    var doc = XDocument.Parse(xrds);
                    // Look for Service with Type = OpenID 2.0 server or signon, then take its URI
                    var types = new[] { "http://specs.openid.net/auth/2.0/server", "http://specs.openid.net/auth/2.0/signon" };
                    var uri = doc.Descendants().
                        Where(e => e.Name.LocalName.Equals("Service", StringComparison.OrdinalIgnoreCase)).
                        Select(s => new
                        {
                            Types = s.Elements().Where(x => x.Name.LocalName.Equals("Type", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).ToList(),
                            Uri = s.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("URI", StringComparison.OrdinalIgnoreCase))?.Value
                        }).
                        FirstOrDefault(s => s.Uri != null && s.Types.Any(t => types.Contains(t)) )?.Uri;
                    return uri;
                }
                catch
                {
                    return null;
                }
            }

        /// <summary>
        /// Build OpenID authentication URL with Attribute Exchange
        /// </summary>
        public async Task<string?> BuildAuthenticationUrlAsync(string identifier, string baseUrl)
        {
            var endpoint = await DiscoverOpenIdEndpointAsync(identifier);
            if (string.IsNullOrEmpty(endpoint))
            {
                return null;
            }

            var returnUrl = baseUrl.TrimEnd('/') + "/verify";
            var queryParams = new Dictionary<string, string>
            {
                { "openid.ns", "http://specs.openid.net/auth/2.0" },
                { "openid.mode", "checkid_setup" },
                // Use directed identity when discovering provider endpoint
                { "openid.claimed_id", "http://specs.openid.net/auth/2.0/identifier_select" },
                { "openid.identity", "http://specs.openid.net/auth/2.0/identifier_select" },
                { "openid.return_to", returnUrl },
                { "openid.realm", baseUrl.TrimEnd('/') + "/" },
                
                // Attribute Exchange extension for PowerSchool
                { "openid.ns.ax", "http://openid.net/srv/ax/1.0" },
                { "openid.ax.mode", "fetch_request" },
                { "openid.ax.type.dcid", "http://powerschool.com/entity/id" },
                { "openid.ax.type.email", "http://powerschool.com/entity/email" },
                { "openid.ax.type.firstName", "http://powerschool.com/entity/firstName" },
                { "openid.ax.type.lastName", "http://powerschool.com/entity/lastName" },
                { "openid.ax.required", "dcid,email,firstName,lastName" }
            };

            var queryString = string.Join("&", queryParams.Select(kvp => 
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            return $"{endpoint}?{queryString}";
        }

        /// <summary>
        /// Verify OpenID assertion from callback
        /// </summary>
        public async Task<OpenIdVerificationResult> VerifyAssertionAsync(IQueryCollection query)
        {
            var result = new OpenIdVerificationResult { Authenticated = false };
            
            // Capture incoming parameters
            var incomingParams = new Dictionary<string, string>();
            foreach (var param in query)
            {
                if (param.Key.StartsWith("openid.", StringComparison.OrdinalIgnoreCase))
                {
                    incomingParams[param.Key] = param.Value.ToString();
                }
            }
            result.IncomingParams = incomingParams;

            try
            {
                var mode = query["openid.mode"].ToString();
                if (mode != "id_res")
                {
                    result.Error = "Invalid OpenID mode";
                    return result;
                }

                var endpoint = query["openid.op_endpoint"].ToString();
                if (string.IsNullOrEmpty(endpoint))
                {
                    result.Error = "Missing OpenID endpoint";
                    return result;
                }

                // Build verification request
                var verifyParams = new Dictionary<string, string>();
                foreach (var param in query)
                {
                    if (param.Key.StartsWith("openid.", StringComparison.OrdinalIgnoreCase))
                    {
                        verifyParams[param.Key] = param.Value.ToString();
                    }
                }
                verifyParams["openid.mode"] = "check_authentication";

                // Capture verify request params
                result.VerifyRequestParams = new Dictionary<string, string>(verifyParams);

                // Log all parameters being sent to the provider
                Console.WriteLine($"[VERIFY] Sending check_authentication request to: {endpoint}");
                Console.WriteLine("[VERIFY] Request Parameters:");
                foreach (var kvp in verifyParams)
                {
                    Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
                }

                // Send verification to provider
                var content = new FormUrlEncodedContent(verifyParams);
                var response = await _httpClient.PostAsync(endpoint, content);
                var responseText = await response.Content.ReadAsStringAsync();

                // Capture provider response details
                result.ProviderResponseStatus = $"{(int)response.StatusCode} {response.StatusCode}";
                result.ProviderResponseBody = responseText;

                // Log provider response
                Console.WriteLine("[VERIFY] Provider Response Status: " + (int)response.StatusCode);
                Console.WriteLine("[VERIFY] Provider Response Body:");
                Console.WriteLine(responseText);

                // Parse key-value response
                var isValid = responseText.Contains("is_valid:true", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"[VERIFY] is_valid check: {isValid}");
                if (!isValid)
                {
                    result.Error = "OpenID verification failed";
                    return result;
                }

                result.Authenticated = true;

                // Extract Attribute Exchange data - PowerSchool sends as openid.ext1.value.* 
                result.Dcid = query["openid.ext1.value.dcid"].ToString();
                result.Email = query["openid.ext1.value.email"].ToString();
                result.FirstName = query["openid.ext1.value.firstName"].ToString();
                result.LastName = query["openid.ext1.value.lastName"].ToString();

                Console.WriteLine($"[VERIFY] Extracted Dcid: '{result.Dcid}', Email: '{result.Email}', FirstName: '{result.FirstName}', LastName: '{result.LastName}'");
                
                // Log all AX values for debugging
                foreach (var param in query)
                {
                    if (param.Key.Contains("ax", StringComparison.OrdinalIgnoreCase) || param.Key.Contains("ext1", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[VERIFY] Param: {param.Key} = {param.Value}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
        }
    }

    public class OpenIdVerificationResult
    {
        public bool Authenticated { get; set; }
        public string? Dcid { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Error { get; set; }
        
        // Verification details for debugging
        public Dictionary<string, string>? IncomingParams { get; set; }
        public Dictionary<string, string>? VerifyRequestParams { get; set; }
        public string? ProviderResponseStatus { get; set; }
        public string? ProviderResponseBody { get; set; }
    }

    /// <summary>
    /// Extension to register OpenID authentication endpoints with Swagger documentation
    /// </summary>
    public static class OpenIdRelyingPartyExtensions
    {
        // Session storage for authenticated users
        private static readonly ConcurrentDictionary<string, UserSessionInfo> _userSessions = 
            new ConcurrentDictionary<string, UserSessionInfo>();

        public static IEndpointRouteBuilder MapOpenIdAuthentication(this IEndpointRouteBuilder app, OpenIdRelyingPartyService service)
        {
            Console.WriteLine("[OPENID] Registering OpenID authentication endpoints");
            // Authenticate endpoint
            app.MapGet("/authenticate", 
                async (string? openid_identifier, HttpContext context) => await AuthenticateAsync(service, openid_identifier, context))
                .WithName("OpenID_Authenticate")
                .WithDescription("Initiates PowerSchool OpenID authentication flow. Redirects user to PowerSchool login.");

            // Verify endpoint
            app.MapGet("/verify", 
                async (HttpContext context) => await VerifyAsync(service, context))
                .WithName("OpenID_Verify")
                .WithDescription("Callback endpoint for PowerSchool OpenID authentication. Verifies and displays user information.");

            // Session info endpoint - retrieve stored user session (persistent, not consumed)
            app.MapGet("/api/auth/session-info", 
                (string? sessionId, HttpContext context) =>
                {
                    if (string.IsNullOrEmpty(sessionId))
                    {
                        return Results.BadRequest(new { error = "sessionId is required" });
                    }

                    var userSession = OpenIdRelyingPartyExtensions.GetUserSession(sessionId);
                    if (userSession == null)
                    {
                        return Results.NotFound(new { error = "Session not found or expired" });
                    }

                    // Re-stamp session cookie if missing, to help browsers that dropped it on redirect
                    if (!context.Request.Cookies.ContainsKey("FileService.Session"))
                    {
                        var sessionExpiration = userSession.ExpiresAt;
                        context.Response.Cookies.Append("FileService.Session", sessionId, new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = sessionExpiration,
                            Path = "/",
                            Domain = ".kaiweneducation.com"
                        });
                        Console.WriteLine($"[OPENID] Re-stamped session cookie for session {sessionId}");
                    }

                    Console.WriteLine($"[OPENID] Session info retrieved: {sessionId}");
                    return Results.Ok(new
                    {
                        dcid = userSession.Dcid,
                        email = userSession.Email,
                        firstName = userSession.FirstName,
                        lastName = userSession.LastName,
                        expiresAt = userSession.ExpiresAt
                    });
                })
                .WithName("OpenID_SessionInfo")
                .WithDescription("Retrieves user session information by session ID (does not consume session).");

            // Logout endpoint - clear session
            app.MapPost("/api/auth/logout", 
                (HttpContext context) =>
                {
                    var sessionId = context.Request.Cookies["FileService.Session"];
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        OpenIdRelyingPartyExtensions.RemoveUserSession(sessionId);
                        context.Response.Cookies.Delete("FileService.Session");
                        Console.WriteLine($"[OPENID] User logged out, session cleared: {sessionId}");
                    }
                    return Results.Ok(new { message = "Logged out successfully" });
                })
                .WithName("OpenID_Logout")
                .WithDescription("Logs out user and clears authentication session.");

            return app;
        }

        private static async Task AuthenticateAsync(OpenIdRelyingPartyService service, string? openid_identifier, HttpContext context)
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("Accepting authentication request ...");
            Console.WriteLine($"Query params: openid_identifier={openid_identifier}");

            if (string.IsNullOrEmpty(openid_identifier))
            {
                await RenderFailure(context, "openid_identifier parameter is required");
                return;
            }

            try
            {
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                var authUrl = await service.BuildAuthenticationUrlAsync(openid_identifier, baseUrl);

                if (string.IsNullOrEmpty(authUrl))
                {
                    await RenderFailure(context, "Could not discover OpenID endpoint");
                    return;
                }

                // Redirect to authentication URL
                context.Response.Redirect(authUrl);
            }
            catch (Exception ex)
            {
                await RenderFailure(context, ex.Message);
            }
        }

        private static async Task VerifyAsync(OpenIdRelyingPartyService service, HttpContext context)
        {
            try
            {
                // Log all incoming callback parameters
                Console.WriteLine("================================================================================");
                Console.WriteLine("VERIFY CALLBACK RECEIVED");
                Console.WriteLine("================================================================================");
                Console.WriteLine("Incoming Query Parameters:");
                foreach (var param in context.Request.Query)
                {
                    if (param.Key.StartsWith("openid", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"  {param.Key} = {param.Value}");
                    }
                }
                Console.WriteLine("");

                var result = await service.VerifyAssertionAsync(context.Request.Query);

                Console.WriteLine("================================================================================");
                Console.WriteLine($"Verification Result: Authenticated={result.Authenticated}");

                if (!result.Authenticated)
                {
                    await RenderFailure(context, result.Error ?? "Authentication failed");
                    return;
                }

                // Build result dictionary
                var values = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(result.Dcid))
                    values["id"] = result.Dcid;
                if (!string.IsNullOrEmpty(result.Email))
                    values["email"] = result.Email;
                if (!string.IsNullOrEmpty(result.FirstName))
                    values["firstName"] = result.FirstName;
                if (!string.IsNullOrEmpty(result.LastName))
                    values["lastName"] = result.LastName;

                // Create persistent session with user info
                var sessionId = Guid.NewGuid().ToString();
                var sessionExpiration = DateTime.UtcNow.AddHours(8);
                Console.WriteLine($"[OPENID] Creating session with Dcid: '{result.Dcid}' (empty={string.IsNullOrWhiteSpace(result.Dcid)})");
                OpenIdRelyingPartyExtensions.StoreUserSession(sessionId, new UserSessionInfo
                {
                    Dcid = result.Dcid,
                    Email = result.Email,
                    FirstName = result.FirstName,
                    LastName = result.LastName,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = sessionExpiration
                });

                // Set secure HttpOnly cookie for authentication
                context.Response.Cookies.Append("FileService.Session", sessionId, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = sessionExpiration,
                    Path = "/", // allow all API paths
                    Domain = ".kaiweneducation.com" // ensure cookie available on custom domain
                });

                Console.WriteLine($"[OPENID] Persistent session created: {sessionId} for user: {result.Dcid}, expires: {sessionExpiration}");

                var config = context.RequestServices.GetRequiredService<IConfiguration>();
                var pluginBaseUrl = config["PowerSchool:PluginBaseUrl"];
                var pluginPath = config["PowerSchool:PluginPath"];
                var redirectUrl = !string.IsNullOrWhiteSpace(pluginBaseUrl)
                    ? $"{pluginBaseUrl!.TrimEnd('/')}{pluginPath}?session={Uri.EscapeDataString(sessionId)}"
                    : $"{pluginPath}?session={Uri.EscapeDataString(sessionId)}";

                Console.WriteLine($"[OPENID] Redirecting to FileServiceTools: {redirectUrl}");
                context.Response.Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                await RenderFailure(context, ex.Message);
            }
        }

        private static async Task RenderFailure(HttpContext context, string message)
        {
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync($@"
<!DOCTYPE html>
<html>
<head>
    <title>Authentication Failed</title>
</head>
<body>
    <h1>Authentication Failed</h1>
    <p>{System.Net.WebUtility.HtmlEncode(message)}</p>
</body>
</html>");
        }

        private static async Task RenderSuccess(HttpContext context, Dictionary<string, string> values, OpenIdVerificationResult result)
        {
            context.Response.ContentType = "text/html";
            var content = @"
<!DOCTYPE html>
<html>
<head>
    <title>Authentication Successful</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .section { margin: 20px 0; padding: 10px; border: 1px solid #ccc; background: #f9f9f9; }
        h2 { color: #333; }
        pre { background: #f4f4f4; padding: 10px; overflow-x: auto; }
        .success { color: green; }
    </style>
</head>
<body>
    <h1 class=""success"">✓ Authentication Successful</h1>
    
    <div class=""section"">
        <h2>Extracted User Attributes</h2>
        <ul>";

            foreach (var kvp in values)
            {
                content += $"\n        <li><strong>{System.Net.WebUtility.HtmlEncode(kvp.Key)}:</strong> {System.Net.WebUtility.HtmlEncode(kvp.Value)}</li>";
            }

            content += @"
        </ul>
    </div>";

            // Add incoming callback parameters
            if (result.IncomingParams != null && result.IncomingParams.Count > 0)
            {
                content += @"
    <div class=""section"">
        <h2>Incoming Callback Parameters (from PowerSchool)</h2>
        <pre>";
                foreach (var kvp in result.IncomingParams)
                {
                    content += System.Net.WebUtility.HtmlEncode($"{kvp.Key} = {kvp.Value}") + "\n";
                }
                content += @"</pre>
    </div>";
            }

            // Add verification request parameters
            if (result.VerifyRequestParams != null && result.VerifyRequestParams.Count > 0)
            {
                content += @"
    <div class=""section"">
        <h2>Verification Request Parameters (sent to PowerSchool)</h2>
        <pre>";
                foreach (var kvp in result.VerifyRequestParams)
                {
                    content += System.Net.WebUtility.HtmlEncode($"{kvp.Key} = {kvp.Value}") + "\n";
                }
                content += @"</pre>
    </div>";
            }

            // Add provider response
            if (!string.IsNullOrEmpty(result.ProviderResponseStatus))
            {
                content += @"
    <div class=""section"">
        <h2>Provider Response</h2>
        <p><strong>Status:</strong> " + System.Net.WebUtility.HtmlEncode(result.ProviderResponseStatus) + @"</p>
        <p><strong>Body:</strong></p>
        <pre>" + System.Net.WebUtility.HtmlEncode(result.ProviderResponseBody ?? "No response") + @"</pre>
    </div>";
            }

            content += @"
</body>
</html>";

            await context.Response.WriteAsync(content);
        }

        /// <summary>
        /// Store user session information with expiration
        /// </summary>
        public static void StoreUserSession(string sessionId, UserSessionInfo userInfo)
        {
            _userSessions[sessionId] = userInfo;
            
            // Background cleanup of expired sessions
            _ = Task.Run(async () =>
            {
                await Task.Delay(userInfo.ExpiresAt - DateTime.UtcNow + TimeSpan.FromMinutes(5));
                _userSessions.TryRemove(sessionId, out _);
            });
        }

        /// <summary>
        /// Retrieve user session without removing it (persistent session)
        /// </summary>
        public static UserSessionInfo? GetUserSession(string sessionId)
        {
            if (_userSessions.TryGetValue(sessionId, out var userInfo))
            {
                // Check if session hasn't expired
                if (userInfo.ExpiresAt > DateTime.UtcNow)
                {
                    return userInfo;
                }
                // Remove expired session
                _userSessions.TryRemove(sessionId, out _);
            }
            return null;
        }

        /// <summary>
        /// Remove user session (for logout)
        /// </summary>
        public static void RemoveUserSession(string sessionId)
        {
            _userSessions.TryRemove(sessionId, out _);
        }

        /// <summary>
        /// Validate session cookie and user header match with CSRF protection
        /// </summary>
        public static bool ValidateSessionAndUser(HttpContext context, string? requiredUserId)
        {
            void SetDebug(string reason, object? details = null)
            {
                context.Items["AuthDebugReason"] = reason;
                context.Items["AuthDebugDetails"] = details;
            }

            Console.WriteLine("[AUTH] --- Validation Start ---");
            Console.WriteLine($"[AUTH] Path: {context.Request.Path}");
            Console.WriteLine($"[AUTH] Origin: {context.Request.Headers["Origin"]}");
            Console.WriteLine($"[AUTH] Referer: {context.Request.Headers["Referer"]}");
            var hdrUser = context.Request.Headers["X-PowerSchool-User"].ToString();
            var hdrUserId = context.Request.Headers["X-PowerSchool-UserId"].ToString();
            // Some environments may send different casing like X-Powerschool-Userid
            var hdrUserIdAlt = string.IsNullOrEmpty(hdrUserId) ? context.Request.Headers["X-Powerschool-Userid"].ToString() : string.Empty;
            Console.WriteLine($"[AUTH] X-PowerSchool-User: {hdrUser}");
            Console.WriteLine($"[AUTH] X-PowerSchool-UserId: {hdrUserId}");
            if (!string.IsNullOrEmpty(hdrUserIdAlt)) Console.WriteLine($"[AUTH] X-Powerschool-Userid (alt): {hdrUserIdAlt}");
            Console.WriteLine($"[AUTH] Cookie (FileService.Session) present: {context.Request.Cookies.ContainsKey("FileService.Session")}");

            // CSRF Protection: Validate Origin header
            var origin = context.Request.Headers["Origin"].ToString();
            var referer = context.Request.Headers["Referer"].ToString();
            
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var allowedOrigins = config.GetSection("PowerSchool:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            
            // Check Origin header (sent on cross-origin requests)
            if (!string.IsNullOrEmpty(origin))
            {
                var isAllowedOrigin = allowedOrigins.Any(allowed => 
                    origin.Equals(allowed, StringComparison.OrdinalIgnoreCase));
                
                if (!isAllowedOrigin)
                {
                    Console.WriteLine($"[CSRF] Blocked request from unauthorized origin: {origin}");
                    SetDebug("Blocked: origin not allowed", new { origin, allowedOrigins });
                    return false;
                }
            }
            // Fallback to Referer validation if Origin not present (some browsers/scenarios)
            else if (!string.IsNullOrEmpty(referer))
            {
                var refererUri = new Uri(referer);
                var refererOrigin = $"{refererUri.Scheme}://{refererUri.Host}";
                
                var isAllowedReferer = allowedOrigins.Any(allowed => 
                    refererOrigin.Equals(allowed, StringComparison.OrdinalIgnoreCase));
                
                if (!isAllowedReferer)
                {
                    Console.WriteLine($"[CSRF] Blocked request from unauthorized referer: {refererOrigin}");
                    SetDebug("Blocked: referer not allowed", new { refererOrigin, allowedOrigins });
                    return false;
                }
            }

            var sessionId = context.Request.Cookies["FileService.Session"];
            var hdrSession = context.Request.Headers["X-Session-Id"].ToString();
            var qsSession = context.Request.Query["session"].ToString();
            Console.WriteLine($"[AUTH] Session lookup - Cookie: {sessionId}, Header: {hdrSession}, Query: {qsSession}");
            
            // Fallback to session header or query if cookie not present
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = !string.IsNullOrEmpty(hdrSession) ? hdrSession : qsSession;
                if (!string.IsNullOrEmpty(sessionId))
                {
                    Console.WriteLine($"[AUTH] Using session from header/query: {sessionId}");
                    // Re-stamp cookie to persist for subsequent calls
                    context.Response.Cookies.Append("FileService.Session", sessionId, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = DateTime.UtcNow.AddHours(8),
                        Path = "/",
                        Domain = ".kaiweneducation.com"
                    });
                }
            }
            if (string.IsNullOrEmpty(sessionId))
            {
                Console.WriteLine("[AUTH] No session cookie found");
                SetDebug("No session cookie", new { hdrUser, hdrUserId, hdrUserIdAlt, origin, referer });
                return false;
            }

            var session = GetUserSession(sessionId);
            if (session == null)
            {
                Console.WriteLine($"[AUTH] Invalid or expired session: {sessionId}");
                SetDebug("Invalid or expired session", new { sessionId, hdrUser, hdrUserId, hdrUserIdAlt, origin, referer });
                return false;
            }

            // Verify the user ID in header matches the authenticated session (accept any of the known header variants)
            var headerCandidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(requiredUserId)) headerCandidates.Add(requiredUserId!);
            if (!string.IsNullOrWhiteSpace(hdrUserId)) headerCandidates.Add(hdrUserId!);
            if (!string.IsNullOrWhiteSpace(hdrUserIdAlt)) headerCandidates.Add(hdrUserIdAlt!);
            if (!string.IsNullOrWhiteSpace(hdrUser)) headerCandidates.Add(hdrUser!);

            var headerMatchesSession = headerCandidates.Any(h => string.Equals(session.Dcid, h, StringComparison.OrdinalIgnoreCase));
            if (!headerMatchesSession)
            {
                Console.WriteLine($"[AUTH] Session user mismatch - Session.Dcid: {session.Dcid}, Candidates: {string.Join(", ", headerCandidates)}");
                SetDebug("Session header mismatch", new { sessionId, sessionDcid = session.Dcid, headerCandidates, origin, referer });
                return false;
            }

            SetDebug("Success", new { sessionId, sessionDcid = session.Dcid, origin, referer });
            Console.WriteLine($"[AUTH] Validation succeeded for user {session.Dcid}");
            Console.WriteLine("[AUTH] --- Validation End ---");
            return true;
        }
    }

    /// <summary>
    /// User session information stored server-side
    /// </summary>
    public class UserSessionInfo
    {
        public string? Dcid { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
