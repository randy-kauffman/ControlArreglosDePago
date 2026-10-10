#!/usr/bin/env python3
"""Comprobación independiente de las tablas: exporta la solución unmanaged por
el Web API (`ExportSolution`) y compara cada playbook de `playbooks/tabla/`
contra el XML de `customizations.xml`. Es otro camino que el de `tabla.py`
(que verifica por `EntityDefinitions`): si los dos coinciden, la tabla está
bien. Solo lee.

    python3 herramientas/construir/muestra_tabla.py [<playbook.md> …] [--guardar]

Sin playbooks, compara todos los de `<carpeta de playbooks>/tabla/` (no los de `ensayos/`); la carpeta sale de `proyecto.json`.
Con `--guardar`, deja el XML real de cada tabla en `<carpeta de playbooks>/tabla/muestras/<tabla>.solucion.xml` y, si la tabla tiene columnas de
fórmula, el yaml de sus expresiones junto a él (`<tabla>.formulas.yaml`).

Lo que la skill 0.2.0 agregó se compara contra exportaciones reales (2026-10-09, `tests/muestras/tabla/sanic_carp_tbl_zzensayo_decimal_motivos.*` y
`sanic_carp_tbl_zzensayo_formula.*`):
- Columna `decimal`: `<Type>decimal</Type>` con `<MinValue>`, `<MaxValue>` y `<Accuracy>` (la precisión). Los tres se comparan NUMÉRICAMENTE con
  `decimal.Decimal` (el XML dice `0` y el playbook puede decir `0` o `0.0`; `999999999.99` tiene que coincidir exacto, sin pasar por `float`).
- Motivos de estado (`motivos_estado`): el atributo `statuscode` trae `<statuses><status value state>` con las etiquetas por idioma. Si el playbook
  declara motivos, se comparan en los DOS sentidos (cada motivo declarado: valor, `statecode` = `state` y etiqueta en cada idioma; y cada `<status>` del
  XML que el playbook no declara es una diferencia, «sobra»). Si es `null` no se compara nada de `statuscode`: la tabla trae los de la plataforma.
  La `descripcion` de un motivo NO aparece en el XML: no se compara.
- Columna `formula` (la crea una persona en el editor de Power Apps; la plataforma no la acepta por código): en el XML es del tipo de su resultado
  (`decimal` → `<Type>decimal</Type>` con `<Accuracy>` = la precisión; `entero` → `<Type>int</Type>`) con `<SourceType>3</SourceType>`. Se comparan además
  requerida, protegida, auditoría, etiquetas y descripción. NO se comparan `MinValue`/`MaxValue` (los fija la plataforma, ±100.000.000.000). La
  EXPRESIÓN no está en el XML: el atributo trae `<FormulaDefinitionFileName>/Formulas/<tabla>-FormulaDefinitions.yaml</…>` y el zip exportado trae ese
  archivo, una línea por columna: `<columna>: =<expresión>` (se lee con `utf-8-sig`). Se compara con la del playbook sin el `=` inicial y normalizando
  los espacios. Si el atributo no trae el nombre del archivo, el zip no trae el archivo o el archivo no tiene la columna, es una diferencia. El yaml
  que no es de una sola línea por columna (un bloque `|`) no se interpreta: sale como diferencia, nunca como coincidencia.
"""
import base64
import decimal
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

from _comun import carpeta_playbooks, dividir_secciones, escribir_metadatos, leer_texto, obtener_componente, obtener_identidad  # noqa: E402

TIPO = "tabla"
COMPONENTE_TABLA = "1"
# Cómo se ve cada tipo del playbook en el XML de la solución (observado, 2026-09-20; `decimal` observado el 2026-10-09).
TIPO_XML = {"texto": "nvarchar", "autonumerico": "nvarchar", "memo": "ntext", "entero": "int", "decimal": "decimal", "choice": "picklist",
            "sino": "bit", "fecha": "datetime", "fechahora": "datetime", "archivo": "file"}
PROPIEDAD_XML = {"usuario": "UserOwned", "organizacion": "OrgOwned"}
SOURCE_TYPE_FORMULA = 3  # <SourceType> de una columna de fórmula en el XML (observado, 2026-10-09)
CARPETA_FORMULAS = "Formulas/"  # en el zip exportado, un yaml por tabla con fórmulas: `Formulas/<tabla>-FormulaDefinitions.yaml`
ATRIBUTO_MOTIVOS = "statuscode"  # el atributo de la tabla cuyo `<optionset>` trae los `<status>`
NO_DECLARABLES = {"lookup", "owner", "primarykey", "uniqueidentifier", "virtual"}


def _etiquetas(nodo, contenedor, hijo):
    c = nodo.find(contenedor) if nodo is not None else None
    return {} if c is None else {int(e.get("languagecode")): e.get("description") or "" for e in c.findall(hijo)}


def _comparar_motivos(atributo, motivos, idiomas, texto, difs):
    """Los motivos de estado declarados contra los `<status value state>` del atributo `statuscode`, en los DOS sentidos. `texto` es el comparador de
    etiquetas por idioma de `comparar_con_xml` (también denuncia una etiqueta en un idioma que el playbook no declara). La `descripcion` de un motivo
    NO se compara: no aparece en el XML exportado."""
    if atributo is None:
        difs.append(f"motivos_estado: la tabla exportada no tiene el atributo {ATRIBUTO_MOTIVOS}")
        return
    en_xml = {}
    for st in atributo.iter("status"):
        en_xml[int(st.get("value"))] = (st.get("state"), _etiquetas(st, "labels", "label"))
    declarados = {m["valor"] for m in motivos}
    for m in motivos:
        v = m["valor"]
        if v not in en_xml:
            difs.append(f"motivos_estado[{v}]: el playbook lo declara y el statuscode del XML no lo tiene (falta)")
            continue
        estado, etiquetas = en_xml[v]
        if estado != str(m["statecode"]):
            difs.append(f"motivos_estado[{v}].statecode: xml={estado!r} playbook={str(m['statecode'])!r}")
        texto(etiquetas, m["etiqueta"], f"motivos_estado[{v}].etiqueta")
    for v in sorted(en_xml.keys() - declarados):
        difs.append(f"motivos_estado[{v}]: el statuscode del XML lo tiene y el playbook no lo declara (sobra)")


def _normalizar(expresion):
    """Los espacios (y saltos de línea, tabuladores) de una expresión se comparan normalizados: el editor puede reacomodarlos."""
    return " ".join(expresion.split())


def expresiones_del_yaml(texto):
    """`{columna: expresión normalizada}` de un `Formulas/<tabla>-FormulaDefinitions.yaml`: una línea `<columna>: =<expresión>` por columna (observado el
    2026-10-09). Sin el `=` inicial. Solo entiende esa forma: una línea que no la cumple no aporta ninguna columna."""
    expresiones = {}
    for linea in texto.lstrip("\ufeff").splitlines():
        m = re.match(r"([A-Za-z0-9_]+):\s*(.*)$", linea)
        if m:
            valor = m.group(2).strip()
            expresiones[m.group(1)] = _normalizar(valor[1:] if valor.startswith("=") else valor)
    return expresiones


def _comparar_expresion(atributo, c, formulas, difs):
    """La expresión del playbook contra la del yaml que el XML nombra en `<FormulaDefinitionFileName>`. `formulas`: `{ruta dentro del zip: texto}`."""
    n = c["nombre"]
    archivo = (atributo.findtext("FormulaDefinitionFileName") or "").strip()
    if not archivo:
        difs.append(f"{n}.expresion: el atributo no trae FormulaDefinitionFileName (no es una columna de fórmula en el XML exportado)")
        return
    texto = (formulas or {}).get(archivo.lstrip("/"))
    if texto is None:
        difs.append(f"{n}.expresion: el atributo apunta a {archivo} y la exportación no trae ese archivo")
        return
    real = expresiones_del_yaml(texto).get(n)
    if real is None:
        difs.append(f"{n}.expresion: {archivo} no tiene la columna {n}")
        return
    esperado = _normalizar(c["expresion"])
    if real != esperado:
        difs.append(f"{n}.expresion: yaml={real!r} playbook={esperado!r}")


def comparar_con_xml(customizations_xml, datos, idiomas, formulas=None):
    """Las diferencias entre el playbook y el XML exportado. `formulas`: `{ruta dentro del zip: texto del yaml}` (ver `leer_exportacion`); solo hace falta
    si el playbook tiene columnas de fórmula."""
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

    def numero(nodo, etiqueta, esperado, que):
        """Compara un número del XML con el del playbook como `Decimal`: `0` == `0.0`, y `999999999.99` exacto (un `float` del playbook se pasa por su
        texto más corto, no por su aproximación binaria). Un valor del XML que no es un número es una diferencia, no una excepción."""
        real = (nodo.findtext(etiqueta) or "").strip()
        try:
            igual_numerico = decimal.Decimal(real) == decimal.Decimal(str(esperado))
        except decimal.InvalidOperation:
            igual_numerico = False
        if not igual_numerico:
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
        tipo_xml = TIPO_XML[c["resultado"] if tipo == "formula" else tipo]  # una fórmula es del tipo de su resultado
        igual(a, "Type", tipo_xml, f"{n}.tipo")
        if (a.findtext("Type") or "") != tipo_xml:
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
        elif tipo == "decimal":
            numero(a, "MinValue", c["minimo"], f"{n}.minimo")
            numero(a, "MaxValue", c["maximo"], f"{n}.maximo")
            numero(a, "Accuracy", c["precision"], f"{n}.precision")  # en el XML la precisión de un decimal es <Accuracy>
        elif tipo == "formula":
            igual(a, "SourceType", SOURCE_TYPE_FORMULA, f"{n}.SourceType")
            if c["resultado"] == "decimal":
                numero(a, "Accuracy", c["precision"], f"{n}.precision")
            # MinValue/MaxValue no se comparan: los fija la plataforma (±100.000.000.000), el playbook no los declara
            _comparar_expresion(a, c, formulas, difs)
        elif tipo == "choice":
            igual(a, "OptionSetName", c["choice"], f"{n}.choice")
        elif tipo == "sino":
            igual(a, "AppDefaultValue", int(c["defecto"]), f"{n}.defecto")
        elif tipo == "archivo":
            igual(a, "MaxValue", c["tamano_kb"], f"{n}.tamano_kb")  # en el XML el tamaño de un archivo es <MaxValue>
        elif tipo in ("fecha", "fechahora"):
            igual(a, "Format", "date" if tipo == "fecha" else "datetime", f"{n}.tipo (formato de la fecha)")
            igual(a, "Behavior", 2 if tipo == "fecha" else 1, f"{n}.tipo (comportamiento: 1 usuario local, 2 fecha sola)")

    if datos.get("motivos_estado") is not None:
        _comparar_motivos(attrs.get(ATRIBUTO_MOTIVOS), datos["motivos_estado"], idiomas, texto, difs)

    nombres = {c["nombre"] for c in declaradas}
    for n, a in sorted(attrs.items()):
        if a.findtext("IsCustomField") == "1" and n not in nombres and (a.findtext("Type") or "") not in NO_DECLARABLES:
            difs.append(f"columna propia que el playbook no declara: {n} ({a.findtext('Type')})")
    return difs


def evaluar(customizations_xml, en_solucion, datos, idiomas, formulas=None):
    """El veredicto de UN playbook: `("OK", [])` o `("DIFIERE", diferencias)`. La tabla también tiene que figurar como RootComponent de tipo tabla."""
    difs = comparar_con_xml(customizations_xml, datos, idiomas, formulas)
    if datos["nombre"] not in en_solucion:
        difs.append("no figura como RootComponent type=1 en solution.xml")
    return ("DIFIERE", difs) if difs else ("OK", [])


def no_coinciden(veredictos):
    """Cuántos veredictos no son `OK`: la herramienta no sale con código 0 si alguno no lo es."""
    return sum(1 for v in veredictos if v != "OK")


def leer_exportacion(contenido):
    """Del zip de `ExportSolution` (bytes) saca lo que se compara: `(customizations.xml, tablas de la solución, yaml de fórmulas)`. Las tablas son los
    `RootComponent type=1` de `solution.xml`; los yaml son los archivos de `Formulas/` por su ruta dentro del zip, leídos con `utf-8-sig` (pueden traer BOM)."""
    z = zipfile.ZipFile(io.BytesIO(contenido))
    custom = z.read("customizations.xml").decode("utf-8-sig")
    en_solucion = {r.get("schemaName") for r in ET.fromstring(z.read("solution.xml")).iter("RootComponent") if r.get("type") == COMPONENTE_TABLA}
    formulas = {n: z.read(n).decode("utf-8-sig") for n in z.namelist() if n.startswith(CARPETA_FORMULAS) and n.lower().endswith((".yaml", ".yml"))}
    return custom, en_solucion, formulas


def fragmento(customizations_xml, nombre):
    raiz = ET.fromstring(customizations_xml)
    for e in raiz.iter("Entity"):
        if e.findtext("Name") == nombre:
            return ET.tostring(e, encoding="unicode")
    return None


def archivo_de_formulas(customizations_xml, nombre):
    """La ruta del yaml de fórmulas que el XML nombra para la tabla `nombre` (`<FormulaDefinitionFileName>`), o `None` si la tabla no tiene fórmulas."""
    raiz = ET.fromstring(customizations_xml)
    for e in raiz.iter("Entity"):
        if e.findtext("Name") == nombre:
            rutas = sorted({(x.text or "").strip() for x in e.iter("FormulaDefinitionFileName") if (x.text or "").strip()})
            return rutas[0] if rutas else None
    return None


def guardar_muestra(carpeta, nombre, customizations_xml, formulas):
    """`--guardar`: deja en `carpeta` el XML real de la tabla (`<tabla>.solucion.xml`) y, si la tabla tiene fórmulas, el yaml que el XML nombra
    (`<tabla>.formulas.yaml`). Devuelve las rutas escritas (ninguna si la tabla no está en el XML)."""
    frag = fragmento(customizations_xml, nombre)
    if not frag:
        return []
    os.makedirs(carpeta, exist_ok=True)
    escritos = [os.path.join(carpeta, f"{nombre}.solucion.xml")]
    with open(escritos[0], "w", encoding="utf-8") as f:
        f.write(frag + "\n")
    archivo = archivo_de_formulas(customizations_xml, nombre)
    if archivo and archivo.lstrip("/") in formulas:
        escritos.append(os.path.join(carpeta, f"{nombre}.formulas.yaml"))
        with open(escritos[1], "w", encoding="utf-8") as f:
            f.write(formulas[archivo.lstrip("/")])
    return escritos


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
    custom, en_solucion, formulas = leer_exportacion(base64.b64decode(cuerpo["ExportSolutionFile"]))
    veredictos = []
    for datos, identidad in playbooks:
        resultado, textos = evaluar(custom, en_solucion, datos, identidad["idiomas"], formulas)
        print(f"{resultado:<12} {datos['nombre']}" + ("" if not textos else ": " + "; ".join(textos)))
        veredictos.append(resultado)
        if guardar:
            guardar_muestra(os.path.join(carpeta, "muestras"), datos["nombre"], custom, formulas)
    malos = no_coinciden(veredictos)
    print(json.dumps({"playbooks": len(playbooks), "coinciden": len(playbooks) - malos, "no_coinciden": malos}))
    return 0 if not malos else 1


if __name__ == "__main__":
    sys.exit(main())
