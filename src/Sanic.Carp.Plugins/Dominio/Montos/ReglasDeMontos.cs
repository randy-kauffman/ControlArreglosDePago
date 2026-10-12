using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio.Montos
{
    /// <summary>Resultado de <see cref="ReglasDeMontos.Calcular"/>: los tres montos, o el error de negocio (diseno/03 2.0.3).</summary>
    public sealed class ResultadoDeMontos
    {
        public bool Ok { get; }

        /// <summary><c>null</c> si <see cref="Ok"/>.</summary>
        public ErrorDeNegocio Error { get; }

        public decimal SaldoRemanente { get; }
        public decimal TotalPagar { get; }
        public decimal MontoUltimaCuota { get; }

        private ResultadoDeMontos(bool ok, ErrorDeNegocio error, decimal saldoRemanente, decimal totalPagar, decimal montoUltimaCuota,
            decimal cuotaMensual, int plazo)
        {
            Ok = ok;
            Error = error;
            SaldoRemanente = saldoRemanente;
            TotalPagar = totalPagar;
            MontoUltimaCuota = montoUltimaCuota;
            CuotaMensual = cuotaMensual;
            Plazo = plazo;
        }

        internal static ResultadoDeMontos Exito(decimal saldoRemanente, decimal totalPagar, decimal montoUltimaCuota, decimal cuotaMensual, int plazo)
        {
            return new ResultadoDeMontos(true, null, saldoRemanente, totalPagar, montoUltimaCuota, cuotaMensual, plazo);
        }

        internal static ResultadoDeMontos Fallo(ErrorDeNegocio error)
        {
            return new ResultadoDeMontos(false, error, 0m, 0m, 0m, 0m, 0);
        }

        // Datos de entrada que necesita MontosDeCuotas(); quedan internos para no ampliar la firma pública de 4.4.
        internal decimal CuotaMensual { get; }
        internal int Plazo { get; }

        /// <summary>
        /// Monto de cada cuota, de la 1 a la última: <c>plazo − 1</c> cuotas de <c>cuotaMensual</c> y la última igual a
        /// <see cref="MontoUltimaCuota"/>; la suma es <see cref="TotalPagar"/> (PA-MON-14). Si no <see cref="Ok"/>, lanza
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
        public IReadOnlyList<decimal> MontosDeCuotas()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>Montos del arreglo (diseno/03 2.0.3; D-30, dicc. DD-12, R-04). Todo en <see cref="decimal"/>, sin redondeos propios.</summary>
    public static class ReglasDeMontos
    {
        /// <summary>
        /// Orden de validación (2.0.3): (1) cuota mensual menor o igual a cero ⇒ <c>CARP-ARR-007</c>; (2) monto abonado mayor que el saldo
        /// inicial ⇒ <c>CARP-ARR-004</c>; (3) saldo remanente = inicial − abonado; (4) si <c>cuota × (plazo − 1) &gt;= remanente</c>
        /// ⇒ <c>CARP-ARR-005</c> con <c>N = ceil(remanente / cuota)</c> (o el texto sin saldo si el remanente es cero); (5) si
        /// <c>cuota × plazo &gt; remanente</c>, total = remanente y última = remanente − cuota × (plazo − 1); (6) si no (alivio temporal),
        /// total = cuota × plazo y última = cuota. Un plazo menor que 1 lanza <see cref="ArgumentOutOfRangeException"/> (decisión del
        /// arquitecto, por analogía con PA-FEC-06: el plugin ya validó el plazo con <c>CARP-ARR-003</c>).
        /// </summary>
        public static ResultadoDeMontos Calcular(decimal saldoInicial, decimal montoAbonado, decimal cuotaMensual, int plazo)
        {
            throw new NotImplementedException();
        }
    }
}
