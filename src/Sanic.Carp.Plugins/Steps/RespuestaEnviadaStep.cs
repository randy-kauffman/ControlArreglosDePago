using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Tiempos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-35 (diseno/03 2.11 y 5): Update · <b>PostOperation · asíncrono</b> · orden 10 · filtro <c>statuscode</c> · sin imagen, sobre <c>email</c>. El nombre de la clase y el namespace son definitivos. Área de
    /// error: <c>RES</c> (solo da forma al <c>-900</c> de una falla inesperada; el catálogo no tiene códigos propios de esta pieza). Todo con <c>contexto.ServicioComoSistema</c> (0.2).
    /// <para>
    /// <b>Contrato</b> ("Decisiones del arquitecto, 2026-10-19 (sublote 5f)"). El step NO mira el <c>Target</c>: decide por la fila releída (DC-29 resuelto: la plataforma ya aplicó el reemplazo
    /// del motivo; ver diseno/03 2.11):
    /// </para>
    /// <list type="number">
    /// <item><c>CorreosDataverse.Leer(contexto.IdPrimario, ReglasDeRespuestaEnviada.ColumnasDelCorreo)</c>. <c>null</c> ⇒ termina. <c>ReglasDeRespuestaEnviada.DebeProcesarse</c> falso ⇒ termina sin escribir.</item>
    /// <item>Instante = <c>InstanteDelEnvio(senton, actualend)</c>; <c>null</c> ⇒ termina sin escribir. <c>createdby</c> ausente ⇒ termina sin escribir.</item>
    /// <item><c>ConversacionesDataverse.SeguirUnida(regarding.Id, ReglasDeEntradaDeCorreo.SaltosDeUnion, ReglasDeRespuestaEnviada.ColumnasDeLaConversacion)</c>: la conversación destino (la propia si no está Unida).
    /// <c>null</c> (cadena que no termina, o sin fila) ⇒ termina sin escribir y traza <c>CARP-ENT-003</c>. Un par <c>statecode</c>/<c>statuscode</c> que <c>Estados.DeConversacion</c> no conoce ⇒ termina sin escribir.</item>
    /// <item><c>TiemposDeConversacion.AplicarRespuestaEnviada(estado, sinrespuestadesde, cantidad (ausente = 0), primera, instante)</c>.</item>
    /// <item>Escribe, en este orden: (a) si la conversación destino no es la del Regarding, <c>ICorreos.ApuntarA(emailId, destino)</c>; (b) <c>ICorreos.EscribirRespuesta(emailId, tiempo, createdby)</c> (DC-12:
    /// siempre <c>sanic_respondidoporid</c>, aunque no haya tiempo); (c) <c>IConversaciones.Actualizar(destino, ReglasDeRespuestaEnviada.CambiosEnLaConversacion(...), null)</c> (sin versión de fila). El Email se escribe ANTES que la conversación (PA-RES-14).</item>
    /// <item>Ninguna escritura repite el filtro <c>statuscode</c> ni las columnas de S-33: el step no se dispara a sí mismo, y S-33 deja pasar a SYSTEM (DC-03). No atrapa ninguna falla.</item>
    /// </list>
    /// </summary>
    public sealed class RespuestaEnviadaStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PostOperation, true, 10, new[] { TablasDelArea.Correo }, new[] { "statuscode" }, null)
        });

        internal override string Area { get { return "RES"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            var servicio = contexto.ServicioComoSistema;
            var correos = new CorreosDataverse(servicio);
            var conversaciones = new ConversacionesDataverse(servicio);
            Guid emailId = contexto.IdPrimario;

            // Se decide por la fila releida, no por el Target (2.11 decision 1). Todo lo que falta termina sin escribir ni lanzar: un reintento no lo arreglaria (decision 2).
            var correo = correos.Leer(emailId, ReglasDeRespuestaEnviada.ColumnasDelCorreo);
            if (correo == null)
            {
                contexto.Trazar("correo " + Texto(emailId) + ": no existe, no se procesa");
                return;
            }
            if (!ReglasDeRespuestaEnviada.DebeProcesarse(correo.Valores))
            {
                contexto.Trazar("correo " + Texto(emailId) + ": no corresponde procesarlo");
                return;
            }

            var instante = ReglasDeRespuestaEnviada.InstanteDelEnvio(Valor(correo.Valores, "senton") as DateTime?, Valor(correo.Valores, "actualend") as DateTime?);
            var creadoPor = Valor(correo.Valores, "createdby") as ReferenciaDeEntidad;
            if (!instante.HasValue || creadoPor == null)
            {
                contexto.Trazar("correo " + Texto(emailId) + ": falta el instante del envio o createdby, no se procesa");
                return;
            }

            var regarding = (ReferenciaDeEntidad)correo.Valores["regardingobjectid"];
            var destino = conversaciones.SeguirUnida(regarding.Id, ReglasDeEntradaDeCorreo.SaltosDeUnion, ReglasDeRespuestaEnviada.ColumnasDeLaConversacion);
            if (destino == null)
            {
                contexto.Trazar("correo " + Texto(emailId) + ": " + CodigosDeError.ENT_003.Codigo);
                return;
            }

            var opcionEstado = Valor(destino.Valores, "statecode") as ValorDeOpcion;
            var opcionMotivo = Valor(destino.Valores, "statuscode") as ValorDeOpcion;
            var estado = opcionEstado == null || opcionMotivo == null ? null : Estados.DeConversacion(new EstadoEfectivo(opcionEstado.Valor, opcionMotivo.Valor));
            if (!estado.HasValue)
            {
                contexto.Trazar("conversacion " + Texto(destino.Id) + ": estado desconocido, no se procesa");
                return;
            }

            var sinRespuestaDesde = Valor(destino.Valores, "sanic_sinrespuestadesde") as DateTime?;
            var primeraRespuesta = Valor(destino.Valores, "sanic_tiempoprimerarespuesta") as int?;
            var resultado = TiemposDeConversacion.AplicarRespuestaEnviada(estado.Value, sinRespuestaDesde, (Valor(destino.Valores, "sanic_cantidadrespuestas") as int?) ?? 0,
                primeraRespuesta, instante.Value);

            // Orden (decision 4, PA-RES-14): el Email antes que la conversacion. Si la conversacion falla, el Email ya quedo marcado (limite declarado, decision 5).
            if (destino.Id != regarding.Id) correos.ApuntarA(emailId, destino.Id);
            correos.EscribirRespuesta(emailId, resultado.TiempoDeEstaRespuesta, creadoPor.Id);
            conversaciones.Actualizar(destino.Id, ReglasDeRespuestaEnviada.CambiosEnLaConversacion(sinRespuestaDesde, primeraRespuesta, resultado), null);
            contexto.Trazar("correo " + Texto(emailId) + ": respuesta aplicada a la conversacion " + Texto(destino.Id));
        }

        private static string Texto(Guid id)
        {
            return id.ToString("D", CultureInfo.InvariantCulture);
        }

        private static object Valor(IReadOnlyDictionary<string, object> valores, string columna)
        {
            object valor;
            return valores.TryGetValue(columna, out valor) ? valor : null;
        }
    }
}
