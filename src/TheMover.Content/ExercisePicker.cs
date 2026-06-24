// TheMover.Content — picks exercises in rotation, never the same back-to-back
namespace TheMover.Content;

public sealed class ExercisePicker
{
    private readonly Random _rng = new();
    private string? _lastId;

    public Exercise Pick()
    {
        var pool = ExerciseLibrary.All;
        var candidates = pool.Count > 1
            ? pool.Where(e => e.Id != _lastId).ToList()
            : (IList<Exercise>)pool;

        var chosen = candidates[_rng.Next(candidates.Count)];
        _lastId = chosen.Id;
        return chosen;
    }
}
