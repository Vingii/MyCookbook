namespace MyCookbook.Api.Dto;

public class StepDto
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public int Order { get; set; }
    public int? DurationSeconds { get; set; }
    public string StepType { get; set; } = "Active";
}
