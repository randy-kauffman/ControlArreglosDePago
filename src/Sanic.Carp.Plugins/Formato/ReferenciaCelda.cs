using System;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>Referencia A1 de una celda. Todo texto que llega de fuera (mapeo, atributos del .xlsx) se interpreta con <see cref="TryParse"/>, nunca se asume.</summary>
    internal struct ReferenciaCelda
    {
        public const int MaxColumna = 16384;      // XFD
        public const int MaxFila = 1048576;

        public int Columna { get; }               // 1 = A
        public int Fila { get; }

        public ReferenciaCelda(int columna, int fila)
        {
            Columna = columna;
            Fila = fila;
        }

        /// <summary>
        /// Solo acepta LETRAS ASCII + NÚMERO canónicos (sin espacios, sin $, sin rangos, sin ceros a la izquierda), de A1 a XFD1048576.
        /// Mismas reglas que el <c>TryParsearCeldaA1</c> privado de <c>ValidadorDePublicacion</c> (dominio); una prueba de paridad
        /// compara los dos.
        /// </summary>
        public static bool TryParse(string texto, out ReferenciaCelda referencia)
        {
            throw new NotImplementedException("Lote 2: portar ReferenciaCelda.TryParse del spike 1.");
        }

        /// <summary>Como <see cref="TryParse"/>, pero lanza <see cref="FormatException"/> si el texto no es una referencia válida.</summary>
        public static ReferenciaCelda Parse(string texto)
        {
            throw new NotImplementedException("Lote 2: portar ReferenciaCelda.Parse del spike 1.");
        }

        /// <summary>1 = "A", 26 = "Z", 27 = "AA", 16384 = "XFD".</summary>
        public static string LetrasDeColumna(int columna)
        {
            throw new NotImplementedException("Lote 2: portar ReferenciaCelda.LetrasDeColumna del spike 1.");
        }

        /// <summary>Notación A1 en mayúsculas, por ejemplo <c>B7</c>.</summary>
        public override string ToString()
        {
            throw new NotImplementedException("Lote 2: portar ReferenciaCelda.ToString del spike 1.");
        }
    }
}
