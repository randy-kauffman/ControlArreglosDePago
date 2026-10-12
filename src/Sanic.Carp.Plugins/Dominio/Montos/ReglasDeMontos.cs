using System;
using System.Collections.Generic;
using System.Globalization;
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
            if (!Ok) throw new InvalidOperationException("Un cálculo con error no tiene montos de cuotas: " + Error);

            var montos = new decimal[Plazo];
            for (int i = 0; i < Plazo - 1; i++) montos[i] = CuotaMensual;
            montos[Plazo - 1] = MontoUltimaCuota;
            return montos;
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
            if (plazo < 1) throw new ArgumentOutOfRangeException(nameof(plazo), plazo, "El plazo debe ser de al menos una cuota; CARP-ARR-003 lo valida antes.");

            if (cuotaMensual <= 0m) return ResultadoDeMontos.Fallo(CodigosDeError.ARR_007.Crear());
            if (montoAbonado > saldoInicial) return ResultadoDeMontos.Fallo(CodigosDeError.ARR_004.Crear());

            decimal saldoRemanente = saldoInicial - montoAbonado;
            decimal cuotasSinLaUltima = cuotaMensual * (plazo - 1);

            if (cuotasSinLaUltima >= saldoRemanente) return ResultadoDeMontos.Fallo(ErrorDeCuotaDeMas(saldoRemanente, cuotaMensual));

            if (cuotaMensual * plazo > saldoRemanente)
            {
                return ResultadoDeMontos.Exito(saldoRemanente, saldoRemanente, saldoRemanente - cuotasSinLaUltima, cuotaMensual, plazo);
            }

            // Alivio temporal: el arreglo no cubre el saldo y se paga lo que alcanzan las cuotas (D-30).
            return ResultadoDeMontos.Exito(saldoRemanente, cuotaMensual * plazo, cuotaMensual, cuotaMensual, plazo);
        }

        // N = ceil(remanente / cuota). Se arma con el resto y no con el cociente: un cociente decimal se redondea a 28 cifras y podría caer
        // justo en un entero cuando la división no es exacta. (remanente − resto) sí es múltiplo exacto de la cuota.
        private static ErrorDeNegocio ErrorDeCuotaDeMas(decimal saldoRemanente, decimal cuotaMensual)
        {
            if (saldoRemanente == 0m) return new ErrorDeNegocio(CodigosDeError.ARR_005.Codigo, CodigosDeError.TextoSinSaldoDeArr005);

            decimal resto = saldoRemanente % cuotaMensual;
            decimal n = (saldoRemanente - resto) / cuotaMensual;
            if (resto != 0m) n += 1m;

            string cantidad = n.ToString("F0", CultureInfo.InvariantCulture);
            ErrorDeNegocio error = CodigosDeError.ARR_005.Crear(cantidad);
            if (cantidad != "1") return error;

            // PA-MON-13: con una sola cuota el texto va en singular ("con 1 cuota").
            return new ErrorDeNegocio(error.Codigo, error.Mensaje.Replace(" 1 cuotas", " 1 cuota"));
        }
    }
}
