#!/usr/bin/env python3
"""Comprobación independiente de los choices globales: exporta la solución
unmanaged por el Web API (acción `ExportSolution`) y compara cada playbook de
`<playbooks>/choice-global/` contra el XML de `customizations.xml`. Es otro
camino que el de la herramienta de construcción (que verifica por
`GlobalOptionSetDefinitions`): si los dos coinciden, el componente está bien.

La carpeta de playbooks sale de la clave `playbooks` de `herramientas/proyecto.json`
(relativa a la raíz del proyecto). Cada nombre visible y cada descripción se
compara en CADA idioma de `idiomas` del playbook, en los dos sentidos: el idioma
cuyo texto falta o difiere, y un texto en un idioma que el playbook no declara.

Solo lee. Uso:
    python3 herramientas/construir/muestra_choice.py            # compara todos
    python3 herramientas/construir/muestra_choice.py --guardar  # además guarda el XML de cada uno en muestras/
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

TIPO = "choice-global"
COMPONENTE_CHOICE = "9"  # tipo de componente de un choice global en solution.xml


def carpeta_playbooks(tipo, config=None):
    """Carpeta de los playbooks de un tipo: la clave `playbooks` de `proyecto.json` (relativa a la raíz del proyecto, o
    absoluta) más el nombre del tipo."""
    return os.path.join(_comun_carpeta_playbooks(config), tipo)


def _etiquetas(nodo, contenedor, hijo):
    """{languagecode: texto} de `<contenedor><hijo description languagecode/>`."""
    c = nodo.find(contenedor)
    if c is None:
        return {}
    return {int(e.get("languagecode")): e.get("description") or "" for e in c.findall(hijo)}


def comparar_con_xml(customizations_xml, datos, idiomas):
    """Lista de diferencias (vacía si coincide) entre el playbook y el
    `<optionset>` de ese nombre en `customizations.xml`, en cada idioma de
    `idiomas`. La descripción es prosa: el mismo texto en cada idioma."""
    raiz = ET.fromstring(customizations_xml)
    nodo = next((o for o in raiz.iter("optionset") if o.get("Name") == datos["nombre"]), None)
    if nodo is None:
        return [f"{datos['nombre']} no está en la solución exportada"]
    difs = []

    def textos(etiquetas, esperado, que):
        """`esperado`: {"1033": ..., "3082": ...} (una clave por cada idioma de `idiomas`)."""
        for lcid in idiomas:
            if etiquetas.get(lcid, "") != esperado[str(lcid)]:
                difs.append(f"{que}[{lcid}]: xml={etiquetas.get(lcid)!r} playbook={esperado[str(lcid)]!r}")
        de_mas = sorted(k for k in etiquetas if k not in idiomas)
        if de_mas:
            difs.append(f"{que}: tiene etiquetas en un idioma que el playbook no declara {de_mas}")

    def uniforme(texto):
        return {str(i): texto for i in idiomas}

    if (nodo.findtext("IsGlobal") or "").strip() != "1":
        difs.append(f"IsGlobal: xml={nodo.findtext('IsGlobal')!r} esperado='1'")
    if (nodo.findtext("OptionSetType") or "").strip().lower() != "picklist":
        difs.append(f"OptionSetType: xml={nodo.findtext('OptionSetType')!r} esperado='picklist'")
    textos(_etiquetas(nodo, "displaynames", "displayname"), datos["displayname"], "displayname")
    textos(_etiquetas(nodo, "Descriptions", "Description"), uniforme(datos["descripcion"]), "descripcion")
    # El atributo del elemento raíz duplica el display name del idioma base del entorno de origen, que el XML no dice
    # cuál es: tiene que ser el de alguno de los idiomas del playbook.
    posibles = [datos["displayname"][str(i)] for i in idiomas]
    if nodo.get("localizedName") not in posibles:
        difs.append(f"atributo localizedName: xml={nodo.get('localizedName')!r} no es el displayname de ningún idioma del playbook {posibles!r}")

    cont = nodo.find("options")
    opciones = [] if cont is None else list(cont)
    if len(opciones) != len(datos["opciones"]):
        difs.append(f"cantidad de opciones: xml={len(opciones)} playbook={len(datos['opciones'])}")
    for i, (op_x, op_p) in enumerate(zip(opciones, datos["opciones"])):
        if op_x.get("value") != str(op_p["valor"]):
            difs.append(f"opciones[{i}].valor: xml={op_x.get('value')!r} playbook={op_p['valor']!r}")
        textos(_etiquetas(op_x, "labels", "label"), op_p["etiqueta"], f"opciones[{i}].etiqueta")
        textos(_etiquetas(op_x, "Descriptions", "Description"), uniforme(op_p.get("descripcion") or ""), f"opciones[{i}].descripcion")
    return difs


def fragmento(customizations_xml, nombre):
    raiz = ET.fromstring(customizations_xml)
    nodo = next((o for o in raiz.iter("optionset") if o.get("Name") == nombre), None)
    return None if nodo is None else ET.tostring(nodo, encoding="unicode")


def main(argv=None, fabrica_cliente=None, config=None):
    """`fabrica_cliente` y `config` se inyectan solo en las pruebas: por defecto, `dataverse_api.Dataverse` y `proyecto.json`."""
    argv = sys.argv[1:] if argv is None else argv
    guardar = "--guardar" in argv
    carpeta = carpeta_playbooks(TIPO, config)
    rutas = sorted(glob.glob(os.path.join(carpeta, "*.md")))
    if not rutas:
        print(f"no hay playbooks en '{carpeta}' (se lee de la clave 'playbooks' de herramientas/proyecto.json)")
        return 1
    playbooks = []
    for ruta in rutas:
        sec = dividir_secciones(leer_texto(ruta))
        playbooks.append((obtener_componente(sec, TIPO), obtener_identidad(sec)))
    soluciones = {i["solucion"] for _, i in playbooks}
    if len(soluciones) != 1:
        print(f"los playbooks nombran más de una solución: {sorted(soluciones)}")
        return 1
    solucion = soluciones.pop()

    if fabrica_cliente is None:
        from dataverse_api import Dataverse as fabrica_cliente  # import tardío: no hace falta para las pruebas
    est, cuerpo, _ = escribir_metadatos(fabrica_cliente(), "POST", "ExportSolution", {"SolutionName": solucion, "Managed": False}, timeout=600)
    if est != 200 or not isinstance(cuerpo, dict) or not isinstance(cuerpo.get("ExportSolutionFile"), str):
        print(f"ExportSolution devolvió HTTP {est} o una forma inesperada")
        return 1
    z = zipfile.ZipFile(io.BytesIO(base64.b64decode(cuerpo["ExportSolutionFile"])))
    custom = z.read("customizations.xml").decode("utf-8")
    raices = [r for r in ET.fromstring(z.read("solution.xml")).iter("RootComponent") if r.get("type") == COMPONENTE_CHOICE]
    en_solucion = {r.get("schemaName") for r in raices}

    malos = 0
    for datos, identidad in playbooks:
        difs = comparar_con_xml(custom, datos, identidad["idiomas"])
        if datos["nombre"] not in en_solucion:
            difs.append("no figura como RootComponent type=9 en solution.xml")
        print(f"{'OK      ' if not difs else 'DIFIERE '} {datos['nombre']}" + ("" if not difs else ": " + "; ".join(difs)))
        malos += bool(difs)
        if guardar:
            frag = fragmento(custom, datos["nombre"])
            if frag:
                muestras = os.path.join(carpeta, "muestras")
                os.makedirs(muestras, exist_ok=True)
                with open(os.path.join(muestras, f"{datos['nombre']}.solucion.xml"), "w", encoding="utf-8") as f:
                    f.write(frag + "\n")
    sobran = sorted(en_solucion - {d["nombre"] for d, _ in playbooks})
    if sobran:
        print(f"AVISO: choices en la solución sin playbook: {sobran}")
    print(json.dumps({"playbooks": len(playbooks), "coinciden": len(playbooks) - malos, "difieren": malos, "sin_playbook": len(sobran)}))
    return 0 if not malos and not sobran else 1


if __name__ == "__main__":
    sys.exit(main())
