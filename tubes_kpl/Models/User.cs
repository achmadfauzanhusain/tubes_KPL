using System;
using System.Collections.Generic;
using System.Text;

namespace Tubes_KPL.Models
{
    public enum KomponenType
    {
        Kognitif,
        TugasBesar,
        Kuis,
        Praktikum,
        UTS,
        UAS
    }

    public class GradeComponent
    {
        public int Id { get; set; }
        public string MataKuliahId { get; set; } = string.Empty;
        public string Nama { get; set; } = string.Empty;
        public KomponenType Tipe { get; set; }
        public double Bobot { get; set; }         
        public int Pertemuan { get; set; }       
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }

        public override string ToString() =>
            $"{Nama} ({Tipe}) - Bobot: {Bobot:P0}";
    }

    public class NilaiMahasiswa
    {
        public int Id { get; set; }
        public int MahasiswaId { get; set; }
        public int KomponenId { get; set; }
        public double Nilai { get; set; }
        public string Keterangan { get; set; } = string.Empty;
        public DateTime EnteredAt { get; set; } = DateTime.Now;
        public int EnteredById { get; set; }

        public bool IsValid() => Nilai >= 0 && Nilai <= 100;
    }

    public class HasilAkhir
    {
        public int MahasiswaId { get; set; }
        public string NamaMahasiswa { get; set; } = string.Empty;
        public string NIM { get; set; } = string.Empty;
        public string MataKuliahId { get; set; } = string.Empty;
        public string NamaMataKuliah { get; set; } = string.Empty;
        public double NilaiAkhir { get; set; }
        public string Grade { get; set; } = string.Empty;

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
}
