"""Lote 0 (2026-10-20), inventario 13.4 y 13.5: queue `Arreglos de Pago` sobre el buzón de prueba PP_MAIL_BUZON_1,
con sincronización del lado del servidor. Idempotente: si la queue ya existe la verifica y no la duplica.
NUNCA imprime secretos, tokens ni la dirección del buzón (en la evidencia va el NOMBRE de la variable).
No envía correos ni borra nada. Uso:  python3 l01_cola_buzon.py [--solo-verificar]"""
import json
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "..", "..", "herramientas"))
from credenciales import leer_secretos  # noqa: E402
from dataverse_api import Dataverse  # noqa: E402

LOTE = os.path.dirname(AQUI)
EVID = os.path.join(LOTE, "evidencia")
ESTADO = os.path.join(LOTE, "estado.json")
NOMBRE_QUEUE = "Arreglos de Pago"
NOMBRE_BU = "Arreglos de Pago"
PERFIL_EXO = "Microsoft Exchange Online"
SSS = 2  # Server-Side Synchronization (incoming/outgoingemaildeliverymethod)
APROBADO = 1  # emailrouteraccessapproval
TODOS_LOS_CORREOS = 0  # incomingemailfilteringmethod
COLS_MBX = ("name,emailaddress,statecode,incomingemailstatus,outgoingemailstatus,emailrouteraccessapproval,"
            "isemailaddressapprovedbyo365admin,incomingemaildeliverymethod,outgoingemaildeliverymethod,"
            "enabledforincomingemail,enabledforoutgoingemail,testemailconfigurationscheduled,"
            "_emailserverprofile_value,_regardingobjectid_value,_ownerid_value,lastsyncerror,"
            "testmailboxaccesscompletedon,lastsuccessfulsynccompletedon")
ETIQ = {"incomingemailstatus": {0: "Not Run", 1: "Success", 2: "Failure"},
        "outgoingemailstatus": {0: "Not Run", 1: "Success", 2: "Failure"},
        "emailrouteraccessapproval": {0: "Empty", 1: "Approved", 2: "Pending Approval", 3: "Rejected"}}


def ok(s, b, que):
    if s >= 300:
        raise SystemExit(f"{que}: HTTP {s} {b.get('error')}")
    return b


def uno(dv, ruta, que):
    filas = ok(*dv.call("GET", ruta)[:2], que).get("value", [])
    if len(filas) > 1:
        raise SystemExit(f"{que}: {len(filas)} filas, se esperaba a lo sumo una")
    return filas[0] if filas else None


def limpio(r, direccion):
    out = {k: v for k, v in r.items() if not k.startswith("@")}
    if "emailaddress" in out:
        out["emailaddress"] = "PP_MAIL_BUZON_1" if (out["emailaddress"] or "").lower() == direccion else "OTRA"
    for k, m in ETIQ.items():
        if k in out:
            out[k + "_etiqueta"] = m.get(out[k], out[k])
    return out


def main():
    solo = "--solo-verificar" in sys.argv
    direccion = leer_secretos(requeridas=("PP_MAIL_BUZON_1",))["PP_MAIL_BUZON_1"].strip().lower()
    dv = Dataverse()
    ev = {"t": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), "pasos": []}

    bu = uno(dv, f"businessunits?$select=businessunitid&$filter=name eq '{NOMBRE_BU}'", "BU")
    team = uno(dv, f"teams?$select=teamid,name&$filter=_businessunitid_value eq {bu['businessunitid']} and isdefault eq true",
               "team del sistema")
    perfil = uno(dv, f"emailserverprofiles?$select=emailserverprofileid&$filter=name eq '{PERFIL_EXO}' and statecode eq 0",
                 "perfil de servidor")

    # Ninguna otra queue ni mailbox activo puede usar la dirección (CARP-CFG-005 y conflicto de sincronización).
    otras = ok(*dv.call("GET", f"queues?$select=queueid,name&$filter=emailaddress eq '{direccion}'")[:2], "queues")["value"]
    otras_q = [q for q in otras if q["name"] != NOMBRE_QUEUE]
    mbx_dir = ok(*dv.call("GET", f"mailboxes?$select=mailboxid,name,_regardingobjectid_value&$filter=emailaddress eq '{direccion}'")[:2],
                 "mailboxes")["value"]
    ev["otras_queues_con_la_direccion"] = [{"id": q["queueid"], "name": q["name"]} for q in otras_q]
    if otras_q:
        raise SystemExit(f"hay otras queues con la dirección de PP_MAIL_BUZON_1: {ev['otras_queues_con_la_direccion']}; no se sigue")

    q = uno(dv, f"queues?$select=queueid,name,emailaddress,queueviewtype,incomingemailfilteringmethod,statecode,"
                f"_ownerid_value,_owningbusinessunit_value,_defaultmailbox_value&$filter=name eq '{NOMBRE_QUEUE}'", "queue")
    if q is None:
        if solo:
            raise SystemExit("la queue no existe (--solo-verificar)")
        cuerpo = {"name": NOMBRE_QUEUE, "emailaddress": direccion, "queueviewtype": 0,
                  "incomingemailfilteringmethod": TODOS_LOS_CORREOS,
                  "description": "Queue del área de Arreglos de Pago (inventario 13.4). Recibe y envía por el buzón del área.",
                  "businessunitid@odata.bind": f"/businessunits({bu['businessunitid']})",
                  "ownerid@odata.bind": f"/teams({team['teamid']})"}
        s, b, h = dv.call("POST", "queues", cuerpo)
        ok(s, b, "crear queue")
        qid = Dataverse.id_creado(h)
        ev["pasos"].append({"paso": "crear queue", "http": s, "id": qid})
        q = uno(dv, f"queues?$select=queueid,name,emailaddress,queueviewtype,incomingemailfilteringmethod,statecode,"
                    f"_ownerid_value,_owningbusinessunit_value,_defaultmailbox_value&$filter=queueid eq {qid}", "queue")
        estado_q = "creada"
    else:
        estado_q = "ya_existia"
        cambios = {}
        if (q.get("emailaddress") or "").lower() != direccion:
            cambios["emailaddress"] = direccion
        if q.get("incomingemailfilteringmethod") != TODOS_LOS_CORREOS:
            cambios["incomingemailfilteringmethod"] = TODOS_LOS_CORREOS
        if cambios and not solo:
            s, b, _ = dv.call("PATCH", f"queues({q['queueid']})", cambios)
            ok(s, b, "corregir queue")
            ev["pasos"].append({"paso": "corregir queue", "http": s, "campos": sorted(cambios)})
            estado_q = "corregida"
    ev["queue"] = limpio(q, direccion)
    ev["queue"]["estado"] = estado_q
    ev["comprobaciones_queue"] = {
        "owner_es_team_del_sistema": q["_ownerid_value"] == team["teamid"],
        "bu_es_arreglos_de_pago": q["_owningbusinessunit_value"] == bu["businessunitid"],  # businessunitid viene null en queues
        "filtro_all_email_messages": q["incomingemailfilteringmethod"] == TODOS_LOS_CORREOS,
        "publica": q["queueviewtype"] == 0, "activa": q["statecode"] == 0}

    mid = q["_defaultmailbox_value"]
    m = ok(*dv.call("GET", f"mailboxes({mid})?$select={COLS_MBX}")[:2], "leer mailbox")
    if not solo:
        cambios = {}
        if (m.get("emailaddress") or "").lower() != direccion:
            cambios["emailaddress"] = direccion
        if m.get("incomingemaildeliverymethod") != SSS:
            cambios["incomingemaildeliverymethod"] = SSS
        if m.get("outgoingemaildeliverymethod") != SSS:
            cambios["outgoingemaildeliverymethod"] = SSS
        if m.get("_emailserverprofile_value") != perfil["emailserverprofileid"]:
            cambios["emailserverprofile@odata.bind"] = f"/emailserverprofiles({perfil['emailserverprofileid']})"
        if cambios:
            s, b, _ = dv.call("PATCH", f"mailboxes({mid})", cambios)
            ev["pasos"].append({"paso": "mailbox: sincronización del servidor y perfil", "http": s,
                                "campos": sorted(cambios), "error": b.get("error")})
            ok(s, b, "configurar mailbox")
        if m.get("emailrouteraccessapproval") != APROBADO:
            s, b, _ = dv.call("PATCH", f"mailboxes({mid})", {"emailrouteraccessapproval": APROBADO})
            ev["pasos"].append({"paso": "aprobar correo", "http": s, "error": b.get("error")})
        m = ok(*dv.call("GET", f"mailboxes({mid})?$select={COLS_MBX}")[:2], "releer mailbox")
        listo = m.get("emailrouteraccessapproval") == APROBADO
        if listo and not (m.get("incomingemailstatus") == 1 and m.get("outgoingemailstatus") == 1):
            s, b, _ = dv.call("PATCH", f"mailboxes({mid})", {"testemailconfigurationscheduled": True})
            ev["pasos"].append({"paso": "Test & Enable (testemailconfigurationscheduled)", "http": s, "error": b.get("error")})
            for _ in range(24):  # hasta 4 minutos
                time.sleep(10)
                m = ok(*dv.call("GET", f"mailboxes({mid})?$select={COLS_MBX}")[:2], "releer mailbox")
                if m.get("incomingemailstatus") and m.get("outgoingemailstatus"):
                    break
    ev["mailbox"] = limpio(m, direccion)
    st, al, _ = dv.call("GET", f"tracelogs?$select=text,level,createdon&$filter=_regardingobjectid_value eq {mid}&$orderby=createdon desc&$top=5")
    ev["alertas_del_mailbox"] = [{"createdon": a["createdon"], "level": a.get("level"), "text": (a.get("text") or "")[:400]}
                                 for a in al.get("value", [])] if st < 300 else {"http": st, "error": al.get("error")}

    nombre = "02_verificacion.json" if solo else "01_cola_buzon.json"
    with open(os.path.join(EVID, nombre), "w", encoding="utf-8") as f:
        json.dump(ev, f, ensure_ascii=False, indent=1)
    estado = {"lote": "lote-0", "queue": {"id": q["queueid"], "name": NOMBRE_QUEUE}, "mailbox": {"id": mid},
              "bu": bu["businessunitid"], "team_del_sistema": team["teamid"], "perfil_servidor": perfil["emailserverprofileid"]}
    if os.path.exists(ESTADO):
        with open(ESTADO, encoding="utf-8") as f:
            previo = json.load(f)
        previo.update(estado)
        estado = previo
    with open(ESTADO, "w", encoding="utf-8") as f:
        json.dump(estado, f, ensure_ascii=False, indent=1)
    print(json.dumps({"queue": ev["queue"], "comprobaciones": ev["comprobaciones_queue"], "mailbox": ev["mailbox"],
                      "pasos": ev["pasos"], "alertas": ev["alertas_del_mailbox"]}, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
