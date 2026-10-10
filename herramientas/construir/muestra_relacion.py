#!/usr/bin/env python3
"""Comprobación independiente de las relaciones: exporta la solución unmanaged
por el Web API (`ExportSolution`) y compara cada playbook de la carpeta
`relacion/` (dentro de la carpeta de playbooks que dice la clave `playbooks` de
`herramientas/proyecto.json`) contra `customizations.xml` (la relación, en
`<EntityRelationships>`; su lookup, dentro de la tabla hija). Es otro camino
que el de `relacion.py`. Solo lee.

    python3 herramientas/construir/muestra_relacion.py [<playbook.md> …] [--guardar]

Variante B (tabla hija estándar, por ejemplo `email`; exportación real del
2026-10-09): el XML nombra las tablas por su nombre de esquema (`Email`,
`SystemUser`) y el playbook por el lógico, así que las tablas se comparan sin
distinguir mayúsculas; la entidad Email trae SOLO nuestros atributos; y además se
mira `solution.xml`: la tabla estándar tiene que figurar exactamente una vez como
`<RootComponent type="1">` con `behavior` distinto de 0. Con `--guardar`, una
hija estándar deja en `muestras/` también la entidad (`<tabla>.solucion.xml`) y
el `solution.xml` (`<relación>.solution.xml`).
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

from _comun import carpeta_playbooks as _comun_carpeta_playbooks, dividir_secciones, escribir_metadatos, leer_texto, obtener_componente, obtener_identidad  # noqa: E402
from credenciales import RAIZ_PROYECTO, cargar_configuracion  # noqa: E402
from relacion import CASCADAS, es_hija_estandar  # noqa: E402

TIPO = "relacion"
# En el XML no existe CascadeMerge; las demás acciones se llaman Cascade<Acción>.
ACCIONES_XML = ["Assign", "Delete", "Reparent", "Share", "Unshare", "RollupView"]


def carpeta_playbooks(config=None):
    """Carpeta de los playbooks de relación: `<raíz del proyecto>/<playbooks>/relacion`, con `playbooks` de `proyecto.json`."""
    return os.path.join(_comun_carpeta_playbooks(config), "relacion")


def _etiquetas(nodo, contenedor, hijo, idiomas, que, esperado, difs):
    """Compara las etiquetas de un nombre o descripción con las del playbook, en los dos sentidos: `esperado` es
    `{"1033": ..., "3082": ...}`; falta o difiere el texto de un idioma del playbook, o el XML trae un idioma que el playbook no declara."""
    c = nodo.find(contenedor) if nodo is not None else None
    etiquetas = {} if c is None else {int(e.get("languagecode")): e.get("description") or "" for e in c.findall(hijo)}
    for lcid in idiomas:
        if etiquetas.get(lcid, "") != esperado[str(lcid)]:
            difs.append(f"{que}[{lcid}]: xml={etiquetas.get(lcid)!r} playbook={esperado[str(lcid)]!r}")
    de_mas = sorted(set(etiquetas) - set(idiomas))
    if de_mas:
        difs.append(f"{que}: tiene etiquetas en un idioma que el playbook no declara {de_mas}")


def _comparar_root_component(solution_xml, tabla, difs):
    """La tabla estándar de la variante B figura en `solution.xml` exactamente una vez como `<RootComponent type="1">` con `behavior` distinto de 0
    (0 la arrastraría entera a la solución)."""
    raices = [r for r in ET.fromstring(solution_xml).iter("RootComponent") if r.get("type") == "1" and (r.get("schemaName") or "").lower() == tabla.lower()]
    if not raices:
        difs.append(f"tabla_hija: '{tabla}' no figura como RootComponent type=\"1\" en solution.xml")
    elif len(raices) > 1:
        difs.append(f"tabla_hija: '{tabla}' figura {len(raices)} veces como RootComponent type=\"1\" en solution.xml, se esperaba 1")
    elif raices[0].get("behavior") == "0":
        difs.append(f"tabla_hija: RootComponent de '{tabla}' con behavior=\"0\" en solution.xml: tiene que ser distinto de 0")


def comparar_con_xml(customizations_xml, datos, idiomas, solution_xml=None):
    """Diferencias entre el playbook y el XML exportado (lista vacía = coincide). `solution_xml` (el `solution.xml` del zip) se pasa solo para una hija
    estándar (variante B): sin él no se mira el `RootComponent`."""
    raiz = ET.fromstring(customizations_xml)
    rel = next((r for r in raiz.iter("EntityRelationship") if r.get("Name") == datos["nombre"]), None)
    if rel is None:
        return [f"{datos['nombre']} no está en la solución exportada"]
    difs, k = [], datos["lookup"]

    def igual(nodo, etiqueta, esperado, que, sin_mayusculas=False):
        real = (nodo.findtext(etiqueta) or "").strip()
        if (real.lower() if sin_mayusculas else real) != str(esperado):
            difs.append(f"{que}: xml={real!r} playbook={str(esperado)!r}")

    igual(rel, "EntityRelationshipType", "OneToMany", "tipo de relación")
    # El formato nunca cubre una relación jerárquica (tampoco la autorreferida, variante A): en el XML es 0.
    igual(rel, "IsHierarchical", 0, "IsHierarchical")
    # El XML nombra las tablas por su nombre de esquema (`SystemUser`); el playbook, por el lógico.
    igual(rel, "ReferencedEntityName", datos["tabla_padre"], "tabla_padre", sin_mayusculas=True)
    igual(rel, "ReferencingEntityName", datos["tabla_hija"], "tabla_hija", sin_mayusculas=True)
    igual(rel, "ReferencingAttributeName", k["nombre"], "lookup.nombre")
    for accion in ACCIONES_XML:
        igual(rel, f"Cascade{accion}", CASCADAS[datos["comportamiento"]][accion], f"comportamiento ({accion})")

    if solution_xml is not None:
        _comparar_root_component(solution_xml, datos["tabla_hija"], difs)
    # La entidad se nombra por su nombre de esquema (`Email`): se busca sin distinguir mayúsculas.
    hija = next((e for e in raiz.iter("entity") if (e.get("Name") or "").lower() == datos["tabla_hija"].lower()), None)
    attrs = hija.find("attributes") if hija is not None else None
    col = None if attrs is None else next((a for a in attrs if a.findtext("LogicalName") == k["nombre"]), None)
    if col is None:
        difs.append(f"falta la columna lookup {k['nombre']} en {datos['tabla_hija']}")
        return difs
    igual(col, "Type", "lookup", "lookup.tipo")
    igual(col, "RequiredLevel", "required" if k["requerida"] else "none", "lookup.requerida")
    igual(col, "IsAuditEnabled", int(k["auditoria"]), "lookup.auditoria")
    igual(col, "IsSecured", 0, "lookup.protegida")
    _etiquetas(col, "displaynames", "displayname", idiomas, "lookup.displayname", k["displayname"], difs)
    # La descripción es prosa: el mismo texto en cada idioma.
    _etiquetas(col, "Descriptions", "Description", idiomas, "lookup.descripcion", {str(i): k["descripcion"] for i in idiomas}, difs)
    return difs


def fragmentos_a_guardar(customizations_xml, solution_xml, datos, estandar):
    """Lo que `--guardar` deja en `muestras/`, como `{nombre de archivo: contenido}`: el `<EntityRelationship>` de la relación (`<relación>.solucion.xml`) y,
    si la hija es estándar, también su `<Entity>` (`<tabla>.solucion.xml`, con SOLO nuestros atributos) y el `solution.xml` completo
    (`<relación>.solution.xml`). Si la relación no está en la exportación no guarda nada."""
    raiz = ET.fromstring(customizations_xml)
    rel = next((r for r in raiz.iter("EntityRelationship") if r.get("Name") == datos["nombre"]), None)
    if rel is None:
        return {}
    fragmentos = {f"{datos['nombre']}.solucion.xml": ET.tostring(rel, encoding="unicode").rstrip() + "\n"}
    if estandar:
        entidad = next((e for e in raiz.iter("Entity") if (e.findtext("Name") or "").lower() == datos["tabla_hija"].lower()), None)
        if entidad is not None:
            fragmentos[f"{datos['tabla_hija']}.solucion.xml"] = ET.tostring(entidad, encoding="unicode").rstrip() + "\n"
        fragmentos[f"{datos['nombre']}.solution.xml"] = solution_xml
    return fragmentos


def main():
    from dataverse_api import Dataverse

    argv = sys.argv[1:]
    guardar = "--guardar" in argv
    carpeta = carpeta_playbooks()
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
    zip_solucion = zipfile.ZipFile(io.BytesIO(base64.b64decode(cuerpo["ExportSolutionFile"])))
    custom = zip_solucion.read("customizations.xml").decode("utf-8-sig")
    solution = zip_solucion.read("solution.xml").decode("utf-8-sig")
    malos = 0
    for datos, identidad in playbooks:
        estandar = es_hija_estandar(datos, identidad)
        difs = comparar_con_xml(custom, datos, identidad["idiomas"], solution_xml=solution if estandar else None)
        print(f"{'OK      ' if not difs else 'DIFIERE '} {datos['nombre']}" + ("" if not difs else ": " + "; ".join(difs)))
        malos += bool(difs)
        if guardar:
            fragmentos = fragmentos_a_guardar(custom, solution, datos, estandar)
            if fragmentos:
                os.makedirs(os.path.join(carpeta, "muestras"), exist_ok=True)
            for nombre, contenido in fragmentos.items():
                with open(os.path.join(carpeta, "muestras", nombre), "w", encoding="utf-8") as f:
                    f.write(contenido)
    print(json.dumps({"playbooks": len(playbooks), "coinciden": len(playbooks) - malos, "difieren": malos}))
    return 0 if not malos else 1


if __name__ == "__main__":
    sys.exit(main())
