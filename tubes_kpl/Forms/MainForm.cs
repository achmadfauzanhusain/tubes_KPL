// ============================================================
// Forms/MainForm.cs
// Dashboard utama (Dosen) - menampilkan semua fitur
// ============================================================

using ManajemenNilai.Contracts;
using ManajemenNilai.Helpers;
using ManajemenNilai.Models;
using ManajemenNilai.Services;

namespace ManajemenNilai.Forms;

public class MainForm : Form
{
    private readonly User _currentUser;
    private readonly IGradeService _gradeService;
    private readonly IKomplainService _komplainService;
    private readonly INotifikasiService _notifikasiService;
    private readonly AuthService _authService;

    // Navigation
    private Panel _sidePanel = null!;
    private Panel _contentPanel = null!;
    private Label _lblUserInfo = null!;
    private Label _lblNotifBadge = null!;
    private Button? _activeNavButton;

    // Content forms
    private Form? _currentSubForm;

    public MainForm(
        User currentUser,
        IGradeService gradeService,
        IKomplainService komplainService,
        INotifikasiService notifikasiService,
        AuthService authService)
    {
        _currentUser = currentUser;
        _gradeService = gradeService;
        _komplainService = komplainService;
        _notifikasiService = notifikasiService;
        _authService = authService;
        InitializeComponent();
        LoadDashboard();
    }

    private void InitializeComponent()
    {
        Text = $"Sistem Manajemen Nilai - {_currentUser.Nama} ({_currentUser.Role})";
        Size = new Size(1200, 750);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);
        MinimumSize = new Size(1000, 650);
        Font = new Font("Segoe UI", 9.5f);

        // === SIDEBAR ===
        _sidePanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 230,
            BackColor = Color.FromArgb(15, 35, 65),
            Padding = new Padding(0)
        };

        // Logo area
        var logoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(0, 82, 165),
        };
        var lblLogo = new Label
        {
            Text = "📊 ManajemenNilai",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        logoPanel.Controls.Add(lblLogo);

        // User info area
        var userPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(25, 50, 90),
            Padding = new Padding(15, 10, 10, 10)
        };
        _lblUserInfo = new Label
        {
            Text = $"👤 {_currentUser.Nama}\n{_currentUser.Role} | {_currentUser.NIM_NIP}",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(180, 200, 230),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        userPanel.Controls.Add(_lblUserInfo);

        // Navigation buttons
        var navContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(10, 15, 10, 10),
            AutoScroll = true,
            BackColor = Color.Transparent
        };

        var navItems = GetNavItems();
        foreach (var (icon, text, action) in navItems)
        {
            var btn = CreateNavButton(icon, text);
            btn.Click += (_, _) =>
            {
                SetActiveNav(btn);
                action();
            };
            navContainer.Controls.Add(btn);
        }

        // Logout button at bottom
        var logoutPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(10, 25, 50),
        };
        var btnLogout = new Button
        {
            Text = "🚪  Keluar",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(200, 100, 100),
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        btnLogout.FlatAppearance.BorderSize = 0;
        btnLogout.Click += async (_, _) =>
        {
            if (MessageBox.Show("Keluar dari sistem?", "Konfirmasi",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                await _authService.LogoutAsync(_currentUser.Id);
                Close();
            }
        };
        logoutPanel.Controls.Add(btnLogout);

        _sidePanel.Controls.Add(navContainer);
        _sidePanel.Controls.Add(userPanel);
        _sidePanel.Controls.Add(logoPanel);
        _sidePanel.Controls.Add(logoutPanel);

        // === CONTENT AREA ===
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(20)
        };

        // Top bar
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.White,
            Padding = new Padding(20, 0, 20, 0)
        };

        var lblPageTitle = new Label
        {
            Text = "Dashboard",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 50, 80),
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Width = 400
        };

        _lblNotifBadge = new Label
        {
            Text = "🔔 0",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(0, 82, 165),
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 80,
            Cursor = Cursors.Hand
        };
        _lblNotifBadge.Click += (_, _) => ShowNotifikasi();

        topBar.Controls.Add(lblPageTitle);
        topBar.Controls.Add(_lblNotifBadge);
        _contentPanel.Controls.Add(topBar);

        Controls.Add(_contentPanel);
        Controls.Add(_sidePanel);

        // Load notification count
        _ = RefreshNotifCountAsync();

        // Auto-refresh notifications every 30s
        var timer = new System.Windows.Forms.Timer { Interval = 30000 };
        timer.Tick += async (_, _) => await RefreshNotifCountAsync();
        timer.Start();
    }

    private List<(string Icon, string Text, Action Action)> GetNavItems()
    {
        if (_currentUser.Role == UserRole.Dosen)
        {
            return new()
            {
                ("🏠", "Dashboard", LoadDashboard),
                ("📚", "Mata Kuliah", ShowMataKuliah),
                ("✏️", "Input Nilai", ShowInputNilai),
                ("📤", "Import Excel", ShowImportExcel),
                ("📊", "Rekap Nilai", ShowRekapNilai),
                ("📢", "Publikasi Nilai", ShowPublikasi),
                ("💬", "Komplain Mahasiswa", ShowKomplainDosen),
                ("👥", "Kelola Pengguna", ShowKelolaUser),
                ("⚙️", "Konfigurasi", ShowKonfigurasi),
            };
        }
        else
        {
            return new()
            {
                ("🏠", "Dashboard", LoadDashboard),
                ("📊", "Nilai Saya", ShowNilaiMahasiswa),
                ("📈", "Perkembangan Nilai", ShowPerkembangan),
                ("💬", "Ajukan Komplain", ShowKomplainMahasiswa),
                ("🔔", "Notifikasi", ShowNotifikasi),
            };
        }
    }

    private void LoadDashboard()
    {
        ClearContent();
        var dashboard = _currentUser.Role == UserRole.Dosen
            ? (Control)new DosenDashboardPanel(_currentUser, _gradeService, _komplainService)
            : new MahasiswaDashboardPanel(_currentUser, _gradeService);
        dashboard.Dock = DockStyle.Fill;
        _contentPanel.Controls.Add(dashboard);
    }

    private void ShowMataKuliah() => LoadSubForm(
        new MataKuliahForm(_currentUser, _gradeService));

    private void ShowInputNilai() => LoadSubForm(
        new InputNilaiForm(_currentUser, _gradeService, _notifikasiService));

    private void ShowImportExcel() => LoadSubForm(
        new ImportExcelForm(_currentUser, _gradeService));

    private void ShowRekapNilai() => LoadSubForm(
        new RekapNilaiForm(_currentUser, _gradeService));

    private void ShowPublikasi() => LoadSubForm(
        new PublikasiForm(_currentUser, _gradeService, _notifikasiService));

    private void ShowKomplainDosen() => LoadSubForm(
        new KomplainDosenForm(_currentUser, _komplainService));

    private void ShowKelolaUser() => LoadSubForm(
        new KelolaUserForm(_currentUser));

    private void ShowKonfigurasi() => LoadSubForm(
        new KonfigurasiForm());

    private void ShowNilaiMahasiswa() => LoadSubForm(
        new NilaiMahasiswaForm(_currentUser, _gradeService));

    private void ShowPerkembangan() => LoadSubForm(
        new PerkembanganNilaiForm(_currentUser, _gradeService));

    private void ShowKomplainMahasiswa() => LoadSubForm(
        new KomplainMahasiswaForm(_currentUser, _komplainService));

    private void ShowNotifikasi() => LoadSubForm(
        new NotifikasiForm(_currentUser, _notifikasiService));

    private void LoadSubForm(Form form)
    {
        ClearContent();
        form.TopLevel = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.Dock = DockStyle.Fill;
        _contentPanel.Controls.Add(form);
        form.Show();
        _currentSubForm = form;
    }

    private void ClearContent()
    {
        _currentSubForm?.Close();
        var toRemove = _contentPanel.Controls.OfType<Control>()
            .Where(c => c is not Panel p || p.Dock != DockStyle.Top)
            .ToList();
        foreach (var c in toRemove)
            _contentPanel.Controls.Remove(c);
    }

    private async Task RefreshNotifCountAsync()
    {
        int count = await _notifikasiService.GetUnreadCountAsync(_currentUser.Id);
        _lblNotifBadge.Text = count > 0 ? $"🔔 {count}" : "🔔";
        _lblNotifBadge.ForeColor = count > 0 ?
            Color.FromArgb(220, 60, 60) : Color.FromArgb(0, 82, 165);
    }

    private Button CreateNavButton(string icon, string text)
    {
        var btn = new Button
        {
            Text = $"  {icon}  {text}",
            Size = new Size(208, 42),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(180, 200, 230),
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 0, 2)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 70, 120);
        btn.MouseEnter += (_, _) =>
        {
            if (btn != _activeNavButton)
                btn.BackColor = Color.FromArgb(30, 55, 100);
        };
        btn.MouseLeave += (_, _) =>
        {
            if (btn != _activeNavButton)
                btn.BackColor = Color.Transparent;
        };
        return btn;
    }

    private void SetActiveNav(Button btn)
    {
        if (_activeNavButton != null)
        {
            _activeNavButton.BackColor = Color.Transparent;
            _activeNavButton.ForeColor = Color.FromArgb(180, 200, 230);
        }
        _activeNavButton = btn;
        btn.BackColor = Color.FromArgb(0, 82, 165);
        btn.ForeColor = Color.White;
    }
}

// ============================================================
// Quick Dashboard Panels
// ============================================================

public class DosenDashboardPanel : Panel
{
    public DosenDashboardPanel(User dosen, IGradeService gradeService, IKomplainService komplainService)
    {
        BackColor = Color.Transparent;
        Padding = new Padding(10);

        var title = new Label
        {
            Text = $"Selamat Datang, {dosen.Nama}!",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 50, 80),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        var subtitle = new Label
        {
            Text = $"Sistem Manajemen Nilai | {RuntimeConfig.Instance.InstitutionName} | {RuntimeConfig.Instance.CurrentSemester}",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(100, 120, 150),
            AutoSize = true,
            Location = new Point(2, 38)
        };

        var cardPanel = new FlowLayoutPanel
        {
            Location = new Point(0, 80),
            Size = new Size(900, 150),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            WrapContents = false
        };

        cardPanel.Controls.Add(CreateCard("📚", "Mata Kuliah", "3 Mata Kuliah", Color.FromArgb(0, 82, 165)));
        cardPanel.Controls.Add(CreateCard("👥", "Mahasiswa", "6 Mahasiswa", Color.FromArgb(34, 139, 87)));
        cardPanel.Controls.Add(CreateCard("✅", "Nilai Terinput", "36 Data", Color.FromArgb(180, 100, 0)));
        cardPanel.Controls.Add(CreateCard("💬", "Komplain Pending", "0 Komplain", Color.FromArgb(150, 30, 30)));

        var techPanel = new GroupBox
        {
            Text = "Teknik Konstruksi yang Diimplementasikan",
            Location = new Point(0, 250),
            Size = new Size(900, 220),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 50, 80)
        };

        var techInfo = new Label
        {
            Text = "Anggota Kelompok & Teknik Konstruksi Perangkat Lunak:\n\n" +
                   "1. Rafi Nadhif        → AUTOMATA (AuthService state machine) + API (REST API layer)\n" +
                   "2. Aditya Arham       → TABLE-DRIVEN (GradeService bobot/grade table) + DbC (validasi nilai)\n" +
                   "3. Salman Al Farizin  → PARAMETERIZATION/GENERICS (ExcelImportProcessor<T>) + AUTOMATA (import state)\n" +
                   "4. Laksamana Daffa    → RUNTIME CONFIG (RuntimeConfig.cs) + CODE REUSE (NotifikasiService, ReportHelper)\n" +
                   "5. Annisa Azzahra     → DbC (KomplainService contracts) + TABLE-DRIVEN (grade validation rules)\n" +
                   "6. Ahmad Fajar Rizki  → API (LocalApiService routing) + CODE REUSE (ValidationHelper library)",
            Location = new Point(15, 25),
            Size = new Size(860, 175),
            Font = new Font("Courier New", 9f),
            ForeColor = Color.FromArgb(40, 60, 90)
        };
        techPanel.Controls.Add(techInfo);

        Controls.AddRange(new Control[] { title, subtitle, cardPanel, techPanel });
    }

    private static Panel CreateCard(string icon, string title, string value, Color color)
    {
        var card = new Panel
        {
            Size = new Size(200, 120),
            BackColor = Color.White,
            Margin = new Padding(0, 0, 15, 0),
            Cursor = Cursors.Hand
        };

        var iconLbl = new Label
        {
            Text = icon,
            Font = new Font("Segoe UI Emoji", 22f),
            Location = new Point(15, 12),
            AutoSize = true
        };

        var titleLbl = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(120, 140, 160),
            Location = new Point(15, 55),
            AutoSize = true
        };

        var valueLbl = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = color,
            Location = new Point(15, 75),
            AutoSize = true
        };

        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 4,
            BackColor = color
        };

        card.Controls.AddRange(new Control[] { topBar, iconLbl, titleLbl, valueLbl });
        return card;
    }
}

public class MahasiswaDashboardPanel : Panel
{
    public MahasiswaDashboardPanel(User mahasiswa, IGradeService gradeService)
    {
        BackColor = Color.Transparent;

        var title = new Label
        {
            Text = $"Halo, {mahasiswa.Nama}!",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 50, 80),
            AutoSize = true,
            Location = new Point(10, 10)
        };

        var subtitle = new Label
        {
            Text = $"NIM: {mahasiswa.NIM_NIP} | {RuntimeConfig.Instance.CurrentSemester}",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(100, 120, 150),
            AutoSize = true,
            Location = new Point(12, 48)
        };

        var infoLbl = new Label
        {
            Text = "Selamat datang di portal nilai mahasiswa.\n" +
                   "Anda dapat memantau perkembangan nilai dan mengajukan komplain kepada dosen melalui menu di samping.",
            Location = new Point(10, 90),
            Size = new Size(700, 60),
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(80, 100, 130)
        };

        Controls.AddRange(new Control[] { title, subtitle, infoLbl });
    }
}