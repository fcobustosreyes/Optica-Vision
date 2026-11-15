namespace OpticaVision.Web.Models;

public sealed record DashboardMetric(string Label, string Value, string Detail, string Icon, string Accent);

public sealed record AppointmentRow(string Time, string Client, string Phone, string Status);

public sealed record LowStockRow(string Product, string Units);

public sealed class DashboardViewModel
{
    public required string Today { get; init; }
    public required IReadOnlyList<DashboardMetric> Metrics { get; init; }
    public required IReadOnlyList<AppointmentRow> Appointments { get; init; }
    public required IReadOnlyList<LowStockRow> LowStock { get; init; }
}

public sealed class ModulePageViewModel
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }

    public required IReadOnlyList<string> Columns { get; init; }
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
}

