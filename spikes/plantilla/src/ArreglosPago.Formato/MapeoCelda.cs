namespace ArreglosPago.Formato
{
    /// <summary>Tipos de mapeo de la tabla "Mapeo de celda" (definicion.md, seccion Datos).</summary>
    public enum TipoMapeo
    {
        /// <summary>Valor simple: reemplaza el contenido de la celda (incluida una fórmula).</summary>
        Simple = 0,
        /// <summary>Total calculado por el generador y escrito como valor.</summary>
        Total = 1,
        /// <summary>Columna del bloque de cuotas. La celda es la de la primera cuota.</summary>
        Bloque = 2
    }

    /// <summary>DTO propio (sin dependencia de Dataverse) de una fila del mapeo.</summary>
    public sealed class MapeoCelda
    {
        public string Hoja { get; }
        public string Celda { get; }
        public TipoMapeo Tipo { get; }
        public string Campo { get; }
        /// <summary>Solo para Bloque: filas máximas (capacidad) del bloque a partir de la celda inicial.</summary>
        public int FilasMaximas { get; }

        public MapeoCelda(string hoja, string celda, TipoMapeo tipo, string campo, int filasMaximas = 0)
        {
            Hoja = hoja;
            Celda = celda;
            Tipo = tipo;
            Campo = campo;
            FilasMaximas = filasMaximas;
        }

        public override string ToString()
        {
            return Hoja + "!" + Celda + " (" + Tipo + " " + Campo + ")";
        }
    }
}
