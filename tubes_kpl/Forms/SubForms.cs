// ============================================================
// Forms/SubForms.cs
// Semua sub-form untuk fitur spesifik sistem
// ============================================================

using Tubes_KPL.Contracts;
using Tubes_KPL.Helpers;
using Tubes_KPL.Models;
using Tubes_KPL.Services;

namespace Tubes_KPL.Forms;

// ============================================================
// MataKuliahForm - Daftar Mata Kuliah
// ============================================================
public class MataKuliahForm : Form
{
    private readonly User _user;
    private readonly IGradeService _gradeService;
    private ListView _listView = null!;

    public MataKuliahForm(User user, IGradeService gradeService)
    {
        _user = user; _gradeService = gradeService;
        InitUI(); _ = LoadData();
    }

    private void InitUI()
    {
        Text = "Daftar Mata Kuliah";
        BackColor = Color.FromArgb(245, 247, 250);

        var header = CreateHeader("📚 Mata Kuliah");

        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.White
        };
        _listView.Columns.Add("Kode MK", 100);
        _listView.Columns.Add("Nama Mata Kuliah", 280);
        _listView.Columns.Add("SKS", 50);
        _listView.Columns.Add("Kelas", 100);
        _listView.Columns.Add("Semester", 160);
        _listView.Columns.Add("Dosen", 200);

        Controls.Add(_listView);
        Controls.Add(header);
    }

    private async Task LoadData()
    {
        _listView.Items.Clear();
        IEnumerable<MataKuliah> mks;
        if (_user.Role == UserRole.Dosen)
            mks = await _gradeService.GetMataKuliahByDosenAsync(_user.NIM_NIP);
        else
            mks = await _gradeService.GetMataKuliahByMahasiswaAsync(_user.Id);

        foreach (var mk in mks)
        {
            var item = new ListViewItem(mk.Id);
            item.SubItems.Add(mk.Nama);
            item.SubItems.Add(mk.SKS.ToString());
            item.SubItems.Add(mk.Kelas);
            item.SubItems.Add(mk.Semester);
            item.SubItems.Add(mk.DosenId);
            _listView.Items.Add(item);
        }
    }

    private void InitializeComponent()
    {

    }

    private static Panel CreateHeader(string title)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Color.White };
        var lbl = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 50, 80),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(15, 0, 0, 0)
        };
        panel.Controls.Add(lbl);
        return panel;
    }
}

// ============================================================
// InputNilaiForm - Input Nilai Manual
// FR-002, FR-004 (Table-Driven + DbC validation)
// ============================================================
public class InputNilaiForm : Form
{
    private readonly User _dosen;
    private readonly IGradeService _gradeService;
    private readonly INotifikasiService _notifikasiService;
    private ComboBox _cmbMK = null!, _cmbKomponen = null!;
    private DataGridView _dgv = null!;
    private List<MataKuliah> _mataKuliahs = new();
    private List<GradeComponent> _komponens = new();
    private List<(int MhsId, string NIM, string Nama, double CurrentNilai)> _mahasiswaList = new();

    public InputNilaiForm(User dosen, IGradeService gradeService, INotifikasiService notif)
    {
        _dosen = dosen; _gradeService = gradeService; _notifikasiService = notif;
        InitUI(); _ = LoadMataKuliah();
    }

    private void InitUI()
    {
        Text = "Input Nilai Mahasiswa";
        BackColor = Color.FromArgb(245, 247, 250);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Color.White, Padding = new Padding(15) };

        var lblMK = new Label { Text = "Mata Kuliah:", Location = new Point(15, 15), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbMK = new ComboBox { Location = new Point(15, 35), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbMK.SelectedIndexChanged += async (_, _) => await LoadKomponen();

        var lblKomp = new Label { Text = "Komponen Nilai:", Location = new Point(330, 15), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbKomponen = new ComboBox { Location = new Point(330, 35), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbKomponen.SelectedIndexChanged += async (_, _) => await LoadNilaiGrid();

        var btnSave = new Button
        {
            Text = "💾 Simpan Nilai",
            Location = new Point(600, 30),
            Size = new Size(140, 35),
            BackColor = Color.FromArgb(0, 82, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += async (_, _) => await SaveNilai();

        topPanel.Controls.AddRange(new Control[] { lblMK, _cmbMK, lblKomp, _cmbKomponen, btnSave });

        _dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            Font = new Font("Segoe UI", 9.5f),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            GridColor = Color.FromArgb(220, 225, 235)
        };
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "NIM", HeaderText = "NIM", ReadOnly = true, Width = 140 });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nama", HeaderText = "Nama Mahasiswa", ReadOnly = true });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nilai", HeaderText = "Nilai (0-100)" });
        _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "MhsId", HeaderText = "ID", ReadOnly = true, Visible = false });

        // Real-time validation coloring
        _dgv.CellValueChanged += (_, e) =>
        {
            if (e.ColumnIndex == 2 && e.RowIndex >= 0)
            {
                var cell = _dgv.Rows[e.RowIndex].Cells["Nilai"];
                if (double.TryParse(cell.Value?.ToString(), out double v))
                {
                    var vr = ValidationHelper.ValidateNilai(v);
                    cell.Style.BackColor = vr.IsValid ? Color.FromArgb(230, 255, 240) : Color.FromArgb(255, 220, 220);
                }
            }
        };
        _dgv.CellEndEdit += (s, e) => _dgv.NotifyCurrentCellDirty(true);

        Controls.Add(_dgv);
        Controls.Add(topPanel);
    }

    private async Task LoadMataKuliah()
    {
        _mataKuliahs = (await _gradeService.GetMataKuliahByDosenAsync(_dosen.NIM_NIP)).ToList();
        _cmbMK.Items.Clear();
        foreach (var mk in _mataKuliahs) _cmbMK.Items.Add($"{mk.Id} - {mk.Nama}");
        if (_cmbMK.Items.Count > 0) _cmbMK.SelectedIndex = 0;
    }

    private async Task LoadKomponen()
    {
        if (_cmbMK.SelectedIndex < 0) return;
        var mk = _mataKuliahs[_cmbMK.SelectedIndex];
        _komponens = (await _gradeService.GetKomponenByMataKuliahAsync(mk.Id)).ToList();
        _cmbKomponen.Items.Clear();
        foreach (var k in _komponens) _cmbKomponen.Items.Add($"{k.Nama} (Bobot: {k.Bobot:P0})");
        if (_cmbKomponen.Items.Count > 0) _cmbKomponen.SelectedIndex = 0;
    }

    private async Task LoadNilaiGrid()
    {
        if (_cmbMK.SelectedIndex < 0 || _cmbKomponen.SelectedIndex < 0) return;
        var mk = _mataKuliahs[_cmbMK.SelectedIndex];
        var komponen = _komponens[_cmbKomponen.SelectedIndex];

        _dgv.Rows.Clear();
        var nilaiList = await _gradeService.GetNilaiByMahasiswaAsync(0, mk.Id);
        // Load all students in the class
        using var conn = ManajemenNilai.Infrastructure.DatabaseContext.Instance.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT u.Id, u.NIM_NIP, u.Nama FROM Users u WHERE u.Role = 1 AND u.IsActive = 1";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int id = r.GetInt32(0);
            string nim = r.GetString(1);
            string nama = r.GetString(2);
            var nilaiEntry = nilaiList.FirstOrDefault(n => n.MahasiswaId == id && n.KomponenId == komponen.Id);
            _dgv.Rows.Add(nim, nama, nilaiEntry?.Nilai.ToString("F1") ?? "", id);
        }
    }

    private async Task SaveNilai()
    {
        if (_cmbKomponen.SelectedIndex < 0) { MessageBox.Show("Pilih komponen terlebih dahulu."); return; }
        var komponen = _komponens[_cmbKomponen.SelectedIndex];
        int saved = 0, errors = 0;

        foreach (DataGridViewRow row in _dgv.Rows)
        {
            if (!int.TryParse(row.Cells["MhsId"].Value?.ToString(), out int mhsId)) continue;
            if (!double.TryParse(row.Cells["Nilai"].Value?.ToString(), out double nilai)) continue;

            var vr = ValidationHelper.ValidateNilai(nilai);
            if (!vr.IsValid) { errors++; continue; }

            var ok = await _gradeService.InputNilaiAsync(new NilaiMahasiswa
            {
                MahasiswaId = mhsId,
                KomponenId = komponen.Id,
                Nilai = nilai,
                EnteredById = _dosen.Id
            });
            if (ok) saved++;
        }

        MessageBox.Show($"✓ {saved} nilai berhasil disimpan.\n{(errors > 0 ? $"⚠ {errors} nilai tidak valid (diabaikan)." : "")}",
            "Simpan Nilai", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

// ============================================================
// ImportExcelForm - Import dari CSV/Excel
// FR-003: Generics technique
// ============================================================
public class ImportExcelForm : Form
{
    private readonly User _dosen;
    private readonly IGradeService _gradeService;
    private Label _lblFile = null!, _lblStatus = null!;
    private RichTextBox _rtbLog = null!;
    private ComboBox _cmbMK = null!;
    private List<MataKuliah> _mataKuliahs = new();

    public ImportExcelForm(User dosen, IGradeService gradeService)
    {
        _dosen = dosen; _gradeService = gradeService;
        InitUI(); _ = LoadMK();
    }

    private void InitUI()
    {
        Text = "Import Nilai dari Excel/CSV";
        BackColor = Color.White;
        Padding = new Padding(20);

        var lblTitle = new Label { Text = "📤 Import Nilai dari File Excel/CSV", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 50, 80), Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5, 0, 0, 0) };

        var infoLabel = new Label
        {
            Text = "Format file CSV: Baris pertama adalah header (NIM, [Nama Komponen 1], [Nama Komponen 2], ...)\n" +
                   "Contoh: NIM,Tugas Mingguan,Kuis 1,UTS",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(100, 120, 150),
            Padding = new Padding(5, 5, 0, 0)
        };

        var optPanel = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(5) };

        var lblMK = new Label { Text = "Mata Kuliah:", Location = new Point(5, 5), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbMK = new ComboBox { Location = new Point(5, 25), Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };

        _lblFile = new Label { Text = "Belum ada file dipilih", Location = new Point(5, 55), Width = 450, ForeColor = Color.Gray };

        var btnPilih = new Button { Text = "📂 Pilih File", Location = new Point(470, 50), Size = new Size(120, 28), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 245, 255), Cursor = Cursors.Hand };
        btnPilih.Click += PilihFile;

        var btnTemplate = new Button { Text = "📋 Unduh Template", Location = new Point(600, 50), Size = new Size(140, 28), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 255, 245), Cursor = Cursors.Hand };
        btnTemplate.Click += async (_, _) => await UnduhTemplate();

        optPanel.Controls.AddRange(new Control[] { lblMK, _cmbMK, _lblFile, btnPilih, btnTemplate });

        var btnImport = new Button
        {
            Text = "▶ Mulai Import",
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.FromArgb(0, 82, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnImport.FlatAppearance.BorderSize = 0;
        btnImport.Click += async (_, _) => await ImportData();

        _rtbLog = new RichTextBox { Dock = DockStyle.Fill, Font = new Font("Courier New", 9f), BackColor = Color.FromArgb(20, 30, 45), ForeColor = Color.LimeGreen, ReadOnly = true };
        _rtbLog.AppendText("Log import akan tampil di sini...\n");

        _lblStatus = new Label { Dock = DockStyle.Bottom, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f), Padding = new Padding(5, 0, 0, 0) };

        Controls.AddRange(new Control[] { _rtbLog, _lblStatus, btnImport, optPanel, infoLabel, lblTitle });
    }

    private async Task LoadMK()
    {
        _mataKuliahs = (await _gradeService.GetMataKuliahByDosenAsync(_dosen.NIM_NIP)).ToList();
        foreach (var mk in _mataKuliahs) _cmbMK.Items.Add($"{mk.Id} - {mk.Nama}");
        if (_cmbMK.Items.Count > 0) _cmbMK.SelectedIndex = 0;
    }

    private string? _selectedFile;

    private void PilihFile(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*", Title = "Pilih File Nilai" };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _selectedFile = dlg.FileName;
            _lblFile.Text = Path.GetFileName(_selectedFile);
            _lblFile.ForeColor = Color.FromArgb(0, 100, 0);
        }
    }

    private async Task ImportData()
    {
        if (_selectedFile == null || _cmbMK.SelectedIndex < 0) { MessageBox.Show("Pilih file dan mata kuliah terlebih dahulu."); return; }
        var mk = _mataKuliahs[_cmbMK.SelectedIndex];
        _rtbLog.Clear();
        AppendLog($"▶ Memulai import: {Path.GetFileName(_selectedFile)}", Color.Yellow);
        AppendLog($"   Mata Kuliah: {mk.Nama}", Color.Cyan);

        var result = await _gradeService.ImportFromExcelAsync(_selectedFile, mk.Id, _dosen.Id);

        AppendLog($"\n✓ Berhasil: {result.SuccessCount} baris", Color.LimeGreen);
        AppendLog($"⚠ Error: {result.ErrorCount} baris", result.HasErrors ? Color.OrangeRed : Color.LimeGreen);
        AppendLog($"⏱ Durasi: {result.Duration.TotalSeconds:F2} detik", Color.Cyan);

        if (result.HasErrors)
        {
            AppendLog("\nDetail Error:", Color.OrangeRed);
            foreach (var err in result.Errors)
                AppendLog($"  Baris {err.RowNumber}, {err.ColumnName}: {err.Message}", Color.OrangeRed);
        }

        _lblStatus.Text = result.HasErrors
            ? $"⚠ Import selesai dengan {result.ErrorCount} error"
            : "✓ Import berhasil tanpa error";
        _lblStatus.ForeColor = result.HasErrors ? Color.OrangeRed : Color.Green;
    }

    private async Task UnduhTemplate()
    {
        if (_cmbMK.SelectedIndex < 0) { MessageBox.Show("Pilih mata kuliah terlebih dahulu."); return; }
        var mk = _mataKuliahs[_cmbMK.SelectedIndex];
        var komponens = await _gradeService.GetKomponenByMataKuliahAsync(mk.Id);
        string csv = ExcelImportService.GenerateTemplateCsv(komponens.Select(k => k.Nama));

        using var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = $"template_nilai_{mk.Id}.csv" };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await File.WriteAllTextAsync(dlg.FileName, csv);
            MessageBox.Show("✓ Template berhasil diunduh!", "Download", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void AppendLog(string text, Color color)
    {
        if (_rtbLog.InvokeRequired) { _rtbLog.Invoke(() => AppendLog(text, color)); return; }
        _rtbLog.SelectionStart = _rtbLog.TextLength;
        _rtbLog.SelectionLength = 0;
        _rtbLog.SelectionColor = color;
        _rtbLog.AppendText(text + "\n");
        _rtbLog.SelectionColor = _rtbLog.ForeColor;
        _rtbLog.ScrollToCaret();
    }
}

// ============================================================
// RekapNilaiForm - Rekap Nilai (FR-006)
// ============================================================
public class RekapNilaiForm : Form
{
    private readonly User _user;
    private readonly IGradeService _gradeService;
    private DataGridView _dgv = null!;
    private ComboBox _cmbMK = null!;
    private List<MataKuliah> _mataKuliahs = new();
    private Label _lblSummary = null!;

    public RekapNilaiForm(User user, IGradeService gradeService)
    {
        _user = user; _gradeService = gradeService;
        InitUI(); _ = LoadMK();
    }

    private void InitUI()
    {
        Text = "Rekap Nilai Mahasiswa";
        BackColor = Color.FromArgb(245, 247, 250);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White, Padding = new Padding(15, 10, 15, 10) };
        var lblMK = new Label { Text = "Mata Kuliah:", Location = new Point(15, 18), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbMK = new ComboBox { Location = new Point(100, 15), Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbMK.SelectedIndexChanged += async (_, _) => await LoadRekap();

        var btnExport = new Button { Text = "📥 Export CSV", Location = new Point(470, 12), Size = new Size(120, 32), BackColor = Color.FromArgb(34, 139, 87), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnExport.FlatAppearance.BorderSize = 0;
        btnExport.Click += async (_, _) => await ExportCsv();
        topPanel.Controls.AddRange(new Control[] { lblMK, _cmbMK, btnExport });

        _dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            Font = new Font("Segoe UI", 9.5f),
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _dgv.Columns.Add("NIM", "NIM");
        _dgv.Columns.Add("Nama", "Nama Mahasiswa");
        _dgv.Columns.Add("NilaiAkhir", "Nilai Akhir");
        _dgv.Columns.Add("Grade", "Grade");

        _lblSummary = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f),
            Padding = new Padding(15, 0, 0, 0),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(60, 80, 110)
        };

        Controls.AddRange(new Control[] { _dgv, _lblSummary, topPanel });
    }

    private async Task LoadMK()
    {
        _mataKuliahs = (await _gradeService.GetMataKuliahByDosenAsync(_user.NIM_NIP)).ToList();
        foreach (var mk in _mataKuliahs) _cmbMK.Items.Add($"{mk.Id} - {mk.Nama}");
        if (_cmbMK.Items.Count > 0) _cmbMK.SelectedIndex = 0;
    }

    private async Task LoadRekap()
    {
        if (_cmbMK.SelectedIndex < 0) return;
        var mk = _mataKuliahs[_cmbMK.SelectedIndex];
        _dgv.Rows.Clear();

        var rekap = (await _gradeService.GetRekapNilaiAsync(mk.Id)).ToList();
        foreach (var h in rekap)
        {
            int idx = _dgv.Rows.Add(h.NIM, h.NamaMahasiswa, h.NilaiAkhir.ToString("F2"), h.Grade);
            Color gradeColor = h.Grade switch
            {
                "A" or "AB" => Color.FromArgb(220, 255, 230),
                "B" or "BC" => Color.FromArgb(230, 245, 255),
                "C" => Color.FromArgb(255, 250, 220),
                _ => Color.FromArgb(255, 230, 230)
            };
            _dgv.Rows[idx].DefaultCellStyle.BackColor = gradeColor;
        }

        if (rekap.Count > 0)
        {
            double avg = rekap.Average(h => h.NilaiAkhir);
            _lblSummary.Text = $"Total: {rekap.Count} mahasiswa | Rata-rata: {avg:F2} | " +
                               $"Grade A: {rekap.Count(h => h.Grade == "A" || h.Grade == "AB")} | " +
                               $"Tidak lulus: {rekap.Count(h => h.Grade is "D" or "E")}";
        }
    }

    private async Task ExportCsv()
    {
        if (_cmbMK.SelectedIndex < 0) return;
        var mk = _mataKuliahs[_cmbMK.SelectedIndex];
        var rekap = await _gradeService.GetRekapNilaiAsync(mk.Id);
        var komponen = await _gradeService.GetKomponenByMataKuliahAsync(mk.Id);
        string csv = ReportHelper.GenerateRekapCsv(rekap, komponen);

        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"rekap_{mk.Id}.csv" };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await File.WriteAllTextAsync(dlg.FileName, csv);
            MessageBox.Show("✓ Rekap berhasil diekspor!", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

// ============================================================
// PublikasiForm - Publikasi Bertahap (FR-007)
// Runtime Configuration + Notifikasi
// ============================================================
public class PublikasiForm : Form
{
    private readonly User _dosen;
    private readonly IGradeService _gradeService;
    private readonly INotifikasiService _notif;
    private ListView _lvKomponen = null!;
    private ComboBox _cmbMK = null!;
    private List<MataKuliah> _mks = new();
    private List<GradeComponent> _komponens = new();

    public PublikasiForm(User dosen, IGradeService gradeService, INotifikasiService notif)
    {
        _dosen = dosen; _gradeService = gradeService; _notif = notif;
        InitUI(); _ = LoadMK();
    }

    private void InitUI()
    {
        Text = "Publikasi Nilai Bertahap";
        BackColor = Color.White;

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(0, 82, 165), Padding = new Padding(15, 10, 15, 10) };
        var lblMK = new Label { Text = "Mata Kuliah:", Location = new Point(15, 18), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.White };
        _cmbMK = new ComboBox { Location = new Point(110, 15), Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbMK.SelectedIndexChanged += async (_, _) => await LoadKomponen();
        topPanel.Controls.AddRange(new Control[] { lblMK, _cmbMK });

        var infoPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(255, 250, 220), Padding = new Padding(15, 10, 15, 0) };
        infoPanel.Controls.Add(new Label { Text = "ℹ️  Centang komponen dan klik 'Publikasikan' untuk merilis nilai kepada mahasiswa.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(120, 90, 0) });

        _lvKomponen = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            CheckBoxes = true,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9.5f)
        };
        _lvKomponen.Columns.Add("Nama Komponen", 200);
        _lvKomponen.Columns.Add("Tipe", 100);
        _lvKomponen.Columns.Add("Bobot", 80);
        _lvKomponen.Columns.Add("Pertemuan", 100);
        _lvKomponen.Columns.Add("Status", 120);

        var btnPub = new Button
        {
            Text = "📢 Publikasikan Komponen Terpilih",
            Dock = DockStyle.Bottom,
            Height = 45,
            BackColor = Color.FromArgb(34, 139, 87),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnPub.FlatAppearance.BorderSize = 0;
        btnPub.Click += async (_, _) => await PublishSelected();

        Controls.AddRange(new Control[] { _lvKomponen, btnPub, infoPanel, topPanel });
    }

    private async Task LoadMK()
    {
        _mks = (await _gradeService.GetMataKuliahByDosenAsync(_dosen.NIM_NIP)).ToList();
        foreach (var mk in _mks) _cmbMK.Items.Add($"{mk.Id} - {mk.Nama}");
        if (_cmbMK.Items.Count > 0) _cmbMK.SelectedIndex = 0;
    }

    private async Task LoadKomponen()
    {
        if (_cmbMK.SelectedIndex < 0) return;
        var mk = _mks[_cmbMK.SelectedIndex];
        _komponens = (await _gradeService.GetKomponenByMataKuliahAsync(mk.Id)).ToList();
        _lvKomponen.Items.Clear();
        foreach (var k in _komponens)
        {
            var item = new ListViewItem(k.Nama) { Tag = k.Id };
            item.SubItems.Add(k.Tipe.ToString());
            item.SubItems.Add(k.Bobot.ToString("P0"));
            item.SubItems.Add($"Pekan {k.Pertemuan}");
            item.SubItems.Add(k.IsPublished ? "✅ Sudah Dipublikasi" : "⏸ Belum Dipublikasi");
            item.ForeColor = k.IsPublished ? Color.Gray : Color.Black;
            _lvKomponen.Items.Add(item);
        }
    }

    private async Task PublishSelected()
    {
        var selectedItems = _lvKomponen.CheckedItems.Cast<ListViewItem>().ToList();
        if (selectedItems.Count == 0) { MessageBox.Show("Pilih minimal satu komponen."); return; }

        int published = 0;
        foreach (var item in selectedItems)
        {
            int id = (int)item.Tag!;
            bool ok = await _gradeService.PublishKomponenAsync(id);
            if (ok) { published++; item.SubItems[4].Text = "✅ Sudah Dipublikasi"; item.ForeColor = Color.Gray; }
        }
        MessageBox.Show($"✓ {published} komponen berhasil dipublikasikan!\nMahasiswa akan menerima notifikasi.", "Publikasi", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

// ============================================================
// KomplainDosenForm - Lihat & Respons Komplain (FR-010)
// ============================================================
public class KomplainDosenForm : Form
{
    private readonly User _dosen;
    private readonly IKomplainService _komplainService;
    private ListView _lv = null!;
    private List<Komplain> _komplains = new();

    public KomplainDosenForm(User dosen, IKomplainService komplainService)
    {
        _dosen = dosen; _komplainService = komplainService;
        InitUI(); _ = LoadData();
    }

    private void InitUI()
    {
        Text = "Komplain Mahasiswa";
        BackColor = Color.White;

        _lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, Font = new Font("Segoe UI", 9.5f) };
        _lv.Columns.Add("No", 40);
        _lv.Columns.Add("Tanggal", 120);
        _lv.Columns.Add("Mahasiswa", 50);
        _lv.Columns.Add("Pesan", 300);
        _lv.Columns.Add("Status", 120);

        var btnRespond = new Button { Text = "💬 Berikan Respons", Dock = DockStyle.Bottom, Height = 40, BackColor = Color.FromArgb(0, 82, 165), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnRespond.FlatAppearance.BorderSize = 0;
        btnRespond.Click += async (_, _) => await RespondKomplain();

        Controls.AddRange(new Control[] { _lv, btnRespond });
    }

    private async Task LoadData()
    {
        _komplains = (await _komplainService.GetKomplainByDosenAsync(_dosen.Id)).ToList();
        _lv.Items.Clear();
        int no = 1;
        foreach (var k in _komplains)
        {
            var item = new ListViewItem(no++.ToString()) { Tag = k.Id };
            item.SubItems.Add(k.CreatedAt.ToString("dd/MM/yyyy HH:mm"));
            item.SubItems.Add(k.MahasiswaId.ToString());
            item.SubItems.Add(k.Pesan.Length > 60 ? k.Pesan[..60] + "..." : k.Pesan);
            item.SubItems.Add(ReportHelper.StatusBadge(k.Status));
            if (k.Status == StatusKomplain.Pending) item.BackColor = Color.FromArgb(255, 245, 220);
            _lv.Items.Add(item);
        }
    }

    private async Task RespondKomplain()
    {
        if (_lv.SelectedItems.Count == 0) { MessageBox.Show("Pilih komplain terlebih dahulu."); return; }
        int idx = _lv.SelectedItems[0].Index;
        var k = _komplains[idx];
        if (k.Status == StatusKomplain.Selesai) { MessageBox.Show("Komplain ini sudah ditanggapi."); return; }

        string respons = Microsoft.VisualBasic.Interaction.InputBox(
            $"Komplain:\n{k.Pesan}\n\nBerikan respons Anda:", "Respons Komplain", "");
        if (string.IsNullOrWhiteSpace(respons)) return;

        bool ok = await _komplainService.ResponKomplainAsync(k.Id, respons, _dosen.Id);
        if (ok) { MessageBox.Show("✓ Respons berhasil dikirim!"); await LoadData(); }
    }
}

// ============================================================
// KomplainMahasiswaForm - Ajukan Komplain (FR-010)
// ============================================================
public class KomplainMahasiswaForm : Form
{
    private readonly User _mahasiswa;
    private readonly IKomplainService _komplainService;
    private ListView _lv = null!;
    private TextBox _txtPesan = null!;
    private NumericUpDown _numKomp = null!;

    public KomplainMahasiswaForm(User mahasiswa, IKomplainService komplainService)
    {
        _mahasiswa = mahasiswa; _komplainService = komplainService;
        InitUI(); _ = LoadData();
    }

    private void InitUI()
    {
        Text = "Komplain & Pertanyaan Nilai";
        BackColor = Color.White;

        var inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 140, BackColor = Color.FromArgb(248, 250, 255), Padding = new Padding(15) };
        var lblPesan = new Label { Text = "Pesan Komplain (min. 10 karakter):", Location = new Point(15, 5), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtPesan = new TextBox { Location = new Point(15, 25), Size = new Size(600, 60), Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 9.5f) };

        var lblKomp = new Label { Text = "ID Komponen:", Location = new Point(15, 95), AutoSize = true };
        _numKomp = new NumericUpDown { Location = new Point(110, 92), Width = 80, Minimum = 1, Maximum = 100, Value = 1 };

        var btnKirim = new Button { Text = "📨 Kirim Komplain", Location = new Point(210, 90), Size = new Size(150, 32), BackColor = Color.FromArgb(0, 82, 165), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btnKirim.FlatAppearance.BorderSize = 0;
        btnKirim.Click += async (_, _) => await KirimKomplain();

        inputPanel.Controls.AddRange(new Control[] { lblPesan, _txtPesan, lblKomp, _numKomp, btnKirim });

        _lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, Font = new Font("Segoe UI", 9.5f) };
        _lv.Columns.Add("Tanggal", 140); _lv.Columns.Add("Pesan", 350); _lv.Columns.Add("Status", 120); _lv.Columns.Add("Respons Dosen", 250);

        var header = new Label { Dock = DockStyle.Top, Height = 40, Text = "💬 Riwayat Komplain Anda", Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 50, 80), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), BackColor = Color.White };

        Controls.AddRange(new Control[] { _lv, inputPanel, header });
    }

    private async Task LoadData()
    {
        _lv.Items.Clear();
        var list = await _komplainService.GetKomplainByMahasiswaAsync(_mahasiswa.Id);
        foreach (var k in list)
        {
            var item = new ListViewItem(k.CreatedAt.ToString("dd/MM/yyyy HH:mm"));
            item.SubItems.Add(k.Pesan.Length > 60 ? k.Pesan[..60] + "..." : k.Pesan);
            item.SubItems.Add(ReportHelper.StatusBadge(k.Status));
            item.SubItems.Add(k.ResponDosen ?? "-");
            _lv.Items.Add(item);
        }
    }

    private async Task KirimKomplain()
    {
        var vr = ValidationHelper.ValidateAll(
            () => string.IsNullOrWhiteSpace(_txtPesan.Text) || _txtPesan.Text.Length < 10
                ? ValidationResult.Fail("Pesan minimal 10 karakter")
                : ValidationResult.Ok()
        );
        if (!vr.IsValid) { MessageBox.Show(vr.ErrorMessage); return; }

        try
        {
            await _komplainService.AjukanKomplainAsync(_mahasiswa.Id, 1, (int)_numKomp.Value, _txtPesan.Text);
            MessageBox.Show("✓ Komplain berhasil dikirim! Dosen akan merespons segera.", "Berhasil", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtPesan.Clear();
            await LoadData();
        }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
    }
}

// ============================================================
// NilaiMahasiswaForm - Lihat Nilai Sendiri (FR-008)
// ============================================================
public class NilaiMahasiswaForm : Form
{
    private readonly User _mahasiswa;
    private readonly IGradeService _gradeService;
    private DataGridView _dgv = null!;
    private ComboBox _cmbMK = null!;
    private List<MataKuliah> _mks = new();
    private Label _lblTotal = null!;

    public NilaiMahasiswaForm(User mahasiswa, IGradeService gradeService)
    {
        _mahasiswa = mahasiswa; _gradeService = gradeService;
        InitUI(); _ = LoadMK();
    }

    private void InitUI()
    {
        Text = "Nilai Saya";
        BackColor = Color.White;

        var top = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Color.FromArgb(0, 82, 165), Padding = new Padding(15, 10, 15, 10) };
        var lbl = new Label { Text = "Mata Kuliah:", Location = new Point(10, 17), AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cmbMK = new ComboBox { Location = new Point(105, 14), Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbMK.SelectedIndexChanged += async (_, _) => await LoadNilai();
        top.Controls.AddRange(new Control[] { lbl, _cmbMK });

        _dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            Font = new Font("Segoe UI", 9.5f),
            AllowUserToAddRows = false,
            ReadOnly = true
        };
        _dgv.Columns.Add("Komponen", "Komponen Nilai");
        _dgv.Columns.Add("Tipe", "Tipe");
        _dgv.Columns.Add("Bobot", "Bobot");
        _dgv.Columns.Add("Nilai", "Nilai");
        _dgv.Columns.Add("Kontribusi", "Kontribusi");

        _lblTotal = new Label { Dock = DockStyle.Bottom, Height = 40, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Padding = new Padding(0, 0, 20, 0), BackColor = Color.FromArgb(240, 245, 255) };

        Controls.AddRange(new Control[] { _dgv, _lblTotal, top });
    }

    private async Task LoadMK()
    {
        _mks = (await _gradeService.GetMataKuliahByMahasiswaAsync(_mahasiswa.Id)).ToList();
        foreach (var mk in _mks) _cmbMK.Items.Add($"{mk.Id} - {mk.Nama}");
        if (_cmbMK.Items.Count > 0) _cmbMK.SelectedIndex = 0;
    }

    private async Task LoadNilai()
    {
        if (_cmbMK.SelectedIndex < 0) return;
        var mk = _mks[_cmbMK.SelectedIndex];
        _dgv.Rows.Clear();

        var nilaiList = await _gradeService.GetNilaiByMahasiswaAsync(_mahasiswa.Id, mk.Id);
        var komponenList = await _gradeService.GetKomponenByMataKuliahAsync(mk.Id);

        double totalNilai = 0;
        foreach (var k in komponenList.Where(k => k.IsPublished))
        {
            var n = nilaiList.FirstOrDefault(x => x.KomponenId == k.Id);
            double kontribusi = n != null ? n.Nilai * k.Bobot : 0;
            totalNilai += kontribusi;
            int idx = _dgv.Rows.Add(k.Nama, k.Tipe, k.Bobot.ToString("P0"), n?.Nilai.ToString("F1") ?? "-", kontribusi.ToString("F2"));
            _dgv.Rows[idx].DefaultCellStyle.BackColor = n != null ? Color.FromArgb(235, 255, 240) : Color.FromArgb(255, 245, 245);
        }

        var hasil = await _gradeService.HitungNilaiAkhirAsync(_mahasiswa.Id, mk.Id);
        _lblTotal.Text = $"Nilai Sementara: {hasil.NilaiAkhir:F2}  |  Grade: {hasil.Grade}";
    }
}

// ============================================================
// Stub forms untuk fitur tambahan
// ============================================================

public class PerkembanganNilaiForm : Form
{
    public PerkembanganNilaiForm(User user, IGradeService gradeService)
    {
        Text = "Perkembangan Nilai Bertahap";
        BackColor = Color.White;
        var lbl = new Label { Text = "📈 Grafik perkembangan nilai per minggu\n(Fitur ini menampilkan nilai yang telah dipublikasikan oleh dosen secara berkala)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11f) };
        Controls.Add(lbl);
    }
}

public class KelolaUserForm : Form
{
    public KelolaUserForm(User user)
    {
        Text = "Kelola Pengguna";
        BackColor = Color.White;
        var lbl = new Label { Text = "👥 Kelola data pengguna (Dosen & Mahasiswa)\nFR-001: Pengelolaan Data Pengguna", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11f) };
        Controls.Add(lbl);
    }
}

public class KonfigurasiForm : Form
{
    public KonfigurasiForm()
    {
        Text = "Konfigurasi Sistem";
        BackColor = Color.White;
        Padding = new Padding(20);

        var cfg = RuntimeConfig.Instance;
        var title = new Label { Text = "⚙️ Runtime Configuration", Font = new Font("Segoe UI", 13f, FontStyle.Bold), Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleLeft };

        var info = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Courier New", 9.5f),
            BackColor = Color.FromArgb(245, 248, 255)
        };
        info.AppendText($"Teknik: RUNTIME CONFIGURATION (Laksamana Dwi Daffa)\n");
        info.AppendText($"Config dapat diubah tanpa recompile aplikasi.\n\n");
        info.AppendText($"Institusi      : {cfg.InstitutionName}\n");
        info.AppendText($"Semester       : {cfg.CurrentSemester}\n");
        info.AppendText($"API Base URL   : {cfg.ApiBaseUrl}\n");
        info.AppendText($"Max Import     : {cfg.MaxImportRows} baris\n");
        info.AppendText($"Import Timeout : {cfg.ImportTimeoutSeconds}s\n");
        info.AppendText($"Min Lulus      : {cfg.NilaiMinimumLulus}\n");
        info.AppendText($"Max Login Fail : {cfg.MaxFailedLoginAttempts}x\n");
        info.AppendText($"Lock Duration  : {cfg.LockDurationMinutes} menit\n");
        info.AppendText($"Auto Publish   : {cfg.AutoPublishEnabled}\n");
        info.AppendText($"Notifikasi     : {cfg.NotificationsEnabled}\n");
        info.AppendText($"\nFile konfigurasi:\n{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ManajemenNilai", "config.json")}");

        Controls.AddRange(new Control[] { info, title });
    }
}

public class NotifikasiForm : Form
{
    public NotifikasiForm(User user, INotifikasiService notifService)
    {
        Text = "Notifikasi";
        BackColor = Color.White;
        var lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, Font = new Font("Segoe UI", 9.5f) };
        lv.Columns.Add("Waktu", 130); lv.Columns.Add("Judul", 200); lv.Columns.Add("Pesan", 400); lv.Columns.Add("Kategori", 80);
        Controls.Add(lv);

        _ = Task.Run(async () =>
        {
            var notifs = await notifService.GetNotifikasiAsync(user.Id);
            lv.Invoke(() =>
            {
                foreach (var n in notifs)
                {
                    var item = new ListViewItem(n.CreatedAt.ToString("dd/MM HH:mm"));
                    item.SubItems.Add(n.Judul); item.SubItems.Add(n.Pesan); item.SubItems.Add(n.Kategori);
                    if (!n.IsRead) item.BackColor = Color.FromArgb(235, 245, 255);
                    lv.Items.Add(item);
                }
            });
        });
    }
}
