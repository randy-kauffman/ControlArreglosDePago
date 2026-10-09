using System;
using System.Collections.Generic;
using System.Linq;
using ArreglosPago.Formato;

namespace Spk.Plugins
{
    /// <summary>
    /// Mapeo y datos ficticios FIJOS del spike 1b. Se comparten (por inclusion de codigo) con la herramienta local
    /// que genera el esperado, para que el sandbox y el equipo local usen exactamente los mismos datos.
    /// Mismos datos que tools/Medicion del spike 1 (60 cuotas con 12 pagos; 6 cuotas sin pagos).
    /// </summary>
    public static class DatosFicticios
    {
        public const string Formato = " FORMATO ARP ELECTRONICO";   // el nombre de la hoja empieza con un espacio (hallazgo 1 del spike 1)
        public const string Control = "CONTROL DE PAGOS";

        public static List<MapeoCelda> Mapa()
        {
            Func<string, string, string, MapeoCelda> s = (h, c, f) => new MapeoCelda(h, c, TipoMapeo.Simple, f);
            Func<string, string, MapeoCelda> b = (c, f) => new MapeoCelda(Control, c, TipoMapeo.Bloque, f, 60);
            return new List<MapeoCelda>
            {
                s(Formato, "B7", "fechaFormalizacion"), s(Formato, "F7", "oficial"), s(Formato, "B8", "clienteNombre"), s(Formato, "F8", "clienteCedula"),
                s(Formato, "B9", "clienteCorreo"), s(Formato, "F9", "clienteCelular"), s(Formato, "B12", "numeroProducto"), s(Formato, "D12", "tipo"),
                s(Formato, "F12", "numeroArreglo"), s(Formato, "B13", "saldoInicial"), s(Formato, "D13", "montoAbonado"), s(Formato, "F13", "saldoRemanente"),
                s(Formato, "B14", "plazo"), s(Formato, "D14", "cuota"), s(Formato, "F14", "totalPagar"), s(Formato, "B15", "fechaInicio"),
                s(Formato, "D15", "fechaFin"), s(Formato, "F15", "cuentaDebitar"), s(Formato, "A18", "comentarios"),
                s(Control, "B4", "clienteNombre"), s(Control, "E4", "fechaFormalizacion"),
                b("A7", Campos.CuotaNumero), b("B7", Campos.CuotaFecha), b("C7", Campos.CuotaMonto), b("D7", Campos.CuotaFechaPago), b("E7", Campos.CuotaMontoPagado),
                new MapeoCelda(Control, "C67", TipoMapeo.Total, Campos.TotalMontoCuotas),
                new MapeoCelda(Control, "E67", TipoMapeo.Total, Campos.TotalMontoPagado),
            };
        }

        /// <summary>Con plazo de 30 o mas, las primeras 12 cuotas traen pago registrado (la 12 parcial). Con menos, ninguna.</summary>
        public static DatosArreglo Construir(int plazo)
        {
            bool conPagos = plazo >= 30;
            var inicio = new DateTime(2026, 11, 8);
            decimal cuota = plazo == 60 ? 2150.35m : 4200.10m;
            var cuotas = new List<CuotaDato>();
            for (int i = 1; i <= plazo; i++)
            {
                var fecha = inicio.AddMonths(i - 1);
                bool pagada = conPagos && i <= 12;
                cuotas.Add(new CuotaDato(i, fecha, cuota, pagada ? fecha.AddDays(1) : (DateTime?)null,
                    pagada ? (i == 12 ? cuota / 2 : cuota) : (decimal?)null));
            }
            var total = cuotas.Sum(c => c.Monto);
            var valores = new Dictionary<string, object>
            {
                { "fechaFormalizacion", new DateTime(2026, 10, 8) }, { "oficial", "Oficial de Prueba" },
                { "clienteNombre", "Ana Lucia Martinez Ruiz (ficticio)" }, { "clienteCedula", "000-000000-0000X" },
                { "clienteCorreo", "cliente.ficticio@ejemplo.com" }, { "clienteCelular", "0000-0000" },
                { "numeroProducto", "0000-0000-0000-0000" }, { "tipo", "Temporal" }, { "numeroArreglo", 3 },
                { "saldoInicial", 125000.75m }, { "montoAbonado", 5000m }, { "saldoRemanente", 120000.75m },
                { "plazo", plazo }, { "cuota", cuota }, { "totalPagar", total },
                { "fechaInicio", inicio }, { "fechaFin", inicio.AddMonths(plazo - 1) }, { "cuentaDebitar", "0000000000000000" },
                { "comentarios", "Datos ficticios del spike. Plazo de " + plazo + " cuotas." },
            };
            return new DatosArreglo(valores, plazo, cuotas);
        }
    }
}
