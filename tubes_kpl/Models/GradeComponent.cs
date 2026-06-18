// ============================================================
// Models/GradeComponent.cs
// FR-002: Pengelolaan Komponen Nilai
// FR-005: Perhitungan Otomatis
// Teknik: Table-Driven Construction - Muhammad Aditya Arham - Annisa Azzahra Putri - Nasywa Azalia Andrean
// ============================================================

namespace Tubes_KPL.Models;

public enum KomponenType
{
    Kognitif,
    TugasBesar,
    Kuis,
    Praktikum,
    UTS,
    UAS
}

/// Table-driven: bobot per komponen didefinisikan sebagai tabel data,
/// bukan hard-coded di logika program.
public class GradeComponent
{
    public int Id { get; set; }
    public string MataKuliahId { get; set; } = string.Empty;
    public string Nama { get; set; } = string.Empty;
    public KomponenType Tipe { get; set; }
    public double Bobot { get; set; }         // 0.0 - 1.0 (e.g. 0.30 = 30%)
    public int Pertemuan { get; set; }        // minggu ke-berapa
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }

    public override string ToString() =>
        $"{Nama} ({Tipe}) - Bobot: {Bobot:P0}";
}

/// FR-004: Validasi Nilai enforced here.
public class NilaiMahasiswa
{
    public int Id { get; set; }
    public int MahasiswaId { get; set; }
    public int KomponenId { get; set; }
    public double Nilai { get; set; }         // 0 - 100
    public string Keterangan { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; } = DateTime.Now;
    public int EnteredById { get; set; }

    // NFR-006: Nilai harus dalam rentang 0–100
    public bool IsValid() => Nilai >= 0 && Nilai <= 100;
}

/// Aggregated result per student per course.
/// FR-005: Perhitungan otomatis nilai akhir.
public class HasilAkhir
{
    public int MahasiswaId { get; set; }
    public string NamaMahasiswa { get; set; } = string.Empty;
    public string NIM { get; set; } = string.Empty;
    public string MataKuliahId { get; set; } = string.Empty;
    public string NamaMataKuliah { get; set; } = string.Empty;
    public double NilaiAkhir { get; set; }
    public string Grade { get; set; } = string.Empty;   // A, B, C, D, E

    public static string HitungGrade(double nilai) => nilai switch
    {
        >= 85 => "A",
        >= 75 => "AB",
        >= 70 => "B",
        >= 65 => "BC",
        >= 60 => "C",
        >= 55 => "CD",
        >= 40 => "D",
        _ => "E"
    };
}

public class MataKuliah
{
    public string Id { get; set; } = string.Empty;
    public string Nama { get; set; } = string.Empty;
    public int SKS { get; set; }
    public string DosenId { get; set; } = string.Empty;
    public string Kelas { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
}