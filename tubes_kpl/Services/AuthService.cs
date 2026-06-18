using Tubes_KPL.Contracts;
using Tubes_KPL.Infrastructure;
using Tubes_KPL.Models;
using System.Data;

namespace Tubes_KPL.Services;

/// <summary>
/// State machine untuk proses autentikasi.
/// States: Idle → Authenticating → Authenticated / Failed → Locked
/// </summary>
public class AuthService : IAuthService
{
    // === AUTOMATA: States ===
    public enum AuthState
    {
        Idle,           // Belum login
        Authenticating, // Sedang proses login
        Authenticated,  // Login berhasil
        Failed,         // Login gagal
        Locked          // Akun terkunci (terlalu banyak percobaan)
    }

    // === AUTOMATA: Events/Transitions ===
    private enum AuthEvent
    {
        Submit,
        Success,
        Failure,
        Logout,
        MaxAttemptsReached,
        UnlockTimeout
    }

    private AuthState _currentState = AuthState.Idle;
    private int _failedAttempts = 0;
    private DateTime? _lockUntil = null;
    private const int MaxFailedAttempts = 5;
    private const int LockDurationMinutes = 15;

    // === AUTOMATA: Transition Table ===
    private readonly Dictionary<(AuthState, AuthEvent), AuthState> _transitions = new()
    {
        { (AuthState.Idle,           AuthEvent.Submit),             AuthState.Authenticating },
        { (AuthState.Authenticating, AuthEvent.Success),            AuthState.Authenticated  },
        { (AuthState.Authenticating, AuthEvent.Failure),            AuthState.Failed          },
        { (AuthState.Authenticating, AuthEvent.MaxAttemptsReached), AuthState.Locked          },
        { (AuthState.Failed,         AuthEvent.Submit),             AuthState.Authenticating  },
        { (AuthState.Failed,         AuthEvent.MaxAttemptsReached), AuthState.Locked          },
        { (AuthState.Authenticated,  AuthEvent.Logout),             AuthState.Idle            },
        { (AuthState.Locked,         AuthEvent.UnlockTimeout),      AuthState.Idle            },
    };

    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => _currentState == AuthState.Authenticated;
    public AuthState CurrentState => _currentState;

    // === AUTOMATA: Transition function ===
    private bool Transition(AuthEvent evt)
    {
        var key = (_currentState, evt);
        if (_transitions.TryGetValue(key, out var nextState))
        {
            OnExitState(_currentState);
            _currentState = nextState;
            OnEnterState(_currentState);
            return true;
        }
        return false; // Invalid transition
    }

    private void OnExitState(AuthState state) { /* cleanup per state */ }

    private void OnEnterState(AuthState state)
    {
        if (state == AuthState.Locked)
        {
            _lockUntil = DateTime.Now.AddMinutes(LockDurationMinutes);
        }
        else if (state == AuthState.Authenticated)
        {
            _failedAttempts = 0;
        }
        else if (state == AuthState.Idle)
        {
            CurrentUser = null;
            _failedAttempts = 0;
            _lockUntil = null;
        }
    }

    public async Task<User?> LoginAsync(string nimNip, string password)
    {
        // Check if locked
        if (_currentState == AuthState.Locked)
        {
            if (_lockUntil.HasValue && DateTime.Now >= _lockUntil)
                Transition(AuthEvent.UnlockTimeout);
            else
                throw new InvalidOperationException(
                    $"Akun terkunci. Coba lagi setelah {_lockUntil:HH:mm}.");
        }

        Transition(AuthEvent.Submit);

        try
        {
            var user = await Task.Run(() => FindUser(nimNip));

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _failedAttempts++;
                if (_failedAttempts >= MaxFailedAttempts)
                    Transition(AuthEvent.MaxAttemptsReached);
                else
                    Transition(AuthEvent.Failure);

                return null;
            }

            if (!user.IsActive)
                throw new InvalidOperationException("Akun tidak aktif. Hubungi administrator.");

            CurrentUser = user;
            Transition(AuthEvent.Success);
            return user;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            Transition(AuthEvent.Failure);
            return null;
        }
    }

    private User? FindUser(string nimNip)
    {
        using var conn = DatabaseContext.Instance.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE NIM_NIP = @nim AND IsActive = 1";
        var param = cmd.CreateParameter();
        param.ParameterName = "@nim";
        param.Value = nimNip;
        cmd.Parameters.Add(param);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        int role = reader.GetInt32(reader.GetOrdinal("Role"));
        if (role == (int)UserRole.Dosen)
        {
            return new Dosen
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                NIM_NIP = reader.GetString(reader.GetOrdinal("NIM_NIP")),
                Nama = reader.GetString(reader.GetOrdinal("Nama")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                Role = UserRole.Dosen,
                IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
                Jabatan = reader.IsDBNull(reader.GetOrdinal("Jabatan")) ? "" :
                          reader.GetString(reader.GetOrdinal("Jabatan"))
            };
        }
        else
        {
            return new Mahasiswa
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                NIM_NIP = reader.GetString(reader.GetOrdinal("NIM_NIP")),
                Nama = reader.GetString(reader.GetOrdinal("Nama")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                Role = UserRole.Mahasiswa,
                IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
                Kelas = reader.IsDBNull(reader.GetOrdinal("Kelas")) ? "" :
                        reader.GetString(reader.GetOrdinal("Kelas"))
            };
        }
    }

    public async Task<bool> LogoutAsync(int userId)
    {
        if (_currentState != AuthState.Authenticated) return false;
        Transition(AuthEvent.Logout);
        return await Task.FromResult(true);
    }

    public int FailedAttempts => _failedAttempts;
    public int RemainingAttempts => Math.Max(0, MaxFailedAttempts - _failedAttempts);
}
