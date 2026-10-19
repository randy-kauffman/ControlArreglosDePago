using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Acceso a <c>sanic_carp_tbl_cliente</c> (diseno/03 4.2; reglas comunes en <see cref="IConversaciones"/>). Lo implementa
    /// <c>Datos/ClientesDataverse(llamador, sistema)</c>: <see cref="BuscarPorCedula"/> usa SIEMPRE el servicio de SYSTEM (DC-22) y los demás
    /// el del llamador.
    /// </summary>
    internal interface IClientes
    {
        /// <summary>El cliente con las <paramref name="columnas"/> pedidas (servicio del llamador: sin el perfil de columna la cédula no viene), o <c>null</c>.</summary>
        FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas);

        /// <summary>
        /// Ids de los clientes con esa cédula (ya normalizada), activos O inactivos, con el servicio de SYSTEM (2.2, DC-22). <c>TopCount</c> 2,
        /// solo ids: el llamador que excluye su propio registro sabe si hay otro. Cédula nula o vacía ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        IReadOnlyList<Guid> BuscarPorCedula(string cedulaNormalizada);

        /// <summary>Ids de los clientes ACTIVOS (<c>statecode</c> 0) con ese <c>sanic_correo</c> (DC-14), hasta <paramref name="tope"/> (el llamador pide 2). Correo nulo o vacío ⇒ <see cref="ArgumentException"/>.</summary>
        IReadOnlyList<Guid> BuscarActivosPorCorreo(string correo, int tope);
    }
}
