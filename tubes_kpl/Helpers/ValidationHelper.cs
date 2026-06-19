// Helpers/ValidationHelper.cs
// Validasi Nilai
// Teknik: Code Reuse/Library - Laksamana Dwi Daffa - Annisa Azzahra Putri - Nasywa Azalia Andrean
//         (reusable validation library lintas fitur)

using System;
using System.Collections.Generic;
using System.Text;

namespace Tubes_KPL.Helpers;

public static class ValidationHelper
{
    public static ValidationResult ValidateAll(params Func<ValidationResult>[] validations)
    {
        foreach (var v in validations)
        {
            var result = v();
            if (!result.IsValid) return result;
        }
        return ValidationResult.Ok();
    }

    public static ValidationResult ValidateBobot(double bobot)
    {
        return (bobot >= 0.0 && bobot <= 1.0)
            ? ValidationResult.Ok()
            : ValidationResult.Fail("Bobot tidak valid.");
    }

    public static ValidationResult ValidateTotalBobot(IEnumerable<double> bobotKomponen)
    {
        return Math.Abs(bobotKomponen.Sum() - 1.0) < 0.0001
            ? ValidationResult.Ok()
            : ValidationResult.Fail("Total bobot tidak valid.");
    }

    public static ValidationResult ValidateNIM(string nim)
    {
        if (string.IsNullOrWhiteSpace(nim))
            return ValidationResult.Fail("NIM tidak boleh kosong");

        if (!nim.All(char.IsDigit))
            return ValidationResult.Fail("NIM hanya boleh berisi angka");

        return ValidationResult.Ok();
    }

    public static ValidationResult ValidateNilai(double nilai)
    {
        if (nilai < 0 || nilai > 100)
            return ValidationResult.Fail("Nilai harus antara 0-100");

        return ValidationResult.Ok();
    }

    public static ValidationResult ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return ValidationResult.Fail("Email tidak boleh kosong");

        if (!email.Contains("@") || !email.Contains("."))
            return ValidationResult.Fail("Format email tidak valid");

        return ValidationResult.Ok();
    }

    public static ValidationResult ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return ValidationResult.Fail("Password tidak boleh kosong");

        if (password.Length < 6)
            return ValidationResult.Fail("Password minimal 6 karakter");

        return ValidationResult.Ok();
    }
}

public class ValidationResult
{
    public bool IsValid { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public static ValidationResult Ok() => new() { IsValid = true };
    public static ValidationResult Fail(string message) =>
        new() { IsValid = false, ErrorMessage = message };

    public override string ToString() => IsValid ? "Valid" : ErrorMessage;
}
