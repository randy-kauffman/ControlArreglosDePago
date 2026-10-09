using System;
using System.Collections.Generic;

namespace ArreglosPago.Formato
{
    public sealed class CuotaDato
    {
        public int Numero { get; }
        public DateTime Fecha { get; }
        public decimal Monto { get; }
        public DateTime? FechaPago { get; }
        public decimal? MontoPagado { get; }

        public CuotaDato(int numero, DateTime fecha, decimal monto, DateTime? fechaPago = null, decimal? montoPagado = null)
        {
            Numero = numero;
            Fecha = fecha;
            Monto = monto;
            FechaPago = fechaPago;
            MontoPagado = montoPagado;
        }
    }

    /// <summary>
    /// Datos ya resueltos por el plugin (instantánea del cliente si está congelado, etc.).
    /// Valores admitidos: string, int, long, decimal, DateTime o null (limpia la celda). double/float se rechazan a propósito.
    /// </summary>
    public sealed class DatosArreglo
    {
        public IReadOnlyDictionary<string, object> Valores { get; }
        public int Plazo { get; }
        public IReadOnlyList<CuotaDato> Cuotas { get; }

        public DatosArreglo(IReadOnlyDictionary<string, object> valores, int plazo, IReadOnlyList<CuotaDato> cuotas)
        {
            Valores = valores;
            Plazo = plazo;
            Cuotas = cuotas;
        }
    }
}
