#!/usr/bin/env python3
"""Construye UNA relación 1:N de Dataverse con su columna lookup, desde un
playbook de tipo `relacion`. Contrato: `power-platform-construir`,
`references/modelo-datos/patrones.md` §1 y §2.3.

    python3 herramientas/construir/relacion.py <playbook.md> [--solo-verificar] [--publicar] [--corregir-lookup] [--permitir-no-verificadas]

La relación y su lookup nacen juntos, en un solo POST; después se publican
las tablas tocadas (una relación, a diferencia de una tabla, no se publica
sola). Verificado contra la plataforma el 2026-09-20: al crear, la plataforma
IGNORA la auditoría del lookup (la deja desactivada); si el playbook la pide,
se reenvía la definición completa del lookup con PUT antes de publicar. Y una
relación NO es un componente propio de la solución: viaja dentro de la tabla
hija, que tiene que estar en la solución con todos sus subcomponentes.

Nunca modifica ni borra: si la relación existe y no coincide, informa
`difiere`. Dos salvedades explícitas: `--publicar` vuelve a publicar solo si
coincide en todo, y `--corregir-lookup` repara solo la auditoría del lookup,
cuando esa es la única diferencia.

Variantes de `relacion.md` (sección 6), ambas con 0 ciclos completos:
- A, autorreferida no jerárquica (`tabla_padre` igual a `tabla_hija`, con
  `jerarquica: false`): implementada pero NO verificada contra la plataforma.
  Mientras no lo esté, la herramienta devuelve `bloqueado`; se ensaya primero
  sobre una tabla descartable con `--permitir-no-verificadas` (solo vale si el
  nombre de la tabla lleva `_tbl_zz`).
- B, tabla hija estándar (por ejemplo `email`): NO implementada. Se detecta
  con `IsCustomEntity` de la hija y se devuelve `bloqueado`.

Última línea de la salida: JSON de una línea con `estado`
(creado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import os
import re
import sys

_AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _AQUI)
sys.path.insert(0, os.path.dirname(_AQUI))

from _comun import (  # noqa: E402
    Bloqueado,
    ErrorEntorno,
    ErrorPlaybook,
    Rastro,
    auditoria_administrada,
    comparar_descripcion,
    comparar_etiquetas,
    comprobar_solucion_e_idioma,
    dividir_secciones,
    escribir_metadatos,
    etiqueta_uniforme,
    etiqueta_web_api,
    exigir_forma,
    leer_entorno,
    leer_texto,
    nivel_requerido,
    obtener_componente,
    obtener_identidad,
    salida,
    validar_textos_por_idioma,
    valor_administrado,
)

TIPO = "relacion"
LARGO_MAXIMO_RELACION = 100  # schema name de una relación (Learn, tabla EntityRelationship)
LARGO_MAXIMO_IDENTIFICADOR = 95
COMPONENTE_TABLA = 1  # la relación viaja dentro de su tabla hija: no tiene fila propia en solutioncomponents
INCLUYE_SUBCOMPONENTES = 0  # rootcomponentbehavior de una tabla agregada con todo lo suyo
TABLAS_DEL_SISTEMA = {"systemuser"}  # únicas tablas ajenas que el formato admite como padre
NOMBRE_ESTANDAR = r"[a-z][a-z0-9_]*"  # nombre lógico de una tabla estándar (variante B): se detecta, no se construye

# Gate "Ensayo". Todo lo que el formato admite y todavía no se ensayó contra la plataforma queda bloqueado: primero se ensaya sobre una
# tabla descartable (`…_tbl_zz…`, con --permitir-no-verificadas) y recién después se agrega a VERIFICADAS_EN_PLATAFORMA.
# La variante A tiene 0 ciclos completos (relacion.md §6 y la receta, patrones.md §2.3): nada de lo suyo se ensayó.
AUTORREFERIDA = "variante:autorreferida"
CARACTERISTICAS = frozenset({AUTORREFERIDA})
VERIFICADAS_EN_PLATAFORMA = frozenset()
INFIJO_DESCARTABLE = "_tbl_zz"
PENDIENTE_DE_VERIFICAR = {
    AUTORREFERIDA: "la relación autorreferida no jerárquica (variante A de relacion.md, sección 6) tiene 0 ciclos completos. Está sin verificar: "
                   "que el alta con IsHierarchical: false explícito sea aceptada y la plataforma lo devuelva así; el error exacto que devuelve la plataforma online "
                   "con comportamiento parental en una autorreferida (la prohibición del formato no depende de eso); y si la plataforma impide que un registro "
                   "se apunte a sí mismo o forme ciclos (Learn no lo documenta)",
}

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

# `jerarquica` NO está en CLAVES: es opcional y solo existe cuando `tabla_padre` es igual a `tabla_hija` (ver `_validar_jerarquica`).
CLAVES = {"tipo": str, "nombre": str, "tabla_padre": str, "tabla_hija": str, "comportamiento": str, "lookup": dict}
# `displayname` es un objeto con un texto por cada idioma de `idiomas`; `descripcion` es prosa y se escribe igual en cada idioma.
CLAVES_LOOKUP = {"nombre": str, "displayname": dict, "descripcion": str, "requerida": bool, "auditoria": bool}

# Cascadas de cada comportamiento estándar (Learn, "Edit relationships").
_SIN = {"Assign": "NoCascade", "Merge": "NoCascade", "Reparent": "NoCascade", "Share": "NoCascade", "Unshare": "NoCascade", "RollupView": "NoCascade"}
CASCADAS = {
    "parental": {"Assign": "Cascade", "Delete": "Cascade", "Merge": "NoCascade", "Reparent": "Cascade", "Share": "Cascade", "Unshare": "Cascade", "RollupView": "NoCascade"},
    "restringido": dict(_SIN, Delete="Restrict"),
    "quitar_vinculo": dict(_SIN, Delete="RemoveLink"),
}


# ---------------------------------------------------------------------------
# Validación sin red
# ---------------------------------------------------------------------------
def _validar_bloque(datos, claves, de_donde):
    if not isinstance(datos, dict):
        raise ErrorPlaybook(f"{de_donde} tiene que ser un objeto, no {datos!r}")
    faltan, sobran = sorted(set(claves) - set(datos)), sorted(set(datos) - set(claves))
    if faltan or sobran:
        raise ErrorPlaybook(f"claves de {de_donde} inválidas: faltan {faltan or 'ninguna'}, sobran {sobran or 'ninguna'}")
    for clave, tipo in claves.items():
        valor = datos[clave]
        donde = f"'{clave}'" if de_donde == "la relación" else f"{de_donde}.{clave}"
        if tipo is bool and not isinstance(valor, bool):
            raise ErrorPlaybook(f"{donde} tiene que ser true o false, no {valor!r}")
        if tipo is str and (not isinstance(valor, str) or not valor.strip()):
            raise ErrorPlaybook(f"{donde} tiene que ser texto no vacío, no {valor!r}")
        if tipo is dict and not isinstance(valor, dict):
            raise ErrorPlaybook(f"{donde} tiene que ser un objeto, no {valor!r}")


def _corto(tabla, prefijo, abrev):
    """`<prefijo>_<abrev>_tbl_cliente` → `cliente`; una tabla del sistema queda igual."""
    inicio = f"{prefijo}_{abrev}_tbl_"
    return tabla[len(inicio):] if tabla.startswith(inicio) else tabla


def _validar_jerarquica(datos, autorreferida):
    """La clave `jerarquica` aparece SOLO cuando `tabla_padre` es igual a `tabla_hija`, y vale `false`. En cualquier otra relación no está
    (así los playbooks existentes, que no la traen, siguen siendo válidos)."""
    if not autorreferida:
        if "jerarquica" in datos:
            raise ErrorPlaybook("sobra la clave 'jerarquica': solo va cuando 'tabla_padre' es igual a 'tabla_hija' (relación autorreferida, variante A de relacion.md)")
        return
    if "jerarquica" not in datos:
        raise ErrorPlaybook("'tabla_padre' y 'tabla_hija' son la misma tabla (relación autorreferida, variante A de relacion.md): falta la clave 'jerarquica', que tiene que ser false")
    valor = datos["jerarquica"]
    if not isinstance(valor, bool):
        raise ErrorPlaybook(f"'jerarquica' tiene que ser true o false, no {valor!r}")
    if valor:
        raise ErrorPlaybook("'jerarquica' = true: marcar una relación como jerárquica es una decisión sobre toda la tabla (es única por tabla y toca rollups y vistas) "
                            "y es otro tipo de playbook; este formato solo cubre 'jerarquica': false")


def _validar_estructura(datos, identidad):
    # `jerarquica` es opcional: se valida aparte, cuando ya se sabe si la relación es autorreferida.
    _validar_bloque({c: v for c, v in datos.items() if c != "jerarquica"} if isinstance(datos, dict) else datos, CLAVES, "la relación")
    prefijo, abrev = identidad["prefijo"], identidad["abrev"]
    patron_tabla = rf"{prefijo}_{abrev}_tbl_[a-z0-9]+"
    # La hija es una tabla de la solución o, en la variante B, una tabla estándar. Esta última se acepta acá solo para poder
    # decir `bloqueado` con el motivo exacto contra el entorno (IsCustomEntity); un nombre con el prefijo del proyecto que no cumple el
    # patrón es un error de tipeo, no una tabla estándar.
    hija = datos["tabla_hija"]
    if not re.fullmatch(patron_tabla, hija) and (hija.startswith(f"{prefijo}_") or not re.fullmatch(NOMBRE_ESTANDAR, hija)):
        raise ErrorPlaybook(f"'tabla_hija' = {hija!r} no cumple {patron_tabla} ni es el nombre lógico de una tabla estándar (en minúscula, sin el prefijo del proyecto)")
    if datos["tabla_padre"] not in TABLAS_DEL_SISTEMA and not re.fullmatch(patron_tabla, datos["tabla_padre"]):
        raise ErrorPlaybook(f"'tabla_padre' = {datos['tabla_padre']!r} no cumple {patron_tabla} ni es una de {sorted(TABLAS_DEL_SISTEMA)}")
    if datos["comportamiento"] not in CASCADAS:
        raise ErrorPlaybook(f"'comportamiento' tiene que ser uno de {sorted(CASCADAS)}, no {datos['comportamiento']!r}")
    autorreferida = datos["tabla_padre"] == datos["tabla_hija"]
    _validar_jerarquica(datos, autorreferida)

    # BP-PP-188: <prefijo>_<abrev>_<padre>_<hijo>, con sufijo libre si hay más de una relación entre el mismo par.
    base = f"{prefijo}_{abrev}_{_corto(datos['tabla_padre'], prefijo, abrev)}_{_corto(datos['tabla_hija'], prefijo, abrev)}"
    if not re.fullmatch(rf"{re.escape(base)}(_[a-z0-9]+)?", datos["nombre"]):
        raise ErrorPlaybook(f"'nombre' = {datos['nombre']!r} tiene que ser {base!r}, o {base + '_<sufijo>'!r} si hay más de una relación entre esas dos tablas (BP-PP-188, todo en minúscula)")
    if len(datos["nombre"]) > LARGO_MAXIMO_RELACION:
        raise ErrorPlaybook(f"'nombre' tiene {len(datos['nombre'])} caracteres; el máximo de una relación es {LARGO_MAXIMO_RELACION}")

    k = datos["lookup"]
    _validar_bloque(k, CLAVES_LOOKUP, "lookup")
    if not re.fullmatch(rf"{prefijo}_[a-z0-9]+", k["nombre"]):
        raise ErrorPlaybook(f"lookup.nombre = {k['nombre']!r} no cumple {prefijo}_[a-z0-9]+ (todo en minúscula, BP-PP-184)")
    if len(k["nombre"]) > LARGO_MAXIMO_IDENTIFICADOR:
        raise ErrorPlaybook(f"lookup.nombre tiene {len(k['nombre'])} caracteres; el máximo es {LARGO_MAXIMO_IDENTIFICADOR}")

    if autorreferida:
        # Variante A (relacion.md §6): nunca parental (la cascada recorrería ciclos y la tabla es padre e hija a la vez), y el lookup no es requerido
        # (el primer registro de la tabla no podría cumplirlo). La prohibición del formato no depende del error que dé la plataforma.
        if datos["comportamiento"] == "parental":
            raise ErrorPlaybook("'comportamiento' = 'parental' no se admite en una relación autorreferida (la cascada recorrería ciclos y la tabla es padre e hija a la vez): "
                                "usar 'restringido' o 'quitar_vinculo'")
        if k["requerida"]:
            raise ErrorPlaybook("lookup.requerida tiene que ser false en una relación autorreferida: el primer registro de la tabla no podría cumplirlo")


def caracteristicas_usadas(datos):
    """Lo que usa el playbook y puede no estar verificado contra la plataforma."""
    return {AUTORREFERIDA} if datos["tabla_padre"] == datos["tabla_hija"] else set()


def comprobar_verificadas(datos, verificadas, permitir_no_verificadas):
    """Gate "Ensayo": `bloqueado` si el playbook usa algo no verificado, salvo con --permitir-no-verificadas y solo sobre una tabla descartable
    (la columna lookup nace en la tabla, así que lo que tiene que ser descartable es la tabla, no el nombre de la relación)."""
    faltan = sorted(caracteristicas_usadas(datos) - set(verificadas))
    if not faltan:
        return
    pendiente = "; ".join(PENDIENTE_DE_VERIFICAR[f] for f in faltan)
    if not permitir_no_verificadas:
        raise Bloqueado(
            f"el playbook usa algo todavía no verificado contra la plataforma ({', '.join(faltan)}): {pendiente}. "
            f"Primero se ensaya sobre una tabla descartable ('…{INFIJO_DESCARTABLE}…') con --permitir-no-verificadas"
        )
    if INFIJO_DESCARTABLE not in datos["tabla_hija"]:
        raise Bloqueado(
            f"--permitir-no-verificadas solo vale sobre una tabla descartable, con '{INFIJO_DESCARTABLE}' en el nombre de la tabla; "
            f"{datos['tabla_hija']!r} no lo es. Sin verificar ({', '.join(faltan)}): {pendiente}"
        )


def validar_playbook(datos, identidad):
    _validar_estructura(datos, identidad)
    validar_textos_por_idioma(datos["lookup"]["displayname"], identidad["idiomas"], "lookup.displayname")


# ---------------------------------------------------------------------------
# Cuerpo del POST
# ---------------------------------------------------------------------------
def construir_payload(datos, identidad, atributo_referenciado):
    idiomas, k = identidad["idiomas"], datos["lookup"]
    payload = {
        "@odata.type": "Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata",
        "SchemaName": datos["nombre"],
        "ReferencedEntity": datos["tabla_padre"],
        "ReferencedAttribute": atributo_referenciado,
        "ReferencingEntity": datos["tabla_hija"],
        "CascadeConfiguration": dict(CASCADAS[datos["comportamiento"]]),
        "AssociatedMenuConfiguration": {"Behavior": "UseCollectionName", "Group": "Details", "Order": 10000},
        "Lookup": {
            "@odata.type": "Microsoft.Dynamics.CRM.LookupAttributeMetadata",
            "AttributeType": "Lookup",
            "AttributeTypeName": {"Value": "LookupType"},
            "SchemaName": k["nombre"],
            "DisplayName": etiqueta_web_api(k["displayname"], idiomas),
            "Description": etiqueta_uniforme(k["descripcion"], idiomas),
            "RequiredLevel": nivel_requerido(k["requerida"]),
            "IsAuditEnabled": auditoria_administrada(k["auditoria"]),
        },
    }
    if datos["tabla_padre"] == datos["tabla_hija"]:
        # Variante A: la tabla se referencia a sí misma. `IsHierarchical: false` va EXPLÍCITO, no se confía en el valor por omisión.
        payload["IsHierarchical"] = False
    return payload


# ---------------------------------------------------------------------------
# Lectura del entorno: una sola función para los tres caminos
# ---------------------------------------------------------------------------
def _exigir_hija_propia(t, tabla, consulta, identidad):
    """Precondición de NEGOCIO (`bloqueado`): la tabla hija es una tabla de la solución. La forma de `IsCustomEntity` se valida por tipo
    (nulo o texto es `error`: no se pudo averiguar). Una hija estándar (por ejemplo `email`) es la variante B de relacion.md §6, sin ensayar:
    NO se crea. Esto también protege a la relación común de crear por error un lookup sobre una tabla estándar.
    La tabla PADRE `systemuser` NO es este caso: el padre estándar ya estaba soportado (se referencia, no se modifica), por eso acá solo se mira la hija."""
    if exigir_forma(t.get("IsCustomEntity"), bool, consulta, "IsCustomEntity"):
        patron = rf"{identidad['prefijo']}_{identidad['abrev']}_tbl_[a-z0-9]+"
        if not re.fullmatch(patron, tabla):
            raise Bloqueado(f"la tabla hija '{tabla}' es una tabla propia (IsCustomEntity = true) pero no es una tabla de la solución (no cumple {patron}): "
                            "el lookup se crea en una tabla de la solución")
        return
    raise Bloqueado(
        f"la tabla hija '{tabla}' no es una tabla propia (IsCustomEntity = false): es la variante B de relacion.md (sección 6), la relación cuya hija es una tabla "
        "estándar, y está sin ensayar. Falta verificar: la pertenencia a la solución (qué filas de solutioncomponents crean la relación y su lookup, y con qué "
        "rootcomponentbehavior queda la tabla estándar), los parámetros exactos de CanBeReferencing / CanBeReferenced y la publicación de una tabla estándar. "
        "La herramienta no crea esta variante ni supone cómo se verifica"
    )


def comprobar_tablas(dv, datos, identidad):
    """Precondición: las dos tablas existen, y la hija es una tabla de la solución. Devuelve la clave primaria de la
    tabla padre (el atributo referenciado) y el MetadataId de la tabla hija."""
    claves, ids = {}, {}
    for rol in ("tabla_padre", "tabla_hija"):
        tabla = datos[rol]
        consulta = f"la consulta de la tabla '{tabla}' (GET EntityDefinitions)"
        t = leer_entorno(dv, f"EntityDefinitions(LogicalName='{tabla}')?$select=LogicalName,PrimaryIdAttribute,IsCustomEntity", consulta, admite_404=True)
        if t is None:
            raise Bloqueado(f"la tabla '{tabla}' ({rol}) no existe en el entorno: se construye antes que sus relaciones")
        if rol == "tabla_hija":
            _exigir_hija_propia(t, tabla, consulta, identidad)
        claves[rol] = exigir_forma(t.get("PrimaryIdAttribute"), str, consulta, "PrimaryIdAttribute", no_vacio=True)
        ids[rol] = exigir_forma(t.get("MetadataId"), str, consulta, "MetadataId", no_vacio=True)
    return claves["tabla_padre"], ids["tabla_hija"]


def _verificar(dv, datos, identidad, solution_id, atributo_referenciado, id_tabla_hija):
    idiomas, difs, k = identidad["idiomas"], [], datos["lookup"]
    cr = f"la consulta de la relación (GET RelationshipDefinitions '{datos['nombre']}')"
    r = leer_entorno(dv, f"RelationshipDefinitions(SchemaName='{datos['nombre']}')/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata", cr, admite_404=True)
    if r is None:
        return {"existe": False, "diffs": None, "metadata_id": None}
    metadata_id = exigir_forma(r.get("MetadataId"), str, cr, "MetadataId", no_vacio=True)
    for campo, tipo, esperado in [("ReferencedEntity", str, datos["tabla_padre"]), ("ReferencedAttribute", str, atributo_referenciado),
                                  ("ReferencingEntity", str, datos["tabla_hija"]), ("ReferencingAttribute", str, k["nombre"]),
                                  ("IsCustomRelationship", bool, True), ("IsManaged", bool, False),
                                  # El formato nunca cubre una relación jerárquica: si la plataforma la devuelve como tal es una diferencia real (cambia el
                                  # alcance de rollups y vistas de la tabla). En una no autorreferida no puede serlo, pero el campo se pide igual: una respuesta sin él no se verificó.
                                  ("IsHierarchical", bool, False)]:
        real = exigir_forma(r.get(campo), tipo, cr, campo)
        if real != esperado:
            difs.append(f"{campo}: entorno={real!r} playbook={esperado!r}")
    cascada = exigir_forma(r.get("CascadeConfiguration"), dict, cr, "CascadeConfiguration")
    for accion, esperado in CASCADAS[datos["comportamiento"]].items():
        real = exigir_forma(cascada.get(accion), str, cr, f"CascadeConfiguration.{accion}")
        if real != esperado:
            difs.append(f"CascadeConfiguration.{accion}: entorno={real!r} playbook={esperado!r} (comportamiento '{datos['comportamiento']}')")

    n = k["nombre"]
    cl = f"la consulta de la columna lookup (GET Attributes '{n}' LookupAttributeMetadata)"
    a = leer_entorno(dv, f"EntityDefinitions(LogicalName='{datos['tabla_hija']}')/Attributes(LogicalName='{n}')/Microsoft.Dynamics.CRM.LookupAttributeMetadata"
                         "?$select=LogicalName,SchemaName,AttributeTypeName,Targets,RequiredLevel,IsAuditEnabled,IsSecured,DisplayName,Description", cl)
    tipo_real = valor_administrado(a.get("AttributeTypeName"), str, cl, "AttributeTypeName")
    if tipo_real != "LookupType":
        difs.append(f"{n}.tipo: entorno={tipo_real!r} playbook='LookupType'")
    destinos = exigir_forma(a.get("Targets"), [str], cl, "Targets")
    if destinos != [datos["tabla_padre"]]:
        difs.append(f"{n}.Targets: entorno={destinos!r} playbook={[datos['tabla_padre']]!r}")
    nivel = valor_administrado(a.get("RequiredLevel"), str, cl, "RequiredLevel")
    if nivel != ("ApplicationRequired" if k["requerida"] else "None"):
        difs.append(f"{n}.RequiredLevel: entorno={nivel!r} playbook={'ApplicationRequired' if k['requerida'] else 'None'!r}")
    if valor_administrado(a.get("IsAuditEnabled"), bool, cl, "IsAuditEnabled") != k["auditoria"]:
        difs.append(f"{n}.IsAuditEnabled: entorno={a['IsAuditEnabled']['Value']!r} playbook={k['auditoria']!r}")
    if exigir_forma(a.get("IsSecured"), bool, cl, "IsSecured"):
        difs.append(f"{n}.IsSecured: entorno=True playbook=False")
    # Una etiqueta por CADA idioma del playbook, y ninguna en un idioma que el playbook no declara (los dos sentidos).
    comparar_etiquetas(a.get("DisplayName"), idiomas, f"{n}.DisplayName", k["displayname"], difs, cl, "DisplayName")
    comparar_descripcion(a.get("Description"), idiomas, f"{n}.Description", k["descripcion"], difs, cl, "Description")

    cs = "la consulta de pertenencia a la solución (GET solutioncomponents)"
    sc = leer_entorno(dv, f"solutioncomponents?$select=rootcomponentbehavior&$filter=_solutionid_value eq {solution_id} and objectid eq {id_tabla_hija} and componenttype eq {COMPONENTE_TABLA}", cs)
    filas = exigir_forma(sc.get("value"), [dict], cs, "value")
    if len(filas) != 1:
        difs.append(f"pertenencia a la solución: la tabla hija '{datos['tabla_hija']}' tiene {len(filas)} filas en solutioncomponents, se esperaba 1 (la relación viaja dentro de ella)")
    else:
        alcance = exigir_forma(filas[0].get("rootcomponentbehavior"), int, cs, "value[0].rootcomponentbehavior")
        if alcance != INCLUYE_SUBCOMPONENTES:
            difs.append(f"la tabla hija '{datos['tabla_hija']}' está en la solución pero no incluye todos sus subcomponentes (rootcomponentbehavior={alcance}): la relación no viajaría con ella")
    return {"existe": True, "diffs": difs, "metadata_id": metadata_id}


def ajustar_lookup(dv, datos, identidad):
    """Reenvía la definición completa del lookup con la auditoría del playbook
    (la plataforma la ignora al crear la relación). Devuelve `None` o el texto
    del problema. El GET con cast no trae '@odata.type' y el PUT lo exige."""
    n = datos["lookup"]["nombre"]
    ruta = f"EntityDefinitions(LogicalName='{datos['tabla_hija']}')/Attributes(LogicalName='{n}')"
    consulta = f"la lectura completa de la columna lookup (GET Attributes '{n}')"
    actual = leer_entorno(dv, ruta + "/Microsoft.Dynamics.CRM.LookupAttributeMetadata", consulta)
    aud = exigir_forma(actual.get("IsAuditEnabled"), dict, consulta, "IsAuditEnabled")
    definicion = {k: v for k, v in actual.items() if k != "@odata.context"}
    definicion["@odata.type"] = "Microsoft.Dynamics.CRM.LookupAttributeMetadata"
    definicion["IsAuditEnabled"] = {**aud, "Value": datos["lookup"]["auditoria"]}
    est, cuerpo, _ = escribir_metadatos(dv, "PUT", ruta, definicion, solucion=identidad["solucion"], cabeceras={"MSCRM.MergeLabels": "true"})
    return None if est == 204 else f"falló ajustar la auditoría del lookup {n}: HTTP {est} {cuerpo}"


def _solo_difiere_la_auditoria(datos, diffs):
    return bool(diffs) and all(d.startswith(f"{datos['lookup']['nombre']}.IsAuditEnabled:") for d in diffs)


def _publicar(dv, datos):
    tablas = list(dict.fromkeys(t for t in (datos["tabla_padre"], datos["tabla_hija"]) if t not in TABLAS_DEL_SISTEMA))  # sin repetir: una autorreferida se publica una vez
    xml = "<importexportxml><entities>" + "".join(f"<entity>{t}</entity>" for t in tablas) + "</entities></importexportxml>"
    est, cuerpo, _ = escribir_metadatos(dv, "POST", "PublishXml", {"ParameterXml": xml})
    return None if est == 204 else f"HTTP {est} {cuerpo}"


def _contra_entorno(dv, datos, identidad, solo_verificar, componente, publicar, corregir_lookup):
    solucion = identidad["solucion"]
    solution_id = comprobar_solucion_e_idioma(dv, identidad)
    atributo, id_hija = comprobar_tablas(dv, datos, identidad)
    actual = _verificar(dv, datos, identidad, solution_id, atributo, id_hija)

    if actual["existe"] and corregir_lookup and not solo_verificar and _solo_difiere_la_auditoria(datos, actual["diffs"]):
        problema = ajustar_lookup(dv, datos, identidad) or _publicar(dv, datos)
        if problema:
            return "error", componente, problema
        actual = _verificar(dv, datos, identidad, solution_id, atributo, id_hija)
        if not actual["diffs"]:
            return "ya_existia", componente, (f"MetadataId {actual['metadata_id']}; la relación existía y su única diferencia era la auditoría del lookup, "
                                              f"que la plataforma ignora al crear; se corrigió y ahora coincide en todo; pertenece a '{solucion}'")

    if solo_verificar and not actual["existe"]:
        return "error", componente, "la relación no existe en el entorno; --solo-verificar no crea nada, correr la herramienta sin ese flag primero"
    if actual["existe"]:
        if actual["diffs"]:
            return "difiere", componente, "; ".join(actual["diffs"])
        extra = "no se modificó nada"
        if publicar and not solo_verificar:
            problema = _publicar(dv, datos)
            if problema:
                return "error", componente, f"la relación coincide con el playbook pero falló publicar: {problema}"
            extra = "se publicó de nuevo y no se modificó nada más"
        return "ya_existia", componente, f"MetadataId {actual['metadata_id']}; coincide en todo lo que exige la receta y pertenece a '{solucion}'; {extra}"

    est, cuerpo, _ = escribir_metadatos(dv, "POST", "RelationshipDefinitions", construir_payload(datos, identidad, atributo), solucion=solucion)
    if est != 204:
        return "error", componente, f"la creación falló: HTTP {est} {cuerpo}"
    if datos["lookup"]["auditoria"]:
        problema = ajustar_lookup(dv, datos, identidad)
        if problema:
            return "error", componente, f"la relación se creó pero quedó incompleta: {problema}. Volver a correr con --corregir-lookup"
    problema = _publicar(dv, datos)
    if problema:
        return "error", componente, f"la relación se creó pero falló publicar: {problema}. Verificar con --solo-verificar y, si coincide, volver a correr con --publicar"
    final = _verificar(dv, datos, identidad, solution_id, atributo, id_hija)
    if not final["existe"]:
        return "error", componente, "se creó (204) pero no aparece al releer del entorno"
    if final["diffs"]:
        return "error", componente, f"se creó pero no coincide con el playbook al releer: {'; '.join(final['diffs'])}"
    return "creado", componente, f"MetadataId {final['metadata_id']}, con su lookup {datos['tabla_hija']}.{datos['lookup']['nombre']}, en la solución '{solucion}'"


def construir(ruta_playbook, solo_verificar, fabrica_cliente, publicar=False, corregir_lookup=False,
              verificadas=VERIFICADAS_EN_PLATAFORMA, permitir_no_verificadas=False):
    """Nunca lanza: siempre devuelve `(estado, componente, detalle)`."""
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
        paso = "validar el bloque de la relación"
        validar_playbook(datos, identidad)
        paso = "comprobar qué variantes están verificadas"
        comprobar_verificadas(datos, verificadas, permitir_no_verificadas)
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
        return _contra_entorno(rastro, datos, identidad, solo_verificar, componente, publicar, corregir_lookup)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except ErrorEntorno as e:
        return "error", componente, str(e)
    except Exception as e:
        return "error", componente, f"fallo inesperado hablando con Dataverse, durante {rastro.en_curso}: {type(e).__name__}: {e}"


BANDERAS = ("--solo-verificar", "--publicar", "--corregir-lookup", "--permitir-no-verificadas")
USO = "uso incorrecto: relacion.py <playbook.md> [" + "] [".join(BANDERAS) + "]"


def main():
    argv = sys.argv[1:]
    rutas = [a for a in argv if not a.startswith("--")]
    # Una bandera que la herramienta no conoce es un ERROR: callar lo que no se entendió es lo que hace que no se pueda confiar en ella.
    desconocidas = sorted({a for a in argv if a.startswith("--")} - set(BANDERAS))
    if desconocidas:
        return salida("error", "desconocido", f"bandera(s) que esta herramienta no conoce: {desconocidas}. " + USO)
    if len(rutas) != 1:
        return salida("error", "desconocido", USO)
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(rutas[0], "--solo-verificar" in argv, Dataverse, publicar="--publicar" in argv, corregir_lookup="--corregir-lookup" in argv,
                                            permitir_no_verificadas="--permitir-no-verificadas" in argv)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
