using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Claves de las columnas del bloque y de los totales que el generador entiende (diseno/03 4.3; portado de <c>Campos</c> del spike 1).
    /// Las claves de los valores simples son libres: el generador no mantiene una lista (la lista "opción ⇒ clave" vive en
    /// <see cref="ClavesDelGenerador"/>), así que la clave <c>moneda</c> (4.3 punto 1) no se agrega aquí.
    /// </summary>
    internal static class Campos
    {
        public const string CuotaNumero = "cuota.numero";
        public const string CuotaFecha = "cuota.fecha";
        public const string CuotaMonto = "cuota.monto";
        public const string CuotaFechaPago = "cuota.fechaPago";
        public const string CuotaMontoPagado = "cuota.montoPagado";
        public const string TotalMontoCuotas = "total.montoCuotas";
        public const string TotalMontoPagado = "total.montoPagado";

        /// <summary>Un bloque está completo solo si trae las cinco columnas del calendario.</summary>
        public static readonly IReadOnlyList<string> BloqueCompleto = new[]
        {
            CuotaNumero, CuotaFecha, CuotaMonto, CuotaFechaPago, CuotaMontoPagado
        };

        public static readonly IReadOnlyList<string> Totales = new[] { TotalMontoCuotas, TotalMontoPagado };

        /// <summary>
        /// Capacidad máxima de cualquier versión de plantilla (RF-06, D-16). Mismo valor que <c>GeneradorDeCalendario.PlazoMaximo</c> del
        /// dominio: una prueba de paridad lo compara.
        /// </summary>
        public const int CapacidadMaxima = 60;
    }
}
