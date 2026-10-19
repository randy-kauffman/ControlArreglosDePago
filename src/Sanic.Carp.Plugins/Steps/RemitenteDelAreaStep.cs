using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-31 (diseno/03 2.9 y 5): Create · PreOperation · síncrono · orden 10 · sin filtro · sin imagen, sobre <c>email</c>. El nombre de la clase y el namespace son definitivos. Área de error: <c>EML</c>.
    /// <para>
    /// <b>Contrato</b> (la regla vive en <see cref="ReglasDeCorreo.EvaluarRemitente"/>, 2.9 y "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"; el step solo convierte y conecta):
    /// </para>
    /// <list type="number">
    /// <item>Arma el <see cref="PedidoDeRemitente"/> desde el <c>Target</c>: <c>directioncode</c> (verdadero ⇒ saliente; ausente ⇒ falso), <c>regardingobjectid</c> (neutro), y <c>from</c> convertido en la
    /// lista de <c>partyid</c> neutros (<c>EntityCollection</c> de <c>activityparty</c>; una parte sin <c>partyid</c> ⇒ <c>new ReferenciaDeEntidad(string.Empty, Guid.Empty)</c>; <c>from</c> ausente ⇒ <c>null</c>), más
    /// <c>contexto.Identidad</c> y <c>contexto.Configuracion</c>. NO crea ningún servicio ni consulta nada por su cuenta: un correo entrante, o saliente sin conversación, termina con 0 consultas y 0 servicios creados (PA-EML-05, PA-EML-06).</item>
    /// <item><c>Evaluar</c> no permitida ⇒ <c>throw resultado.Error;</c> (también deja subir tal cual un <c>CARP-CFG-004</c> o <c>-005</c> de la configuración).</item>
    /// <item>Permitida con <c>Escribir["from"]</c> ⇒ pone en el <c>Target</c> <c>from</c> = una <c>EntityCollection</c> de UN <c>activityparty</c> con <c>partyid</c> = <c>EntityReference("queue", queueId)</c>
    /// (sin <c>participationtypemask</c>: la plataforma lo completa). Reemplaza la colección entera; no escribe nada más. No toca <c>parentactivityid</c> (lo completa el botón Responder, spike 4).</item>
    /// </list>
    /// </summary>
    public sealed class RemitenteDelAreaStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Correo }, null, null)
        });

        internal override string Area { get { return "EML"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
