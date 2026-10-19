using System;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>Acceso a <c>systemuser</c> (diseno/03 4.2). Lo implementa <c>Datos/UsuariosDataverse</c>.</summary>
    internal interface IUsuarios
    {
        /// <summary>
        /// <c>fullname</c> del usuario, o <c>null</c> si no existe, no se puede leer (1.3 paso 5: "otro oficial") o no tiene nombre. Un
        /// <c>Retrieve</c> que pide solo <c>fullname</c>. Cualquier otra falla escapa.
        /// </summary>
        string NombreCompleto(Guid id);
    }
}
