using System.Net.Http.Headers;
using System.Text.Json;

namespace DashTweaks.Core.Managers;

public class UpdateCheckResult
{
    public bool Success { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class UpdateManager
{
    private const string GitHubApiUrl = "https://api.github.com/repos/jose-seminario/DashTweaks/releases/latest";
    private const string CurrentAppVersion = "2.0.0"; // Versión 2.0 definitiva de DashTweaks

    public string CurrentVersion => CurrentAppVersion;

    /// <summary>
    /// Consulta de forma asíncrona la API pública de GitHub para verificar si existe una versión superior.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = CurrentAppVersion
        };

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(8);

            // GitHub API requiere la cabecera User-Agent obligatoriamente
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DashTweaks-App", "2.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            var response = await client.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.ErrorMessage = $"Servidor GitHub respondió con código {(int)response.StatusCode} ({response.ReasonPhrase}).";
                return result;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : "";
            string name = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
            string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";

            string downloadUrl = htmlUrl;

            // Buscar asset (.exe o .zip) para descarga directa
            if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElem.EnumerateArray())
                {
                    string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        if (asset.TryGetProperty("browser_download_url", out var dUrl))
                        {
                            downloadUrl = dUrl.GetString() ?? htmlUrl;
                            break;
                        }
                    }
                }
            }

            result.LatestVersion = tagName.Trim().TrimStart('v', 'V');
            result.ReleaseName = string.IsNullOrWhiteSpace(name) ? tagName : name;
            result.ReleaseNotes = body;
            result.DownloadUrl = downloadUrl;
            result.Success = true;

            // Comparación semántica de versiones
            if (TryParseVersion(result.LatestVersion, out Version? latest) &&
                TryParseVersion(result.CurrentVersion, out Version? current))
            {
                result.IsUpdateAvailable = latest > current;
            }
            else
            {
                result.IsUpdateAvailable = !string.Equals(result.LatestVersion, result.CurrentVersion, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error de red al consultar GitHub: {ex.Message}";
            return result;
        }
        catch (TaskCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "El tiempo de espera al conectar con GitHub expiró.";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"No se pudo verificar actualizaciones: {ex.Message}";
            return result;
        }
    }

    private static bool TryParseVersion(string vStr, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(vStr)) return false;

        string clean = vStr.Trim().TrimStart('v', 'V');
        string[] parts = clean.Split('.');
        if (parts.Length == 1) clean += ".0.0.0";
        else if (parts.Length == 2) clean += ".0.0";
        else if (parts.Length == 3) clean += ".0";

        return Version.TryParse(clean, out version);
    }
}
