using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xrm.Sdk;
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
            var target = contexto.Target;
            var esSaliente = target.Contains("directioncode") && target["directioncode"] is bool && (bool)target["directioncode"];
            var regardingSdk = target.Contains("regardingobjectid") ? target["regardingobjectid"] as EntityReference : null;
            var regarding = regardingSdk == null ? null : new ReferenciaDeEntidad(regardingSdk.LogicalName, regardingSdk.Id);

            // Identidad y Configuracion son perezosas: leerlas aqui no crea servicios ni consulta (PA-EML-05, PA-EML-06).
            var pedido = new PedidoDeRemitente(esSaliente, regarding, RemitentesDe(target), contexto.Identidad, contexto.Configuracion);
            var resultado = ReglasDeCorreo.EvaluarRemitente(pedido);
            if (!resultado.Permitida) throw resultado.Error;

            object pedidos;
            if (!resultado.Escribir.TryGetValue(ReglasDeCorreo.ColumnaFrom, out pedidos)) return;

            // Sin participationtypemask: la plataforma lo completa (2.9 punto 3). Coleccion nueva: no se muta la que mando la persona.
            var partes = new List<Entity>();
            foreach (var referencia in (IReadOnlyList<ReferenciaDeEntidad>)pedidos)
            {
                partes.Add(new Entity("activityparty") { ["partyid"] = new EntityReference(referencia.Tabla, referencia.Id) });
            }
            target["from"] = new EntityCollection(partes);
        }

        // null = el Target no trae `from`. Una parte sin partyid cuenta como "otra parte" (ReferenciaDeEntidad vacia).
        private static IReadOnlyList<ReferenciaDeEntidad> RemitentesDe(Entity target)
        {
            if (!target.Contains("from")) return null;
            var coleccion = target["from"] as EntityCollection;
            if (coleccion == null) return null;
            var lista = new List<ReferenciaDeEntidad>();
            foreach (var parte in coleccion.Entities)
            {
                var partyid = parte.Contains("partyid") ? parte["partyid"] as EntityReference : null;
                lista.Add(partyid == null ? new ReferenciaDeEntidad(string.Empty, Guid.Empty) : new ReferenciaDeEntidad(partyid.LogicalName, partyid.Id));
            }
            return lista;
        }
    }
}
