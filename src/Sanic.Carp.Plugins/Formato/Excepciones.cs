using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Base de todos los errores controlados del generador. La Custom API los traduce a <c>CARP-GEN-008</c> a <c>-011</c> (diseno/03 1.1,
    /// paso 9); el generador conserva el tipo de la excepción y no la convierte.
    /// </summary>
    internal class FormatoException : Exception
    {
        public FormatoException(string mensaje) : base(mensaje) { }
        public FormatoException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }

    /// <summary>El SHA-256 recalculado de la plantilla no coincide con la huella guardada al publicar (D-15) ⇒ <c>CARP-GEN-008</c>.</summary>
    internal sealed class HuellaNoCoincideException : FormatoException
    {
        public HuellaNoCoincideException(string mensaje) : base(mensaje) { }
    }

    /// <summary>El mapeo celda-campo es inválido ⇒ <c>CARP-GEN-010</c>. Reúne todos los errores encontrados, no solo el primero.</summary>
    internal sealed class MapeoInvalidoException : FormatoException
    {
        public IReadOnlyList<string> Errores { get; }

        public MapeoInvalidoException(IReadOnlyList<string> errores)
            : base("El mapeo es inválido: " + string.Join(" | ", errores))
        {
            Errores = errores;
        }
    }

    /// <summary>
    /// Los datos del arreglo no sirven para esta plantilla (plazo mayor que la capacidad, campo sin valor, tipo no soportado…)
    /// ⇒ <c>CARP-GEN-011</c>.
    /// </summary>
    internal sealed class DatosInvalidosException : FormatoException
    {
        public DatosInvalidosException(string mensaje) : base(mensaje) { }
    }

    /// <summary>
    /// El archivo de plantilla está corrupto o no tiene la estructura esperada (contenedor roto o contenido interno ilegible)
    /// ⇒ <c>CARP-GEN-009</c>. La <see cref="Exception.InnerException"/> guarda la causa original (spike 1, hallazgo 6).
    /// </summary>
    internal sealed class PlantillaInvalidaException : FormatoException
    {
        public PlantillaInvalidaException(string mensaje) : base(mensaje) { }
        public PlantillaInvalidaException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }
}
