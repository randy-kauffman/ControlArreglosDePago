"""Columnas PROPIAS sobre una tabla ESTÁNDAR que ya existe (por ejemplo `sanic_tiemporespuesta` sobre `email`), desde un playbook de tipo
`columnas-estandar`. Variante de `tabla.py`: la skill `power-platform-especificar` (0.2.0, `tabla.md` sección 6) la documenta dentro de la
referencia de la tabla y no como tipo propio, y la herramienta la despacha desde `tabla.py` (`tabla.py <playbook.md>`).

La tabla estándar NO se crea ni se modifica. Se agregan sus columnas con `POST EntityDefinitions(LogicalName='<tabla>')/Attributes`
(el mismo cuerpo que una columna de una tabla propia: una sola receta por tipo) y la cabecera `MSCRM.SolutionUniqueName`; se publica la tabla
una vez; se vuelve a leer y se compara con las MISMAS comprobaciones que una tabla propia (tipo, requerida, protegida, auditoría, nombres por
idioma y lo propio de cada tipo), más que ninguna columna con el prefijo del publisher falte en el playbook (una tabla estándar trae cientos
de columnas del sistema: «columnas de más» solo cuenta las propias del publisher, salvo lookups y sombras).

**Variante sin ensayar (0 ciclos).** Mientras no pase por un ciclo completo la herramienta devuelve `bloqueado`:
- `variante:columnas-estandar`: que la tabla acepte columnas propias (`CanCreateAttributes`), que se pueda publicar una tabla estándar y la
  combinación tipo de columna por tabla (el primer ciclo es un entero sobre `email`).
- `variante:pertenencia`: la pertenencia a la solución. Learn recomienda agregar solo lo nuevo; lo esperado es que la tabla figure con
  `rootcomponentbehavior` distinto de 0, pero si el alta con la cabecera agrega la tabla así y qué filas quedan (columna, tabla) no se sabe.
  Mientras no esté verificada la herramienta la OBSERVA y la informa (con las filas que ve), y nunca la da por buena. Si alguien la marca
  verificada, exige que la tabla figure en la solución, una vez, con `rootcomponentbehavior` distinto de 0.
`--permitir-no-verificadas` corre la receta, pero solo si TODAS las columnas del playbook son descartables (`_zz` en el nombre): el ensayo se
hace con columnas que se borran después, nunca con las del diseño. Las banderas de reparación de `tabla.py` no aplican a esta variante.

Última línea de la salida: JSON de una línea con `estado` (creado | ya_existia | difiere | bloqueado | error), `componente` (la tabla estándar)
y `detalle`.
"""
import os
import re
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)

import tabla as tb  # noqa: E402
from _comun import (  # noqa: E402
    Bloqueado,
    ErrorEntorno,
    ErrorPlaybook,
    Rastro,
    comprobar_solucion_e_idioma,
    dividir_secciones,
    escribir_metadatos,
    exigir_forma,
    leer_texto,
    obtener_componente,
    obtener_identidad,
    validar_textos_por_idioma,
)

TIPO = "columnas-estandar"
CLAVES_VARIANTE = {"tipo": str, "tabla": str, "columnas": list}
CARACTERISTICAS_DE_LA_VARIANTE = frozenset({"variante:columnas-estandar", "variante:pertenencia"})
INFIJO_DESCARTABLE_COLUMNA = "_zz"
TIPOS_NO_ENSAYADOS_EN_ESTANDAR = ("autonumerico", "archivo")  # `tabla.md` sección 6: «no se admiten, no se ensayaron sobre una estándar»
COMPONENTE_ATRIBUTO = 2  # tipo de componente de una columna en solutioncomponents
REPARACIONES_QUE_NO_APLICAN = ("--corregir-primaria", "--publicar", "--corregir-nombres", "--agregar-columnas")

PENDIENTE_DE_LA_VARIANTE = {
    "variante:columnas-estandar": "variante sin ciclo completo: falta comprobar que la tabla estándar acepte columnas propias en este entorno (CanCreateAttributes), "
                                  "que se pueda publicar una tabla estándar y la combinación tipo de columna por tabla (el primer ciclo es un entero sobre `email`)",
    "variante:pertenencia": "la pertenencia a la solución no tiene criterio fijado: falta comprobar si crear la columna con la cabecera MSCRM.SolutionUniqueName agrega "
                            "la tabla con rootcomponentbehavior distinto de 0 y qué filas de solutioncomponents quedan (columna, tabla)",
}


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def validar_playbook(datos, identidad):
    tb._validar_bloque(datos, CLAVES_VARIANTE, "la variante columnas-estandar")
    prefijo, idiomas = identidad["prefijo"], identidad["idiomas"]
    tb._validar_nombre(datos["tabla"], r"[a-z][a-z0-9_]*", "'tabla'")
    if datos["tabla"].startswith(f"{prefijo}_"):
        raise ErrorPlaybook(f"'tabla' = {datos['tabla']!r} lleva el prefijo del publisher: es una tabla propia, no estándar (va con el playbook de tabla)")
    if not datos["columnas"]:
        raise ErrorPlaybook("'columnas' no puede estar vacía: un playbook de columnas-estandar agrega al menos una columna")
    nombres = set()
    for i, c in enumerate(datos["columnas"]):
        if isinstance(c, dict) and c.get("tipo") in TIPOS_NO_ENSAYADOS_EN_ESTANDAR:
            raise ErrorPlaybook(f"columnas[{i}].tipo = {c['tipo']!r}: una columna {c['tipo']} no se admite sobre una tabla estándar (no se ensayó; `tabla.md`, sección 6)")
        tb._validar_columna(c, f"columnas[{i}]", i, datos["columnas"], nombres, identidad)
        validar_textos_por_idioma(c["displayname"], idiomas, f"columnas[{i}].displayname ({c['nombre']})")
        for etiqueta in ("etiqueta_si", "etiqueta_no"):
            if etiqueta in c:
                validar_textos_por_idioma(c[etiqueta], idiomas, f"columnas[{i}].{etiqueta} ({c['nombre']})")
    tb._validar_profundidad_de_formulas(datos["columnas"])


def comprobar_verificadas(datos, verificadas, permitir_no_verificadas):
    """El gate «Ensayo» de la variante: lo suyo (`variante:*`) más lo sin ensayar de cada columna. El ensayo solo vale con columnas descartables."""
    usadas = set(CARACTERISTICAS_DE_LA_VARIANTE) | tb.caracteristicas_de_columnas(datos["columnas"])
    faltan = sorted(usadas - set(verificadas))
    if not faltan:
        return
    if not permitir_no_verificadas:
        textos = {**tb.PENDIENTE_DE_VERIFICAR, **PENDIENTE_DE_LA_VARIANTE}
        pendiente = "".join(f" [{c}: {textos[c]}]" for c in faltan if c in textos)
        raise Bloqueado(
            f"el playbook usa características todavía no verificadas contra la plataforma: {faltan}.{pendiente} "
            f"Primero se ensayan con columnas descartables ('…{INFIJO_DESCARTABLE_COLUMNA}…') y --permitir-no-verificadas"
        )
    no_descartables = [c["nombre"] for c in datos["columnas"] if INFIJO_DESCARTABLE_COLUMNA not in c["nombre"]]
    if no_descartables:
        raise Bloqueado(
            f"--permitir-no-verificadas solo vale cuando TODAS las columnas son descartables, con '{INFIJO_DESCARTABLE_COLUMNA}' en el nombre; "
            f"no lo son: {no_descartables}. Sin verificar: {faltan}"
        )


# ---------------------------------------------------------------------------
# Contra el entorno
# ---------------------------------------------------------------------------
def comprobar_tabla(dv, datos, identidad):
    """Precondiciones de la tabla estándar (`tabla.md`, sección 6): existe, es estándar, acepta columnas propias, y si una columna pide auditoría la
    tabla ya está auditada (este playbook no cambia la auditoría de la tabla: es un hueco del diseño). Devuelve `{"metadata_id", "auditada"}`."""
    tabla = datos["tabla"]
    consulta = f"la consulta de la tabla estándar (GET EntityDefinitions '{tabla}')"
    t = tb._get(dv, f"EntityDefinitions(LogicalName='{tabla}')?$select=LogicalName,IsCustomEntity,CanCreateAttributes,IsAuditEnabled", consulta, admite_404=True)
    if t is None:
        raise Bloqueado(f"la tabla estándar '{tabla}' no existe en el entorno")
    metadata_id = exigir_forma(t.get("MetadataId"), str, consulta, "MetadataId", no_vacio=True)
    es_propia = exigir_forma(t.get("IsCustomEntity"), bool, consulta, "IsCustomEntity")
    acepta = tb._administrada(t.get("CanCreateAttributes"), bool, consulta, "CanCreateAttributes")
    auditada = tb._administrada(t.get("IsAuditEnabled"), bool, consulta, "IsAuditEnabled")
    if es_propia:
        raise Bloqueado(f"la tabla '{tabla}' es una tabla propia (IsCustomEntity = true): sus columnas van en el playbook de la tabla, no en uno de columnas-estandar")
    if not acepta:
        raise Bloqueado(f"la tabla '{tabla}' no acepta columnas propias: CanCreateAttributes.Value = false (Learn condiciona el alta de columnas a esa propiedad administrada)")
    pide_auditoria = [c["nombre"] for c in datos["columnas"] if c["auditoria"]]
    if pide_auditoria and not auditada:
        raise Bloqueado(f"las columnas {pide_auditoria} piden auditoría y la tabla '{tabla}' no está auditada: este playbook no cambia la auditoría de la tabla, "
                        "así que es un hueco del diseño que se resuelve con el aprobador, no acá")
    return {"metadata_id": metadata_id, "auditada": auditada}


def _pertenencia(dv, datos, solution_id, info, en_entorno, verificadas, difs, notas):
    """Pertenencia a la solución. Siempre se OBSERVA (la tabla y cada columna, con `rootcomponentbehavior`); solo se EXIGE si `variante:pertenencia` está
    verificada. La skill fija la expectativa de la tabla (figura en la solución con `rootcomponentbehavior` distinto de 0) y deja pendiente qué filas
    crea cada columna: por eso de las columnas solo se informa."""
    consulta = "la consulta de pertenencia a la solución (GET solutioncomponents)"
    filas = tb._lista(dv, f"solutioncomponents?$filter=_solutionid_value eq {solution_id} and objectid eq {info['metadata_id']} and componenttype eq {tb.COMPONENTE_TABLA}", consulta)
    comportamientos = [exigir_forma(f.get("rootcomponentbehavior"), int, consulta, f"value[{i}].rootcomponentbehavior", permite_nulo=True) for i, f in enumerate(filas)]
    por_columna = []
    for c in datos["columnas"]:
        g = en_entorno.get(c["nombre"])
        if g is None:
            continue  # ausente (fórmula pendiente): no hay nada que observar
        id_columna = exigir_forma(g.get("MetadataId"), str, "la consulta de las columnas", f"value[{g['_i']}].MetadataId", no_vacio=True)
        del_columna = tb._lista(dv, f"solutioncomponents?$filter=_solutionid_value eq {solution_id} and objectid eq {id_columna} and componenttype eq {COMPONENTE_ATRIBUTO}", consulta)
        por_columna.append(f"{c['nombre']}: {len(del_columna)} fila(s)")
    if "variante:pertenencia" in verificadas:
        if len(filas) != 1 or comportamientos[0] in (None, 0):
            difs.append(f"pertenencia a la solución: la tabla '{datos['tabla']}' tiene que figurar una vez en la solución con rootcomponentbehavior distinto de 0 "
                        f"(0 la arrastraría entera al managed); hay {len(filas)} fila(s), rootcomponentbehavior {comportamientos}")
    else:
        notas.append(f"pertenencia a la solución NO verificada (criterio pendiente de fijar en el primer ciclo): la tabla '{datos['tabla']}' tiene {len(filas)} fila(s) "
                     f"en solutioncomponents (rootcomponentbehavior {comportamientos}); columnas: {'; '.join(por_columna) or 'ninguna'}")


def verificar(dv, datos, identidad, solution_id, info, verificadas):
    """Una sola función para los tres caminos (recién creado, ya existía, `--solo-verificar`)."""
    difs = []
    declaradas = [dict(c, _primaria=False) for c in datos["columnas"]]
    en_entorno, pendientes, notas = tb._verificar_columnas(dv, datos["tabla"], declaradas, identidad["idiomas"], difs, solo_prefijo=identidad["prefijo"])
    _pertenencia(dv, datos, solution_id, info, en_entorno, verificadas, difs, notas)
    return {"existe": True, "diffs": difs, "metadata_id": info["metadata_id"], "pendientes": pendientes, "notas": notas}


def _faltan_solo_columnas(datos, diffs):
    """Si TODAS las diferencias son columnas (no fórmulas) que el playbook declara y el entorno no tiene, devuelve esas columnas en orden; si hay
    cualquier otra diferencia, `None`: una columna que difiere no se toca."""
    prefijo = "falta la columna "
    declaradas = {c["nombre"] for c in datos["columnas"]}
    faltan = set()
    for d in diffs:
        if not d.startswith(prefijo) or d[len(prefijo):] not in declaradas:
            return None
        faltan.add(d[len(prefijo):])
    return [c for c in datos["columnas"] if c["nombre"] in faltan] or None


def agregar(dv, datos, identidad, choices, columnas):
    """Alta de cada columna, en el orden del playbook, y publicar la tabla una vez. Devuelve `None` o el problema."""
    tabla, hechas = datos["tabla"], []
    for c in columnas:
        est, cuerpo, _ = escribir_metadatos(dv, "POST", f"EntityDefinitions(LogicalName='{tabla}')/Attributes",
                                            tb._columna_payload(c, identidad["idiomas"], choices), solucion=identidad["solucion"])
        if est != 204:
            return f"falló agregar la columna {c['nombre']}: HTTP {est} {cuerpo}; ya se agregaron: {', '.join(hechas) or 'ninguna'}"
        hechas.append(c["nombre"])
    xml = f"<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>"
    est, cuerpo, _ = escribir_metadatos(dv, "POST", "PublishXml", {"ParameterXml": xml})
    return None if est == 204 else f"se agregaron las columnas ({', '.join(hechas)}) pero falló publicar la tabla '{tabla}': HTTP {est} {cuerpo}"


def _flujo(dv, datos, identidad, solo_verificar, componente, verificadas, contexto):
    solucion = identidad["solucion"]
    solution_id = comprobar_solucion_e_idioma(dv, identidad)
    choices = tb.comprobar_choices(dv, datos)
    info = comprobar_tabla(dv, datos, identidad)

    def ver():
        contexto["actual"] = verificar(dv, datos, identidad, solution_id, info, verificadas)
        return contexto["actual"]

    actual = ver()
    if not actual["diffs"]:
        return "ya_existia", componente, f"tabla estándar {datos['tabla']} (MetadataId {info['metadata_id']}); las {len(datos['columnas'])} columnas coinciden en todo lo que exige la receta; no se modificó nada"
    faltan = _faltan_solo_columnas(datos, actual["diffs"])
    if solo_verificar or not faltan:
        return "difiere", componente, "; ".join(actual["diffs"])
    problema = agregar(dv, datos, identidad, choices, faltan)
    if problema:
        return "error", componente, problema
    final = ver()
    if final["diffs"]:
        return "error", componente, f"se agregaron las columnas pero no coincide con el playbook al releer: {'; '.join(final['diffs'])}"
    return "creado", componente, (f"tabla estándar {datos['tabla']} (MetadataId {info['metadata_id']}); se agregaron {len(faltan)} columnas "
                                   f"({', '.join(c['nombre'] for c in faltan)}) en la solución '{solucion}'")


def construir(ruta_playbook, solo_verificar, fabrica_cliente, verificadas, permitir_no_verificadas, reparaciones=None):
    """Nunca lanza: siempre devuelve `(estado, componente, detalle)`."""
    componente = os.path.basename(ruta_playbook)
    paso = "leer el playbook"
    try:
        secciones = dividir_secciones(leer_texto(ruta_playbook))
        paso = "leer el bloque '## 2. Qué se crea'"
        datos = obtener_componente(secciones, TIPO)
        if isinstance(datos.get("tabla"), str):
            componente = datos["tabla"]
        paso = "leer el bloque '## 1. Identidad'"
        identidad = obtener_identidad(secciones)
        paso = "comprobar las banderas"
        pedidas = sorted(b for b, activa in (reparaciones or {}).items() if activa)
        if pedidas:
            raise ErrorPlaybook(f"las banderas {pedidas} no aplican a un playbook columnas-estandar: una columna que difiere no se toca, se informa")
        paso = "validar el bloque de la variante"
        validar_playbook(datos, identidad)
        paso = "comprobar qué características están verificadas"
        comprobar_verificadas(datos, verificadas, permitir_no_verificadas)
    except ErrorPlaybook as e:
        return "error", componente, str(e)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except OSError as e:
        return "error", componente, f"no se pudo leer el playbook '{ruta_playbook}': {e}"
    except Exception as e:
        return "error", componente, f"fallo inesperado validando el playbook (offline), al {paso}: {type(e).__name__}: {e}"

    print(f"Playbook leído y validado (offline). tipo={TIPO} tabla={componente} columnas={len(datos['columnas'])}")
    try:
        dv = fabrica_cliente()
    except Exception:
        return "error", componente, tb.MENSAJE_FALLO_CLIENTE

    rastro = Rastro(dv)
    contexto = {}
    try:
        estado, comp, detalle = _flujo(rastro, datos, identidad, solo_verificar, componente, verificadas, contexto)
        return tb._con_pendientes(estado, comp, detalle, contexto.get("actual"), datos, identidad)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except ErrorEntorno as e:
        return "error", componente, str(e)
    except Exception as e:
        return "error", componente, f"fallo inesperado hablando con Dataverse, durante {rastro.en_curso}: {type(e).__name__}: {e}"
