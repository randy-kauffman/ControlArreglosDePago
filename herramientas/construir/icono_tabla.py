#!/usr/bin/env python3
"""Pone el ícono SVG de las tablas propias desde un playbook de tipo
`icono-tabla`. Contrato: `power-platform-construir`, formato común.

    python3 herramientas/construir/icono_tabla.py <playbook.md> [--solo-verificar]

El ícono de una tabla es `EntityMetadata.IconVectorName`: el nombre de un web
resource SVG (Learn, "Image web resources": en lugar de `IconSmallName`,
`IconMediumName` e `IconLargeName`). Lo usan la navegación, los lookups, los
formularios y las vistas de la tabla.

Se cambia con un PUT de la definición COMPLETA de la tabla, partiendo de la que
devuelve el entorno (PUT reemplaza todo), con `MSCRM.MergeLabels` para no
perder las etiquetas de los otros idiomas, y después se publica la tabla. El
web resource tiene que existir antes: si falta, `bloqueado` sin escribir nada.
No hace falta `--completar`: lo único que se escribe es el ícono, que es lo que
el playbook pide.

Última línea de la salida: JSON de una línea con `estado`
(actualizado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import os
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _comun import (  # noqa: E402
    Bloqueado,
    ErrorEntorno,
    ErrorPlaybook,
    Rastro,
    comprobar_solucion_e_idioma,
    dividir_secciones,
    escribir_metadatos,
    exigir_forma,
    leer_entorno,
    leer_texto,
    obtener_componente,
    obtener_identidad,
    salida,
)

TIPO = "icono-tabla"

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

C_RECURSO = "la consulta del ícono (GET webresourceset)"


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def validar_playbook(datos, identidad):
    esperadas = {"tipo", "tablas"}
    faltan, sobran = sorted(esperadas - set(datos)), sorted(set(datos) - esperadas)
    if faltan or sobran:
        partes = ([f"faltan {faltan}"] if faltan else []) + ([f"sobran {sobran}"] if sobran else [])
        raise ErrorPlaybook(f"el bloque de íconos no tiene las claves esperadas: {'; '.join(partes)}")
    if datos["tipo"] != TIPO:
        raise ErrorPlaybook(f"el bloque dice tipo = {datos['tipo']!r}, se esperaba {TIPO!r}")
    tablas = exigir_forma(datos["tablas"], [dict], "el bloque de íconos", "tablas", no_vacio=True)
    pre_tabla = f"{identidad['prefijo']}_{identidad['abrev']}_tbl_"
    pre_icono = f"{identidad['prefijo']}_{identidad['abrev']}_wr_svg_"
    vistas = set()
    for i, t in enumerate(tablas):
        donde = f"tablas[{i}]"
        if set(t) != {"tabla", "icono"}:
            raise ErrorPlaybook(f"{donde} tiene que tener exactamente 'tabla' e 'icono'; tiene {sorted(t)}")
        exigir_forma(t["tabla"], str, "el bloque de íconos", f"{donde}.tabla", no_vacio=True)
        exigir_forma(t["icono"], str, "el bloque de íconos", f"{donde}.icono", no_vacio=True)
        if not t["tabla"].startswith(pre_tabla):
            raise ErrorPlaybook(f"{donde}.tabla = {t['tabla']!r}: solo tablas propias ({pre_tabla}<nombre>)")
        if not t["icono"].startswith(pre_icono):
            raise ErrorPlaybook(f"{donde}.icono = {t['icono']!r}: tiene que ser un SVG del proyecto ({pre_icono}<nombre>)")
        if t["tabla"] in vistas:
            raise ErrorPlaybook(f"{donde}.tabla = {t['tabla']!r} está repetida")
        vistas.add(t["tabla"])


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def _existe_recurso(dv, nombre):
    filas = exigir_forma(leer_entorno(dv, f"webresourceset?$select=webresourceid,name&$filter=name eq '{nombre}'",
                                      C_RECURSO).get("value"), [dict], C_RECURSO, "value")
    return len(filas) == 1


def _definicion(dv, tabla):
    consulta = f"la definición de la tabla (GET EntityDefinitions '{tabla}')"
    d = exigir_forma(leer_entorno(dv, f"EntityDefinitions(LogicalName='{tabla}')", consulta), dict, consulta, "cuerpo")
    exigir_forma(d.get("MetadataId"), str, consulta, "MetadataId", no_vacio=True)
    return d, consulta


def _contra_entorno(dv, datos, identidad, solo_verificar, componente):
    comprobar_solucion_e_idioma(dv, identidad)
    faltan = sorted({t["icono"] for t in datos["tablas"] if not _existe_recurso(dv, t["icono"])})
    if faltan:
        raise Bloqueado(f"los web resources {faltan} no existen; hay que crearlos primero (playbooks de web-resource)")

    difs, cambiadas = [], []
    for t in datos["tablas"]:
        tabla, icono = t["tabla"], t["icono"]
        actual, consulta = _definicion(dv, tabla)
        if actual.get("IconVectorName") == icono:
            continue
        if actual.get("IsManaged"):
            difs.append(f"{tabla}: está managed en este entorno; no se toca desde acá")
            continue
        if solo_verificar:
            difs.append(f"{tabla}: IconVectorName es {actual.get('IconVectorName')!r} y el playbook dice {icono!r}")
            continue
        cuerpo = {k: v for k, v in actual.items() if k != "@odata.context"}
        cuerpo["IconVectorName"] = icono
        est, resp, _ = escribir_metadatos(dv, "PUT", f"EntityDefinitions({actual['MetadataId']})", cuerpo,
                                          solucion=identidad["solucion"], cabeceras={"MSCRM.MergeLabels": "true"})
        if est != 204:
            return "error", componente, f"{tabla}: el PUT de la definición devolvió HTTP {est} (se esperaba 204): {resp}"
        xml = f"<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>"
        est, resp, _ = escribir_metadatos(dv, "POST", "PublishXml", {"ParameterXml": xml})
        if est != 204:
            return "error", componente, f"{tabla}: se cambió el ícono pero falló publicar la tabla: HTTP {est} {resp}"
        releida, _ = _definicion(dv, tabla)
        if releida.get("IconVectorName") != icono:
            return "error", componente, (f"{tabla}: se escribió pero al releer IconVectorName es "
                                         f"{releida.get('IconVectorName')!r}")
        cambiadas.append(tabla)

    if difs:
        prefijo = f"se actualizaron {cambiadas}; " if cambiadas else ""
        return "difiere", componente, prefijo + "; ".join(difs)
    total = len(datos["tablas"])
    if cambiadas:
        return "actualizado", componente, f"ícono puesto y publicado en {cambiadas}; las {total} tablas quedan con su ícono"
    return "ya_existia", componente, f"las {total} tablas ya tienen el ícono del playbook"


def construir(ruta_playbook, solo_verificar, fabrica_cliente):
    """Nunca lanza: siempre devuelve `(estado, componente, detalle)`."""
    componente = os.path.basename(ruta_playbook)
    paso = "leer el playbook"
    try:
        secciones = dividir_secciones(leer_texto(ruta_playbook))
        paso = "leer el bloque '## 2. Qué se crea'"
        datos = obtener_componente(secciones, TIPO)
        paso = "leer el bloque '## 1. Identidad'"
        identidad = obtener_identidad(secciones)
        paso = "validar el bloque de íconos"
        validar_playbook(datos, identidad)
    except ErrorPlaybook as e:
        return "error", componente, str(e)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except OSError as e:
        return "error", componente, f"no se pudo leer el playbook '{ruta_playbook}': {e}"
    except Exception as e:
        return "error", componente, f"fallo inesperado validando el playbook (offline), al {paso}: {type(e).__name__}: {e}"

    print(f"Playbook leído y validado (offline). tipo={TIPO} tablas={len(datos['tablas'])}")
    try:
        dv = fabrica_cliente()
    except Exception:
        return "error", componente, MENSAJE_FALLO_CLIENTE

    rastro = Rastro(dv)
    try:
        return _contra_entorno(rastro, datos, identidad, solo_verificar, componente)
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
    desconocidas = sorted(banderas - {"--solo-verificar"})
    if len(rutas) != 1 or desconocidas:
        return salida("error", "desconocido", "uso incorrecto: icono_tabla.py <playbook.md> [--solo-verificar]"
                      + (f"; banderas desconocidas: {desconocidas}" if desconocidas else ""))
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(rutas[0], "--solo-verificar" in banderas, Dataverse)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
