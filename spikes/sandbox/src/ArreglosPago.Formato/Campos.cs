namespace ArreglosPago.Formato
{
    /// <summary>Campos del bloque y de los totales que el generador entiende. Los campos simples son libres (clave del diccionario de valores).</summary>
    public static class Campos
    {
        public const string CuotaNumero = "cuota.numero";
        public const string CuotaFecha = "cuota.fecha";
        public const string CuotaMonto = "cuota.monto";
        public const string CuotaFechaPago = "cuota.fechaPago";
        public const string CuotaMontoPagado = "cuota.montoPagado";
        public const string TotalMontoCuotas = "total.montoCuotas";
        public const string TotalMontoPagado = "total.montoPagado";

        /// <summary>Un bloque está completo solo si trae las cinco columnas del calendario.</summary>
        public static readonly string[] BloqueCompleto =
        {
            CuotaNumero, CuotaFecha, CuotaMonto, CuotaFechaPago, CuotaMontoPagado
        };

        public static readonly string[] Totales = { TotalMontoCuotas, TotalMontoPagado };

        /// <summary>Capacidad máxima de cualquier versión de plantilla (RF-06, D-16).</summary>
        public const int CapacidadMaxima = 60;
    }
}
