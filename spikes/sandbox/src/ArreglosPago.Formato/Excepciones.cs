using System;
using System.Collections.Generic;

namespace ArreglosPago.Formato
{
    /// <summary>Base de todos los errores controlados del generador. El plugin los traduce a InvalidPluginExecutionException.</summary>
    public class FormatoException : Exception
    {
        public FormatoException(string mensaje) : base(mensaje) { }
        public FormatoException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }

    /// <summary>El SHA-256 recalculado de la plantilla no coincide con la huella guardada al publicar (D-15).</summary>
    public sealed class HuellaNoCoincideException : FormatoException
    {
        public HuellaNoCoincideException(string mensaje) : base(mensaje) { }
    }

    /// <summary>El mapeo celda-campo es inválido. Reúne todos los errores encontrados, no solo el primero.</summary>
    public sealed class MapeoInvalidoException : FormatoException
    {
        public IReadOnlyList<string> Errores { get; }

        public MapeoInvalidoException(IReadOnlyList<string> errores)
            : base("El mapeo es inválido: " + string.Join(" | ", errores))
        {
            Errores = errores;
        }
    }

    /// <summary>Los datos del arreglo no sirven para esta plantilla (plazo mayor que la capacidad, campo sin valor, tipo no soportado...).</summary>
    public sealed class DatosInvalidosException : FormatoException
    {
        public DatosInvalidosException(string mensaje) : base(mensaje) { }
    }

    /// <summary>El archivo de plantilla está corrupto o no tiene la estructura esperada (contenedor roto o contenido interno ilegible).</summary>
    public sealed class PlantillaInvalidaException : FormatoException
    {
        public PlantillaInvalidaException(string mensaje) : base(mensaje) { }
        public PlantillaInvalidaException(string mensaje, Exception interna) : base(mensaje, interna) { }
    }
}
