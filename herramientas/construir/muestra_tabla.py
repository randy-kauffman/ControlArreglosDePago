#!/usr/bin/env python3
"""Comprobación independiente de las tablas: exporta la solución unmanaged por
el Web API (`ExportSolution`) y compara cada playbook de `playbooks/tabla/`
contra el XML de `customizations.xml`. Es otro camino que el de `tabla.py`
(que verifica por `EntityDefinitions`): si los dos coinciden, la tabla está
bien. Solo lee.

    python3 herramientas/construir/muestra_tabla.py [<playbook.md> …] [--guardar]

Sin playbooks, compara todos los de `<carpeta de playbooks>/tabla/` (no los de `ensayos/`); la carpeta sale de `proyecto.json`.
Con `--guardar`, deja el XML real de cada tabla en `<carpeta de playbooks>/tabla/muestras/`.

Lo que la skill 0.2.0 agregó (columnas `decimal` y `formula`, motivos de estado) no tiene todavía una muestra real de cómo se ve en el XML
exportado: la herramienta no inventa esa forma. Lo nombra como «sin comparar» (`no_comparables`) y el playbook no sale `OK` hasta que una
muestra real fije la comparación.
"""
import base64
import glob
import io
import json
import os
import sys
import xml.etree.ElementTree as ET
import zipfile

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _comun import carpeta_playbooks, dividir_secciones, escribir_metadatos, leer_texto, obtener_componente, obtener_identidad  # noqa: E402

TIPO = "tabla"
COMPONENTE_TABLA = "1"
# Cómo se ve cada tipo del playbook en el XML de la solución (observado, 2026-09-20).
TIPO_XML = {"texto": "nvarchar", "autonumerico": "nvarchar", "memo": "ntext", "entero": "int", "choice": "picklist",
            "sino": "bit", "fecha": "datetime", "fechahora": "datetime", "archivo": "file"}
PROPIEDAD_XML = {"usuario": "UserOwned", "organizacion": "OrgOwned"}
TIPOS_SIN_MUESTRA = ("decimal", "formula")  # skill 0.2.0: sin exportación real de cómo se ven en el XML
NO_DECLARABLES = {"lookup", "owner", "primarykey", "uniqueidentifier", "virtual"}


def _etiquetas(nodo, contenedor, hijo):
    c = nodo.find(contenedor) if nodo is not None else None
    return {} if c is None else {int(e.get("languagecode")): e.get("description") or "" for e in c.findall(hijo)}


def comparar_con_xml(customizations_xml, datos, idiomas):
    raiz = ET.fromstring(customizations_xml)
    ent = next((e for e in raiz.iter("entity") if e.get("Name") == datos["nombre"]), None)
    if ent is None:
        return [f"{datos['nombre']} no está en la solución exportada"]
    difs = []

    def texto(etiquetas, esperado, que):
        """`esperado`: `{"1033": ..., "3082": ...}` (un nombre visible) o una cadena (una descripción: el mismo texto en cada idioma).
        Se compara cada idioma de `idiomas` y también se denuncia una etiqueta en un idioma que el playbook no declara."""
        de_mas = sorted(k for k in etiquetas if k not in idiomas)
        if de_mas:
            difs.append(f"{que}: tiene etiquetas en un idioma que el playbook no declara {de_mas}")
        for lcid in idiomas:
            quiere = esperado if isinstance(esperado, str) else esperado[str(lcid)]
            if etiquetas.get(lcid, "") != quiere:
                difs.append(f"{que}[{lcid}]: xml={etiquetas.get(lcid)!r} playbook={quiere!r}")

    def igual(nodo, etiqueta, esperado, que):
        real = (nodo.findtext(etiqueta) or "").strip()
        if real != str(esperado):
            difs.append(f"{que}: xml={real!r} playbook={str(esperado)!r}")

    texto(_etiquetas(ent, "LocalizedNames", "LocalizedName"), datos["displayname"], "displayname")
    texto(_etiquetas(ent, "LocalizedCollectionNames", "LocalizedCollectionName"), datos["displayname_plural"], "displayname_plural")
    texto(_etiquetas(ent, "Descriptions", "Description"), datos["descripcion"], "descripcion")
    igual(ent, "OwnershipTypeMask", PROPIEDAD_XML[datos["propiedad"]], "propiedad")
    igual(ent, "IsAuditEnabled", int(datos["auditoria"]), "auditoria de la tabla")

    cont = ent.find("attributes")
    attrs = {a.findtext("LogicalName"): a for a in ([] if cont is None else list(cont))}
    p = datos["primaria"]
    declaradas = [{**p, "tipo": "autonumerico" if p["autonumerico"] else "texto", "formato": p["autonumerico"],
                   "protegida": False, "auditoria": datos["auditoria"], "_clave_formato": "autonumerico"}] + list(datos["columnas"])
    for c in declaradas:
        n, a = c["nombre"], attrs.get(c["nombre"])
        if a is None:
            difs.append(f"falta la columna {n}")
            continue
        tipo = c["tipo"]
        if tipo in TIPOS_SIN_MUESTRA:
            continue  # presente en el XML, pero su forma no se conoce: ver `no_comparables`
        igual(a, "Type", TIPO_XML[tipo], f"{n}.tipo")
        if (a.findtext("Type") or "") != TIPO_XML[tipo]:
            continue
        igual(a, "RequiredLevel", "required" if c["requerida"] else "none", f"{n}.requerida")
        igual(a, "IsSecured", int(c["protegida"]), f"{n}.protegida")
        igual(a, "IsAuditEnabled", int(c["auditoria"]), f"{n}.auditoria")
        texto(_etiquetas(a, "displaynames", "displayname"), c["displayname"], f"{n}.displayname")
        texto(_etiquetas(a, "Descriptions", "Description"), c["descripcion"], f"{n}.descripcion")
        if tipo in ("texto", "autonumerico", "memo"):
            igual(a, "MaxLength", c["largo"], f"{n}.largo")
        if tipo in ("texto", "autonumerico"):
            igual(a, "AutoNumberFormat", c.get("formato") or "", f"{n}.{c.get('_clave_formato', 'formato')}")
        elif tipo == "entero":
            igual(a, "MinValue", c["minimo"], f"{n}.minimo")
            igual(a, "MaxValue", c["maximo"], f"{n}.maximo")
        elif tipo == "choice":
            igual(a, "OptionSetName", c["choice"], f"{n}.choice")
        elif tipo == "sino":
            igual(a, "AppDefaultValue", int(c["defecto"]), f"{n}.defecto")
        elif tipo == "archivo":
            igual(a, "MaxValue", c["tamano_kb"], f"{n}.tamano_kb")  # en el XML el tamaño de un archivo es <MaxValue>
        elif tipo in ("fecha", "fechahora"):
            igual(a, "Format", "date" if tipo == "fecha" else "datetime", f"{n}.tipo (formato de la fecha)")
            igual(a, "Behavior", 2 if tipo == "fecha" else 1, f"{n}.tipo (comportamiento: 1 usuario local, 2 fecha sola)")

    nombres = {c["nombre"] for c in declaradas}
    for n, a in sorted(attrs.items()):
        if a.findtext("IsCustomField") == "1" and n not in nombres and (a.findtext("Type") or "") not in NO_DECLARABLES:
            difs.append(f"columna propia que el playbook no declara: {n} ({a.findtext('Type')})")
    return difs


def evaluar(customizations_xml, en_solucion, datos, idiomas):
    """El veredicto de UN playbook: `("OK", [])`, `("DIFIERE", diferencias)` o `("SIN COMPARAR", lo que no se pudo comparar)`. Solo `OK` cuenta
    como coincidencia: una tabla con algo sin comparar no sale verde por no haber encontrado diferencias en lo que sí se comparó."""
    difs = comparar_con_xml(customizations_xml, datos, idiomas)
    if datos["nombre"] not in en_solucion:
        difs.append("no figura como RootComponent type=1 en solution.xml")
    if difs:
        return "DIFIERE", difs
    sin_comparar = no_comparables(datos)
    return ("SIN COMPARAR", sin_comparar) if sin_comparar else ("OK", [])


def no_coinciden(veredictos):
    """Cuántos veredictos no son `OK`. `SIN COMPARAR` también cuenta: la herramienta no sale con código 0 si algo quedó sin comparar."""
    return sum(1 for v in veredictos if v != "OK")


def no_comparables(datos):
    """Lo que esta herramienta NO sabe comparar contra el XML por falta de una muestra real (skill 0.2.0). Cada ítem lo dice con la misma frase."""
    cosas = [f"{c['nombre']} (columna {c['tipo']})" for c in datos["columnas"] if c["tipo"] in TIPOS_SIN_MUESTRA]
    if datos.get("motivos_estado") is not None:
        cosas.append("motivos_estado (motivos de estado de statuscode)")
    return [f"{cosa}: sin muestra real de cómo se ve en el XML exportado; se compara cuando un ciclo asiente la forma" for cosa in cosas]


def fragmento(customizations_xml, nombre):
    raiz = ET.fromstring(customizations_xml)
    for e in raiz.iter("Entity"):
        if e.findtext("Name") == nombre:
            return ET.tostring(e, encoding="unicode")
    return None


def main():
    from dataverse_api import Dataverse

    argv = sys.argv[1:]
    guardar = "--guardar" in argv
    carpeta = os.path.join(carpeta_playbooks(), "tabla")
    rutas = [a for a in argv if not a.startswith("--")] or sorted(glob.glob(os.path.join(carpeta, "*.md")))
    playbooks = []
    for ruta in rutas:
        sec = dividir_secciones(leer_texto(ruta))
        playbooks.append((obtener_componente(sec, TIPO), obtener_identidad(sec)))
    soluciones = {i["solucion"] for _, i in playbooks}
    if len(soluciones) != 1:
        print(f"los playbooks nombran {len(soluciones)} soluciones: {sorted(soluciones)}; se compara de a una")
        return 1
    solucion = soluciones.pop()
    est, cuerpo, _ = escribir_metadatos(Dataverse(), "POST", "ExportSolution", {"SolutionName": solucion, "Managed": False}, timeout=900)
    if est != 200 or not isinstance(cuerpo, dict) or not isinstance(cuerpo.get("ExportSolutionFile"), str):
        print(f"ExportSolution devolvió HTTP {est} o una forma inesperada")
        return 1
    z = zipfile.ZipFile(io.BytesIO(base64.b64decode(cuerpo["ExportSolutionFile"])))
    custom = z.read("customizations.xml").decode("utf-8")
    en_solucion = {r.get("schemaName") for r in ET.fromstring(z.read("solution.xml")).iter("RootComponent") if r.get("type") == COMPONENTE_TABLA}
    veredictos = []
    for datos, identidad in playbooks:
        resultado, textos = evaluar(custom, en_solucion, datos, identidad["idiomas"])
        print(f"{resultado:<12} {datos['nombre']}" + ("" if not textos else ": " + "; ".join(textos)))
        veredictos.append(resultado)
        if guardar:
            frag = fragmento(custom, datos["nombre"])
            if frag:
                os.makedirs(os.path.join(carpeta, "muestras"), exist_ok=True)
                with open(os.path.join(carpeta, "muestras", f"{datos['nombre']}.solucion.xml"), "w", encoding="utf-8") as f:
                    f.write(frag + "\n")
    malos = no_coinciden(veredictos)
    print(json.dumps({"playbooks": len(playbooks), "coinciden": len(playbooks) - malos, "no_coinciden": malos}))
    return 0 if not malos else 1


if __name__ == "__main__":
    sys.exit(main())
