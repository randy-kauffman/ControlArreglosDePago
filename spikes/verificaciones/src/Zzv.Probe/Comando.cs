using System;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Zzv.Probe
{
    /// <summary>
    /// Ejecuta una orden de prueba escrita en Cliente.sanic_nombrecompleto con la forma
    ///   ZZV-CMD|etiqueta|op|svc|...   (svc = sys: CreateOrganizationService(null) = SYSTEM; usr: el usuario del contexto)
    ///   upd      |sys|entidad|guid|atributo|valor        Update de UNA columna de texto
    ///   updstate |sys|entidad|guid|statecode|statuscode  Update con statecode y statuscode juntos (SDK)
    ///   setstate |sys|entidad|guid|statecode|statuscode  SetStateRequest
    ///   updbool  |sys|entidad|guid|atributo|true/false  Update de una columna booleana
    ///   updref   |sys|entidad|guid|atributo|entidadDestino|guidDestino  Update de un lookup
    ///   whoami   |sys
    /// El resultado (OK o la excepcion completa) se escribe en la traza; nunca se relanza.
    /// </summary>
    public class ZzvComando : IPlugin
    {
        public void Execute(IServiceProvider sp)
        {
            var ctx = (IPluginExecutionContext)sp.GetService(typeof(IPluginExecutionContext));
            var tr = (ITracingService)sp.GetService(typeof(ITracingService));
            var fac = (IOrganizationServiceFactory)sp.GetService(typeof(IOrganizationServiceFactory));
            var target = ctx.InputParameters.Contains("Target") ? ctx.InputParameters["Target"] as Entity : null;
            if (target == null) return;
            var txt = target.GetAttributeValue<string>("sanic_nombrecompleto");
            if (txt == null || !txt.StartsWith("ZZV-CMD|", StringComparison.Ordinal)) return;
            var p = txt.Split('|');
            var tag = p[1];
            tr.Trace("ZZVCMD|{0}|INICIO op={1} svc={2} depth={3} user={4} init={5}", tag, p[2], p[3], ctx.Depth, ctx.UserId, ctx.InitiatingUserId);
            try
            {
                IOrganizationService svc = p[3] == "sys" ? fac.CreateOrganizationService(null) : fac.CreateOrganizationService(ctx.UserId);
                var yo = ((WhoAmIResponse)svc.Execute(new WhoAmIRequest())).UserId;
                string nombre = "?";
                try { nombre = svc.Retrieve("systemuser", yo, new ColumnSet("fullname")).GetAttributeValue<string>("fullname"); } catch (Exception) { nombre = "(no legible)"; }
                tr.Trace("ZZVCMD|{0}|WHOAMI del servicio = {1} fullname='{2}'", tag, yo, nombre);
                switch (p[2])
                {
                    case "whoami":
                        break;
                    case "upd":
                        {
                            var e = new Entity(p[4], Guid.Parse(p[5]));
                            e[p[6]] = p[7];
                            svc.Update(e);
                            break;
                        }
                    case "updbool":
                        {
                            var e = new Entity(p[4], Guid.Parse(p[5]));
                            e[p[6]] = p[7] == "true";
                            svc.Update(e);
                            break;
                        }
                    case "updref":
                        {
                            var e = new Entity(p[4], Guid.Parse(p[5]));
                            e[p[6]] = new EntityReference(p[7], Guid.Parse(p[8]));
                            svc.Update(e);
                            break;
                        }
                    case "updstate":
                        {
                            var e = new Entity(p[4], Guid.Parse(p[5]));
                            e["statecode"] = new OptionSetValue(int.Parse(p[6]));
                            e["statuscode"] = new OptionSetValue(int.Parse(p[7]));
                            svc.Update(e);
                            break;
                        }
                    case "setstate":
                        {
                            svc.Execute(new SetStateRequest
                            {
                                EntityMoniker = new EntityReference(p[4], Guid.Parse(p[5])),
                                State = new OptionSetValue(int.Parse(p[6])),
                                Status = new OptionSetValue(int.Parse(p[7]))
                            });
                            break;
                        }
                    default:
                        tr.Trace("ZZVCMD|{0}|op desconocida", tag);
                        return;
                }
                tr.Trace("ZZVCMD|{0}|RESULTADO=OK", tag);
            }
            catch (FaultException<OrganizationServiceFault> fe)
            {
                tr.Trace("ZZVCMD|{0}|RESULTADO=FALLO FaultException code={1} msg={2}", tag, fe.Detail.ErrorCode, fe.Detail.Message);
            }
            catch (Exception ex)
            {
                tr.Trace("ZZVCMD|{0}|RESULTADO=FALLO {1}: {2}", tag, ex.GetType().Name, ex.Message);
            }
        }
    }
}
