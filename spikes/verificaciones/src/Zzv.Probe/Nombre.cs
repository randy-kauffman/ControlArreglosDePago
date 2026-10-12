using System;
using System.Linq;
using Microsoft.Xrm.Sdk;

namespace Zzv.Probe
{
    /// <summary>
    /// V-8 (2026-10-11). Observador de solo lectura de la columna primaria autonumerica sanic_nombre del Arreglo.
    /// Traza, por etapa: si el Target trae la clave "sanic_nombre", su valor crudo (solo si empieza con ARR- o ZZV-, que es el
    /// formato autonumerico ARR-{SEQNUM:6} o un dato ficticio del spike), las claves del Target y el estado del contexto.
    /// No escribe en ninguna tabla ni lanza excepciones. El unsecure config es una etiqueta del step.
    /// </summary>
    public class ZzvNombre : IPlugin
    {
        private readonly string _etiqueta;

        public ZzvNombre(string unsecure, string secure)
        {
            _etiqueta = string.IsNullOrEmpty(unsecure) ? "sin-etiqueta" : unsecure;
        }

        public void Execute(IServiceProvider sp)
        {
            var ctx = (IPluginExecutionContext)sp.GetService(typeof(IPluginExecutionContext));
            var tr = (ITracingService)sp.GetService(typeof(ITracingService));
            var target = ctx.InputParameters.Contains("Target") ? ctx.InputParameters["Target"] as Entity : null;
            string estado;
            if (target == null)
            {
                estado = "Target=(ausente)";
            }
            else
            {
                var tiene = target.Contains("sanic_nombre");
                string valor = "(no esta la clave)";
                if (tiene)
                {
                    var v = target["sanic_nombre"];
                    var s = v as string;
                    if (v == null) valor = "null";
                    else if (s == null) valor = "<" + v.GetType().Name + ">";
                    else if (s.StartsWith("ARR-", StringComparison.Ordinal) || s.StartsWith("ZZV-", StringComparison.Ordinal)) valor = "'" + s + "'";
                    else valor = "<string len=" + s.Length + ">";
                }
                estado = "Target.Id=" + target.Id + "|contieneNombre=" + tiene + "|valorNombre=" + valor
                         + "|claves=[" + string.Join(",", target.Attributes.Keys.OrderBy(k => k).ToArray()) + "]";
            }
            tr.Trace("ZZV|" + _etiqueta + "|msg=" + ctx.MessageName + "|stage=" + ctx.Stage + "|mode=" + ctx.Mode + "|depth=" + ctx.Depth
                     + "|user=" + ctx.UserId + "|init=" + ctx.InitiatingUserId + "|" + estado);

            // Modos de prueba (solo si la etiqueta los pide): "#quitar" saca sanic_nombre del Target; "#fijar=ZZV-xxx" lo reemplaza. Sin modo: solo observa.
            if (target == null) return;
            if (_etiqueta.Contains("#quitar"))
            {
                target.Attributes.Remove("sanic_nombre");
                tr.Trace("ZZV|" + _etiqueta + "|accion=quitado|contieneNombreAhora=" + target.Contains("sanic_nombre"));
            }
            var i = _etiqueta.IndexOf("#fijar=", StringComparison.Ordinal);
            if (i >= 0)
            {
                var v = _etiqueta.Substring(i + 7);
                if (v.StartsWith("ZZV-", StringComparison.Ordinal))
                {
                    target["sanic_nombre"] = v;
                    tr.Trace("ZZV|" + _etiqueta + "|accion=fijado|valor='" + v + "'");
                }
            }
        }
    }
}
