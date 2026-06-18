using System.Diagnostics.Contracts;

namespace ManajemenNilai.Models;

public enum StatusKomplain { Pending, DiProses, Selesai, Ditolak }

public class Komplain
{
    public int Id { get; set; }
    public int MahasiswaId { get; set; }
    public int NilaiId { get; set; }
    public int KomponenId { get; set; }
    public string Pesan { get; set; } = string.Empty;
    public StatusKomplain Status { get; set; } = StatusKomplain.Pending;
    public string? ResponDosen { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ResponseAt { get; set; }
    public int? RespondedById { get; set; }

    // DbC Precondition: Pesan tidak boleh kosong
    public static Komplain Create(int mahasiswaId, int nilaiId, int komponenId, string pesan)
    {
        Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(pesan),
            "Pesan komplain tidak boleh kosong");
        Contract.Requires<ArgumentException>(mahasiswaId > 0, "ID mahasiswa tidak valid");

        return new Komplain
        {
            MahasiswaId = mahasiswaId,
            NilaiId = nilaiId,
            KomponenId = komponenId,
            Pesan = pesan.Trim()
        };
    }

    // DbC Postcondition: Status harus berubah setelah respons
    public void SetRespons(string respons, int dosenId)
    {
        Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(respons),
            "Respons tidak boleh kosong");
        Contract.Requires<InvalidOperationException>(
            Status == StatusKomplain.Pending || Status == StatusKomplain.DiProses,
            "Komplain sudah selesai diproses");

        ResponDosen = respons;
        RespondedById = dosenId;
        ResponseAt = DateTime.Now;
        Status = StatusKomplain.Selesai;

        Contract.Ensures(Status == StatusKomplain.Selesai);
    }
}

/// <summary>
/// FR-007: Publikasi Bertahap - notifikasi ke mahasiswa
/// </summary>
public class Notifikasi
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Judul { get; set; } = string.Empty;
    public string Pesan { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Kategori { get; set; } = "Umum";  // Nilai, Komplain, Sistem
}