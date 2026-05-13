using System.Diagnostics.Contracts;
using ManajemenNilai.Contracts;
using ManajemenNilai.Infrastructure;
using ManajemenNilai.Models;

namespace ManajemenNilai.Services;

public class KomplainService : IKomplainService
{
    private readonly INotifikasiService _notifikasi;

    public KomplainService(INotifikasiService notifikasi)
    {
        _notifikasi = notifikasi;
    }

    public async Task<Komplain> AjukanKomplainAsync(
        int mahasiswaId, int nilaiId, int komponenId, string pesan)
    {
        Contract.Requires<ArgumentException>(mahasiswaId > 0, "ID mahasiswa tidak valid");
        Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(pesan),
            "Pesan komplain tidak boleh kosong");
        Contract.Requires<ArgumentException>(pesan.Length >= 10,
            "Pesan komplain minimal 10 karakter");

        var komplain = Komplain.Create(mahasiswaId, nilaiId, komponenId, pesan);

        await Task.Run(() =>
        {
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Komplain (MahasiswaId, NilaiId, KomponenId, Pesan, Status)
                VALUES (@mhs, @nilai, @komp, @pesan, @status)";
            AddParam(cmd, "@mhs", mahasiswaId);
            AddParam(cmd, "@nilai", nilaiId);
            AddParam(cmd, "@komp", komponenId);
            AddParam(cmd, "@pesan", komplain.Pesan);
            AddParam(cmd, "@status", (int)StatusKomplain.Pending);

            cmd.ExecuteNonQuery();

            using var idCmd = conn.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid()";
            komplain.Id = (int)(long)(idCmd.ExecuteScalar() ?? 0L);
        });

        Contract.Ensures(komplain.Id > 0, "Komplain gagal disimpan");

        await NotifikasiKeDosen(mahasiswaId, komponenId);

        return komplain;
    }

    private async Task NotifikasiKeDosen(int mahasiswaId, int komponenId)
    {
        using var conn = DatabaseContext.Instance.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT DISTINCT u.Id FROM Users u
            JOIN MataKuliah mk ON mk.DosenId = u.NIM_NIP
            JOIN GradeComponents gc ON gc.MataKuliahId = mk.Id
            WHERE gc.Id = @komp AND u.Role = 0";
        AddParam(cmd, "@komp", komponenId);

        var dosenIds = new List<int>();
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                dosenIds.Add(r.GetInt32(0));

        using var mhsCmd = conn.CreateCommand();
        mhsCmd.CommandText = "SELECT Nama FROM Users WHERE Id = @id";
        AddParam(mhsCmd, "@id", mahasiswaId);
        var mhsNama = mhsCmd.ExecuteScalar()?.ToString() ?? "Mahasiswa";

        foreach (var dosenId in dosenIds)
        {
            await _notifikasi.KirimNotifikasiAsync(
                dosenId,
                "Komplain Nilai Baru",
                $"{mhsNama} mengajukan komplain nilai. Silakan cek dan berikan respons.",
                "Komplain");
        }
    }

    public async Task<bool> ResponKomplainAsync(int komplainId, string respons, int dosenId)
    {
        Contract.Requires<ArgumentException>(komplainId > 0, "ID komplain tidak valid");
        Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(respons),
            "Respons tidak boleh kosong");

        return await Task.Run(async () =>
        {
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Komplain
                SET Status = @status, ResponDosen = @respons,
                    ResponseAt = datetime('now'), RespondedById = @dosen
                WHERE Id = @id AND Status IN (0, 1)";
            AddParam(cmd, "@status", (int)StatusKomplain.Selesai);
            AddParam(cmd, "@respons", respons);
            AddParam(cmd, "@dosen", dosenId);
            AddParam(cmd, "@id", komplainId);
            bool ok = cmd.ExecuteNonQuery() > 0;

            if (ok)
            {
                using var mhsCmd = conn.CreateCommand();
                mhsCmd.CommandText = "SELECT MahasiswaId FROM Komplain WHERE Id = @id";
                AddParam(mhsCmd, "@id", komplainId);
                int mhsId = (int)(long)(mhsCmd.ExecuteScalar() ?? 0L);

                await _notifikasi.KirimNotifikasiAsync(
                    mhsId,
                    "Komplain Nilai Ditanggapi",
                    $"Dosen telah merespons komplain Anda: {respons[..Math.Min(100, respons.Length)]}...",
                    "Komplain");
            }

            Contract.Ensures(!ok || true);
            return ok;
        });
    }

    public async Task<IEnumerable<Komplain>> GetKomplainByDosenAsync(int dosenId)
    {
        return await Task.Run(() =>
        {
            var list = new List<Komplain>();
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT k.* FROM Komplain k
                JOIN GradeComponents gc ON k.KomponenId = gc.Id
                JOIN MataKuliah mk ON gc.MataKuliahId = mk.Id
                JOIN Users dosen ON mk.DosenId = dosen.NIM_NIP
                WHERE dosen.Id = @dosen
                ORDER BY k.CreatedAt DESC";
            AddParam(cmd, "@dosen", dosenId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadKomplain(r));
            return list;
        });
    }

    public async Task<IEnumerable<Komplain>> GetKomplainByMahasiswaAsync(int mahasiswaId)
    {
        return await Task.Run(() =>
        {
            var list = new List<Komplain>();
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Komplain WHERE MahasiswaId = @mhs ORDER BY CreatedAt DESC";
            AddParam(cmd, "@mhs", mahasiswaId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadKomplain(r));
            return list;
        });
    }

    private static Komplain ReadKomplain(System.Data.IDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("Id")),
        MahasiswaId = r.GetInt32(r.GetOrdinal("MahasiswaId")),
        NilaiId = r.GetInt32(r.GetOrdinal("NilaiId")),
        KomponenId = r.GetInt32(r.GetOrdinal("KomponenId")),
        Pesan = r.GetString(r.GetOrdinal("Pesan")),
        Status = (StatusKomplain)r.GetInt32(r.GetOrdinal("Status")),
        ResponDosen = r.IsDBNull(r.GetOrdinal("ResponDosen")) ? null :
                      r.GetString(r.GetOrdinal("ResponDosen")),
    };

    private static void AddParam(System.Data.IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}

public class NotifikasiService : INotifikasiService
{
    private static async Task<bool> SaveNotifikasiAsync(
        int userId, string judul, string pesan, string kategori)
    {
        return await Task.Run(() =>
        {
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Notifikasi (UserId, Judul, Pesan, Kategori)
                VALUES (@uid, @judul, @pesan, @kat)";
            AddParam(cmd, "@uid", userId);
            AddParam(cmd, "@judul", judul);
            AddParam(cmd, "@pesan", pesan);
            AddParam(cmd, "@kat", kategori);
            return cmd.ExecuteNonQuery() > 0;
        });
    }

    public async Task KirimNotifikasiAsync(
        int userId, string judul, string pesan, string kategori = "Umum") =>
        await SaveNotifikasiAsync(userId, judul, pesan, kategori);

    public async Task KirimNotifikasiBroadcastAsync(
        IEnumerable<int> userIds, string judul, string pesan)
    {        foreach (var uid in userIds)
            await SaveNotifikasiAsync(uid, judul, pesan, "Broadcast");
    }

    public async Task<IEnumerable<Notifikasi>> GetNotifikasiAsync(int userId)
    {
        return await Task.Run(() =>
        {
            var list = new List<Notifikasi>();
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM Notifikasi WHERE UserId = @uid
                ORDER BY CreatedAt DESC LIMIT 50";
            AddParam(cmd, "@uid", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Notifikasi
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    UserId = r.GetInt32(r.GetOrdinal("UserId")),
                    Judul = r.GetString(r.GetOrdinal("Judul")),
                    Pesan = r.GetString(r.GetOrdinal("Pesan")),
                    IsRead = r.GetInt32(r.GetOrdinal("IsRead")) == 1,
                    Kategori = r.GetString(r.GetOrdinal("Kategori")),
                    CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
                });
            return list;
        });
    }

    public async Task<bool> MarkAsReadAsync(int notifikasiId)
    {
        return await Task.Run(() =>
        {
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Notifikasi SET IsRead = 1 WHERE Id = @id";
            AddParam(cmd, "@id", notifikasiId);
            return cmd.ExecuteNonQuery() > 0;
        });
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await Task.Run(() =>
        {
            using var conn = DatabaseContext.Instance.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Notifikasi WHERE UserId = @uid AND IsRead = 0";
            AddParam(cmd, "@uid", userId);
            return (int)(long)(cmd.ExecuteScalar() ?? 0L);
        });
    }

    private static void AddParam(System.Data.IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}