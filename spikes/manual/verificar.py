#!/usr/bin/env python3
"""Verifica las tres pruebas manuales del Spike 4 DESPUES de que el usuario las haga. Solo lee (Dataverse y Graph); no crea ni borra nada.
Uso:  python3 verificar.py            (veredicto por prueba)       python3 verificar.py --json   (ademas guarda evidencia/verificar_manual.json)
Veredictos: CONFIRMADO / REFUTADO / PENDIENTE (aun no hay datos para decidir) / INCONCLUSO (hay datos pero no alcanzan).
  Prueba 1: la respuesta enviada desde la interfaz, ¿tiene parentactivityid = correo respondido y llego al buzon 2 con In-Reply-To?
  Prueba 2: la imagen pegada, ¿donde quedo (msdyn_richtextfile / adjunto / Base64 en el cuerpo) y se relaciona con su Email?
  Prueba 3: la respuesta del cliente con el asunto cambiado, ¿quedo con Regarding a la conversacion?
"""
import re
import sys
import urllib.parse
from lib import *

dv = DvAdmin(); est = cargar(); D = est.get("D")
if os.environ.get("VERIFICAR_AUTOPRUEBA_CON_B"):   # autoprueba: usa los datos de la tarea B (hay respuestas enviadas, sin imagen ni respuesta del cliente)
    B = est["B"]; D = {"email_id": B["entrante"]["dv_email_id"], "conversacion": B["conversacion"], "internetMessageId": B["entrante"]["internetMessageId"], "t_envio_utc": B["entrante"]["t_envio_utc"]}
if not D:
    sys.exit("Falta la preparacion (python3 s40_d_preparar.py).")
g = Graph4(); b1, b2 = g.buzon[1], g.buzon[2]
GUARDAR = "--json" in sys.argv
EVI = {}
COLS = ("activityid,subject,statuscode,statecode,directioncode,_parentactivityid_value,_regardingobjectid_value,messageid,inreplyto,sender,torecipients,"
        "createdon,senton,actualend,_ownerid_value,_createdby_value,correlationmethod,correlatedsubjectchanged,attachmentcount,description")
E0 = D["email_id"]; CONV = D["conversacion"]; T0 = D["t_envio_utc"]

def emails_regarding():
    s, b, _ = dv.call("GET", f"emails?$select={COLS}&$filter=_regardingobjectid_value eq {CONV}&$orderby=createdon asc", cabeceras=PREFER)
    assert s == 200, (s, b)
    return b["value"]

def norm(x): return (x or "").strip().lower()
def etq(e, k): return e.get(f"{k}@OData.Community.Display.V1.FormattedValue")

def graph_msgs(box, carpeta):
    s, v = g.mensajes(box, top=40, carpeta=carpeta)
    return v if s == 200 else []

def cuerpo_graph(box, gid):
    s, m = g.call("GET", f"/users/{box}/messages/{gid}?$select=body,hasAttachments,subject")
    return m if s == 200 else {}

def adjuntos_graph(box, gid):
    s, m = g.call("GET", f"/users/{box}/messages/{gid}/attachments?$select=name,contentType,isInline,size,contentId")
    return m.get("value", []) if s == 200 else []

filas = emails_regarding()
salientes = [e for e in filas if e["directioncode"] is True and e["createdon"] >= T0]
entrantes_posteriores = [e for e in filas if e["directioncode"] is False and e["activityid"] != E0 and e["createdon"] >= T0]
enviadas = [e for e in salientes if etq(e, "statuscode") in ("Sent", "Pending Send", "Sending") or e["statuscode"] in (4, 6)]
print(f"Conversacion 'Prueba manual SPIKE4' ({CONV}); correo original {E0}")
print(f"Correos con Regarding a la conversacion: {len(filas)} ({len(salientes)} salientes, {len(entrantes_posteriores)} entrantes ademas del original)\n")

# ---------------------------------------------------------------- PRUEBA 1
print("=" * 78); print("PRUEBA 1  Responder desde la interfaz: parentactivityid y llegada con In-Reply-To"); print("-" * 78)
EVI["prueba1"] = {"respuestas": []}
enviadas_ok = [e for e in salientes if e["statuscode"] == 4 or etq(e, "statuscode") == "Sent"]
if not salientes:
    v1 = "PENDIENTE"; print("No hay ninguna respuesta saliente con Regarding a la conversacion. Hacer la Prueba 1 (o no se pudo guardar/enviar).")
else:
    veredictos = []
    for e in salientes:
        # llegada al buzon 2 (por Message-ID; si aun no esta enviado, no hay messageid utilizable)
        llego = None
        for m in graph_msgs(b2, "inbox"):
            if norm(m["internetMessageId"]) == norm(e.get("messageid")) and e.get("messageid"):
                llego = m; break
        r = {"email": e["activityid"], "asunto": e["subject"], "estado": etq(e, "statuscode") or e["statuscode"], "remitente(sender)": e.get("sender"),
             "para": e.get("torecipients"), "propietario": etq(e, "_ownerid_value"), "creado": e["createdon"], "enviado": e.get("senton"),
             "parentactivityid": e.get("_parentactivityid_value"), "parent_es_el_original": e.get("_parentactivityid_value") == E0,
             "inreplyto_en_dataverse": e.get("inreplyto"), "inreplyto_coincide_con_original": norm(e.get("inreplyto")) == norm(D["internetMessageId"])}
        if llego:
            irt = encabezado(llego, "In-Reply-To")
            r.update({"llego_al_buzon2": True, "from": llego["from"]["emailAddress"]["address"], "in_reply_to_graph": irt,
                      "in_reply_to_coincide": norm(irt) == norm(D["internetMessageId"]), "llegada_utc": llego["receivedDateTime"]})
        else:
            r["llego_al_buzon2"] = False
        EVI["prueba1"]["respuestas"].append(r)
        print(json.dumps(r, ensure_ascii=False, indent=1))
        if r["estado"] != "Sent":
            veredictos.append("PENDIENTE"); print("  -> aun no esta enviada (estado:", r["estado"], ")\n"); continue
        if not r["parent_es_el_original"]:
            veredictos.append("REFUTADO"); print("  -> REFUTADO: la respuesta NO tiene parentactivityid = el correo original (plan B: plugin pre-create que lo fije)\n")
        elif not r["llego_al_buzon2"]:
            veredictos.append("PENDIENTE"); print("  -> parentactivityid correcto; todavia no se ve en el buzon 2 (reintentar en 1 minuto)\n")
        elif not r.get("in_reply_to_coincide"):
            veredictos.append("REFUTADO"); print("  -> llego pero SIN In-Reply-To correcto: el hilo se rompe\n")
        else:
            veredictos.append("CONFIRMADO"); print("  -> CONFIRMADO: parentactivityid = original y llego al buzon 2 con In-Reply-To del original\n")
    v1 = ("CONFIRMADO" if veredictos and all(v == "CONFIRMADO" for v in veredictos) else "REFUTADO" if "REFUTADO" in veredictos else "PENDIENTE")
    if v1 == "CONFIRMADO" and len(veredictos) > 1: v1 = f"CONFIRMADO ({len(veredictos)} respuestas, todas con parentactivityid)"
EVI["prueba1"]["veredicto"] = v1

# ---------------------------------------------------------------- PRUEBA 2
print("=" * 78); print("PRUEBA 2  Imagen pegada en la respuesta: donde quedo y como se relaciona con su Email"); print("-" * 78)
EVI["prueba2"] = {"hallazgos": []}
candidatos = salientes
if not candidatos:
    v2 = "PENDIENTE"; print("No hay respuesta saliente (depende de la Prueba 1).")
else:
    # tabla msdyn_richtextfiles (todas las filas creadas desde el envio del original, para no depender de que el vinculo por texto funcione)
    rt = []
    for sel in ("msdyn_richtextfileid,msdyn_name,msdyn_parententityname,msdyn_parentid,msdyn_parententity_fieldname,createdon,_ownerid_value",):
        s, b, _ = dv.call("GET", f"msdyn_richtextfiles?$select={sel}&$filter=createdon ge {T0}&$orderby=createdon asc", cabeceras=PREFER)
        if s == 200: rt = b["value"]
        else: print("  (no se pudo leer msdyn_richtextfiles:", s, str(b)[:200], ")")
    EVI["prueba2"]["richtextfiles_desde_el_envio"] = rt
    veredictos2 = []
    for e in candidatos:
        cuerpo = e.get("description") or ""
        imgs = re.findall(r"<img\b[^>]*>", cuerpo, re.I)
        srcs = [re.search(r'src="([^"]*)"', i, re.I).group(1) if re.search(r'src="([^"]*)"', i, re.I) else "" for i in imgs]
        base64_en_cuerpo = [x[:40] + "..." for x in srcs if x.lower().startswith("data:image")]
        cid = [x for x in srcs if x.lower().startswith("cid:")]
        urls_rt = [x for x in srcs if "msdyn_richtextfile" in x.lower()]
        ids_en_urls = [m.group(1) for x in urls_rt for m in [re.search(r"msdyn_richtextfiles\(([0-9a-f-]{36})\)", x, re.I)] if m]
        s, ad, _ = dv.call("GET", f"activitymimeattachments?$select=activitymimeattachmentid,filename,mimetype,attachmentcontentid,_objectid_value&$filter=_objectid_value eq {e['activityid']}")
        adjuntos = ad.get("value", []) if s == 200 else []
        eid = norm(e["activityid"])
        rt_del_email = [r for r in rt if eid in norm(r.get("msdyn_parentid")).replace("{", "").replace("}", "") and norm(r.get("msdyn_parententityname")) in ("email", "")]
        rt_en_urls = [r for r in rt if r["msdyn_richtextfileid"] in ids_en_urls]
        h = {"email": e["activityid"], "asunto": e["subject"], "etiquetas_img_en_cuerpo": len(imgs), "src_de_las_imagenes": [x if len(x) < 160 else x[:160] + "..." for x in srcs],
             "base64_en_cuerpo": base64_en_cuerpo, "cid": cid, "urls_a_richtextfile": urls_rt, "ids_de_richtextfile_en_el_cuerpo": ids_en_urls,
             "richtextfile_vinculadas_por_msdyn_parentid": [{k: r.get(k) for k in ("msdyn_richtextfileid", "msdyn_name", "msdyn_parententityname", "msdyn_parentid", "msdyn_parententity_fieldname", "createdon")} for r in rt_del_email],
             "richtextfile_referenciadas_desde_el_cuerpo": [r["msdyn_richtextfileid"] for r in rt_en_urls],
             "adjuntos_activitymimeattachment": [{k: a.get(k) for k in ("filename", "mimetype", "attachmentcontentid")} for a in adjuntos]}
        # lo que recibe el cliente (buzon 2)
        llego = next((m for m in graph_msgs(b2, "inbox") if e.get("messageid") and norm(m["internetMessageId"]) == norm(e["messageid"])), None)
        if llego:
            cg = cuerpo_graph(b2, llego["id"]); html = (cg.get("body") or {}).get("content", "")
            srcs_g = [re.search(r'src="([^"]*)"', i, re.I).group(1) for i in re.findall(r"<img\b[^>]*>", html, re.I) if re.search(r'src="([^"]*)"', i, re.I)]
            h["lo_que_recibe_el_cliente"] = {"imagenes_en_cuerpo": len(srcs_g), "src": [x if len(x) < 160 else x[:160] + "..." for x in srcs_g],
                                              "adjuntos": [{k: a.get(k) for k in ("name", "contentType", "isInline", "size")} for a in adjuntos_graph(b2, llego["id"])]}
        EVI["prueba2"]["hallazgos"].append(h); print(json.dumps(h, ensure_ascii=False, indent=1))
        if not imgs and not adjuntos and not rt_del_email:
            veredictos2.append("SIN_IMAGEN"); print("  -> esta respuesta no tiene ninguna imagen (ni en el cuerpo, ni adjunta, ni en msdyn_richtextfile)\n"); continue
        donde = []
        if rt_del_email or rt_en_urls: donde.append("msdyn_richtextfile")
        if adjuntos: donde.append("activitymimeattachment (adjunto del Email)")
        if base64_en_cuerpo: donde.append("Base64 dentro del cuerpo")
        if cid and not adjuntos: donde.append("cid sin adjunto (inconsistente)")
        rel = ("por msdyn_parentid (texto)" if rt_del_email else "solo por la URL del cuerpo" if rt_en_urls else "adjunto con objectid = Email" if adjuntos else "n/a")
        print(f"  -> DONDE QUEDO: {', '.join(donde) or 'no identificado'} | RELACION con el Email: {rel}\n")
        veredictos2.append("; ".join(donde) + f" [relacion: {rel}]" if donde else "INCONCLUSO")
    con_imagen = [v for v in veredictos2 if v != "SIN_IMAGEN"]
    v2 = ("CONFIRMADO: " + " | ".join(con_imagen)) if con_imagen else ("PENDIENTE (hay respuesta pero sin imagen: ¿se pego la imagen antes de enviar?)" if veredictos2 else "PENDIENTE")
EVI["prueba2"]["veredicto"] = v2

# ---------------------------------------------------------------- PRUEBA 3
print("=" * 78); print("PRUEBA 3  Respuesta del cliente (buzon 2) con el asunto cambiado: ¿queda con Regarding a la conversacion?"); print("-" * 78)
EVI["prueba3"] = {}
v3 = "PENDIENTE"
mi_respuesta = next((e for e in reversed(enviadas_ok) if e.get("messageid")), None)
enviados_b2 = [m for m in graph_msgs(b2, "sentitems") if m["sentDateTime"] >= T0]
cand = []
for m in enviados_b2:
    if norm(m["internetMessageId"]) == norm(D["internetMessageId"]): continue   # es el original [SPIKE4] Prueba manual
    irt = encabezado(m, "In-Reply-To")
    es_resp = bool(mi_respuesta and norm(irt) == norm(mi_respuesta.get("messageid")))
    if es_resp or any(norm(irt) == norm(e.get("messageid")) for e in salientes if e.get("messageid")):
        cand.append((m, irt, True))
if not cand:
    print("El buzon 2 no envio ninguna respuesta a tu respuesta (o la Prueba 3 se omitio, o todavia no se envio).")
    otros = [m["subject"] for m in enviados_b2 if norm(m["internetMessageId"]) != norm(D["internetMessageId"])]
    if otros: print("  (mensajes enviados por el buzon 2 desde la prueba, sin In-Reply-To a tu respuesta:", otros[:5], ")")
else:
    for m, irt, _ in cand:
        asunto_resp = m["subject"]
        cambio = norm(asunto_resp) != norm(mi_respuesta["subject"]) and not norm(asunto_resp).endswith(norm(mi_respuesta["subject"]).replace("re: ", ""))
        s, b, _ = dv.call("GET", f"emails?$select={COLS}&$filter=createdon ge {T0}&$orderby=createdon asc", cabeceras=PREFER)
        en_dv = [x for x in b.get("value", []) if norm(x.get("messageid")) == norm(m["internetMessageId"])]
        r = {"asunto_del_cliente": asunto_resp, "asunto_de_tu_respuesta": mi_respuesta["subject"] if mi_respuesta else None, "asunto_cambiado": cambio,
             "in_reply_to_enviado_por_el_cliente": irt, "emails_en_dataverse_con_ese_message_id": []}
        for x in en_dv:
            r["emails_en_dataverse_con_ese_message_id"].append({"email": x["activityid"], "creado": x["createdon"], "regarding": etq(x, "_regardingobjectid_value") or x.get("_regardingobjectid_value"),
                                                                "regarding_es_la_conversacion": x.get("_regardingobjectid_value") == CONV, "correlationmethod": etq(x, "correlationmethod") or x.get("correlationmethod"),
                                                                "correlatedsubjectchanged": x.get("correlatedsubjectchanged"), "parentactivityid": x.get("_parentactivityid_value"), "inreplyto": x.get("inreplyto")})
        EVI["prueba3"].setdefault("respuestas", []).append(r); print(json.dumps(r, ensure_ascii=False, indent=1))
        if not en_dv:
            print("  -> el correo del cliente todavia no aparece en Dataverse (esperar 1 minuto y reintentar)\n"); continue
        ok = any(x["regarding_es_la_conversacion"] for x in r["emails_en_dataverse_con_ese_message_id"])
        if not cambio:
            v3 = "INCONCLUSO (el asunto NO cambio; repetir la respuesta cambiando el asunto)"
        elif ok:
            v3 = "CONFIRMADO (con el asunto cambiado, el correo quedo con Regarding a la conversacion)"
        else:
            v3 = "REFUTADO (con el asunto cambiado el correo NO quedo con Regarding a la conversacion)"
        print("  ->", v3, "\n")
EVI["prueba3"]["veredicto"] = v3

print("=" * 78); print("VEREDICTOS"); print("=" * 78)
print("Prueba 1 (Responder: parentactivityid + In-Reply-To):", v1)
print("Prueba 2 (imagen pegada: donde queda):               ", v2)
print("Prueba 3 (respuesta del cliente con otro asunto):    ", v3)
if GUARDAR:
    volcar("verificar_manual.json", EVI); print("\nEvidencia guardada en evidencia/verificar_manual.json")
