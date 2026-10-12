namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Tipos de mapeo del generador (diseno/03 4.3). Se obtienen del choice <c>TipoDeMapeo</c> con
    /// <see cref="ClavesDelGenerador.TipoDelGenerador"/>.
    /// Es <c>public</c> (el resto del generador es <c>internal</c>) porque aparece en la firma de una prueba de aceptación y xUnit exige
    /// clases de prueba públicas: "salvo lo que usan las pruebas", diseno/03 4.3 punto 2.
    /// </summary>
    public enum TipoMapeo
    {
        /// <summary>Valor simple: reemplaza el contenido de la celda (incluida una fórmula).</summary>
        Simple = 0,
        /// <summary>Total calculado por el generador y escrito como valor.</summary>
        Total = 1,
        /// <summary>Columna del bloque de cuotas. La celda es la de la primera cuota.</summary>
        Bloque = 2
    }

    /// <summary>
    /// Una fila del mapeo tal como la entiende el generador: sin dependencia de Dataverse y con la clave del generador (texto) en lugar
    /// de la opción del choice. La fila del dominio (<c>FilaDeMapeo</c>) se convierte con <see cref="ClavesDelGenerador.DeFila"/>.
    /// </summary>
    internal sealed class MapeoCelda
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
