// Program.cs - Entry Point Utama - Achmad Fauzan Husain
// Sistem Manajemen Nilai - Telkom University

using ManajemenNilai.Forms;
using ManajemenNilai.Infrastructure;
using ManajemenNilai.Services;
using Tubes_KPL;

namespace Tubes_KPL;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Aktifkan visual styles Windows modern
        ApplicationConfiguration.Initialize();

        // Inisialisasi database (auto-create + seed data)
        try
        {
            _ = DatabaseContext.Instance;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Gagal menginisialisasi database:\n{ex.Message}",
                "Error Database",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // Inisialisasi semua service
        var authService = new AuthService();
        var notifService = new NotifikasiService();
        var komplainService = new KomplainService(notifService);
        var gradeService = new GradeService();

        // Tampilkan Login Form
        using var loginForm = new LoginForm(authService);
        var result = loginForm.ShowDialog();

        if (result == DialogResult.OK && loginForm.LoggedInUser != null)
        {
            // Login berhasil → buka Main Form
            var mainForm = new MainForm(
                loginForm.LoggedInUser,
                gradeService,
                komplainService,
                notifService,
                authService
            );
            Application.Run(mainForm);
        }
        // Jika login dibatalkan, aplikasi langsung tutup
    }
}