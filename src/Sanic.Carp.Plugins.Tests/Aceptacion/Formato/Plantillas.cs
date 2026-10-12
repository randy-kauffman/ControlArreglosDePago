using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Sanic.Carp.Plugins.Formato;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>Plantillas reales y mapeo/datos ficticios de las pruebas. Portada de <c>Plantillas</c> del spike 1 (diseno/03 4.3).</summary>
    internal static class Plantillas
    {
        // Ojo: la hoja real se llama " FORMATO ARP ELECTRONICO", con un espacio al principio.
        public const string HojaFormato = " FORMATO ARP ELECTRONICO";
        public const string HojaControl = "CONTROL DE PAGOS";

        /// <summary>Copia exacta de <c>spikes/plantilla/F-NIC-0000052_v07_borrador.xlsx</c> (diseno/03 4.3).</summary>
        public static byte[] V07 { get { return Leer("F-NIC-0000052_v07.xlsx"); } }

        /// <summary>
        /// Copia exacta de <c>spikes/plantilla/base/F-NIC-0000052_v06_copia.xlsx</c>: la v06 real (48 filas, TODAY(), calcChain, números
        /// preimpresos) que las pruebas del spike usan para los casos de capacidad 48 y de fórmulas. El diseño nombra solo la v07
        /// (4.3); la v06 se agrega porque varias pruebas del spike la necesitan.
        /// </summary>
        public static byte[] V06 { get { return Leer("F-NIC-0000052_v06.xlsx"); } }

        private static byte[] Leer(string nombre)
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", nombre));
        }

        /// <summary>SHA-256 hexadecimal en minúscula calculado con la BCL: referencia independiente del generador.</summary>
        public static string Sha256(byte[] contenido)
        {
            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder();
                foreach (var b in sha.ComputeHash(contenido)) sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>Mapeo de la v07 propuesto en el mockup (MAP_V07), con el nombre real de la hoja.</summary>
        public static List<MapeoCelda> MapaV07(bool conTotales = true, int capacidad = 60, int ultimaFilaTotales = 67)
        {
            var m = new List<MapeoCelda>
            {
                S(HojaFormato, "B7", "fechaFormalizacion"), S(HojaFormato, "F7", "oficial"),
                S(HojaFormato, "B8", "clienteNombre"), S(HojaFormato, "F8", "clienteCedula"),
                S(HojaFormato, "B9", "clienteCorreo"), S(HojaFormato, "F9", "clienteCelular"),
                S(HojaFormato, "B12", "numeroProducto"), S(HojaFormato, "D12", "tipo"), S(HojaFormato, "F12", "numeroArreglo"),
                S(HojaFormato, "B13", "saldoInicial"), S(HojaFormato, "D13", "montoAbonado"), S(HojaFormato, "F13", "saldoRemanente"),
                S(HojaFormato, "B14", "plazo"), S(HojaFormato, "D14", "cuota"), S(HojaFormato, "F14", "totalPagar"),
                S(HojaFormato, "B15", "fechaInicio"), S(HojaFormato, "D15", "fechaFin"), S(HojaFormato, "F15", "cuentaDebitar"),
                S(HojaFormato, "A18", "comentarios"),
                S(HojaControl, "B4", "clienteNombre"), S(HojaControl, "E4", "fechaFormalizacion"),
                B(HojaControl, "A7", Campos.CuotaNumero, capacidad), B(HojaControl, "B7", Campos.CuotaFecha, capacidad),
                B(HojaControl, "C7", Campos.CuotaMonto, capacidad), B(HojaControl, "D7", Campos.CuotaFechaPago, capacidad),
                B(HojaControl, "E7", Campos.CuotaMontoPagado, capacidad),
            };
            if (conTotales)
            {
                m.Add(new MapeoCelda(HojaControl, "C" + ultimaFilaTotales, TipoMapeo.Total, Campos.TotalMontoCuotas));
                m.Add(new MapeoCelda(HojaControl, "E" + ultimaFilaTotales, TipoMapeo.Total, Campos.TotalMontoPagado));
            }
            return m;
        }

        /// <summary>Mapeo equivalente para la v06 real (48 filas, totales en la fila 55).</summary>
        public static List<MapeoCelda> MapaV06(bool conTotales = true)
        {
            return MapaV07(conTotales, 48, 55);
        }

        public static MapeoCelda S(string hoja, string celda, string campo)
        {
            return new MapeoCelda(hoja, celda, TipoMapeo.Simple, campo);
        }

        public static MapeoCelda B(string hoja, string celda, string campo, int filas)
        {
            return new MapeoCelda(hoja, celda, TipoMapeo.Bloque, campo, filas);
        }

        public static Dictionary<string, object> ValoresEjemplo()
        {
            return new Dictionary<string, object>
            {
                { "fechaFormalizacion", new DateTime(2026, 10, 8) },
                { "oficial", "Mariela Gutiérrez" },
                { "clienteNombre", "Ana Lucía Martínez Ruiz" },
                { "clienteCedula", "001-120488-0023K" },
                { "clienteCorreo", "ana.martinez@ejemplo.com" },
                { "clienteCelular", "8812-4410" },
                { "numeroProducto", "4500-0000-1234-5678" },
                { "tipo", "Temporal" },
                { "numeroArreglo", 3 },
                { "saldoInicial", 125000.75m },
                { "montoAbonado", 5000m },
                { "saldoRemanente", 120000.75m },
                { "plazo", 60 },
                { "cuota", 2000.01m },
                { "totalPagar", 120000.60m },
                { "fechaInicio", new DateTime(2026, 11, 8) },
                { "fechaFin", new DateTime(2031, 10, 8) },
                { "cuentaDebitar", "1002003004005006" },
                { "comentarios", "Arreglo de ejemplo con datos ficticios." },
                // AJUSTE AL DISEÑO (no estaba en el spike): la clave moneda viaja en los datos aunque la v07 no tenga celda para
                // ella (diseno/03 1.1 y OA-7). Ningún valor esperado de las pruebas portadas cambia: el generador ignora las claves
                // no mapeadas, y MonedaEnElGeneradorAceptacion lo demuestra.
                { "moneda", "Córdobas" },
            };
        }

        public static DatosArreglo Datos(int plazo, Action<Dictionary<string, object>> ajustar = null, Func<int, CuotaDato> cuota = null)
        {
            var valores = ValoresEjemplo();
            valores["plazo"] = plazo;
            if (ajustar != null) ajustar(valores);
            var cuotas = new List<CuotaDato>();
            for (int i = 1; i <= plazo; i++)
            {
                cuotas.Add(cuota != null ? cuota(i) : new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 2000.01m));
            }
            return new DatosArreglo(valores, plazo, cuotas);
        }
    }
}
