"""Genera las definiciones (flujos/*.json) y los playbooks (playbooks/*.md) de los 3 flujos del spike F0 de diseno/08.
Se escriben con Python (no a mano) para no equivocar comillas ni escapes; el JSON generado es lo que sube herramientas/construir/flujo.py.
Los flujos se crean APAGADOS, en la solución `sanic_carp_sol_spikecorreo`. Sin direcciones ni GUID: todo entra por variables de entorno."""
import json
import os

AQUI = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(AQUI, "flujos")
PB = os.path.join(AQUI, "playbooks")
os.makedirs(OUT, exist_ok=True)

K = "parameters('@@ev:sanic_carp_ev_spkbuzon@@')"       # buzón de prueba 4
KS = "parameters('@@ev:sanic_carp_ev_spksalida@@')"     # buzón desde el que sale la respuesta del área (buzón 1)
NB = "parameters('@@ev:sanic_carp_ev_nombrebu@@')"   # nombre de la BU del área (variable de la solución principal)
EQ = "variables('varTeam')"   # r10: team por defecto de la BU del área, buscado en la corrida (como hace el plugin)
EC = "variables('varCola')"   # r10: cola cuyo emailaddress es el buzón que dispara
G = "https://graph.microsoft.com/v1.0/users/"
OFFICE = "/providers/Microsoft.PowerApps/apis/shared_office365"
DV = "/providers/Microsoft.PowerApps/apis/shared_commondataserviceforapps"
RETRY = {"type": "fixed", "count": 3, "interval": "PT10S"}


def ra(*nombres, estados=("Succeeded",)):
    return {n: list(estados) for n in nombres}


def http(corre_despues, uri, metodo="GET", body=None):
    p = {"Uri": uri, "Method": metodo, "ContentType": "application/json"}
    if body is not None:
        p["Body"] = body
    return {"runAfter": corre_despues, "type": "OpenApiConnection",
            "inputs": {"host": {"connectionName": "@@conr:outlook@@", "operationId": "HttpRequest", "apiId": OFFICE},
                       "parameters": p},
            "retryPolicy": RETRY}


def dv(op, corre_despues, params, nombre_conn="@@conr:dataverse@@"):
    return {"runAfter": corre_despues, "type": "OpenApiConnection",
            "inputs": {"host": {"connectionName": nombre_conn, "operationId": op, "apiId": DV},
                       "parameters": params}}


def compose(corre_despues, valor):
    return {"runAfter": corre_despues, "type": "Compose", "inputs": valor}


def query(corre_despues, desde, donde):
    return {"runAfter": corre_despues, "type": "Query", "inputs": {"from": desde, "where": donde}}


def select(corre_despues, desde, mapa):
    return {"runAfter": corre_despues, "type": "Select", "inputs": {"from": desde, "select": mapa}}


def trigger_buzon(filtro_asunto, con_adjuntos, buzon=K, seguro=False):
    """Disparador de correo nuevo en el buzón compartido. Sin `filtro_asunto` (producción) no se filtra por asunto.
    `seguro`: salidas del disparador ocultas en el historial (traen el correo entero)."""
    parametros = {"mailboxAddress": f"@{buzon}", "folderId": "Inbox"}
    if filtro_asunto is not None:
        parametros["subjectFilter"] = filtro_asunto
    parametros.update({"importance": "Any", "includeAttachments": con_adjuntos})
    t = {"recurrence": {"interval": 1, "frequency": "Minute"},
         "splitOn": "@triggerOutputs()?['body/value']",
         "type": "OpenApiConnection",
         "inputs": {"host": {"connectionName": "@@conr:outlook@@", "operationId": "SharedMailboxOnNewEmailV2", "apiId": OFFICE},
                    "parameters": parametros,
                    "authentication": "@parameters('$authentication')"}}
    if seguro:
        t["runtimeConfiguration"] = {"secureData": {"properties": ["outputs"]}}
    return {"Al_llegar_correo": t}


TODOS = ["Succeeded", "Failed", "Skipped", "TimedOut"]
ESTADO_ACCIONES = lambda scope, desde: select({scope: TODOS}, f"@result('{scope}')", {
    "nombre": "@item()?['name']", "estado": "@item()?['status']", "codigo": "@item()?['code']",
    "error": "@item()?['error']", "cuerpo": "@take(string(item()?['outputs']?['body']), 600)"})
DIAG = lambda quien, prefijo, ra_: dv("CreateRecord", ra_, {
    "entityName": "emails", "item/subject": f"@concat('SPK-DIAG {quien} ', coalesce({prefijo}, ''))",
    "item/description": "@string(outputs('Armar_diagnostico'))", "item/directioncode": True})
HDR = ("?$select=id,internetMessageId,conversationId,internetMessageHeaders,subject,hasAttachments,receivedDateTime")
msg = lambda buzon: f"concat('{G}', {buzon}, '/messages/', triggerOutputs()?['body/id'])"
MSG = msg(K)


# --------------------------------------------------------------------------------------------- V-2
def flujo_v2():
    ac = {}
    ac["Intento"] = {"runAfter": {}, "type": "Scope", "actions": {
        "Leer_mensaje": http({}, f"@concat({MSG}, '{HDR}')"),
        "Filtrar_en_respuesta_a": query(ra("Leer_mensaje"), "@coalesce(body('Leer_mensaje')?['internetMessageHeaders'], json('[]'))",
                                        "@equals(toLower(item()?['name']), 'in-reply-to')"),
        "Nombres_de_encabezados": select(ra("Leer_mensaje"), "@coalesce(body('Leer_mensaje')?['internetMessageHeaders'], json('[]'))", "@item()?['name']"),
        "Filtrar_references": query(ra("Nombres_de_encabezados"), "@coalesce(body('Leer_mensaje')?['internetMessageHeaders'], json('[]'))",
                                    "@equals(toLower(item()?['name']), 'references')"),
        "Crear_respuesta": http(ra("Filtrar_references"), f"@concat({MSG}, '/createReply')", "POST",
                                "{\"comment\": \"Respuesta automatica del spike SPK-FC (V-2). Datos ficticios.\"}"),
        "Leer_borrador": http(ra("Crear_respuesta"), f"@concat('{G}', {K}, '/messages/', body('Crear_respuesta')?['id'], '?$select=id,internetMessageId,subject')"),
        "Enviar_respuesta": http(ra("Leer_borrador"), f"@concat('{G}', {K}, '/messages/', body('Crear_respuesta')?['id'], '/send')", "POST"),
        "Esperar_el_envio": {"runAfter": ra("Enviar_respuesta"), "type": "Wait", "inputs": {"interval": {"count": 15, "unit": "Second"}}},
        "Buscar_en_enviados": http(ra("Esperar_el_envio"), f"@concat('{G}', {K}, '/mailFolders/SentItems/messages?$top=5&$orderby=sentDateTime%20desc&$select=id,internetMessageId,subject,sentDateTime')"),
        "Coincidencia_en_enviados": query(ra("Buscar_en_enviados"), "@coalesce(body('Buscar_en_enviados')?['value'], json('[]'))",
                                          "@equals(item()?['internetMessageId'], body('Leer_borrador')?['internetMessageId'])"),
    }}
    ac["Estado_de_las_acciones"] = ESTADO_ACCIONES("Intento", None)
    ac["Armar_diagnostico"] = compose({"Estado_de_las_acciones": TODOS}, {
        "flujo": "V2 encabezados y respuesta",
        "recibido": "@utcNow()",
        "trigger_id_largo": "@length(string(triggerOutputs()?['body/id']))",
        "trigger_internetMessageId": "@triggerOutputs()?['body/internetMessageId']",
        "graph_internetMessageId": "@body('Leer_mensaje')?['internetMessageId']",
        "graph_conversationId_presente": "@not(empty(body('Leer_mensaje')?['conversationId']))",
        "encabezados_cantidad": "@length(coalesce(body('Leer_mensaje')?['internetMessageHeaders'], json('[]')))",
        "encabezados_nombres": "@join(body('Nombres_de_encabezados'), ',')",
        "in_reply_to": "@first(body('Filtrar_en_respuesta_a'))?['value']",
        "references": "@first(body('Filtrar_references'))?['value']",
        "borrador_id_largo": "@length(string(body('Crear_respuesta')?['id']))",
        "borrador_internetMessageId_createReply": "@body('Crear_respuesta')?['internetMessageId']",
        "borrador_internetMessageId_get": "@body('Leer_borrador')?['internetMessageId']",
        "enviar_status": "@outputs('Enviar_respuesta')?['statusCode']",
        "enviados_coincide_con_borrador": "@greater(length(body('Coincidencia_en_enviados')), 0)",
        "enviados_internetMessageId": "@first(body('Coincidencia_en_enviados'))?['internetMessageId']",
        "acciones": "@body('Estado_de_las_acciones')"})
    ac["Registrar_diagnostico"] = DIAG("V2", "triggerOutputs()?['body/subject']", ra("Armar_diagnostico", estados=TODOS))
    return {"triggers": trigger_buzon("SPK-FC-V2", False), "actions": ac}


# --------------------------------------------------------------------------------------------- V-4 entrada
def _opcionales(EC, MSG, diag):
    cola = dv("CreateRecord", {}, {
        "entityName": "queueitems", "item/queueid@odata.bind": f"@concat('/queues(', {EC}, ')')",
        "item/objectid_email@odata.bind": "@concat('/emails(', variables('varEmailId'), ')')"})
    op = {
        "Opcionales": {"runAfter": ra("Por_cada_adjunto"), "type": "Scope", "actions": {
            "Agregar_a_la_cola": cola,
            "Marcar_categoria": http(ra("Agregar_a_la_cola", estados=["Succeeded", "Failed"]), f"@{MSG}", "PATCH", "{\"categories\": [\"CARP-registrado\"]}")}}}
    if not diag:
        return op
    op.update({
        "Resumen_opcionales": select({"Opcionales": TODOS}, "@result('Opcionales')", {
            "nombre": "@item()?['name']", "estado": "@item()?['status']", "codigo": "@item()?['code']",
            "error": "@item()?['error']", "cuerpo": "@take(string(item()?['outputs']?['body']), 600)"}),
        "Fijar_opcionales": {"runAfter": {"Resumen_opcionales": TODOS}, "type": "SetVariable", "inputs": {"name": "varOpc", "value": "@string(body('Resumen_opcionales'))"}},
        "Fijar_modo_final": {"runAfter": {"Fijar_opcionales": TODOS}, "type": "AppendToStringVariable", "inputs": {"name": "varModo", "value": "+registrado"}},
    })
    return op


def _asegurar(acciones):
    """Producción (diseno/07 §8.1): secure inputs y outputs en toda acción que lee o escribe correo o filas de Dataverse.
    Recorre también las ramas If/Else y los Scope. Foreach/If/Scope no admiten el ajuste (no tienen contenido propio)."""
    for a in acciones.values():
        if a["type"] in ("OpenApiConnection", "Compose", "Query", "Select", "SetVariable"):
            a["runtimeConfiguration"] = dict(a.get("runtimeConfiguration", {}), secureData={"properties": ["inputs", "outputs"]})
        _asegurar(a.get("actions", {}))
        _asegurar(a.get("else", {}).get("actions", {}))


def _entrada(K, diag, seguro, despues=None):
    """Cuerpo COMPARTIDO de la entrada de correo: lo usan el spike V4 (`flujo_v4`) y el flujo de producción ENT (`flujo_ent`), para que
    no se separen. `K` = expresión del buzón que dispara (spike: sanic_carp_ev_spkbuzon; producción: sanic_carp_ev_buzonarea).
    `diag`=True agrega el andamiaje del spike (varModo, varOpc, Resumen_adjuntos, Resumen_opcionales). `seguro`=True aplica secure data.
    `despues`: acción tras la que arranca (producción: la compuerta de modo). Devuelve las acciones hasta `Intento` inclusive."""
    MSG = msg(K)
    ac = {}
    previa = [despues]

    def init(clave, nombre, tipo_valor, valor):
        ac[clave] = {"runAfter": ra(previa[0]) if previa[0] else {}, "type": "InitializeVariable",
                     "inputs": {"variables": [{"name": nombre, "type": tipo_valor, "value": valor}]}}
        previa[0] = clave

    init("Variable_conversacion", "varConv", "string", "")
    init("Variable_correo", "varEmailId", "string", "")
    if diag:
        init("Variable_modo", "varModo", "string", "sin_procesar")
        init("Variable_opcionales", "varOpc", "string", "")
    init("Variable_team", "varTeam", "string", "")
    init("Variable_cola", "varCola", "string", "")

    cuerpo_comun = {
        "entityName": "emails",
        "item/subject": "@triggerOutputs()?['body/subject']",
        "item/description": "@triggerOutputs()?['body/body']",
        "item/directioncode": False,
        "item/messageid": "@triggerOutputs()?['body/internetMessageId']",
        "item/sender": "@triggerOutputs()?['body/from']",
        "item/torecipients": "@triggerOutputs()?['body/to']",
        "item/actualend": "@triggerOutputs()?['body/receivedDateTime']",
        "item/ownerid_email@odata.bind": f"@concat('/teams(', {EQ}, ')')",
        "item/activitypointer_activity_parties": [
            {"participationtypemask": 1, "addressused": "@triggerOutputs()?['body/from']"},
            {"participationtypemask": 2, "partyid_queue@odata.bind": f"@concat('/queues(', {EC}, ')')"}]}
    con_conv = dict(cuerpo_comun)
    con_conv["item/regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind"] = "@concat('/sanic_carp_tbl_conversacions(', variables('varConv'), ')')"

    def setv(nombre, ra_, nom, valor):
        return {"runAfter": ra_, "type": "SetVariable", "inputs": {"name": nom, "value": valor}}

    def modo(nombre, ra_, valor):
        """Fija varModo: solo existe en el spike (diag); en producción no se emite."""
        return {nombre: setv(nombre, ra_, "varModo", valor)} if diag else {}

    crear_a = {"Crear_sin_conversacion": dv("CreateRecord", {}, cuerpo_comun),
               "Fijar_correo_a": setv("varEmailId", ra("Crear_sin_conversacion"), "varEmailId", "@outputs('Crear_sin_conversacion')?['body/activityid']")}
    crear_a.update(modo("Fijar_modo_a", ra("Fijar_correo_a"), "ruta_C_sin_regarding"))
    crear_b = {"Crear_con_conversacion": dv("CreateRecord", {}, con_conv),
               "Fijar_correo_b": setv("varEmailId", ra("Crear_con_conversacion"), "varEmailId", "@outputs('Crear_con_conversacion')?['body/activityid']")}
    crear_b.update(modo("Fijar_modo_b", ra("Fijar_correo_b"), "ruta_A_con_regarding"))

    procesar = {
        "Hay_en_respuesta_a": {"runAfter": {}, "type": "If",
            "expression": {"and": [{"not": {"equals": ["@outputs('Variantes')?['a']", ""]}}]},
            "actions": {
                "Buscar_original": dv("ListRecords", {}, {
                    "entityName": "emails", "$select": "activityid,_regardingobjectid_value,createdon", "$top": 1, "$orderby": "createdon asc",
                    "$filter": "@concat('_regardingobjectid_value ne null and (messageid eq ''', outputs('Variantes')?['a'], ''' or messageid eq ''', outputs('Variantes')?['b'], ''' or messageid eq ''', outputs('Variantes')?['c'], ''')')"}),
                "Hay_original": {"runAfter": ra("Buscar_original"), "type": "If",
                    "expression": {"and": [{"greater": ["@length(outputs('Buscar_original')?['body/value'])", 0]}]},
                    "actions": {
                        "Buscar_conversacion": dv("ListRecords", {}, {
                            "entityName": "sanic_carp_tbl_conversacions", "$select": "sanic_carp_tbl_conversacionid", "$top": 1,
                            "$filter": "@concat('sanic_carp_tbl_conversacionid eq ', first(outputs('Buscar_original')?['body/value'])?['_regardingobjectid_value'])"}),
                        "Hay_conversacion": {"runAfter": ra("Buscar_conversacion"), "type": "If",
                            "expression": {"and": [{"greater": ["@length(outputs('Buscar_conversacion')?['body/value'])", 0]}]},
                            "actions": {"Fijar_conversacion": setv("varConv", {}, "varConv", "@first(outputs('Buscar_conversacion')?['body/value'])?['sanic_carp_tbl_conversacionid']")},
                            "else": {"actions": {}}}},
                    "else": {"actions": {}}}},
            "else": {"actions": {}}},
        "Crear_el_correo": {"runAfter": ra("Hay_en_respuesta_a"), "type": "If",
            "expression": {"and": [{"equals": ["@variables('varConv')", ""]}]},
            "actions": crear_a,
            "else": {"actions": crear_b}},
        "Cerrar_como_recibido": dv("UpdateOnlyRecord", ra("Crear_el_correo"), {
            "entityName": "emails", "recordId": "@variables('varEmailId')", "item/statecode": 1, "item/statuscode": 4}),
        "Por_cada_adjunto": {"runAfter": ra("Cerrar_como_recibido"), "type": "Foreach",
            "foreach": "@coalesce(body('Leer_adjuntos')?['value'], json('[]'))",
            "runtimeConfiguration": {"concurrency": {"repetitions": 1}},
            "actions": {"Es_archivo": {"runAfter": {}, "type": "If",
                "expression": {"and": [{"not": {"equals": ["@coalesce(items('Por_cada_adjunto')?['contentBytes'], '')", ""]}}]},
                # r10: el conector exige item/attachmentid@odata.bind (ApplicationRequired); la Web API acepta null y crea el adjunto igual (probado)
                "actions": {"Crear_adjunto": dv("CreateRecord", {}, {
                    "entityName": "activitymimeattachments",
                    "item/filename": "@items('Por_cada_adjunto')?['name']",
                    "item/subject": "@items('Por_cada_adjunto')?['name']",
                    "item/mimetype": "@items('Por_cada_adjunto')?['contentType']",
                    "item/body": "@items('Por_cada_adjunto')?['contentBytes']",
                    "item/objectid_activitypointer@odata.bind": "@concat('/activitypointers(', variables('varEmailId'), ')')",
                    "item/objecttypecode": "email",
                    "item/attachmentid@odata.bind": "@json('null')",
                    "item/attachmentcontentid": "@if(equals(items('Por_cada_adjunto')?['isInline'], true), items('Por_cada_adjunto')?['contentId'], '')"})},
                "else": {"actions": {}}}}},
    }
    procesar.update(_opcionales(EC, MSG, diag))

    intento = {
        "Buscar_unidad": dv("ListRecords", {}, {"entityName": "businessunits", "$select": "businessunitid", "$top": 1,
                                                "$filter": f"@concat('name eq ''', {NB}, '''')"}),
        "Buscar_team": dv("ListRecords", ra("Buscar_unidad"), {"entityName": "teams", "$select": "teamid", "$top": 1,
            "$filter": "@concat('isdefault eq true and _businessunitid_value eq ', first(outputs('Buscar_unidad')?['body/value'])?['businessunitid'])"}),
        "Fijar_team": setv("varTeam", ra("Buscar_team"), "varTeam", "@first(outputs('Buscar_team')?['body/value'])?['teamid']"),
        "Buscar_cola": dv("ListRecords", ra("Fijar_team"), {"entityName": "queues", "$select": "queueid", "$top": 1,
            "$filter": f"@concat('emailaddress eq ''', {K}, '''')"}),
        "Fijar_cola": setv("varCola", ra("Buscar_cola"), "varCola", "@first(outputs('Buscar_cola')?['body/value'])?['queueid']"),
        "Leer_mensaje": http(ra("Fijar_cola"), f"@concat({MSG}, '{HDR}')"),
        "Filtrar_en_respuesta_a": query(ra("Leer_mensaje"), "@coalesce(body('Leer_mensaje')?['internetMessageHeaders'], json('[]'))",
                                        "@equals(toLower(item()?['name']), 'in-reply-to')"),
        "Variantes": compose(ra("Filtrar_en_respuesta_a"), {
            "a": "@trim(coalesce(first(body('Filtrar_en_respuesta_a'))?['value'], ''))",
            "b": "@replace(replace(trim(coalesce(first(body('Filtrar_en_respuesta_a'))?['value'], '')), '<', ''), '>', '')",
            "c": "@concat('<', replace(replace(trim(coalesce(first(body('Filtrar_en_respuesta_a'))?['value'], '')), '<', ''), '>', ''), '>')"}),
        "Leer_adjuntos": http(ra("Variantes"), f"@concat({MSG}, '/attachments')"),  # r8: $select=contentBytes falla en /attachments; el tipo base attachment no la tiene
    }
    antes_duplicado = "Leer_adjuntos"
    if diag:
        intento["Resumen_adjuntos"] = select(ra("Leer_adjuntos"), "@coalesce(body('Leer_adjuntos')?['value'], json('[]'))", {
            "nombre": "@item()?['name']", "tipo": "@if(empty(item()?['contentBytes']), 'sin_contenido_incrustado', 'archivo')", "enLinea": "@item()?['isInline']", "contentId": "@item()?['contentId']",
            "tamano": "@item()?['size']", "tieneBytes": "@not(empty(item()?['contentBytes']))"})
        antes_duplicado = "Resumen_adjuntos"
    intento["Buscar_duplicado"] = dv("ListRecords", ra(antes_duplicado), {
        "entityName": "emails", "$select": "activityid", "$top": 1,
        "$filter": "@concat('directioncode eq false and messageid eq ''', triggerOutputs()?['body/internetMessageId'], '''')"})
    intento["Es_nuevo"] = {"runAfter": ra("Buscar_duplicado"), "type": "If",
        "expression": {"and": [{"equals": ["@length(outputs('Buscar_duplicado')?['body/value'])", 0]}]},
        "actions": procesar,
        "else": {"actions": modo("Fijar_modo_duplicado", {}, "duplicado_no_se_crea")}}
    ac["Intento"] = {"runAfter": ra(previa[0]), "type": "Scope", "actions": intento}
    if seguro:
        _asegurar(ac)
    return ac


def flujo_v4():
    ac = _entrada(K, diag=True, seguro=False)
    ac["Estado_de_las_acciones"] = ESTADO_ACCIONES("Intento", None)
    ac["Armar_diagnostico"] = compose({"Estado_de_las_acciones": TODOS}, {
        "flujo": "V4 entrada completa", "recibido": "@utcNow()",
        "modo": "@variables('varModo')",
        "correo_creado": "@variables('varEmailId')",
        "conversacion_encontrada": "@variables('varConv')",
        "in_reply_to": "@outputs('Variantes')?['a']",
        "trigger_internetMessageId": "@triggerOutputs()?['body/internetMessageId']",
        "graph_internetMessageId": "@body('Leer_mensaje')?['internetMessageId']",
        "trigger_adjuntos_cantidad": "@length(coalesce(triggerOutputs()?['body/attachments'], json('[]')))",
        "trigger_hasAttachment": "@triggerOutputs()?['body/hasAttachment']",
        "graph_adjuntos": "@body('Resumen_adjuntos')",
        "duplicado": "@greater(length(coalesce(outputs('Buscar_duplicado')?['body/value'], json('[]'))), 0)",
        "acciones": "@body('Estado_de_las_acciones')",
        "opcionales": "@variables('varOpc')"})
    ac["Registrar_diagnostico"] = DIAG("V4", "triggerOutputs()?['body/subject']", ra("Armar_diagnostico", estados=TODOS))
    return {"triggers": trigger_buzon("SPK-FC-V4", True), "actions": ac}


# --------------------------------------------------------------------------------------------- ENT (producción, F1)
BZ = "parameters('@@ev:sanic_carp_ev_buzonarea@@')"       # buzón del área (variable de la solución principal)
MODO = "parameters('@@ev:sanic_carp_ev_modocorreo@@')"    # sss | flujos


def flujo_ent():
    """Flujo de producción `Cloud Flow - CARP - ENT - Capturar correo entrante` (diseno/08 §3). Misma lógica que el spike V4 (r10),
    generada por `_entrada`; difiere en lo que el spike necesita y producción no: sin filtro de asunto, sin filas SPK-DIAG ni
    andamiaje de diagnóstico, con secure data, con compuerta de modo y con cierre en Failed si falla el intento."""
    ac = {"Verificar_modo": {"runAfter": {}, "type": "If",
            # vacío o cualquier valor distinto de `flujos` = modo SSS: no se hace nada
            "expression": {"and": [{"not": {"equals": [f"@toLower(trim(coalesce({MODO}, '')))", "flujos"]}}]},
            "actions": {"Terminar_modo_sss": {"runAfter": {}, "type": "Terminate", "inputs": {"runStatus": "Succeeded"}}},
            "else": {"actions": {}}}}
    ac.update(_entrada(BZ, diag=False, seguro=True, despues="Verificar_modo"))
    # que la corrida termine Failed si el intento falla, para que el monitoreo (SAL, historial de corridas) la vea
    ac["Fallar_si_el_intento_fallo"] = {"runAfter": {"Intento": ["Failed", "TimedOut"]}, "type": "Terminate",
        "inputs": {"runStatus": "Failed", "runError": {"code": "CARP-ENT-FALLO",
                   "message": "El flujo ENT no pudo registrar el correo entrante; ver la accion que fallo en el historial de la corrida."}}}
    return {"triggers": trigger_buzon(None, True, buzon=BZ, seguro=True), "actions": ac}


# --------------------------------------------------------------------------------------------- salida (e2e)
def _salida(bz, diag, seguro, sin_padre, despues=None):
    """Cuerpo COMPARTIDO de la salida: lo usan el spike SND (`flujo_snd`) y el flujo de producción SND (`flujo_snd_produccion`), para que
    no se separen. `bz` = expresión del buzón desde el que sale la respuesta (spike: sanic_carp_ev_spksalida; producción: sanic_carp_ev_buzonarea).
    `diag`=True agrega el andamiaje del spike (varModo, Armar_diagnostico, fila SPK-DIAG). `seguro`=True aplica secure data.
    `sin_padre`=True (producción) agrega la rama de correo SIN original en Graph: un mensaje nuevo en vez de createReply (diseno/08 §11).
    `despues`: acción tras la que arranca (producción: la compuerta de modo). Devuelve el disparador y las acciones."""
    ac = {}
    id_ = "triggerOutputs()?['body/activityid']"
    previa = [despues]

    def init(clave, nombre, tipo_valor, valor):
        ac[clave] = {"runAfter": ra(previa[0]) if previa[0] else {}, "type": "InitializeVariable",
                     "inputs": {"variables": [{"name": nombre, "type": tipo_valor, "value": valor}]}}
        previa[0] = clave

    init("Variable_bloqueado", "varBloqueado", "boolean", False)
    if diag:
        init("Variable_modo", "varModo", "string", "no_es_del_area")
    init("Variable_imid", "varImid", "string", "")
    if sin_padre:
        init("Variable_original", "varOriginal", "string", "")
        init("Variable_borrador", "varBorrador", "string", "")
    ac["Es_conversacion"] = dv("ListRecords", ra(previa[0]), {
        "entityName": "sanic_carp_tbl_conversacions", "$select": "sanic_carp_tbl_conversacionid", "$top": 1,
        "$filter": "@concat('sanic_carp_tbl_conversacionid eq ', coalesce(triggerOutputs()?['body/_regardingobjectid_value'], '00000000-0000-0000-0000-000000000000'))"})
    ac["Remitente_del_correo"] = dv("ListRecords", ra("Es_conversacion"), {
        "entityName": "activityparties", "$select": "addressused", "$top": 5,
        "$filter": f"@concat('_activityid_value eq ', {id_}, ' and participationtypemask eq 1')"})
    ac["Sigue_pendiente"] = dv("ListRecords", ra("Remitente_del_correo"), {
        "entityName": "emails", "$select": "activityid", "$top": 1,
        "$filter": f"@concat('activityid eq ', {id_}, ' and statuscode eq 6')"})
    ac["Remitente_coincide"] = query(ra("Sigue_pendiente"), "@coalesce(outputs('Remitente_del_correo')?['body/value'], json('[]'))",
                                     f"@equals(toLower(coalesce(item()?['addressused'], '')), toLower({bz}))")

    # borrador: el spike usa el de createReply; producción lo guarda en varBorrador (viene de createReply o de un mensaje nuevo)
    borrador = "variables('varBorrador')" if sin_padre else "body('Crear_respuesta')?['id']"
    cuerpo = {"Leer_correo": dv("GetItem", {}, {"entityName": "emails", "recordId": f"@{id_}", "$select": "subject,description,_parentactivityid_value"})}
    if sin_padre:
        # el padre puede no existir: ListRecords con un GUID nulo devuelve vacío en vez de fallar como lo haría GetItem
        cuerpo["Leer_padre"] = dv("ListRecords", ra("Leer_correo"), {
            "entityName": "emails", "$select": "messageid", "$top": 1,
            "$filter": "@concat('activityid eq ', coalesce(outputs('Leer_correo')?['body/_parentactivityid_value'], '00000000-0000-0000-0000-000000000000'))"})
    else:
        cuerpo["Leer_padre"] = dv("GetItem", ra("Leer_correo"), {"entityName": "emails", "recordId": "@outputs('Leer_correo')?['body/_parentactivityid_value']", "$select": "messageid"})
    cuerpo.update({
        "Destinatarios": dv("ListRecords", ra("Leer_padre"), {
            "entityName": "activityparties", "$select": "addressused,participationtypemask",
            "$filter": f"@concat('_activityid_value eq ', {id_}, ' and (participationtypemask eq 2 or participationtypemask eq 3)')"}),
        "Para_filtrado": query(ra("Destinatarios"), "@coalesce(outputs('Destinatarios')?['body/value'], json('[]'))", "@equals(item()?['participationtypemask'], 2)"),
        "Para_lista": select(ra("Para_filtrado"), "@body('Para_filtrado')", {"emailAddress": {"address": "@item()?['addressused']"}}),
        "Copia_filtrada": query(ra("Para_lista"), "@coalesce(outputs('Destinatarios')?['body/value'], json('[]'))", "@equals(item()?['participationtypemask'], 3)"),
        "Copia_lista": select(ra("Copia_filtrada"), "@body('Copia_filtrada')", {"emailAddress": {"address": "@item()?['addressused']"}})})
    cuerpo_reply = {"body": {"contentType": "HTML", "content": "@outputs('Leer_correo')?['body/description']"},
                    "toRecipients": "@body('Para_lista')", "ccRecipients": "@body('Copia_lista')"}
    if not sin_padre:
        cuerpo["Buscar_original"] = http(ra("Copia_lista"), f"@concat('{G}', {bz}, '/messages?$top=1&$select=id,internetMessageId,subject&$filter=', uriComponent(concat('internetMessageId eq ''', outputs('Leer_padre')?['body/messageid'], '''')))")
        cuerpo["Crear_respuesta"] = http(ra("Buscar_original"), f"@concat('{G}', {bz}, '/messages/', first(body('Buscar_original')?['value'])?['id'], '/createReply')", "POST", "{}")
        cuerpo["Cuerpo_del_borrador"] = compose(ra("Crear_respuesta"), cuerpo_reply)
        cuerpo["Completar_borrador"] = http(ra("Cuerpo_del_borrador"), f"@concat('{G}', {bz}, '/messages/', body('Crear_respuesta')?['id'])", "PATCH", "@string(outputs('Cuerpo_del_borrador'))")
        antes_adjuntos = "Completar_borrador"
    else:
        # 1) buscar el original en Graph por el messageid del padre (solo si hay padre con messageid)
        hay_id = "@coalesce(first(outputs('Leer_padre')?['body/value'])?['messageid'], '')"
        cuerpo["Buscar_el_original"] = {"runAfter": ra("Copia_lista"), "type": "If",
            "expression": {"and": [{"not": {"equals": [hay_id, ""]}}]},
            "actions": {"Buscar_original": http({}, f"@concat('{G}', {bz}, '/messages?$top=1&$select=id,internetMessageId,subject&$filter=', uriComponent(concat('internetMessageId eq ''', first(outputs('Leer_padre')?['body/value'])?['messageid'], '''')))"),
                        "Fijar_original": {"runAfter": ra("Buscar_original"), "type": "SetVariable", "inputs": {"name": "varOriginal", "value": "@coalesce(first(body('Buscar_original')?['value'])?['id'], '')"}}},
            "else": {"actions": {}}}
        # 2) con original: createReply (hilo conservado, validado en el spike); sin original: mensaje nuevo (NO validado en Dev, va a F4)
        responder = {
            "Crear_respuesta": http({}, f"@concat('{G}', {bz}, '/messages/', variables('varOriginal'), '/createReply')", "POST", "{}"),
            "Cuerpo_del_borrador": compose(ra("Crear_respuesta"), cuerpo_reply),
            "Completar_borrador": http(ra("Cuerpo_del_borrador"), f"@concat('{G}', {bz}, '/messages/', body('Crear_respuesta')?['id'])", "PATCH", "@string(outputs('Cuerpo_del_borrador'))"),
            "Fijar_borrador_respuesta": {"runAfter": ra("Completar_borrador"), "type": "SetVariable", "inputs": {"name": "varBorrador", "value": "@body('Crear_respuesta')?['id']"}}}
        nuevo = {
            "Cuerpo_del_mensaje_nuevo": compose({}, dict({"subject": "@outputs('Leer_correo')?['body/subject']"}, **cuerpo_reply)),
            "Crear_mensaje_nuevo": http(ra("Cuerpo_del_mensaje_nuevo"), f"@concat('{G}', {bz}, '/messages')", "POST", "@string(outputs('Cuerpo_del_mensaje_nuevo'))"),
            "Fijar_borrador_nuevo": {"runAfter": ra("Crear_mensaje_nuevo"), "type": "SetVariable", "inputs": {"name": "varBorrador", "value": "@body('Crear_mensaje_nuevo')?['id']"}}}
        cuerpo["Hay_original"] = {"runAfter": ra("Buscar_el_original"), "type": "If",
            "expression": {"and": [{"not": {"equals": ["@variables('varOriginal')", ""]}}]},
            "actions": responder, "else": {"actions": nuevo}}
        antes_adjuntos = "Hay_original"
    cuerpo.update({
        "Adjuntos_del_correo": dv("ListRecords", ra(antes_adjuntos), {
            "entityName": "activitymimeattachments", "$select": "filename,mimetype,body,attachmentcontentid",
            "$filter": f"@concat('_objectid_value eq ', {id_})"}),
        "Por_cada_adjunto": {"runAfter": ra("Adjuntos_del_correo"), "type": "Foreach",
            "foreach": "@coalesce(outputs('Adjuntos_del_correo')?['body/value'], json('[]'))",
            "runtimeConfiguration": {"concurrency": {"repetitions": 1}},
            "actions": {"Agregar_adjunto": http({}, f"@concat('{G}', {bz}, '/messages/', {borrador}, '/attachments')", "POST",
                "@string(json(concat('{\"@odata.type\":\"#microsoft.graph.fileAttachment\",\"name\":\"', items('Por_cada_adjunto')?['filename'], '\",\"contentType\":\"', items('Por_cada_adjunto')?['mimetype'], '\",\"contentBytes\":\"', items('Por_cada_adjunto')?['body'], '\",\"isInline\":', if(empty(items('Por_cada_adjunto')?['attachmentcontentid']), 'false', 'true'), ',\"contentId\":\"', coalesce(items('Por_cada_adjunto')?['attachmentcontentid'], ''), '\"}')))")}},
        "Leer_borrador": http(ra("Por_cada_adjunto"), f"@concat('{G}', {bz}, '/messages/', {borrador}, '?$select=id,internetMessageId,subject')"),
        "Fijar_imid": {"runAfter": ra("Leer_borrador"), "type": "SetVariable", "inputs": {"name": "varImid", "value": "@body('Leer_borrador')?['internetMessageId']"}},
        "Enviar_respuesta": http(ra("Fijar_imid"), f"@concat('{G}', {bz}, '/messages/', {borrador}, '/send')", "POST"),
        "Cerrar_como_enviado": dv("UpdateOnlyRecord", ra("Enviar_respuesta"), {
            "entityName": "emails", "recordId": f"@{id_}", "item/statecode": 1, "item/statuscode": 3, "item/messageid": "@variables('varImid')"}),
    })

    def modo(nombre, ra_, valor):
        """Fija varModo: solo existe en el spike (diag); en producción no se emite."""
        return {nombre: {"runAfter": ra_, "type": "SetVariable", "inputs": {"name": "varModo", "value": valor}}} if diag else {}

    intento_acc = {
        "Bloquear": dv("UpdateOnlyRecord", {}, {"entityName": "emails", "recordId": f"@{id_}", "item/statuscode": 7}),
        "Fijar_bloqueado": {"runAfter": ra("Bloquear"), "type": "SetVariable", "inputs": {"name": "varBloqueado", "value": True}}}
    intento_acc.update(modo("Fijar_modo", ra("Fijar_bloqueado"), "procesando"))
    intento_acc["Envio"] = {"runAfter": ra("Fijar_modo" if diag else "Fijar_bloqueado"), "type": "Scope", "actions": cuerpo}
    # Failed es del estado Open: statecode 0 + statuscode 8 (RESULTADO, V-1)
    si_fallo = {"Marcar_fallido": dv("UpdateOnlyRecord", {}, {"entityName": "emails", "recordId": f"@{id_}", "item/statecode": 0, "item/statuscode": 8})}
    si_fallo.update(modo("Fijar_modo_fallo", ra("Marcar_fallido"), "fallido_8"))
    if not diag:
        # la corrida termina Failed DESPUES de dejar el correo en Failed, para que el monitoreo (SAL) la vea; corre aunque Marcar_fallido falle
        si_fallo["Terminar_con_fallo"] = {"runAfter": ra("Marcar_fallido", estados=TODOS), "type": "Terminate",
            "inputs": {"runStatus": "Failed", "runError": {"code": "CARP-SND-FALLO",
                       "message": "El flujo SND no pudo enviar la respuesta; el correo quedo en Failed. Ver la accion que fallo en el historial de la corrida."}}}
    intento_acc["Si_fallo"] = {"runAfter": {"Envio": ["Failed", "TimedOut"]}, "type": "If",
        "expression": {"and": [{"equals": ["@variables('varBloqueado')", True]}]},
        "actions": si_fallo, "else": {"actions": {}}}
    intento_acc.update(modo("Fijar_modo_ok", {"Envio": ["Succeeded"]}, "enviado_3"))
    ac["Intento"] = {"runAfter": ra("Remitente_coincide"), "type": "If",
        "expression": {"and": [{"greater": ["@length(outputs('Es_conversacion')?['body/value'])", 0]},
                               {"greater": ["@length(body('Remitente_coincide'))", 0]},
                               {"greater": ["@length(outputs('Sigue_pendiente')?['body/value'])", 0]}]},
        "actions": intento_acc, "else": {"actions": {}}}
    if diag:
        ac["Armar_diagnostico"] = compose({"Intento": TODOS}, {
            "flujo": "SND salida", "recibido": "@utcNow()", "modo": "@variables('varModo')",
            "email": f"@{id_}", "es_conversacion": "@greater(length(coalesce(outputs('Es_conversacion')?['body/value'], json('[]'))), 0)",
            "remitente_coincide": "@greater(length(coalesce(body('Remitente_coincide'), json('[]'))), 0)",
            "sigue_pendiente": "@greater(length(coalesce(outputs('Sigue_pendiente')?['body/value'], json('[]'))), 0)",
            "imid_grabado": "@variables('varImid')"})
        ac["Si_era_del_area"] = {"runAfter": {"Armar_diagnostico": TODOS}, "type": "If",
            "expression": {"and": [{"not": {"equals": ["@variables('varModo')", "no_es_del_area"]}}]},
            "actions": {"Registrar_diagnostico": DIAG("SND", "concat(variables('varModo'), ' ', string(" + id_ + "))", {})},
            "else": {"actions": {}}}
    else:
        # por si falla algo ANTES de poder marcar el correo (el propio bloqueo): la corrida queda Failed y el correo sigue en Pending Send
        ac["Fallar_si_el_intento_fallo"] = {"runAfter": {"Intento": ["Failed", "TimedOut"]}, "type": "Terminate",
            "inputs": {"runStatus": "Failed", "runError": {"code": "CARP-SND-FALLO",
                       "message": "El flujo SND fallo antes de poder enviar la respuesta; ver la accion que fallo en el historial de la corrida."}}}
    if seguro:
        _asegurar(ac)
    # concurrencia 1: dos corridas no toman el mismo correo (diseno/08 §4.2)
    cfg = {"concurrency": {"runs": 1}}
    if seguro:
        cfg["secureData"] = {"properties": ["outputs"]}   # el disparador trae el cuerpo del correo
    trig = {"Al_quedar_pendiente_de_envio": {"type": "OpenApiConnectionWebhook",
        "inputs": {"host": {"connectionName": "@@conr:dataverse@@", "operationId": "SubscribeWebhookTrigger", "apiId": DV},
                   "parameters": {"subscriptionRequest/message": 3, "subscriptionRequest/entityname": "email", "subscriptionRequest/scope": 4,
                                  "subscriptionRequest/filteringattributes": "statuscode",
                                  "subscriptionRequest/filterexpression": "statuscode eq 6 and directioncode eq true"},
                   "authentication": "@parameters('$authentication')"},
        "runtimeConfiguration": cfg}}
    return {"triggers": trig, "actions": ac}


def flujo_snd():
    return _salida(KS, diag=True, seguro=False, sin_padre=False)


def flujo_snd_produccion():
    """Flujo de producción `Cloud Flow - CARP - SND - Enviar respuestas` (diseno/08 §4). Misma lógica que el spike SND (r2), generada por
    `_salida`; difiere en lo que el spike necesita y producción no (sin SPK-DIAG ni varModo), y agrega: compuerta de modo, secure data,
    cierre de la corrida en Failed (`CARP-SND-FALLO`) y la rama de correo SIN original en Graph (mensaje nuevo en vez de createReply)."""
    gate = {"Verificar_modo": {"runAfter": {}, "type": "If",
            "expression": {"and": [{"not": {"equals": [f"@toLower(trim(coalesce({MODO}, '')))", "flujos"]}}]},
            "actions": {"Terminar_modo_sss": {"runAfter": {}, "type": "Terminate", "inputs": {"runStatus": "Succeeded"}}},
            "else": {"actions": {}}}}
    d = _salida(BZ, diag=False, seguro=True, sin_padre=True, despues="Verificar_modo")
    gate.update(d["actions"])
    d["actions"] = gate
    return d


# ------------------------------------------------------------------------------------------ escritura
IDENT = {"tipo_playbook": "flujo", "version_skill": "0.3.0", "proyecto": "2026-003-control-arreglos-pago", "inventario": "SPK-F0", "fase": 0,
         "entorno_url": "https://org36e60d9d.crm.dynamics.com/", "solucion": "sanic_carp_sol_spikecorreo", "publisher": "Sistemas_Abiertos_Nicaragua",
         "prefijo": "sanic", "abrev": "carp", "prefijo_opciones": 15946, "idiomas": [1033, 3082]}
CONN_O = {"referencia": "sanic_carp_conr_outlook", "api": "shared_office365"}
CONN_D = {"referencia": "sanic_carp_conr_dataverse", "api": "shared_commondataserviceforapps"}
FLUJOS = [
    ("carp-spk-v2", "Cloud Flow - CARP - SPK - V2 Encabezados y respuesta", flujo_v2,
     "Spike F0 V-2: lee los encabezados del correo nuevo del buzon de prueba 4 por Graph, responde con createReply y send y lee el internetMessageId. Deja una fila SPK-DIAG.",
     {"outlook": CONN_O, "dataverse": CONN_D}, ["sanic_carp_ev_spkbuzon"]),
    ("carp-spk-v4", "Cloud Flow - CARP - SPK - V4 Entrada completa (r10)", flujo_v4,
     "Spike F0 V-4: captura el correo nuevo del buzon de prueba 4 y crea el email en Dataverse con dueno el team del area (idempotencia, correlacion por In-Reply-To, adjuntos, cola, categoria). Deja una fila SPK-DIAG.",
     {"outlook": CONN_O, "dataverse": CONN_D}, ["sanic_carp_ev_spkbuzon", "sanic_carp_ev_nombrebu"]),
    ("carp-spk-snd", "Cloud Flow - CARP - SPK - SND Salida de respuestas (r2)", flujo_snd,
     "Spike F0 salida: toma el email saliente del area en Pending Send, lo envia desde el buzon del area con Graph (createReply, adjuntos, send) y lo pasa a Sent con el messageid real, o a Failed. Deja una fila SPK-DIAG.",
     {"outlook": CONN_O, "dataverse": CONN_D}, ["sanic_carp_ev_spksalida"]),
]
for archivo, nombre, fn, desc, conex, variables in FLUJOS:
    definicion = fn()
    texto = json.dumps(definicion, ensure_ascii=False, indent=2)
    # la herramienta exige que cada variable declarada se use
    for v in variables:
        assert f"@@ev:{v}@@" in texto, (archivo, v)
    usadas_ev = {x for x in ("sanic_carp_ev_spkbuzon", "sanic_carp_ev_spkteamarea", "sanic_carp_ev_spkcola", "sanic_carp_ev_spksalida", "sanic_carp_ev_nombrebu") if f"@@ev:{x}@@" in texto}
    assert usadas_ev == set(variables), (archivo, usadas_ev)
    open(os.path.join(OUT, archivo + ".json"), "w", encoding="utf-8").write(texto)
    comp = {"tipo": "flujo", "nombre": nombre, "descripcion": desc, "archivo": f"spikes/flujos-correo/flujos/{archivo}.json",
            "conexiones": conex, "variables": variables, "hijos": [], "activar": False}
    md = f"""# Playbook (spike F0): flujo · {nombre}

> Spike de `diseno/08` (correo por flujos). Temporal, en la solución `sanic_carp_sol_spikecorreo`, fuera de la principal. Se crea APAGADO (`activar: false`); lo activa una persona (admin@) porque un service principal no puede (ConnectionAuthorizationFailed). La definición se genera con `generar_flujos.py`.

## 1. Identidad

```json
{json.dumps(IDENT, indent=2, ensure_ascii=False)}
```

## 2. Qué se crea

```json
{json.dumps(comp, indent=2, ensure_ascii=False)}
```
"""
    open(os.path.join(PB, archivo + ".md"), "w", encoding="utf-8").write(md)
print("generados:", [f[0] for f in FLUJOS])

# flujo de producción ENT: el JSON va a playbooks/flujo/ (lo sube herramientas/construir/flujo.py); el playbook .md se escribe a mano
RAIZ = os.path.abspath(os.path.join(AQUI, "..", ".."))
def_ent = json.dumps(flujo_ent(), ensure_ascii=False, indent=2)
for v in ("sanic_carp_ev_buzonarea", "sanic_carp_ev_nombrebu", "sanic_carp_ev_modocorreo"):
    assert f"@@ev:{v}@@" in def_ent, v
assert "SPK" not in def_ent and "spk" not in def_ent, "el flujo de producción no puede tener restos del spike"
open(os.path.join(RAIZ, "playbooks", "flujo", "carp-ent.json"), "w", encoding="utf-8").write(def_ent)
print("generado: playbooks/flujo/carp-ent.json")

# flujo de producción SND: igual que ENT (el playbook .md se escribe a mano)
def_snd = json.dumps(flujo_snd_produccion(), ensure_ascii=False, indent=2)
for v in ("sanic_carp_ev_buzonarea", "sanic_carp_ev_modocorreo"):
    assert f"@@ev:{v}@@" in def_snd, v
assert "sanic_carp_ev_nombrebu" not in def_snd, "SND no usa la BU"
assert "SPK" not in def_snd and "spk" not in def_snd, "el flujo de producción no puede tener restos del spike"
open(os.path.join(RAIZ, "playbooks", "flujo", "carp-snd.json"), "w", encoding="utf-8").write(def_snd)
print("generado: playbooks/flujo/carp-snd.json")

# variables de entorno del spike (valor null: lo carga v50_solucion_y_variables.py desde memoria)
VARS = [("sanic_carp_ev_spkbuzon", "EV - CARP - SPK Buzon de prueba 4", "Direccion del buzon de prueba 4 (PP_MAIL_BUZON_4) que leen y usan los flujos del spike F0. El valor solo vive en este entorno."),
        ("sanic_carp_ev_spkteamarea", "EV - CARP - SPK Team del area", "Identificador (GUID) del team propietario del area, para que el flujo de entrada del spike cree el correo con ese duenio. Solo Dev."),
        ("sanic_carp_ev_spkcola", "EV - CARP - SPK Cola de prueba", "Identificador (GUID) de la cola de prueba sobre el buzon 4, a donde el flujo de entrada del spike manda el correo. Solo Dev."),
        ("sanic_carp_ev_spksalida", "EV - CARP - SPK Buzon de salida del area", "Direccion del buzon desde el que el flujo de salida del spike envia la respuesta (la cola del area, PP_MAIL_BUZON_1: S-31 la fija como remitente). Solo Dev.")]
for n, d, desc in VARS:
    ident = dict(IDENT, tipo_playbook="variable-entorno")
    comp = {"tipo": "variable-entorno", "nombre": n, "displayname": d, "descripcion": desc, "tipovalor": "Texto", "obligatoria": False, "valor": None}
    md = f"""# Playbook (spike F0): variable de entorno · {d}

> Spike de `diseno/08` (correo por flujos). Vive en la solución `sanic_carp_sol_spikecorreo`, fuera de la principal. El valor NO está en este archivo: lo carga `v50_solucion_y_variables.py` desde memoria.

## 1. Identidad

```json
{json.dumps(ident, indent=2, ensure_ascii=False)}
```

## 2. Qué se crea

```json
{json.dumps(comp, indent=2, ensure_ascii=False)}
```
"""
    open(os.path.join(PB, n + ".md"), "w", encoding="utf-8").write(md)
