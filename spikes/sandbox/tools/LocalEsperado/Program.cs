using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ArreglosPago.Formato;
using Spk.Plugins;

/// <summary>Genera en local (net8.0) el archivo esperado para cada plazo y mide tiempos, con los mismos datos que el plugin.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var plantilla = File.ReadAllBytes(args[0]);
        var salida = args[1];
        int n = args.Length > 2 ? int.Parse(args[2]) : 100;
        Directory.CreateDirectory(salida);
        var huella = GeneradorFormato.CalcularHuella(plantilla);
        var mapa = DatosFicticios.Mapa();
        Console.WriteLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + "; CPUs " + Environment.ProcessorCount);
        var json = new List<string>();
        foreach (var plazo in new[] { 6, 30, 60 })
        {
            var datos = DatosFicticios.Construir(plazo);
            var sw = Stopwatch.StartNew();
            var primero = GeneradorFormato.Generar(plantilla, huella, mapa, datos);
            sw.Stop();
            double frio = sw.Elapsed.TotalMilliseconds;
            var t = new List<double>();
            for (int i = 0; i < n; i++)
            {
                sw.Restart();
                GeneradorFormato.Generar(plantilla, huella, mapa, datos);
                sw.Stop();
                t.Add(sw.Elapsed.TotalMilliseconds);
            }
            t.Sort();
            File.WriteAllBytes(Path.Combine(salida, "local-" + plazo + ".xlsx"), primero);
            var b64 = Convert.ToBase64String(primero).Length;
            Console.WriteLine("plazo {0}: primera {1:F0} ms | caliente mediana {2:F1} ms, p95 {3:F1} ms, max {4:F1} ms | {5} bytes | base64 {6} chars",
                plazo, frio, t[t.Count / 2], t[(int)Math.Ceiling(t.Count * 0.95) - 1], t.Last(), primero.Length, b64);
        }
        return 0;
    }
}
