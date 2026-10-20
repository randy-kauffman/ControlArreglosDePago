"""Tanda 3, línea base (solo lecturas, salvo el cliente HUMO): buzón sincronizando, 42 steps habilitados, usuarios en la BU
y lo que la primera sincronización ya trajo. Crea (una vez) `cliente-t3` con el correo del buzón 2, para medir DC-14
(la conversación nueva se enlaza al único Cliente activo con el correo del remitente)."""
from t3lib import *

dv = adm()
R = Res("00_linea_base.json")
base = {"fecha": "2026-10-20"}
mb = get(dv, f"mailboxes({MAILBOX})?$select=incomingemailstatus,outgoingemailstatus,lastsuccessfulsynccompletedon,processingstatecode,"
             "enabledforincomingemail,enabledforoutgoingemail,isemailaddressapprovedbyo365admin,_regardingobjectid_value")
base["mailbox"] = limpio(mb)
R.v("base", "buzón con entrada y salida en Success", mb["incomingemailstatus"] == 1 and mb["outgoingemailstatus"] == 1, "1/1",
    (mb["incomingemailstatus"], mb["outgoingemailstatus"], mb["lastsuccessfulsynccompletedon"]))
st = get(dv, "sdkmessageprocessingsteps?$select=name,statecode&$filter=startswith(name,'STEP - CARP')")["value"]
R.v("base", "37 steps CARP habilitados", len(st) == 37 and all(s["statecode"] == 0 for s in st), 37, (len(st), sum(s["statecode"] == 0 for s in st)))
for papel, uid in (("oficial", OFICIAL), ("jefe", JEFE)):
    u = get(dv, f"systemusers({uid})?$select=isdisabled,_businessunitid_value")
    R.v("base", f"{papel} habilitado y en la BU", (not u["isdisabled"]) and u["_businessunitid_value"] == BU_AREA, True, limpio(u))

# Lo que la primera sincronización trajo de la bandeja del buzón 1 (correos de spikes anteriores, todos de prueba)
prev = get(dv, f"emails?$select=createdon,actualend,directioncode,_regardingobjectid_value,_ownerid_value,_owningbusinessunit_value"
               f"&$filter=_receivingmailboxid_value eq {MAILBOX}&$orderby=createdon asc")["value"]
base["primera_sincronizacion"] = {"correos": len(prev), "todos_entrantes": all(not x["directioncode"] for x in prev),
                                  "con_conversacion": sum(1 for x in prev if x["_regardingobjectid_value"]),
                                  "dueno_team_del_area": sum(1 for x in prev if x["_ownerid_value"] == TEAM_DEF),
                                  "bu_del_area": sum(1 for x in prev if x["_owningbusinessunit_value"] == BU_AREA),
                                  "actualend_mas_viejo": prev[0]["actualend"] if prev else None, "creados": [prev[0]["createdon"], prev[-1]["createdon"]] if prev else None}
R.v("H-26", "primera sincronización: entrantes previos con la BU y el team del área",
    base["primera_sincronizacion"]["bu_del_area"] == len(prev) and base["primera_sincronizacion"]["dueno_team_del_area"] == len(prev), len(prev),
    base["primera_sincronizacion"])

e = est3()
if "cliente_t3" not in e:
    c = Correo()
    est, b, cli = crear(dv, "cliente-t3", "cliente", {"sanic_nombrecompleto": "HUMO Cliente T3 Buzon 2", "sanic_cedula": "HUMO-ced-t3",
                                                     "sanic_correo": c.b[2], "sanic_celular": "8888-0303", **OWNER}, "correo = PP_MAIL_BUZON_2")
    R.v("prep", "cliente HUMO con el correo del buzón 2", est == 201, 201, est if est == 201 else err(b))
    poner("cliente_t3", cli)
base["cliente_t3"] = est3()["cliente_t3"]
poner("linea_base", base)
volcar("00_base.json", base)
print(json.dumps(base, indent=1, ensure_ascii=False))
