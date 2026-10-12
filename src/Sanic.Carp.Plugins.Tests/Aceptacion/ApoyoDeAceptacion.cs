using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Apoyos de las pruebas de aceptación del arquitecto. Viven en <c>Aceptacion/</c> (y no en <c>Apoyo/</c>) para que el constructor
    /// no pueda tocarlos.
    /// </summary>
    internal static class ApoyoDeAceptacion
    {
        /// <summary>
        /// Culturas con las que se corren las pruebas de dependencia de cultura (diseno/03 0.6, 2.0.1 "siempre con cultura invariante"):
        /// turco y azerí (la "i" sin punto cambia mayúsculas y minúsculas), español de Nicaragua (la del cliente), francés (separador
        /// decimal coma y espacio fino en los miles) y tailandés (calendario budista).
        /// </summary>
        public static readonly string[] NombresDeCulturas = { "tr-TR", "az-Latn-AZ", "es-NI", "fr-FR", "th-TH" };

        public static IEnumerable<object[]> Culturas()
        {
            foreach (var nombre in NombresDeCulturas) yield return new object[] { nombre };
        }

        /// <summary>Ejecuta <paramref name="accion"/> con la cultura del hilo (formato e interfaz) cambiada y la restaura al terminar.</summary>
        public static void ConCultura(string nombreDeCultura, Action accion)
        {
            var anterior = CultureInfo.CurrentCulture;
            var anteriorUi = CultureInfo.CurrentUICulture;
            try
            {
                var cultura = new CultureInfo(nombreDeCultura);
                CultureInfo.CurrentCulture = cultura;
                CultureInfo.CurrentUICulture = cultura;
                accion();
            }
            finally
            {
                CultureInfo.CurrentCulture = anterior;
                CultureInfo.CurrentUICulture = anteriorUi;
            }
        }

        /// <summary>
        /// Lee un documento del repositorio (por ejemplo <c>diseno/03-contratos-custom-api.md</c>) buscándolo hacia arriba desde el
        /// directorio de la prueba. Si no lo encuentra, falla con un mensaje claro: una prueba que contrasta con el diseño no puede
        /// pasar en silencio sin el diseño (diseno/03 2.0.8).
        /// </summary>
        public static string[] LineasDelDocumento(string rutaRelativa)
        {
            var directorio = new DirectoryInfo(AppContext.BaseDirectory);
            while (directorio != null)
            {
                var candidato = Path.Combine(directorio.FullName, rutaRelativa.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidato)) return File.ReadAllLines(candidato, System.Text.Encoding.UTF8);
                directorio = directorio.Parent;
            }
            throw new FileNotFoundException("No se encontró '" + rutaRelativa + "' subiendo desde '" + AppContext.BaseDirectory
                + "'. Las pruebas de aceptación contrastan contra el diseño y necesitan el repositorio completo.");
        }
    }
}
