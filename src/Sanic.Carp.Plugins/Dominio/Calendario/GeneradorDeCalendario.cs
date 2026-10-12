using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Calendario
{
    /// <summary>Una cuota del calendario: número (1..plazo), vencimiento (solo fecha) y monto.</summary>
    public sealed class CuotaDeCalendario
    {
        public int Numero { get; }
        public DateTime FechaVencimiento { get; }
        public decimal Monto { get; }

        public CuotaDeCalendario(int numero, DateTime fechaVencimiento, decimal monto)
        {
            Numero = numero;
            FechaVencimiento = fechaVencimiento;
            Monto = monto;
        }
    }

    /// <summary>
    /// Calendario de cuotas (diseno/03 2.0.4, OA-5 opción A). Toda la regla de fechas vive en este único lugar (def. ítem 8):
    /// la cuota <c>n</c> vence en <c>fechaInicio.Date.AddMonths(n − 1)</c>, siempre contado desde la fecha de inicio y nunca encadenando
    /// mes a mes; si el mes no tiene ese día, el último del mes.
    /// </summary>
    public static class GeneradorDeCalendario
    {
        /// <summary>Plazo máximo de cualquier versión de plantilla (RF-06, D-16).</summary>
        public const int PlazoMaximo = 60;

        /// <summary>
        /// <paramref name="plazo"/> cuotas: las primeras <c>plazo − 1</c> de <paramref name="cuotaMensual"/> y la última de
        /// <paramref name="montoUltimaCuota"/>. Solo se usa la fecha de <paramref name="fechaInicio"/> (se descarta la hora).
        /// Un plazo fuera de 1..<see cref="PlazoMaximo"/> lanza <see cref="ArgumentOutOfRangeException"/> (el plugin ya rechazó antes
        /// con <c>CARP-ARR-003</c>).
        /// </summary>
        public static IReadOnlyList<CuotaDeCalendario> Generar(DateTime fechaInicio, int plazo, decimal cuotaMensual, decimal montoUltimaCuota)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Vencimiento de la última cuota (OA-5 opción i: el plugin calcula <c>sanic_fechafin</c> y sobrescribe lo que venga). Completa el
        /// <c>FechaFin(...)</c> que 4.4 deja con puntos suspensivos: misma regla de fechas y mismo rango de plazo que
        /// <see cref="Generar"/>, solo fecha.
        /// </summary>
        public static DateTime FechaFin(DateTime fechaInicio, int plazo)
        {
            throw new NotImplementedException();
        }
    }
}
