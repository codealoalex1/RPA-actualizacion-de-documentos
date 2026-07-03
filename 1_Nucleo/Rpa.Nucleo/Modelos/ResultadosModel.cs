using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rpa.Nucleo.Modelos;

public class ResultadosModel<T>
{
    public string? SitioWeb { get; set; }
    public DateTime FechaVerificacion { get; set; } = ObtenerHoraActual();
    public List<T> ContenidoIdentificado { get; set; } = new();
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Procesado { get; set; } = false;

    private static DateTime ObtenerHoraActual()
    {
        DateTime horaUtc = DateTime.UtcNow;
        TimeZoneInfo zonaBolivia = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time");
        DateTime horaBolivia = TimeZoneInfo.ConvertTimeFromUtc(horaUtc, zonaBolivia);
        return horaBolivia;
    }
    public long convertirHora(string fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha))
        {
            return DateTime.UtcNow.Ticks; // Retorna la fecha actual si la cadena viene vacía
        }

        // 1. Limpiamos espacios en blanco accidentales al inicio o al final (como el de la Gaceta)
        string fechaLimpia = fecha.Trim();

        // 2. Intentamos parsear de forma inteligente usando la cultura de Bolivia (entiende meses en español y formatos ISO)
        if (DateTime.TryParse(fechaLimpia, new CultureInfo("es-BO"), DateTimeStyles.None, out DateTime fechaParseada))
        {
            return fechaParseada.Ticks;
        }

        // 3. Plan de contingencia: Si falla, intentamos con la cultura invariante por si acaso
        if (DateTime.TryParse(fechaLimpia, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fechaInvariante))
        {
            return fechaInvariante.Ticks;
        }

        // 4. Si el string es un formato totalmente exótico que sigue fallando, registramos el error y evitamos que caiga el contenedor
        Console.WriteLine($"[ERROR FORMATO FECHA] No se pudo reconocer la fecha: '{fecha}'");
        return DateTime.UtcNow.Ticks;
    }
}