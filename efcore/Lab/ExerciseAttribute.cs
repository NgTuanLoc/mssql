using System.Runtime.CompilerServices;

namespace EfCoreLessons.Lab;

// A check for one exercise. While the exercise still throws NotImplementedException
// the check is reported as skipped (not attempted) rather than failed.
public sealed class ExerciseAttribute : FactAttribute
{
    public ExerciseAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        SkipExceptions = [typeof(NotImplementedException)];
    }
}
