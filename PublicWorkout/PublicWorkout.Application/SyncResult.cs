using PublicWorkout.Application.Dtos;

namespace PublicWorkout.Application;

public class SyncResult
{
    public PublicWorkoutDto Workout { get; }
    public bool IsNew { get; }

    public SyncResult(PublicWorkoutDto workout, bool isNew)
    {
        Workout = workout;
        IsNew = isNew;
    }
}
