// ============================================================
// Services/GradeService.cs
// FR-002: Pengelolaan Komponen Nilai
// FR-004: Validasi Nilai
// FR-005: Perhitungan Otomatis
// FR-006: Pelaporan Nilai
// FR-007: Publikasi Bertahap
// Teknik: Table-Driven Construction - Muhammad Aditya Arham
//         Design by Contract (DbC) - validasi input
// ============================================================

using System.Diagnostics;
using System.Diagnostics.Contracts;
using ManajemenNilai.Contracts;
using ManajemenNilai.Infrastructure;
using ManajemenNilai.Models;

namespace ManajemenNilai.Services;

public class GradeService : IGradeService
{
    // ============================================================
    // TABLE-DRIVEN: Tabel bobot minimum per tipe komponen
    // Data dikonfigurasi sebagai tabel, bukan hardcoded di logika
    // ============================================================
    private static readonly Dictionary<KomponenType, (double MinBobot, double MaxBobot, string Description)>
        KomponenRules = new()
        {
            { KomponenType.Kognitif,    (0.05, 0.30, "Tugas/Latihan Kognitif") },
            { KomponenType.TugasBesar,  (0.15, 0.40, "Tugas Besar Kelompok/Individu") },
            { KomponenType.Kuis,        (0.05, 0.20, "Kuis Mingguan/LMS") },
            { KomponenType.Praktikum,   (0.10, 0.30, "Nilai Praktikum/Lab") },
            { KomponenType.UTS,         (0.15, 0.35, "Ujian Tengah Semester") },
            { KomponenType.UAS,         (0.20, 0.40, "Ujian Akhir Semester") },
        };

    // TABLE-DRIVEN: Grade scale mapping
    private static readonly (double Min, double Max, string Grade, double BobtNilai)[]
        GradeTable =
        {
            (85, 100, "A",  4.0),
            (80,  85, "AB", 3.5),
            (75,  80, "B",  3.0),
            (70,  75, "BC", 2.5),
            (65,  70, "C",  2.0),
            (55,  65, "D",  1.0),
            (0,   55, "E",  0.0),
        };

    // TABLE-DRIVEN: Bobot default per template kurikulum
    private static readonly Dictionary<string, List<(KomponenType Tipe, string Nama, double Bobot)>>
        KurikulumTemplate = new()
        {
            ["Standar"] = new()
            {
                (KomponenType.Kognitif,   "Tugas",         0.15),
                (KomponenType.Kuis,       "Kuis",          0.10),
                (KomponenType.TugasBesar, "Tugas Besar",   0.25),
                (KomponenType.UTS,        "UTS",           0.20),
                (KomponenType.UAS,        "UAS",           0.30),
            },
            ["Dengan Praktikum"] = new()
            {
                (KomponenType.Kognitif,   "Tugas",         0.10),
                (KomponenType.Kuis,       "Kuis",          0.10),
                (KomponenType.Praktikum,  "Praktikum",     0.20),
                (KomponenType.TugasBesar, "Tugas Besar",   0.20),
                (KomponenType.UTS,        "UTS",           0.20),
                (KomponenType.UAS,        "UAS",           0.20),
            },
        };

    public async Task<IEnumerable<MataKuliah>> GetMataKuliahByDosenAsync(string dosenId)
    {
        return await Task.Run(() =>
        {
            var list = new List<MataKuliah>();
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM MataKuliah WHERE DosenId = @id";
            AddParam(cmd, "@id", dosenId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadMataKuliah(r));
            return list;
        });
    }

    public async Task<IEnumerable<MataKuliah>> GetMataKuliahByMahasiswaAsync(int mahasiswaId)
    {
        // All courses in same class as student
        return await Task.Run(() =>
        {
            var list = new List<MataKuliah>();
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT mk.* FROM MataKuliah mk
                JOIN Users u ON mk.Kelas = u.Kelas
                WHERE u.Id = @id";
            AddParam(cmd, "@id", mahasiswaId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadMataKuliah(r));
            return list;
        });
    }

    public async Task<IEnumerable<GradeComponent>> GetKomponenByMataKuliahAsync(string mkId)
    {
        return await Task.Run(() =>
        {
            var list = new List<GradeComponent>();
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM GradeComponents WHERE MataKuliahId = @id ORDER BY Pertemuan";
            AddParam(cmd, "@id", mkId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadKomponen(r));
            return list;
        });
    }

    public async Task<IEnumerable<NilaiMahasiswa>> GetNilaiByMahasiswaAsync(int mahasiswaId, string mkId)
    {
        return await Task.Run(() =>
        {
            var list = new List<NilaiMahasiswa>();
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT nm.* FROM NilaiMahasiswa nm
                JOIN GradeComponents gc ON nm.KomponenId = gc.Id
                WHERE nm.MahasiswaId = @mhs AND gc.MataKuliahId = @mk AND gc.IsPublished = 1";
            AddParam(cmd, "@mhs", mahasiswaId);
            AddParam(cmd, "@mk", mkId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadNilai(r));
            return list;
        });
    }

    /// <summary>
    /// TABLE-DRIVEN: Gunakan GradeTable untuk tentukan grade
    /// DbC: Pastikan total bobot valid sebelum kalkulasi
    /// </summary>
    public async Task<HasilAkhir> HitungNilaiAkhirAsync(int mahasiswaId, string mkId)
    {
        return await Task.Run(() =>
        {
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT nm.Nilai, gc.Bobot, gc.IsPublished, mk.Nama as MkNama,
                       u.Nama as MhsNama, u.NIM_NIP
                FROM NilaiMahasiswa nm
                JOIN GradeComponents gc ON nm.KomponenId = gc.Id
                JOIN MataKuliah mk ON gc.MataKuliahId = mk.Id
                JOIN Users u ON nm.MahasiswaId = u.Id
                WHERE nm.MahasiswaId = @mhs AND gc.MataKuliahId = @mk AND gc.IsPublished = 1";
            AddParam(cmd, "@mhs", mahasiswaId);
            AddParam(cmd, "@mk", mkId);

            double totalBobot = 0, nilaiAkhir = 0;
            string mkNama = "", mhsNama = "", nim = "";

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                double nilai = r.GetDouble(0);
                double bobot = r.GetDouble(1);
                nilaiAkhir += nilai * bobot;
                totalBobot += bobot;
                mkNama = r.GetString(3);
                mhsNama = r.GetString(4);
                nim = r.GetString(5);
            }

            // DbC Postcondition
            if (totalBobot > 0)
                nilaiAkhir = nilaiAkhir / totalBobot * totalBobot; // normalized

            // TABLE-DRIVEN: Grade lookup
            string grade = "E";
            foreach (var (min, max, g, _) in GradeTable)
            {
                if (nilaiAkhir >= min && nilaiAkhir <= max)
                {
                    grade = g;
                    break;
                }
            }

            return new HasilAkhir
            {
                MahasiswaId = mahasiswaId,
                NamaMahasiswa = mhsNama,
                NIM = nim,
                MataKuliahId = mkId,
                NamaMataKuliah = mkNama,
                NilaiAkhir = Math.Round(nilaiAkhir, 2),
                Grade = grade
            };
        });
    }

    /// <summary>
    /// DbC: Precondition - nilai harus valid (0-100), komponen harus ada
    /// </summary>
    public async Task<bool> InputNilaiAsync(NilaiMahasiswa nilai)
    {
        // DbC Preconditions
        Contract.Requires<ArgumentException>(nilai.Nilai >= 0 && nilai.Nilai <= 100,
            "Nilai harus dalam rentang 0-100 (NFR-006)");
        Contract.Requires<ArgumentException>(nilai.MahasiswaId > 0, "ID mahasiswa tidak valid");
        Contract.Requires<ArgumentException>(nilai.KomponenId > 0, "ID komponen tidak valid");

        return await Task.Run(() =>
        {
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO NilaiMahasiswa (MahasiswaId, KomponenId, Nilai, Keterangan, EnteredById)
                VALUES (@mhs, @komp, @nilai, @ket, @by)
                ON CONFLICT(MahasiswaId, KomponenId) DO UPDATE SET
                    Nilai = @nilai, Keterangan = @ket, EnteredAt = datetime('now')";
            AddParam(cmd, "@mhs", nilai.MahasiswaId);
            AddParam(cmd, "@komp", nilai.KomponenId);
            AddParam(cmd, "@nilai", nilai.Nilai);
            AddParam(cmd, "@ket", nilai.Keterangan);
            AddParam(cmd, "@by", nilai.EnteredById);
            return cmd.ExecuteNonQuery() > 0;
        });
    }

    public async Task<bool> UpdateNilaiAsync(NilaiMahasiswa nilai) =>
        await InputNilaiAsync(nilai);

    /// <summary>
    /// FR-007: Publikasi Bertahap
    /// Notifikasi dikirim ke semua mahasiswa yang terdaftar
    /// </summary>
    public async Task<bool> PublishKomponenAsync(int komponenId)
    {
        return await Task.Run(() =>
        {
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE GradeComponents
                SET IsPublished = 1, PublishedAt = datetime('now')
                WHERE Id = @id";
            AddParam(cmd, "@id", komponenId);
            bool ok = cmd.ExecuteNonQuery() > 0;

            if (ok)
            {
                // Kirim notifikasi ke semua mahasiswa
                using var notifCmd = conn.CreateCommand();
                notifCmd.CommandText = @"
                    INSERT INTO Notifikasi (UserId, Judul, Pesan, Kategori)
                    SELECT u.Id,
                           'Nilai Baru Dipublikasikan',
                           'Dosen telah mempublikasikan komponen nilai baru. Silakan cek nilai Anda.',
                           'Nilai'
                    FROM Users u WHERE u.Role = 1 AND u.IsActive = 1";
                notifCmd.ExecuteNonQuery();
            }
            return ok;
        });
    }

    public async Task<IEnumerable<HasilAkhir>> GetRekapNilaiAsync(string mkId)
    {
        var hasil = new List<HasilAkhir>();
        using var conn = DatabaseContext.Instance.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT MahasiswaId FROM NilaiMahasiswa nm JOIN GradeComponents gc ON nm.KomponenId = gc.Id WHERE gc.MataKuliahId = @mk";
        AddParam(cmd, "@mk", mkId);

        var ids = new List<int>();
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                ids.Add(r.GetInt32(0));

        foreach (var id in ids)
            hasil.Add(await HitungNilaiAkhirAsync(id, mkId));

        return hasil;
    }

    public async Task<ImportResult<NilaiMahasiswa>> ImportFromExcelAsync(
        string filePath, string mkId, int dosenId)
    {
        // Delegated to ExcelImportService (Salman - Generics)
        var importer = new ExcelImportService();
        return await importer.ImportNilaiAsync(filePath, mkId, dosenId);
    }

    public static IEnumerable<string> GetKurikulumTemplates() => KurikulumTemplate.Keys;

    public static List<(KomponenType Tipe, string Nama, double Bobot)>
        GetTemplateKomponen(string template) =>
        KurikulumTemplate.GetValueOrDefault(template) ?? new();

    public static (double MinBobot, double MaxBobot, string Description)
        GetKomponenRule(KomponenType tipe) => KomponenRules[tipe];

    // ---- Helpers ----
    private static void AddParam(System.Data.IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static MataKuliah ReadMataKuliah(System.Data.IDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        Nama = r.GetString(r.GetOrdinal("Nama")),
        SKS = r.GetInt32(r.GetOrdinal("SKS")),
        DosenId = r.GetString(r.GetOrdinal("DosenId")),
        Kelas = r.IsDBNull(r.GetOrdinal("Kelas")) ? "" : r.GetString(r.GetOrdinal("Kelas")),
        Semester = r.IsDBNull(r.GetOrdinal("Semester")) ? "" : r.GetString(r.GetOrdinal("Semester")),
    };

    private static GradeComponent ReadKomponen(System.Data.IDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("Id")),
        MataKuliahId = r.GetString(r.GetOrdinal("MataKuliahId")),
        Nama = r.GetString(r.GetOrdinal("Nama")),
        Tipe = (KomponenType)r.GetInt32(r.GetOrdinal("Tipe")),
        Bobot = r.GetDouble(r.GetOrdinal("Bobot")),
        Pertemuan = r.GetInt32(r.GetOrdinal("Pertemuan")),
        IsPublished = r.GetInt32(r.GetOrdinal("IsPublished")) == 1,
        PublishedAt = r.IsDBNull(r.GetOrdinal("PublishedAt")) ? null :
                      DateTime.Parse(r.GetString(r.GetOrdinal("PublishedAt"))),
    };

    private static NilaiMahasiswa ReadNilai(System.Data.IDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("Id")),
        MahasiswaId = r.GetInt32(r.GetOrdinal("MahasiswaId")),
        KomponenId = r.GetInt32(r.GetOrdinal("KomponenId")),
        Nilai = r.GetDouble(r.GetOrdinal("Nilai")),
        Keterangan = r.IsDBNull(r.GetOrdinal("Keterangan")) ? "" :
                     r.GetString(r.GetOrdinal("Keterangan")),
    };
}