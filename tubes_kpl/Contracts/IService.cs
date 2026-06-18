using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tubes_KPL.Models;

namespace Tubes_KPL.Contracts
{
    // FR-001, FR-002, FR-003, FR-004, FR-005
    // Muhammad Aditya Arham
    internal interface IGradeService
    {
        Task<IEnumerable<MataKuliah>> GetMataKuliahByDosenAsync(string dosenId);
        Task<IEnumerable<MataKuliah>> GetMataKuliahByMahasiswaAsync(int mahasiswaId);
        Task<IEnumerable<GradeComponent>> GetKomponenByMataKuliahAsync(string mkId);
        Task<IEnumerable<NilaiMahasiswa>> GetNilaiByMahasiswaAsync(int mahasiswaId, string mkId);
        Task<HasilAkhir> HitungNilaiAkhirAsync(int mahasiswaId, string mkId);
        Task<bool> InputNilaiAsync(NilaiMahasiswa nilai);
        Task<bool> UpdateNilaiAsync(NilaiMahasiswa nilai);
        Task<bool> PublishKomponenAsync(int komponenId);
        Task<IEnumerable<HasilAkhir>> GetRekapNilaiAsync(string mkId);
        Task<ImportResult<NilaiMahasiswa>> ImportFromExcelAsync(string filePath, string mkId, int dosenId);
    }

    // FR-010: Komplain
    // Muhammad Aditya Arham
    internal interface IKomplainService
    {
        Task<Complaint> AjukanKomplainAsync(int mahasiswaId, int nilaiId, int komponenId, string pesan);
        Task<bool> ResponKomplainAsync(int komplainId, string respons, int dosenId);
        Task<IEnumerable<Complaint>> GetKomplainByDosenAsync(int dosenId);
        Task<IEnumerable<Complaint>> GetKomplainByMahasiswaAsync(int mahasiswaId);
    }

    // FR-007: Publikasi Bertahap + Notifikasi
    // Muhammad Aditya Arham
    internal interface INotifikasiService
    {
        Task KirimNotifikasiAsync(int userId, string judul, string pesan, string kategori = "Umum");
        Task KirimNotifikasiBroadcastAsync(IEnumerable<int> userIds, string judul, string pesan);
        Task<IEnumerable<Notifikasi>> GetNotifikasiAsync(int userId);
        Task<bool> MarkAsReadAsync(int notifikasiId);
        Task<int> GetUnreadCountAsync(int userId);
    }
}