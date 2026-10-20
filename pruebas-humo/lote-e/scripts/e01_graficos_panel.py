"""Lote E (2026-10-20), inventario 12.15, 12.16 y 12.14: los dos gráficos de sistema y el dashboard del jefe,
por Web API (DA-05 cambiada el 2026-10-20: script versionado en vez de construir a mano).
Lee el bloque json de la sección 2 de playbooks/grafico/*.md y playbooks/dashboard/supervision.md.
Idempotente: busca por nombre; si existe compara y solo escribe si difiere. Crea con MSCRM.SolutionUniqueName
y comprueba la pertenencia a la solución (componenttype 59 gráfico, 60 dashboard).
NUNCA imprime secretos ni tokens. No envía correos ni borra nada.
Uso:  python3 e01_graficos_panel.py [--solo-verificar]"""
import json
import os
import re
import sys
import uuid
from xml.sax.saxutils import quoteattr

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
sys.path.insert(0, os.path.join(RAIZ, "herramientas"))
from dataverse_api import Dataverse  # noqa: E402

LOTE = os.path.dirname(AQUI)
EVID = os.path.join(LOTE, "evidencia")
ESTADO = os.path.join(LOTE, "estado.json")
SOLUCION = "sanic_carp_sol_controlarreglospago"
PB_GRAFICOS = [os.path.join(RAIZ, "playbooks", "grafico", n) for n in ("promedio_por_respuesta.md", "tiempo_total.md")]
PB_PANEL = os.path.join(RAIZ, "playbooks", "dashboard", "supervision.md")
TIPO_GRAFICO, TIPO_DASHBOARD = 59, 60          # solutioncomponent.componenttype
SUBGRID = "{E7A81278-8635-4d9e-8D4D-59480B391C5B}"
NS = uuid.UUID("5b0c3f0e-3c2a-4b8e-9d55-2026003e0e14")  # ids estables del formxml (idempotencia)

# Presentación de los gráficos que arma el diseñador (tomada de un gráfico de barras de Dev, 2026-10-20).
PRESENTACION = (
    '<Chart Palette="None" PaletteCustomColors="91,151,213; 237,125,49; 160,116,166; 255,192,0; 68,114,196; '
    '112,173,71; 37,94,145; 158,72,14; 117,55,125; 153,115,0; 38,68,120; 67,104,43;"><Series>'
    '<Series ChartType="{serie}" IsValueShownAsLabel="True" Font="{{0}}, 9.5px" LabelForeColor="59, 59, 59" '
    'CustomProperties="PointWidth=0.75, MaxPixelPointWidth=40"></Series></Series><ChartAreas>'
    '<ChartArea BorderColor="White" BorderDashStyle="Solid"><AxisY LabelAutoFitMinFontSize="8" '
    'TitleForeColor="59, 59, 59" TitleFont="{{0}}, 10.5px" LineColor="165, 172, 181" IntervalAutoMode="VariableCount">'
    '<MajorGrid LineColor="239, 242, 246" /><MajorTickMark LineColor="165, 172, 181" />'
    '<LabelStyle Font="{{0}}, 10.5px" ForeColor="59, 59, 59" /></AxisY><AxisX LabelAutoFitMinFontSize="8" '
    'TitleForeColor="59, 59, 59" TitleFont="{{0}}, 10.5px" LineColor="165, 172, 181" IntervalAutoMode="VariableCount">'
    '<MajorTickMark LineColor="165, 172, 181" /><MajorGrid LineColor="Transparent" />'
    '<LabelStyle Font="{{0}}, 10.5px" ForeColor="59, 59, 59" /></AxisX></ChartArea></ChartAreas>'
    '<Titles><Title Alignment="TopLeft" DockingOffset="-3" Font="{{0}}, 13px" ForeColor="59, 59, 59"></Title>'
    '</Titles></Chart>')


def bloque(ruta):
    """El bloque json de '## 2. Qué se crea'."""
    texto = open(ruta, encoding="utf-8").read()
    seccion = texto.split("## 2.", 1)[1]
    return json.loads(re.search(r"```json\n(.*?)\n```", seccion, re.S).group(1))


def ok(s, b, que):
    if s >= 300:
        raise SystemExit(f"{que}: HTTP {s} {b.get('error')}")
    return b


def uno(dv, ruta, que):
    filas = ok(*dv.call("GET", ruta)[:2], que).get("value", [])
    if len(filas) > 1:
        raise SystemExit(f"{que}: {len(filas)} filas con el mismo nombre, se esperaba a lo sumo una")
    return filas[0] if filas else None


def norm(x):
    return "".join((x or "").split()).lower()


def fetch_grafico(g):
    cond = []
    for f in g["filtros"]:
        valor = f' value="{f["valor"]}"' if "valor" in f else ""
        cond.append(f'<condition attribute="{f["columna"]}" operator="{f["operador"]}"{valor} />')
    return (f'<fetch mapping="logical" aggregate="true"><entity name="{g["tabla"]}">'
            f'<attribute name="{g["categoria"]}" groupby="true" alias="groupby_column" />'
            f'<attribute name="{g["medida"]}" aggregate="{g["agregado"]}" alias="aggregate_column" />'
            f'<filter type="and">{"".join(cond)}</filter></entity></fetch>')


def datadescription(g):
    return ("<datadefinition><fetchcollection>" + fetch_grafico(g) + "</fetchcollection><categorycollection>"
            "<category><measurecollection><measure alias=\"aggregate_column\" /></measurecollection></category>"
            "</categorycollection></datadefinition>")


def en_solucion(dv, objectid, tipo, tabla=None):
    """Fila propia del componente, o (gráfico) su tabla en la solución con todos los subcomponentes
    (`rootcomponentbehavior` 0): entonces el gráfico viaja con la tabla y la plataforma no le da fila propia."""
    sol = uno(dv, f"solutions?$select=solutionid&$filter=uniquename eq '{SOLUCION}'", "solución")["solutionid"]
    filas = ok(*dv.call("GET", f"solutioncomponents?$select=objectid&$filter=_solutionid_value eq {sol} "
                             f"and componenttype eq {tipo} and objectid eq {objectid}")[:2], "solutioncomponents")
    if filas.get("value"):
        return "en la solución"
    if tabla:
        mid = ok(*dv.call("GET", f"EntityDefinitions(LogicalName='{tabla}')?$select=MetadataId")[:2],
                 "MetadataId")["MetadataId"]
        t = ok(*dv.call("GET", f"solutioncomponents?$select=rootcomponentbehavior&$filter=_solutionid_value eq {sol} "
                             f"and componenttype eq 1 and objectid eq {mid}")[:2], "solutioncomponents")
        if any(f.get("rootcomponentbehavior") == 0 for f in t.get("value", [])):
            return "en la solución (con su tabla, que incluye todos los subcomponentes)"
    return None


def asegurar_en_solucion(dv, objectid, tipo, solo, tabla=None):
    ya = en_solucion(dv, objectid, tipo, tabla)
    if ya:
        return ya
    if solo:
        return "FUERA de la solución"
    ok(*dv.call("POST", "AddSolutionComponent", {
        "ComponentId": objectid, "ComponentType": tipo, "SolutionUniqueName": SOLUCION,
        "AddRequiredComponents": False})[:2], "AddSolutionComponent")
    return "agregado a la solución" if en_solucion(dv, objectid, tipo, tabla) else "FUERA de la solución (falló el alta)"


def grafico(dv, g, solo):
    dd, pd = datadescription(g), PRESENTACION.format(serie=g["serie"])
    fila = uno(dv, "savedqueryvisualizations?$select=savedqueryvisualizationid,name,datadescription,"
                   f"presentationdescription,description&$filter=name eq '{g['nombre']}' "
                   f"and primaryentitytypecode eq '{g['tabla']}'", g["nombre"])
    cuerpo = {"name": g["nombre"], "primaryentitytypecode": g["tabla"], "description": g["descripcion"],
              "datadescription": dd, "presentationdescription": pd, "charttype": 0, "isdefault": False}
    if fila is None:
        if solo:
            return {"nombre": g["nombre"], "estado": "difiere", "detalle": "no existe"}, None
        s, b, h = dv.call("POST", "savedqueryvisualizations", cuerpo, solucion=SOLUCION)
        ok(s, b, f"alta de {g['nombre']}")
        gid, estado = Dataverse.id_creado(h), "creado"
    else:
        gid = fila["savedqueryvisualizationid"]
        difs = [k for k, v in (("datadescription", dd), ("presentationdescription", pd),
                               ("description", g["descripcion"])) if norm(fila.get(k)) != norm(v)]
        if not difs:
            estado = "ya_existia"
        elif solo:
            return {"nombre": g["nombre"], "id": gid, "estado": "difiere", "detalle": difs}, gid
        else:
            ok(*dv.call("PATCH", f"savedqueryvisualizations({gid})",
                        {k: cuerpo[k] for k in difs})[:2], f"corrección de {g['nombre']}")
            estado = "actualizado"
    # La consulta del gráfico corre de verdad (la misma que arma la plataforma al dibujarlo).
    conjunto = ok(*dv.call("GET", f"EntityDefinitions(LogicalName='{g['tabla']}')?$select=EntitySetName")[:2],
                  "EntitySetName")["EntitySetName"]
    s, b, _ = dv.call("GET", f"{conjunto}?fetchXml={fetch_grafico(g)}")
    datos = [{"categoria": r.get("groupby_column@OData.Community.Display.V1.FormattedValue",
                                 "con valor" if r.get("groupby_column") else None),
              "promedio": r.get("aggregate_column")} for r in (b.get("value") or [])] if s == 200 else b
    return {"nombre": g["nombre"], "id": gid, "estado": estado,
            "solucion": asegurar_en_solucion(dv, gid, TIPO_GRAFICO, solo, g["tabla"]),
            "consulta_http": s, "filas_de_la_consulta": len(datos) if s == 200 else None,
            "consulta": datos}, gid


def formxml(p, graficos, vistas, rol_id):
    def gid(*partes):
        return "{" + str(uuid.uuid5(NS, "|".join(partes))) + "}"

    def labels(t):
        return "<labels>" + "".join(f'<label description={quoteattr(t[str(i)])} languagecode="{i}" />'
                                    for i in (1033, 3082)) + "</labels>"

    filas_xml = []
    for nro in sorted({c["fila"] for c in p["componentes"]}):
        celdas = []
        for c in [c for c in p["componentes"] if c["fila"] == nro]:
            clave = c["vista"]
            vis = f"<VisualizationId>{{{graficos[c['grafico']].upper()}}}</VisualizationId>" if c["modo"] == "Chart" \
                else "<VisualizationId />"
            celdas.append(
                f'<cell colspan="2" rowspan="12" showlabel="true" id="{gid("celda", clave)}" auto="false">'
                + labels(c["titulo"]) +
                f'<control id="{"Componente_" + uuid.uuid5(NS, clave).hex[:8]}" uniqueid="{gid("control", clave)}" '
                f'classid="{SUBGRID}" indicationOfSubgrid="true"><parameters>'
                f"<TargetEntityType>{c['tabla']}</TargetEntityType><ChartGridMode>{c['modo']}</ChartGridMode>"
                "<EnableQuickFind>false</EnableQuickFind><EnableViewPicker>false</EnableViewPicker>"
                "<EnableJumpBar>false</EnableJumpBar><RecordsPerPage>10</RecordsPerPage><HeaderColorCode />"
                f"<ViewId>{{{vistas[c['vista']].upper()}}}</ViewId><IsUserView>false</IsUserView><ViewIds />"
                f"<AutoExpand>Fixed</AutoExpand>{vis}<IsUserChart>false</IsUserChart>"
                "<EnableChartPicker>false</EnableChartPicker><RelationshipName /></parameters></control></cell>")
        filas_xml.append("<row>" + "".join(celdas) + "</row>" + "<row />" * 11)
    return ('<form><tabs><tab showlabel="false" verticallayout="true" id="' + gid("tab") + '">'
            + labels({"1033": "Tab", "3082": "Pestana"}) + '<columns><column width="100%"><sections>'
            '<section showlabel="false" showbar="false" columns="1111" id="' + gid("seccion") + '">'
            + labels({"1033": "Section", "3082": "Seccion"}) + "<rows>" + "".join(filas_xml) + "</rows></section>"
            "</sections></column></columns></tab></tabs>"
            f'<DisplayConditions FallbackForm="true"><Role Id="{{{rol_id.upper()}}}" /></DisplayConditions></form>')


def huella_panel(xml):
    """Lo que importa del formxml, inmune a cómo lo reescriba la plataforma."""
    xml = xml or ""
    return {"vistas": sorted(v.lower() for v in re.findall(r"<ViewId>\{?([0-9a-fA-F-]{36})", xml)),
            "graficos": sorted(v.lower() for v in re.findall(r"<VisualizationId>\{?([0-9a-fA-F-]{36})", xml)),
            "modos": sorted(re.findall(r"<ChartGridMode>(\w+)<", xml)),
            "tablas": sorted(re.findall(r"<TargetEntityType>(\w+)<", xml)),
            "roles": sorted(r.lower() for r in re.findall(r'<Role Id="\{?([0-9a-fA-F-]{36})', xml)),
            # `systemforms` devuelve el formxml solo en el idioma del usuario (1033 en Dev): el 3082 se verifica
            # contra la solución exportada.
            "titulos": sorted(re.findall(r'<label description="([^"]*)" languagecode="1033" />',
                                         " ".join(re.findall(r"<cell[^>]*>\s*<labels>(.*?)</labels>", xml))))}


def panel(dv, p, ids_graficos, solo):
    vistas = {}
    for c in p["componentes"]:
        v = uno(dv, f"savedqueries?$select=savedqueryid&$filter=name eq '{c['vista']}' "
                    f"and returnedtypecode eq '{c['tabla']}'", c["vista"])
        if v is None:
            raise SystemExit(f"falta la vista '{c['vista']}' de {c['tabla']}: hay que construirla antes")
        vistas[c["vista"]] = v["savedqueryid"]
    rol = uno(dv, f"roles?$select=roleid&$filter=name eq '{p['rol_que_lo_ve']}' and parentroleid eq null",
              p["rol_que_lo_ve"])
    xml = formxml(p, dict(ids_graficos), vistas, rol["roleid"])
    fila = uno(dv, "systemforms?$select=formid,formxml,description&$filter=type eq 0 "
                   f"and name eq '{p['nombre']}'", p["nombre"])
    if fila is None:
        if solo:
            return {"nombre": p["nombre"], "estado": "difiere", "detalle": "no existe"}
        s, b, h = dv.call("POST", "systemforms", {"name": p["nombre"], "type": 0, "objecttypecode": "none",
                                                  "description": p["descripcion"], "formxml": xml,
                                                  "isdefault": False}, solucion=SOLUCION)
        ok(s, b, f"alta de {p['nombre']}")
        fid, estado = Dataverse.id_creado(h), "creado"
    else:
        fid = fila["formid"]
        if huella_panel(fila.get("formxml")) == huella_panel(xml) and norm(fila.get("description")) == \
                norm(p["descripcion"]):
            estado = "ya_existia"
        elif solo:
            return {"nombre": p["nombre"], "id": fid, "estado": "difiere",
                    "detalle": {"entorno": huella_panel(fila.get("formxml")), "playbook": huella_panel(xml)}}
        else:
            ok(*dv.call("PATCH", f"systemforms({fid})", {"formxml": xml, "description": p["descripcion"]})[:2],
               f"corrección de {p['nombre']}")
            estado = "actualizado"
    if estado != "ya_existia":
        ok(*dv.call("POST", "PublishXml", {"ParameterXml": "<importexportxml><dashboards>"
                                                          f"<dashboard>{fid}</dashboard></dashboards></importexportxml>"})[:2],
           "publicación del dashboard")
    publicado = uno(dv, f"systemforms?$select=formxml&$filter=formid eq {fid}", "relectura")
    return {"nombre": p["nombre"], "id": fid, "estado": estado,
            "solucion": asegurar_en_solucion(dv, fid, TIPO_DASHBOARD, solo),
            "huella_publicada": huella_panel(publicado.get("formxml")),
            "coincide_con_playbook": huella_panel(publicado.get("formxml")) == huella_panel(xml)}


def main():
    solo = "--solo-verificar" in sys.argv
    dv = Dataverse()
    out, ids = {"fecha": "2026-10-20", "solo_verificar": solo, "graficos": []}, {}
    for ruta in PB_GRAFICOS:
        g = bloque(ruta)
        r, gid = grafico(dv, g, solo)
        out["graficos"].append(r)
        if gid:
            ids[g["nombre"]] = gid
        print(f"{r['nombre']}: {r['estado']} · {r.get('solucion')} · consulta HTTP {r.get('consulta_http')} "
              f"con {r.get('filas_de_la_consulta')} filas")
    p = bloque(PB_PANEL)
    if len(ids) == len(PB_GRAFICOS):
        r = panel(dv, p, ids, solo)
        out["panel"] = r
        print(f"{r['nombre']}: {r['estado']} · {r.get('solucion')} · coincide {r.get('coincide_con_playbook')}")
    os.makedirs(EVID, exist_ok=True)
    nombre = "02_verificacion.json" if solo else "01_graficos_panel.json"
    with open(os.path.join(EVID, nombre), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    if not solo:
        estado = {"graficos": {k: v for k, v in ids.items()},
                  "panel": (out.get("panel") or {}).get("id")}
        with open(ESTADO, "w", encoding="utf-8") as f:
            json.dump(estado, f, ensure_ascii=False, indent=1)
    malos = [x for x in out["graficos"] + [out.get("panel") or {}]
             if x.get("estado") not in ("creado", "ya_existia", "actualizado")
             or "FUERA" in str(x.get("solucion"))]
    sys.exit(1 if malos else 0)


if __name__ == "__main__":
    main()
