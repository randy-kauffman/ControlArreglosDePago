using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>Acceso a <c>sanic_carp_tbl_cuota</c> (diseno/03 4.2; reglas comunes en <see cref="IConversaciones"/>). Lo implementa <c>Datos/CuotasDataverse</c>.</summary>
    internal interface ICuotas
    {
        /// <summary>Las cuotas del arreglo ordenadas por <c>sanic_numero</c> ascendente (1.1 paso 4). Pide las <paramref name="columnas"/> (vacío = solo ids, 2.5 paso 4) y pagina hasta agotar.</summary>
        IReadOnlyList<FilaDeDatos> Listar(Guid arregloId, IReadOnlyCollection<string> columnas);

        /// <summary><c>Create</c> individual con exactamente las columnas de <paramref name="valores"/> (DC-20). Devuelve el id.</summary>
        Guid Crear(IReadOnlyDictionary<string, object> valores);

        /// <summary><c>Delete</c> de la cuota. Si no existe, la falla <c>0x80040217</c> escapa.</summary>
        void Borrar(Guid id);

        /// <summary>¿Alguna cuota del arreglo tiene <c>sanic_fechapago</c> no nula? (DC-21.) Una consulta, <c>TopCount</c> 1, solo el id.</summary>
        bool HayPagos(Guid arregloId);

        /// <summary>¿Alguna cuota del arreglo tiene <c>sanic_fechapago</c> nula? (DC-21, paso a Terminado.) Una consulta, <c>TopCount</c> 1, solo el id.</summary>
        bool QuedaAlgunaSinPago(Guid arregloId);
    }
}
