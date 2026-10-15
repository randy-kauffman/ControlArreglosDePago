using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

public static partial class Program
{
    // ------------------------------------------------------------------ (9) Name de un lookup
    private static JsonNode? RefDe(Entity e, string col)
    {
        if (!e.Contains(col)) return new JsonObject { ["presente"] = false };
        var r = e.GetAttributeValue<EntityReference>(col);
        return new JsonObject
        {
            ["presente"] = true, ["logicalName"] = r.LogicalName, ["Name_es_nulo"] = r.Name == null,
            ["Name_empieza_con_ZZF"] = r.Name?.StartsWith("ZZF", StringComparison.Ordinal),
            ["Name_es_el_enviado"] = r.Name == "Nombre-falso-enviado",
            ["Name_largo"] = r.Name?.Length,
        };
    }

    private static void NameDelLookup()
    {
        var c = new JsonArray();
        const string falso = "Nombre-falso-enviado";
        c.Add(Intento("Create con Name enviado distinto en sanic_clienteid, ownerid (team) y sanic_oficialasignadoid (systemuser); Retrieve", () =>
        {
            var e = ConvE("Nm1", x =>
            {
                x["sanic_clienteid"] = new EntityReference(CLI, G("cli-A")) { Name = falso };
                x["ownerid"] = new EntityReference("team", TeamDef) { Name = falso };
                x["sanic_oficialasignadoid"] = new EntityReference("systemuser", UserId) { Name = falso };
            });
            var id = Crear("conv-Nm1", e);
            var r = Leer(CONV, id, "sanic_clienteid", "ownerid", "sanic_oficialasignadoid");
            return new JsonObject { ["sanic_clienteid"] = RefDe(r, "sanic_clienteid"), ["ownerid"] = RefDe(r, "ownerid"), ["sanic_oficialasignadoid"] = RefDe(r, "sanic_oficialasignadoid"), ["fila_completa_anonimizada"] = S.Entidad(r) };
        }));
        c.Add(Intento("RetrieveMultiple de la misma fila con las mismas columnas", () =>
        {
            var q = new QueryExpression(CONV) { ColumnSet = new ColumnSet("sanic_clienteid", "ownerid", "sanic_oficialasignadoid") };
            q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.Equal, "ZZF-d-Nm1");
            var r = Svc.RetrieveMultiple(q).Entities[0];
            return new JsonObject { ["sanic_clienteid"] = RefDe(r, "sanic_clienteid"), ["ownerid"] = RefDe(r, "ownerid"), ["sanic_oficialasignadoid"] = RefDe(r, "sanic_oficialasignadoid") };
        }));
        c.Add(Intento("Update con Name enviado en sanic_clienteid (cambia a otro cliente: se crea cli-B) y relectura", () =>
        {
            Crear("cli-B", new Entity(CLI) { ["sanic_nombre"] = "ZZF-cli-B", ["sanic_nombrecompleto"] = "ZZF-cli-B", ["sanic_cedula"] = "ZZF-ced-B", ["ownerid"] = Dueno() });
            Svc.Update(new Entity(CONV, G("conv-Nm1")) { ["sanic_clienteid"] = new EntityReference(CLI, G("cli-B")) { Name = falso } });
            var r = Leer(CONV, G("conv-Nm1"), "sanic_clienteid");
            return new JsonObject { ["sanic_clienteid"] = RefDe(r, "sanic_clienteid") };
        }));
        c.Add(Intento("Create con Name = null y con Name = '' ", () =>
        {
            var e = ConvE("Nm2", x => { x["sanic_clienteid"] = new EntityReference(CLI, G("cli-A")) { Name = null }; });
            var id = Crear("conv-Nm2", e);
            var r = Leer(CONV, id, "sanic_clienteid");
            Svc.Update(new Entity(CONV, id) { ["sanic_clienteid"] = new EntityReference(CLI, G("cli-A")) { Name = "" } });
            var r2 = Leer(CONV, id, "sanic_clienteid");
            return new JsonObject { ["con_Name_nulo"] = RefDe(r, "sanic_clienteid"), ["con_Name_vacio"] = RefDe(r2, "sanic_clienteid") };
        }));
        c.Add(Intento("Create con el lookup a una referencia cuyo LogicalName esta en mayusculas ('SANIC_CARP_TBL_CLIENTE')", () =>
        {
            var e = ConvE("Nm3", x => x["sanic_clienteid"] = new EntityReference("SANIC_CARP_TBL_CLIENTE", G("cli-A")));
            var id = CrearIntento("conv-Nm3", e);
            return RefDe(Leer(CONV, id, "sanic_clienteid"), "sanic_clienteid");
        }));
        Escribir("15-name-del-lookup.json", new JsonObject
        {
            ["cabecera"] = Cabecera("15-name-del-lookup", "SINTETICAS ZZF-",
                "Punto (9): la plataforma ignora el Name que se envia en un EntityReference? Que Name devuelve al releer?",
                "No se vuelca el Name leido (es el nombre primario real del destino, p. ej. del team): solo si es nulo, si empieza con ZZF, si es el enviado y su largo."),
            ["contenido"] = new JsonObject { ["casos"] = c },
        });
    }

    // ------------------------------------------------------------------ (12)(14)(d)(c)(i)
    private static int LargoMax(string tabla, string col)
    {
        var r = (RetrieveAttributeResponse)Svc.Execute(new RetrieveAttributeRequest { EntityLogicalName = tabla, LogicalName = col, RetrieveAsIfPublished = true });
        return ((StringAttributeMetadata)r.AttributeMetadata).MaxLength ?? -1;
    }

    private static JsonObject TextoCaso(string caso, string tabla, string clave, string col, string texto)
    {
        return Intento(caso, () =>
        {
            var id = G(clave);
            Svc.Update(new Entity(tabla, id) { [col] = texto });
            var leido = Leer(tabla, id, col).GetAttributeValue<string>(col);
            return new JsonObject
            {
                ["unidades_UTF16"] = texto.Length, ["puntos_de_codigo"] = texto.EnumerateRunes().Count(), ["bytes_UTF8"] = Encoding.UTF8.GetByteCount(texto), ["bytes_UTF16"] = texto.Length * 2,
                ["elementos_de_texto_grafemas"] = new StringInfo(texto).LengthInTextElements,
                ["releido_igual_al_enviado"] = leido == texto, ["releido_unidades_UTF16"] = leido?.Length,
            };
        });
    }

    private static string Rep(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

    private static void TextoYEsquema()
    {
        var texto = new JsonArray(); var esquema = new JsonArray(); var vacio = new JsonArray(); var igualdad = new JsonArray(); var inexistente = new JsonArray();

        // ---- (12) largo del texto
        int max = LargoMax(CLI, "sanic_celular");
        const string emoji = "\U0001F600", comb = "é", acento = "é";
        var casosTexto = new (string etq, string s)[]
        {
            ("ASCII x max", Rep("a", max)), ("ASCII x max+1", Rep("a", max + 1)),
            ("e con acento precompuesto x max (2 bytes UTF-8 cada uno)", Rep(acento, max)), ("e con acento precompuesto x max+1", Rep(acento, max + 1)),
            ("emoji (par sustituto) x max/2: max unidades UTF-16, max/2 puntos de codigo", Rep(emoji, max / 2)),
            ("emoji x max/2+1: max+2 unidades UTF-16", Rep(emoji, max / 2 + 1)),
            ("emoji x max: 2*max unidades, max puntos de codigo", Rep(emoji, max)),
            ("e + acento combinante x max/2: max unidades, max/2 puntos, max/2 grafemas", Rep(comb, max / 2)),
            ("e + acento combinante x max: 2*max unidades, max grafemas", Rep(comb, max)),
        };
        foreach (var (etq, s) in casosTexto) texto.Add(TextoCaso($"sanic_celular T({max}): {etq}", CLI, "cli-A", "sanic_celular", s));
        // restaurar un valor corto para que la fila no quede con el ultimo
        Svc.Update(new Entity(CLI, G("cli-A")) { ["sanic_celular"] = "ZZF-0000" });

        // ---- (d) cadena vacia por Create y por Update (Cd nacio con asunto ""; Ce nacio con 'ZZF-Gamma')
        vacio.Add(Intento("Create con sanic_asunto = '' (fila Cd): relectura", () =>
        {
            var r = Leer(CONV, G("conv-Cd"), "sanic_asunto");
            return new JsonObject { ["columna_presente"] = r.Contains("sanic_asunto"), ["valor_es_cadena_vacia"] = r.Contains("sanic_asunto") && r.GetAttributeValue<string>("sanic_asunto") == "" };
        }));
        vacio.Add(Intento("Update de sanic_asunto 'ZZF-Gamma' a '' (fila Ce): relectura", () =>
        {
            Svc.Update(new Entity(CONV, G("conv-Ce")) { ["sanic_asunto"] = "" });
            var r = Leer(CONV, G("conv-Ce"), "sanic_asunto");
            return new JsonObject { ["columna_presente"] = r.Contains("sanic_asunto"), ["valor_es_cadena_vacia"] = r.Contains("sanic_asunto") && r.GetAttributeValue<string>("sanic_asunto") == "" };
        }));
        vacio.Add(Intento("Update de sanic_asunto a null (fila Ce): relectura", () =>
        {
            Svc.Update(new Entity(CONV, G("conv-Ce")) { ["sanic_asunto"] = null });
            var r = Leer(CONV, G("conv-Ce"), "sanic_asunto");
            return new JsonObject { ["columna_presente"] = r.Contains("sanic_asunto") };
        }));
        vacio.Add(Intento("Update de sanic_asunto a '   ' (solo espacios) y relectura", () =>
        {
            Svc.Update(new Entity(CONV, G("conv-Ce")) { ["sanic_asunto"] = "   " });
            var r = Leer(CONV, G("conv-Ce"), "sanic_asunto");
            return new JsonObject { ["columna_presente"] = r.Contains("sanic_asunto"), ["largo"] = r.GetAttributeValue<string>("sanic_asunto")?.Length };
        }));
        vacio.Add(Consulta("consulta: sanic_asunto Null (Ca, Cd y Ce ahora)", CONV, q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.Null)));
        vacio.Add(Consulta("consulta: sanic_asunto Equal '' ", CONV, q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.Equal, "")));

        // ---- (c) igualdad de texto sin distinguir mayusculas ni acentos. CliA: sanic_nombrecompleto = 'ZZF-José'
        foreach (var v in new[] { "ZZF-José", "zzf-josé", "ZZF-JOSÉ", "ZZF-Jose", "ZZF-jose", "ZZF-JOSE", "ZZF-José" /* e + combinante */, "ZZF-Josè" })
        {
            var valor = v;
            igualdad.Add(Consulta($"Equal sobre sanic_nombrecompleto: '{valor}'", CLI, q => q.Criteria.AddCondition("sanic_nombrecompleto", ConditionOperator.Equal, valor)));
        }
        igualdad.Add(Consulta("Like 'ZZF-jos%' sobre sanic_nombrecompleto", CLI, q => q.Criteria.AddCondition("sanic_nombrecompleto", ConditionOperator.Like, "ZZF-jos%")));
        igualdad.Add(Consulta("Like 'ZZF-jose%' (sin acento) sobre sanic_nombrecompleto", CLI, q => q.Criteria.AddCondition("sanic_nombrecompleto", ConditionOperator.Like, "ZZF-jose%")));
        igualdad.Add(Consulta("BeginsWith 'zzf-JOSE' sobre sanic_nombre", CLI, q => q.Criteria.AddCondition("sanic_nombre", ConditionOperator.BeginsWith, "zzf-JOSE")));
        igualdad.Add(Consulta("In ['zzf-josé'] sobre sanic_nombre", CLI, q => q.Criteria.AddCondition("sanic_nombre", ConditionOperator.In, new object[] { "zzf-josé" })));

        // ---- rechazos de esquema: tipo, HRESULT, InnerFault, ErrorDetails de cada uno
        var cli = G("cli-A"); var a2 = G("arr-A2"); var ca = G("conv-Ca");
        JsonObject Upd(string caso, string tabla, Guid id, string col, object? valor) => Intento(caso, () => { Svc.Update(new Entity(tabla, id) { [col] = valor }); return null; });
        // Update + relectura de la MISMA columna: distingue "aceptado y aplicado" de "aceptado e ignorado"
        JsonObject UpdLee(string caso, string tabla, Guid id, string col, object? valor) => Intento(caso, () =>
        {
            var antes = Svc.Retrieve(tabla, id, new ColumnSet(col));
            Svc.Update(new Entity(tabla, id) { [col] = valor });
            var despues = Svc.Retrieve(tabla, id, new ColumnSet(col));
            object? a = antes.Contains(col) ? antes[col] : null, d = despues.Contains(col) ? despues[col] : null;
            return new JsonObject
            {
                ["antes"] = a == null ? null : V(a, col, tabla), ["enviado"] = valor == null ? null : V(valor, col, tabla), ["despues"] = d == null ? null : V(d, col, tabla),
                ["la_columna_cambio"] = !Equals(a, d) && !(a is EntityReference ra && d is EntityReference rd && ra.Id == rd.Id), ["el_valor_leido_es_el_enviado"] = Equals(d, valor),
            };
        });
        esquema.Add(Upd("Update de columna inexistente (sanic_noexiste) en Cliente", CLI, cli, "sanic_noexiste", "ZZF-x"));
        esquema.Add(Intento("Create de Cliente con columna inexistente", () => { CrearIntento("cli-noexiste", new Entity(CLI) { ["sanic_nombre"] = "ZZF-noexiste", ["sanic_noexiste"] = "ZZF-x", ["ownerid"] = Dueno() }); return null; }));
        esquema.Add(Upd("Update int en columna decimal (sanic_saldoinicial = 5)", ARR, a2, "sanic_saldoinicial", 5));
        esquema.Add(Upd("Update Money en columna decimal", ARR, a2, "sanic_saldoinicial", new Money(5m)));
        esquema.Add(UpdLee("Update double en columna decimal (5.5d)", ARR, a2, "sanic_saldoinicial", 5.5d));
        esquema.Add(Upd("Update string en columna decimal", ARR, a2, "sanic_saldoinicial", "5.5"));
        esquema.Add(UpdLee("Update decimal con 3 decimales en columna decimal(2) (5.555)", ARR, a2, "sanic_saldoinicial", 5.555m));
        esquema.Add(UpdLee("Update decimal con 3 decimales (5.554) en decimal(2)", ARR, a2, "sanic_saldoinicial", 5.554m));
        esquema.Add(UpdLee("Update decimal (1000.5m) en decimal(2): restaura", ARR, a2, "sanic_saldoinicial", 1000.5m));
        esquema.Add(Upd("Update decimal fuera de rango (1e12) en decimal(2)", ARR, a2, "sanic_saldoinicial", 1000000000000m));
        esquema.Add(Upd("Update decimal en columna entera (sanic_plazo = 6.5m)", ARR, a2, "sanic_plazo", 6.5m));
        esquema.Add(Upd("Update string en columna entera (sanic_plazo = '6')", ARR, a2, "sanic_plazo", "6"));
        esquema.Add(Upd("Update entero fuera de rango Int32 (sanic_plazo = long 3000000000)", ARR, a2, "sanic_plazo", 3000000000L));
        esquema.Add(Upd("Update int en columna de texto (sanic_celular = 123)", CLI, cli, "sanic_celular", 123));
        esquema.Add(Upd("Update string en columna de fecha (sanic_fechafin = '2026-01-01')", ARR, a2, "sanic_fechafin", "2026-01-01"));
        esquema.Add(Upd("Update int en columna choice (sanic_tipo = 159460001 como int)", ARR, a2, "sanic_tipo", 159460001));
        esquema.Add(Upd("Update OptionSetValue en columna de texto (sanic_celular)", CLI, cli, "sanic_celular", new OptionSetValue(1)));
        esquema.Add(Upd("Update string en columna de lookup (sanic_clienteid = 'x')", ARR, a2, "sanic_clienteid", "x"));
        esquema.Add(Upd("Update OptionSetValue inexistente en choice (sanic_tipo = 999)", ARR, a2, "sanic_tipo", new OptionSetValue(999)));
        esquema.Add(Intento("Create de Arreglo con choice inexistente (sanic_producto = 999)", () => { CrearIntento("arr-choice", ArrE("ch", cli, G("ver-A"), e => e["sanic_producto"] = new OptionSetValue(999))); return null; }));
        esquema.Add(Upd("Update lookup a la tabla equivocada (Arreglo.sanic_clienteid = Conversacion)", ARR, a2, "sanic_clienteid", new EntityReference(CONV, ca)));
        esquema.Add(Intento("Create de Conversacion con lookup a la tabla equivocada (sanic_clienteid = Version)", () => { CrearIntento("conv-lookup-tabla", ConvE("lt", e => e["sanic_clienteid"] = new EntityReference(VER, G("ver-A")))); return null; }));
        esquema.Add(Upd("Update ownerid a la tabla equivocada (ownerid = Cliente)", CONV, ca, "ownerid", new EntityReference(CLI, cli)));
        esquema.Add(UpdLee("Update de createdon (2020-01-01)", ARR, a2, "createdon", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        esquema.Add(Intento("Create con createdon enviado (2020-01-01)", () => { var id = CrearIntento("conv-createdon", ConvE("co", e => e["createdon"] = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc))); return new JsonObject { ["createdon_releido_es_2020"] = Leer(CONV, id, "createdon").GetAttributeValue<DateTime>("createdon").Year == 2020 }; }));
        esquema.Add(Intento("Create con overriddencreatedon enviado (2020-01-01)", () => { var id = CrearIntento("conv-overridden", ConvE("ov", e => e["overriddencreatedon"] = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc))); var r = Leer(CONV, id, "createdon", "overriddencreatedon"); return new JsonObject { ["createdon_releido_es_2020"] = r.GetAttributeValue<DateTime>("createdon").Year == 2020, ["overriddencreatedon_presente"] = r.Contains("overriddencreatedon") }; }));
        esquema.Add(UpdLee("Update de createdby", ARR, a2, "createdby", new EntityReference("systemuser", UserId)));
        esquema.Add(UpdLee("Update de sanic_antiguedad (formula) = 5", CONV, ca, "sanic_antiguedad", 5m));
        esquema.Add(Intento("Create con sanic_antiguedad (formula) = 5", () => { var id = CrearIntento("conv-formula", ConvE("fo", e => e["sanic_antiguedad"] = 5m)); var r = Leer(CONV, id, "sanic_antiguedad"); return new JsonObject { ["sanic_antiguedad_presente"] = r.Contains("sanic_antiguedad"), ["es_el_enviado"] = r.Contains("sanic_antiguedad") && r.GetAttributeValue<decimal>("sanic_antiguedad") == 5m }; }));
        esquema.Add(UpdLee("Update de versionnumber = 5", CONV, ca, "versionnumber", 5L));
        esquema.Add(UpdLee("Update de owningbusinessunit (a la misma BU)", CONV, ca, "owningbusinessunit", new EntityReference("businessunit", Guid.Parse(Base["bu_id"]!.GetValue<string>()))));
        esquema.Add(Intento("Create de Cliente SIN sanic_nombre (primaria, requerida de negocio)", () => { var id = CrearIntento("cli-sin-nombre", new Entity(CLI) { ["sanic_nombrecompleto"] = "ZZF-sin-nombre", ["ownerid"] = Dueno() }); return new JsonObject { ["se_creo"] = true }; }));
        esquema.Add(Intento("Create de Arreglo SIN sanic_clienteid (requerido de negocio)", () => { var e = ArrE("sc", cli, G("ver-A")); e.Attributes.Remove("sanic_clienteid"); CrearIntento("arr-sin-cliente", e); return new JsonObject { ["se_creo"] = true }; }));
        esquema.Add(Intento("Create de Conversacion SIN sanic_huellamessageid (clave K-01 vacia) x2: la segunda tambien", () =>
        {
            var e1 = ConvE("sinhuella1"); e1["sanic_asunto"] = "ZZF-d-sinhuella1"; e1.Attributes.Remove("sanic_huellamessageid"); CrearIntento("conv-sin-huella-1", e1);
            var e2 = ConvE("sinhuella2"); e2["sanic_asunto"] = "ZZF-d-sinhuella2"; e2.Attributes.Remove("sanic_huellamessageid"); CrearIntento("conv-sin-huella-2", e2);
            return new JsonObject { ["se_crearon_las_dos"] = true };
        }));
        esquema.Add(Intento("Create de Conversacion con huella de 65 caracteres (T(64))", () => { CrearIntento("conv-huella-65", ConvE(Rep("h", 59))); return null; }));

        // ---- (14) lookup a una fila que NO existe
        var fantasma = Guid.NewGuid();
        inexistente.Add(Upd("Update de Conversacion.sanic_clienteid a un cliente inexistente", CONV, ca, "sanic_clienteid", new EntityReference(CLI, fantasma)));
        inexistente.Add(Intento("Create de Conversacion con sanic_clienteid a un cliente inexistente", () => { CrearIntento("conv-fantasma", ConvE("fa", e => e["sanic_clienteid"] = new EntityReference(CLI, fantasma))); return null; }));
        inexistente.Add(Upd("Update de Conversacion.sanic_oficialasignadoid a un systemuser inexistente", CONV, ca, "sanic_oficialasignadoid", new EntityReference("systemuser", fantasma)));
        inexistente.Add(Upd("Update de ownerid a un team inexistente", CONV, ca, "ownerid", new EntityReference("team", fantasma)));
        inexistente.Add(Upd("Update de ownerid a un systemuser inexistente", CONV, ca, "ownerid", new EntityReference("systemuser", fantasma)));
        inexistente.Add(Upd("Update de Conversacion.sanic_unidaaid (autorreferida) a una conversacion inexistente", CONV, ca, "sanic_unidaaid", new EntityReference(CONV, fantasma)));
        inexistente.Add(Upd("Update de lookup con Guid.Empty", CONV, ca, "sanic_clienteid", new EntityReference(CLI, Guid.Empty)));
        inexistente.Add(Intento("Update de una fila inexistente (Cliente) ", () => { Svc.Update(new Entity(CLI, fantasma) { ["sanic_celular"] = "ZZF-x" }); return null; }));
        inexistente.Add(Intento("Delete de una fila inexistente (Cliente)", () => { Svc.Delete(CLI, fantasma); return null; }));
        inexistente.Add(Intento("Retrieve de una tabla inexistente", () => { Svc.Retrieve("sanic_carp_tbl_noexiste", fantasma, new ColumnSet(true)); return null; }));
        inexistente.Add(Intento("Retrieve con columna inexistente en el ColumnSet", () => { Svc.Retrieve(CLI, cli, new ColumnSet("sanic_noexiste")); return null; }));
        inexistente.Add(Intento("RetrieveMultiple con columna inexistente en una condicion", () => { var q = new QueryExpression(CLI) { ColumnSet = new ColumnSet(Pk(CLI)) }; q.Criteria.AddCondition("sanic_noexiste", ConditionOperator.Null); Svc.RetrieveMultiple(q); return null; }));
        inexistente.Add(Intento("RetrieveMultiple con columna inexistente en un orden", () => { var q = new QueryExpression(CLI) { ColumnSet = new ColumnSet(Pk(CLI)) }; q.AddOrder("sanic_noexiste", OrderType.Ascending); Svc.RetrieveMultiple(q); return null; }));
        inexistente.Add(Intento("RetrieveMultiple de una tabla inexistente", () => { Svc.RetrieveMultiple(new QueryExpression("sanic_carp_tbl_noexiste") { ColumnSet = new ColumnSet(true) }); return null; }));

        Escribir("18-texto-y-esquema.json", new JsonObject
        {
            ["cabecera"] = Cabecera("18-texto-y-esquema", "SINTETICAS ZZF-",
                $"Puntos (12), (14), (d), (c), (i). Maximo real de sanic_celular leido de los metadatos: {max}.",
                "Cada rechazo lleva la falla completa: tipo exterior, ErrorCode decimal y hex, InnerFault, claves de ErrorDetails y cadena de excepciones (ver Serializador.Falla).",
                "Los textos con emoji y combinantes no se vuelcan: se informa su largo en unidades UTF-16, puntos de codigo, bytes y grafemas, y si se releyeron iguales."),
            ["contenido"] = new JsonObject
            {
                ["largo_maximo_sanic_celular"] = max,
                ["punto_12_largo_del_texto"] = texto, ["punto_12_rechazos_de_esquema"] = esquema, ["punto_14_lookup_a_fila_inexistente"] = inexistente,
                ["punto_d_cadena_vacia"] = vacio, ["punto_c_igualdad_de_texto"] = igualdad,
            },
        });
    }

    // ------------------------------------------------------------------ (10) cookie de paginacion
    private static JsonObject Pagina(string caso, string tabla, Action<QueryExpression> filtro, string orden, int pagina, string? cookie, int count, out EntityCollection? resultado)
    {
        EntityCollection? rr = null;
        var o = Intento(caso, () =>
        {
            var q = new QueryExpression(tabla) { ColumnSet = new ColumnSet(Pk(tabla)) };
            filtro(q);
            q.AddOrder(orden, OrderType.Ascending);
            q.PageInfo = new PagingInfo { Count = count, PageNumber = pagina, PagingCookie = cookie, ReturnTotalRecordCount = false };
            rr = Svc.RetrieveMultiple(q);
            return new JsonObject
            {
                ["pagina_pedida"] = pagina, ["n"] = rr.Entities.Count, ["claves"] = Arr(rr.Entities.Select(e => K(e.Id))), ["MoreRecords"] = rr.MoreRecords,
                ["cookie_vacia"] = string.IsNullOrEmpty(rr.PagingCookie), ["cookie_page_attr"] = System.Text.RegularExpressions.Regex.Match(rr.PagingCookie ?? "", "page=\"(\\d+)\"").Groups[1].Value,
            };
        });
        resultado = rr;
        return o;
    }

    private static void CookieDePaginacion()
    {
        var c = new JsonArray();
        Action<QueryExpression> todas = q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.BeginsWith, "ZZF-d-");
        Action<QueryExpression> otras = q => { q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.BeginsWith, "ZZF-d-C"); };   // otro filtro
        const string ord = "sanic_huellamessageid";
        c.Add(Pagina("p1 (Count=2, orden por huella)", CONV, todas, ord, 1, null, 2, out var r1));
        string? ck1 = r1?.PagingCookie;
        c.Add(Pagina("p2 con la cookie de p1 (correcto)", CONV, todas, ord, 2, ck1, 2, out var r2));
        string? ck2 = r2?.PagingCookie;
        c.Add(Pagina("p2 SIN cookie (paginacion simple)", CONV, todas, ord, 2, null, 2, out _));
        c.Add(Pagina("p3 con la cookie de p2 (correcto)", CONV, todas, ord, 3, ck2, 2, out _));
        c.Add(Pagina("p3 con la cookie de p1 (cookie de OTRA pagina: se salta la 2)", CONV, todas, ord, 3, ck1, 2, out _));
        c.Add(Pagina("p2 con la cookie de p2 (la de la MISMA pagina pedida)", CONV, todas, ord, 2, ck2, 2, out _));
        c.Add(Pagina("p1 con la cookie de p2 (cookie de una pagina posterior)", CONV, todas, ord, 1, ck2, 2, out _));
        // cookie de OTRA consulta (otro filtro y otro orden)
        c.Add(Pagina("p1 de OTRA consulta (filtro 'ZZF-d-C', orden por sanic_asunto)", CONV, otras, "sanic_asunto", 1, null, 2, out var ro));
        string? ckOtra = ro?.PagingCookie;
        c.Add(Pagina("p2 de la consulta principal con la cookie de OTRA consulta (otro filtro y otro orden)", CONV, todas, ord, 2, ckOtra, 2, out _));
        // cookie de OTRA tabla
        c.Add(Pagina("p1 de OTRA tabla (Cliente, orden por sanic_nombre)", CLI, q => q.Criteria.AddCondition("sanic_nombre", ConditionOperator.BeginsWith, "ZZF-"), "sanic_nombre", 1, null, 1, out var rc));
        string? ckTabla = rc?.PagingCookie;
        c.Add(Pagina("p2 de Conversacion con la cookie de OTRA TABLA (Cliente)", CONV, todas, ord, 2, ckTabla, 2, out _));
        // cookies malformadas
        c.Add(Pagina("p2 con cookie basura 'xyz'", CONV, todas, ord, 2, "xyz", 2, out _));
        c.Add(Pagina("p2 con cookie XML minima '<cookie page=\"9\"/>'", CONV, todas, ord, 2, "<cookie page=\"9\"/>", 2, out _));
        c.Add(Pagina("p2 con cookie vacia ''", CONV, todas, ord, 2, "", 2, out _));
        c.Add(Pagina("p2 con la cookie de p1 pero editada a page=\"1\" -> page=\"7\"", CONV, todas, ord, 2, ck1?.Replace("page=\"1\"", "page=\"7\""), 2, out _));
        // pagina pasada del final con cookie valida
        c.Add(Pagina("p40 (mas alla del final) sin cookie", CONV, todas, ord, 40, null, 2, out _));
        c.Add(Pagina("p40 con la cookie de p1", CONV, todas, ord, 40, ck1, 2, out _));
        Escribir("16-paginacion-cookie.json", new JsonObject
        {
            ["cabecera"] = Cabecera("16-paginacion-cookie", "SINTETICAS ZZF- (conversaciones ZZF-d-*, Count=2, orden por sanic_huellamessageid asc)",
                "Punto (10): PagingCookie de otra consulta, otra pagina, otra tabla o malformada. El limite de 50.000 filas de la paginacion simple NO SE MIDE (costo): NO MEDIDO.",
                "'claves' son las filas devueltas por clave del spike en el orden en que llegaron."),
            ["contenido"] = new JsonObject { ["casos"] = c },
        });
    }
}
