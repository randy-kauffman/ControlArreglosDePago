#!/usr/bin/env python3
"""Comprobación independiente de los roles: exporta la solución unmanaged por
el Web API (`ExportSolution`) y compara cada playbook de la carpeta `rol/` de la carpeta
de playbooks del proyecto (clave `playbooks` de `herramientas/proyecto.json`)
contra `customizations.xml` (`<Roles><Role name="…"><RolePrivileges>`). Es
otro camino que el de `rol.py`. Solo lee.

    python3 herramientas/construir/muestra_rol.py [<playbook.md> …] [--guardar]

Sin rutas compara todos los playbooks de esa carpeta. Con `--guardar` deja, al
lado de ellos y en `muestras/`, el XML de cada rol encontrado en la exportación.

Qué compara: la descripción; los privilegios sobre las TABLAS DE LA SOLUCIÓN,
exactamente (ni uno de menos, ni uno de más, ni otro alcance); y los
`otros_privilegios`, que estén con su alcance. Los privilegios que vienen del
rol base no se pueden distinguir en el XML: eso lo comprueba `rol.py` contra
el entorno, que sí puede leer el rol base.
"""
import base64
import glob
import io
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _comun import carpeta_playbooks as _comun_carpeta_playbooks, dividir_secciones, escribir_metadatos, leer_texto, nombre_de_archivo, obtener_componente, obtener_identidad  # noqa: E402
from credenciales import RAIZ_PROYECTO, cargar_configuracion  # noqa: E402
from rol import ACCIONES, ALCANCES  # noqa: E402

TIPO = "rol"


def carpeta_de_playbooks(config=None, raiz=None):
    """Carpeta con los playbooks de este tipo: `<raiz del proyecto>/<playbooks de proyecto.json>/rol`."""
    return os.path.join(_comun_carpeta_playbooks(config, raiz), TIPO)


def _buscar(raiz, datos):
    return next((r for r in raiz.iter("Role") if r.get("name") == datos["nombre"]), None)


def comparar_con_xml(customizations_xml, datos, identidad):
    rol = _buscar(ET.fromstring(customizations_xml), datos)
    if rol is None:
        return [f"{datos['nombre']} no está en la solución exportada"]
    difs = []
    descripcion = (rol.findtext("Description") or "").strip()
    if descripcion != datos["descripcion"]:
        difs.append(f"descripcion: xml={descripcion!r} playbook={datos['descripcion']!r}")
    contenedor = rol.find("RolePrivileges")
    reales = {} if contenedor is None else {p.get("name"): p.get("level") for p in contenedor.findall("RolePrivilege")}

    esperados = {f"prv{ACCIONES[a]}{t}": ALCANCES[alcance] for t, acciones in datos["tablas"].items() for a, alcance in acciones.items()}
    esperados.update({n: ALCANCES[alcance] for n, alcance in datos["otros_privilegios"].items()})
    for nombre, alcance in sorted(esperados.items()):
        if nombre not in reales:
            difs.append(f"falta {nombre} ({alcance})")
        elif reales[nombre] != alcance:
            difs.append(f"{nombre}: xml={reales[nombre]!r} playbook={alcance!r}")
    de_la_solucion = re.compile(rf"prv[A-Za-z]+{identidad['prefijo']}_{identidad['abrev']}_tbl_[a-z0-9]+")
    difs += [f"sobra {n} ({reales[n]}): es de una tabla de la solución y el playbook no lo declara" for n in sorted(reales) if de_la_solucion.fullmatch(n) and n not in esperados]
    return difs


def main(argv=None, fabrica_cliente=None, config=None, raiz=None):
    """`fabrica_cliente`, `config` y `raiz` se inyectan solo en las pruebas."""
    if fabrica_cliente is None:
        from dataverse_api import Dataverse as fabrica_cliente

    argv = sys.argv[1:] if argv is None else argv
    guardar = "--guardar" in argv
    try:
        carpeta = carpeta_de_playbooks(config, raiz)
    except ValueError as e:
        print(e)
        return 1
    rutas = [a for a in argv if not a.startswith("--")] or sorted(glob.glob(os.path.join(carpeta, "*.md")))
    if not rutas:
        print(f"no hay playbooks de {TIPO} en {carpeta}")
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
    est, cuerpo, _ = escribir_metadatos(fabrica_cliente(), "POST", "ExportSolution", {"SolutionName": solucion, "Managed": False}, timeout=900)
    if est != 200 or not isinstance(cuerpo, dict) or not isinstance(cuerpo.get("ExportSolutionFile"), str):
        print(f"ExportSolution devolvió HTTP {est} o una forma inesperada")
        return 1
    custom = zipfile.ZipFile(io.BytesIO(base64.b64decode(cuerpo["ExportSolutionFile"]))).read("customizations.xml").decode("utf-8")
    raiz_xml = ET.fromstring(custom)
    malos = 0
    for datos, identidad in playbooks:
        difs = comparar_con_xml(custom, datos, identidad)
        print(f"{'OK      ' if not difs else 'DIFIERE '} {datos['nombre']}" + ("" if not difs else ": " + "; ".join(difs)))
        malos += bool(difs)
        if guardar:
            rol = _buscar(raiz_xml, datos)
            if rol is not None:
                os.makedirs(os.path.join(carpeta, "muestras"), exist_ok=True)
                archivo = nombre_de_archivo(datos["nombre"]) + ".solucion.xml"
                with open(os.path.join(carpeta, "muestras", archivo), "w", encoding="utf-8") as f:
                    f.write(ET.tostring(rol, encoding="unicode") + "\n")
    print(json.dumps({"playbooks": len(playbooks), "coinciden": len(playbooks) - malos, "difieren": malos}))
    return 0 if not malos else 1


if __name__ == "__main__":
    sys.exit(main())
