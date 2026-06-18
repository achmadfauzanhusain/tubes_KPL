// Helpers/ValidationHelper.cs
// FR-004: Validasi Nilai
// Teknik: Code Reuse/Library - Laksamana Dwi Daffa - Annisa Azzahra Putri - Nasywa Azalia Andrean

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
