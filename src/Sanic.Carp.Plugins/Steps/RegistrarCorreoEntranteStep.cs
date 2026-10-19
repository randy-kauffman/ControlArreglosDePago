using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-34 (diseno/03 2.10 y 5): Create · <b>PostOperation · asíncrono</b> · orden 10 · sin filtro · sin imagen, sobre <c>email</c>. El nombre de la clase y el namespace son definitivos. Área de error: <c>ENT</c>.
    /// Corre como el job asíncrono: <c>InitiatingUserId</c> es quien creó el correo (o la sincronización); TODO lo que lee y escribe va con <c>contexto.ServicioComoSistema</c> (0.2).
    /// <para>
    /// <b>Contrato</b> (la lógica vive en <see cref="RegistradorDeCorreo"/>; "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"):
    /// </para>
    /// <list type="number">
    /// <item>Arma <c>new RegistradorDeCorreo(new CorreosDataverse(s), new ConversacionesDataverse(s), new ClientesDataverse(s, s), contexto.Configuracion, contexto.Trazar)</c> con <c>s</c> =
    /// <c>contexto.ServicioComoSistema</c> (el mismo servicio para el llamador y para SYSTEM de <c>ClientesDataverse</c>: acá no hay llamador, PA-ENT-23).</item>
    /// <item>Llama a <c>Registrar(contexto.IdPrimario)</c>. NO usa nada del <c>Target</c> más que el id (PA-ENT-22): el Regarding de la correlación puede llegar en un <c>Update</c> posterior al <c>Create</c> (spike 3).</item>
    /// <item>Un resultado <c>Omitido</c> NO lanza: este step se registra sobre TODO <c>Create</c> de Email, y cada correo saliente da <c>CARP-ENT-001</c>; lanzar llenaría el registro de trabajos fallidos. Se
    /// traza el tipo del resultado y, si se omitió, el código. Cualquier otra falla (K-01, datos, <c>CARP-CFG-*</c>) sube sin atraparse: el job falla y la reconciliación (1.2) lo repara (D-03).</item>
    /// </list>
    /// </summary>
    public sealed class RegistrarCorreoEntranteStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PostOperation, true, 10, new[] { TablasDelArea.Correo }, null, null)
        });

        internal override string Area { get { return "ENT"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            // El mismo servicio como llamador y como SYSTEM de ClientesDataverse: en el job asincrono no hay llamador al que respetarle permisos (PA-ENT-23).
            var servicio = contexto.ServicioComoSistema;
            var registrador = new RegistradorDeCorreo(new CorreosDataverse(servicio), new ConversacionesDataverse(servicio), new ClientesDataverse(servicio, servicio),
                contexto.Configuracion, contexto.Trazar);

            // Solo el id del Target (PA-ENT-22). Un Omitido no lanza (2.10 decision 10): el registrador ya trazo el codigo; cualquier otra falla sube sin atraparse.
            var resultado = registrador.Registrar(contexto.IdPrimario);
            contexto.Trazar("resultado " + resultado.Tipo + (resultado.Omision == null ? string.Empty : " " + resultado.Omision.Codigo));
        }
    }
}
