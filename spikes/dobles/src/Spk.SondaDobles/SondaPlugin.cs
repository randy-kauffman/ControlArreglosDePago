using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Spk.SondaDobles
{
    /// <summary>
    /// SONDA descartable del mini spike de verificacion de los dobles (proyecto 2026-003, diseno/03 4.5, lote 3).
    ///
    /// Reglas: (1) Sale SIN HACER NADA salvo que el InitiatingUserId sea el usuario de aplicacion del spike (configuracion NO segura del step: "u=&lt;guid&gt;;s=&lt;guid de SYSTEM&gt;")
    /// Y la fila lleve el marcador "ZZF-SND-&lt;escenario&gt;-&lt;etiqueta&gt;" en su columna de marca (cliente: sanic_nombre; conversacion: sanic_huellamessageid; arreglo: sanic_numeroproducto).
    /// (2) Solo se registra sobre tablas propias sanic_carp_tbl_*: nunca sobre email, queue ni una tabla nativa.
    /// (3) Saca sus observaciones por ITracingService, en lineas "SND|escenario|etiqueta|paso|clave=valor|...". NO vuelca ningun GUID de usuario ni texto de columnas:
    /// solo tipos .NET, Kind, banderas y comparaciones (el spike las lee de plugintracelogs).
    /// </summary>
    public class SondaPlugin : IPlugin
    {
        private const string Prefijo = "ZZF-SND-";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private IPluginExecutionContext _ctx;
        private ITracingService _trace;
        private IOrganizationServiceFactory _fab;
        private string _esc = "", _tag = "";
        private Guid _usuarioSpike, _system;
        private int _seq;

        public SondaPlugin(string configuracionNoSegura, string configuracionSegura)
        {
            foreach (var par in (configuracionNoSegura ?? "").Split(';'))
            {
                var kv = par.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                if (kv[0].Trim() == "u") Guid.TryParse(kv[1].Trim(), out _usuarioSpike);
                if (kv[0].Trim() == "s") Guid.TryParse(kv[1].Trim(), out _system);
            }
        }

        public void Execute(IServiceProvider sp)
        {
            _ctx = (IPluginExecutionContext)sp.GetService(typeof(IPluginExecutionContext));
            // GUARDA 1: solo el usuario de aplicacion del spike
            if (_usuarioSpike == Guid.Empty || _ctx.InitiatingUserId != _usuarioSpike) return;
            if (_ctx.PrimaryEntityName == null || !_ctx.PrimaryEntityName.StartsWith("sanic_carp_tbl_", StringComparison.Ordinal)) return;

            // GUARDA 2: la fila es ZZF-SND- (marca en el Target o, en un Update, en la pre-imagen)
            Entity target = _ctx.InputParameters.Contains("Target") ? _ctx.InputParameters["Target"] as Entity : null;
            Entity pre = _ctx.PreEntityImages.Contains("PreImagen") ? _ctx.PreEntityImages["PreImagen"] : null;
            var marca = Marca(target) ?? Marca(pre);
            if (marca == null || !marca.StartsWith(Prefijo, StringComparison.Ordinal)) return;
            var partes = marca.Substring(Prefijo.Length).Split('-');
            _esc = partes[0];
            _tag = partes.Length > 1 ? partes[1] : "";

            _trace = (ITracingService)sp.GetService(typeof(ITracingService));
            _fab = (IOrganizationServiceFactory)sp.GetService(typeof(IOrganizationServiceFactory));
            try { Despachar(target, pre, marca); }
            catch (InvalidPluginExecutionException) { throw; }
            catch (Exception ex) { Linea("error_inesperado", "tipo=" + ex.GetType().FullName, "mensaje=" + Corto(ex.Message)); }
        }

        // ------------------------------------------------------------------ escenarios
        private void Despachar(Entity target, Entity pre, string marca)
        {
            var paso = _ctx.MessageName + "." + Etapa() + "." + _ctx.PrimaryEntityName.Substring("sanic_carp_tbl_".Length);
            Contexto(paso, target);
            switch (_esc)
            {
                case "ids":      // (f) Target.Id y PrimaryEntityId en las tres etapas de un Create, sin y con id enviado (el escenario "idsenv" es igual)
                case "idsenv":
                    break;
                case "sv":       // (f) SharedVariables de PreOperation -> escritura anidada (mismo usuario) y su PostOperation
                case "sys":      // idem con la escritura anidada hecha con el servicio de SYSTEM
                    SharedVariables();
                    if (_ctx.MessageName == "Create" && _ctx.Stage == 20 && _ctx.PrimaryEntityName.EndsWith("cliente", StringComparison.Ordinal))
                    {
                        _ctx.SharedVariables["spk.sondaPreOp"] = "puesta-en-PreOperation-" + _tag;
                        var ids = IdsReservados(target);
                        var svc = _fab.CreateOrganizationService(_esc == "sys" ? (Guid?)null : _ctx.UserId);
                        Crear(svc, "anidada", ConvAnidada(target, ids[0], "n1"));
                    }
                    break;
                case "tx0": case "tx1": case "tx2": case "tx3": case "tx4":
                    if (_ctx.MessageName == "Create" && _ctx.Stage == 20 && _ctx.PrimaryEntityName.EndsWith("cliente", StringComparison.Ordinal)) Transaccion(target);
                    break;
                case "rev":      // (11) el Create falla DESPUES de que la plataforma asigno el autonumerico: se revierte
                    if (_ctx.MessageName == "Create" && _ctx.Stage == 40 && _ctx.PrimaryEntityName.EndsWith("conversacion", StringComparison.Ordinal))
                    {
                        Linea("rev_lanza", "nombre_presente=" + (target != null && target.Contains("sanic_nombre")));
                        throw new InvalidPluginExecutionException("SND-rev: revierte el Create despues del autonumerico");
                    }
                    break;
                case "tipos":    // (16) tipos .NET y Kind dentro del sandbox: Target, pre-imagen y Retrieve desde el plugin
                    Tipos(target, pre);
                    break;
                default:
                    Linea("escenario_desconocido");
                    break;
            }
        }

        private void Contexto(string paso, Entity target)
        {
            Linea("ctx:" + paso,
                "stage=" + _ctx.Stage, "mode=" + _ctx.Mode, "depth=" + _ctx.Depth, "msg=" + _ctx.MessageName, "tabla=" + _ctx.PrimaryEntityName,
                "UserId_igual_a_InitiatingUserId=" + (_ctx.UserId == _ctx.InitiatingUserId),
                "UserId_es_el_usuario_del_spike=" + (_ctx.UserId == _usuarioSpike),
                "UserId_es_SYSTEM=" + (_system != Guid.Empty && _ctx.UserId == _system),
                "InitiatingUserId_es_el_usuario_del_spike=" + (_ctx.InitiatingUserId == _usuarioSpike),
                "PrimaryEntityId_vacio=" + (_ctx.PrimaryEntityId == Guid.Empty),
                "Target_Id_vacio=" + (target != null && target.Id == Guid.Empty),
                "Target_Id_igual_a_PrimaryEntityId=" + (target != null && target.Id == _ctx.PrimaryEntityId),
                "Target_tiene_atributo_id_primario=" + (target != null && target.Attributes.Keys.Any(k => k == _ctx.PrimaryEntityName + "id")),
                "OutputParameters_id=" + (_ctx.OutputParameters.Contains("id") ? "presente_" + (((Guid)_ctx.OutputParameters["id"]) == Guid.Empty ? "vacio" : "con_valor") : "ausente"),
                "ParentContext=" + (_ctx.ParentContext != null ? "si:" + _ctx.ParentContext.MessageName + "." + _ctx.ParentContext.PrimaryEntityName + ".depth" + _ctx.ParentContext.Depth : "no"),
                "IsInTransaction=" + _ctx.IsInTransaction,
                "IsolationMode=" + _ctx.IsolationMode);
        }

        private void SharedVariables()
        {
            var claves = _ctx.SharedVariables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var padre = _ctx.ParentContext == null ? new string[0] : _ctx.ParentContext.SharedVariables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            Linea("shared",
                "propias=[" + string.Join(",", claves) + "]",
                "tiene_spk.sondaPreOp=" + _ctx.SharedVariables.ContainsKey("spk.sondaPreOp"),
                "padre=[" + string.Join(",", padre) + "]",
                "padre_tiene_spk.sondaPreOp=" + (_ctx.ParentContext != null && _ctx.ParentContext.SharedVariables.ContainsKey("spk.sondaPreOp")));
        }

        // ------------------------------------------------------------------ escrituras anidadas
        private static List<Guid> IdsReservados(Entity target)
        {
            // el spike reserva (y registra en estado.json ANTES) los GUID de las filas que crea el plugin; llegan en sanic_nombrecompleto del cliente
            var txt = target != null && target.Contains("sanic_nombrecompleto") ? target.GetAttributeValue<string>("sanic_nombrecompleto") : "";
            return Regex.Matches(txt ?? "", "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}").Cast<Match>().Select(m => Guid.Parse(m.Value)).ToList();
        }

        private Entity ConvAnidada(Entity cliente, Guid id, string sufijo)
        {
            var e = new Entity("sanic_carp_tbl_conversacion", id);
            e["sanic_huellamessageid"] = Prefijo + _esc + "-" + _tag + "-" + sufijo;
            e["sanic_fechaprimercorreo"] = DateTime.UtcNow;
            e["sanic_correonuevoenfinalizada"] = false;
            e["sanic_cantidadrespuestas"] = 0;
            e["ownerid"] = cliente.GetAttributeValue<EntityReference>("ownerid");
            return e;
        }

        private void Crear(IOrganizationService svc, string etiqueta, Entity e)
        {
            try
            {
                var id = svc.Create(e);
                Linea("nested_create_" + etiqueta, "resultado=ok", "id_devuelto_igual_al_enviado=" + (id == e.Id));
            }
            catch (Exception ex) { Falla("nested_create_" + etiqueta, ex); }
        }

        private void Transaccion(Entity cliente)
        {
            var ids = IdsReservados(cliente);   // g0..g4
            var svc = _fab.CreateOrganizationService(_ctx.UserId);
            Linea("tx_inicio", "escenario=" + _esc, "ids_reservados=" + ids.Count);
            Crear(svc, "a_ok", ConvAnidada(cliente, ids[0], "a"));
            switch (_esc)
            {
                case "tx1":   // clave alterna duplicada (K-01): falla de plataforma en el INSERT
                    Crear(svc, "b_duplicada_K01", ConvAnidada(cliente, ids[1], "a"));
                    break;
                case "tx2":   // lectura de una fila inexistente
                    try { svc.Retrieve("sanic_carp_tbl_cliente", Guid.NewGuid(), new ColumnSet(false)); Linea("tx_retrieve_inexistente", "resultado=ok_inesperado"); }
                    catch (Exception ex) { Falla("tx_retrieve_inexistente", ex); }
                    break;
                case "tx3":   // actualizacion de una fila inexistente
                    try { svc.Update(new Entity("sanic_carp_tbl_cliente", Guid.NewGuid()) { ["sanic_celular"] = "ZZF-x" }); Linea("tx_update_inexistente", "resultado=ok_inesperado"); }
                    catch (Exception ex) { Falla("tx_update_inexistente", ex); }
                    break;
                case "tx4":   // lookup a una fila inexistente en un Create
                    {
                        var e = ConvAnidada(cliente, ids[1], "b"); e["sanic_clienteid"] = new EntityReference("sanic_carp_tbl_cliente", Guid.NewGuid());
                        Crear(svc, "b_lookup_inexistente", e);
                    }
                    break;
            }
            // la falla (si hubo) fue ATRAPADA: se sigue con otra escritura y con lecturas
            Crear(svc, "c_despues_de_la_falla", ConvAnidada(cliente, ids[2], "c"));
            try { var r = svc.Retrieve("sanic_carp_tbl_conversacion", ids[0], new ColumnSet(false)); Linea("tx_retrieve_de_la_primera", "resultado=ok"); }
            catch (Exception ex) { Falla("tx_retrieve_de_la_primera", ex); }
            Crear(svc, "d_otra_mas", ConvAnidada(cliente, ids[3], "d"));
            Linea("tx_fin", "el_plugin_termina_sin_lanzar=True");
        }

        // ------------------------------------------------------------------ tipos y Kind dentro del sandbox
        private void Tipos(Entity target, Entity pre)
        {
            if (target != null) Atributos("Target", target);
            if (pre != null) Atributos("PreImagen", pre);
            // Retrieve desde el plugin: en PostOperation del Create (la fila ya existe en la transaccion) y en PreOperation del Update (estado previo)
            bool leer = (_ctx.MessageName == "Create" && _ctx.Stage == 40) || (_ctx.MessageName == "Update" && _ctx.Stage == 20);
            if (leer && _ctx.PrimaryEntityId != Guid.Empty)
            {
                var svc = _fab.CreateOrganizationService(_ctx.UserId);
                try
                {
                    var e = svc.Retrieve(_ctx.PrimaryEntityName, _ctx.PrimaryEntityId, new ColumnSet(true));
                    Atributos("Retrieve", e);
                    var q = new QueryExpression(_ctx.PrimaryEntityName) { ColumnSet = new ColumnSet("sanic_fechainicio", "sanic_fechafin", "sanic_fechacongelamiento", "sanic_saldoinicial", "createdon", "ownerid", "statecode") };
                    q.Criteria.AddCondition(_ctx.PrimaryEntityName + "id", ConditionOperator.Equal, _ctx.PrimaryEntityId);
                    var col = svc.RetrieveMultiple(q);
                    if (col.Entities.Count > 0) Atributos("RetrieveMultiple", col.Entities[0]);
                }
                catch (Exception ex) { Falla("retrieve_desde_el_plugin", ex); }
            }
        }

        private void Atributos(string origen, Entity e)
        {
            foreach (var k in e.Attributes.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (k.EndsWith("_base", StringComparison.Ordinal)) continue;
                Linea(origen + ".attr", "col=" + k, "valor=" + Descr(e.Attributes[k]));
            }
        }

        private static string Descr(object v)
        {
            if (v == null) return "null";
            var t = v.GetType().FullName;
            if (v is DateTime) { var d = (DateTime)v; return t + ";Kind=" + d.Kind + ";" + d.ToString("o", Inv); }
            if (v is decimal) { var m = (decimal)v; return t + ";" + m.ToString(Inv) + ";escala=" + ((decimal.GetBits(m)[3] >> 16) & 0xFF); }
            if (v is Money) { var m = ((Money)v).Value; return t + ";" + m.ToString(Inv) + ";escala=" + ((decimal.GetBits(m)[3] >> 16) & 0xFF); }
            if (v is OptionSetValue) return t + ";" + ((OptionSetValue)v).Value;
            if (v is EntityReference) { var r = (EntityReference)v; return t + ";tabla=" + r.LogicalName + ";Name_nulo=" + (r.Name == null); }
            if (v is bool || v is int || v is long) return t + ";" + Convert.ToString(v, Inv);
            if (v is Guid) return t + ";vacio=" + ((Guid)v == Guid.Empty);
            if (v is string) { var s = (string)v; return t + ";largo=" + s.Length + ";empieza_ZZF=" + s.StartsWith("ZZF", StringComparison.Ordinal); }
            return t;
        }

        // ------------------------------------------------------------------ utilidades
        private string Etapa() { return _ctx.Stage == 10 ? "PreValidation" : _ctx.Stage == 20 ? "PreOperation" : _ctx.Stage == 40 ? "PostOperation" : "Etapa" + _ctx.Stage; }

        private static string Marca(Entity e)
        {
            if (e == null) return null;
            foreach (var col in new[] { "sanic_huellamessageid", "sanic_numeroproducto", "sanic_nombre", "sanic_nombrecompleto" })
                if (e.Contains(col) && e[col] is string && ((string)e[col]).StartsWith(Prefijo, StringComparison.Ordinal)) return (string)e[col];
            return null;
        }

        private void Linea(string paso, params string[] kv)
        {
            _seq++;
            var sb = new StringBuilder("SND|").Append(_esc).Append('|').Append(_tag).Append('|').Append(_seq).Append('|').Append(paso);
            foreach (var x in kv) sb.Append('|').Append(x);
            _trace.Trace("{0}", sb.ToString());
        }

        private void Falla(string paso, Exception ex)
        {
            var cadena = new List<string>();
            for (Exception e = ex; e != null; e = e.InnerException) cadena.Add(e.GetType().FullName + "(HResult=" + e.HResult.ToString("X8", Inv) + ")");
            string codigo = "", hex = "", inner = "", claves = "";
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                var t = e.GetType();
                if (!t.IsGenericType) continue;
                var det = t.GetProperty("Detail");
                var d = det != null ? det.GetValue(e, null) as OrganizationServiceFault : null;
                if (d == null) continue;
                codigo = d.ErrorCode.ToString(Inv); hex = "0x" + unchecked((uint)d.ErrorCode).ToString("X8");
                inner = d.InnerFault != null ? "0x" + unchecked((uint)d.InnerFault.ErrorCode).ToString("X8") : "ninguno";
                claves = string.Join(",", d.ErrorDetails.Keys.OfType<object>().Select(x => Convert.ToString(x, Inv)).OrderBy(x => x, StringComparer.Ordinal));
                break;
            }
            Linea(paso, "resultado=FALLA", "cadena=" + string.Join(">", cadena), "ErrorCode=" + codigo, "hex=" + hex, "InnerFault=" + inner, "claves_ErrorDetails=[" + claves + "]", "mensaje=" + Corto(ex.Message));
        }

        private static string Corto(string s)
        {
            s = Regex.Replace(s ?? "", "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}", "<guid>").Replace('\n', ' ').Replace('\r', ' ').Replace('|', '/');
            return s.Length > 220 ? s.Substring(0, 220) : s;
        }
    }
}
