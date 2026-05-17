// ============================================================
// Services/ExcelImportService.cs
// FR-003: Sinkronisasi dan Impor Nilai dari Excel
// Teknik: Parameterization/Generics - Salman Al Farizin
// Generic importer yang bisa handle berbagai tipe data dari Excel
// ============================================================

using ManajemenNilai.Contracts;
using ManajemenNilai.Infrastructure;
using ManajemenNilai.Models;
using System.Diagnostics;

namespace ManajemenNilai.Services;

/// <summary>
/// Generic Excel column mapping definition.
/// Teknik Generics: ExcelColumnMap<T> dapat digunakan untuk
/// berbagai model (NilaiMahasiswa, User, MataKuliah, dll).
/// </summary>
public class ExcelColumnMap<T>
{
    public string ColumnHeader { get; set; } = string.Empty;
    public Func<string, T> Parser { get; set; } = _ => default!;
    public bool IsRequired { get; set; } = true;
    public Func<T, bool>? Validator { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
}

/// <summary>
/// Generic Excel row processor.
/// Dapat digunakan untuk import berbagai jenis data.
/// </summary>
public class ExcelImportProcessor<TRow> where TRow : class, new()
{
    private readonly List<(string Header, Func<string, object?> Parser,
        Action<TRow, object?> Setter, bool Required)> _mappings = new();

    public ExcelImportProcessor<TRow> MapColumn<TProp>(
        string header,
        Func<string, TProp> parser,
        Action<TRow, TProp> setter,
        bool required = true)
    {
        _mappings.Add((header, v => parser(v), (row, val) => setter(row, (TProp)val!), required));
        return this;
    }

    public ImportResult<TRow> Process(string[][] rawData, string[] headers)
    {
        var result = new ImportResult<TRow> { TotalRows = rawData.Length };
        var headerIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Length; i++)
            headerIdx[headers[i].Trim()] = i;

        for (int rowIdx = 0; rowIdx < rawData.Length; rowIdx++)
        {
            var row = rawData[rowIdx];
            var obj = new TRow();
            bool hasError = false;

            foreach (var (header, parser, setter, required) in _mappings)
            {
                if (!headerIdx.TryGetValue(header, out int colIdx))
                {
                    if (required)
                    {
                        result.Errors.Add(new ImportError
                        {
                            RowNumber = rowIdx + 2,
                            ColumnName = header,
                            Message = $"Kolom '{header}' tidak ditemukan"
                        });
                        hasError = true;
                    }
                    continue;
                }

                string rawVal = colIdx < row.Length ? row[colIdx] : string.Empty;
                try
                {
                    var parsed = parser(rawVal);
                    setter(obj, parsed);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowIdx + 2,
                        ColumnName = header,
                        Message = ex.Message,
                        RawValue = rawVal
                    });
                    hasError = true;
                }
            }

            if (!hasError)
                result.SuccessItems.Add(obj);
        }

        return result;
    }
}

/// <summary>
/// Concrete Excel importer for NilaiMahasiswa.
/// Uses the generic ExcelImportProcessor<T>.
/// </summary>
public class ExcelImportService
{
    /// <summary>
    /// Import nilai from .xlsx file.
    /// Expected columns: NIM, [KomponenName1], [KomponenName2], ...
    /// </summary>
    public async Task<ImportResult<NilaiMahasiswa>> ImportNilaiAsync(
        string filePath, string mkId, int dosenId)
    {
        var sw = Stopwatch.StartNew();
        var result = new ImportResult<NilaiMahasiswa>();

        if (!File.Exists(filePath))
        {
            result.Errors.Add(new ImportError { Message = "File tidak ditemukan: " + filePath });
            return result;
        }

        try
        {
            // Parse CSV/Excel (simplified CSV parser for demo without EPPlus)
            var lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length < 2)
            {
                result.Errors.Add(new ImportError { Message = "File kosong atau hanya memiliki header" });
                return result;
            }

            string[] headers = lines[0].Split(',');
            result.TotalRows = lines.Length - 1;

            // Get komponen list for this MK
            using var conn = DatabaseContext.Instance.GetConnection();
            var komponenMap = GetKomponenMap(conn, mkId);
            var mahasiswaMap = GetMahasiswaMap(conn);

            for (int i = 1; i < lines.Length; i++)
            {
                string[] cols = lines[i].Split(',');
                if (cols.Length == 0 || string.IsNullOrWhiteSpace(cols[0])) continue;

                string nim = cols[0].Trim();
                if (!mahasiswaMap.TryGetValue(nim, out int mhsId))
                {
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = i + 1,
                        ColumnName = "NIM",
                        Message = $"NIM '{nim}' tidak ditemukan",
                        RawValue = nim
                    });
                    continue;
                }

                for (int j = 1; j < headers.Length && j < cols.Length; j++)
                {
                    string komponenNama = headers[j].Trim();
                    if (!komponenMap.TryGetValue(komponenNama, out int kompId))
                        continue;

                    if (!double.TryParse(cols[j].Trim(), out double nilai))
                    {
                        result.Errors.Add(new ImportError
                        {
                            RowNumber = i + 1,
                            ColumnName = komponenNama,
                            Message = $"Nilai tidak valid: '{cols[j]}'",
                            RawValue = cols[j]
                        });
                        continue;
                    }

                    if (nilai < 0 || nilai > 100)
                    {
                        result.Errors.Add(new ImportError
                        {
                            RowNumber = i + 1,
                            ColumnName = komponenNama,
                            Message = $"Nilai harus 0-100, ditemukan: {nilai}"
                        });
                        continue;
                    }

                    var nm = new NilaiMahasiswa
                    {
                        MahasiswaId = mhsId,
                        KomponenId = kompId,
                        Nilai = nilai,
                        EnteredById = dosenId,
                        Keterangan = "Import dari Excel"
                    };

                    // Save to DB
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO NilaiMahasiswa (MahasiswaId, KomponenId, Nilai, Keterangan, EnteredById)
                        VALUES (@mhs, @komp, @nilai, @ket, @by)
                        ON CONFLICT(MahasiswaId, KomponenId) DO UPDATE SET
                            Nilai = @nilai, EnteredAt = datetime('now')";
                    AddParam(cmd, "@mhs", mhsId);
                    AddParam(cmd, "@komp", kompId);
                    AddParam(cmd, "@nilai", nilai);
                    AddParam(cmd, "@ket", "Import Excel");
                    AddParam(cmd, "@by", dosenId);
                    cmd.ExecuteNonQuery();

                    result.SuccessItems.Add(nm);
                }
            }

            // Log import
            using var logCmd = conn.CreateCommand();
            logCmd.CommandText = @"
                INSERT INTO ImportLog (DosenId, MataKuliahId, FileName, JumlahBaris, Status)
                VALUES (@dosen, @mk, @file, @rows, @status)";
            AddParam(logCmd, "@dosen", dosenId);
            AddParam(logCmd, "@mk", mkId);
            AddParam(logCmd, "@file", Path.GetFileName(filePath));
            AddParam(logCmd, "@rows", result.TotalRows);
            AddParam(logCmd, "@status", result.HasErrors ? "Partial" : "Success");
            logCmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportError { Message = "Error: " + ex.Message });
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    private static Dictionary<string, int> GetKomponenMap(System.Data.IDbConnection conn, string mkId)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nama FROM GradeComponents WHERE MataKuliahId = @mk";
        var p = cmd.CreateParameter(); p.ParameterName = "@mk"; p.Value = mkId;
        cmd.Parameters.Add(p);
        using var r = cmd.ExecuteReader();
        while (r.Read()) map[r.GetString(1)] = r.GetInt32(0);
        return map;
    }

    private static Dictionary<string, int> GetMahasiswaMap(System.Data.IDbConnection conn)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, NIM_NIP FROM Users WHERE Role = 1 AND IsActive = 1";
        using var r = cmd.ExecuteReader();
        while (r.Read()) map[r.GetString(1)] = r.GetInt32(0);
        return map;
    }

    private static void AddParam(System.Data.IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    /// <summary>
    /// Generate template CSV untuk dosen
    /// </summary>
    public static string GenerateTemplateCsv(IEnumerable<string> komponenNames)
    {
        var header = "NIM," + string.Join(",", komponenNames);
        return header + Environment.NewLine;
    }
}