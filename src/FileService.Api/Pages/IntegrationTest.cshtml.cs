using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileService.Api.Pages
{
    [IgnoreAntiforgeryToken]
    public class IntegrationTestModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public IntegrationTestModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [BindProperty]
        public IFormFile? UploadFile { get; set; }
        [BindProperty]
        public string? Filename { get; set; }
        public List<string>? Files { get; set; }
        public string? Message { get; set; }

        public async Task OnGetAsync([FromQuery] string? devUser, [FromQuery] string? role)
        {
            // If the page was loaded with devUser/role in query, persist them into cookies
            if (!string.IsNullOrEmpty(devUser))
            {
                Response.Cookies.Append("devUser", devUser, new CookieOptions { HttpOnly = false });
            }
            if (!string.IsNullOrEmpty(role))
            {
                Response.Cookies.Append("role", role, new CookieOptions { HttpOnly = false });
            }
            await Task.CompletedTask;
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (UploadFile == null)
            {
                Message = "No file selected";
                return Page();
            }

            var client = BuildClientForCurrentRequest();

            using var content = new MultipartFormDataContent();
            using var stream = UploadFile.OpenReadStream();
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(UploadFile.ContentType ?? "application/octet-stream");
            content.Add(streamContent, "file", UploadFile.FileName);

            var resp = await client.PostAsync("/api/files/upload", content);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                Message = $"Upload succeeded: {body}";
            }
            else
            {
                Message = $"Upload failed: {resp.StatusCode} - {await resp.Content.ReadAsStringAsync()}";
            }
            return Page();
        }

        public async Task<IActionResult> OnGetList()
        {
            var client = BuildClientForCurrentRequest();
            var resp = await client.GetAsync("/api/files");
            if (!resp.IsSuccessStatusCode)
            {
                Message = $"List failed: {resp.StatusCode}";
                return Page();
            }
            var json = await resp.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var elements = doc.RootElement.EnumerateArray();
                var list = new List<string>();
                foreach (var e in elements)
                {
                    if (e.TryGetProperty("fileName", out var fn) || e.TryGetProperty("FileName", out fn))
                    {
                        list.Add(fn.GetString() ?? string.Empty);
                        continue;
                    }
                    // fallback to any string property
                    foreach (var prop in e.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            list.Add(prop.Value.GetString() ?? string.Empty);
                            break;
                        }
                    }
                }
                Files = list;
            }
            catch (Exception ex)
            {
                Message = $"Failed to parse list: {ex.Message}";
            }
            return Page();
        }

        public async Task<IActionResult> OnPostDownloadAsync()
        {
            if (HttpContext.Request.HasFormContentType)
            {
                // ensure Request.Form is parsed so BuildClientForCurrentRequest can read devUser/role
                await HttpContext.Request.ReadFormAsync();
            }

            if (string.IsNullOrEmpty(Filename))
            {
                Message = "Please supply a filename to download";
                return Page();
            }

            var client = BuildClientForCurrentRequest();
            // Find file id by listing
            var resp = await client.GetAsync("/api/files");
            if (!resp.IsSuccessStatusCode)
            {
                Message = $"List failed: {resp.StatusCode}";
                return Page();
            }
            var json = await resp.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var elements = doc.RootElement.EnumerateArray();
                Guid? foundId = null;
                string? foundName = null;
                foreach (var e in elements)
                {
                    Guid id = Guid.Empty;
                    if (e.TryGetProperty("id", out var pid) || e.TryGetProperty("Id", out pid))
                    {
                        if (pid.ValueKind == JsonValueKind.String && Guid.TryParse(pid.GetString(), out var g)) id = g;
                        else if (pid.ValueKind == JsonValueKind.String == false && pid.TryGetGuid(out var gg)) id = gg;
                        else if (pid.ValueKind == JsonValueKind.Number) { /* ignore */ }
                    }
                    string? name = null;
                    if (e.TryGetProperty("fileName", out var pfn) || e.TryGetProperty("FileName", out pfn))
                        name = pfn.GetString();

                    if (!string.IsNullOrEmpty(name) && string.Equals(name, Filename, StringComparison.OrdinalIgnoreCase))
                    {
                        foundId = id == Guid.Empty ? null : id;
                        foundName = name;
                        break;
                    }
                }
                var match = (foundId.HasValue) ? new { Id = foundId.Value, Name = foundName } : null;

                if (match == null)
                {
                    Message = "File not found";
                    return Page();
                }

                var getResp = await client.GetAsync($"/api/files/{match.Id}");
                if (!getResp.IsSuccessStatusCode)
                {
                    Message = $"Get failed: {getResp.StatusCode}";
                    return Page();
                }
                var body = await getResp.Content.ReadAsStringAsync();
                using var bodyDoc = JsonDocument.Parse(body);
                string? downloadUrl = null;
                if (bodyDoc.RootElement.TryGetProperty("downloadUrl", out var d1)) downloadUrl = d1.GetString();
                else if (bodyDoc.RootElement.TryGetProperty("DownloadUrl", out var d2)) downloadUrl = d2.GetString();
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Message = "No download URL returned";
                    return Page();
                }
                return Redirect(downloadUrl);
            }
            catch (Exception ex)
            {
                Message = $"Download failed: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            if (HttpContext.Request.HasFormContentType)
            {
                // ensure Request.Form is parsed so BuildClientForCurrentRequest can read devUser/role
                await HttpContext.Request.ReadFormAsync();
            }

            if (string.IsNullOrEmpty(Filename))
            {
                Message = "Please supply a filename to delete";
                return Page();
            }

            var client = BuildClientForCurrentRequest();
            var resp = await client.GetAsync("/api/files");
            if (!resp.IsSuccessStatusCode)
            {
                Message = $"List failed: {resp.StatusCode}";
                return Page();
            }
            var json = await resp.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var elements = doc.RootElement.EnumerateArray();
                var match = elements.Select(e => new
                {
                    Id = e.GetProperty("id").GetGuid(),
                    Name = e.GetProperty("fileName").GetString()
                }).FirstOrDefault(x => string.Equals(x.Name, Filename, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    Message = "File not found";
                    return Page();
                }

                var delResp = await client.DeleteAsync($"/api/files/{match.Id}");
                if (delResp.IsSuccessStatusCode)
                {
                    Message = "Deleted";
                }
                else
                {
                    Message = $"Delete failed: {delResp.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                Message = $"Delete failed: {ex.Message}";
            }
            return Page();
        }

        private HttpClient BuildClientForCurrentRequest()
        {
            var client = _httpClientFactory.CreateClient();
            // Set base address to current request origin so relative urls work
            var req = HttpContext.Request;
            var baseUrl = $"{req.Scheme}://{req.Host.Value}";
            client.BaseAddress = new Uri(baseUrl);

            var envMode = _configuration.GetValue<string>("EnvironmentMode");
            if (string.IsNullOrEmpty(envMode))
            {
                var hostEnv = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Hosting.IHostEnvironment)) as Microsoft.Extensions.Hosting.IHostEnvironment;
                envMode = hostEnv?.EnvironmentName ?? string.Empty;
            }

            if (envMode.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                // Prefer query string but fall back to form values (so POST actions keep devUser/role)
                var devUser = HttpContext.Request.Query["devUser"].ToString();
                if (string.IsNullOrEmpty(devUser) && HttpContext.Request.HasFormContentType)
                {
                    devUser = HttpContext.Request.Form["devUser"].ToString();
                }
                // fallback to cookie if present
                if (string.IsNullOrEmpty(devUser) && HttpContext.Request.Cookies.TryGetValue("devUser", out var cookieDevUser) && !string.IsNullOrEmpty(cookieDevUser))
                {
                    devUser = cookieDevUser;
                }
                // Authentication removed: do not inject dev headers here.
                var role = HttpContext.Request.Query["role"].ToString();
                if (string.IsNullOrEmpty(role) && HttpContext.Request.HasFormContentType)
                {
                    role = HttpContext.Request.Form["role"].ToString();
                }
                // fallback to cookie if present
                if (string.IsNullOrEmpty(role) && HttpContext.Request.Cookies.TryGetValue("role", out var cookieRole) && !string.IsNullOrEmpty(cookieRole))
                {
                    role = cookieRole;
                }
                // Authentication removed: do not inject dev role header here.
            }
            return client;
        }

        public async Task<IActionResult> OnGetStatusAsync()
        {
            var client = BuildClientForCurrentRequest();
            var response = await client.GetAsync("/api/control/status");
            var body = await response.Content.ReadAsStringAsync();
            Message = response.IsSuccessStatusCode ? body : $"Failed to get service status: {(int)response.StatusCode} {response.ReasonPhrase} - {body}";
            return Page();
        }

        public async Task<IActionResult> OnPostStartAsync()
        {
            if (HttpContext.Request.HasFormContentType)
            {
                // force form parsing so BuildClientForCurrentRequest can read Request.Form
                await HttpContext.Request.ReadFormAsync();
            }
            var client = BuildClientForCurrentRequest();
            var response = await client.PostAsync("/api/control/start", null);
            var body = await response.Content.ReadAsStringAsync();
            Message = response.IsSuccessStatusCode ? body : $"Failed to start service: {(int)response.StatusCode} {response.ReasonPhrase} - {body}";
            return Page();
        }

        public async Task<IActionResult> OnPostStopAsync()
        {
            if (HttpContext.Request.HasFormContentType)
            {
                await HttpContext.Request.ReadFormAsync();
            }
            var client = BuildClientForCurrentRequest();
            var response = await client.PostAsync("/api/control/stop", null);
            var body = await response.Content.ReadAsStringAsync();
            Message = response.IsSuccessStatusCode ? body : $"Failed to stop service: {(int)response.StatusCode} {response.ReasonPhrase} - {body}";
            return Page();
        }
    }
}
