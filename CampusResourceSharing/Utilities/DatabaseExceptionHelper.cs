using Microsoft.EntityFrameworkCore;

namespace CampusResourceSharing.Utilities
{
    public static class DatabaseExceptionHelper
    {
        /// <summary>
        /// Inspects a DbUpdateException to determine if it was caused by a unique constraint violation
        /// on PhoneNumber or Email/UserName.
        /// </summary>
        /// <param name="ex">The DbUpdateException thrown during SaveChanges.</param>
        /// <param name="conflictingField">Outputs "PhoneNumber", "Email", or null.</param>
        /// <returns>True if the exception is an identifiable unique constraint violation.</returns>
        public static bool IsUniqueConstraintViolation(DbUpdateException ex, out string? conflictingField)
        {
            conflictingField = null;
            var fullExceptionText = ex.ToString();

            // SQL Server error numbers 2601 (duplicate key on unique index) and 2627 (unique constraint)
            if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                if (sqlEx.Number == 2601 || sqlEx.Number == 2627)
                {
                    if (fullExceptionText.Contains("PhoneNumber", StringComparison.OrdinalIgnoreCase))
                    {
                        conflictingField = "PhoneNumber";
                        return true;
                    }
                    if (fullExceptionText.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                        fullExceptionText.Contains("UserName", StringComparison.OrdinalIgnoreCase))
                    {
                        conflictingField = "Email";
                        return true;
                    }
                    return true;
                }
                return false;
            }

            // SQLite (error code 19) or generic provider message checks
            if (fullExceptionText.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) ||
                fullExceptionText.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                fullExceptionText.Contains("2601") ||
                fullExceptionText.Contains("2627"))
            {
                if (fullExceptionText.Contains("PhoneNumber", StringComparison.OrdinalIgnoreCase))
                {
                    conflictingField = "PhoneNumber";
                    return true;
                }
                if (fullExceptionText.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                    fullExceptionText.Contains("UserName", StringComparison.OrdinalIgnoreCase))
                {
                    conflictingField = "Email";
                    return true;
                }
                return true;
            }

            return false;
        }
    }
}
