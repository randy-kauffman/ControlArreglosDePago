using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ArreglosPago.Formato;

/// <summary>
/// Herramienta del spike C-01: genera los ejemplos con datos ficticios y mide tiempo y tamaño.
/// Uso: Medicion &lt;plantilla.xlsx&gt; &lt;carpeta-de-salida&gt; [iteraciones]
/// </summary>
internal static class Program
{
    private const string Formato = " FORMATO ARP ELECTRONICO";
    private const string Control = "CONTROL DE PAGOS";

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Uso: Medicion <plantilla.xlsx> <carpeta-de-salida> [iteraciones]");
            return 2;
        }
        var plantilla = File.ReadAllBytes(args[0]);
        var salida = args[1];
        int iteraciones = args.Length > 2 ? int.Parse(args[2]) : 30;
        Directory.CreateDirectory(salida);
        var huella = GeneradorFormato.CalcularHuella(plantilla);
        var mapa = Mapa();

        Console.WriteLine("Plantilla: " + args[0] + "  (" + plantilla.Length + " bytes)  SHA-256 " + huella);
        Console.WriteLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        Console.WriteLine("CPUs: " + Environment.ProcessorCount);

        foreach (var plazo in new[] { 60, 6 })
        {
            var datos = Datos(plazo, plazo == 60);
            var cronometro = Stopwatch.StartNew();
            var primera = GeneradorFormato.GenerarConInforme(plantilla, huella, mapa, datos);     // incluye JIT y carga de ensamblados
            cronometro.Stop();
            var frio = cronometro.Elapsed.TotalMilliseconds;

            var tiempos = new List<double>();
            long asignado = 0;
            for (int i = 0; i < iteraciones; i++)
            {
                long antes = GC.GetAllocatedBytesForCurrentThread();
                cronometro.Restart();
                GeneradorFormato.Generar(plantilla, huella, mapa, datos);
                cronometro.Stop();
                asignado += GC.GetAllocatedBytesForCurrentThread() - antes;
                tiempos.Add(cronometro.Elapsed.TotalMilliseconds);
            }
            tiempos.Sort();
            var nombre = Path.Combine(salida, "salida-ejemplo-" + plazo + "-cuotas.xlsx");
            File.WriteAllBytes(nombre, primera.Archivo);
            Console.WriteLine();
            Console.WriteLine("== " + plazo + " cuotas -> " + nombre);
            Console.WriteLine("  primera llamada (en frio): " + frio.ToString("F1") + " ms");
            Console.WriteLine("  en caliente (" + iteraciones + " corridas): mediana " + tiempos[tiempos.Count / 2].ToString("F1")
                + " ms, p95 " + tiempos[(int)Math.Ceiling(tiempos.Count * 0.95) - 1].ToString("F1") + " ms, max " + tiempos.Last().ToString("F1") + " ms");
            Console.WriteLine("  memoria asignada por llamada (promedio): " + (asignado / iteraciones / (1024.0 * 1024.0)).ToString("F1") + " MB; "
                + "pico de memoria del proceso hasta ahora: " + (Process.GetCurrentProcess().PeakWorkingSet64 / (1024.0 * 1024.0)).ToString("F0") + " MB");
            Console.WriteLine("  tamano .xlsx: " + primera.Archivo.Length + " bytes (" + (primera.Archivo.Length / 1024.0).ToString("F1") + " KB)");
            var b64 = Convert.ToBase64String(primera.Archivo);
            Console.WriteLine("  tamano Base64: " + b64.Length + " caracteres (" + (b64.Length / 1024.0).ToString("F1") + " KB)");
            Console.WriteLine("  recalculo al abrir: " + primera.MarcadoRecalculoAlAbrir + "; advertencias: " + primera.Advertencias.Count);
            foreach (var a in primera.Advertencias) Console.WriteLine("    - " + a);
        }
        return 0;
    }

    private static List<MapeoCelda> Mapa()
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

    /// <summary>Datos ficticios. Con conPagos, las primeras 12 cuotas traen pago registrado (la 12 parcial).</summary>
    private static DatosArreglo Datos(int plazo, bool conPagos)
    {
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
            { "comentarios", "Datos ficticios del spike C-01. Plazo de " + plazo + " cuotas." },
        };
        return new DatosArreglo(valores, plazo, cuotas);
    }
}
