// ============================================================
// Contracts/IRepository.cs
// Teknik: Parameterization/Generics - Salman Al Farizin - Nasywa Azalia Andrean
// Generic repository interface reusable untuk semua model
// ============================================================

namespace ManajemenNilai.Contracts;

/// <summary>
/// Generic repository contract.
/// Menggunakan Parameterization/Generics agar reusable untuk
/// semua entitas (User, NilaiMahasiswa, Komplain, dll).
/// </summary>
/// <typeparam name="T">Tipe entitas</typeparam>
/// <typeparam name="TKey">Tipe primary key</typeparam>
public interface IRepository<T, TKey> where T : class
{
    Task<T?> GetByIdAsync(TKey id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<int> InsertAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(TKey id);
}

/// <summary>
/// Extension: query by predicate
/// </summary>
public interface IQueryableRepository<T, TKey> : IRepository<T, TKey> where T : class
{
    Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);
    Task<T?> FirstOrDefaultAsync(Func<T, bool> predicate);
}

/// <summary>
/// Generic paged result
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Generic import result untuk Excel import
/// </summary>
public class ImportResult<T>
{
    public List<T> SuccessItems { get; set; } = new();
    public List<ImportError> Errors { get; set; } = new();
    public int TotalRows { get; set; }
    public int SuccessCount => SuccessItems.Count;
    public int ErrorCount => Errors.Count;
    public bool HasErrors => Errors.Count > 0;
    public TimeSpan Duration { get; set; }
}

public class ImportError
{
    public int RowNumber { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;
}