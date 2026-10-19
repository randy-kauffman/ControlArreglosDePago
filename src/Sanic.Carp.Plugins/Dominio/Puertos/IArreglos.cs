using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Acceso a <c>sanic_carp_tbl_arreglo</c> y a la bitácora <c>sanic_carp_tbl_generacionformato</c> (diseno/03 4.2; reglas comunes en
    /// <see cref="IConversaciones"/>). Lo implementa <c>Datos/ArreglosDataverse</c>. <c>VersionesVigentes()</c> de la tabla 4.2 pasó a
    /// <see cref="IVersiones.Vigentes"/> y <c>CongelarYBitacora</c> se partió en <see cref="Actualizar"/> + <see cref="RegistrarGeneracion"/>.
    /// </summary>
    internal interface IArreglos
    {
        /// <summary>El arreglo con las <paramref name="columnas"/> pedidas, o <c>null</c> si no existe o no se puede leer.</summary>
        FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas);

        /// <summary>
        /// Los <c>sanic_numeroarreglo</c> NO nulos de todos los arreglos del crédito (<c>sanic_producto</c> igual a
        /// <paramref name="producto"/> y <c>sanic_numeroproducto</c> igual a <paramref name="numeroProducto"/>, ya normalizado), en
        /// CUALQUIER estado (2.0.5). Sin orden; pagina hasta agotar. <paramref name="numeroProducto"/> nulo o vacío ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        IReadOnlyList<int> NumerosDelCredito(ProductoCrediticio producto, string numeroProducto);

        /// <summary>Ids de TODOS los arreglos con <c>sanic_conversacionid</c> igual a la conversación, en cualquier estado (1.4 paso 6b). Pagina hasta agotar.</summary>
        IReadOnlyList<Guid> IdsDeConversacion(Guid conversacionId);

        /// <summary><c>Update</c> común con exactamente las columnas de <paramref name="cambios"/> (nulo ⇒ <see cref="ArgumentNullException"/>, vacío ⇒ <see cref="ArgumentException"/>).</summary>
        void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios);

        /// <summary>
        /// <c>Create</c> de una fila de bitácora (1.1 paso 10b) con exactamente: <c>sanic_arregloid</c>, <c>sanic_versionplantillaid</c>,
        /// <c>sanic_generadoporid</c> (<c>systemuser</c>), <c>sanic_fechageneracion</c> y <c>ownerid</c> (<c>team</c>). Sin <c>sanic_nombre</c>.
        /// Devuelve el id.
        /// </summary>
        Guid RegistrarGeneracion(Guid arregloId, Guid versionId, Guid generadoPorId, DateTime fechaGeneracion, Guid propietarioTeamId);
    }
}
