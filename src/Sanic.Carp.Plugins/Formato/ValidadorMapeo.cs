using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Reglas del mapeo que el generador necesita para NO corromper el archivo ni escribir donde no se ve (portado de
    /// <c>ValidadorMapeo</c> del spike 1). Lanza <see cref="MapeoInvalidoException"/> con TODOS los errores, no solo el primero.
    /// <para>
    /// Relación con <c>ValidadorDePublicacion</c> (dominio, diseno/03 2.0.7), decisión del arquitecto del 2026-10-12 (4.3): son dos
    /// implementaciones, cada una con su trabajo, y una prueba de paridad impide que se separen. Este validador
    /// existe porque la vista previa (1.1) genera con un mapeo que NUNCA pasó por la publicación; no conoce el choice
    /// <c>CampoMapeable</c> (las claves de valor simple son libres) y por eso no tiene lista blanca de campos ni el
    /// <c>CARP-VER-008</c> (capacidad), que son de la publicación. Invariante: todo mapeo que la publicación acepta, este validador lo acepta.
    /// </para>
    /// </summary>
    internal static class ValidadorMapeo
    {
        /// <summary>Datos del bloque ya validado: hoja, fila inicial, filas máximas y columna de cada campo.</summary>
        internal sealed class Bloque
        {
            public string Hoja;
            public int FilaInicial;
            public int FilasMaximas;
            public Dictionary<string, int> ColumnaPorCampo = new Dictionary<string, int>(StringComparer.Ordinal);
            public int FilaFinal { get { return FilaInicial + FilasMaximas - 1; } }
        }

        /// <summary>Lanza <see cref="MapeoInvalidoException"/> si el mapeo tiene algún problema; una lista nula o vacía es un problema.</summary>
        public static void Validar(IReadOnlyList<MapeoCelda> mapeo)
        {
            var errores = Recolectar(mapeo);
            if (errores.Count > 0) throw new MapeoInvalidoException(errores);
        }

        /// <summary>Valida y devuelve el bloque. Lanza <see cref="MapeoInvalidoException"/> si algo está mal.</summary>
        public static Bloque ValidarYObtenerBloque(IReadOnlyList<MapeoCelda> mapeo)
        {
            Validar(mapeo);
            var bloque = new Bloque();
            foreach (var m in mapeo.Where(x => x.Tipo == TipoMapeo.Bloque))
            {
                ReferenciaCelda r;
                ReferenciaCelda.TryParse(m.Celda, out r);
                bloque.Hoja = m.Hoja;
                bloque.FilaInicial = r.Fila;
                bloque.FilasMaximas = m.FilasMaximas;
                bloque.ColumnaPorCampo[m.Campo] = r.Columna;
            }
            return bloque;
        }

        private static List<string> Recolectar(IReadOnlyList<MapeoCelda> mapeo)
        {
            var errores = new List<string>();
            if (mapeo == null || mapeo.Count == 0)
            {
                errores.Add("El mapeo está vacío.");
                return errores;
            }

            var validas = new List<KeyValuePair<MapeoCelda, ReferenciaCelda>>();
            for (int i = 0; i < mapeo.Count; i++)
            {
                var m = mapeo[i];
                if (m == null)
                {
                    errores.Add("La fila " + (i + 1).ToString(CultureInfo.InvariantCulture) + " del mapeo es nula.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(m.Hoja))
                {
                    errores.Add("La celda '" + m.Celda + "' (campo '" + m.Campo + "') no indica la hoja.");
                    continue;
                }
                ReferenciaCelda r;
                if (!ReferenciaCelda.TryParse(m.Celda, out r))
                {
                    errores.Add("Celda mal formada '" + m.Celda + "' en la hoja '" + m.Hoja + "' (campo '" + m.Campo
                        + "'): se espera notación A1 como B7, de A1 a XFD1048576.");
                    continue;
                }
                if (!CampoCoherenteConTipo(m, r, errores)) continue;
                validas.Add(new KeyValuePair<MapeoCelda, ReferenciaCelda>(m, r));
            }

            // Dos campos en la misma celda.
            foreach (var grupo in validas.GroupBy(v => v.Key.Hoja.ToUpperInvariant() + "!" + v.Value, StringComparer.Ordinal))
            {
                if (grupo.Count() > 1)
                {
                    errores.Add("Dos campos en la misma celda " + grupo.First().Value + " de la hoja '" + grupo.First().Key.Hoja + "': "
                        + string.Join(" y ", grupo.Select(g => "'" + g.Key.Campo + "'")) + ".");
                }
            }

            ValidarBloque(validas, errores);
            return errores;
        }

        private static bool CampoCoherenteConTipo(MapeoCelda m, ReferenciaCelda r, List<string> errores)
        {
            var donde = "La celda " + r + " de la hoja '" + m.Hoja + "'";
            if (string.IsNullOrWhiteSpace(m.Campo))
            {
                errores.Add(donde + " no tiene campo de origen.");
                return false;
            }
            switch (m.Tipo)
            {
                case TipoMapeo.Simple:
                    if (m.Campo.StartsWith("cuota.", StringComparison.Ordinal) || m.Campo.StartsWith("total.", StringComparison.Ordinal))
                    {
                        errores.Add(donde + ": el campo '" + m.Campo + "' no corresponde al tipo valor simple (use tipo bloque o total).");
                        return false;
                    }
                    break;
                case TipoMapeo.Bloque:
                    if (!Campos.BloqueCompleto.Contains(m.Campo))
                    {
                        errores.Add(donde + ": el campo '" + m.Campo + "' no es una columna válida del bloque de cuotas ("
                            + string.Join(", ", Campos.BloqueCompleto) + ").");
                        return false;
                    }
                    break;
                case TipoMapeo.Total:
                    if (!Campos.Totales.Contains(m.Campo))
                    {
                        errores.Add(donde + ": el campo '" + m.Campo + "' no es un total conocido (" + string.Join(", ", Campos.Totales) + ").");
                        return false;
                    }
                    break;
                default:
                    errores.Add(donde + ": tipo de mapeo desconocido.");
                    return false;
            }
            return true;
        }

        private static void ValidarBloque(List<KeyValuePair<MapeoCelda, ReferenciaCelda>> validas, List<string> errores)
        {
            var delBloque = validas.Where(v => v.Key.Tipo == TipoMapeo.Bloque).ToList();

            if (delBloque.Select(v => v.Key.Hoja.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                errores.Add("El bloque de cuotas debe estar en una sola hoja.");
                return;
            }

            foreach (var g in delBloque.GroupBy(v => v.Key.Campo, StringComparer.Ordinal))
            {
                if (g.Count() > 1) errores.Add("La columna de bloque '" + g.Key + "' está mapeada más de una vez.");
            }

            var faltantes = Campos.BloqueCompleto.Where(c => delBloque.All(v => v.Key.Campo != c)).ToList();
            if (faltantes.Count > 0)
            {
                errores.Add("Bloque de cuotas incompleto: faltan las columnas " + string.Join(", ", faltantes.Select(f => "'" + f + "'")) + ".");
            }
            if (delBloque.Count == 0) return;

            if (delBloque.Select(v => v.Value.Fila).Distinct().Count() > 1)
            {
                errores.Add("Las columnas del bloque de cuotas deben tener la misma fila inicial.");
            }
            if (delBloque.Select(v => v.Key.FilasMaximas).Distinct().Count() > 1)
            {
                errores.Add("Las columnas del bloque de cuotas deben tener las mismas filas máximas.");
            }
            foreach (var v in delBloque)
            {
                if (v.Key.FilasMaximas < 1 || v.Key.FilasMaximas > Campos.CapacidadMaxima)
                {
                    errores.Add("Las filas máximas del bloque en la celda " + v.Value + " (" + v.Key.FilasMaximas.ToString(CultureInfo.InvariantCulture)
                        + ") deben estar entre 1 y " + Campos.CapacidadMaxima.ToString(CultureInfo.InvariantCulture) + ".");
                }
                else if ((long)v.Value.Fila + v.Key.FilasMaximas - 1 > ReferenciaCelda.MaxFila)
                {
                    errores.Add("El bloque que empieza en " + v.Value + " se sale de la hoja.");
                }
            }

            // Una celda de otro tipo dentro del rango de una columna del bloque.
            foreach (var otra in validas.Where(v => v.Key.Tipo != TipoMapeo.Bloque))
            {
                foreach (var col in delBloque)
                {
                    if (!string.Equals(otra.Key.Hoja, col.Key.Hoja, StringComparison.OrdinalIgnoreCase)) continue;
                    if (otra.Value.Columna != col.Value.Columna) continue;
                    long ultima = (long)col.Value.Fila + Math.Max(col.Key.FilasMaximas, 1) - 1;
                    if (otra.Value.Fila >= col.Value.Fila && otra.Value.Fila <= ultima)
                    {
                        errores.Add("La celda " + otra.Value + " ('" + otra.Key.Campo + "') se pisa con el bloque de cuotas ('"
                            + col.Key.Campo + "', filas " + col.Value.Fila.ToString(CultureInfo.InvariantCulture) + " a "
                            + ultima.ToString(CultureInfo.InvariantCulture) + ").");
                    }
                }
            }
        }
    }
}
