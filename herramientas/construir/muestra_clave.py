#!/usr/bin/env python3
"""Comprobación independiente de las claves alternativas: exporta la solución
unmanaged por el Web API (`ExportSolution`) y compara cada playbook de
`<playbooks>/clave/` contra `customizations.xml` (la clave, en `<EntityKeys>`
dentro de su tabla). Es otro camino que el de `clave.py`. Solo lee.

La carpeta de playbooks sale de la clave `playbooks` de `herramientas/proyecto.json`
(relativa a la raíz del proyecto). El nombre visible se compara en CADA idioma
de `idiomas` del playbook, en los dos sentidos: el idioma cuyo texto falta o
difiere, y una etiqueta en un idioma que el playbook no declara.

    python3 herramientas/construir/muestra_clave.py [<playbook.md> …] [--guardar]

El XML no dice si el índice está activo: eso solo lo sabe el Web API
(`EntityKeyIndexStatus`), y lo comprueba `clave.py --solo-verificar`.
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

TIPO = "clave"


def carpeta_playbooks(tipo, config=None):
    """Carpeta de los playbooks de un tipo: la clave `playbooks` de `proyecto.json` (relativa a la raíz del proyecto, o
    absoluta) más el nombre del tipo."""
    return os.path.join(_comun_carpeta_playbooks(config), tipo)


def _buscar(raiz, datos):
    tabla = next((e for e in raiz.iter("entity") if (e.get("Name") or "").lower() == datos["tabla"]), None)
    claves = None if tabla is None else tabla.find("EntityKeys")
    return None if claves is None else next((k for k in claves.findall("EntityKey") if k.findtext("LogicalName") == datos["nombre"]), None)


def comparar_con_xml(customizations_xml, datos, idiomas):
    """Lista de diferencias (vacía si coincide) entre el playbook y la clave en `customizations.xml`; el nombre visible
    se compara en cada idioma de `idiomas`."""
    clave = _buscar(ET.fromstring(customizations_xml), datos)
    if clave is None:
        return [f"{datos['nombre']} no está en la solución exportada, dentro de {datos['tabla']}"]
    difs = []
    contenedor = clave.find("EntityKeyAttributes")
    columnas = [] if contenedor is None else [(a.text or "").strip() for a in contenedor.findall("AttributeName")]
    # La plataforma escribe las columnas en su propio orden: la unicidad no depende del orden.
    if sorted(columnas) != sorted(datos["columnas"]):
        difs.append(f"columnas: xml={sorted(columnas)!r} playbook={sorted(datos['columnas'])!r}")
    nombres = clave.find("displaynames")
    etiquetas = {} if nombres is None else {int(e.get("languagecode")): e.get("description") or "" for e in nombres.findall("displayname")}
    for lcid in idiomas:
        if etiquetas.get(lcid, "") != datos["displayname"][str(lcid)]:
            difs.append(f"displayname[{lcid}]: xml={etiquetas.get(lcid)!r} playbook={datos['displayname'][str(lcid)]!r}")
    de_mas = sorted(k for k in etiquetas if k not in idiomas)
    if de_mas:
        difs.append(f"displayname: tiene etiquetas en un idioma que el playbook no declara {de_mas}")
    return difs


def main(argv=None, fabrica_cliente=None, config=None):
    """`fabrica_cliente` y `config` se inyectan solo en las pruebas: por defecto, `dataverse_api.Dataverse` y `proyecto.json`."""
    argv = sys.argv[1:] if argv is None else argv
    guardar = "--guardar" in argv
    carpeta = carpeta_playbooks(TIPO, config)
    rutas = [a for a in argv if not a.startswith("--")] or sorted(glob.glob(os.path.join(carpeta, "*.md")))
    if not rutas:
        print(f"no hay playbooks en '{carpeta}' (se lee de la clave 'playbooks' de herramientas/proyecto.json)")
        return 1
    playbooks = []
    for ruta in rutas:
        sec = dividir_secciones(leer_texto(ruta))
        playbooks.append((obtener_componente(sec, TIPO), obtener_identidad(sec)))
    soluciones = {i["solucion"] for _, i in playbooks}
    if len(soluciones) != 1:
        print(f"los playbooks nombran {len(soluciones)} soluciones: {sorted(soluciones)}; se compara de a una")
        return 1
    solucion = soluciones.pop()
    if fabrica_cliente is None:
        from dataverse_api import Dataverse as fabrica_cliente  # import tardío: no hace falta para las pruebas
    est, cuerpo, _ = escribir_metadatos(fabrica_cliente(), "POST", "ExportSolution", {"SolutionName": solucion, "Managed": False}, timeout=900)
    if est != 200 or not isinstance(cuerpo, dict) or not isinstance(cuerpo.get("ExportSolutionFile"), str):
        print(f"ExportSolution devolvió HTTP {est} o una forma inesperada")
        return 1
    custom = zipfile.ZipFile(io.BytesIO(base64.b64decode(cuerpo["ExportSolutionFile"]))).read("customizations.xml").decode("utf-8")
    raiz = ET.fromstring(custom)
    malos = 0
    for datos, identidad in playbooks:
        difs = comparar_con_xml(custom, datos, identidad["idiomas"])
        print(f"{'OK      ' if not difs else 'DIFIERE '} {datos['nombre']}" + ("" if not difs else ": " + "; ".join(difs)))
        malos += bool(difs)
        if guardar:
            clave = _buscar(raiz, datos)
            if clave is not None:
                muestras = os.path.join(carpeta, "muestras")
                os.makedirs(muestras, exist_ok=True)
                with open(os.path.join(muestras, f"{datos['nombre']}.solucion.xml"), "w", encoding="utf-8") as f:
                    f.write(ET.tostring(clave, encoding="unicode") + "\n")
    print(json.dumps({"playbooks": len(playbooks), "coinciden": len(playbooks) - malos, "difieren": malos}))
    return 0 if not malos else 1


if __name__ == "__main__":
    sys.exit(main())
