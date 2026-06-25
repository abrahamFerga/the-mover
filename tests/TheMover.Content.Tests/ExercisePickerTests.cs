// TheMover.Content.Tests — ExercisePicker rotation and no-back-to-back guarantee
using TheMover.Content;

namespace TheMover.Content.Tests;

public sealed class ExercisePickerTests
{
    [Fact]
    public void Library_HasAtLeastTenExercises()
    {
        Assert.True(ExerciseLibrary.All.Count >= 10, $"Expected >= 10 exercises, got {ExerciseLibrary.All.Count}");
    }

    [Fact]
    public void AllExercises_HaveNonEmptyFields()
    {
        foreach (var ex in ExerciseLibrary.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(ex.Id), $"Exercise Id is empty");
            Assert.False(string.IsNullOrWhiteSpace(ex.Title), $"Exercise {ex.Id} Title is empty");
            Assert.False(string.IsNullOrWhiteSpace(ex.Instruction), $"Exercise {ex.Id} Instruction is empty");
        }
    }

    [Fact]
    public void AllExercises_HaveUniqueIds()
    {
        var ids = ExerciseLibrary.All.Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Pick_NeverReturnsBackToBack()
    {
        var picker = new ExercisePicker();
        string? lastId = null;
        for (var i = 0; i < 100; i++)
        {
            var ex = picker.Pick();
            if (lastId is not null)
                Assert.NotEqual(lastId, ex.Id);
            lastId = ex.Id;
        }
    }

    [Fact]
    public void Pick_ReturnsDifferentExercisesOverTime()
    {
        var picker = new ExercisePicker();
        var seen = new HashSet<string>();
        for (var i = 0; i < 50; i++)
            seen.Add(picker.Pick().Id);
        Assert.True(seen.Count >= 5, $"Expected variety after 50 picks, got only {seen.Count} distinct exercises");
    }
}
