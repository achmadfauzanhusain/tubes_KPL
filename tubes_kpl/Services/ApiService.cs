using ManajemenNilai.Contracts;
using ManajemenNilai.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Windows.Forms.Design;
using Tubes_KPL.Models;
using Tubes_KPL.Services;

namespace ManajemenNilai.Services;

public class LocalApiService : IApiService
{
    private readonly IGradeService _gradeService;
    private readonly IKomplainService _komplainService;
    private string? _authToken;
    private int _currentUserId;

    private static readonly Dictionary<string, Func<LocalApiService, string?, Task<object?>>>
        GetRoutes = new(StringComparer.OrdinalIgnoreCase);

    static LocalApiService()
    {
        GetRoutes["nilai/mahasiswa"] = async (svc, param) =>
        {
            if (!int.TryParse(param, out int id)) return null;
            return await svc._gradeService.GetMataKuliahByMahasiswaAsync(id);
        };

        GetRoutes["matakuliah/dosen"] = async (svc, param) =>
        {
            return await svc._gradeService.GetMataKuliahByDosenAsync(param ?? "");
        };

        GetRoutes["rekap"] = async (svc, param) =>
        {
            return await svc._gradeService.GetRekapNilaiAsync(param ?? "");
        };
    }

    public LocalApiService(IGradeService gradeService, IKomplainService komplainService)
    {
        _gradeService = gradeService;
        _komplainService = komplainService;
    }

    public void SetAuthToken(string token)
    {
        _authToken = token;
        var parts = token.Split(':');
        if (parts.Length >= 1 && int.TryParse(parts[0], out int uid))
            _currentUserId = uid;
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        var parts = endpoint.Split('/');
        string route = string.Join("/", parts.Take(2));
        string? param = parts.Length > 2 ? string.Join("/", parts.Skip(2)) : null;

        if (GetRoutes.TryGetValue(route, out var handler))
        {
            var result = await handler(this, param);
            if (result is T typed) return typed;
        }
        return default;
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload)
    {
        if (endpoint == "komplain" && payload is Komplain k)
        {
            var result = await _komplainService.AjukanKomplainAsync(
                k.MahasiswaId, k.NilaiId, k.KomponenId, k.Pesan);
            if (result is TResponse r) return r;
        }
        return default;
    }

    public Task<bool> PutAsync<T>(string endpoint, T payload) => Task.FromResult(false);
    public Task<bool> DeleteAsync(string endpoint) => Task.FromResult(false);
}