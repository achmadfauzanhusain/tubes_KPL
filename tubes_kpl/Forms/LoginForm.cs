// ============================================================
// Forms/LoginForm.cs
// FR-009: Login dengan Akun Institusi (SSO)
// Automata State Machine visualization in UI
// ============================================================

using Tubes_KPL.Models;
using Tubes_KPL.Services;
using Tubes_KPL.Services;

namespace Tubes_KPL.Forms;

public class LoginForm : Form
{
    private readonly AuthService _authService;

    // UI Controls
    private Panel _headerPanel = null!;
    private Label _lblTitle = null!;
    private Label _lblSubtitle = null!;
    private Label _lblInstitusi = null!;
    private Panel _loginPanel = null!;
    private Label _lblNIM = null!;
    private TextBox _txtNIM = null!;
    private Label _lblPassword = null!;
    private TextBox _txtPassword = null!;
    private Button _btnLogin = null!;
    private Label _lblStatus = null!;
    private Label _lblStateInfo = null!;
    private ProgressBar _progressBar = null!;
    private PictureBox _logoBox = null!;
    private CheckBox _chkShowPassword = null!;

    public User? LoggedInUser { get; private set; }

    public LoginForm(AuthService authService)
    {
        _authService = authService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // Form settings
        Text = "Sistem Manajemen Nilai - Login";
        Size = new Size(500, 620);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 9.5f);

        // Header
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 160,
            BackColor = Color.FromArgb(0, 82, 165), // Telkom blue
        };

        _lblInstitusi = new Label
        {
            Text = "TELKOM UNIVERSITY",
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 210, 255),
            AutoSize = true,
            Location = new Point(20, 18)
        };

        _lblTitle = new Label
        {
            Text = "Sistem Manajemen Nilai",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(20, 40)
        };

        _lblSubtitle = new Label
        {
            Text = "Academic Grade Management System",
            Font = new Font("Segoe UI", 10f, FontStyle.Italic),
            ForeColor = Color.FromArgb(200, 220, 255),
            AutoSize = true,
            Location = new Point(22, 82)
        };

        var divider = new Panel
        {
            Location = new Point(20, 115),
            Size = new Size(440, 2),
            BackColor = Color.FromArgb(100, 130, 200)
        };

        var lblVersion = new Label
        {
            Text = "v1.0 | Kelompok Tubes CLO2",
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(160, 190, 240),
            AutoSize = true,
            Location = new Point(22, 125)
        };

        _headerPanel.Controls.AddRange(new Control[]
        {
            _lblInstitusi, _lblTitle, _lblSubtitle, divider, lblVersion
        });

        // Login Card Panel
        _loginPanel = new Panel
        {
            Location = new Point(50, 185),
            Size = new Size(390, 350),
            BackColor = Color.White,
            Padding = new Padding(30)
        };
        AddShadow(_loginPanel);

        var lblLoginTitle = new Label
        {
            Text = "Masuk ke Akun Anda",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 50, 80),
            AutoSize = true,
            Location = new Point(30, 25)
        };

        _lblNIM = CreateLabel("NIM / NIP Institusi", new Point(30, 68));

        _txtNIM = new TextBox
        {
            Location = new Point(30, 88),
            Size = new Size(330, 30),
            Font = new Font("Segoe UI", 10f),
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Masukkan NIM/NIP..."
        };

        _lblPassword = CreateLabel("Password", new Point(30, 130));

        _txtPassword = new TextBox
        {
            Location = new Point(30, 150),
            Size = new Size(330, 30),
            Font = new Font("Segoe UI", 10f),
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = true,
            PlaceholderText = "Masukkan password..."
        };

        _chkShowPassword = new CheckBox
        {
            Text = "Tampilkan Password",
            Location = new Point(30, 188),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 120, 150)
        };
        _chkShowPassword.CheckedChanged += (_, _) =>
            _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;

        _btnLogin = new Button
        {
            Text = "MASUK",
            Location = new Point(30, 222),
            Size = new Size(330, 42),
            BackColor = Color.FromArgb(0, 82, 165),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += BtnLogin_Click;

        _progressBar = new ProgressBar
        {
            Location = new Point(30, 270),
            Size = new Size(330, 6),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Visible = false
        };

        _lblStatus = new Label
        {
            Location = new Point(30, 282),
            Size = new Size(330, 45),
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        _loginPanel.Controls.AddRange(new Control[]
        {
            lblLoginTitle, _lblNIM, _txtNIM, _lblPassword, _txtPassword,
            _chkShowPassword, _btnLogin, _progressBar, _lblStatus
        });

        // State info label (Automata visualization)
        _lblStateInfo = new Label
        {
            Location = new Point(50, 545),
            Size = new Size(390, 30),
            Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(120, 140, 160),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = $"State: {_authService.CurrentState} | Institusi: Telkom University"
        };

        // Demo credentials hint
        var lblDemo = new Label
        {
            Location = new Point(50, 575),
            Size = new Size(390, 20),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
            ForeColor = Color.FromArgb(150, 160, 180),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Demo - Dosen: 198501012010011001 / dosen123 | Mhs: 1302220142 / mhs123"
        };

        Controls.AddRange(new Control[]
        {
            _headerPanel, _loginPanel, _lblStateInfo, lblDemo
        });

        // Enter key triggers login
        _txtPassword.KeyPress += (_, e) =>
        {
            if (e.KeyChar == (char)Keys.Enter) BtnLogin_Click(null!, null!);
        };
        _txtNIM.KeyPress += (_, e) =>
        {
            if (e.KeyChar == (char)Keys.Enter) _txtPassword.Focus();
        };
    }

    private async void BtnLogin_Click(object sender, EventArgs e)
    {
        string nim = _txtNIM.Text.Trim();
        string password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(nim) || string.IsNullOrWhiteSpace(password))
        {
            ShowStatus("⚠ NIM/NIP dan password harus diisi", Color.OrangeRed);
            return;
        }

        SetLoading(true);
        ShowStatus("Mengautentikasi...", Color.FromArgb(0, 82, 165));

        try
        {
            var user = await _authService.LoginAsync(nim, password);
            _lblStateInfo.Text = $"State: {_authService.CurrentState}";

            if (user != null)
            {
                ShowStatus($"✓ Login berhasil! Selamat datang, {user.Nama}", Color.Green);
                LoggedInUser = user;
                await Task.Delay(800);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                int remaining = _authService.RemainingAttempts;
                ShowStatus(
                    $"✗ NIM/NIP atau password salah. Sisa percobaan: {remaining}",
                    Color.OrangeRed);
            }
        }
        catch (InvalidOperationException ex)
        {
            ShowStatus($"🔒 {ex.Message}", Color.DarkRed);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", Color.Red);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        _btnLogin.Enabled = !loading;
        _progressBar.Visible = loading;
        _txtNIM.Enabled = !loading;
        _txtPassword.Enabled = !loading;
    }

    private void ShowStatus(string message, Color color)
    {
        _lblStatus.Text = message;
        _lblStatus.ForeColor = color;
        _lblStatus.Visible = true;
    }

    private static Label CreateLabel(string text, Point location) => new()
    {
        Text = text,
        Location = location,
        AutoSize = true,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        ForeColor = Color.FromArgb(60, 80, 110)
    };

    private static void AddShadow(Control ctrl)
    {
        // Simple border effect for "shadow"
        ctrl.Paint += (_, e) =>
        {
            ControlPaint.DrawBorder(e.Graphics, ctrl.ClientRectangle,
                Color.FromArgb(220, 225, 235), ButtonBorderStyle.Solid);
        };
    }
}