using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>Una cuota tal como la imprime el bloque del formato (diseno/03 1.1, "Cuota: …").</summary>
    internal sealed class CuotaDato
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
    /// Datos ya resueltos por la Custom API (instantánea del cliente si el arreglo está congelado, etc.; diseno/03 1.1).
    /// Valores admitidos: string, int, long, decimal, DateTime o null (limpia la celda). double/float se rechazan a propósito.
    /// El generador exige una clave por cada campo simple mapeado, aunque el valor sea <c>null</c> (spike 1, hallazgo 3).
    /// </summary>
    internal sealed class DatosArreglo
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
