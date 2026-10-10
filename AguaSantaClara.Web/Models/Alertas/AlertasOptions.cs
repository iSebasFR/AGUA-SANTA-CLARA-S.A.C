namespace AguaSantaClara.Web.Models.Alertas;

public sealed class AlertasOptions
{
    public const string SectionName = "Alertas";

    public int HoraEjecucion { get; set; } = 22;
    public int MinutoEjecucion { get; set; }
    public int UtcOffsetHoras { get; set; } = -5;
    public int FrecuenciaDias { get; set; } = 1;
    public int IntervaloRevisionSegundos { get; set; } = 30;
}
