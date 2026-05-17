// ============================================================
// Infrastructure/DatabaseContext.cs
// Local SQLite database setup (untuk development/demo)
// ============================================================

using System.Data;
using Microsoft.Data.Sqlite;

namespace ManajemenNilai.Infrastructure;

/// <summary>
/// Singleton database context menggunakan SQLite.
/// Pattern: Code Reuse/Library - Laksamana Dwi Daffa
/// </summary>
public sealed class DatabaseContext
{
    private static DatabaseContext? _instance;
    private static readonly object _lock = new();
    private readonly string _connectionString;

    public static DatabaseContext Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new DatabaseContext();
                return _instance;
            }
        }
    }

    private DatabaseContext()
    {
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ManajemenNilai", "nilai.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";

        InitializeDatabase();
    }

    public IDbConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void InitializeDatabase()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                NIM_NIP TEXT NOT NULL UNIQUE,
                Nama TEXT NOT NULL,
                Email TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                Role INTEGER NOT NULL,
                IsActive INTEGER DEFAULT 1,
                Jabatan TEXT,
                Kelas TEXT,
                Program TEXT,
                AngkatanTahun INTEGER,
                Prodi TEXT,
                CreatedAt TEXT DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS MataKuliah (
                Id TEXT PRIMARY KEY,
                Nama TEXT NOT NULL,
                SKS INTEGER NOT NULL,
                DosenId TEXT NOT NULL,
                Kelas TEXT,
                Semester TEXT
            );

            CREATE TABLE IF NOT EXISTS GradeComponents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MataKuliahId TEXT NOT NULL,
                Nama TEXT NOT NULL,
                Tipe INTEGER NOT NULL,
                Bobot REAL NOT NULL,
                Pertemuan INTEGER DEFAULT 0,
                IsPublished INTEGER DEFAULT 0,
                PublishedAt TEXT,
                FOREIGN KEY (MataKuliahId) REFERENCES MataKuliah(Id)
            );

            CREATE TABLE IF NOT EXISTS NilaiMahasiswa (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MahasiswaId INTEGER NOT NULL,
                KomponenId INTEGER NOT NULL,
                Nilai REAL NOT NULL CHECK(Nilai >= 0 AND Nilai <= 100),
                Keterangan TEXT,
                EnteredAt TEXT DEFAULT (datetime('now')),
                EnteredById INTEGER,
                FOREIGN KEY (MahasiswaId) REFERENCES Users(Id),
                FOREIGN KEY (KomponenId) REFERENCES GradeComponents(Id),
                UNIQUE(MahasiswaId, KomponenId)
            );

            CREATE TABLE IF NOT EXISTS Komplain (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MahasiswaId INTEGER NOT NULL,
                NilaiId INTEGER NOT NULL,
                KomponenId INTEGER NOT NULL,
                Pesan TEXT NOT NULL,
                Status INTEGER DEFAULT 0,
                ResponDosen TEXT,
                CreatedAt TEXT DEFAULT (datetime('now')),
                ResponseAt TEXT,
                RespondedById INTEGER,
                FOREIGN KEY (MahasiswaId) REFERENCES Users(Id)
            );

            CREATE TABLE IF NOT EXISTS Notifikasi (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Judul TEXT NOT NULL,
                Pesan TEXT NOT NULL,
                IsRead INTEGER DEFAULT 0,
                Kategori TEXT DEFAULT 'Umum',
                CreatedAt TEXT DEFAULT (datetime('now')),
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );

            CREATE TABLE IF NOT EXISTS ImportLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DosenId INTEGER NOT NULL,
                MataKuliahId TEXT NOT NULL,
                FileName TEXT,
                JumlahBaris INTEGER,
                Status TEXT,
                ImportedAt TEXT DEFAULT (datetime('now'))
            );
        ";
        cmd.ExecuteNonQuery();

        SeedData(conn);
    }

    private void SeedData(IDbConnection conn)
    {
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Users";
        var count = (long)(checkCmd.ExecuteScalar() ?? 0L);
        if (count > 0) return;

        // Hash passwords (simple for demo)
        string dosenHash = BCrypt.Net.BCrypt.HashPassword("dosen123");
        string mhsHash = BCrypt.Net.BCrypt.HashPassword("mhs123");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO Users (NIM_NIP, Nama, Email, PasswordHash, Role, Jabatan, Prodi)
            VALUES
            ('198501012010011001', 'Dr. Budi Santoso, M.T.', 'budi@telkomuniversity.ac.id', '{dosenHash}', 0, 'Lektor', 'Teknik Informatika'),
            ('198703152012012002', 'Dr. Siti Rahayu, M.Kom.', 'siti@telkomuniversity.ac.id', '{dosenHash}', 0, 'Asisten Ahli', 'Sistem Informasi');

            INSERT INTO Users (NIM_NIP, Nama, Email, PasswordHash, Role, Kelas, Program, AngkatanTahun)
            VALUES
            ('1302220142', 'Muhammad Rafi Nadhif', 'rafi@student.telkomuniversity.ac.id', '{mhsHash}', 1, 'IF-46-02', 'S1 Informatika', 2022),
            ('103022400104', 'Muhammad Aditya Arham', 'aditya@student.telkomuniversity.ac.id', '{mhsHash}', 1, 'IF-46-02', 'S1 Informatika', 2022),
            ('103022400101', 'Salman Al Farizin', 'salman@student.telkomuniversity.ac.id', '{mhsHash}', 1, 'IF-46-02', 'S1 Informatika', 2022),
            ('103022400139', 'Laksamana Dwi Daffa', 'daffa@student.telkomuniversity.ac.id', '{mhsHash}', 1, 'IF-46-02', 'S1 Informatika', 2022),
            ('103022400033', 'Annisa Azzahra Putri', 'annisa@student.telkomuniversity.ac.id', '{mhsHash}', 1, 'IF-46-02', 'S1 Informatika', 2022),
            ('103022400050', 'Ahmad Fajar Rizki', 'fajar@student.telkomuniversity.ac.id', '{mhsHash}', 1, 'IF-46-02', 'S1 Informatika', 2022);

            INSERT INTO MataKuliah (Id, Nama, SKS, DosenId, Kelas, Semester)
            VALUES
            ('IF3110', 'Rekayasa Perangkat Lunak', 3, '198501012010011001', 'IF-46-02', 'Ganjil 2024/2025'),
            ('IF3120', 'Basis Data', 3, '198501012010011001', 'IF-46-02', 'Ganjil 2024/2025'),
            ('IF3130', 'Jaringan Komputer', 2, '198703152012012002', 'IF-46-02', 'Ganjil 2024/2025');

            INSERT INTO GradeComponents (MataKuliahId, Nama, Tipe, Bobot, Pertemuan, IsPublished)
            VALUES
            ('IF3110', 'Tugas Mingguan', 0, 0.15, 1, 1),
            ('IF3110', 'Kuis 1', 2, 0.10, 3, 1),
            ('IF3110', 'Tugas Besar', 1, 0.25, 8, 0),
            ('IF3110', 'UTS', 4, 0.20, 8, 1),
            ('IF3110', 'UAS', 5, 0.30, 16, 0);

            INSERT INTO GradeComponents (MataKuliahId, Nama, Tipe, Bobot, Pertemuan, IsPublished)
            VALUES
            ('IF3120', 'Kuis 1', 2, 0.15, 2, 1),
            ('IF3120', 'Praktikum', 3, 0.20, 4, 1),
            ('IF3120', 'Tugas Besar', 1, 0.25, 8, 0),
            ('IF3120', 'UTS', 4, 0.20, 8, 1),
            ('IF3120', 'UAS', 5, 0.20, 16, 0);
        ";
        cmd.ExecuteNonQuery();

        // Seed some grade data
        using var nilaiCmd = conn.CreateCommand();
        nilaiCmd.CommandText = @"
            INSERT OR IGNORE INTO NilaiMahasiswa (MahasiswaId, KomponenId, Nilai, EnteredById)
            SELECT u.Id, gc.Id, 
                CASE (u.Id + gc.Id) % 5 
                    WHEN 0 THEN 85 WHEN 1 THEN 78 WHEN 2 THEN 90 WHEN 3 THEN 72 ELSE 88 
                END,
                1
            FROM Users u
            CROSS JOIN GradeComponents gc
            WHERE u.Role = 1 AND gc.IsPublished = 1;
        ";
        nilaiCmd.ExecuteNonQuery();
    }
}