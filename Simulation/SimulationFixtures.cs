namespace Exatech_Indotel_API.Simulation;

internal record SimulationClientSeed(string Document, int Id, string? WisproState)
{
    public bool Even => (Document[^1] - '0') % 2 == 0;
    public bool HasContract => WisproState is not null;
    public bool Activated => WisproState is "enabled" or "disabled" &&
        (Even ? WisproState == "enabled" : WisproState == "disabled");
    public bool Disabled => WisproState is "enabled" or "disabled" && !Activated;
}

internal static class SimulationFixtures
{
    public static readonly SimulationClientSeed[] Clients = [
        new("00100118793", 1, "enabled"),
        new("66666666666", 2, "disabled"),
        new("77777777777", 3, "alerted"),
        new("99999999999", 4, null),
        new("22222222224", 5, "enabled"),
        new("55555555555", 6, "disabled")
    ];
}
