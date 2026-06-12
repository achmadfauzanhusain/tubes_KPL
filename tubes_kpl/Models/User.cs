using System;
using System.Collections.Generic;
using System.Text;

namespace Tubes_KPL.Models
{
    public enum UserRole { Dosen, Mahasiswa }
    public class User
    {
        public int Id { get; set; }
        public string NIM_NIP { get; set; } = string.Empty;
        public string Nama { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(!string.IsNullOrWhiteSpace(NIM_NIP), "NIM/NIP tidak boleh kosong");
            Contract.Invariant(!string.IsNullOrWhiteSpace(Nama), "Nama tidak boleh kosong");
            Contract.Invariant(Email.Contains('@'), "Email harus valid");
        }

        public override string ToString() => $"[{Role}] {Nama} ({NIM_NIP})";
    }

    public class Mahasiswa : User
    {
        public string Kelas { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public int AngkatanTahun { get; set; }

        public Mahasiswa() { Role = UserRole.Mahasiswa; }
    }

    public class Dosen : User
    {
        public string Jabatan { get; set; } = string.Empty;
        public string Prodi { get; set; } = string.Empty;

        public Dosen() { Role = UserRole.Dosen; }
    }
}
