// Helpers/ReportHelper.cs
// FR-006: Pelaporan Nilai
// Teknik: Code Reuse/Library - Laksamana Dwi Daffa

using System;
using System.Collections.Generic;
using System.Text;

using Tubes_KPL.Models;

namespace Tubes_KPL.Helpers;

public static class ReportHelper
{
    /// <summary>
    /// Generate CSV rekapitulasi nilai
    /// </summary>
    public static string GenerateRekapCsv(
        IEnumerable<HasilAkhir> hasil,
        IEnumerable<GradeComponent> komponen)
    {
        var sb = new System.Text.StringBuilder();
        var komponenList = komponen.ToList();

        // Header
        sb.Append("No,NIM,Nama Mahasiswa");
        foreach (var k in komponenList)
            sb.Append($",{k.Nama} ({k.Bobot:P0})");
        sb.AppendLine(",Nilai Akhir,Grade");

        // Data
        int no = 1;
        foreach (var h in hasil)
        {
            sb.Append($"{no++},{h.NIM},{h.NamaMahasiswa}");
            foreach (var k in komponenList)
                sb.Append(",");  // Would be filled with actual per-component values
            sb.AppendLine($",{h.NilaiAkhir:F2},{h.Grade}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate text summary report
    /// </summary>
    public static string GenerateSummaryText(
        string mataKuliah, string semester,
        IEnumerable<HasilAkhir> hasil)
    {
        var list = hasil.ToList();
        if (list.Count == 0) return "Tidak ada data nilai.";

        double avg = list.Average(h => h.NilaiAkhir);
        double max = list.Max(h => h.NilaiAkhir);
        double min = list.Min(h => h.NilaiAkhir);

        var gradeCounts = list
            .GroupBy(h => h.Grade)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=" + new string('=', 49));
        sb.AppendLine($"  REKAP NILAI: {mataKuliah}");
        sb.AppendLine($"  Semester: {semester}");
        sb.AppendLine("=" + new string('=', 49));
        sb.AppendLine($"  Total Mahasiswa : {list.Count}");
        sb.AppendLine($"  Rata-rata       : {avg:F2}");
        sb.AppendLine($"  Nilai Tertinggi : {max:F2}");
        sb.AppendLine($"  Nilai Terendah  : {min:F2}");
        sb.AppendLine();
        sb.AppendLine("  Distribusi Grade:");
        foreach (var (grade, count) in gradeCounts)
        {
            string bar = new string('█', count);
            sb.AppendLine($"    {grade,3}: {bar} ({count})");
        }
        sb.AppendLine("=" + new string('=', 49));
        return sb.ToString();
    }

    /// <summary>
    /// Format nilai untuk display
    /// </summary>
    public static string FormatNilai(double nilai) =>
        nilai switch
        {
            >= 85 => $"{nilai:F1} (A)",
            >= 75 => $"{nilai:F1} (AB)",
            >= 70 => $"{nilai:F1} (B)",
            >= 65 => $"{nilai:F1} (BC)",
            >= 60 => $"{nilai:F1} (C)",
            >= 40 => $"{nilai:F1} (D)",
            _ => $"{nilai:F1} (E)"
        };

    public static string StatusBadge(StatusKomplain status) => status switch
    {
        StatusKomplain.Pending => "⏳ Menunggu",
        StatusKomplain.DiProses => "🔄 Diproses",
        StatusKomplain.Selesai => "✅ Selesai",
        StatusKomplain.Ditolak => "❌ Ditolak",
        _ => status.ToString()
    };
}    
