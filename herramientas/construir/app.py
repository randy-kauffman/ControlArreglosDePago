#!/usr/bin/env python3
"""Construye la app model-driven y su sitemap, desde un playbook de tipo `app`.
Contrato: `power-platform-construir`; Learn, "Create, manage, and publish
model-driven apps using code".

    python3 herramientas/construir/app.py <playbook.md> [--solo-verificar] [--completar]

Van en UNA herramienta porque son una sola cosa: un sitemap sin app no lo abre
nadie, y una app sin sitemap no navega a ningún lado. La secuencia
(`sitemap` → `appmodule` → `AddAppComponents` del sitemap → roles → `PublishXml` → tablas por `ImportSolution` →
`ValidateApp`) conviene que se ejecute junta y se verifique junta.

**Las TABLAS de la app no entran por `AddAppComponents`.** Con `{"@odata.type": "Microsoft.Dynamics.CRM.entity",
"entityid": "<MetadataId>"}` la plataforma responde 204 e IGNORA el GUID: agrega siempre la tabla de metadatos `entity`
(`objectid` 9d0f025b-…) y nunca la pedida (reproducido en Dev el 2026-09-22 y el 2026-10-20, con y sin `#` en el tipo,
con el tipo de la tabla y con la `@odata.type` primero; Learn solo documenta el ejemplo de vista y formulario). Las apps
reales del entorno tienen sus tablas como componentes tipo 1 cuyo `objectid` es el MetadataId, pero llegan por solución.
Por eso, si después de publicar faltan tablas, se meten con `importar_solucion.importar_tablas_de_la_app` (exporta la
solución, reemplaza el bloque `<AppModuleComponents>` de la app, importa, publica y relee). Esa importación reemplaza la
lista de componentes de la app: también se lleva la referencia basura a `entity` que dejó el intento anterior.

El `sitemapxml` se genera del playbook. Su esquema no está publicado en Learn:
se sacó leyendo un sitemap real del entorno (2026-09-22).

Dos cosas que el playbook declara y la herramienta resuelve contra el entorno:
los íconos, que se referencian como `/WebResources/<nombre>.svg`, y las vistas,
que se resuelven a su `savedqueryid`.

**Entradas con vista propia**: un `SubArea` con `Entity=` abre la vista POR
DEFECTO de esa tabla. Como el sitemap puede tener varias entradas sobre la
misma tabla (cada una con su vista), las que nombran una vista se arman con
`Url` apuntando a su `viewid`.

**Privilegios y dashboard** (diseno/07, G-A1): una entrada admite `privilegios: [{tabla, privilegio}]` (`<Privilege Entity=..
Privilege=../>` hijo de la `SubArea`, después de los títulos; `privilegio` es uno o varios de Read, Write, Append, AppendTo,
Create, Delete, Share, Assign, All, separados por coma sin espacios) y, en lugar de `tabla`, `dashboard: "<nombre>"` (se
resuelve a su `formid` y se abre con `Url=/main.aspx?pagetype=dashboard&id=%7b..%7d`). El dashboard entra además como
componente de la app (`AddAppComponents` con `systemform`, componente 60) y la verificación cuenta que haya quedado.

**Idiomas**: los títulos del sitemap llevan UNA etiqueta por cada idioma de la
Identidad (`idiomas`); el bloque de la app no declara idiomas propios. Ese patrón NO se pudo verificar
contra ningún sitemap del entorno: hay que mirarlo en la app una vez.

Última línea de la salida: JSON de una línea con `estado`
(creado | actualizado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import os
import sys
from xml.sax.saxutils import quoteattr

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _comun import (  # noqa: E402
    estado_de_escritura,
    Bloqueado,
    ErrorEntorno,
    ErrorPlaybook,
    Rastro,
    comprobar_solucion_e_idioma,
    dividir_secciones,
    escribir_metadatos,
    exigir_forma,
    exigir_sin_tildes,
    leer_entorno,
    leer_texto,
    obtener_componente,
    obtener_identidad,
    salida,
    validar_textos_por_idioma,
)
from importar_solucion import importar_tablas_de_la_app as importar_tablas  # noqa: E402

TIPO = "app"

# Copiados tal cual de un sitemap real del entorno. `ToolTipResourseId` va con
# esa errata: así está en el esquema de la plataforma.
CLIENTES = "All,Outlook,OutlookLaptopClient,OutlookWorkstationClient,Web"
SKU = "All,OnPremise,Live,SPLA"
RECURSO_AREA = "SitemapDesigner.NewTitle"
RECURSO_GRUPO = "SitemapDesigner.NewGroup"
RECURSO_TOOLTIP = "SitemapDesigner.Unknown"

CLIENTE_UNIFICADO = 4    # appmodule.clienttype: 4 = Unified Interface, 2 = cliente web clásico
FORMFACTOR_TODOS = 1     # el valor que llevan todas las apps del entorno
NAVEGACION_AREAS = 0     # navegación por áreas del sitemap

COMPONENTE_SITEMAP = 62  # Learn, AppModuleComponent
VISTA_LISTA = 1039       # viewtype de una vista de lista en /main.aspx

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

CLAVES = {"tipo": str, "nombre": str, "uniquename": str, "descripcion": str, "icono": str,
          "sitemap": str, "roles": list, "tablas": list, "areas": list}

C_APP = "la consulta de la app (GET appmodules)"
C_SITEMAP = "la consulta del sitemap (GET sitemaps)"
C_RECURSO = "la consulta del icono (GET webresourceset)"
C_VISTA = "la consulta de una vista del sitemap (GET savedqueries)"
C_ROL = "la consulta de un rol (GET roles)"
C_COMPONENTES = "la consulta de los componentes de la app (GET RetrieveAppComponents)"

COMPONENTE_TABLA = 1  # appmodulecomponent.componenttype
COMPONENTE_DASHBOARD = 60  # appmodulecomponent.componenttype de un systemform (el dashboard es un systemform de tipo 0)

# Learn, SiteMap schema (SiteMap.xsd, CRM_PrivilegeId_SiteMap): los de datos de una tabla.
PRIVILEGIOS_DE_SITEMAP = {"Read", "Write", "Append", "AppendTo", "Create", "Delete", "Share", "Assign", "All"}
TIPO_DASHBOARD = 0       # systemform.type: 0 = dashboard
C_DASHBOARD = "la consulta de un dashboard del sitemap (GET systemforms)"


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def _validar_titulos(titulos, idiomas, donde):
    # `01-convenciones.md`: las etiquetas embebidas en el XML del sitemap van UNA POR CADA IDIOMA de la Identidad,
    # porque ahí la plataforma no sustituye. El bloque de la app no declara idiomas propios: usa los de la Identidad.
    # Los títulos del sitemap son nombres visibles: sin tildes ni ñ (BP-PP-197), en cada idioma.
    validar_textos_por_idioma(titulos, idiomas, f"{donde}: el titulo")


def _validar_privilegios(privilegios, tablas, donde):
    """`<Privilege Entity=... Privilege=.../>` por tabla: la entrada se muestra solo a quien lo tiene (Learn, "SiteMap schema")."""
    exigir_forma(privilegios, list, donde, "privilegios", no_vacio=True)
    vistas = set()
    for i, p in enumerate(privilegios):
        aqui = f"{donde}.privilegios[{i}]"
        exigir_forma(p, dict, donde, f"privilegios[{i}]")
        faltan, sobran = sorted({"tabla", "privilegio"} - set(p)), sorted(set(p) - {"tabla", "privilegio"})
        if faltan or sobran:
            raise ErrorPlaybook(f"{aqui}: faltan {faltan}, sobran {sobran}")
        tabla = exigir_forma(p["tabla"], str, aqui, "tabla", no_vacio=True)
        texto = exigir_forma(p["privilegio"], str, aqui, "privilegio", no_vacio=True)
        if tabla not in tablas:
            raise ErrorPlaybook(f"{aqui}: la tabla '{tabla}' no está en la lista de tablas de la app")
        if tabla in vistas:
            raise ErrorPlaybook(f"{aqui}: la tabla '{tabla}' ya tiene un privilegio en esta entrada (se juntan con comas)")
        vistas.add(tabla)
        lista = texto.split(",")
        malos = [x for x in lista if x not in PRIVILEGIOS_DE_SITEMAP]
        if malos:
            raise ErrorPlaybook(f"{aqui}: privilegio {malos} no admitido; los válidos son {sorted(PRIVILEGIOS_DE_SITEMAP)}, "
                                "separados por comas y sin espacios")
        if len(set(lista)) != len(lista):
            raise ErrorPlaybook(f"{aqui}: hay privilegios repetido en {texto!r}")


def _validar_estructura(datos, identidad):
    faltan, sobran = sorted(set(CLAVES) - set(datos)), sorted(set(datos) - set(CLAVES))
    if faltan or sobran:
        partes = []
        if faltan:
            partes.append(f"faltan {faltan}")
        if sobran:
            partes.append(f"sobran {sobran}")
        raise ErrorPlaybook(f"el bloque de la app no tiene las claves esperadas: {'; '.join(partes)}")
    for clave, tipo in CLAVES.items():
        exigir_forma(datos[clave], tipo, "el bloque de la app", clave, no_vacio=True)
    if datos["tipo"] != TIPO:
        raise ErrorPlaybook(f"el bloque dice tipo = {datos['tipo']!r}, se esperaba {TIPO!r}")

    exigir_sin_tildes(datos["nombre"], "nombre")
    for clave, patron in (("uniquename", f"{identidad['prefijo']}_{identidad['abrev']}_mda_"),
                          ("sitemap", f"{identidad['prefijo']}_{identidad['abrev']}_sm_")):
        if not datos[clave].startswith(patron):
            raise ErrorPlaybook(f"{clave} = {datos[clave]!r} no sigue el patrón {patron}<nombre> (`01-convenciones.md` §2)")

    idiomas = identidad["idiomas"]
    exigir_forma(datos["roles"], [str], "el bloque de la app", "roles", no_vacio=True)
    exigir_forma(datos["tablas"], [str], "el bloque de la app", "tablas", no_vacio=True)

    ids, entradas = set(), 0
    for i, area in enumerate(exigir_forma(datos["areas"], list, "el bloque de la app", "areas", no_vacio=True)):
        aqui = f"el area [{i}]"
        exigir_forma(area, dict, aqui, "area")
        if sorted(area) != ["grupos", "titulo"]:
            raise ErrorPlaybook(f"{aqui}: se admite exactamente {{titulo, grupos}}")
        _validar_titulos(area["titulo"], idiomas, aqui)
        for j, grupo in enumerate(exigir_forma(area["grupos"], list, aqui, "grupos", no_vacio=True)):
            donde_g = f"{aqui}.grupo[{j}]"
            exigir_forma(grupo, dict, donde_g, "grupo")
            if sorted(grupo) != ["entradas", "titulo"]:
                raise ErrorPlaybook(f"{donde_g}: se admite exactamente {{titulo, entradas}}")
            _validar_titulos(grupo["titulo"], idiomas, donde_g)
            for k, e in enumerate(exigir_forma(grupo["entradas"], list, donde_g, "entradas", no_vacio=True)):
                donde_e = f"{donde_g}.entrada[{k}]"
                exigir_forma(e, dict, donde_e, "entrada")
                # Una entrada abre una tabla (con vista opcional) O un dashboard, exactamente uno.
                sobran_e = sorted(set(e) - {"id", "titulo", "tabla", "vista", "icono", "dashboard", "privilegios"})
                faltan_e = sorted({"id", "titulo", "icono"} - set(e))
                if "dashboard" in e and "vista" in e:
                    sobran_e = sorted(set(sobran_e) | {"vista"})
                if faltan_e or sobran_e:
                    raise ErrorPlaybook(f"{donde_e}: faltan {faltan_e}, sobran {sobran_e}")
                if ("tabla" in e) == ("dashboard" in e):
                    raise ErrorPlaybook(f"{donde_e}: una entrada lleva 'tabla' o 'dashboard', exactamente uno")
                for campo in ("id", "icono") + (("tabla",) if "tabla" in e else ("dashboard",)):
                    exigir_forma(e[campo], str, donde_e, campo, no_vacio=True)
                if "dashboard" in e:
                    exigir_sin_tildes(e["dashboard"], "dashboard")
                if "vista" in e:
                    exigir_forma(e["vista"], str, donde_e, "vista", no_vacio=True)
                _validar_titulos(e["titulo"], idiomas, donde_e)
                if e["id"] in ids:
                    raise ErrorPlaybook(f"{donde_e}: el id '{e['id']}' está repetido en el sitemap")
                ids.add(e["id"])
                if "tabla" in e and e["tabla"] not in datos["tablas"]:
                    raise ErrorPlaybook(
                        f"{donde_e}: la entrada abre '{e['tabla']}', que no está en la lista de tablas de la app")
                if "privilegios" in e:
                    _validar_privilegios(e["privilegios"], datos["tablas"], donde_e)
                entradas += 1
    if entradas == 0:
        raise ErrorPlaybook("el sitemap no tiene ninguna entrada")


def validar_playbook(datos, identidad):
    _validar_estructura(datos, identidad)


# ---------------------------------------------------------------------------
# Generación del sitemapxml
# ---------------------------------------------------------------------------
def _titulos(titulos, idiomas):
    partes = "".join(f'<Title LCID="{i}" Title={quoteattr(titulos[str(i)])} />' for i in idiomas)
    return f"<Titles>{partes}</Titles>"


def sitemapxml(datos, iconos, vistas, idiomas, dashboards=None):
    """`iconos` mapea nombre de web resource → ruta `/WebResources/...`;
    `vistas` mapea (tabla, nombre de vista) → savedqueryid; `idiomas` son los
    de la Identidad, en el orden en que se escriben los títulos; `dashboards`
    mapea nombre de dashboard → formid."""
    dashboards = dashboards or {}
    partes = ['<SiteMap IntroducedVersion="7.0.0.0">']
    for i, area in enumerate(datos["areas"]):
        partes.append(
            f'<Area Id="area_{i}" ResourceId="{RECURSO_AREA}" DescriptionResourceId="{RECURSO_AREA}" '
            f'ShowGroups="true" IntroducedVersion="7.0.0.0">' + _titulos(area["titulo"], idiomas))
        for j, grupo in enumerate(area["grupos"]):
            partes.append(
                f'<Group Id="group_{i}_{j}" ResourceId="{RECURSO_GRUPO}" DescriptionResourceId="{RECURSO_GRUPO}" '
                f'IntroducedVersion="7.0.0.0" IsProfile="false" ToolTipResourseId="{RECURSO_TOOLTIP}">'
                + _titulos(grupo["titulo"], idiomas))
            for e in grupo["entradas"]:
                icono = iconos[e["icono"]]
                comun = (f'Id={quoteattr(e["id"])} VectorIcon={quoteattr(icono)} Icon={quoteattr(icono)} '
                         f'Client="{CLIENTES}" AvailableOffline="true" PassParams="false" Sku="{SKU}"')
                privilegios = "".join(
                    f"<Privilege Entity={quoteattr(p['tabla'])} Privilege={quoteattr(p['privilegio'])} />"
                    for p in e.get("privilegios", []))
                if "dashboard" in e:
                    # Se abre por URL con el id del dashboard (`main.aspx?pagetype=dashboard&id=`). [verificar en la app]
                    url = f"/main.aspx?pagetype=dashboard&amp;id=%7b{dashboards[e['dashboard']]}%7d"
                    partes.append(f'<SubArea {comun} Url="{url}">' + _titulos(e["titulo"], idiomas) + privilegios
                                  + "</SubArea>")
                elif "vista" in e:
                    # `Entity=` abriria la vista POR DEFECTO de la tabla; con
                    # cuatro entradas sobre la misma tabla, las cuatro abririan
                    # lo mismo. Por eso las que nombran una vista van por Url.
                    vista_id = vistas[(e["tabla"], e["vista"])]
                    url = (f"/main.aspx?etn={e['tabla']}&amp;pagetype=entitylist"
                           f"&amp;viewid=%7b{vista_id}%7d&amp;viewtype={VISTA_LISTA}")
                    partes.append(f'<SubArea {comun} Url="{url}">' + _titulos(e["titulo"], idiomas) + privilegios
                                  + "</SubArea>")
                else:
                    partes.append(f'<SubArea {comun} Entity={quoteattr(e["tabla"])}>'
                                  + _titulos(e["titulo"], idiomas) + privilegios + "</SubArea>")
            partes.append("</Group>")
        partes.append("</Area>")
    partes.append("</SiteMap>")
    return "".join(partes)


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def _icono(dv, nombre):
    cuerpo = leer_entorno(dv, f"webresourceset?$select=name&$filter=name eq '{nombre}'", C_RECURSO)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_RECURSO, "value")
    if len(filas) != 1:
        raise Bloqueado(f"el icono '{nombre}' no existe como web resource (hay {len(filas)}); hay que crearlo primero")
    return "/WebResources/" + nombre


def _vista(dv, tabla, nombre):
    ruta = f"savedqueries?$select=savedqueryid&$filter=name eq '{nombre}' and returnedtypecode eq '{tabla}'"
    cuerpo = leer_entorno(dv, ruta, C_VISTA)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_VISTA, "value")
    if len(filas) != 1:
        raise Bloqueado(f"la entrada de sitemap pide la vista '{nombre}' sobre '{tabla}' y hay {len(filas)}")
    return exigir_forma(filas[0].get("savedqueryid"), str, C_VISTA, "savedqueryid", no_vacio=True)


def _dashboard(dv, nombre):
    """El `formid` del dashboard (un `systemform` de tipo 0) con ese nombre: la entrada del sitemap lo abre por su id."""
    ruta = f"systemforms?$select=formid&$filter=type eq {TIPO_DASHBOARD} and name eq '{nombre}'"
    cuerpo = leer_entorno(dv, ruta, C_DASHBOARD)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_DASHBOARD, "value")
    if len(filas) != 1:
        raise Bloqueado(f"la entrada de sitemap pide el dashboard '{nombre}' y hay {len(filas)}; hay que crearlo primero")
    return exigir_forma(filas[0].get("formid"), str, C_DASHBOARD, "formid", no_vacio=True)


def _rol(dv, nombre):
    cuerpo = leer_entorno(dv, f"roles?$select=roleid,name&$filter=name eq '{nombre}'", C_ROL)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_ROL, "value")
    if not filas:
        raise Bloqueado(f"el rol '{nombre}' no existe; la app no se puede asociar a un rol que no está")
    # Un rol existe una vez por unidad de negocio: alcanza con el de la raíz.
    return exigir_forma(filas[0].get("roleid"), str, C_ROL, "roleid", no_vacio=True)


def _leer_sitemap(dv, unico):
    # OJO: un GET sin filtro devuelve SOLO el sitemap clasico, aunque el
    # entorno tenga varios de apps (verificado en Dev). Siempre con filtro.
    ruta = f"sitemaps?$select=sitemapid,sitemapnameunique,sitemapxml,isappaware&$filter=sitemapnameunique eq '{unico}'"
    cuerpo = leer_entorno(dv, ruta, C_SITEMAP)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_SITEMAP, "value")
    if len(filas) > 1:
        raise ErrorEntorno(f"{C_SITEMAP} devolvió {len(filas)} sitemaps llamados '{unico}'; el nombre único es único")
    return filas[0] if filas else None


def _leer_app(dv, unico):
    # OJO: `GET appmodules?` devuelve SOLO la capa PUBLICADA. Una app recién
    # creada nace con `componentstate = 1` (sin publicar) y no aparece ahí,
    # aunque exista. Si la herramienta no la ve, la vuelve a crear y la
    # plataforma responde `HTTP 400 {"error":"-2147155681"}`, que significa
    # "unique name duplicado" y no lo dice por ningún lado (verificado en Dev
    # el 2026-09-22 creando dos veces el mismo unique name). Por eso se lee
    # SIEMPRE por la capa sin publicar, que devuelve las dos.
    ruta = ("appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()"
            "?$select=appmoduleid,name,uniquename,description,webresourceid,ismanaged,componentstate"
            f"&$filter=uniquename eq '{unico}'")
    cuerpo = leer_entorno(dv, ruta, C_APP)
    filas = exigir_forma(cuerpo.get("value"), [dict], C_APP, "value")
    if len(filas) > 1:
        raise ErrorEntorno(f"{C_APP} devolvió {len(filas)} apps con unique name '{unico}'")
    return filas[0] if filas else None


def _tablas_de_la_app(dv, id_app):
    """Nombres lógicos de las tablas que la app REALMENTE incluye.
    `RetrieveAppComponents` es la función documentada para esto; el `objectid`
    de un componente de tipo 1 es el `MetadataId` de la tabla."""
    cuerpo = leer_entorno(dv, f"RetrieveAppComponents(AppModuleId={id_app})", C_COMPONENTES)
    nombres = set()
    for fila in exigir_forma(cuerpo.get("value"), [dict], C_COMPONENTES, "value"):
        if fila.get("componenttype") != COMPONENTE_TABLA:
            continue
        definicion = leer_entorno(dv, f"EntityDefinitions({fila['objectid']})?$select=LogicalName",
                                  C_COMPONENTES, admite_404=True)
        if definicion and definicion.get("LogicalName"):
            nombres.add(definicion["LogicalName"])
    return nombres


def _dashboards_de_la_app(dv, id_app):
    """`formid` de los systemform (dashboards) que la app REALMENTE incluye. `AddAppComponents` con
    `Microsoft.Dynamics.CRM.systemform` sí los agrega (verificado en Dev el 2026-10-20), pero se cuentan igual."""
    cuerpo = leer_entorno(dv, f"RetrieveAppComponents(AppModuleId={id_app})", C_COMPONENTES)
    return {str(f.get("objectid")).lower() for f in exigir_forma(cuerpo.get("value"), [dict], C_COMPONENTES, "value")
            if f.get("componenttype") == COMPONENTE_DASHBOARD}


def _normalizar_xml(texto):
    return "".join((texto or "").split())


def _contra_entorno(dv, datos, identidad, solo_verificar, completar, componente, dormir):
    solucion = identidad["solucion"]
    comprobar_solucion_e_idioma(dv, identidad)

    iconos = {}
    for area in datos["areas"]:
        for grupo in area["grupos"]:
            for e in grupo["entradas"]:
                if e["icono"] not in iconos:
                    iconos[e["icono"]] = _icono(dv, e["icono"])
    iconos[datos["icono"]] = _icono(dv, datos["icono"])

    vistas = {}
    for area in datos["areas"]:
        for grupo in area["grupos"]:
            for e in grupo["entradas"]:
                if "vista" in e and (e["tabla"], e["vista"]) not in vistas:
                    vistas[(e["tabla"], e["vista"])] = _vista(dv, e["tabla"], e["vista"])

    dashboards = {}
    for area in datos["areas"]:
        for grupo in area["grupos"]:
            for e in grupo["entradas"]:
                if "dashboard" in e and e["dashboard"] not in dashboards:
                    dashboards[e["dashboard"]] = _dashboard(dv, e["dashboard"])

    xml = sitemapxml(datos, iconos, vistas, identidad["idiomas"], dashboards)
    hubo_escritura = False
    hubo_alta = False

    # --- el sitemap ---
    sitemap = _leer_sitemap(dv, datos["sitemap"])
    if sitemap is None:
        if solo_verificar:
            return "difiere", componente, f"el sitemap '{datos['sitemap']}' no existe en el entorno"
        cuerpo = {"sitemapnameunique": datos["sitemap"], "sitemapname": datos["nombre"],
                  "sitemapxml": xml, "isappaware": True}
        est, resp, _ = escribir_metadatos(dv, "POST", "sitemaps", cuerpo, dormir=dormir, solucion=solucion)
        if est not in (200, 201, 204):
            return "error", componente, f"el alta del sitemap devolvió HTTP {est} (se esperaba 204): {resp}"
        sitemap = _leer_sitemap(dv, datos["sitemap"])
        if sitemap is None:
            return "error", componente, "el alta del sitemap no dio error pero no aparece al releer"
        hubo_escritura = True
        hubo_alta = True
    elif _normalizar_xml(sitemap.get("sitemapxml")) != _normalizar_xml(xml) and not solo_verificar:
        id_sm = exigir_forma(sitemap.get("sitemapid"), str, C_SITEMAP, "sitemapid", no_vacio=True)
        est, resp, _ = escribir_metadatos(
            dv, "PATCH", f"sitemaps({id_sm})", {"sitemapxml": xml}, dormir=dormir, solucion=solucion)
        if est not in (200, 204):
            return "error", componente, f"la corrección del sitemap devolvió HTTP {est}: {resp}"
        sitemap = _leer_sitemap(dv, datos["sitemap"])
        hubo_escritura = True

    id_sitemap = exigir_forma(sitemap.get("sitemapid"), str, C_SITEMAP, "sitemapid", no_vacio=True)

    # --- la app ---
    app = _leer_app(dv, datos["uniquename"])
    if app is None:
        if solo_verificar:
            return "difiere", componente, f"la app '{datos['uniquename']}' no existe en el entorno"
        icono_id = leer_entorno(
            dv, f"webresourceset?$select=webresourceid&$filter=name eq '{datos['icono']}'", C_RECURSO)
        id_icono = exigir_forma(icono_id.get("value"), [dict], C_RECURSO, "value", no_vacio=True)[0]["webresourceid"]
        # `clienttype` NO se puede omitir: la plataforma lo pone en 2 (cliente
        # web clásico) y la app arranca con el cartel "This app is designed for
        # the legacy web client...". Todas las apps del entorno están en 4
        # (Unified Interface); la nuestra nació en 2 por no declararlo
        # (verificado en Dev el 2026-09-23).
        cuerpo = {"name": datos["nombre"], "uniquename": datos["uniquename"],
                  "description": datos["descripcion"], "webresourceid": id_icono,
                  "clienttype": CLIENTE_UNIFICADO, "formfactor": FORMFACTOR_TODOS,
                  "navigationtype": NAVEGACION_AREAS}
        est, resp, _ = escribir_metadatos(dv, "POST", "appmodules", cuerpo, dormir=dormir, solucion=solucion)
        if est not in (200, 201, 204):
            return "error", componente, f"el alta de la app devolvió HTTP {est} (se esperaba 204): {resp}"
        app = _leer_app(dv, datos["uniquename"])
        if app is None:
            return "error", componente, "el alta de la app no dio error pero no aparece al releer"
        hubo_escritura = True
        hubo_alta = True

    id_app = exigir_forma(app.get("appmoduleid"), str, C_APP, "appmoduleid", no_vacio=True)
    if app.get("ismanaged"):
        return "difiere", componente, "la app está managed en este entorno; no se toca desde acá"

    # --- componentes: sitemap y tablas ---
    if not solo_verificar:
        componentes = [{"sitemapid": id_sitemap, "@odata.type": "Microsoft.Dynamics.CRM.sitemap"}]
        # El dashboard de una entrada tiene que ser componente de la app, no solo una URL del sitemap.
        componentes += [{"formid": fid, "@odata.type": "Microsoft.Dynamics.CRM.systemform"}
                        for fid in dashboards.values()]
        est, resp, _ = escribir_metadatos(
            dv, "POST", "AddAppComponents", {"AppId": id_app, "Components": componentes}, dormir=dormir)
        if est not in (200, 204):
            return "error", componente, f"AddAppComponents del sitemap devolvió HTTP {est}: {resp}"
        hubo_escritura = True

        # --- roles ---
        for nombre_rol in datos["roles"]:
            id_rol = _rol(dv, nombre_rol)
            # El `@odata.id` de un `$ref` tiene que ser un URI ABSOLUTO: con
            # uno relativo OData responde 400 pidiendo `odata.context`.
            est, resp, _ = escribir_metadatos(
                dv, "POST", f"appmodules({id_app})/appmoduleroles_association/$ref",
                {"@odata.id": dv.api + f"roles({id_rol})"}, dormir=dormir)
            if est not in (200, 204):
                # Que el rol ya esté asociado no es un fallo.
                if est != 400 or "duplicate" not in str(resp).lower():
                    return "error", componente, f"asociar el rol '{nombre_rol}' devolvió HTTP {est}: {resp}"

        est, resp, _ = escribir_metadatos(
            dv, "POST", "PublishXml",
            {"ParameterXml": f"<importexportxml><appmodules><appmodule>{id_app}</appmodule></appmodules>"
                             "</importexportxml>"}, dormir=dormir)
        if est not in (200, 204):
            return "error", componente, f"la publicación de la app devolvió HTTP {est}: {resp}"

        # --- tablas: por ImportSolution, no por AddAppComponents (ver el docstring) ---
        faltan_antes = [t for t in datos["tablas"] if t not in _tablas_de_la_app(dv, id_app)]
        if faltan_antes:
            est_i, _, detalle_i = importar_tablas(
                dv, identidad, datos["uniquename"], datos["sitemap"], datos["tablas"], componente)
            if est_i not in ("creado", "ya_existia"):
                return "error", componente, (
                    f"faltaban {len(faltan_antes)} tablas en la app y la importación terminó en '{est_i}': {detalle_i}")
            hubo_escritura = True

    if not solo_verificar:
        # Tras el PATCH, `GET sitemaps?` sigue devolviendo el XML publicado hasta que `PublishXml` publica la app
        # (verificado en Dev el 2026-10-20): la comparación final se hace con lo que quedó después de publicar.
        sitemap = _leer_sitemap(dv, datos["sitemap"]) or sitemap

    # --- validación de la propia plataforma ---
    est, resp, _ = dv.call("GET", f"ValidateApp(AppModuleId={id_app})")
    problemas = []
    if est == 200:
        for issue in ((resp.get("AppValidationResponse") or {}).get("ValidationIssueList") or []):
            if issue.get("ErrorType") == "Error":
                problemas.append(issue.get("Message") or str(issue))
    else:
        problemas.append(f"ValidateApp devolvió HTTP {est}: {resp}")

    # Un 2xx no es una verificación: `AddAppComponents` devolvía 204 sin agregar la tabla pedida (verificado en Dev
    # el 2026-09-22 y el 2026-10-20), y `ValidateApp` tampoco se queja. Se cuenta lo que quedó.
    tablas_puestas = _tablas_de_la_app(dv, id_app)
    faltan_tablas = [t for t in datos["tablas"] if t not in tablas_puestas]

    difs = list(problemas)
    if dashboards:
        puestos = _dashboards_de_la_app(dv, id_app)
        for nombre, fid in dashboards.items():
            if str(fid).lower() not in puestos:
                difs.append(f"la app no incluye el dashboard '{nombre}' (componente {COMPONENTE_DASHBOARD}); "
                            "hay que correr app.py sin --solo-verificar (lo agrega con AddAppComponents)")
    if faltan_tablas:
        difs.append(
            f"la app no incluye {len(faltan_tablas)} de las {len(datos['tablas'])} tablas del playbook "
            f"(faltan {faltan_tablas}); `AddAppComponents` no las agrega: hay que correr app.py sin --solo-verificar "
            "(las mete por ImportSolution)")
    if _normalizar_xml(sitemap.get("sitemapxml")) != _normalizar_xml(xml):
        difs.append("el sitemapxml no coincide con el que genera el playbook")
    if not sitemap.get("isappaware"):
        difs.append("el sitemap no está marcado como isappaware: no es el de una app moderna")

    if difs:
        estado = "error" if hubo_escritura else "difiere"
        prefijo = "se escribió pero no quedó bien: " if hubo_escritura else ""
        return estado, componente, prefijo + "; ".join(difs)

    entradas = sum(len(g["entradas"]) for a in datos["areas"] for g in a["grupos"])
    return estado_de_escritura(hubo_alta, hubo_escritura), componente, (
        f"appmoduleid {id_app}, sitemapid {id_sitemap}, {len(datos['areas'])} areas y {entradas} entradas, "
        f"{len(datos['tablas'])} tablas, {len(datos['roles'])} roles, idiomas {identidad['idiomas']}; ValidateApp sin errores")


def construir(ruta_playbook, solo_verificar, fabrica_cliente, completar=False, dormir=None):
    """Nunca lanza: siempre devuelve `(estado, componente, detalle)`."""
    if dormir is None:
        import time

        dormir = time.sleep
    componente = os.path.basename(ruta_playbook)
    paso = "leer el playbook"
    try:
        secciones = dividir_secciones(leer_texto(ruta_playbook))
        paso = "leer el bloque '## 2. Qué se crea'"
        datos = obtener_componente(secciones, TIPO)
        if isinstance(datos.get("uniquename"), str):
            componente = datos["uniquename"]
        paso = "leer el bloque '## 1. Identidad'"
        identidad = obtener_identidad(secciones)
        paso = "validar el bloque de la app"
        validar_playbook(datos, identidad)
    except ErrorPlaybook as e:
        return "error", componente, str(e)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except OSError as e:
        return "error", componente, f"no se pudo leer el playbook '{ruta_playbook}': {e}"
    except Exception as e:
        return "error", componente, f"fallo inesperado validando el playbook (offline), al {paso}: {type(e).__name__}: {e}"

    print(f"Playbook leído y validado (offline). tipo={TIPO} uniquename={componente}")
    try:
        dv = fabrica_cliente()
    except Exception:
        return "error", componente, MENSAJE_FALLO_CLIENTE

    rastro = Rastro(dv)
    try:
        return _contra_entorno(rastro, datos, identidad, solo_verificar, completar, componente, dormir)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except ErrorEntorno as e:
        return "error", componente, str(e)
    except Exception as e:
        return "error", componente, f"fallo inesperado hablando con Dataverse, durante {rastro.en_curso}: {type(e).__name__}: {e}"


def main():
    argv = sys.argv[1:]
    rutas = [a for a in argv if not a.startswith("--")]
    banderas = {a for a in argv if a.startswith("--")}
    desconocidas = sorted(banderas - {"--solo-verificar", "--completar"})
    if len(rutas) != 1 or desconocidas:
        return salida(
            "error", "desconocido",
            "uso incorrecto: app.py <playbook.md> [--solo-verificar] [--completar]"
            + (f"; banderas desconocidas: {desconocidas}" if desconocidas else ""),
        )
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(
        rutas[0], "--solo-verificar" in banderas, Dataverse, completar="--completar" in banderas)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
