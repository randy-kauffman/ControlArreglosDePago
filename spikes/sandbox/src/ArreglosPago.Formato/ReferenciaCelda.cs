using System;

namespace ArreglosPago.Formato
{
    /// <summary>Referencia A1 de una celda. Todo texto que llega de fuera (mapeo, atributos del .xlsx) se parsea con TryParse.</summary>
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

        /// <summary>Solo acepta LETRAS+NÚMERO canónicos (sin espacios, sin $, sin rangos, sin ceros a la izquierda).</summary>
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

        public static ReferenciaCelda Parse(string texto)
        {
            ReferenciaCelda r;
            if (!TryParse(texto, out r)) throw new FormatException("Referencia de celda inválida: '" + texto + "'");
            return r;
        }

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

        public override string ToString()
        {
            return LetrasDeColumna(Columna) + Fila;
        }

        private static bool EsLetraAscii(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }
    }
}
