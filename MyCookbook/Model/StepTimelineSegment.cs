using MyCookbook.Data.CookbookDatabase;

namespace MyCookbook.Model
{
    public class StepTimelineSegment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Step Step { get; set; }
        public TimeSpan? Duration => Step.Duration;
        public StepType ColorState => Step.StepType;
    }

    public enum StepType
    {
        Active,
        SemiPassive,
        Passive
    }
}
