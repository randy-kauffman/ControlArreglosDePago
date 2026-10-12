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
            referencia = default(ReferenciaCelda);
            if (string.IsNullOrEmpty(texto) || texto.Length > 10) return false;

            int i = 0;
            long col = 0;
            while (i < texto.Length && EsLetraAscii(texto[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(texto[i]) - 'A' + 1);
                i++;
                if (i > 3) return false;
            }
            if (i == 0 || col > MaxColumna) return false;

            int inicioDigitos = i;
            if (i >= texto.Length || texto[i] == '0') return false;
            long fila = 0;
            while (i < texto.Length && texto[i] >= '0' && texto[i] <= '9')
            {
                fila = fila * 10 + (texto[i] - '0');
                i++;
            }
            if (i != texto.Length || i == inicioDigitos || fila > MaxFila) return false;

            referencia = new ReferenciaCelda((int)col, (int)fila);
            return true;
        }

        /// <summary>Como <see cref="TryParse"/>, pero lanza <see cref="FormatException"/> si el texto no es una referencia válida.</summary>
        public static ReferenciaCelda Parse(string texto)
        {
            ReferenciaCelda r;
            if (!TryParse(texto, out r)) throw new FormatException("Referencia de celda inválida: '" + texto + "'");
            return r;
        }

        /// <summary>1 = "A", 26 = "Z", 27 = "AA", 16384 = "XFD".</summary>
        public static string LetrasDeColumna(int columna)
        {
            var letras = string.Empty;
            while (columna > 0)
            {
                int resto = (columna - 1) % 26;
                letras = (char)('A' + resto) + letras;
                columna = (columna - 1) / 26;
            }
            return letras;
        }

        /// <summary>Notación A1 en mayúsculas, por ejemplo <c>B7</c>.</summary>
        public override string ToString()
        {
            return LetrasDeColumna(Columna) + Fila.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool EsLetraAscii(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }
    }
}
