using System;
using System.Collections.Generic;

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
            throw new NotImplementedException("Lote 2: portar ValidadorMapeo.Validar del spike 1.");
        }

        /// <summary>Valida y devuelve el bloque. Lanza <see cref="MapeoInvalidoException"/> si algo está mal.</summary>
        public static Bloque ValidarYObtenerBloque(IReadOnlyList<MapeoCelda> mapeo)
        {
            throw new NotImplementedException("Lote 2: portar ValidadorMapeo.ValidarYObtenerBloque del spike 1.");
        }
    }
}
