using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
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

                // Extract Attribute Exchange data
                result.Dcid = query["openid.ax.value.dcid"].ToString();
                result.Email = query["openid.ax.value.email"].ToString();
                result.FirstName = query["openid.ax.value.firstName"].ToString();
                result.LastName = query["openid.ax.value.lastName"].ToString();

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

                await RenderSuccess(context, values, result);
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
    }
}
