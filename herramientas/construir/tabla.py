#!/usr/bin/env python3
"""Construye UNA tabla de Dataverse con sus columnas propias, desde un playbook
de tipo `tabla`. Contrato: `power-platform-construir`,
`references/modelo-datos/patrones.md` §1 y §2.2.

    python3 herramientas/construir/tabla.py <playbook.md> [--solo-verificar]
                                            [--permitir-no-verificadas] [--corregir-primaria]

La tabla y sus columnas propias viajan en un solo POST: o existe completa o
no existe. La única excepción son las columnas protegidas (seguridad de
columna): la plataforma rechaza con un 400 genérico una columna con
`IsSecured = true` dentro del POST que crea la tabla, y sí la acepta agregada
después (verificado el 2026-09-20); esas se agregan una por una enseguida.
Segunda excepción, también verificada: al crear la tabla la plataforma IGNORA
el largo y el "requerida" de la columna primaria (la deja en 850 y opcional),
salvo que sea autonumérica. Enseguida de crear se reenvía su definición
completa con PUT y se publica. `--corregir-primaria` repara una tabla que ya
existe cuando su ÚNICA diferencia es la primaria: largo, requerida, o pasar
de texto común a autonumérica (nunca otra cosa, y nunca al revés).
`--agregar-columnas` (D-15, 2026-09-21) agrega a una tabla que ya existe las
columnas que el playbook declara y el entorno no tiene, con el mismo cuerpo
que la creación, y solo si esa es su ÚNICA diferencia; nunca cambia ni borra
una columna existente. Ensayado contra la plataforma el 2026-09-21 con
entero, sí/no y texto protegido (playbooks/tabla/ensayos/zz_agregar_*.md).
Las columnas lookup no van acá: nacen con su relación.
Fuera de esos flags de reparación, nunca modifica ni borra: si la tabla
existe y no coincide, informa `difiere`.

Última línea de la salida: JSON de una línea con `estado`
(creado | ya_existia | difiere | bloqueado | error), `componente`, `detalle`.
"""
import decimal
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
    comprobar_solucion_e_idioma,
    dividir_secciones,
    comparar_descripcion,
    comparar_etiquetas,
    escribir_metadatos,
    etiqueta_uniforme,
    etiqueta_web_api,
    EXCEPCIONES_NOMBRES,
    exigir_etiqueta,
    exigir_forma,
    leer_texto,
    obtener_bloque_json,
    obtener_componente,
    obtener_identidad,
    salida,
    textos_del_entorno,
    validar_textos_por_idioma,
)

TIPO = "tabla"
TIPO_VARIANTE = "columnas-estandar"  # columnas propias sobre una tabla estándar (`columnas_estandar.py`); skill 0.2.0, `tabla.md` sección 6
LARGO_MAXIMO_IDENTIFICADOR = 95  # verificado contra la plataforma (2026-09-20)
COMPONENTE_TABLA = 1  # tipo de componente de una tabla en solutioncomponents
ESTADOS_EXITO = ("creado", "actualizado", "ya_existia")
ENTERO_MIN, ENTERO_MAX = -2147483648, 2147483647
MARCADOR_AUTONUMERICO = re.compile(r"\{(SEQNUM:\d+|RANDSTRING:[1-6]|DATETIMEUTC:[^}]+)\}")

MENSAJE_FALLO_CLIENTE = (
    "no se pudo inicializar el cliente de Dataverse (fallo de red o de credenciales); "
    "no se expone el detalle porque podría incluir datos de la solicitud de token"
)

# Claves de cada bloque y su tipo. `bool` y `int` se validan estrictos.
CLAVES_TABLA = {"tipo": str, "nombre": str, "displayname": dict, "displayname_plural": dict, "descripcion": str,
                "propiedad": str, "notas": bool, "actividades": bool, "auditoria": bool, "primaria": dict, "columnas": list}
CLAVES_PRIMARIA = {"nombre": str, "displayname": dict, "descripcion": str, "largo": int, "requerida": bool, "autonumerico": "texto_o_vacio"}
CLAVES_COLUMNA = {"nombre": str, "displayname": dict, "descripcion": str, "tipo": str, "requerida": bool, "protegida": bool, "auditoria": bool}
CLAVES_POR_TIPO = {
    "texto": {"largo": int},
    "autonumerico": {"largo": int, "formato": str},
    "memo": {"largo": int},
    "entero": {"minimo": int, "maximo": int},
    "decimal": {"precision": int, "minimo": "numero", "maximo": "numero"},
    # La fórmula la crea una persona en el editor de Power Apps (la plataforma no soporta definirla por código); la herramienta solo la lee (ensayada 2026-10-09).
    # `precision` se suma solo cuando `resultado` es decimal (ver `_claves_de_columna`).
    "formula": {"expresion": str, "resultado": str, "depende_de": list},
    "choice": {"choice": str},
    "sino": {"etiqueta_si": dict, "etiqueta_no": dict, "defecto": bool},
    "fecha": {},
    "fechahora": {},
    "archivo": {"tamano_kb": int},
}
# Motivos de estado (skill 0.2.0, `tabla.md`). La clave es opcional al LEER: un playbook de la versión 0.1.0 no la trae y significa `null`.
CLAVES_MOTIVO = {"statecode": int, "valor": int, "origen": str, "etiqueta": dict, "descripcion": "texto_o_vacio"}
ESTADOS_DE_TABLA = (0, 1)  # statecode: activo / inactivo; fijos en la plataforma, no se crean
MOTIVO_POR_DEFECTO = {0: 1, 1: 2}  # statecode -> valor del motivo que trae la plataforma
PROPIEDAD = {"usuario": "UserOwned", "organizacion": "OrganizationOwned"}

# La variante `columnas-estandar` (`columnas_estandar.py`): columnas propias sobre una tabla estándar. No son características de un playbook de tabla,
# pero el mismo gate (`verificadas`) las gobierna. Ensayada el 2026-10-09 (Dev, org36e60d9d; playbook `email_zz`: entero `sanic_zztiemporespuesta`
# sobre `email`, `--permitir-no-verificadas` → `creado`): la tabla estándar aceptó la columna (CanCreateAttributes) y se publicó; en `solutioncomponents`
# la tabla `email` quedó UNA vez con `rootcomponentbehavior` 1 y la columna con 1 fila; la solución exportada trae `<RootComponent type="1"
# schemaName="email" behavior="1" />` y en customizations.xml la entidad Email trae SOLO el atributo `sanic_zztiemporespuesta` (sin formularios ni vistas).
# Criterio de pertenencia fijado: la tabla figura en la solución exactamente una vez con `rootcomponentbehavior` distinto de 0 y cada columna con 1 fila.
CARACTERISTICAS_DE_LA_VARIANTE = frozenset({"variante:columnas-estandar", "variante:pertenencia"})
SIN_ENSAYAR_DE_LA_VARIANTE = frozenset()
# Todo lo que el formato admite…
CARACTERISTICAS = frozenset({f"tipo:{t}" for t in CLAVES_POR_TIPO} | {"tabla:auditoria", "columna:auditoria", "columna:protegida", "primaria:autonumerica",
                                                                       "decimal:precision0", "motivos_estado"})
# …lo que la skill 0.2.0 agregó y NO tiene ciclo completo contra la plataforma (`tabla.md` y la receta de `construir`, 2.2:
# «Características que el playbook ya admite y la herramienta aún no ensayó»). Queda la precisión 0 de un decimal…
SIN_ENSAYAR = frozenset({"decimal:precision0"})
# …y lo que ya se comprobó contra la plataforma real. Un playbook que usa algo
# que no está acá se bloquea: primero se ensaya sobre una tabla descartable
# (`…_tbl_zz…`, con --permitir-no-verificadas) y recién después se mueve de SIN_ENSAYAR a acá, con la fecha del ensayo.
# Los 9 tipos de la versión 0.1.0 de la skill (texto, autonumérico, multilínea, entero, choice, sí/no, fecha, fecha y hora, archivo),
# la primaria autonumérica, la auditoría de tabla y de columna y una columna protegida se ensayaron con el proyecto 2026-001
# (2026-09-20 y 2026-09-21: creado, releído y `ya_existia`).
# `tipo:decimal` y `motivos_estado` se ensayaron el 2026-10-09 (Dev, org36e60d9d, tabla descartable `sanic_carp_tbl_zzensayo`, `--permitir-no-verificadas`:
# `creado`, verificación propia OK y releído a mano en `EntityDefinitions`):
# - Decimal: `MinValue`/`MaxValue`/`Precision` vuelven exactos (0 / 999999999.99 / 2 y -1000 / 1000 / 4) e `IsAuditEnabled` se respeta; en el XML
#   exportado es `<Type>decimal</Type>` con `MinValue`, `MaxValue` y `Accuracy` (la precisión).
# - Motivos de estado: `UpdateOptionValue` sobre statuscode SÍ renombra los dos motivos por defecto (1 y 2) en 1033 y 3082; `InsertStatusValue` respeta
#   el `Value` explícito (159460001..3) con su `State`; `$expand=OptionSet` sobre `StatusAttributeMetadata` devuelve las opciones con `State`; un alta
#   sin statuscode toma statecode 0 / statuscode 1; un PATCH de statecode 0 con un statuscode del estado 1 lo rechaza la plataforma (400); statecode y
#   statuscode nacen con `IsAuditEnabled = true` si la tabla es auditada. En el XML: `<attribute PhysicalName="statuscode">` con `<statuses><status value state>`.
# `tipo:formula` se ensayó el 2026-10-09 (Dev, org36e60d9d, misma tabla descartable): una persona creó la columna en el editor de Power Apps (dentro de la
# solución; la herramienta NO la crea y eso no cambia) y la herramienta la leyó y la exportó:
# - `FormulaDefinition` es EXACTAMENTE la expresión tipeada (sin `=` ni envoltorio), `SourceType` 3, `Precision`, `MinValue`/`MaxValue` ±100.000.000.000
#   (los fija la plataforma), `IsValidForCreate`/`IsValidForUpdate` false, `RequiredLevel` None, `IsSecured` y `IsAuditEnabled` false.
# - El editor deja `DisplayName` SOLO en 1033 y `Description` vacía (solo 1033 con ""): `--corregir-nombres` lo completa. La reparación que funcionó es el
#   GET de la definición completa con el cast del resultado (`DecimalAttributeMetadata`), `@odata.type`, `DisplayName` y `Description` en los dos idiomas y
#   PUT con `MSCRM.MergeLabels` y `MSCRM.SolutionUniqueName` (204) + PublishXml; `FormulaDefinition`, `SourceType` y `Precision` quedaron intactos.
# - Pertenencia: la tabla está en la solución con rootcomponentbehavior 0 y la columna no tiene fila propia en solutioncomponents (como las demás).
# - En el XML exportado: `<Type>decimal</Type>`, `<SourceType>3</SourceType>`, `<Accuracy>` y `<FormulaDefinitionFileName>`; la expresión va en el yaml
#   `Formulas/<tabla>-FormulaDefinitions.yaml` del zip (`muestra_tabla.py` la compara).
VERIFICADAS_EN_PLATAFORMA = frozenset(CARACTERISTICAS - SIN_ENSAYAR) | (CARACTERISTICAS_DE_LA_VARIANTE - SIN_ENSAYAR_DE_LA_VARIANTE)
# Qué está pendiente de cada característica sin ensayar (receta de `construir`, 2.2): se dice en el mensaje, nunca se supone. Hoy solo la precisión 0.
PENDIENTE_DE_VERIFICAR = {
    "decimal:precision0": "Learn documenta la precisión de 1 a 10: el 0 en una columna decimal común está pendiente de verificar "
                          "(si el diseño pide cero decimales, la columna es un `entero`)",
}
FORMULA_RESULTADOS = ("decimal", "entero")  # los dos tipos de resultado creados y leídos en el entorno (spike 2, 2026-10-08)
FORMULA_LARGO_MAXIMO = 1000
FORMULA_PROFUNDIDAD_MAXIMA = 10
FORMULA_PRECISION_MAXIMA = 10
INFIJO_DESCARTABLE = "_tbl_zz"

# Cast del Web API y propiedades propias de cada familia de columna.
FAMILIAS = {
    "String": ("StringType", "LogicalName,MaxLength,FormatName,AutoNumberFormat", ""),
    "Memo": ("MemoType", "LogicalName,MaxLength", ""),
    "Integer": ("IntegerType", "LogicalName,MinValue,MaxValue", ""),
    "Decimal": ("DecimalType", "LogicalName,Precision,MinValue,MaxValue", ""),
    "Picklist": ("PicklistType", "LogicalName", "&$expand=GlobalOptionSet($select=Name,IsGlobal)"),
    "Boolean": ("BooleanType", "LogicalName,DefaultValue", "&$expand=OptionSet"),
    "DateTime": ("DateTimeType", "LogicalName,Format,DateTimeBehavior", ""),
    "File": ("FileType", "LogicalName,MaxSizeInKB", ""),
}
FAMILIA_DE = {"texto": "String", "autonumerico": "String", "memo": "Memo", "entero": "Integer", "decimal": "Decimal", "choice": "Picklist",
              "sino": "Boolean", "fecha": "DateTime", "fechahora": "DateTime", "archivo": "File"}
# Columnas propias que NO declara el playbook de la tabla y son legítimas.
TIPOS_AJENOS = {"LookupType", "OwnerType", "CustomerType", "UniqueidentifierType", "VirtualType", "EntityNameType", "PartyListType"}


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
        valor, donde = datos[clave], f"{de_donde}.{clave}" if de_donde != "la tabla" else f"'{clave}'"
        if tipo is bool:
            if not isinstance(valor, bool):
                raise ErrorPlaybook(f"{donde} tiene que ser true o false, no {valor!r}")
        elif tipo is int:
            if not isinstance(valor, int) or isinstance(valor, bool):
                raise ErrorPlaybook(f"{donde} tiene que ser un entero, no {valor!r}")
        elif tipo is str:
            if not isinstance(valor, str) or not valor.strip():
                raise ErrorPlaybook(f"{donde} tiene que ser texto no vacío, no {valor!r}")
        elif tipo == "numero":
            if isinstance(valor, bool) or not isinstance(valor, (int, decimal.Decimal)):
                raise ErrorPlaybook(f"{donde} tiene que ser un número, no {valor!r}")
        elif tipo == "lista_o_nulo":
            if valor is not None and not isinstance(valor, list):
                raise ErrorPlaybook(f"{donde} tiene que ser null (conserva los dos motivos por defecto) o una lista, no {valor!r}")
        elif tipo == "texto_o_vacio":
            if not isinstance(valor, str):
                raise ErrorPlaybook(f"{donde} tiene que ser texto (vacío si no aplica), no {valor!r}")
        elif not isinstance(valor, tipo):
            raise ErrorPlaybook(f"{donde} tiene que ser {'un objeto' if tipo is dict else 'una lista'}, no {valor!r}")


def _claves_de_columna(c):
    """Claves exactas de una columna: las comunes más las de su tipo. La fórmula decimal suma `precision`; la entera no la lleva."""
    claves = {**CLAVES_COLUMNA, **CLAVES_POR_TIPO[c["tipo"]]}
    if c["tipo"] == "formula" and c.get("resultado") == "decimal":
        claves["precision"] = int
    return claves


def _validar_nombre(nombre, patron, donde):
    if not re.fullmatch(patron, nombre):
        raise ErrorPlaybook(f"{donde} = {nombre!r} no cumple el patrón {patron} (todo en minúscula, BP-PP-184)")
    if len(nombre) > LARGO_MAXIMO_IDENTIFICADOR:
        raise ErrorPlaybook(f"{donde} tiene {len(nombre)} caracteres; el máximo de la plataforma es {LARGO_MAXIMO_IDENTIFICADOR}")


DECIMAL_LIMITE = decimal.Decimal(100_000_000_000)  # rango documentado de DecimalAttributeMetadata: ±100.000.000.000
DECIMAL_PRECISION_MAXIMA = 10
DECIMAL_DIGITOS_EXACTOS = 15  # Learn: las apps manejan con exactitud hasta 15 dígitos


def _validar_decimal(c, d):
    """Columna `decimal` (skill 0.2.0): las tres claves vienen del diccionario y se escriben siempre (un `MinValue` sin
    asignar toma 0 en el SDK: un mínimo omitido sería una decisión de la plataforma). Sin formato: el tipo no lo tiene."""
    if not 0 <= c["precision"] <= DECIMAL_PRECISION_MAXIMA:
        raise ErrorPlaybook(f"{d}.precision tiene que estar entre 0 y {DECIMAL_PRECISION_MAXIMA}, no {c['precision']} (Learn documenta 1 a 10)")
    for k in ("minimo", "maximo"):
        if abs(c[k]) > DECIMAL_LIMITE:
            raise ErrorPlaybook(f"{d}.{k} = {c[k]} está fuera del rango de un decimal de Dataverse (±{DECIMAL_LIMITE})")
    if c["minimo"] > c["maximo"]:
        raise ErrorPlaybook(f"{d}: minimo ({c['minimo']}) es mayor que maximo ({c['maximo']})")
    enteros = max(len(str(int(abs(c[k])))) for k in ("minimo", "maximo"))
    if enteros + c["precision"] > DECIMAL_DIGITOS_EXACTOS:
        raise ErrorPlaybook(f"{d}: {enteros} dígitos enteros más {c['precision']} decimales superan los {DECIMAL_DIGITOS_EXACTOS} que las apps "
                            "manejan con exactitud (Learn); el número se procesaría con error aunque Dataverse lo guarde bien")


def _usa(expresion, funcion):
    return re.search(rf"\b{funcion}\s*\(", expresion, re.IGNORECASE) is not None


def _validar_formula(c, d, columnas, posicion):
    """Columna `formula` (skill 0.2.0), sin red. Una fórmula es de solo lectura: nada que exigir, proteger ni auditar. La herramienta no
    interpreta la expresión; solo hace cumplir lo que la skill fija: largo, `UTCNow()` fuera, `Now()` solo sobre fecha y hora de usuario
    local, y que las columnas de las que depende estén declaradas ANTES (o ya existan en la tabla)."""
    for k in ("requerida", "protegida", "auditoria"):
        if c[k]:
            raise ErrorPlaybook(f"{d}.{k} tiene que ser false en una fórmula (es de solo lectura; Learn: no se puede proteger y no se almacena)")
    if c["resultado"] == "decimal" and not 0 <= c["precision"] <= FORMULA_PRECISION_MAXIMA:
        raise ErrorPlaybook(f"{d}.precision tiene que estar entre 0 y {FORMULA_PRECISION_MAXIMA}, no {c['precision']}")
    expresion, nombre = c["expresion"], c["nombre"]
    if len(expresion) > FORMULA_LARGO_MAXIMO:
        raise ErrorPlaybook(f"{d}.expresion tiene {len(expresion)} caracteres; el máximo de Learn es 1.000")
    if _usa(expresion, "UTCNow"):
        raise ErrorPlaybook(f"{d}.expresion usa UTCNow(): no se admite (devuelve fecha y hora independiente de zona y el formato solo tiene "
                            "fecha y hora de usuario local); se usa Now() sobre columnas fechahora")
    if re.search(rf"\b{re.escape(nombre)}\b", expresion):
        raise ErrorPlaybook(f"{d}.expresion se referencia a sí misma ({nombre})")
    bases = c["depende_de"]
    if not bases:
        raise ErrorPlaybook(f"{d}.depende_de no puede estar vacía: lista las columnas del mismo registro que usa la expresión")
    declaradas = {col["nombre"]: j for j, col in enumerate(columnas)}
    for k, base in enumerate(bases):
        donde = f"{d}.depende_de[{k}]"
        if not isinstance(base, str) or not re.fullmatch(r"[a-z][a-z0-9_]*", base):
            raise ErrorPlaybook(f"{donde} = {base!r} tiene que ser el nombre lógico de una columna (minúscula, BP-PP-184)")
        if base == nombre:
            raise ErrorPlaybook(f"{donde}: una fórmula no puede depender de sí misma ({nombre})")
        if bases.count(base) > 1:
            raise ErrorPlaybook(f"{donde} = {base!r} está repetido en depende_de")
        if base in declaradas and declaradas[base] > posicion:
            raise ErrorPlaybook(f"{donde} = {base!r} está declarada después de la fórmula: una fórmula con una base ausente queda inválida; "
                                "las bases se declaran antes")
        if base in declaradas and _usa(expresion, "Now") and columnas[declaradas[base]]["tipo"] == "fecha":
            raise ErrorPlaybook(f"{d}: Now() solo se admite con columnas fechahora (usuario local) y {base} es una fecha sola; "
                                "DateDiff no mezcla comportamientos (Learn; en el spike, HTTP 405)")


def _validar_profundidad_de_formulas(columnas):
    """Learn: hasta 10 niveles de fórmulas que dependen de fórmulas. No hay ciclos posibles: cada base se declara antes."""
    profundidad = {}
    for c in columnas:
        if c["tipo"] != "formula":
            continue
        nivel = 1 + max([profundidad[b] for b in c["depende_de"] if b in profundidad], default=0)
        profundidad[c["nombre"]] = nivel
        if nivel > FORMULA_PROFUNDIDAD_MAXIMA:
            raise ErrorPlaybook(f"{c['nombre']}: la cadena de fórmulas tiene profundidad {nivel}; el máximo de Learn es {FORMULA_PROFUNDIDAD_MAXIMA}")


def _validar_autonumerico(formato, donde):
    if not MARCADOR_AUTONUMERICO.search(formato):
        raise ErrorPlaybook(f"{donde} = {formato!r} no tiene ningún marcador ({{SEQNUM:n}}, {{RANDSTRING:n}} o {{DATETIMEUTC:formato}})")


def _validar_motivos(motivos, identidad):
    """`motivos_estado` (skill 0.2.0): una lista de motivos de `statuscode`. Los dos por defecto (valores 1 y 2) se RENOMBRAN y siempre
    están; los demás se agregan con el valor completo (prefijo de opciones x 10.000 + correlativo, BP-PP-193). No se crean estados."""
    if not motivos:
        raise ErrorPlaybook("'motivos_estado' no puede ser una lista vacía: o es null (conserva los dos motivos por defecto) o los trae todos")
    idiomas, prefijo = identidad["idiomas"], identidad["prefijo_opciones"]
    for i, m in enumerate(motivos):
        d = f"motivos_estado[{i}]"
        _validar_bloque(m, CLAVES_MOTIVO, d)
        if m["statecode"] not in ESTADOS_DE_TABLA:
            raise ErrorPlaybook(f"{d}.statecode tiene que ser 0 o 1 (los estados de una tabla son fijos y no se crean), no {m['statecode']}")
        if m["origen"] not in ("defecto", "agregado"):
            raise ErrorPlaybook(f"{d}.origen tiene que ser 'defecto' (se renombra el motivo que ya existe) o 'agregado' (se crea), no {m['origen']!r}")
        if m["origen"] == "defecto" and m["valor"] != MOTIVO_POR_DEFECTO[m["statecode"]]:
            raise ErrorPlaybook(f"{d}.valor = {m['valor']}: el motivo por defecto del statecode {m['statecode']} tiene el valor {MOTIVO_POR_DEFECTO[m['statecode']]}")
        if m["origen"] == "agregado":
            if m["valor"] // 10000 != prefijo:
                raise ErrorPlaybook(f"{d}.valor = {m['valor']}: un motivo agregado lleva el entero completo, prefijo de opciones {prefijo} x 10.000 + "
                                    f"correlativo (por ejemplo {prefijo * 10000 + 1}), no el correlativo suelto ni el de otro publisher")
            if m["valor"] % 10000 == 0:
                raise ErrorPlaybook(f"{d}.valor = {m['valor']}: el correlativo 0 no es válido; empieza en 1")
        validar_textos_por_idioma(m["etiqueta"], idiomas, f"{d}.etiqueta")
    valores = [m["valor"] for m in motivos]
    if len(valores) != len(set(valores)):
        raise ErrorPlaybook(f"motivos_estado tiene valores repetidos: {sorted(v for v in set(valores) if valores.count(v) > 1)}")
    por_defecto = sorted((m["statecode"], m["valor"]) for m in motivos if m["origen"] == "defecto")
    if por_defecto != sorted(MOTIVO_POR_DEFECTO.items()):
        raise ErrorPlaybook("motivos_estado tiene que traer los dos motivos por defecto, una vez cada uno (statecode 0 con valor 1 y statecode 1 con valor 2), "
                            f"aunque conserven el nombre; trae {por_defecto}")
    for lcid in idiomas:
        etiquetas = [m["etiqueta"][str(lcid)] for m in motivos]
        repetidas = sorted({e for e in etiquetas if etiquetas.count(e) > 1})
        if repetidas:
            raise ErrorPlaybook(f"motivos_estado tiene etiquetas repetidas en el idioma {lcid}: {repetidas}")


def _validar_estructura(datos, identidad):
    claves = dict(CLAVES_TABLA)
    if "motivos_estado" in datos:
        claves["motivos_estado"] = "lista_o_nulo"
    _validar_bloque(datos, claves, "la tabla")
    if datos.get("motivos_estado") is not None:
        _validar_motivos(datos["motivos_estado"], identidad)
    prefijo, abrev = identidad["prefijo"], identidad["abrev"]
    _validar_nombre(datos["nombre"], rf"{prefijo}_{abrev}_tbl_[a-z0-9]+", "'nombre'")
    if datos["propiedad"] not in PROPIEDAD:
        raise ErrorPlaybook(f"'propiedad' tiene que ser una de {sorted(PROPIEDAD)}, no {datos['propiedad']!r}")

    p = datos["primaria"]
    _validar_bloque(p, CLAVES_PRIMARIA, "primaria")
    _validar_nombre(p["nombre"], rf"{prefijo}_[a-z0-9]+", "primaria.nombre")
    if not 1 <= p["largo"] <= 4000:
        raise ErrorPlaybook(f"primaria.largo tiene que estar entre 1 y 4000, no {p['largo']}")
    if p["autonumerico"]:
        _validar_autonumerico(p["autonumerico"], "primaria.autonumerico")

    nombres = {p["nombre"]}
    for i, c in enumerate(datos["columnas"]):
        _validar_columna(c, f"columnas[{i}]", i, datos["columnas"], nombres, identidad)
    _validar_profundidad_de_formulas(datos["columnas"])


def _validar_columna(c, d, i, columnas, nombres, identidad):
    """Una columna del playbook, sin red: claves y tipos exactos, nombre, y las reglas propias de su tipo. `nombres` acumula los ya usados.
    La comparten la tabla propia y la variante de columnas sobre una tabla estándar."""
    prefijo, abrev = identidad["prefijo"], identidad["abrev"]
    if not isinstance(c, dict):
        raise ErrorPlaybook(f"{d} tiene que ser un objeto, no {c!r}")
    tipo = c.get("tipo")
    if tipo not in CLAVES_POR_TIPO:
        raise ErrorPlaybook(f"{d}.tipo tiene que ser uno de {sorted(CLAVES_POR_TIPO)}, no {tipo!r} (un lookup nace con su relación, no acá)")
    if tipo == "formula" and c.get("resultado") not in FORMULA_RESULTADOS:
        # Antes de las claves: sin el resultado no se sabe si lleva `precision`. Learn lista más tipos de resultado, pero
        # solo decimal y entero se crearon y leyeron en el entorno; el resto no entra al formato hasta ensayarse.
        raise ErrorPlaybook(f"{d}.resultado tiene que ser uno de {list(FORMULA_RESULTADOS)}, no {c.get('resultado')!r} "
                            "(moneda no se puede producir en una fórmula; los demás tipos de resultado no se ensayaron)")
    _validar_bloque(c, _claves_de_columna(c), d)
    _validar_nombre(c["nombre"], rf"{prefijo}_[a-z0-9]+", f"{d}.nombre")
    if c["nombre"] in nombres:
        raise ErrorPlaybook(f"{d}.nombre = {c['nombre']!r} está repetido en la tabla")
    nombres.add(c["nombre"])
    if tipo in ("texto", "autonumerico") and not 1 <= c["largo"] <= 4000:
        raise ErrorPlaybook(f"{d}.largo tiene que estar entre 1 y 4000, no {c['largo']}")
    if tipo == "memo" and not 1 <= c["largo"] <= 1048576:
        raise ErrorPlaybook(f"{d}.largo tiene que estar entre 1 y 1048576, no {c['largo']}")
    if tipo == "entero":
        for k in ("minimo", "maximo"):
            if not ENTERO_MIN <= c[k] <= ENTERO_MAX:
                raise ErrorPlaybook(f"{d}.{k} = {c[k]} está fuera del rango de un entero de Dataverse")
        if c["minimo"] > c["maximo"]:
            raise ErrorPlaybook(f"{d}: minimo ({c['minimo']}) es mayor que maximo ({c['maximo']})")
    if tipo == "decimal":
        _validar_decimal(c, d)
    if tipo == "choice":
        _validar_nombre(c["choice"], rf"{prefijo}_{abrev}_ch_[a-z0-9]+", f"{d}.choice")
    if tipo == "archivo":
        if not 1 <= c["tamano_kb"] <= 10485760:
            raise ErrorPlaybook(f"{d}.tamano_kb tiene que estar entre 1 y 10485760, no {c['tamano_kb']}")
        if c["requerida"]:
            raise ErrorPlaybook(f"{d}: una columna de archivo no puede ser requerida (el archivo se carga después de crear el registro)")
    if tipo == "autonumerico":
        _validar_autonumerico(c["formato"], f"{d}.formato")
    if tipo == "formula":
        _validar_formula(c, d, columnas, i)


def caracteristicas_de_columnas(columnas):
    """Lo que usan las columnas de un playbook (de una tabla propia o de la variante sobre una estándar)."""
    usadas = {f"tipo:{c['tipo']}" for c in columnas}
    if any(c["auditoria"] for c in columnas):
        usadas.add("columna:auditoria")
    if any(c["protegida"] for c in columnas):
        usadas.add("columna:protegida")
    if any(c["tipo"] == "decimal" and c["precision"] == 0 for c in columnas):
        usadas.add("decimal:precision0")
    return usadas


def caracteristicas_usadas(datos):
    usadas = caracteristicas_de_columnas(datos["columnas"])
    if datos["auditoria"]:
        usadas.add("tabla:auditoria")
    if datos["primaria"]["autonumerico"]:
        usadas.add("primaria:autonumerica")
    if datos.get("motivos_estado") is not None:
        usadas.add("motivos_estado")
    return usadas


def comprobar_verificadas(datos, verificadas, permitir_no_verificadas):
    faltan = sorted(caracteristicas_usadas(datos) - set(verificadas))
    if not faltan:
        return
    if not permitir_no_verificadas:
        pendiente = "".join(f" [{c}: {PENDIENTE_DE_VERIFICAR[c]}]" for c in faltan if c in PENDIENTE_DE_VERIFICAR)
        raise Bloqueado(
            f"el playbook usa características todavía no verificadas contra la plataforma: {faltan}.{pendiente} "
            f"Primero se ensayan sobre una tabla descartable ('…{INFIJO_DESCARTABLE}…') con --permitir-no-verificadas"
        )
    if INFIJO_DESCARTABLE not in datos["nombre"]:
        raise Bloqueado(
            f"--permitir-no-verificadas solo vale sobre una tabla descartable, con '{INFIJO_DESCARTABLE}' en el nombre; "
            f"{datos['nombre']!r} no lo es. Sin verificar: {faltan}"
        )


def validar_playbook(datos, identidad):
    """Todo nombre visible es un objeto con un texto por CADA idioma de la Identidad, sin tildes ni ñ (BP-PP-197)."""
    _validar_estructura(datos, identidad)
    idiomas = identidad["idiomas"]
    validar_textos_por_idioma(datos["displayname"], idiomas, "'displayname'")
    validar_textos_por_idioma(datos["displayname_plural"], idiomas, "'displayname_plural'")
    validar_textos_por_idioma(datos["primaria"]["displayname"], idiomas, "primaria.displayname")
    for i, c in enumerate(datos["columnas"]):
        validar_textos_por_idioma(c["displayname"], idiomas, f"columnas[{i}].displayname ({c['nombre']})")
        for etiqueta in ("etiqueta_si", "etiqueta_no"):
            if etiqueta in c:
                validar_textos_por_idioma(c[etiqueta], idiomas, f"columnas[{i}].{etiqueta} ({c['nombre']})")


# ---------------------------------------------------------------------------
# Cuerpo del POST
# ---------------------------------------------------------------------------
def _nivel(requerida):
    return {"Value": "ApplicationRequired" if requerida else "None", "CanBeChanged": True,
            "ManagedPropertyLogicalName": "canmodifyrequirementlevelsettings"}


def _auditoria(activa):
    return {"Value": activa, "CanBeChanged": True, "ManagedPropertyLogicalName": "canmodifyauditsettings"}


def _columna_payload(c, idiomas, choices):
    tipo = c["tipo"]
    if tipo == "formula":
        # Microsoft no soporta definir fórmulas por código (la API la acepta, pero no es soportado): es un paso manual.
        raise RuntimeError(f"error de programación: la fórmula {c['nombre']} no se crea por código")
    a = {
        "SchemaName": c["nombre"],
        "DisplayName": etiqueta_web_api(c["displayname"], idiomas),
        "Description": etiqueta_uniforme(c["descripcion"], idiomas),
        "RequiredLevel": _nivel(c["requerida"]),
        "IsSecured": c["protegida"],
        "IsAuditEnabled": _auditoria(c["auditoria"]),
    }
    if tipo in ("texto", "autonumerico"):
        a.update({"@odata.type": "Microsoft.Dynamics.CRM.StringAttributeMetadata", "AttributeType": "String",
                  "AttributeTypeName": {"Value": "StringType"}, "FormatName": {"Value": "Text"}, "MaxLength": c["largo"]})
        if tipo == "autonumerico":
            a["AutoNumberFormat"] = c["formato"]
    elif tipo == "memo":
        a.update({"@odata.type": "Microsoft.Dynamics.CRM.MemoAttributeMetadata", "AttributeType": "Memo",
                  "AttributeTypeName": {"Value": "MemoType"}, "Format": "TextArea", "MaxLength": c["largo"]})
    elif tipo == "entero":
        a.update({"@odata.type": "Microsoft.Dynamics.CRM.IntegerAttributeMetadata", "AttributeType": "Integer",
                  "AttributeTypeName": {"Value": "IntegerType"}, "Format": "None", "MinValue": c["minimo"], "MaxValue": c["maximo"]})
    elif tipo == "decimal":
        # Cuerpo del ejemplo de Learn («Create a decimal column»). Los límites viajan como número JSON: `float` de un Decimal de
        # hasta 15 dígitos (regla de _validar_decimal) vuelve a escribirse con los mismos dígitos.
        a.update({"@odata.type": "Microsoft.Dynamics.CRM.DecimalAttributeMetadata", "AttributeType": "Decimal",
                  "AttributeTypeName": {"Value": "DecimalType"}, "Precision": c["precision"],
                  "MinValue": float(c["minimo"]), "MaxValue": float(c["maximo"])})
    elif tipo == "choice":
        a.update({"@odata.type": "Microsoft.Dynamics.CRM.PicklistAttributeMetadata", "AttributeType": "Picklist",
                  "AttributeTypeName": {"Value": "PicklistType"}, "SourceTypeMask": 0,
                  "GlobalOptionSet@odata.bind": f"/GlobalOptionSetDefinitions({choices[c['choice']]})"})
    elif tipo == "sino":
        a.update({"@odata.type": "Microsoft.Dynamics.CRM.BooleanAttributeMetadata", "AttributeType": "Boolean",
                  "AttributeTypeName": {"Value": "BooleanType"}, "DefaultValue": c["defecto"],
                  "OptionSet": {"OptionSetType": "Boolean",
                                "TrueOption": {"Value": 1, "Label": etiqueta_web_api(c["etiqueta_si"], idiomas)},
                                "FalseOption": {"Value": 0, "Label": etiqueta_web_api(c["etiqueta_no"], idiomas)}}})
    elif tipo in ("fecha", "fechahora"):
        solo_fecha = tipo == "fecha"
        a.update({"@odata.type": "Microsoft.Dynamics.CRM.DateTimeAttributeMetadata", "AttributeType": "DateTime",
                  "AttributeTypeName": {"Value": "DateTimeType"}, "Format": "DateOnly" if solo_fecha else "DateAndTime",
                  "DateTimeBehavior": {"Value": "DateOnly" if solo_fecha else "UserLocal"}})
    elif tipo == "archivo":
        a.update({"@odata.type": "Microsoft.Dynamics.CRM.FileAttributeMetadata", "AttributeTypeName": {"Value": "FileType"},
                  "MaxSizeInKB": c["tamano_kb"]})
    return a


def construir_payload(datos, identidad, choices):
    """`choices`: {nombre del choice global: su MetadataId}, ya comprobados."""
    idiomas, p = identidad["idiomas"], datos["primaria"]
    primaria = _columna_payload({**p, "tipo": "autonumerico" if p["autonumerico"] else "texto", "formato": p["autonumerico"],
                                 "protegida": False, "auditoria": datos["auditoria"]}, idiomas, choices)
    primaria["IsPrimaryName"] = True
    return {
        "@odata.type": "Microsoft.Dynamics.CRM.EntityMetadata",
        "SchemaName": datos["nombre"],
        "DisplayName": etiqueta_web_api(datos["displayname"], idiomas),
        "DisplayCollectionName": etiqueta_web_api(datos["displayname_plural"], idiomas),
        "Description": etiqueta_uniforme(datos["descripcion"], idiomas),
        "OwnershipType": PROPIEDAD[datos["propiedad"]],
        "HasNotes": datos["notas"],
        "HasActivities": datos["actividades"],
        "IsActivity": False,
        "IsAuditEnabled": _auditoria(datos["auditoria"]),
        # Las protegidas no pueden ir acá: ver `columnas_protegidas_payload`.
        "Attributes": [primaria] + [_columna_payload(c, idiomas, choices) for c in datos["columnas"] if not c["protegida"] and c["tipo"] != "formula"],
    }


def columnas_protegidas_payload(datos, identidad, choices):
    """Las columnas con seguridad de columna, para agregarlas con
    `POST EntityDefinitions(LogicalName='<tabla>')/Attributes` después de
    crear la tabla, en el orden del playbook."""
    return [_columna_payload(c, identidad["idiomas"], choices) for c in datos["columnas"] if c["protegida"]]


# ---------------------------------------------------------------------------
# Lectura del entorno y comparación: una sola función para los tres caminos
# ---------------------------------------------------------------------------
def _get(dv, ruta, consulta, admite_404=False):
    est, cuerpo, _ = dv.call("GET", ruta)
    if est == 404 and admite_404:
        return None
    if est != 200:
        raise ErrorEntorno(f"{consulta} devolvió HTTP {est} (se esperaba 200{' o 404' if admite_404 else ''}): {cuerpo}")
    return exigir_forma(cuerpo, dict, consulta, "cuerpo")


def _lista(dv, ruta, consulta):
    return exigir_forma(_get(dv, ruta, consulta).get("value"), [dict], consulta, "value")


def _administrada(objeto, tipo, consulta, campo):
    """Propiedad administrada del Web API: `{Value, CanBeChanged, …}`."""
    exigir_forma(objeto, dict, consulta, campo)
    return exigir_forma(objeto.get("Value"), tipo, consulta, f"{campo}.Value")


def comprobar_choices(dv, datos):
    """Precondición: cada choice global que usa una columna existe. Devuelve
    {nombre: MetadataId} para enlazarlos al crear."""
    encontrados = {}
    for nombre in sorted({c["choice"] for c in datos["columnas"] if c["tipo"] == "choice"}):
        consulta = f"la consulta del choice global '{nombre}' (GET GlobalOptionSetDefinitions)"
        cuerpo = _get(dv, f"GlobalOptionSetDefinitions(Name='{nombre}')?$select=Name,IsGlobal", consulta, admite_404=True)
        if cuerpo is None:
            raise Bloqueado(f"el choice global '{nombre}' no existe en el entorno: se construye antes que la tabla que lo usa")
        encontrados[nombre] = exigir_forma(cuerpo.get("MetadataId"), str, consulta, "MetadataId", no_vacio=True)
    return encontrados


def _verificar_columnas(dv, tabla, declaradas, idiomas, difs, solo_prefijo=None):
    """Las columnas declaradas contra el entorno, en los tres pasos de siempre: lo común a todas (una consulta), lo propio de cada familia (un cast
    por familia presente) y la fórmula (lectura aparte). La comparten la tabla propia y la variante sobre una tabla estándar. `solo_prefijo`:
    en una tabla estándar solo cuentan como «columna propia de más» las que llevan el prefijo del publisher (trae cientos de columnas del sistema).
    Devuelve `(en_entorno, pendientes, notas)`."""
    # Columnas: lo común a todas, en una consulta…
    ca = f"la consulta de las columnas (GET EntityDefinitions '{tabla}' /Attributes)"
    genericas = _lista(dv, f"EntityDefinitions(LogicalName='{tabla}')/Attributes?$select=LogicalName,SchemaName,AttributeOf,AttributeTypeName,"
                           "IsCustomAttribute,IsPrimaryName,RequiredLevel,IsSecured,IsAuditEnabled,DisplayName,Description", ca)
    en_entorno = {}
    for i, g in enumerate(genericas):
        nombre = exigir_forma(g.get("LogicalName"), str, ca, f"value[{i}].LogicalName", no_vacio=True)
        g["_tipo"] = _administrada(g.get("AttributeTypeName"), str, ca, f"value[{i}].AttributeTypeName")
        g["_propia"] = exigir_forma(g.get("IsCustomAttribute"), bool, ca, f"value[{i}].IsCustomAttribute")
        exigir_forma(g.get("AttributeOf"), str, ca, f"value[{i}].AttributeOf", permite_nulo=True)
        g["_i"] = i
        en_entorno[nombre] = g

    presentes, pendientes, notas = [], [], []
    for c in declaradas:
        n, g = c["nombre"], en_entorno.get(c["nombre"])
        if g is None:
            if c["tipo"] == "formula":
                # La herramienta no crea una fórmula: si falta, es un paso manual pendiente, no una diferencia.
                pendientes.append(c)
            else:
                difs.append(f"falta la columna {n}")
            continue
        i = g["_i"]
        tipo_esperado = _tipo_de_plataforma(c)
        if g["_tipo"] != tipo_esperado:
            difs.append(f"{n}.tipo: entorno={g['_tipo']!r} playbook={tipo_esperado!r}")
            continue
        presentes.append(c)
        nivel = _administrada(g.get("RequiredLevel"), str, ca, f"value[{i}].RequiredLevel")
        if nivel != ("ApplicationRequired" if c["requerida"] else "None"):
            difs.append(f"{n}.RequiredLevel: entorno={nivel!r} playbook={'ApplicationRequired' if c['requerida'] else 'None'!r}")
        if exigir_forma(g.get("IsSecured"), bool, ca, f"value[{i}].IsSecured") != c["protegida"]:
            difs.append(f"{n}.IsSecured: entorno={g['IsSecured']!r} playbook={c['protegida']!r}")
        if _administrada(g.get("IsAuditEnabled"), bool, ca, f"value[{i}].IsAuditEnabled") != c["auditoria"]:
            difs.append(f"{n}.IsAuditEnabled: entorno={g['IsAuditEnabled']['Value']!r} playbook={c['auditoria']!r}")
        if exigir_forma(g.get("IsPrimaryName"), bool, ca, f"value[{i}].IsPrimaryName") != c["_primaria"]:
            difs.append(f"{n}.IsPrimaryName: entorno={g['IsPrimaryName']!r} playbook={c['_primaria']!r}")
        comparar_etiquetas(g.get("DisplayName"), idiomas, f"{n}.DisplayName", c["displayname"], difs, ca, f"value[{i}].DisplayName")
        comparar_descripcion(g.get("Description"), idiomas, f"{n}.Description", c["descripcion"], difs, ca, f"value[{i}].Description")

    declaradas_nombres = {c["nombre"] for c in declaradas}
    for nombre, g in sorted(en_entorno.items()):
        if (g["_propia"] and g.get("AttributeOf") is None and g["_tipo"] not in TIPOS_AJENOS and nombre not in declaradas_nombres
                and (solo_prefijo is None or nombre.startswith(f"{solo_prefijo}_"))):
            difs.append(f"columna propia que el playbook no declara: {nombre} ({g['_tipo']})")

    # …y lo propio de cada familia, con un cast por familia presente. La fórmula no tiene familia propia (se lee aparte), pero si usa
    # Now() hace falta el comportamiento de las fechas de las que depende: se lee la familia DateTime aunque la tabla no declare otra.
    formulas = [c for c in presentes if c["tipo"] == "formula"]
    familias = {FAMILIA_DE[c["tipo"]] for c in presentes if c["tipo"] != "formula"}
    if any(_usa(c["expresion"], "Now") for c in formulas):
        familias.add("DateTime")
    por_familia = {}
    for familia in sorted(familias):
        _, select, expand = FAMILIAS[familia]
        cf = f"la consulta de columnas {familia}AttributeMetadata de '{tabla}'"
        filas = _lista(dv, f"EntityDefinitions(LogicalName='{tabla}')/Attributes/Microsoft.Dynamics.CRM.{familia}AttributeMetadata?$select={select}{expand}", cf)
        por_nombre = {}
        for i, f in enumerate(filas):
            por_nombre[exigir_forma(f.get("LogicalName"), str, cf, f"value[{i}].LogicalName", no_vacio=True)] = (i, f)
        por_familia[familia] = (cf, por_nombre)
        for c in presentes:
            if c["tipo"] == "formula" or FAMILIA_DE[c["tipo"]] != familia:
                continue
            n = c["nombre"]
            if n not in por_nombre:
                raise ErrorEntorno(f"{cf} devolvió 200 con forma inesperada: '{n}' no vino entre las columnas de esa familia")
            i, f = por_nombre[n]
            _comparar_familia(c, f, i, cf, idiomas, difs, tabla)

    for c in formulas:
        _verificar_formula(dv, tabla, c, idiomas, en_entorno, por_familia, difs, notas)

    return en_entorno, pendientes, notas


def _verificar(dv, datos, identidad, solution_id):
    idiomas, tabla, difs = identidad["idiomas"], datos["nombre"], []
    ct = f"la consulta de la tabla (GET EntityDefinitions '{tabla}')"
    t = _get(dv, f"EntityDefinitions(LogicalName='{tabla}')?$select=LogicalName,SchemaName,OwnershipType,HasNotes,HasActivities,"
                 "IsAuditEnabled,IsManaged,IsCustomEntity,PrimaryNameAttribute,DisplayName,DisplayCollectionName,Description", ct, admite_404=True)
    if t is None:
        return {"existe": False, "diffs": None, "metadata_id": None, "pendientes": [], "notas": []}

    metadata_id = exigir_forma(t.get("MetadataId"), str, ct, "MetadataId", no_vacio=True)
    esperado = [("SchemaName", str, tabla), ("OwnershipType", str, PROPIEDAD[datos["propiedad"]]), ("HasNotes", bool, datos["notas"]),
                ("HasActivities", bool, datos["actividades"]), ("IsManaged", bool, False), ("IsCustomEntity", bool, True),
                ("PrimaryNameAttribute", str, datos["primaria"]["nombre"])]
    for campo, tipo, valor in esperado:
        real = exigir_forma(t.get(campo), tipo, ct, campo)
        if real != valor:
            difs.append(f"{campo}: entorno={real!r} playbook={valor!r}")
    if _administrada(t.get("IsAuditEnabled"), bool, ct, "IsAuditEnabled") != datos["auditoria"]:
        difs.append(f"IsAuditEnabled: entorno={t['IsAuditEnabled']['Value']!r} playbook={datos['auditoria']!r}")
    comparar_etiquetas(t.get("DisplayName"), idiomas, "DisplayName", datos["displayname"], difs, ct, "DisplayName")
    comparar_etiquetas(t.get("DisplayCollectionName"), idiomas, "DisplayCollectionName", datos["displayname_plural"], difs, ct, "DisplayCollectionName")
    comparar_descripcion(t.get("Description"), idiomas, "Description", datos["descripcion"], difs, ct, "Description")

    p = datos["primaria"]
    declaradas = [{**p, "tipo": "autonumerico" if p["autonumerico"] else "texto", "formato": p["autonumerico"], "protegida": False,
                   "auditoria": datos["auditoria"], "_primaria": True}] + [dict(c, _primaria=False) for c in datos["columnas"]]
    _, pendientes, notas = _verificar_columnas(dv, tabla, declaradas, idiomas, difs)

    _verificar_motivos(dv, datos, idiomas, difs)

    cs = "la consulta de pertenencia a la solución (GET solutioncomponents)"
    filas_sc = _lista(dv, f"solutioncomponents?$filter=_solutionid_value eq {solution_id} and objectid eq {metadata_id} and componenttype eq {COMPONENTE_TABLA}", cs)
    if len(filas_sc) != 1:
        difs.append(f"pertenencia a la solución: {len(filas_sc)} filas en solutioncomponents, se esperaba 1")
    return {"existe": True, "diffs": difs, "metadata_id": metadata_id, "pendientes": pendientes, "notas": notas}


def _opciones_de(cuerpo, consulta, con_estado):
    """Las opciones de un `OptionSet` expandido: `{valor: (estado o None, Label)}`, con la forma de CADA nivel validada. Esta lectura
    (`$expand=OptionSet($select=Options)` sobre StatusAttributeMetadata / StateAttributeMetadata) devuelve las opciones con `State` (ensayo del 2026-10-09)."""
    os_ = exigir_forma(cuerpo.get("OptionSet"), dict, consulta, "OptionSet")
    opciones = exigir_forma(os_.get("Options"), [dict], consulta, "OptionSet.Options", no_vacio=True)
    resultado = {}
    for i, o in enumerate(opciones):
        valor = exigir_forma(o.get("Value"), int, consulta, f"OptionSet.Options[{i}].Value")
        estado = exigir_forma(o.get("State"), int, consulta, f"OptionSet.Options[{i}].State") if con_estado else None
        exigir_etiqueta(o.get("Label"), consulta, f"OptionSet.Options[{i}].Label")
        resultado[valor] = (estado, o.get("Label"))
    return resultado


def _verificar_motivos(dv, datos, idiomas, difs):
    """Punto 7 de la verificación de la skill: `statecode` conserva exactamente los estados 0 y 1; `statuscode` tiene exactamente los motivos
    del playbook (mismo valor, mismo estado, misma etiqueta en CADA idioma) o, con `motivos_estado` null, exactamente el 1 (estado 0) y el 2
    (estado 1). Se lee SIEMPRE, también cuando el playbook no declara motivos."""
    tabla, motivos = datos["nombre"], datos.get("motivos_estado")
    ruta = f"EntityDefinitions(LogicalName='{tabla}')/Attributes"
    cs = f"la lectura de statecode (GET EntityDefinitions '{tabla}' /Attributes 'statecode')"
    estados = _opciones_de(_get(dv, f"{ruta}(LogicalName='statecode')/Microsoft.Dynamics.CRM.StateAttributeMetadata?$select=LogicalName&$expand=OptionSet($select=Options)", cs),
                           cs, con_estado=False)
    if sorted(estados) != list(ESTADOS_DE_TABLA):
        difs.append(f"statecode: estados del entorno {sorted(estados)}, se esperaba exactamente {list(ESTADOS_DE_TABLA)}")
    cm = f"la lectura de statuscode (GET EntityDefinitions '{tabla}' /Attributes 'statuscode')"
    reales = _opciones_de(_get(dv, f"{ruta}(LogicalName='statuscode')/Microsoft.Dynamics.CRM.StatusAttributeMetadata?$select=LogicalName&$expand=OptionSet($select=Options)", cm),
                          cm, con_estado=True)
    esperados = ({m["valor"]: m for m in motivos} if motivos is not None
                 else {valor: {"statecode": estado, "etiqueta": None} for estado, valor in MOTIVO_POR_DEFECTO.items()})
    for valor in sorted(set(esperados) - set(reales)):
        difs.append(f"statuscode: falta el motivo {valor}")
    for valor in sorted(set(reales) - set(esperados)):
        difs.append(f"statuscode: el motivo {valor} no está en el playbook"
                    + ("" if motivos is not None else " (con motivos_estado null la tabla conserva solo los dos motivos por defecto)"))
    for valor in sorted(set(esperados) & set(reales)):
        estado, etiqueta = reales[valor]
        if estado != esperados[valor]["statecode"]:
            difs.append(f"statuscode.{valor}.State: entorno={estado!r} playbook={esperados[valor]['statecode']!r}")
        if motivos is not None:
            comparar_etiquetas(etiqueta, idiomas, f"statuscode.{valor}.Label", esperados[valor]["etiqueta"], difs, cm, f"OptionSet.Options[{valor}].Label")


def _tipo_de_plataforma(c):
    """`AttributeTypeName` que la plataforma devuelve para esta columna. Una fórmula es del tipo de su resultado (`SourceType` la distingue)."""
    if c["tipo"] == "formula":
        return "DecimalType" if c["resultado"] == "decimal" else "IntegerType"
    return FAMILIAS[FAMILIA_DE[c["tipo"]]][0]


SELECT_FORMULA = ("LogicalName,AttributeTypeName,SourceType,SourceTypeMask,FormulaDefinition,IsValidForCreate,IsValidForUpdate,"
                  "IsSecured,IsAuditEnabled,RequiredLevel,DisplayName,Description")
BIT_FORMULA_INVALIDA, BIT_FORMULA_MISMO_REGISTRO = 32, 1  # SourceTypeMask (receta de `construir`, 2.2)
SOURCE_TYPE_FORMULA = 3


def _verificar_formula(dv, tabla, c, idiomas, en_entorno, por_familia, difs, notas):
    """Punto 8 de la verificación de la skill, por lectura de metadata (no escribe nada). El nombre visible y la descripción ya los
    comparó la lectura genérica de columnas. La EXPRESIÓN del playbook se compara con `FormulaDefinition` normalizando los espacios: el editor de
    Power Apps la guarda EXACTAMENTE como se tipeó, sin `=` ni envoltorio (ensayo del 2026-10-09). `notas` ya no lleva nada de la fórmula."""
    n = c["nombre"]
    consulta = f"la lectura de la fórmula '{n}' (GET EntityDefinitions '{tabla}' /Attributes '{n}')"
    ruta = f"EntityDefinitions(LogicalName='{tabla}')/Attributes(LogicalName='{n}')"
    # Con el cast del tipo de su resultado: SourceTypeMask no existe en AttributeMetadata y sin el cast la plataforma responde 400 (Dev, 2026-10-09).
    f = _get(dv, f"{ruta}/Microsoft.Dynamics.CRM.{CAST_DE_RESULTADO[c['resultado']]}AttributeMetadata?$select={SELECT_FORMULA}", consulta)
    # Primero la forma de TODO lo que se va a usar; recién después se decide.
    tipo_fuente = exigir_forma(f.get("SourceType"), int, consulta, "SourceType")
    mascara = exigir_forma(f.get("SourceTypeMask"), int, consulta, "SourceTypeMask")
    definicion = exigir_forma(f.get("FormulaDefinition"), str, consulta, "FormulaDefinition", permite_nulo=True)
    valido_alta = exigir_forma(f.get("IsValidForCreate"), bool, consulta, "IsValidForCreate")
    valido_cambio = exigir_forma(f.get("IsValidForUpdate"), bool, consulta, "IsValidForUpdate")
    protegida = exigir_forma(f.get("IsSecured"), bool, consulta, "IsSecured")
    auditada = _administrada(f.get("IsAuditEnabled"), bool, consulta, "IsAuditEnabled")
    nivel = _administrada(f.get("RequiredLevel"), str, consulta, "RequiredLevel")
    precision = None
    if c["resultado"] == "decimal":
        cuerpo = _get(dv, f"{ruta}/Microsoft.Dynamics.CRM.DecimalAttributeMetadata?$select=LogicalName,Precision", f"la lectura de la precisión de la fórmula '{n}'")
        precision = exigir_forma(cuerpo.get("Precision"), int, f"la lectura de la precisión de la fórmula '{n}'", "Precision")

    if tipo_fuente != SOURCE_TYPE_FORMULA:
        difs.append(f"{n}.SourceType: entorno={tipo_fuente!r} playbook={SOURCE_TYPE_FORMULA} (columna de fórmula)")
    if mascara & BIT_FORMULA_INVALIDA:
        difs.append(f"{n}.SourceTypeMask: entorno={mascara} tiene el bit {BIT_FORMULA_INVALIDA} (la fórmula quedó inválida: una columna base falta o cambió)")
    if not mascara & BIT_FORMULA_MISMO_REGISTRO:
        difs.append(f"{n}.SourceTypeMask: entorno={mascara} no tiene el bit {BIT_FORMULA_MISMO_REGISTRO} (fuentes del mismo registro)")
    for campo, real in (("IsValidForCreate", valido_alta), ("IsValidForUpdate", valido_cambio), ("IsSecured", protegida), ("IsAuditEnabled", auditada)):
        if real is not False:
            difs.append(f"{n}.{campo}: entorno={real!r} playbook=False")
    if nivel != "None":
        difs.append(f"{n}.RequiredLevel: entorno={nivel!r} playbook='None'")
    if precision is not None and precision != c["precision"]:
        difs.append(f"{n}.Precision: entorno={precision!r} playbook={c['precision']!r}")

    usa_now = _usa(c["expresion"], "Now")
    for base in c["depende_de"]:
        g = en_entorno.get(base)
        if g is None:
            difs.append(f"{n}.depende_de: la columna {base} no existe en la tabla (una base ausente deja la fórmula inválida)")
        elif usa_now and g["_tipo"] == "DateTimeType":
            cf, filas = por_familia["DateTime"]
            if base not in filas:
                raise ErrorEntorno(f"{cf} devolvió 200 con forma inesperada: '{base}' no vino entre las columnas de esa familia")
            i, fila = filas[base]
            comportamiento = _administrada(fila.get("DateTimeBehavior"), str, cf, f"value[{i}].DateTimeBehavior")
            if comportamiento != "UserLocal":
                difs.append(f"{n}.depende_de: {base} tiene comportamiento de fecha {comportamiento!r} y la expresión usa Now(): tiene que ser 'UserLocal'")
    esperada = " ".join(c["expresion"].split())
    if definicion is None or " ".join(definicion.split()) != esperada:
        mostrada = definicion if definicion is None else " ".join(definicion.split())
        difs.append(f"{n}.FormulaDefinition: entorno={mostrada!r} playbook={esperada!r}")


def _comparar_familia(c, f, i, consulta, idiomas, difs, tabla_nombre):
    n, tipo = c["nombre"], c["tipo"]

    def igual(campo, tipo_py, esperado, permite_nulo=False, vacio_es_nulo=False):
        """`vacio_es_nulo`: la plataforma devuelve `''` donde el playbook no declara nada.

        No es un matiz: una diferencia que NO se puede resolver deja la tabla en
        `difiere` para siempre, y `--agregar-columnas` exige que las columnas
        faltantes sean la ÚNICA diferencia. O sea que un falso positivo acá vuelve
        IMPOSIBLE agregarle una columna a esa tabla con esta herramienta. Pasó el
        2026-09-23 con `AutoNumberFormat` en las seis columnas de texto de Solicitud.
        """
        real = exigir_forma(f.get(campo), tipo_py, consulta, f"value[{i}].{campo}", permite_nulo=permite_nulo)
        if vacio_es_nulo and real == "":
            real = None
        if real != esperado:
            difs.append(f"{n}.{campo}: entorno={real!r} playbook={esperado!r}")

    if tipo in ("texto", "autonumerico"):
        igual("MaxLength", int, c["largo"])
        igual("AutoNumberFormat", str, c.get("formato") or None, permite_nulo=True, vacio_es_nulo=True)
    elif tipo == "memo":
        igual("MaxLength", int, c["largo"])
    elif tipo == "entero":
        igual("MinValue", int, c["minimo"])
        igual("MaxValue", int, c["maximo"])
    elif tipo == "decimal":
        igual("Precision", int, c["precision"])
        for campo in ("MinValue", "MaxValue"):
            # Exacto, no de punto flotante: el JSON del entorno trae 999999999.99 o un 100 entero; se compara como Decimal.
            real = exigir_forma(f.get(campo), "numero", consulta, f"value[{i}].{campo}")
            if decimal.Decimal(str(real)) != c["minimo" if campo == "MinValue" else "maximo"]:
                difs.append(f"{n}.{campo}: entorno={real} playbook={c['minimo' if campo == 'MinValue' else 'maximo']}")
    elif tipo == "archivo":
        igual("MaxSizeInKB", int, c["tamano_kb"])
    elif tipo in ("fecha", "fechahora"):
        solo_fecha = tipo == "fecha"
        igual("Format", str, "DateOnly" if solo_fecha else "DateAndTime")
        real = _administrada(f.get("DateTimeBehavior"), str, consulta, f"value[{i}].DateTimeBehavior")
        if real != ("DateOnly" if solo_fecha else "UserLocal"):
            difs.append(f"{n}.DateTimeBehavior: entorno={real!r} playbook={'DateOnly' if solo_fecha else 'UserLocal'!r}")
    elif tipo == "choice":
        g = exigir_forma(f.get("GlobalOptionSet"), dict, consulta, f"value[{i}].GlobalOptionSet")
        real = exigir_forma(g.get("Name"), str, consulta, f"value[{i}].GlobalOptionSet.Name")
        if real != c["choice"]:
            difs.append(f"{n}.choice: entorno={real!r} playbook={c['choice']!r}")
    elif tipo == "sino":
        igual("DefaultValue", bool, c["defecto"])
        o = exigir_forma(f.get("OptionSet"), dict, consulta, f"value[{i}].OptionSet")
        # El conjunto de opciones local de un sí/no tiene su propio nombre visible: nace copiado del de la columna
        # y no la sigue si se la renombra; el Web API no deja cambiarlo (ensayado el 2026-09-21 por cuatro vías).
        exigir_etiqueta(o.get("DisplayName"), consulta, f"value[{i}].OptionSet.DisplayName")
        reales = textos_del_entorno(o.get("DisplayName"))
        propias = []
        comparar_etiquetas(o.get("DisplayName"), idiomas, f"{n}.OptionSet.DisplayName", c["displayname"], propias, consulta, f"value[{i}].OptionSet.DisplayName")
        if propias and not optionset_exceptuado(tabla_nombre, n, reales):
            difs.extend(f"{d} (nombre del conjunto de opciones local del sí/no: no se puede cambiar por Web API; se resuelve con el aprobador)" for d in propias)
        for clave, que, esperado in (("TrueOption", "etiqueta_si", c["etiqueta_si"]), ("FalseOption", "etiqueta_no", c["etiqueta_no"])):
            op = exigir_forma(o.get(clave), dict, consulta, f"value[{i}].OptionSet.{clave}")
            comparar_etiquetas(op.get("Label"), idiomas, f"{n}.{que}", esperado, difs, consulta, f"value[{i}].OptionSet.{clave}.Label")


# ---------------------------------------------------------------------------
def ajustar_primaria(dv, datos, identidad):
    """Reenvía la definición completa de la columna primaria con el largo, el
    nivel de requerida y la auditoría del playbook, y publica la tabla. Devuelve `None` si
    salió bien, o el texto del problema. PUT reemplaza la definición entera:
    por eso se parte de la que devuelve el entorno y se cambian solo esas dos
    propiedades. El GET con cast no trae '@odata.type' y el PUT lo exige."""
    tabla, p = datos["nombre"], datos["primaria"]
    ruta = f"EntityDefinitions(LogicalName='{tabla}')/Attributes(LogicalName='{p['nombre']}')"
    consulta = f"la lectura de la columna primaria (GET Attributes '{p['nombre']}')"
    actual = _get(dv, ruta + "/Microsoft.Dynamics.CRM.StringAttributeMetadata", consulta)
    nivel = exigir_forma(actual.get("RequiredLevel"), dict, consulta, "RequiredLevel")
    definicion = {k: v for k, v in actual.items() if k != "@odata.context"}
    definicion["@odata.type"] = "Microsoft.Dynamics.CRM.StringAttributeMetadata"
    definicion["MaxLength"] = p["largo"]
    # El nombre visible y la descripción viajan en el mismo PUT (con MSCRM.MergeLabels).
    definicion["DisplayName"] = etiqueta_web_api(p["displayname"], identidad["idiomas"])
    definicion["Description"] = etiqueta_uniforme(p["descripcion"], identidad["idiomas"])
    if p["autonumerico"]:
        # Volver autonumérica una primaria de texto que ya existe es el mismo PUT (ensayado el 2026-09-20).
        definicion["AutoNumberFormat"] = p["autonumerico"]
    definicion["RequiredLevel"] = {**nivel, "Value": "ApplicationRequired" if p["requerida"] else "None"}
    # La primaria hereda la auditoría de la tabla, y la plataforma también la ignora al crear: la deja auditada.
    aud = exigir_forma(actual.get("IsAuditEnabled"), dict, consulta, "IsAuditEnabled")
    definicion["IsAuditEnabled"] = {**aud, "Value": datos["auditoria"]}
    est, cuerpo, _ = escribir_metadatos(dv, "PUT", ruta, definicion, solucion=identidad["solucion"], cabeceras={"MSCRM.MergeLabels": "true"})
    if est != 204:
        return f"falló ajustar la columna primaria {p['nombre']}: HTTP {est} {cuerpo}"
    xml = f"<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>"
    est, cuerpo, _ = escribir_metadatos(dv, "POST", "PublishXml", {"ParameterXml": xml})
    if est != 204:
        return f"se ajustó la columna primaria {p['nombre']} pero falló publicar la tabla: HTTP {est} {cuerpo}"
    return None


def _solo_difiere_la_primaria(datos, diffs):
    """Lo único que `--corregir-primaria` acepta reparar, y solo en la primaria:
    largo, requerida, auditoría, nombre visible, descripción y,
    si el playbook la quiere autonumérica, darle su formato. Quitarle el
    formato a una columna que ya es autonumérica NO: eso no lo pide ningún
    playbook y cambiaría cómo se numeran los registros."""
    n = datos["primaria"]["nombre"]
    admitidas = [f"{n}.MaxLength:", f"{n}.RequiredLevel:", f"{n}.DisplayName[", f"{n}.Description[", f"{n}.IsAuditEnabled:"]
    if datos["primaria"]["autonumerico"]:
        admitidas.append(f"{n}.AutoNumberFormat:")
    return bool(diffs) and all(d.startswith(tuple(admitidas)) for d in diffs)


def optionset_exceptuado(tabla, columna, textos_reales):
    """Excepción aprobada para el nombre del conjunto de opciones local de un sí/no (`proyecto: excepciones_nombres.json`,
    vacío en este proyecto). La excepción es un objeto por idioma y `textos_reales` es `{LCID: texto}` tal como está en el
    entorno: solo se exceptúa si coinciden en TODOS los idiomas y no hay ninguno de más."""
    excepcion = EXCEPCIONES_NOMBRES["optionset_si_no"].get(f"{tabla}.{columna}")
    return excepcion is not None and {int(k): v for k, v in excepcion.items()} == {k: (v or "") for k, v in textos_reales.items()}


CAST_DE_TIPO = {"texto": "String", "autonumerico": "String", "memo": "Memo", "entero": "Integer", "decimal": "Decimal", "choice": "Picklist", "sino": "Boolean",
                "fecha": "DateTime", "fechahora": "DateTime", "archivo": "File"}
CAST_DE_RESULTADO = {"decimal": "Decimal", "entero": "Integer"}  # una fórmula es del tipo de su resultado


def _nombres_que_difieren(datos, diffs):
    """Si TODAS las diferencias son de nombres visibles, devuelve qué hay que
    corregir: {"tabla": bool, "columnas": [nombres], "si_no": [(columna, valor, clave)]};
    si hay cualquier otra diferencia, `None`: no se toca nada.
    Una columna de fórmula es la excepción en lo que se repara: el editor de Power Apps la deja con el nombre visible y la descripción SOLO en el
    idioma del usuario (ensayo del 2026-10-09), así que en ella también una diferencia de DESCRIPCIÓN por idioma es reparable (se corrige junto con el
    nombre, en un solo PUT). La descripción de cualquier otra columna no lo es: no la deja a medias ninguna herramienta."""
    plan = {"tabla": False, "columnas": [], "si_no": []}
    formulas = {c["nombre"] for c in datos["columnas"] if c["tipo"] == "formula"}
    columnas = [datos["primaria"]["nombre"]] + [c["nombre"] for c in datos["columnas"]]
    for d in diffs:
        que = re.sub(r"\[\d+\]$", "", d.split(": entorno=", 1)[0]) if ": entorno=" in d else None
        nombre_de_columna = que[: que.rindex(".")] if que and "." in que else None
        if que in ("DisplayName", "DisplayCollectionName"):
            plan["tabla"] = True
        elif que and que.endswith(".DisplayName") and nombre_de_columna in columnas:
            # Hay una diferencia por idioma: la columna se corrige UNA vez, con las etiquetas de todos los idiomas.
            if nombre_de_columna not in plan["columnas"]:
                plan["columnas"].append(nombre_de_columna)
        elif que and que.endswith(".Description") and nombre_de_columna in formulas:
            if nombre_de_columna not in plan["columnas"]:
                plan["columnas"].append(nombre_de_columna)
        elif que and que.rsplit(".", 1)[-1] in ("etiqueta_si", "etiqueta_no") and que.rsplit(".", 1)[0] in columnas:
            item = (que.rsplit(".", 1)[0], 1 if que.endswith("etiqueta_si") else 0, que.rsplit(".", 1)[-1])
            if item not in plan["si_no"]:
                plan["si_no"].append(item)
        else:
            return None
    return plan if diffs else None


def corregir_nombres(dv, datos, identidad, plan):
    """Ensayado contra la plataforma el 2026-09-21 con todos los tipos de
    columna: PUT de la definición completa (con '@odata.type', que el GET no
    trae) para la tabla y para cada columna; `UpdateOptionValue` para las
    etiquetas de un sí/no; y publicar. No cambia nada más. Devuelve `None` o el problema.
    Una columna de fórmula (ensayado el 2026-10-09) se lee y se reescribe con el cast de su resultado (`decimal` → Decimal, `entero` → Integer) y se le
    corrige también la `Description`: el editor la deja en un solo idioma. Lo demás de su definición (`FormulaDefinition`, `SourceType`, `Precision`) viaja
    intacto, porque el PUT parte de la definición completa que devolvió el entorno."""
    tabla, solucion, idiomas = datos["nombre"], identidad["solucion"], identidad["idiomas"]
    fusion = {"MSCRM.MergeLabels": "true"}

    def leer(ruta, que):
        est, cuerpo, _ = dv.call("GET", ruta)
        if est != 200:
            raise ErrorEntorno(f"la lectura completa de {que} devolvió HTTP {est}: {cuerpo}")
        return {k: v for k, v in exigir_forma(cuerpo, dict, f"la lectura completa de {que}", "cuerpo").items() if k != "@odata.context"}

    if plan["tabla"]:
        definicion = leer(f"EntityDefinitions(LogicalName='{tabla}')", f"la tabla {tabla}")
        definicion["@odata.type"] = "Microsoft.Dynamics.CRM.EntityMetadata"
        definicion["DisplayName"] = etiqueta_web_api(datos["displayname"], idiomas)
        definicion["DisplayCollectionName"] = etiqueta_web_api(datos["displayname_plural"], idiomas)
        est, resp, _ = escribir_metadatos(dv, "PUT", f"EntityDefinitions(LogicalName='{tabla}')", definicion, solucion=solucion, cabeceras=fusion)
        if est != 204:
            return f"falló cambiar el nombre visible de la tabla: HTTP {est} {resp}"
    por_nombre = {c["nombre"]: c for c in datos["columnas"]}
    por_nombre[datos["primaria"]["nombre"]] = {**datos["primaria"], "tipo": "texto"}
    for n in plan["columnas"]:
        c = por_nombre[n]
        es_formula = c["tipo"] == "formula"
        cast = f"Microsoft.Dynamics.CRM.{CAST_DE_RESULTADO[c['resultado']] if es_formula else CAST_DE_TIPO[c['tipo']]}AttributeMetadata"
        ruta = f"EntityDefinitions(LogicalName='{tabla}')/Attributes(LogicalName='{n}')"
        definicion = leer(f"{ruta}/{cast}", f"la columna {n}")
        definicion["@odata.type"] = cast
        definicion["DisplayName"] = etiqueta_web_api(c["displayname"], idiomas)
        if es_formula:
            definicion["Description"] = etiqueta_uniforme(c["descripcion"], idiomas)
        est, resp, _ = escribir_metadatos(dv, "PUT", ruta, definicion, solucion=solucion, cabeceras=fusion)
        if est != 204:
            return (f"falló cambiar el nombre visible y la descripción de la columna de fórmula {n}: HTTP {est} {resp}" if es_formula
                    else f"falló cambiar el nombre visible de la columna {n}: HTTP {est} {resp}")
    for n, valor, clave in plan["si_no"]:
        cuerpo = {"EntityLogicalName": tabla, "AttributeLogicalName": n, "Value": valor, "Label": etiqueta_web_api(por_nombre[n][clave], idiomas), "MergeLabels": True, "SolutionUniqueName": solucion}
        est, resp, _ = escribir_metadatos(dv, "POST", "UpdateOptionValue", cuerpo)
        if est != 204:
            return f"falló cambiar {clave} de la columna {n}: HTTP {est} {resp}"
    xml = f"<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>"
    est, resp, _ = escribir_metadatos(dv, "POST", "PublishXml", {"ParameterXml": xml})
    return None if est == 204 else f"se cambiaron los nombres pero falló publicar la tabla: HTTP {est} {resp}"


def _columnas_que_faltan(datos, diffs):
    """Lo único que `--agregar-columnas` acepta reparar: que TODAS las
    diferencias sean columnas que el playbook declara (no la primaria) y el
    entorno no tiene. Devuelve esas columnas en el orden del playbook; si hay
    cualquier otra diferencia, `None`: no se toca nada."""
    prefijo = "falta la columna "
    declaradas = {c["nombre"] for c in datos["columnas"]}
    faltan = set()
    for d in diffs:
        if not d.startswith(prefijo) or d[len(prefijo):] not in declaradas:
            return None
        faltan.add(d[len(prefijo):])
    return [c for c in datos["columnas"] if c["nombre"] in faltan] or None


def agregar_columnas(dv, datos, identidad, choices, columnas):
    """`POST …/Attributes` por cada columna, con EL MISMO cuerpo que usa la
    creación de la tabla (es la ruta que ya usan las columnas protegidas), y
    publicar una vez. Nunca borra ni cambia una columna existente. Devuelve `None` o el problema."""
    tabla, hechas = datos["nombre"], []
    for c in columnas:
        est, cuerpo, _ = escribir_metadatos(dv, "POST", f"EntityDefinitions(LogicalName='{tabla}')/Attributes",
                                            _columna_payload(c, identidad["idiomas"], choices), solucion=identidad["solucion"])
        if est != 204:
            return f"falló agregar la columna {c['nombre']}: HTTP {est} {cuerpo}; ya se agregaron: {', '.join(hechas) or 'ninguna'}. Se puede volver a correr con --agregar-columnas"
        hechas.append(c["nombre"])
    xml = f"<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>"
    est, cuerpo, _ = escribir_metadatos(dv, "POST", "PublishXml", {"ParameterXml": xml})
    return None if est == 204 else f"se agregaron las columnas ({', '.join(hechas)}) pero falló publicar la tabla: HTTP {est} {cuerpo}. Volver a correr con --publicar"


def _paso_manual_de_renombre(datos, m, idiomas):
    nombres = ", ".join(f"{i}: {m['etiqueta'][str(i)]}" for i in idiomas)
    return (f"renombrar a mano el motivo {m['valor']} (estado {m['statecode']}) de statuscode como ({nombres}): en Power Apps, Soluciones > la solución > "
            f"tabla {datos['nombre']} > Columnas > Motivo para el estado (statuscode) > Opciones")


def _escribir_motivos(dv, datos, identidad):
    """Alta de los motivos de estado, en el orden del playbook, y publicar la tabla una vez. Devuelve `None` si salió bien o
    `(estado, detalle)`. Ensayado el 2026-10-09: `UpdateOptionValue` sobre `statuscode` SÍ renombra los motivos por defecto e `InsertStatusValue` respeta el
    `Value` explícito con su `State`. Si un entorno no lo hiciera, la herramienta NO lo supone y se detiene como salvaguarda: un 4xx (la plataforma dice
    que no lo admite) o un valor asignado distinto es `bloqueado` y se dice qué pasó; un 5xx, el 429 sin resolver o cualquier otra cosa es `error`."""
    tabla, solucion, idiomas, hechos = datos["nombre"], identidad["solucion"], identidad["idiomas"], []

    def incompleta():
        return ("la tabla se creó pero quedó incompleta; la herramienta no repara una tabla existente: borrarla (está vacía) y volver a correr. "
                f"Motivos ya escritos: {', '.join(map(str, hechos)) or 'ninguno'}")

    for m in datos["motivos_estado"]:
        cuerpo = {"EntityLogicalName": tabla, "AttributeLogicalName": "statuscode", "Value": m["valor"],
                  "Label": etiqueta_web_api(m["etiqueta"], idiomas), "SolutionUniqueName": solucion}
        if m["descripcion"]:
            cuerpo["Description"] = etiqueta_uniforme(m["descripcion"], idiomas)
        agregar = m["origen"] == "agregado"
        accion = "InsertStatusValue" if agregar else "UpdateOptionValue"
        if agregar:
            cuerpo["StateCode"] = m["statecode"]
        else:
            cuerpo["MergeLabels"] = True
        est, resp, _ = escribir_metadatos(dv, "POST", accion, cuerpo)
        etiqueta = m["etiqueta"][str(idiomas[0])]
        if est == (200 if agregar else 204):
            if agregar:
                # La plataforma respeta el valor que se le manda (ensayado el 2026-10-09): si asignara otro, no se acepta (salvaguarda).
                asignado = exigir_forma(resp.get("NewOptionValue") if isinstance(resp, dict) else None, int,
                                        f"la respuesta de InsertStatusValue del motivo {m['valor']}", "NewOptionValue")
                if asignado != m["valor"]:
                    return "bloqueado", (f"InsertStatusValue asignó el valor {asignado} al motivo {etiqueta!r} y el playbook pide {m['valor']}: la plataforma respeta "
                                         "un Value explícito (ensayado el 2026-10-09), así que esto no es lo esperado; la herramienta no acepta otro valor y se detiene "
                                         f"como salvaguarda. {incompleta()}")
            hechos.append(m["valor"])
            continue
        if 400 <= est < 500 and est != 429:
            salvaguarda = ("UpdateOptionValue sobre statuscode funcionó en el ensayo del 2026-10-09 (renombró los motivos 1 y 2); este rechazo no es lo esperado y la "
                           f"herramienta se detiene como salvaguarda. Si persiste, renombrar es un paso manual: {_paso_manual_de_renombre(datos, m, idiomas)}"
                           if not agregar else "InsertStatusValue funcionó en el ensayo del 2026-10-09 (respeta el Value y el State); este rechazo no es lo esperado "
                                               "y la herramienta se detiene como salvaguarda")
            return "bloqueado", f"la plataforma rechazó {accion} sobre statuscode para el motivo {m['valor']} ({etiqueta!r}) con HTTP {est}: {resp}. {salvaguarda}. {incompleta()}"
        return "error", f"falló {accion} sobre statuscode para el motivo {m['valor']} ({etiqueta!r}): HTTP {est} {resp}. {incompleta()}"
    xml = f"<importexportxml><entities><entity>{tabla}</entity></entities></importexportxml>"
    est, resp, _ = escribir_metadatos(dv, "POST", "PublishXml", {"ParameterXml": xml})
    if est != 204:
        return "error", f"se escribieron los motivos de estado pero falló publicar la tabla: HTTP {est} {resp}. Volver a correr con --publicar"
    return None


CIERRE_DEL_PASO_MANUAL = ("después correr esta herramienta con --corregir-nombres para completar las etiquetas y la descripción en todos los idiomas "
                          "(el editor solo deja el idioma del usuario)")
CIERRE_DEL_PASO_MANUAL_DE_LA_VARIANTE = "después se vuelve a correr esta herramienta"  # la variante no admite --corregir-nombres


def _instruccion_paso_manual(datos, formulas, idiomas):
    """El paso manual de cada fórmula, literal y para una persona (`tabla.md`, «Cómo se crea: paso manual»). Cierra UNA vez, al final: el editor de Power Apps
    deja el nombre visible y la descripción solo en el idioma del usuario (ensayo del 2026-10-09), y `--corregir-nombres` los completa en todos. En una
    tabla estándar (`columnas-estandar`) esa bandera no aplica y el cierre es solo volver a correr."""
    nombre_tabla = datos.get("nombre") or datos.get("tabla")  # la variante sobre una estándar no tiene `nombre`
    pasos = []
    for c in formulas:
        nombres = "; ".join(f"{i}: {c['displayname'][str(i)]}" for i in idiomas)
        resultado = f"precisión {c['precision']}" if c["resultado"] == "decimal" else "resultado entero"
        pasos.append(
            f"{c['nombre']}: en Power Apps, Soluciones > la solución del playbook > tabla {nombre_tabla} > Columnas > Nueva columna; "
            f"tipo de datos Fórmula; nombre lógico exactamente {c['nombre']}; nombre para mostrar ({nombres}) y descripción como el playbook; "
            f"expresión: {c['expresion']}; {resultado}; guardar y publicar. Se hace después de que existan las columnas base "
            f"({', '.join(c['depende_de'])}): con una base ausente la fórmula queda inválida"
        )
    cierre = CIERRE_DEL_PASO_MANUAL if "nombre" in datos else CIERRE_DEL_PASO_MANUAL_DE_LA_VARIANTE
    return " | ".join(pasos) + f"; {cierre}"


def _con_pendientes(estado, componente, detalle, actual, datos, identidad):
    """Lo que la herramienta sabe y no puede hacer sola: una fórmula que falta es un paso manual, no un éxito. Un estado de éxito con
    una fórmula pendiente pasa a `bloqueado` (con la instrucción); `difiere` la nombra; y las notas de verificación (lo que no se
    pudo comparar) viajan en el detalle de un éxito, nunca se callan."""
    if not actual:
        return estado, componente, detalle
    pendientes = actual.get("pendientes") or []
    nombres = ", ".join(c["nombre"] for c in pendientes)
    if estado in ESTADOS_EXITO and pendientes:
        situacion = "la tabla se creó" if estado == "creado" else "la tabla existe y coincide en todo lo demás"
        return "bloqueado", componente, (f"paso manual pendiente de la fórmula {nombres}: {situacion} ({detalle}), pero la herramienta no crea columnas de "
                                         f"fórmula (Microsoft no lo soporta por código). {_instruccion_paso_manual(datos, pendientes, identidad['idiomas'])}")
    if estado == "difiere" and pendientes:
        return estado, componente, f"{detalle}; además falta el paso manual de la fórmula {nombres}"
    if estado in ESTADOS_EXITO and actual.get("notas"):
        return estado, componente, f"{detalle}. Sin comparar: {' | '.join(actual['notas'])}"
    return estado, componente, detalle


def _contra_entorno(dv, datos, identidad, solo_verificar, componente, corregir_primaria=False, publicar=False, corregir=False, agregar=False):
    contexto = {}
    estado, comp, detalle = _flujo(dv, datos, identidad, solo_verificar, componente, contexto, corregir_primaria, publicar, corregir, agregar)
    return _con_pendientes(estado, comp, detalle, contexto.get("actual"), datos, identidad)


def _flujo(dv, datos, identidad, solo_verificar, componente, contexto, corregir_primaria=False, publicar=False, corregir=False, agregar=False):
    solucion = identidad["solucion"]
    solution_id = comprobar_solucion_e_idioma(dv, identidad)
    choices = comprobar_choices(dv, datos)

    def ver():
        contexto["actual"] = _verificar(dv, datos, identidad, solution_id)
        return contexto["actual"]

    actual = ver()

    if solo_verificar and not actual["existe"]:
        return "error", componente, "la tabla no existe en el entorno; --solo-verificar no crea nada, correr la herramienta sin ese flag primero"
    if actual["existe"] and corregir and not solo_verificar:
        plan = _nombres_que_difieren(datos, actual["diffs"])
        if plan:
            problema = corregir_nombres(dv, datos, identidad, plan)
            if problema:
                return "error", componente, problema
            actual = ver()
            if not actual["diffs"]:
                cuantos = int(plan["tabla"]) + len(plan["columnas"]) + len(plan["si_no"])
                return "ya_existia", componente, (f"MetadataId {actual['metadata_id']}; la tabla existía y solo diferían nombres visibles (o la descripción de una fórmula); se corrigieron {cuantos} "
                                                  f"y ahora coincide en todo; pertenece a '{solucion}'")
    if actual["existe"] and agregar and not solo_verificar:
        faltan = _columnas_que_faltan(datos, actual["diffs"])
        if faltan:
            problema = agregar_columnas(dv, datos, identidad, choices, faltan)
            if problema:
                return "error", componente, problema
            actual = ver()
            if actual["diffs"]:
                return "error", componente, f"se agregaron las columnas pero no coincide con el playbook al releer: {'; '.join(actual['diffs'])}"
            nombres = ", ".join(c["nombre"] for c in faltan)
            return "ya_existia", componente, (f"MetadataId {actual['metadata_id']}; la tabla existía y solo le faltaban columnas; se agregaron {len(faltan)} columnas "
                                              f"({nombres}) y ahora coincide en todo; pertenece a '{solucion}'")
    if actual["existe"] and corregir_primaria and not solo_verificar and _solo_difiere_la_primaria(datos, actual["diffs"]):
        problema = ajustar_primaria(dv, datos, identidad)
        if problema:
            return "error", componente, problema
        actual = ver()
        if not actual["diffs"]:
            return "ya_existia", componente, (
                "la tabla existía y su única diferencia era la columna primaria, que la plataforma había creado con sus valores "
                f"por defecto o como texto común; se corrigió (largo, requerida o formato autonumérico) y ahora coincide en todo; pertenece a '{solucion}'"
            )
    if actual["existe"] and publicar and not solo_verificar and not actual["diffs"]:
        # Publicar es inofensivo y se puede repetir: sirve cuando una corrida anterior ajustó la primaria y el publicar falló.
        xml = f"<importexportxml><entities><entity>{datos['nombre']}</entity></entities></importexportxml>"
        est, cuerpo, _ = escribir_metadatos(dv, "POST", "PublishXml", {"ParameterXml": xml})
        if est != 204:
            return "error", componente, f"la tabla coincide con el playbook pero falló publicarla: HTTP {est} {cuerpo}"
        return "ya_existia", componente, f"MetadataId {actual['metadata_id']}; coincide en todo lo que exige la receta y pertenece a '{solucion}'; se publicó de nuevo y no se modificó nada más"
    if actual["existe"]:
        if actual["diffs"]:
            return "difiere", componente, "; ".join(actual["diffs"])
        return "ya_existia", componente, f"MetadataId {actual['metadata_id']}; coincide en todo lo que exige la receta y pertenece a '{solucion}'; no se modificó nada"

    est, cuerpo, _ = escribir_metadatos(dv, "POST", "EntityDefinitions", construir_payload(datos, identidad, choices), solucion=solucion)
    if est != 204:
        return "error", componente, f"la creación falló: HTTP {est} {cuerpo}"
    for extra in columnas_protegidas_payload(datos, identidad, choices):
        est, cuerpo, _ = escribir_metadatos(dv, "POST", f"EntityDefinitions(LogicalName='{datos['nombre']}')/Attributes", extra, solucion=solucion)
        if est != 204:
            return (
                "error",
                componente,
                f"la tabla se creó pero quedó incompleta: falló agregar la columna protegida {extra['SchemaName']}: HTTP {est} {cuerpo}. "
                "La herramienta no repara una tabla existente: borrar la tabla (está vacía) y volver a correr",
            )
    if not datos["primaria"]["autonumerico"]:
        problema = ajustar_primaria(dv, datos, identidad)
        if problema:
            return "error", componente, f"la tabla se creó pero quedó incompleta: {problema}. Volver a correr con --corregir-primaria; si la verificación ya coincide y solo faltó publicar, con --publicar"
    if datos.get("motivos_estado") is not None:
        falla = _escribir_motivos(dv, datos, identidad)
        if falla:
            return falla[0], componente, falla[1]
    final = ver()
    if not final["existe"]:
        return "error", componente, "se creó (204) pero no aparece al releer del entorno"
    if final["diffs"]:
        return "error", componente, f"se creó pero no coincide con el playbook al releer: {'; '.join(final['diffs'])}"
    return "creado", componente, f"MetadataId {final['metadata_id']}, con {1 + len(datos['columnas'])} columnas, en la solución '{solucion}'"


def _construir_variante(ruta_playbook, solo_verificar, fabrica_cliente, verificadas, permitir_no_verificadas, reparaciones):
    """El playbook `columnas-estandar` lo resuelve su módulo; se importa acá (y no arriba) porque ese módulo usa las piezas de este."""
    import columnas_estandar

    return columnas_estandar.construir(ruta_playbook, solo_verificar, fabrica_cliente, verificadas, permitir_no_verificadas, reparaciones)


def construir(ruta_playbook, solo_verificar, fabrica_cliente, verificadas=VERIFICADAS_EN_PLATAFORMA, permitir_no_verificadas=False,
              corregir_primaria=False, publicar=False, corregir_nombres=False, agregar_columnas=False):
    """Nunca lanza: siempre devuelve `(estado, componente, detalle)`."""
    componente = os.path.basename(ruta_playbook)
    paso = "leer el playbook"
    try:
        secciones = dividir_secciones(leer_texto(ruta_playbook))
        paso = "leer el bloque '## 2. Qué se crea'"
        if obtener_bloque_json(secciones, "2", "Qué se crea").get("tipo") == TIPO_VARIANTE:
            return _construir_variante(ruta_playbook, solo_verificar, fabrica_cliente, verificadas, permitir_no_verificadas,
                                       {"--corregir-primaria": corregir_primaria, "--publicar": publicar, "--corregir-nombres": corregir_nombres,
                                        "--agregar-columnas": agregar_columnas})
        datos = obtener_componente(secciones, TIPO)
        if isinstance(datos.get("nombre"), str):
            componente = datos["nombre"]
        paso = "leer el bloque '## 1. Identidad'"
        identidad = obtener_identidad(secciones)
        paso = "validar el bloque de la tabla"
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

    print(f"Playbook leído y validado (offline). tipo={TIPO} nombre={componente} columnas={1 + len(datos['columnas'])}")
    try:
        dv = fabrica_cliente()
    except Exception:
        return "error", componente, MENSAJE_FALLO_CLIENTE

    rastro = Rastro(dv)
    try:
        return _contra_entorno(rastro, datos, identidad, solo_verificar, componente, corregir_primaria, publicar, corregir=corregir_nombres, agregar=agregar_columnas)
    except Bloqueado as e:
        return "bloqueado", componente, str(e)
    except ErrorEntorno as e:
        return "error", componente, str(e)
    except Exception as e:
        return "error", componente, f"fallo inesperado hablando con Dataverse, durante {rastro.en_curso}: {type(e).__name__}: {e}"


BANDERAS = ("--solo-verificar", "--permitir-no-verificadas", "--corregir-primaria", "--publicar",
            "--corregir-nombres", "--agregar-columnas")
USO = "uso incorrecto: tabla.py <playbook.md> [" + "] [".join(BANDERAS) + "]"


def main():
    argv = sys.argv[1:]
    rutas = [a for a in argv if not a.startswith("--")]
    # Una bandera que la herramienta no conoce es un ERROR, no un encogimiento de hombros.
    # El 2026-09-25 se le pasó `--completar` —que existe en rol.py y en datos.py, no acá— y siguió
    # como si nada, devolviendo `difiere`: parecía que se negaba a agregar columnas cuando en
    # realidad estaba ignorando la instrucción. Misma familia que el `ya_existia` mentiroso del
    # ribbon: una herramienta que calla lo que no entendió es una herramienta en la que no se
    # puede confiar, y todo el método de este proyecto se apoya en confiar en ellas (D-56).
    desconocidas = sorted({a for a in argv if a.startswith("--")} - set(BANDERAS))
    if desconocidas:
        return salida("error", "desconocido", f"bandera(s) que esta herramienta no conoce: {desconocidas}. " + USO)
    if len(rutas) != 1:
        return salida("error", "desconocido", USO)
    from dataverse_api import Dataverse

    estado, componente, detalle = construir(rutas[0], "--solo-verificar" in argv, Dataverse,
                                            permitir_no_verificadas="--permitir-no-verificadas" in argv,
                                            corregir_primaria="--corregir-primaria" in argv, publicar="--publicar" in argv,
                                            corregir_nombres="--corregir-nombres" in argv, agregar_columnas="--agregar-columnas" in argv)
    return salida(estado, componente, detalle)


if __name__ == "__main__":
    sys.exit(main())
