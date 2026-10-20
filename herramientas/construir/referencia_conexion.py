#!/usr/bin/env python3
"""Construye UNA connection reference de Dataverse desde un playbook de tipo
`referencia-conexion`. Contrato: `power-platform-construir`,
`references/dataverse/alm.md`.

    python3 herramientas/construir/referencia_conexion.py <playbook.md> [--solo-verificar] [--completar]

Dos cosas distintas, como en la environment variable: la **referencia**
(nombre lógico y conector) viaja en la solución y es la misma en todos los
entornos; la **conexión** a la que apunta (`connectionid`) es de ESTE entorno y
la crea una persona iniciando sesión en el portal: no hay API para crearla.
Por eso el playbook admite `conexion: null`: la referencia se crea igual y el
estado final es `bloqueado`, con lo que falta dicho.

El conector NO se puede cambiar una vez creada la referencia: si difiere, se
informa y se para.

Última línea de la salida: JSON de una línea con `estado`
(creado | actualizado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import os
import sys

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
)

TIPO = "referencia-conexion"

# Forma de `connectorid` leída de las connection references que ya viven en Dev (2026-10-20).
RAIZ_CONECTOR = "/providers/Microsoft.PowerApps/apis/"

LARGO_NOMBRE = 100
LARGO_DISPLAYNAME = 100

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

CLAVES = {"tipo": str, "nombre": str, "displayname": str, "descripcion": str, "conector": str}
# `conexion` va aparte porque es la única clave que admite nulo a propósito.
CLAVE_CONEXION = "conexion"

C_REFERENCIA = "la consulta de la referencia (GET connectionreferences)"
C_COMPONENTES = "la consulta de pertenencia a la solución (GET solutioncomponents)"


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def _caracteres_de_identificador(texto):
    return all(("a" <= c <= "z") or ("A" <= c <= "Z") or ("0" <= c <= "9") or c == "_" for c in texto)


def validar_playbook(datos, identidad):
    esperadas = set(CLAVES) | {CLAVE_CONEXION}
    faltan, sobran = sorted(esperadas - set(datos)), sorted(set(datos) - esperadas)
    if faltan or sobran:
        partes = []
        if faltan:
            partes.append(f"faltan {faltan}")
        if sobran:
            partes.append(f"sobran {sobran}")
        raise ErrorPlaybook(f"el bloque de la referencia no tiene las claves esperadas: {'; '.join(partes)}")
    for clave, tipo in CLAVES.items():
        exigir_forma(datos[clave], tipo, "el bloque de la referencia", clave, no_vacio=True)
    if datos[CLAVE_CONEXION] is not None:
        exigir_forma(datos[CLAVE_CONEXION], str, "el bloque de la referencia", CLAVE_CONEXION, no_vacio=True)
    if datos["tipo"] != TIPO:
        raise ErrorPlaybook(f"el bloque de la referencia dice tipo = {datos['tipo']!r}, se esperaba {TIPO!r}")

    nombre = datos["nombre"]
    prefijo = f"{identidad['prefijo']}_{identidad['abrev']}_conr_"
    if not nombre.startswith(prefijo):
        raise ErrorPlaybook(f"nombre = {nombre!r} no sigue el patrón {prefijo}<nombre> (`01-convenciones.md` §2)")
    if not _caracteres_de_identificador(nombre):
        raise ErrorPlaybook(f"nombre = {nombre!r} solo puede llevar letras y dígitos ASCII y guión bajo")
    if len(nombre) > LARGO_NOMBRE:
        raise ErrorPlaybook(f"nombre tiene {len(nombre)} caracteres; el máximo es {LARGO_NOMBRE}")

    exigir_sin_tildes(datos["displayname"], "displayname")
    if len(datos["displayname"]) > LARGO_DISPLAYNAME:
        raise ErrorPlaybook(f"displayname tiene {len(datos['displayname'])} caracteres; el máximo es {LARGO_DISPLAYNAME}")

    conector = datos["conector"]
    if "/" in conector or not conector.startswith("shared_"):
        raise ErrorPlaybook(
            f"conector = {conector!r}: va el nombre de la API (por ejemplo 'shared_office365'), "
            f"no la ruta; la herramienta arma '{RAIZ_CONECTOR}<conector>'")


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def _leer(dv, nombre):
    ruta = ("connectionreferences?$select=connectionreferenceid,connectionreferencelogicalname,"
            "connectionreferencedisplayname,description,connectorid,connectionid,ismanaged"
            f"&$filter=connectionreferencelogicalname eq '{nombre}'")
    filas = exigir_forma(leer_entorno(dv, ruta, C_REFERENCIA).get("value"), [dict], C_REFERENCIA, "value")
    if len(filas) > 1:
        raise ErrorEntorno(f"{C_REFERENCIA} devolvió {len(filas)} filas para '{nombre}'; se esperaba a lo sumo una")
    return filas[0] if filas else None


def _en_la_solucion(dv, solution_id, objeto_id):
    ruta = ("solutioncomponents?$select=componenttype,objectid"
            f"&$filter=_solutionid_value eq {solution_id} and objectid eq {objeto_id}")
    filas = exigir_forma(leer_entorno(dv, ruta, C_COMPONENTES).get("value"), [dict], C_COMPONENTES, "value")
    return filas[0] if filas else None


def _diferencias(datos, ref):
    difs, corregibles = [], {}
    if ref.get("connectorid") != RAIZ_CONECTOR + datos["conector"]:
        difs.append(f"connectorid es {ref.get('connectorid')!r} y el playbook dice "
                    f"{RAIZ_CONECTOR + datos['conector']!r} (INMUTABLE: hay que borrar la referencia y rehacerla)")
    campos = [("connectionreferencedisplayname", datos["displayname"]), ("description", datos["descripcion"])]
    if datos[CLAVE_CONEXION] is not None:
        campos.append(("connectionid", datos[CLAVE_CONEXION]))
    for campo, valor in campos:
        if ref.get(campo) != valor:
            difs.append(f"{campo} es {ref.get(campo)!r} y el playbook dice {valor!r}")
            corregibles[campo] = valor
    if ref.get("ismanaged"):
        difs.append("la referencia está managed en este entorno; no se toca desde acá")
        corregibles.clear()
    return difs, corregibles


def _contra_entorno(dv, datos, identidad, solo_verificar, completar, componente, dormir):
    solucion = identidad["solucion"]
    solution_id = comprobar_solucion_e_idioma(dv, identidad)
    ref = _leer(dv, datos["nombre"])
    hubo_escritura = hubo_alta = False

    if ref is None:
        if solo_verificar:
            return "difiere", componente, f"la referencia '{datos['nombre']}' no existe en el entorno"
        cuerpo = {"connectionreferencelogicalname": datos["nombre"],
                  "connectionreferencedisplayname": datos["displayname"],
                  "description": datos["descripcion"],
                  "connectorid": RAIZ_CONECTOR + datos["conector"]}
        if datos[CLAVE_CONEXION] is not None:
            cuerpo["connectionid"] = datos[CLAVE_CONEXION]
        est, resp, _ = escribir_metadatos(dv, "POST", "connectionreferences", cuerpo, dormir=dormir, solucion=solucion)
        if est not in (200, 201, 204):
            return "error", componente, f"el alta de la referencia devolvió HTTP {est} (se esperaba 204): {resp}"
        ref = _leer(dv, datos["nombre"])
        if ref is None:
            return "error", componente, "el alta no dio error pero la referencia no aparece al releer"
        hubo_escritura = hubo_alta = True
    elif completar and not solo_verificar:
        difs, corregibles = _diferencias(datos, ref)
        if not [d for d in difs if "INMUTABLE" in d] and corregibles:
            id_ref = exigir_forma(ref.get("connectionreferenceid"), str, C_REFERENCIA, "connectionreferenceid", no_vacio=True)
            est, resp, _ = escribir_metadatos(dv, "PATCH", f"connectionreferences({id_ref})", corregibles,
                                              dormir=dormir, solucion=solucion)
            if est not in (200, 204):
                return "error", componente, f"la corrección devolvió HTTP {est} (se esperaba 204): {resp}"
            ref = _leer(dv, datos["nombre"])
            hubo_escritura = True

    id_ref = exigir_forma(ref.get("connectionreferenceid"), str, C_REFERENCIA, "connectionreferenceid", no_vacio=True)
    difs, _ = _diferencias(datos, ref)
    fila = _en_la_solucion(dv, solution_id, id_ref)
    if fila is None:
        difs.append(f"la referencia no figura como componente de la solución (solutionid {solution_id})")
    if difs:
        prefijo = "se escribió pero no quedó bien: " if hubo_escritura else ""
        return ("error" if hubo_escritura else "difiere"), componente, prefijo + "; ".join(difs)

    detalle = (f"connectionreferenceid {id_ref}, conector {datos['conector']}, en la solución '{solucion}' "
               f"con componenttype {fila.get('componenttype')}")
    if not ref.get("connectionid"):
        return "bloqueado", componente, (
            detalle + ("; creada" if hubo_alta else "") + ". La referencia no tiene conexión: una persona tiene que "
            "crear la conexión iniciando sesión en el portal y asociarla a esta referencia")
    return estado_de_escritura(hubo_alta, hubo_escritura), componente, detalle


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
        if isinstance(datos.get("nombre"), str):
            componente = datos["nombre"]
        paso = "leer el bloque '## 1. Identidad'"
        identidad = obtener_identidad(secciones)
        paso = "validar el bloque de la referencia"
        validar_playbook(datos, identidad)
    except ErrorPlaybook as e:
        return "error", componente, str(e)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except OSError as e:
        return "error", componente, f"no se pudo leer el playbook '{ruta_playbook}': {e}"
    except Exception as e:
        return "error", componente, f"fallo inesperado validando el playbook (offline), al {paso}: {type(e).__name__}: {e}"

    print(f"Playbook leído y validado (offline). tipo={TIPO} nombre={componente}")
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
            "uso incorrecto: referencia_conexion.py <playbook.md> [--solo-verificar] [--completar]"
            + (f"; banderas desconocidas: {desconocidas}" if desconocidas else ""),
        )
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(
        rutas[0], "--solo-verificar" in banderas, Dataverse, completar="--completar" in banderas)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
