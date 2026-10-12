using System;

namespace Sanic.Carp.Plugins.Dominio.Errores
{
    /// <summary>
    /// Fallo de negocio o de configuracion con su codigo <c>CARP-AREA-NNN</c> y su texto en espanol (diseno/03 0.5 y 4.4).
    /// Es a la vez el valor que devuelven las piezas puras (<c>ResultadoDeMontos.Error</c>) y la excepcion que lanzan los puertos
    /// (<c>IConfiguracionDelArea</c> lanza <c>CARP-CFG-*</c>, 4.2): por eso hereda de <see cref="Exception"/>. Crear la instancia
    /// no captura traza de pila; eso solo ocurre al lanzarla.
    /// </summary>
    public sealed class ErrorDeNegocio : Exception
    {
        public string Codigo { get; }

        public string Mensaje { get { return Message; } }

        public ErrorDeNegocio(string codigo, string mensaje)
            : base(mensaje)
        {
            if (string.IsNullOrEmpty(codigo)) throw new ArgumentException("El codigo es obligatorio.", nameof(codigo));
            if (mensaje == null) throw new ArgumentNullException(nameof(mensaje));
            Codigo = codigo;
        }

        /// <summary>Forma que ve quien llama a la Custom API: <c>CARP-ARR-004: texto</c> (diseno/03 0.5).</summary>
        public override string ToString()
        {
            return Codigo + ": " + Message;
        }
    }
}
