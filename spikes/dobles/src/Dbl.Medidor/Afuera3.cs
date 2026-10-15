using System.Text.Json.Nodes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

public static partial class Program
{
    private static JsonNode? Par(Entity e)
    {
        int? sc = e.Contains("statecode") ? e.GetAttributeValue<OptionSetValue>("statecode").Value : null;
        int? st = e.Contains("statuscode") ? e.GetAttributeValue<OptionSetValue>("statuscode").Value : null;
        return new JsonObject { ["statecode"] = sc, ["statuscode"] = st };
    }

    private static JsonNode? ParDe(string tabla, Guid id) => Par(Leer(tabla, id, "statecode", "statuscode"));

    private static OptionSetValue O(int v) => new(v);

    /// <summary>Update de estado y relectura. Informa el par ANTES, lo enviado y el par DESPUES, o la falla.</summary>
    private static JsonObject UpdateEstado(string caso, string tabla, string clave, int? statecode, int? statuscode)
    {
        return Intento(caso, () =>
        {
            var id = G(clave);
            var antes = ParDe(tabla, id);
            var e = new Entity(tabla, id);
            if (statecode != null) e["statecode"] = O(statecode.Value);
            if (statuscode != null) e["statuscode"] = O(statuscode.Value);
            Svc.Update(e);
            return new JsonObject { ["antes"] = antes, ["enviado"] = new JsonObject { ["statecode"] = statecode, ["statuscode"] = statuscode }, ["despues"] = ParDe(tabla, id) };
        });
    }

    private static JsonObject CreateEstado(string caso, string tabla, string clave, Func<Entity> fabrica)
    {
        return Intento(caso, () =>
        {
            var e = fabrica();
            var env = new JsonObject { ["statecode"] = e.Contains("statecode") ? e.GetAttributeValue<OptionSetValue>("statecode").Value : null, ["statuscode"] = e.Contains("statuscode") ? e.GetAttributeValue<OptionSetValue>("statuscode").Value : null };
            var id = CrearIntento(clave, e);
            return new JsonObject { ["enviado"] = env, ["despues"] = ParDe(tabla, id) };
        });
    }

    // ------------------------------------------------------------------ (6)
    private static void Estados()
    {
        var conv = new JsonArray(); var arr = new JsonArray();
        var cli = G("cli-A"); var ver = G("ver-A");

        // ---- Conversacion (pares de 0.7: 0/1 Abierta, 1/2 Finalizada, 1/159460001 Unida). Cada paso informa el par ANTES; los pasos se ordenan para que la precondicion sea la que dice el titulo.
        Crear("conv-S1", ConvE("S1"));
        conv.Add(UpdateEstado("S1 en 0/1: Update con SOLO statuscode=2 (Finalizada, motivo de otro estado)", CONV, "conv-S1", null, 2));
        conv.Add(UpdateEstado("S1 en 0/1: Update con statecode=0 + statuscode=2 (par invalido)", CONV, "conv-S1", 0, 2));
        conv.Add(UpdateEstado("S1 en 0/1: Update con SOLO statecode=0 (igual al actual, motivo por defecto)", CONV, "conv-S1", 0, null));
        conv.Add(UpdateEstado("S1 en 0/1: Update con SOLO statecode=1 (cambio de estado, sin motivo)", CONV, "conv-S1", 1, null));
        conv.Add(UpdateEstado("S1 en 1/2: Update con SOLO statecode=1 (igual al actual, motivo por defecto)", CONV, "conv-S1", 1, null));
        conv.Add(UpdateEstado("S1 en 1/2: Update con SOLO statuscode=1 (Abierta, motivo de otro estado)", CONV, "conv-S1", null, 1));
        conv.Add(UpdateEstado("S1 en 1/2: Update con SOLO statecode=0 (cambio de estado, sin motivo)", CONV, "conv-S1", 0, null));
        Crear("conv-S2", ConvE("S2"));
        conv.Add(UpdateEstado("S2 en 0/1: Update con SOLO statuscode=159460001 (Unida, motivo de otro estado)", CONV, "conv-S2", null, 159460001));
        conv.Add(UpdateEstado("S2 en 0/1: Update con statecode=1 + statuscode=159460001 (par valido: va a Unida)", CONV, "conv-S2", 1, 159460001));
        conv.Add(UpdateEstado("S2 en 1/159460001 (motivo NO por defecto): Update con SOLO statecode=1 (igual al actual)", CONV, "conv-S2", 1, null));
        conv.Add(UpdateEstado("S2 de nuevo a 1/159460001: Update con statecode=1 + statuscode=159460001", CONV, "conv-S2", 1, 159460001));
        conv.Add(UpdateEstado("S2 en 1/159460001: Update con statecode=1 + statuscode=159460001 (el MISMO par)", CONV, "conv-S2", 1, 159460001));
        conv.Add(UpdateEstado("S2 en 1/159460001: Update con SOLO statuscode=2 (mismo estado, otro motivo)", CONV, "conv-S2", null, 2));
        conv.Add(UpdateEstado("S2 en 1/2: Update con SOLO statuscode=159460001 (mismo estado, otro motivo)", CONV, "conv-S2", null, 159460001));
        conv.Add(UpdateEstado("S2 en 1/159460001: Update con SOLO statecode=0 (cambio de estado, sin motivo)", CONV, "conv-S2", 0, null));
        conv.Add(CreateEstado("Create con statecode=1 SOLO (Inactivo)", CONV, "conv-S3", () => ConvE("S3", e => e["statecode"] = O(1))));
        conv.Add(CreateEstado("Create con statecode=1 + statuscode=2", CONV, "conv-S4", () => ConvE("S4", e => { e["statecode"] = O(1); e["statuscode"] = O(2); })));
        conv.Add(CreateEstado("Create con statecode=1 + statuscode=159460001", CONV, "conv-S5", () => ConvE("S5", e => { e["statecode"] = O(1); e["statuscode"] = O(159460001); })));
        conv.Add(CreateEstado("Create con statecode=0 SOLO (explicito)", CONV, "conv-S6", () => ConvE("S6", e => e["statecode"] = O(0))));
        conv.Add(CreateEstado("Create con statecode=0 + statuscode=1 (explicitos)", CONV, "conv-S6b", () => ConvE("S6b", e => { e["statecode"] = O(0); e["statuscode"] = O(1); })));
        conv.Add(CreateEstado("Create con SOLO statuscode=2 (sin statecode)", CONV, "conv-S7", () => ConvE("S7", e => e["statuscode"] = O(2))));
        conv.Add(CreateEstado("Create con SOLO statuscode=159460001 (sin statecode)", CONV, "conv-S7b", () => ConvE("S7b", e => e["statuscode"] = O(159460001))));
        conv.Add(CreateEstado("Create con statecode=0 + statuscode=2 (par invalido)", CONV, "conv-S8", () => ConvE("S8", e => { e["statecode"] = O(0); e["statuscode"] = O(2); })));
        conv.Add(CreateEstado("Create con statecode=7 (inexistente)", CONV, "conv-S9", () => ConvE("S9", e => e["statecode"] = O(7))));
        conv.Add(CreateEstado("Create con statuscode=999 (inexistente)", CONV, "conv-S10", () => ConvE("S10", e => e["statuscode"] = O(999))));

        // ---- Arreglo (0/1 Borrador, 0/159460001 Vigente, 1/2 Terminado, 1/159460002 Anulado, 1/159460003 Descartado)
        Crear("arr-R1", ArrE("R1", cli, ver));
        arr.Add(UpdateEstado("R1 en 0/1: Update con SOLO statuscode=159460001 (Vigente, mismo estado, otro motivo)", ARR, "arr-R1", null, 159460001));
        arr.Add(UpdateEstado("R1 en 0/159460001 (motivo NO por defecto): Update con SOLO statecode=0 (igual al actual)", ARR, "arr-R1", 0, null));
        arr.Add(UpdateEstado("R1 de nuevo a 0/159460001: Update con SOLO statuscode=159460001", ARR, "arr-R1", null, 159460001));
        arr.Add(UpdateEstado("R1 en 0/159460001: Update con SOLO statuscode=2 (Terminado, motivo de otro estado)", ARR, "arr-R1", null, 2));
        arr.Add(UpdateEstado("R1 en 0/159460001: Update con statecode=0 + statuscode=159460002 (par invalido: Anulado es de Inactivo)", ARR, "arr-R1", 0, 159460002));
        arr.Add(UpdateEstado("R1 en 0/159460001: Update con SOLO statecode=1 (cambio de estado, sin motivo)", ARR, "arr-R1", 1, null));
        arr.Add(UpdateEstado("R1 en 1/2: Update con SOLO statuscode=159460003 (Descartado, mismo estado)", ARR, "arr-R1", null, 159460003));
        arr.Add(UpdateEstado("R1 en 1/159460003 (motivo NO por defecto): Update con SOLO statecode=1 (igual al actual)", ARR, "arr-R1", 1, null));
        arr.Add(UpdateEstado("R1 de nuevo a 1/159460002: Update con statecode=1 + statuscode=159460002 (Anulado)", ARR, "arr-R1", 1, 159460002));
        arr.Add(UpdateEstado("R1 en 1/159460002: Update con SOLO statuscode=1 (Borrador, motivo de otro estado)", ARR, "arr-R1", null, 1));
        arr.Add(UpdateEstado("R1 en 1/159460002: Update con SOLO statecode=0 (V-5: de Inactivo a Activo, motivo no por defecto de origen)", ARR, "arr-R1", 0, null));
        arr.Add(CreateEstado("Create con statecode=1 SOLO", ARR, "arr-R2", () => ArrE("R2", cli, ver, e => e["statecode"] = O(1))));
        arr.Add(CreateEstado("Create con statecode=1 + statuscode=159460003", ARR, "arr-R3", () => ArrE("R3", cli, ver, e => { e["statecode"] = O(1); e["statuscode"] = O(159460003); })));
        arr.Add(CreateEstado("Create con statecode=0 + statuscode=159460001 (Vigente)", ARR, "arr-R4", () => ArrE("R4", cli, ver, e => { e["statecode"] = O(0); e["statuscode"] = O(159460001); })));
        arr.Add(CreateEstado("Create con SOLO statuscode=159460001 (sin statecode)", ARR, "arr-R5", () => ArrE("R5", cli, ver, e => e["statuscode"] = O(159460001))));
        arr.Add(CreateEstado("Create con SOLO statuscode=2 (Terminado, sin statecode)", ARR, "arr-R6", () => ArrE("R6", cli, ver, e => e["statuscode"] = O(2))));
        if (PorClave.ContainsKey("arr-R4"))
            arr.Add(UpdateEstado("R4 (nacida en 0/159460001): Update con SOLO statecode=0 (igual al actual)", ARR, "arr-R4", 0, null));

        Escribir("13-estados.json", new JsonObject
        {
            ["cabecera"] = Cabecera("13-estados", "SINTETICAS ZZF- (Conversaciones S1..S10 y Arreglos R1..R5)",
                "Punto (6): pares de 0.7 del diccionario (§3.2). Cada caso: par antes, lo enviado, par despues (releido) o la falla completa.",
                "Conversacion: 0/1 Abierta, 1/2 Finalizada, 1/159460001 Unida. Arreglo: 0/1 Borrador, 0/159460001 Vigente, 1/2 Terminado, 1/159460002 Anulado, 1/159460003 Descartado.",
                "Ningun step del proyecto esta registrado: lo que se ve es la plataforma sola."),
            ["contenido"] = new JsonObject { ["conversacion"] = conv, ["arreglo"] = arr },
        });
    }

    // ------------------------------------------------------------------ (7)(8) email
    private static Entity EmailE(string sufijo, bool saliente, Action<Entity>? mas = null)
    {
        var e = new Entity("email")
        {
            ["subject"] = "ZZF-d-email " + sufijo, ["description"] = "ZZF-cuerpo " + sufijo, ["directioncode"] = saliente,
            ["messageid"] = $"<zzf-d-{sufijo}@example.com>", ["actualend"] = Ahora(), ["ownerid"] = Ref("systemuser", UserId),
        };
        mas?.Invoke(e);
        return e;
    }

    private static JsonNode? EmailLeer(Guid id)
    {
        var r = Leer("email", id, "statecode", "statuscode", "senton", "modifiedon", "inreplyto", "directioncode", "actualend", "createdon");
        var o = new JsonObject { ["statecode"] = r.GetAttributeValue<OptionSetValue>("statecode")?.Value, ["statuscode"] = r.GetAttributeValue<OptionSetValue>("statuscode")?.Value };
        o["senton"] = r.Contains("senton") ? Dt(r.GetAttributeValue<DateTime>("senton")) : null;
        o["modifiedon"] = r.Contains("modifiedon") ? Dt(r.GetAttributeValue<DateTime>("modifiedon")) : null;
        o["actualend"] = r.Contains("actualend") ? Dt(r.GetAttributeValue<DateTime>("actualend")) : null;
        o["inreplyto_presente"] = r.Contains("inreplyto");
        o["inreplyto_igual_al_enviado"] = r.Contains("inreplyto") ? (r.GetAttributeValue<string>("inreplyto") ?? "").StartsWith("<zzf-d-padre") : null;
        o["directioncode"] = r.Contains("directioncode") ? r.GetAttributeValue<bool>("directioncode") : null;
        return o;
    }

    private static void Emails()
    {
        var def = new JsonArray(); var pares = new JsonArray(); var s8 = new JsonArray();

        // ---- (7a) statuscode por defecto de cada statecode: Create con SOLO statecode
        def.Add(Intento("Create sin estado", () => { var id = CrearIntento("email-E0", EmailE("E0", false)); return EmailLeer(id); }));
        foreach (var sc in new[] { 0, 1, 2 })
            def.Add(Intento($"Create con SOLO statecode={sc}", () => { var id = CrearIntento("email-C" + sc, EmailE("C" + sc, false, e => e["statecode"] = O(sc))); return EmailLeer(id); }));

        // ---- (7b) statuscode por defecto: Update con SOLO statecode (una cadena sobre un mismo correo, de 0 a 1 a 2 y de vuelta)
        Crear("email-E1", EmailE("E1", false));
        foreach (var sc in new[] { 0, 1, 2, 0, 2, 1 })
            def.Add(UpdateEstado($"E1: Update con SOLO statecode={sc}", "email", "email-E1", sc, null));

        // ---- (7c) pares validos. Estados de email (Learn): statecode 0 Open, 1 Completed, 2 Canceled; statuscode 1 Draft, 2 Completed, 3 Sent, 4 Received, 5 Canceled, 6 Pending Send, 7 Sending, 8 Failed.
        // NO se prueban 6 (Pending Send) ni 7 (Sending): son los estados que toma el correo cuando se ENVIA; aunque no hay partes (no hay a quien enviar), la regla del spike es "NUNCA enviarlo".
        foreach (var sc in new[] { 0, 1, 2 })
            foreach (var st in new[] { 1, 2, 3, 4, 5, 8 })
            {
                var clave = $"email-P{sc}{st}";
                Crear(clave, EmailE($"P{sc}{st}", false));   // cada par sobre un correo recien nacido en 0/1 (sin arrastre del caso anterior)
                pares.Add(UpdateEstado($"par ({sc},{st}) sobre un correo nacido en (0,1)", "email", clave, sc, st));
            }

        // ---- (8) senton, inreplyto
        var inrep = "<zzf-d-padre@example.com>";
        // Create con inreplyto: saliente y entrante, con y sin estado en el Create
        s8.Add(Intento("Create entrante con inreplyto (nace 0/1)", () => { var id = CrearIntento("email-I1", EmailE("I1", false, e => e["inreplyto"] = inrep)); return EmailLeer(id); }));
        s8.Add(Intento("Create saliente con inreplyto (nace 0/1)", () => { var id = CrearIntento("email-I2", EmailE("I2", true, e => e["inreplyto"] = inrep)); return EmailLeer(id); }));
        s8.Add(Intento("Create entrante con inreplyto SIN corchetes angulares ('zzf-d-padre-plano')", () => { var id = CrearIntento("email-I3", EmailE("I3", false, e => e["inreplyto"] = "zzf-d-padre-plano")); return EmailLeer(id); }));
        s8.Add(Intento("Create entrante con inreplyto, directioncode=false, SIN messageid", () => { var id = CrearIntento("email-I4", EmailE("I4", false, e => { e["inreplyto"] = inrep; e.Attributes.Remove("messageid"); })); return EmailLeer(id); }));
        // Update posterior: inreplyto conservado?
        s8.Add(Intento("I1: Update posterior de inreplyto (correo abierto 0/1)", () => { Svc.Update(new Entity("email", G("email-I1")) { ["inreplyto"] = inrep }); return EmailLeer(G("email-I1")); }));
        s8.Add(Intento("I1: Update de inreplyto junto con el cambio a Received (1/4)", () => { Svc.Update(new Entity("email", G("email-I1")) { ["inreplyto"] = inrep, ["statecode"] = O(1), ["statuscode"] = O(4) }); return EmailLeer(G("email-I1")); }));
        s8.Add(Intento("I1 (ya 1/4 completado): Update posterior de inreplyto", () => { Svc.Update(new Entity("email", G("email-I1")) { ["inreplyto"] = inrep }); return EmailLeer(G("email-I1")); }));
        s8.Add(Intento("I2 (saliente, abierto): Update de inreplyto", () => { Svc.Update(new Entity("email", G("email-I2")) { ["inreplyto"] = inrep }); return EmailLeer(G("email-I2")); }));
        s8.Add(Intento("I2: Update de inreplyto a null y luego a otro valor (el valor no se guarda; nada que borrar)", () => { Svc.Update(new Entity("email", G("email-I2")) { ["inreplyto"] = null }); Svc.Update(new Entity("email", G("email-I2")) { ["inreplyto"] = "<zzf-d-otro@example.com>" }); return EmailLeer(G("email-I2")); }));
        // senton al pasar a Sent por Update
        s8.Add(Intento("I2: Update a Sent (1/3); senton vs modifiedon vs reloj del cliente", () =>
        {
            var antes = DateTime.UtcNow;
            Svc.Update(new Entity("email", G("email-I2")) { ["statecode"] = O(1), ["statuscode"] = O(3) });
            var despues = DateTime.UtcNow;
            var r = Leer("email", G("email-I2"), "senton", "modifiedon", "actualend");
            var o = new JsonObject { ["relectura"] = EmailLeer(G("email-I2")), ["cliente_antes_utc"] = Dt(antes), ["cliente_despues_utc"] = Dt(despues) };
            if (r.Contains("senton") && r.Contains("modifiedon"))
            {
                var s = r.GetAttributeValue<DateTime>("senton"); var m = r.GetAttributeValue<DateTime>("modifiedon");
                o["senton_menos_modifiedon_ms"] = (s - m).TotalMilliseconds;
                o["senton_dentro_de_la_ventana_del_cliente"] = s >= antes.AddSeconds(-1) && s <= despues.AddSeconds(1);
                o["senton_menos_inicio_del_cliente_ms"] = (s - antes).TotalMilliseconds;
            }
            return o;
        }));
        Crear("email-I5", EmailE("I5", true));
        s8.Add(Intento("I5: Update a Sent (1/3) con senton enviado a mano (2020-01-01)", () =>
        {
            Svc.Update(new Entity("email", G("email-I5")) { ["statecode"] = O(1), ["statuscode"] = O(3), ["senton"] = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
            return EmailLeer(G("email-I5"));
        }));
        Crear("email-I6", EmailE("I6", false));
        s8.Add(Intento("I6: Update a Received (1/4); senton aparece?", () => { Svc.Update(new Entity("email", G("email-I6")) { ["statecode"] = O(1), ["statuscode"] = O(4) }); return EmailLeer(G("email-I6")); }));

        Escribir("14-email.json", new JsonObject
        {
            ["cabecera"] = Cabecera("14-email", "SINTETICAS ZZF- (correos sin partes from/to: ninguno tiene destinatarios, NINGUNO se envio, no se uso SendEmail)",
                "Puntos (7) y (8). Sin ninguna direccion en el repo ni en el correo: los correos no llevan activityparty.",
                "NO MEDIDO por riesgo de envio: statuscode 6 (Pending Send) y 7 (Sending), los estados que toma un correo al enviarse.",
                "senton se compara con modifiedon (mismo instante del servidor?) y con el reloj del cliente antes y despues del Update."),
            ["contenido"] = new JsonObject { ["punto_7_statuscode_por_defecto"] = def, ["punto_7_pares"] = pares, ["punto_8_senton_e_inreplyto"] = s8 },
        });
    }
}
