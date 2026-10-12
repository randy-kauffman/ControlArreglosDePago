using System;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Datos sintéticos para la generación de prueba que se hace al publicar una versión de plantilla (DC-19; diseno/03 2.8,
    /// <c>VersionPlantillaActualizarStep</c> paso 3): un <see cref="DatosArreglo"/> con plazo igual a la capacidad y TODAS las claves
    /// del generador con valores inventados, para que cualquier celda mapeada tenga algo que imprimir.
    /// <para>
    /// Nunca son datos reales (def. L411, diseno/03 4.5): cada texto que parece un dato personal o de cuenta (oficial, nombre, cédula,
    /// correo, celular, número de producto, cuenta a debitar y comentarios) lleva la palabra <c>PRUEBA</c> y el correo usa el dominio
    /// reservado <c>example.com</c> (RFC 2606). Son deterministas: no leen el reloj ni números aleatorios (diseno/03 0.6).
    /// </para>
    /// </summary>
    internal static class DatosDePrueba
    {
        /// <summary>
        /// Datos de prueba para una plantilla de <paramref name="capacidad"/> cuotas. Una capacidad fuera de 1..<see cref="Campos.CapacidadMaxima"/>
        /// lanza <see cref="ArgumentOutOfRangeException"/> (la publicación ya la rechazó con <c>CARP-VER-007</c> o <c>-008</c>).
        /// </summary>
        public static DatosArreglo Crear(int capacidad)
        {
            throw new NotImplementedException("Lote 2: datos sintéticos de la publicación (diseno/03 2.8, DC-19).");
        }
    }
}
