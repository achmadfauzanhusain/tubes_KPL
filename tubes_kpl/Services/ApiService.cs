using ManajemenNilai.Contracts;
using ManajemenNilai.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Windows.Forms.Design;
using Tubes_KPL.Models;
using Tubes_KPL.Services;

namespace ManajemenNilai.Services;

/// <summary>
/// HTTP REST API client untuk integrasi dengan sistem eksternal
/// </summary>
public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private string _baseUrl;

    public ApiService(string baseUrl = "http://localhost:5000/api")
    {
        _baseUrl = baseUrl;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/{endpoint}");
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch
        {
            return default;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest payload)
    {
        try
        {
            string json = JsonConvert.SerializeObject(payload);

            var content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/{endpoint}",
                content);

            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<TResponse>(responseJson);
        }
        catch
        {
            return default;
        }
    }

    public async Task<bool> PutAsync<T>(string endpoint, T payload)
    {
        try
        {
            string json = JsonConvert.SerializeObject(payload);

            var content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PutAsync(
                $"{_baseUrl}/{endpoint}",
                content);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string endpoint)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"{_baseUrl}/{endpoint}");

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Simulasi REST API secara lokal untuk demo dan testing
/// </summary>
public class LocalApiService : IApiService
{
    private readonly IGradeService _gradeService;
    private readonly IKomplainService _komplainService;

    private string? _authToken;
    private int _currentUserId;

    private static readonly Dictionary<string,
        Func<LocalApiService, string?, Task<object?>>> GetRoutes =
        new(StringComparer.OrdinalIgnoreCase);

    static LocalApiService()
    {
        GetRoutes["nilai/mahasiswa"] = async (svc, param) =>
        {
            if (!int.TryParse(param, out int id))
                return null;

            return await svc._gradeService
                .GetMataKuliahByMahasiswaAsync(id);
        };

        GetRoutes["matakuliah/dosen"] = async (svc, param) =>
        {
            return await svc._gradeService
                .GetMataKuliahByDosenAsync(param ?? "");
        };

        GetRoutes["rekap"] = async (svc, param) =>
        {
            return await svc._gradeService
                .GetRekapNilaiAsync(param ?? "");
        };
    }

    public LocalApiService(
        IGradeService gradeService,
        IKomplainService komplainService)
    {
        _gradeService = gradeService;
        _komplainService = komplainService;
    }

    public void SetAuthToken(string token)
    {
        _authToken = token;

        // Format token: userId:role
        var parts = token.Split(':');

        if (parts.Length >= 1 &&
            int.TryParse(parts[0], out int uid))
        {
            _currentUserId = uid;
        }
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        var parts = endpoint.Split('/');

        string route = string.Join("/", parts.Take(2));
        string? param = parts.Length > 2
            ? string.Join("/", parts.Skip(2))
            : null;

        if (GetRoutes.TryGetValue(route, out var handler))
        {
            var result = await handler(this, param);

            if (result is T typed)
                return typed;
        }

        return default;
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest payload)
    {
        if (endpoint == "komplain" &&
            payload is Komplain k)
        {
            var result =
                await _komplainService.AjukanKomplainAsync(
                    k.MahasiswaId,
                    k.NilaiId,
                    k.KomponenId,
                    k.Pesan);

            if (result is TResponse response)
                return response;
        }

        return default;
    }

    public Task<bool> PutAsync<T>(
        string endpoint,
        T payload)
    {
        return Task.FromResult(false);
    }

    public Task<bool> DeleteAsync(string endpoint)
    {
        return Task.FromResult(false);
    }
}