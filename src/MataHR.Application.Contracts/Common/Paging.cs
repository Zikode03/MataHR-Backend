using System.ComponentModel.DataAnnotations;
namespace MataHR.Common.Contracts;
public class PageQuery
{
    [Range(0, int.MaxValue)]
    public int SkipCount { get; set; }

    [Range(1, 200)]
    public int MaxResultCount { get; set; } = 50;

    [StringLength(100)]
    public string? Search { get; set; }
}

public record Page<T>(long TotalCount, IReadOnlyList<T> Items);
