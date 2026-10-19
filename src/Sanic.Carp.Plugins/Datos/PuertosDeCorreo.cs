using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Correo;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary>
    /// <see cref="ILecturasDeCorreo"/> sobre el servicio que se le dé (S-33 le pasa el de SYSTEM). Contrato en el puerto y en diseno/03 2.9. Vive en <c>Datos</c> y no en <c>Steps</c> (la guardia
    /// <c>PA5A_REG_04</c> exige que todo tipo de <c>Steps</c> sea un step).
    /// <list type="bullet">
    /// <item><b>Perezoso:</b> recibe una <c>Func</c> y NO la invoca al construirse ni al pedir <c>ColumnasDelSistema</c> hasta que hace falta leer; así S-32 y S-33 no crean el servicio de SYSTEM en el camino barato (el
    /// <c>Target</c> sin las columnas, o el actor del sistema). Una <c>Func</c> nula ⇒ <see cref="ArgumentNullException"/> al construirse.</item>
    /// <item><c>ColumnasDelSistema</c>: <c>new CorreosDataverse(servicio).Leer(emailId, ReglasDeCorreo.ColumnasSoloSistema)</c> y completa con <c>null</c> la columna ausente; si no hay fila, las dos en <c>null</c>.</item>
    /// </list>
    /// </summary>
    internal sealed class LecturasDeCorreoDataverse : ILecturasDeCorreo, ILecturasDelRemitente
    {
        private readonly Func<IOrganizationService> _servicio;

        public LecturasDeCorreoDataverse(Func<IOrganizationService> servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public IReadOnlyDictionary<string, object> ColumnasDelSistema(Guid emailId)
        {
            var fila = new CorreosDataverse(_servicio()).Leer(emailId, ReglasDeCorreo.ColumnasSoloSistema);
            var valores = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var columna in ReglasDeCorreo.ColumnasSoloSistema)
            {
                object valor = null;
                if (fila != null) fila.Valores.TryGetValue(columna, out valor);
                valores[columna] = valor;
            }
            return new ReadOnlyDictionary<string, object>(valores);
        }

        /// <summary>
        /// <see cref="ILecturasDelRemitente.ColumnasDelRemitente"/> (S-37): <c>new CorreosDataverse(servicio).Leer(emailId, ReglasDeCorreo.ColumnasDelRemitente)</c> (UN <c>Retrieve</c> por id con esas dos columnas,
        /// nunca todas) y devuelve SIEMPRE las dos claves, con <c>null</c> en la que la fila no trae y en las dos si no hay fila o no hay acceso. Pide el servicio a la <c>Func</c> recién aquí.
        /// </summary>
        public IReadOnlyDictionary<string, object> ColumnasDelRemitente(Guid emailId)
        {
            throw new NotImplementedException("S-37: lo implementa el constructor (diseno/03 2.9, cierre del sublote 5f).");
        }
    }
}
