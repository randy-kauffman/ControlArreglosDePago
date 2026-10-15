using System;
using System.Globalization;
using System.Text;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// El texto sin las marcas de acento, para comparar como lo hace la plataforma (medido en Dev, <c>18-texto-y-esquema.json</c> punto_c: <c>Equal</c>, <c>Like</c>, <c>BeginsWith</c> e <c>In</c> ignoran mayúsculas Y
    /// acentos). No depende de <c>CompareOptions.IgnoreNonSpace</c> (que con la globalización invariante no hace nada) ni de ICU:
    /// <list type="number">
    /// <item>se descompone en FormD y se quitan los <see cref="UnicodeCategory.NonSpacingMark"/>, que es la regla de la decisión del orquestador (lote 3b, decisión 5);</item>
    /// <item>como <see cref="string.Normalize()"/> usa ICU en Linux y con la globalización invariante devuelve el texto tal cual, se aplica además una tabla EXPLÍCITA de las letras latinas con acento
    /// (Latin-1 Suplemento y Latin Extendido-A), las que tienen descomposición canónica, y se quitan las marcas combinantes por su rango (U+0300 a U+036F). Así <c>José</c>, <c>Josè</c> y <c>Jose</c> más una marca
    /// combinante dan lo mismo con o sin ICU, en net8.0 y en net462.</item>
    /// </list>
    /// Lo que no tiene descomposición (<c>ø</c>, <c>æ</c>, <c>ß</c>, <c>đ</c>) se deja igual, como hace FormD.
    /// </summary>
    internal static class TextoSinAcentos
    {
        // Pares alineados: la letra con acento y la letra base. Solo las que tienen descomposición canónica.
        internal const string ConAcento =
            "ÀÁÂÃÄÅÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜÝàáâãäåçèéêëìíîïñòóôõöùúûüýÿ"
            + "ĀāĂăĄąĆćĈĉĊċČčĎďĒēĔĕĖėĘęĚěĜĝĞğĠġĢģĤĥĨĩĪīĬĭĮįİĴĵĶķĹĺĻļĽľŃńŅņŇňŌōŎŏŐőŔŕŖŗŘřŚśŜŝŞşŠšŢţŤťŨũŪūŬŭŮůŰűŲųŴŵŶŷŸŹźŻżŽž";

        internal const string Base =
            "AAAAAACEEEEIIIINOOOOOUUUUYaaaaaaceeeeiiiinooooouuuuyy"
            + "AaAaAaCcCcCcCcDdEeEeEeEeEeGgGgGgGgHhIiIiIiIiIJjKkLlLlLlNnNnNnOoOoOoRrRrRrSsSsSsSsTtTtUuUuUuUuUuUuWwYyYZzZzZz";

        /// <summary>El texto sin acentos. Un texto solo ASCII vuelve igual, sin tocar.</summary>
        public static string Quitar(string texto)
        {
            if (EsAscii(texto)) return texto;
            string descompuesto;
            try
            {
                descompuesto = texto.Normalize(NormalizationForm.FormD);
            }
            catch (ArgumentException)
            {
                descompuesto = texto;      // un par sustituto roto no se puede normalizar: se compara como llegó
            }
            catch (PlatformNotSupportedException)
            {
                descompuesto = texto;      // sin ICU y sin normalización del sistema: lo cubre la tabla explícita
            }
            return QuitarConLaTabla(descompuesto);
        }

        /// <summary>
        /// Solo la tabla explícita y las marcas combinantes, sin llamar a <see cref="string.Normalize()"/>: es lo que garantiza el resultado cuando la plataforma no normaliza (globalización invariante en Linux).
        /// </summary>
        internal static string QuitarConLaTabla(string texto)
        {
            var limpio = new StringBuilder(texto.Length);
            foreach (var c in texto)
            {
                if (EsMarcaCombinante(c)) continue;
                var indice = ConAcento.IndexOf(c);
                limpio.Append(indice >= 0 ? Base[indice] : c);
            }
            return limpio.ToString();
        }

        private static bool EsMarcaCombinante(char c)
        {
            // La categoría cubre las marcas que el runtime conoce; el rango U+0300 a U+036F son las marcas combinantes latinas y no depende de ninguna tabla de globalización.
            return (c >= '̀' && c <= 'ͯ') || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;
        }

        private static bool EsAscii(string texto)
        {
            foreach (var c in texto)
                if (c > '\u007f') return false;
            return true;
        }
    }
}
