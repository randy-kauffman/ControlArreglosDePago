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
EQ = "parameters('@@ev:sanic_carp_ev_spkteamarea@@')"
EC = "parameters('@@ev:sanic_carp_ev_spkcola@@')"
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


def trigger_buzon(filtro_asunto, con_adjuntos):
    return {"Al_llegar_correo": {
        "recurrence": {"interval": 1, "frequency": "Minute"},
        "splitOn": "@triggerOutputs()?['body/value']",
        "type": "OpenApiConnection",
        "inputs": {"host": {"connectionName": "@@conr:outlook@@", "operationId": "SharedMailboxOnNewEmailV2", "apiId": OFFICE},
                   "parameters": {"mailboxAddress": f"@{K}", "folderId": "Inbox", "subjectFilter": filtro_asunto,
                                  "importance": "Any", "includeAttachments": con_adjuntos},
                   "authentication": "@parameters('$authentication')"}}}


TODOS = ["Succeeded", "Failed", "Skipped", "TimedOut"]
ESTADO_ACCIONES = lambda scope, desde: select({scope: TODOS}, f"@result('{scope}')", {
    "nombre": "@item()?['name']", "estado": "@item()?['status']", "codigo": "@item()?['code']",
    "error": "@coalesce(item()?['error']?['message'], item()?['outputs']?['body']?['error']?['message'], '')"})
DIAG = lambda quien, prefijo, ra_: dv("CreateRecord", ra_, {
    "entityName": "emails", "item/subject": f"@concat('SPK-DIAG {quien} ', coalesce({prefijo}, ''))",
    "item/description": "@string(outputs('Armar_diagnostico'))", "item/directioncode": True})
HDR = ("?$select=id,internetMessageId,conversationId,internetMessageHeaders,subject,hasAttachments,receivedDateTime")
MSG = f"concat('{G}', {K}, '/messages/', triggerOutputs()?['body/id'])"


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
def _opcionales_v4(EC):
    cola = dv("CreateRecord", {}, {
        "entityName": "queueitems", "item/queueid@odata.bind": f"@concat('/queues(', {EC}, ')')",
        "item/objectid_email@odata.bind": "@concat('/emails(', variables('varEmailId'), ')')"})
    return {
        "Opcionales": {"runAfter": ra("Por_cada_adjunto"), "type": "Scope", "actions": {
            "Agregar_a_la_cola": cola,
            "Marcar_categoria": http(ra("Agregar_a_la_cola", estados=["Succeeded", "Failed"]), f"@{MSG}", "PATCH", "{\"categories\": [\"CARP-registrado\"]}")}},
        "Resumen_opcionales": select({"Opcionales": TODOS}, "@result('Opcionales')", {
            "nombre": "@item()?['name']", "estado": "@item()?['status']", "codigo": "@item()?['code']",
            "error": "@coalesce(item()?['error']?['message'], item()?['outputs']?['body']?['error']?['message'], '')"}),
        "Fijar_opcionales": {"runAfter": {"Resumen_opcionales": TODOS}, "type": "SetVariable", "inputs": {"name": "varOpc", "value": "@string(body('Resumen_opcionales'))"}},
        "Fijar_modo_final": {"runAfter": {"Fijar_opcionales": TODOS}, "type": "AppendToStringVariable", "inputs": {"name": "varModo", "value": "+registrado"}},
    }


def flujo_v4():
    ac = {}
    ac["Variable_conversacion"] = {"runAfter": {}, "type": "InitializeVariable", "inputs": {"variables": [{"name": "varConv", "type": "string", "value": ""}]}}
    ac["Variable_correo"] = {"runAfter": ra("Variable_conversacion"), "type": "InitializeVariable", "inputs": {"variables": [{"name": "varEmailId", "type": "string", "value": ""}]}}
    ac["Variable_modo"] = {"runAfter": ra("Variable_correo"), "type": "InitializeVariable", "inputs": {"variables": [{"name": "varModo", "type": "string", "value": "sin_procesar"}]}}
    ac["Variable_opcionales"] = {"runAfter": ra("Variable_modo"), "type": "InitializeVariable", "inputs": {"variables": [{"name": "varOpc", "type": "string", "value": ""}]}}

    cuerpo_comun = {
        "entityName": "emails",
        "item/subject": "@triggerOutputs()?['body/subject']",
        "item/description": "@triggerOutputs()?['body/body']",
        "item/directioncode": False,
        "item/messageid": "@triggerOutputs()?['body/internetMessageId']",
        "item/sender": "@triggerOutputs()?['body/from']",
        "item/torecipients": "@triggerOutputs()?['body/to']",
        "item/actualend": "@triggerOutputs()?['body/receivedDateTime']",
        "item/ownerid@odata.bind": f"@concat('/teams(', {EQ}, ')')",
        "item/email_activity_parties": [
            {"participationtypemask": 1, "addressused": "@triggerOutputs()?['body/from']"},
            {"participationtypemask": 2, "partyid_queue@odata.bind": f"@concat('/queues(', {EC}, ')')"}]}
    con_conv = dict(cuerpo_comun)
    con_conv["item/regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind"] = "@concat('/sanic_carp_tbl_conversacions(', variables('varConv'), ')')"

    def setv(nombre, ra_, nom, valor):
        return {"runAfter": ra_, "type": "SetVariable", "inputs": {"name": nom, "value": valor}}

    OPCIONALES_V4 = _opcionales_v4(EC)
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
            "actions": {
                "Crear_sin_conversacion": dv("CreateRecord", {}, cuerpo_comun),
                "Fijar_correo_a": setv("varEmailId", ra("Crear_sin_conversacion"), "varEmailId", "@outputs('Crear_sin_conversacion')?['body/activityid']"),
                "Fijar_modo_a": setv("varModo", ra("Fijar_correo_a"), "varModo", "ruta_C_sin_regarding")},
            "else": {"actions": {
                "Crear_con_conversacion": dv("CreateRecord", {}, con_conv),
                "Fijar_correo_b": setv("varEmailId", ra("Crear_con_conversacion"), "varEmailId", "@outputs('Crear_con_conversacion')?['body/activityid']"),
                "Fijar_modo_b": setv("varModo", ra("Fijar_correo_b"), "varModo", "ruta_A_con_regarding")}}},
        "Cerrar_como_recibido": dv("UpdateOnlyRecord", ra("Crear_el_correo"), {
            "entityName": "emails", "recordId": "@variables('varEmailId')", "item/statecode": 1, "item/statuscode": 4}),
        "Por_cada_adjunto": {"runAfter": ra("Cerrar_como_recibido"), "type": "Foreach",
            "foreach": "@coalesce(body('Leer_adjuntos')?['value'], json('[]'))",
            "runtimeConfiguration": {"concurrency": {"repetitions": 1}},
            "actions": {"Es_archivo": {"runAfter": {}, "type": "If",
                "expression": {"and": [{"not": {"equals": ["@coalesce(items('Por_cada_adjunto')?['contentBytes'], '')", ""]}}]},
                "actions": {"Crear_adjunto": dv("CreateRecord", {}, {
                    "entityName": "activitymimeattachments",
                    "item/filename": "@items('Por_cada_adjunto')?['name']",
                    "item/subject": "@items('Por_cada_adjunto')?['name']",
                    "item/mimetype": "@items('Por_cada_adjunto')?['contentType']",
                    "item/body": "@items('Por_cada_adjunto')?['contentBytes']",
                    "item/objectid_email@odata.bind": "@concat('/emails(', variables('varEmailId'), ')')",
                    "item/objecttypecode": "email",
                    "item/attachmentcontentid": "@if(equals(items('Por_cada_adjunto')?['isInline'], true), items('Por_cada_adjunto')?['contentId'], '')"})},
                "else": {"actions": {}}}}},
    }
    ac["Intento"] = {"runAfter": ra("Variable_opcionales"), "type": "Scope", "actions": {
        "Leer_mensaje": http({}, f"@concat({MSG}, '{HDR}')"),
        "Filtrar_en_respuesta_a": query(ra("Leer_mensaje"), "@coalesce(body('Leer_mensaje')?['internetMessageHeaders'], json('[]'))",
                                        "@equals(toLower(item()?['name']), 'in-reply-to')"),
        "Variantes": compose(ra("Filtrar_en_respuesta_a"), {
            "a": "@trim(coalesce(first(body('Filtrar_en_respuesta_a'))?['value'], ''))",
            "b": "@replace(replace(trim(coalesce(first(body('Filtrar_en_respuesta_a'))?['value'], '')), '<', ''), '>', '')",
            "c": "@concat('<', replace(replace(trim(coalesce(first(body('Filtrar_en_respuesta_a'))?['value'], '')), '<', ''), '>', ''), '>')"}),
        "Leer_adjuntos": http(ra("Variantes"), f"@concat({MSG}, '/attachments?$select=id,name,contentType,size,isInline,contentId,contentBytes')"),
        "Resumen_adjuntos": select(ra("Leer_adjuntos"), "@coalesce(body('Leer_adjuntos')?['value'], json('[]'))", {
            "nombre": "@item()?['name']", "tipo": "@if(empty(item()?['contentBytes']), 'sin_contenido_incrustado', 'archivo')", "enLinea": "@item()?['isInline']", "contentId": "@item()?['contentId']",
            "tamano": "@item()?['size']", "tieneBytes": "@not(empty(item()?['contentBytes']))"}),
        "Buscar_duplicado": dv("ListRecords", ra("Resumen_adjuntos"), {
            "entityName": "emails", "$select": "activityid", "$top": 1,
            "$filter": "@concat('directioncode eq false and messageid eq ''', triggerOutputs()?['body/internetMessageId'], '''')"}),
        "Es_nuevo": {"runAfter": ra("Buscar_duplicado"), "type": "If",
            "expression": {"and": [{"equals": ["@length(outputs('Buscar_duplicado')?['body/value'])", 0]}]},
            "actions": dict(procesar, **OPCIONALES_V4),
            "else": {"actions": {"Fijar_modo_duplicado": setv("varModo", {}, "varModo", "duplicado_no_se_crea")}}},
    }}
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


# --------------------------------------------------------------------------------------------- salida (e2e)
def flujo_snd():
    ac = {}
    id_ = "triggerOutputs()?['body/activityid']"
    ac["Variable_bloqueado"] = {"runAfter": {}, "type": "InitializeVariable", "inputs": {"variables": [{"name": "varBloqueado", "type": "boolean", "value": False}]}}
    ac["Variable_modo"] = {"runAfter": ra("Variable_bloqueado"), "type": "InitializeVariable", "inputs": {"variables": [{"name": "varModo", "type": "string", "value": "no_es_del_area"}]}}
    ac["Variable_imid"] = {"runAfter": ra("Variable_modo"), "type": "InitializeVariable", "inputs": {"variables": [{"name": "varImid", "type": "string", "value": ""}]}}
    ac["Es_conversacion"] = dv("ListRecords", ra("Variable_imid"), {
        "entityName": "sanic_carp_tbl_conversacions", "$select": "sanic_carp_tbl_conversacionid", "$top": 1,
        "$filter": "@concat('sanic_carp_tbl_conversacionid eq ', coalesce(triggerOutputs()?['body/_regardingobjectid_value'], '00000000-0000-0000-0000-000000000000'))"})
    ac["Remitente_del_correo"] = dv("ListRecords", ra("Es_conversacion"), {
        "entityName": "activityparties", "$select": "addressused", "$top": 5,
        "$filter": f"@concat('_activityid_value eq ', {id_}, ' and participationtypemask eq 1')"})
    ac["Sigue_pendiente"] = dv("ListRecords", ra("Remitente_del_correo"), {
        "entityName": "emails", "$select": "activityid", "$top": 1,
        "$filter": f"@concat('activityid eq ', {id_}, ' and statuscode eq 6')"})
    ac["Remitente_coincide"] = query(ra("Sigue_pendiente"), "@coalesce(outputs('Remitente_del_correo')?['body/value'], json('[]'))",
                                     f"@equals(toLower(coalesce(item()?['addressused'], '')), toLower({KS}))")

    cuerpo = {
        "Leer_correo": dv("GetItem", {}, {"entityName": "emails", "recordId": f"@{id_}", "$select": "subject,description,_parentactivityid_value"}),
        "Leer_padre": dv("GetItem", ra("Leer_correo"), {"entityName": "emails", "recordId": "@outputs('Leer_correo')?['body/_parentactivityid_value']", "$select": "messageid"}),
        "Destinatarios": dv("ListRecords", ra("Leer_padre"), {
            "entityName": "activityparties", "$select": "addressused,participationtypemask",
            "$filter": f"@concat('_activityid_value eq ', {id_}, ' and (participationtypemask eq 2 or participationtypemask eq 3)')"}),
        "Para_filtrado": query(ra("Destinatarios"), "@coalesce(outputs('Destinatarios')?['body/value'], json('[]'))", "@equals(item()?['participationtypemask'], 2)"),
        "Para_lista": select(ra("Para_filtrado"), "@body('Para_filtrado')", {"emailAddress": {"address": "@item()?['addressused']"}}),
        "Copia_filtrada": query(ra("Para_lista"), "@coalesce(outputs('Destinatarios')?['body/value'], json('[]'))", "@equals(item()?['participationtypemask'], 3)"),
        "Copia_lista": select(ra("Copia_filtrada"), "@body('Copia_filtrada')", {"emailAddress": {"address": "@item()?['addressused']"}}),
        "Buscar_original": http(ra("Copia_lista"), f"@concat('{G}', {KS}, '/messages?$top=1&$select=id,internetMessageId,subject&$filter=', uriComponent(concat('internetMessageId eq ''', outputs('Leer_padre')?['body/messageid'], '''')))"),
        "Crear_respuesta": http(ra("Buscar_original"), f"@concat('{G}', {KS}, '/messages/', first(body('Buscar_original')?['value'])?['id'], '/createReply')", "POST", "{}"),
        "Cuerpo_del_borrador": compose(ra("Crear_respuesta"), {
            "body": {"contentType": "HTML", "content": "@outputs('Leer_correo')?['body/description']"},
            "toRecipients": "@body('Para_lista')", "ccRecipients": "@body('Copia_lista')"}),
        "Completar_borrador": http(ra("Cuerpo_del_borrador"), f"@concat('{G}', {KS}, '/messages/', body('Crear_respuesta')?['id'])", "PATCH", "@string(outputs('Cuerpo_del_borrador'))"),
        "Adjuntos_del_correo": dv("ListRecords", ra("Completar_borrador"), {
            "entityName": "activitymimeattachments", "$select": "filename,mimetype,body,attachmentcontentid",
            "$filter": f"@concat('_objectid_value eq ', {id_})"}),
        "Por_cada_adjunto": {"runAfter": ra("Adjuntos_del_correo"), "type": "Foreach",
            "foreach": "@coalesce(outputs('Adjuntos_del_correo')?['body/value'], json('[]'))",
            "runtimeConfiguration": {"concurrency": {"repetitions": 1}},
            "actions": {"Agregar_adjunto": http({}, f"@concat('{G}', {KS}, '/messages/', body('Crear_respuesta')?['id'], '/attachments')", "POST",
                "@string(json(concat('{\"@odata.type\":\"#microsoft.graph.fileAttachment\",\"name\":\"', items('Por_cada_adjunto')?['filename'], '\",\"contentType\":\"', items('Por_cada_adjunto')?['mimetype'], '\",\"contentBytes\":\"', items('Por_cada_adjunto')?['body'], '\",\"isInline\":', if(empty(items('Por_cada_adjunto')?['attachmentcontentid']), 'false', 'true'), ',\"contentId\":\"', coalesce(items('Por_cada_adjunto')?['attachmentcontentid'], ''), '\"}')))")}},
        "Leer_borrador": http(ra("Por_cada_adjunto"), f"@concat('{G}', {KS}, '/messages/', body('Crear_respuesta')?['id'], '?$select=id,internetMessageId,subject')"),
        "Fijar_imid": {"runAfter": ra("Leer_borrador"), "type": "SetVariable", "inputs": {"name": "varImid", "value": "@body('Leer_borrador')?['internetMessageId']"}},
        "Enviar_respuesta": http(ra("Fijar_imid"), f"@concat('{G}', {KS}, '/messages/', body('Crear_respuesta')?['id'], '/send')", "POST"),
        "Cerrar_como_enviado": dv("UpdateOnlyRecord", ra("Enviar_respuesta"), {
            "entityName": "emails", "recordId": f"@{id_}", "item/statecode": 1, "item/statuscode": 3, "item/messageid": "@variables('varImid')"}),
    }
    ac["Intento"] = {"runAfter": ra("Remitente_coincide"), "type": "If",
        "expression": {"and": [{"greater": ["@length(outputs('Es_conversacion')?['body/value'])", 0]},
                               {"greater": ["@length(body('Remitente_coincide'))", 0]},
                               {"greater": ["@length(outputs('Sigue_pendiente')?['body/value'])", 0]}]},
        "actions": dict({
            "Bloquear": dv("UpdateOnlyRecord", {}, {"entityName": "emails", "recordId": f"@{id_}", "item/statuscode": 7}),
            "Fijar_bloqueado": {"runAfter": ra("Bloquear"), "type": "SetVariable", "inputs": {"name": "varBloqueado", "value": True}},
            "Fijar_modo": {"runAfter": ra("Fijar_bloqueado"), "type": "SetVariable", "inputs": {"name": "varModo", "value": "procesando"}},
            "Envio": {"runAfter": ra("Fijar_modo"), "type": "Scope", "actions": cuerpo},
            "Si_fallo": {"runAfter": {"Envio": ["Failed", "TimedOut"]}, "type": "If",
                "expression": {"and": [{"equals": ["@variables('varBloqueado')", True]}]},
                "actions": {"Marcar_fallido": dv("UpdateOnlyRecord", {}, {"entityName": "emails", "recordId": f"@{id_}", "item/statecode": 0, "item/statuscode": 8}),
                            "Fijar_modo_fallo": {"runAfter": ra("Marcar_fallido"), "type": "SetVariable", "inputs": {"name": "varModo", "value": "fallido_8"}}},
                "else": {"actions": {}}},
            "Fijar_modo_ok": {"runAfter": {"Envio": ["Succeeded"]}, "type": "SetVariable", "inputs": {"name": "varModo", "value": "enviado_3"}},
        }),
        "else": {"actions": {}}}
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
    trig = {"Al_quedar_pendiente_de_envio": {"type": "OpenApiConnectionWebhook",
        "inputs": {"host": {"connectionName": "@@conr:dataverse@@", "operationId": "SubscribeWebhookTrigger", "apiId": DV},
                   "parameters": {"subscriptionRequest/message": 3, "subscriptionRequest/entityname": "email", "subscriptionRequest/scope": 4,
                                  "subscriptionRequest/filteringattributes": "statuscode",
                                  "subscriptionRequest/filterexpression": "statuscode eq 6 and directioncode eq true"},
                   "authentication": "@parameters('$authentication')"},
        "runtimeConfiguration": {"concurrency": {"runs": 1}}}}
    return {"triggers": trig, "actions": ac}


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
    ("carp-spk-v4", "Cloud Flow - CARP - SPK - V4 Entrada completa (r4)", flujo_v4,
     "Spike F0 V-4: captura el correo nuevo del buzon de prueba 4 y crea el email en Dataverse con dueno el team del area (idempotencia, correlacion por In-Reply-To, adjuntos, cola, categoria). Deja una fila SPK-DIAG.",
     {"outlook": CONN_O, "dataverse": CONN_D}, ["sanic_carp_ev_spkbuzon", "sanic_carp_ev_spkteamarea", "sanic_carp_ev_spkcola"]),
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
    usadas_ev = {x for x in ("sanic_carp_ev_spkbuzon", "sanic_carp_ev_spkteamarea", "sanic_carp_ev_spkcola", "sanic_carp_ev_spksalida") if f"@@ev:{x}@@" in texto}
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
