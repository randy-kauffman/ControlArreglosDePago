using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;

namespace Zzv.Probe
{
    /// <summary>
    /// Observador de solo lectura. Escribe en el ITracingService lo que ve el plugin (mensaje, etapa, modo, profundidad,
    /// usuarios, parametros, imagenes). NO escribe en ninguna tabla ni lanza excepciones. El unsecure config es una etiqueta
    /// que identifica el step. Los valores de texto solo se imprimen si empiezan con "ZZV-" (datos ficticios del spike).
    /// </summary>
    public class ZzvObserva : IPlugin
    {
        private readonly string _etiqueta;

        public ZzvObserva(string unsecure, string secure)
        {
            _etiqueta = string.IsNullOrEmpty(unsecure) ? "sin-etiqueta" : unsecure;
        }

        public void Execute(IServiceProvider sp)
        {
            var ctx = (IPluginExecutionContext)sp.GetService(typeof(IPluginExecutionContext));
            var tr = (ITracingService)sp.GetService(typeof(ITracingService));
            var sb = new StringBuilder();
            sb.Append("ZZV|").Append(_etiqueta)
              .Append("|msg=").Append(ctx.MessageName)
              .Append("|stage=").Append(ctx.Stage)
              .Append("|mode=").Append(ctx.Mode)
              .Append("|depth=").Append(ctx.Depth)
              .Append("|primary=").Append(ctx.PrimaryEntityName)
              .Append("|primaryId=").Append(ctx.PrimaryEntityId)
              .Append("|user=").Append(ctx.UserId)
              .Append("|init=").Append(ctx.InitiatingUserId)
              .Append("|parentMsg=").Append(ctx.ParentContext == null ? "(none)" : ctx.ParentContext.MessageName + "/stage" + ctx.ParentContext.Stage);
            tr.Trace(sb.ToString());

            foreach (var kv in ctx.InputParameters)
            {
                tr.Trace("ZZV|{0}|IN {1} = {2}", _etiqueta, kv.Key, Describir(kv.Value));
            }
            foreach (var kv in ctx.PreEntityImages)
            {
                tr.Trace("ZZV|{0}|PRE[{1}] = {2}", _etiqueta, kv.Key, Describir(kv.Value));
            }
            foreach (var kv in ctx.PostEntityImages)
            {
                tr.Trace("ZZV|{0}|POST[{1}] = {2}", _etiqueta, kv.Key, Describir(kv.Value));
            }
        }

        internal static string Describir(object o)
        {
            if (o == null) return "null";
            var e = o as Entity;
            if (e != null)
            {
                var partes = e.Attributes.Select(a => a.Key + "=" + Valor(a.Value)).ToArray();
                return "Entity(" + e.LogicalName + "," + e.Id + ")[" + string.Join("; ", partes) + "]";
            }
            var er = o as EntityReference;
            if (er != null) return "EntityReference(" + er.LogicalName + "," + er.Id + ")";
            return Valor(o);
        }

        internal static string Valor(object v)
        {
            if (v == null) return "null";
            var s = v as string;
            if (s != null) return s.StartsWith("ZZV-", StringComparison.Ordinal) ? "'" + (s.Length > 120 ? s.Substring(0, 120) : s) + "'" : "<string len=" + s.Length + ">";
            var os = v as OptionSetValue;
            if (os != null) return "Option(" + os.Value + ")";
            var er = v as EntityReference;
            if (er != null) return "Ref(" + er.LogicalName + "," + er.Id + ")";
            var m = v as Money;
            if (m != null) return "Money(" + m.Value.ToString(CultureInfo.InvariantCulture) + ")";
            if (v is DateTime) return ((DateTime)v).ToString("o", CultureInfo.InvariantCulture);
            if (v is bool || v is int || v is long || v is decimal || v is double || v is Guid)
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            var arr = v as byte[];
            if (arr != null) return "byte[" + arr.Length + "]";
            return "<" + v.GetType().Name + ">";
        }
    }
}
